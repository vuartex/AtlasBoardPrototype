/**
 * AtlasBoard Phase 16I | Local measured discovery and active-match read load
 *
 * Runs ONLY against the three AtlasBoard localhost Firebase emulators.
 * Does NOT deploy, alter source, change production data or benchmark the Internet.
 *
 * From Backend/Firebase/functions (after starting the emulators once):
 *   node scripts/phase16i-capacity-load.mjs
 *   node scripts/phase16i-capacity-load.mjs --rooms=120 --matches=3 --concurrency=2,4,8 --requests=5
 *   node scripts/phase16i-capacity-load.mjs --self-test
 *   node scripts/phase16i-capacity-load.mjs --help
 *
 * Measurement boundary:
 * - synthetic discovery projections measure the REAL cursor callable, but do not
 *   represent concurrent live hosts or actual hosted games;
 * - real Firebase callables bootstrap each two-human private match, so the match
 *   read results cover actual authoritative match snapshots, not fake documents;
 * - no writes/intents are issued during measured request rounds; setup / heartbeat
 *   refresh / cleanup calls are recorded separately;
 * - queryCount / scannedDocuments are FUNCTION diagnostics, NOT billed reads;
 * - localhost latencies are NOT cloud, Steam, Internet or player capacity claims.
 */
import {createHmac, randomBytes} from "node:crypto";
import {writeFile} from "node:fs/promises";
import {tmpdir} from "node:os";
import {join} from "node:path";
import {performance} from "node:perf_hooks";

const PROJECT = "atlasboard-usa";
const REGION = "europe-west1";
const HOST = "127.0.0.1";
const PORTS = {auth: 9099, firestore: 8080, functions: 5001, hub: 4400};
const BASE = {
  auth: `http://${HOST}:${PORTS.auth}`,
  firestore: `http://${HOST}:${PORTS.firestore}`,
  functions: `http://${HOST}:${PORTS.functions}`,
  hub: `http://${HOST}:${PORTS.hub}`,
};
const API_KEY = "atlasboard-local-emulator-only";
const APP_CHECK_PLACEHOLDER = "phase16i-local-emulator-only";
const JOIN_PEPPER = "atlasboard-local-emulator-lobby-code-pepper-v1";
const REQUEST_TIMEOUT_MS = 20000;
const runId = `${Date.now().toString(36)}${randomBytes(3).toString("hex")}`;

const state = {
  people: [],
  rooms: [],
  matches: [],
  setupWrites: 0,
  heartbeatWrites: 0,
  cleanupWrites: 0,
  setupCallables: 0,
  warmups: 0,
  stopped: false,
};

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function parseOptions(argv) {
  const defaults = {rooms: 80, matches: 2, concurrency: [2, 4, 8], requests: 4, pageSize: 10};
  const options = {...defaults};
  if (argv.includes("--help") || argv.includes("-h")) return {help: true};
  if (argv.includes("--self-test")) return {selfTest: true};
  for (const arg of argv) {
    const match = /^--(rooms|matches|concurrency|requests|page-size)=(.+)$/.exec(arg);
    if (!match) throw new Error(`Unknown option: ${arg}`);
    const [, key, raw] = match;
    if (key === "concurrency") {
      options.concurrency = raw.split(",").map(Number);
    } else {
      options[key === "page-size" ? "pageSize" : key] = Number(raw);
    }
  }
  const intIn = (value, min, max) => Number.isSafeInteger(value) && value >= min && value <= max;
  assert(intIn(options.rooms, 20, 150), "--rooms must be an integer in 20..150.");
  assert(intIn(options.matches, 1, 4), "--matches must be an integer in 1..4.");
  assert(intIn(options.requests, 1, 12), "--requests must be an integer in 1..12.");
  assert(intIn(options.pageSize, 1, 20), "--page-size must be an integer in 1..20.");
  assert(options.concurrency.length >= 1 && options.concurrency.length <= 5 &&
    options.concurrency.every((v) => intIn(v, 1, 16)),
  "--concurrency needs 1..5 comma-separated integer values, each 1..16.");
  assert(new Set(options.concurrency).size === options.concurrency.length,
    "--concurrency contains duplicates.");
  assert(options.concurrency.reduce((a, b) => a + b, 0) * options.requests <= 500,
    "Request budget exceeds hard limit of 500 attempts per workload.");
  return options;
}

function validateLocalUrl(url, service) {
  const parsed = new URL(url);
  assert(parsed.protocol === "http:" && parsed.hostname === HOST &&
    parsed.port === String(PORTS[service]) && !parsed.username && !parsed.password,
  `Refusing non-local ${service} destination: ${url}`);
  if (service === "functions") {
    assert(parsed.pathname.startsWith(`/${PROJECT}/${REGION}/`), "Invalid callable project/region.");
  } else if (service === "firestore") {
    assert(parsed.pathname.startsWith(`/v1/projects/${PROJECT}/databases/(default)/documents/`),
      "Invalid Firestore project path.");
  }
}

async function httpJson(url, service, {method = "GET", headers = {}, body} = {}) {
  validateLocalUrl(url, service);
  const started = performance.now();
  try {
    const response = await fetch(url, {
      method,
      headers,
      ...(body === undefined ? {} : {body: JSON.stringify(body)}),
      signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
    });
    const text = await response.text();
    let json;
    try { json = text ? JSON.parse(text) : null; } catch { json = null; }
    return {ok: response.ok, status: response.status, json, ms: performance.now() - started,
      errorKey: json?.error?.details?.errorKey ?? json?.error?.status ?? null};
  } catch (error) {
    return {ok: false, status: 0, json: null, ms: performance.now() - started,
      errorKey: error?.name === "TimeoutError" ? "TIMEOUT" : "NETWORK_ERROR"};
  }
}

function callableUrl(name) {
  assert(/^[A-Za-z][A-Za-z0-9]*$/.test(name), "Invalid callable name.");
  return `${BASE.functions}/${PROJECT}/${REGION}/${name}`;
}

async function callable(name, person, data, countSetup = false) {
  assert(typeof person?.token === "string" && person.token.length > 20,
    "Missing real Auth Emulator Firebase ID token.");
  if (countSetup) state.setupCallables++;
  return httpJson(callableUrl(name), "functions", {
    method: "POST",
    headers: {"Content-Type": "application/json", Authorization: `Bearer ${person.token}`,
      "X-Firebase-AppCheck": APP_CHECK_PLACEHOLDER},
    body: {data},
  });
}

function payload(call) { return call.json?.result ?? call.json?.data; }

function docUrl(path, query = "") {
  assert(/^[a-z0-9_/-]+$/i.test(path), "Invalid emulator document path.");
  return `${BASE.firestore}/v1/projects/${PROJECT}/databases/(default)/documents/${path}${query}`;
}

async function adminDoc(path, method, fields, updateMask = "") {
  const result = await httpJson(docUrl(path, updateMask), "firestore", {
    method,
    headers: {Authorization: "Bearer owner", ...(fields ? {"Content-Type": "application/json"} : {})},
    ...(fields ? {body: {fields}} : {}),
  });
  return result;
}

async function preflight() {
  const hubs = await httpJson(`${BASE.hub}/emulators`, "hub");
  assert(hubs.ok, "Emulator Hub is unavailable. Start Auth/Firestore/Functions ONCE first.");
  for (const [service, port] of Object.entries(PORTS)) {
    if (service === "hub") continue;
    const entry = hubs.json?.[service];
    assert(entry && Number(entry.port) === port &&
      [HOST, "localhost", "::1"].includes(entry.host),
    `Wrong ${service} emulator (expected loopback port ${port}).`);
  }
  console.log("PASS Strict emulator-only safety preflight: atlasboard-usa, 127.0.0.1.");
}

async function createPerson(label) {
  const email = `atlasboard.load.${runId}.${label}@example.com`;
  const password = `AtlasBoardLoad!${runId}!${label}`;
  const auth = await httpJson(`${BASE.auth}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=${API_KEY}`,
    "auth", {method: "POST", headers: {"Content-Type": "application/json"},
      body: {email, password, returnSecureToken: true}});
  assert(auth.ok && auth.json?.idToken && auth.json?.localId,
    `Auth Emulator user creation failed: ${auth.status} ${auth.errorKey ?? ""}`);
  const person = {uid: auth.json.localId, token: auth.json.idToken};
  state.people.push(person);
  const user = await adminDoc(`users/${person.uid}`, "PATCH", {
    accountStatus: {stringValue: "active"}, membershipTier: {stringValue: "normal"},
    schemaVersion: {integerValue: "1"},
  });
  assert(user.ok, `Profile account seed failed: ${user.status} ${user.errorKey ?? ""}`);
  state.setupWrites++;
  const profile = await adminDoc(`public_profiles/${person.uid}`, "PATCH", {
    displayName: {stringValue: `Load ${label}`}, avatarId: {stringValue: ""},
    profileFrameId: {stringValue: ""}, schemaVersion: {integerValue: "1"},
  });
  assert(profile.ok, `Public profile seed failed: ${profile.status} ${profile.errorKey ?? ""}`);
  state.setupWrites++;
  return person;
}

function discoveryFields(id, now, versions, i) {
  return {
    schemaVersion: {integerValue: "1"}, lobbyId: {stringValue: id},
    visibility: {stringValue: "public"}, lifecycleState: {stringValue: "waiting"},
    joinable: {booleanValue: true},
    hostDisplayName: {stringValue: `Load ${String(i).padStart(3, "0")}`},
    mapId: {stringValue: "Turkey"}, themeId: {stringValue: "Classic Table"},
    roundLimit: {integerValue: "20"}, maxPlayers: {integerValue: "4"},
    occupiedPlayers: {integerValue: "1"}, openOnlineSeatCount: {integerValue: "3"},
    regionId: {stringValue: versions.regionId},
    gameVersion: {stringValue: versions.gameVersion},
    protocolVersion: {integerValue: "1"}, rulesVersion: {integerValue: "1"},
    contentVersion: {stringValue: versions.contentVersion},
    settingsRevision: {integerValue: "1"}, hasPassword: {booleanValue: false},
    createdAtEpochMs: {integerValue: String(now - i)},
    hostHeartbeatAtEpochMs: {integerValue: String(now)},
    updatedAtEpochMs: {integerValue: String(now)},
  };
}

async function runBatches(items, width, fn, ignoreInterrupt = false) {
  const errors = [];
  for (let i = 0; i < items.length; i += width) {
    const batch = await Promise.all(items.slice(i, i + width).map(async (item) => {
      try { await fn(item); } catch (error) { errors.push(error); }
    }));
    void batch;
    if (errors.length) throw errors[0];
    if (state.stopped && !ignoreInterrupt) throw new Error("Interrupted by user; cleanup requested.");
  }
}

async function seedDiscovery(rooms, versions) {
  console.log(`Seeding ${rooms} synthetic discovery projections (NOT real active rooms)...`);
  const now = Date.now();
  const ids = Array.from({length: rooms}, (_, i) => `phase16i_load_${runId}_${String(i).padStart(4, "0")}`);
  // Add IDs to cleanup registry BEFORE writing; failed/interrupted seeding stays reversible.
  state.rooms.push(...ids);
  await runBatches(ids.map((id, i) => ({id, i})), 8, async ({id, i}) => {
    const response = await adminDoc(`lobby_discovery/${id}`, "PATCH",
      discoveryFields(id, now, versions, i));
    assert(response.ok, `Discovery seed failed (${response.status}): ${id}`);
    state.setupWrites++;
  });
}

async function refreshDiscoveryHeartbeat() {
  const now = Date.now();
  await runBatches(state.rooms, 8, async (id) => {
    const response = await adminDoc(`lobby_discovery/${id}`, "PATCH",
      {hostHeartbeatAtEpochMs: {integerValue: String(now)}},
      "?updateMask.fieldPaths=hostHeartbeatAtEpochMs");
    assert(response.ok, `Heartbeat refresh failed: ${response.status} ${id}`);
    state.heartbeatWrites++;
  });
}

const baseSettings = {
  mapId: "Turkey", themeId: "Classic Table", roundLimit: 20,
  maxPlayers: 2, balancedDevelopment: true, doublesEnabled: true,
  tripleDoublePenaltyEnabled: true,
};

async function createActiveMatches(count, versions) {
  console.log(`Bootstrapping ${count} real two-human private matches via callables...`);
  for (let index = 0; index < count; index++) {
    const host = await createPerson(`host${index}`);
    const guest = await createPerson(`guest${index}`);
    const create = await callable("lobbyCreatePrivateRoom", host,
      {...baseSettings, ...versions}, true);
    assert(create.ok, `Match ${index} private lobby create failed: ${create.status} ${create.errorKey}`);
    const created = payload(create);
    const lobbyId = created?.snapshot?.lobbyId;
    const roomCode = created?.roomCode;
    assert(lobbyId && roomCode, `Match ${index} missing room identifiers.`);
    const entry = {host, guest, lobbyId, roomCode, matchId: ""};
    state.matches.push(entry);
    const joined = await callable("lobbyJoinByCode", guest,
      {roomCode, password: "", idempotencyKey: `atlas-load-guest-${runId}-${index}`, ...versions}, true);
    assert(joined.ok, `Match ${index} guest join failed: ${joined.status} ${joined.errorKey}`);
    const rev = payload(joined)?.snapshot?.settingsRevision;
    assert(Number.isSafeInteger(rev), "Guest join missing settings revision.");
    const ready = await callable("lobbySetReady", guest,
      {lobbyId, expectedSettingsRevision: rev, ready: true}, true);
    assert(ready.ok, `Match ${index} guest ready failed: ${ready.status} ${ready.errorKey}`);
    const start = await callable("lobbyStartMatch", host,
      {lobbyId, expectedSettingsRevision: payload(ready)?.snapshot?.settingsRevision}, true);
    assert(start.ok, `Match ${index} start failed: ${start.status} ${start.errorKey}`);
    entry.matchId = payload(start)?.snapshot?.matchId ?? "";
    assert(entry.matchId, `Match ${index} missing authoritative matchId.`);
  }
  console.log(`PASS ${state.matches.length} real matches started (no fake match documents).`);
}

function summarize(observations, seconds) {
  const sorted = observations.filter((x) => x.ok).map((x) => x.ms).sort((a, b) => a - b);
  const p = (fraction) => sorted.length ? Math.round(sorted[Math.ceil(sorted.length * fraction) - 1] * 10) / 10 : null;
  const failed = observations.filter((x) => !x.ok);
  const diagnostics = observations.filter((x) => x.ok && x.diagnostics).map((x) => x.diagnostics);
  const callableAttempts = observations.reduce((n, x) => n + (x.callableAttempts ?? 1), 0);
  const sum = (key) => diagnostics.reduce((total, x) => total + (Number(x?.[key]) || 0), 0);
  return {
    attempts: observations.length,
    successes: sorted.length,
    failures: failed.length,
    errorRatePercent: observations.length ? +((failed.length / observations.length) * 100).toFixed(2) : 0,
    wallSeconds: +seconds.toFixed(3),
    callableAttempts,
    throughputWorkflowsPerSecond: seconds ? +(observations.length / seconds).toFixed(2) : 0,
    throughputCallableRequestsPerSecond: seconds ? +(callableAttempts / seconds).toFixed(2) : 0,
    latencyMs: {p50: p(0.5), p95: p(0.95), p99: p(0.99), max: sorted.length ? +sorted.at(-1).toFixed(1) : null},
    failureTypes: [...new Set(failed.map((x) => `${x.status}:${x.errorKey ?? "UNKNOWN"}`))].slice(0, 8),
    discoveryDiagnostics: diagnostics.length ? {
      samples: diagnostics.length,
      observedFirestoreQueryCalls: sum("queryCount"),
      scannedDocuments: sum("scannedDocuments"),
      averageScannedPerDiscoveryCall: +(sum("scannedDocuments") / diagnostics.length).toFixed(2),
      scanLimitReachedCalls: diagnostics.filter((x) => x.scanLimitReached === true).length,
    } : null,
  };
}

async function measuredDiscovery(person, versions, pageSize, useCursor) {
  const first = await callable("lobbyListPublicRoomsPage", person,
    {...versions, limit: pageSize, cursor: ""});
  if (!first.ok) return {ok: false, ms: first.ms, status: first.status, errorKey: first.errorKey, callableAttempts: 1};
  const response = payload(first);
  if (!Array.isArray(response?.rooms)) return {ok: false, ms: first.ms, status: 200, errorKey: "BAD_SHAPE", callableAttempts: 1};
  if (response.rooms.length === 0) return {ok: false, ms: first.ms, status: 200, errorKey: "NO_COMPATIBLE_ROOMS", callableAttempts: 1};
  if (!useCursor || !response.hasMore || !response.nextCursor) {
    return {ok: true, ms: first.ms, diagnostics: response.diagnostics, status: 200, callableAttempts: 1};
  }
  const second = await callable("lobbyListPublicRoomsPage", person,
    {...versions, limit: pageSize, cursor: response.nextCursor});
  if (!second.ok) return {ok: false, ms: first.ms + second.ms,
    status: second.status, errorKey: second.errorKey, callableAttempts: 2};
  const page2 = payload(second);
  const before = new Set(response.rooms.map((x) => x.lobbyId));
  if (!Array.isArray(page2?.rooms) || page2.rooms.some((x) => before.has(x.lobbyId))) {
    return {ok: false, ms: first.ms + second.ms, status: 200, errorKey: "PAGINATION_DUPLICATE_OR_SHAPE", callableAttempts: 2};
  }
  return {ok: true, ms: first.ms + second.ms, status: 200, callableAttempts: 2,
    diagnostics: {
      scannedDocuments: (response.diagnostics?.scannedDocuments ?? 0) + (page2.diagnostics?.scannedDocuments ?? 0),
      queryCount: (response.diagnostics?.queryCount ?? 0) + (page2.diagnostics?.queryCount ?? 0),
      scanLimitReached: Boolean(response.diagnostics?.scanLimitReached || page2.diagnostics?.scanLimitReached),
    }};
}

async function measuredMatch(entry, alternateGuest) {
  const person = alternateGuest ? entry.guest : entry.host;
  const read = await callable("matchGetSnapshot", person, {matchId: entry.matchId});
  return {ok: read.ok && Boolean(payload(read)), ms: read.ms, status: read.status, callableAttempts: 1,
    errorKey: read.ok && !payload(read) ? "MISSING_MATCH_RESPONSE" : read.errorKey};
}

async function runStage(kind, clients, requestsPerClient, browser, versions, pageSize) {
  const observations = [];
  const started = performance.now();
  await Promise.all(Array.from({length: clients}, (_, worker) => (async () => {
    for (let req = 0; req < requestsPerClient && !state.stopped; req++) {
      let observation;
      if (kind === "discovery") {
        observation = await measuredDiscovery(browser, versions, pageSize, req % 2 === 1);
      } else if (kind === "match") {
        observation = await measuredMatch(state.matches[worker % state.matches.length], req % 2 === 1);
      } else {
        observation = (worker + req) % 2 === 0 ?
          await measuredDiscovery(browser, versions, pageSize, req % 2 === 1) :
          await measuredMatch(state.matches[worker % state.matches.length], req % 2 === 1);
      }
      observations.push(observation);
    }
  })()));
  const seconds = (performance.now() - started) / 1000;
  const metrics = summarize(observations, seconds);
  const description = `${kind.padEnd(9)} c=${String(clients).padStart(2)}  ` +
    `success=${metrics.successes}/${metrics.attempts}  p50=${metrics.latencyMs.p50}ms  ` +
    `p95=${metrics.latencyMs.p95}ms  workflows=${metrics.throughputWorkflowsPerSecond}/s  ` +
    `callables=${metrics.throughputCallableRequestsPerSecond}/s`;
  console.log(description);
  if (metrics.failures) console.warn(`  ERRORS: ${metrics.failureTypes.join(", ")}`);
  return {kind, clients, requestsPerClient, ...metrics};
}

function hashRoomCode(code) {
  return createHmac("sha256", JOIN_PEPPER)
    .update(`atlasboard:lobby-code:${code}`, "utf8").digest("hex");
}

async function cleanup() {
  console.log("Cleanup: removing ONLY this run's synthetic data and test accounts...");
  const safeDelete = async (path) => {
    try {
      const result = await adminDoc(path, "DELETE");
      if (result.ok || result.status === 404) state.cleanupWrites++;
      else console.warn(`Cleanup warning: ${path} HTTP ${result.status}`);
    } catch (error) { console.warn(`Cleanup warning: ${path}`, error.message); }
  };
  for (const item of state.matches) {
    if (item.matchId) {
      for (let seat = 1; seat <= 4; seat++) await safeDelete(`matches/${item.matchId}/seats/seat_${seat}`);
      await safeDelete(`matches/${item.matchId}`);
    }
    if (item.lobbyId) {
      for (let seat = 1; seat <= 4; seat++) await safeDelete(`lobbies/${item.lobbyId}/members/seat_${seat}`);
      await safeDelete(`lobby_discovery/${item.lobbyId}`);
      await safeDelete(`lobbies/${item.lobbyId}`);
    }
    if (item.roomCode) await safeDelete(`join_codes/${hashRoomCode(item.roomCode)}`);
  }
  await runBatches(state.rooms, 8, async (id) => safeDelete(`lobby_discovery/${id}`)).catch((e) =>
    console.warn("Synthetic cleanup warning:", e.message));
  for (const person of state.people) {
    await safeDelete(`public_profiles/${person.uid}`);
    await safeDelete(`users/${person.uid}`);
    try {
      await httpJson(`${BASE.auth}/identitytoolkit.googleapis.com/v1/accounts:delete?key=${API_KEY}`,
        "auth", {method: "POST", headers: {"Content-Type": "application/json"},
          body: {idToken: person.token}});
    } catch (error) { console.warn("Auth cleanup warning:", error.message); }
  }
  console.log("Cleanup completed (review any warnings above).");
}

function showHelp() {
  console.log(`AtlasBoard Phase 16I local load / measurement
  --rooms=80             Synthetic browser projection count (20..150)
  --matches=2           Real two-human started matches (1..4)
  --concurrency=2,4,8   Parallel simulated callers, 1..16 per stage
  --requests=4          Requests per simulated caller and stage (1..12)
  --page-size=10        Browser page size (1..20)
  --self-test           Offline stats / argument checks, no emulator access

Requires existing local Auth/Firestore/Functions emulator process started ONCE.
Do not run alongside node scripts/qa.mjs --full. Writes only temporary test documents
in the local Firestore emulator. Structured measurement JSON is written to OS TEMP.
`);
}

function selfTest() {
  const a = summarize([{ok: true, ms: 10}, {ok: true, ms: 20},
    {ok: false, ms: 30, status: 400, errorKey: "X"}], 1);
  assert(a.successes === 2 && a.failures === 1 && a.latencyMs.p95 === 20,
    "Percentile/stat accounting self-test failed.");
  assert(parseOptions(["--rooms=40", "--matches=1", "--concurrency=2,8"]).rooms === 40,
    "CLI self-test failed.");
  let rejected = false;
  try { validateLocalUrl("https://example.com/hack", "functions"); } catch { rejected = true; }
  assert(rejected, "External-host fail-closed check failed.");
  console.log("PASS Phase16I load tool offline self-test: statistics, CLI, URL safety.");
}

let report;
let failed = false;
let shouldCleanup = false;
try {
  const options = parseOptions(process.argv.slice(2));
  if (options.help) {
    showHelp();
  } else if (options.selfTest) {
    selfTest();
  } else {
    process.on("SIGINT", () => {
      state.stopped = true;
      console.warn("CTRL+C received: finishing current requests, then cleaning test data...");
    });
    console.log("AtlasBoard Phase16I LOCAL capacity/load baseline v1");
    console.log("Scope: Firestore emulator synthetic room projections + actual callable-started match reads.");
    console.log("This is NOT production user capacity or a Firebase billing estimate.");
    console.log(`Configuration: ${JSON.stringify(options)}`);
    await preflight();
    shouldCleanup = true;
    const versions = {
      gameVersion: `phase16i-load-${runId}`, protocolVersion: 1, rulesVersion: 1,
      contentVersion: "1", regionId: "auto",
    };
    const browser = await createPerson("browser");
    await seedDiscovery(options.rooms, versions);
    await createActiveMatches(options.matches, versions);
    const warmup = await callable("lobbyListPublicRoomsPage", browser,
      {...versions, limit: options.pageSize, cursor: ""});
    state.warmups++;
    assert(warmup.ok && payload(warmup)?.rooms?.length > 0,
      `Page-callable warmup failed: ${warmup.status} ${warmup.errorKey}`);
    for (const entry of state.matches) {
      const warm = await callable("matchGetSnapshot", entry.host, {matchId: entry.matchId});
      state.warmups++;
      assert(warm.ok && payload(warm), `Match warmup failed: ${warm.status} ${warm.errorKey}`);
    }
    const stages = [];
    for (const kind of ["discovery", "match", "mixed"]) {
      for (const clients of options.concurrency) {
        if (state.stopped) break;
        // Real host heartbeats would refresh these. This synthetic fixture
        // refresh happens outside the timer so stale-room tests aren't conflated
        // with read load. Count separately; not a live-host simulation.
        await refreshDiscoveryHeartbeat();
        stages.push(await runStage(kind, clients, options.requests,
          browser, versions, options.pageSize));
      }
    }
    report = {
      label: "AtlasBoard Phase16I LOCAL capacity-load baseline",
      timestampUtc: new Date().toISOString(),
      runId, project: PROJECT, environment: "EMULATOR_ONLY_LOCALHOST",
      options,
      scenario: {
        syntheticDiscoveryDocuments: state.rooms.length,
        startedTwoHumanMatchSnapshots: state.matches.length,
        notSimulated: "In-game active intents / host state publication / network jitter / dedicated physical clients",
        paginationRequestsMayUseTwoCallables: true,
        pageReadDiagnosticsNotEqualBilledReads: true,
      },
      setup: {emulatorFirestoreDocumentWrites: state.setupWrites,
        heartbeatRefreshWrites: state.heartbeatWrites,
        setupCallables: state.setupCallables, warmupCallables: state.warmups},
      stages,
      limitations: [
        "Synthetic public projections are not real concurrent hosted lobbies.",
        "Match snapshots are created by real two-human Start flow but do not prove active gameplay / concurrent live matches.",
        "matchGetSnapshot reads are authoritative but do not simulate match intent traffic, rendering, or host networking.",
        "Firestore queryCount and scannedDocuments are diagnostics, not exact billable read/write totals.",
        "Localhost p95/throughput cannot predict deployed latency, quotas, costs or maximum players.",
        "For actual capacity claims: production-like backend, calibrated load, Cloud Monitoring and multi-PC/region testing required.",
      ],
    };
    const failures = stages.reduce((total, s) => total + s.failures, 0);
    if (failures) {
      failed = true;
      console.warn(`Measured stages contained ${failures} failures. See report for details.`);
    } else {
      console.log("PASS All measured local load request rounds completed without callable failures.");
    }
  }
} catch (error) {
  failed = true;
  console.error(`Phase16I load test FAILED: ${error?.message ?? error}`);
} finally {
  if (shouldCleanup) await cleanup();
  if (report) {
    report.cleanup = {attempted: true, firestoreDeletesOrMissing: state.cleanupWrites};
    const reportPath = join(tmpdir(), `AtlasBoard_Phase16I_Load_${runId}.json`);
    try {
      await writeFile(reportPath, JSON.stringify(report, null, 2) + "\n", "utf8");
      console.log(`REPORT_JSON: ${reportPath}`);
    } catch (error) {
      failed = true;
      console.error(`Could not save JSON report: ${error.message}`);
    }
  }
  if (state.stopped) failed = true;
  if (failed) process.exitCode = 1;
}
