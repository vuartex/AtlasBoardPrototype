/**
 * AtlasBoard Phase 16I | ACTIVE MATCH TRANSPORT timed SOAK v1
 *
 * Measures real Auth + Functions + Firestore emulator callables with independent
 * host/guest pairs, real guest intents, host pending-list/ACK, revisioned host
 * publication and guest propagation. This is NOT a Unity rules-engine simulation.
 *
 * Usage (emulators already started, from Backend/Firebase/functions):
 *   node scripts/phase16i-active-match-soak.mjs --self-test
 *   node scripts/phase16i-active-match-soak.mjs
 *   node scripts/phase16i-active-match-soak.mjs --matches=16 --concurrency=8,16 --seconds=75 --interval-ms=2500
 *
 * No source/deploy/roadmap/Git changes. Strict 127.0.0.1 emulator destinations.
 */
import {createHash, createHmac, randomBytes} from "node:crypto";
import {writeFile} from "node:fs/promises";
import {tmpdir} from "node:os";
import {join} from "node:path";
import {performance} from "node:perf_hooks";

const PROJECT = "atlasboard-usa";
const REGION = "europe-west1";
const HOST = "127.0.0.1";
const PORTS = Object.freeze({auth: 9099, firestore: 8080, functions: 5001, hub: 4400});
const URLS = Object.freeze(Object.fromEntries(Object.entries(PORTS)
  .map(([name, port]) => [name, `http://${HOST}:${port}`])));
const API_KEY = "atlasboard-local-emulator-only";
const APP_CHECK_PLACEHOLDER = "phase16i-local-emulator-only";
const JOIN_PEPPER = "atlasboard-local-emulator-lobby-code-pepper-v1";
const TIMEOUT_MS = 20000;
const REPORT_WINDOW_MS = 15000;
const MAX_CYCLES_PER_MATCH = 100;
const runId = `${Date.now().toString(36)}${randomBytes(4).toString("hex")}`;
const test = {users: [], matches: [], stopped: false, cleanupDeletes: 0,
  cleanupWarnings: [], setupCallables: 0, warmupCallables: 0, expectedRejections: 0,
  replayProbes: 0, setupDocumentWrites: 0};

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function parseArgs(args) {
  if (args.includes("--help") || args.includes("-h")) return {help: true};
  if (args.includes("--self-test")) return {selfTest: true};
  const o = {matches: 16, concurrency: [8, 16], seconds: 75, intervalMs: 2500};
  for (const arg of args) {
    const m = /^--(matches|concurrency|seconds|interval-ms)=(.+)$/.exec(arg);
    assert(m, `Unknown option ${arg}. Run --help.`);
    if (m[1] === "concurrency") o.concurrency = m[2].split(",").map(Number);
    else if (m[1] === "interval-ms") o.intervalMs = Number(m[2]);
    else o[m[1]] = Number(m[2]);
  }
  const isInt = (n, lo, hi) => Number.isSafeInteger(n) && n >= lo && n <= hi;
  assert(isInt(o.matches, 1, 16), "--matches must be 1..16.");
  assert(isInt(o.seconds, 20, 180), "--seconds must be 20..180 per stage.");
  assert(isInt(o.intervalMs, 1500, 10000), "--interval-ms must be 1500..10000.");
  assert(o.concurrency.length > 0 && o.concurrency.length <= 3 &&
    o.concurrency.every((c) => isInt(c, 1, 16)) &&
    new Set(o.concurrency).size === o.concurrency.length &&
    o.concurrency.every((c) => c <= o.matches),
  "--concurrency needs 1..3 unique values, each 1..16 and <= --matches.");
  const worstCycles = o.concurrency.reduce((a, b) => a + b, 0) *
    Math.ceil(o.seconds * 1000 / o.intervalMs);
  assert(worstCycles <= 2400, "Measured cycle upper bound is 2400. Reduce duration/concurrency.");
  return o;
}

function pause(ms) {
  return new Promise((resolve) => setTimeout(resolve, Math.max(0, ms)));
}

function allowLocalUrl(url, service) {
  const u = new URL(url);
  assert(u.protocol === "http:" && u.hostname === HOST &&
    u.port === String(PORTS[service]) && !u.username && !u.password,
  `Refusing non-local ${service} URL: ${url}`);
  if (service === "functions") assert(u.pathname.startsWith(`/${PROJECT}/${REGION}/`),
    "Wrong project/region in callable URL.");
  if (service === "firestore") assert(u.pathname.startsWith(`/v1/projects/${PROJECT}/databases/(default)/documents/`),
    "Wrong Firestore project path.");
}

async function request(url, service, {method = "GET", headers = {}, body} = {}) {
  allowLocalUrl(url, service);
  const start = performance.now();
  try {
    const response = await fetch(url, {method, headers,
      ...(body === undefined ? {} : {body: JSON.stringify(body)}),
      signal: AbortSignal.timeout(TIMEOUT_MS)});
    const text = await response.text();
    let json = null;
    try { json = text ? JSON.parse(text) : null; } catch { /* safe sanitized errors below */ }
    return {ok: response.ok, status: response.status, json,
      ms: performance.now() - start,
      errorKey: json?.error?.details?.errorKey ?? json?.error?.status ?? null};
  } catch (err) {
    return {ok: false, status: 0, json: null, ms: performance.now() - start,
      errorKey: err?.name === "TimeoutError" ? "TIMEOUT" : "NETWORK_ERROR"};
  }
}

function output(result) { return result.json?.result ?? result.json?.data; }

async function call(name, person, data, setup = false) {
  assert(/^[a-zA-Z][a-zA-Z0-9]*$/.test(name), "Bad callable name.");
  assert(person && typeof person.token === "string" && person.token.length > 20,
    "Missing Firebase Auth Emulator token.");
  if (setup) test.setupCallables++;
  return request(`${URLS.functions}/${PROJECT}/${REGION}/${name}`, "functions", {
    method: "POST", headers: {"Content-Type": "application/json",
      Authorization: `Bearer ${person.token}`, "X-Firebase-AppCheck": APP_CHECK_PLACEHOLDER},
    body: {data},
  });
}

async function firestore(path, method = "GET", fields) {
  assert(/^[a-zA-Z0-9_/-]+$/.test(path), "Unsafe Firestore document path.");
  return request(`${URLS.firestore}/v1/projects/${PROJECT}/databases/(default)/documents/${path}`,
    "firestore", {method, headers: {Authorization: "Bearer owner",
      ...(fields ? {"Content-Type": "application/json"} : {})},
    ...(fields ? {body: {fields}} : {})});
}

async function preflight() {
  const hub = await request(`${URLS.hub}/emulators`, "hub");
  assert(hub.ok, "Firebase Emulator Hub unavailable. Start emulators ONCE.");
  for (const service of ["auth", "firestore", "functions"]) {
    const e = hub.json?.[service];
    assert(e && Number(e.port) === PORTS[service] &&
      [HOST, "localhost", "::1"].includes(e.host),
    `Wrong ${service} emulator (must be localhost:${PORTS[service]}).`);
  }
  console.log("PASS Local Auth/Firestore/Functions emulator preflight (127.0.0.1, atlasboard-usa).");
}

async function createUser(label) {
  const email = `atlasboard.active.${runId}.${label}@example.com`;
  const password = `AtlasActive!${runId}!${label}`;
  const signUp = await request(`${URLS.auth}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=${API_KEY}`,
    "auth", {method: "POST", headers: {"Content-Type": "application/json"},
      body: {email, password, returnSecureToken: true}});
  assert(signUp.ok && signUp.json?.idToken && signUp.json?.localId,
    `Auth Emulator signup (${label}) failed: HTTP ${signUp.status} ${signUp.errorKey ?? ""}`);
  const u = {uid: signUp.json.localId, token: signUp.json.idToken};
  test.users.push(u); // Register for cleanup as soon as signup succeeds.
  let res = await firestore(`users/${u.uid}`, "PATCH", {
    accountStatus: {stringValue: "active"}, membershipTier: {stringValue: "normal"},
    schemaVersion: {integerValue: "1"}});
  assert(res.ok, `Seed users failed: HTTP ${res.status}`);
  test.setupDocumentWrites++;
  res = await firestore(`public_profiles/${u.uid}`, "PATCH", {
    displayName: {stringValue: `Active Load ${label}`}, avatarId: {stringValue: ""},
    profileFrameId: {stringValue: ""}, schemaVersion: {integerValue: "1"}});
  assert(res.ok, `Seed public_profiles failed: HTTP ${res.status}`);
  test.setupDocumentWrites++;
  return u;
}

const settings = {mapId: "Turkey", themeId: "Classic Table", roundLimit: 20,
  maxPlayers: 2, balancedDevelopment: true, doublesEnabled: true,
  tripleDoublePenaltyEnabled: true};

async function createMatch(index, versions) {
  const host = await createUser(`host${index}`);
  const guest = await createUser(`guest${index}`);
  const made = await call("lobbyCreatePrivateRoom", host, {...settings, ...versions}, true);
  assert(made.ok, `Lobby creation ${index} failed: HTTP ${made.status} ${made.errorKey ?? ""}`);
  const lobbyId = output(made)?.snapshot?.lobbyId;
  const roomCode = output(made)?.roomCode;
  assert(lobbyId && roomCode, `Missing private room code/lobby ID for match ${index}`);
  const m = {host, guest, lobbyId, roomCode, matchId: "", intentIds: new Set(),
    lastAcceptedCommandId: "", lastAcceptedEnvelope: null};
  test.matches.push(m); // Cleanup on partial startup failure too.
  const join = await call("lobbyJoinByCode", guest, {roomCode, password: "",
    idempotencyKey: `phase16i-active-${runId}-${index}`, ...versions}, true);
  assert(join.ok, `Join ${index} failed: HTTP ${join.status} ${join.errorKey ?? ""}`);
  const rev = output(join)?.snapshot?.settingsRevision;
  assert(Number.isSafeInteger(rev), `Missing join revision for ${index}`);
  const ready = await call("lobbySetReady", guest, {lobbyId,
    expectedSettingsRevision: rev, ready: true}, true);
  assert(ready.ok, `Ready ${index} failed: HTTP ${ready.status} ${ready.errorKey ?? ""}`);
  const start = await call("lobbyStartMatch", host,
    {lobbyId, expectedSettingsRevision: output(ready)?.snapshot?.settingsRevision}, true);
  assert(start.ok, `Start ${index} failed: HTTP ${start.status} ${start.errorKey ?? ""}`);
  m.matchId = output(start)?.snapshot?.matchId ?? "";
  assert(m.matchId, `Missing matchId for ${index}`);
  const snapshot = await call("matchGetSnapshot", guest, {matchId: m.matchId});
  test.warmupCallables++;
  assert(snapshot.ok && output(snapshot)?.snapshot,
    `Initial authoritative network snapshot ${index} failed: HTTP ${snapshot.status} ${snapshot.errorKey ?? ""}`);
  return m;
}

function envelope(snapshot) {
  assert(snapshot && Number.isSafeInteger(snapshot.revision) &&
    Number.isSafeInteger(snapshot.eventSequence) &&
    Number.isSafeInteger(snapshot.authorityEpoch) && typeof snapshot.phase === "string",
  "Incomplete authoritative match envelope.");
  return {observedRevision: snapshot.revision,
    observedEventSequence: snapshot.eventSequence,
    observedAuthorityEpoch: snapshot.authorityEpoch, observedPhase: snapshot.phase};
}

function commandDocId(uid, commandId) {
  return createHash("sha256").update(`${uid}:${commandId}`, "utf8").digest("hex");
}

async function runCycle(m, stageIdx, round) {
  const started = performance.now();
  const steps = {};
  const checked = async (step, fn) => {
    const r = await fn();
    steps[step] = +r.ms.toFixed(1);
    if (!r.ok) throw new Error(`${step} HTTP ${r.status} ${r.errorKey ?? "UNKNOWN"}`);
    return r;
  };
  const matchId = m.matchId;
  const raw = await checked("guestReadBefore", () => call("matchGetSnapshot", m.guest, {matchId}));
  const before = output(raw)?.snapshot;
  const seen = envelope(before);
  const id = `active-${runId}-s${stageIdx}-r${round}-m${matchId.slice(0,8)}`;
  const intent = {matchId, clientCommandId: id, intentType: "request_roll",
    payloadJson: JSON.stringify({transportLoadOnly: true, stageIdx, round}), ...seen};
  m.intentIds.add(commandDocId(m.guest.uid, id)); // Cleanup even if response is lost.
  const submit = await checked("guestSubmit", () => call("matchSubmitIntent", m.guest, intent));
  const accepted = output(submit);
  assert(accepted?.accepted === true && accepted.idempotentReplay === false && accepted.intentId,
    "New guest intent was not accepted exactly once.");
  const pending = await checked("hostList", () => call("matchHostListPendingIntents", m.host, {matchId}));
  const queue = output(pending)?.intents;
  assert(Array.isArray(queue) && queue.filter((x) => x.intentId === accepted.intentId).length === 1,
    `Host did not find exactly one current pending intent (stage ${stageIdx}, round ${round}).`);
  const ack = await checked("hostAck", () => call("matchHostAcknowledgeIntents", m.host,
    {matchId, intentIds: [accepted.intentId]}));
  assert(output(ack)?.acknowledged === 1, "Host failed to ACK accepted intent.");
  const publish = await checked("hostPublish", () => call("matchHostPublishState", m.host,
    {matchId, expectedRevision: before.revision, phase: "awaiting_roll", turnSeatId: "seat_2",
      eventSequence: before.eventSequence + 1,
      snapshotJson: JSON.stringify({transportLoadOnly: true, stageIdx, round, acked: 1})}));
  const state = output(publish)?.state;
  assert(state?.revision === before.revision + 1 && state?.eventSequence === before.eventSequence + 1,
    "Host publication failed revision/event progression.");
  const afterCall = await checked("guestReadAfter", () => call("matchGetSnapshot", m.guest, {matchId}));
  const after = output(afterCall)?.snapshot;
  assert(after?.revision === state.revision && after?.eventSequence === state.eventSequence &&
    after?.phase === "awaiting_roll" && after?.turnSeatId === "seat_2",
  "Guest did not receive the authoritative published network state.");
  m.lastAcceptedCommandId = id;
  m.lastAcceptedEnvelope = seen;
  return {ok: true, ms: performance.now() - started, steps,
    expectedRevision: before.revision, publishedRevision: after.revision, callableAttempts: 6};
}

function percentile(sorted, p) {
  return sorted.length ? +sorted[Math.ceil(sorted.length * p) - 1].toFixed(1) : null;
}
function summarize(observations, wallSeconds) {
  const successful = observations.filter((x) => x.ok);
  const values = successful.map((x) => x.ms).sort((a, b) => a - b);
  const stepNames = ["guestReadBefore", "guestSubmit", "hostList", "hostAck", "hostPublish", "guestReadAfter"];
  const steps = {};
  for (const name of stepNames) {
    const lat = successful.map((x) => x.steps[name]).filter(Number.isFinite).sort((a, b) => a - b);
    steps[name] = {p50: percentile(lat, 0.5), p95: percentile(lat, 0.95), p99: percentile(lat, 0.99)};
  }
  const failed = observations.filter((x) => !x.ok);
  return {attempts: observations.length, successes: successful.length, failures: failed.length,
    errorRatePercent: observations.length ? +(100 * failed.length / observations.length).toFixed(2) : 0,
    wallSeconds: +wallSeconds.toFixed(3), throughputCyclesPerSecond:
      wallSeconds ? +(successful.length / wallSeconds).toFixed(2) : 0,
    completeCycleLatencyMs: {p50: percentile(values, 0.5), p95: percentile(values, 0.95),
      p99: percentile(values, 0.99), max: values.length ? +values.at(-1).toFixed(1) : null},
    perCallableStepLatencyMs: steps,
    errors: failed.map((x) => ({stage: x.stage, matchIndex: x.matchIndex, round: x.round,
      error: x.error})).slice(0, 16)};
}

function resourceSample(elapsedSeconds) {
  const mem = process.memoryUsage();
  return {elapsedSeconds: +elapsedSeconds.toFixed(2),
    loadGeneratorRssMiB: +(mem.rss / 1048576).toFixed(2),
    loadGeneratorHeapUsedMiB: +(mem.heapUsed / 1048576).toFixed(2)};
}

async function runStage(matchCount, seconds, intervalMs, idx) {
  // Independent sequential worker per match, with a fixed minimum start interval.
  // Stop scheduling when the stage deadline is reached; let in-flight calls finish.
  const observations = [];
  const t0 = performance.now();
  const deadline = t0 + seconds * 1000;
  let roundBudgetHit = 0;
  const memory = [resourceSample(0)];
  const livePrint = setInterval(() => {
    const elapsed = (performance.now() - t0) / 1000;
    const recent = observations.filter((x) => x.finishedAt >= performance.now() - REPORT_WINDOW_MS);
    const sum = summarize(recent, Math.min(REPORT_WINDOW_MS / 1000, elapsed));
    memory.push(resourceSample(elapsed));
    console.log(`  +${Math.round(elapsed)}s matches=${matchCount} recent=${sum.successes}/${sum.attempts}` +
      ` p95=${sum.completeCycleLatencyMs.p95 ?? "n/a"}ms failed=${sum.failures}`);
  }, REPORT_WINDOW_MS);
  livePrint.unref();
  try {
    await Promise.all(test.matches.slice(0, matchCount).map(async (m, matchIndex) => {
      let round = 0;
      while (!test.stopped && performance.now() < deadline && round < MAX_CYCLES_PER_MATCH) {
        const cycleStart = performance.now();
        try {
          const cycle = await runCycle(m, idx, round);
          observations.push({...cycle, matchIndex, round, finishedAt: performance.now()});
        } catch (error) {
          observations.push({ok: false, matchIndex, round, stage: matchCount,
            finishedAt: performance.now(),
            error: String(error?.message ?? error).slice(0, 240)});
          break; // Never continue a damaged match's revision state.
        }
        round++;
        // Preserve pacing while keeping the scheduled test bounded.
        if (!test.stopped && performance.now() < deadline) {
          await pause(Math.min(deadline - performance.now(),
            Math.max(0, intervalMs - (performance.now() - cycleStart))));
        }
      }
      if (round === MAX_CYCLES_PER_MATCH) roundBudgetHit++;
    }));
  } finally {
    clearInterval(livePrint);
    memory.push(resourceSample((performance.now() - t0) / 1000));
  }
  const wallSeconds = (performance.now() - t0) / 1000;
  const m = summarize(observations, wallSeconds);
  const windows = [];
  const winCount = Math.ceil(wallSeconds * 1000 / REPORT_WINDOW_MS);
  for (let w = 0; w < winCount; w++) {
    const start = t0 + w * REPORT_WINDOW_MS;
    const end = start + REPORT_WINDOW_MS;
    const obs = observations.filter((x) => x.finishedAt >= start && x.finishedAt < end);
    windows.push({startSeconds: w * REPORT_WINDOW_MS / 1000,
      endSeconds: Math.min((w + 1) * REPORT_WINDOW_MS / 1000, +wallSeconds.toFixed(3)),
      ...summarize(obs, Math.min(REPORT_WINDOW_MS / 1000, Math.max(0, wallSeconds - w * REPORT_WINDOW_MS / 1000)))});
  }
  // Empty completed matches indicate that load was never exercised; fail closed.
  const distinctMatchesSucceeded = new Set(observations.filter((x) => x.ok).map((x) => x.matchIndex)).size;
  const incompleteMatches = matchCount - distinctMatchesSucceeded;
  console.log(`soak matches=${String(matchCount).padStart(2)} ` +
    `cycles=${m.successes}/${m.attempts} p50=${m.completeCycleLatencyMs.p50}ms ` +
    `p95=${m.completeCycleLatencyMs.p95}ms rate=${m.throughputCyclesPerSecond}/s ` +
    `errors=${m.failures} idleMatches=${incompleteMatches}`);
  if (m.failures) console.warn(`  Failures: ${JSON.stringify(m.errors)}`);
  return {kind: "active-match-timed-soak", concurrentMatches: matchCount,
    targetSeconds: seconds, intervalMs, distinctMatchesSucceeded,
    incompleteMatches, roundBudgetHit, observationsPerMatch: Object.fromEntries(
      Array.from({length: matchCount}, (_, k) => [String(k),
        observations.filter((x) => x.matchIndex === k).length])),
    timeWindows: windows, loadGeneratorMemorySamples: memory, ...m};
}

async function runGuardProbes() {
  const stats = [];
  for (const m of test.matches) {
    if (!m.lastAcceptedCommandId) continue;
    const last = m.lastAcceptedEnvelope;
    const id = m.lastAcceptedCommandId;
    const replay = await call("matchSubmitIntent", m.guest,
      {matchId: m.matchId, clientCommandId: id, intentType: "request_roll",
        payloadJson: JSON.stringify({transportLoadOnly: true, replay: true}), ...last});
    assert(replay.ok && output(replay)?.idempotentReplay === true,
      `Idempotency replay after revision advancement failed: HTTP ${replay.status} ${replay.errorKey}`);
    test.replayProbes++;
    const staleId = `stale-${runId}-${m.matchId.slice(0,8)}`;
    m.intentIds.add(commandDocId(m.guest.uid, staleId));
    const stale = await call("matchSubmitIntent", m.guest,
      {matchId: m.matchId, clientCommandId: staleId, intentType: "request_roll",
        payloadJson: "{}", ...last});
    assert(!stale.ok && stale.errorKey === "match.error.stale_client_state",
      `Stale client-state guard did not reject correctly: HTTP ${stale.status} ${stale.errorKey}`);
    test.expectedRejections++;
    const rejectedPublish = await call("matchHostPublishState", m.host,
      {matchId: m.matchId, expectedRevision: last.observedRevision,
        phase: "awaiting_roll", turnSeatId: "seat_2", eventSequence: last.observedEventSequence,
        snapshotJson: "{}"});
    assert(!rejectedPublish.ok && rejectedPublish.errorKey === "match.error.revision_mismatch",
      `Stale host revision guard did not reject correctly: HTTP ${rejectedPublish.status} ${rejectedPublish.errorKey}`);
    test.expectedRejections++;
    stats.push({match: stats.length, replay: "PASS", staleClientRejected: "PASS",
      staleHostPublishRejected: "PASS"});
  }
  console.log(`PASS ${test.replayProbes} idempotent replay probes; ${test.expectedRejections} expected stale-revision protections.`);
  return stats;
}

function hashedCode(code) {
  return createHmac("sha256", JOIN_PEPPER).update(`atlasboard:lobby-code:${code}`, "utf8").digest("hex");
}

async function cleanup() {
  console.log("Cleanup: deleting only this run's created match/lobby/accounts...");
  async function safeDelete(path) {
    try {
      const r = await firestore(path, "DELETE");
      if (r.ok || r.status === 404) test.cleanupDeletes++;
      else {test.cleanupWarnings.push(`${path} HTTP ${r.status}`);}
    } catch (error) { test.cleanupWarnings.push(`${path}: ${error.message}`); }
  }
  for (const m of test.matches) {
    if (m.matchId) {
      for (const id of m.intentIds) await safeDelete(`matches/${m.matchId}/intents/${id}`);
      await safeDelete(`matches/${m.matchId}/network/state`);
      for (let seat = 1; seat <= 4; seat++) await safeDelete(`matches/${m.matchId}/seats/seat_${seat}`);
      await safeDelete(`matches/${m.matchId}`);
    }
    if (m.lobbyId) {
      for (let seat = 1; seat <= 4; seat++) await safeDelete(`lobbies/${m.lobbyId}/members/seat_${seat}`);
      await safeDelete(`lobby_discovery/${m.lobbyId}`);
      await safeDelete(`lobbies/${m.lobbyId}`);
    }
    if (m.roomCode) await safeDelete(`join_codes/${hashedCode(m.roomCode)}`);
  }
  for (const u of test.users) {
    await safeDelete(`public_profiles/${u.uid}`);
    await safeDelete(`users/${u.uid}`);
    const r = await request(`${URLS.auth}/identitytoolkit.googleapis.com/v1/accounts:delete?key=${API_KEY}`,
      "auth", {method: "POST", headers: {"Content-Type": "application/json"},
        body: {idToken: u.token}});
    if (!r.ok) test.cleanupWarnings.push(`auth user delete HTTP ${r.status}`);
  }
  console.log(`Cleanup: ${test.cleanupDeletes} document delete/missing operations; ` +
    `${test.cleanupWarnings.length} warnings.`);
}

function selfTest() {
  const args = parseArgs(["--matches=4", "--concurrency=2,4", "--seconds=30", "--interval-ms=2000"]);
  assert(args.matches === 4 && args.concurrency.length === 2 && args.seconds === 30 &&
    args.intervalMs === 2000, "CLI argument test failed.");
  let refused = false;
  try { allowLocalUrl("https://example.com/project/hack", "functions"); } catch { refused = true; }
  assert(refused, "Non-loopback target must fail closed.");
  refused = false;
  try { parseArgs(["--matches=2", "--concurrency=16"]); } catch { refused = true; }
  assert(refused, "Excessive concurrency must fail closed.");
  refused = false;
  try { parseArgs(["--seconds=999"]); } catch { refused = true; }
  assert(refused, "Unbounded duration must fail closed.");
  const fake = summarize([{ok: true, ms: 12, steps: Object.fromEntries(
    ["guestReadBefore", "guestSubmit", "hostList", "hostAck", "hostPublish", "guestReadAfter"]
      .map((x) => [x, 2]))}, {ok: false, error: "X"}], 1);
  assert(fake.successes === 1 && fake.failures === 1 && fake.completeCycleLatencyMs.p95 === 12,
    "Summary/percentile self-test failed.");
  const hashed = commandDocId("uid", "abc12345");
  assert(hashed.length === 64 && hashed === commandDocId("uid", "abc12345"),
    "Deterministic intent ID self-test failed.");
  assert(resourceSample(1).loadGeneratorRssMiB > 0, "Memory probe failed.");
  console.log("PASS self-test: bounds, destination protection, metrics, memory probe, intent cleanup IDs.");
}

function help() {
  console.log(`AtlasBoard Phase16I Active Match Transport timed SOAK v1 (emulator-only)
  --matches=16          Real private two-account test matches (1..16)
  --concurrency=8,16   Independent active matches per timed stage (1..16)
  --seconds=75          Seconds per stage (20..180)
  --interval-ms=2500   Target minimum cycle-start interval per match (1500..10000)
  --self-test          Offline safety and statistics unit checks
  --help               Show this help

Run ONLY with existing Firebase Auth/Firestore/Functions EMULATORS on 127.0.0.1.
Do not launch QA --full or a second emulator while testing.
Writes only temporary user, lobby, match and intent records for this run.
Time-window and load-generator memory metrics are provided; server-side memory
and real Internet capacity are NOT measured.
`);
}

let report = null;
let failed = false;
let doCleanup = false;
try {
  const options = parseArgs(process.argv.slice(2));
  if (options.help) help();
  else if (options.selfTest) selfTest();
  else {
    process.on("SIGINT", () => {
      test.stopped = true;
      console.warn("CTRL+C: finish in-flight operations, then clean up.");
    });
    console.log("AtlasBoard Phase16I Active Match Transport TIMED SOAK v1 (EMULATOR ONLY)");
    console.log(`Options: ${JSON.stringify(options)}`);
    await preflight();
    doCleanup = true;
    const versions = {gameVersion: `phase16i-active-${runId}`,
      protocolVersion: 1, rulesVersion: 1, contentVersion: "1", regionId: "auto"};
    for (let i = 0; i < options.matches && !test.stopped; i++) {
      await createMatch(i, versions);
      console.log(`Created private test matches: ${test.matches.length}/${options.matches}`);
    }
    assert(!test.stopped, "Interrupted during setup.");
    const stages = [];
    for (let i = 0; i < options.concurrency.length && !test.stopped; i++) {
      const stage = await runStage(options.concurrency[i], options.seconds, options.intervalMs, i);
      stages.push(stage);
      if (stage.failures || stage.incompleteMatches) {failed = true; break;}
    }
    let probes = [];
    if (!failed && !test.stopped) probes = await runGuardProbes();
    report = {
      label: "AtlasBoard Phase16I ACTIVE MATCH timed SOAK v1",
      timestampUtc: new Date().toISOString(), runId, environment: "EMULATOR_ONLY_LOCALHOST",
      project: PROJECT, options,
      scenario: {realCallableStartedTwoHumanMatches: test.matches.length,
        physicalPlayers: 0, gameRulesSimulated: false,
        transportCycle: "guest snapshot -> guest request_roll intent -> host pending list -> host ACK -> host revision publish -> guest snapshot",
        matchRoundCycles: stages.reduce((n, s) => n + s.successes, 0),
        timeWindowsSeconds: REPORT_WINDOW_MS / 1000,
        testStageDurationSeconds: options.seconds,
        loadGeneratorMemoryOnly: true},
      setup: {accountSeedFirestoreWrites: test.setupDocumentWrites,
        setupCallables: test.setupCallables, warmupCallables: test.warmupCallables},
      stages, guardProbes: probes,
      limitations: [
        "Transport-only workflow: no Unity dice, board simulation, real rule validation or P2P packet transport.",
        "Each concurrent match has one authenticated host and one guest; parallel load is across independent matches.",
        "The test host publishes synthetic snapshot JSON as a measured transport operation, not actual game simulation.",
        "Intent list queries and ACK/publication are real emulator Firestore operations; results do not equal cloud billing.",
        "Localhost throughput and latency do not establish player capacity, cloud quotas, or Internet performance.",
        "Node memory samples describe only the load generator, NOT the Firebase emulators or Unity client.",
        "Sustained test has fixed pacing and bounded duration; not an unlimited closed-loop maximum throughput test.",
      ],
    };
    if (!failed && !test.stopped) console.log("PASS sustained active-match transport cycles and revision/idempotency guards.");
  }
} catch (err) {
  failed = true;
  console.error(`Active-match soak FAILED: ${String(err?.message ?? err).slice(0,400)}`);
} finally {
  if (doCleanup) await cleanup();
  if (report) {
    report.cleanup = {attempted: true, firestoreDeletesOrMissing: test.cleanupDeletes,
      warnings: test.cleanupWarnings};
    const file = join(tmpdir(), `AtlasBoard_Phase16I_ActiveMatchSoak_${runId}.json`);
    try {
      await writeFile(file, JSON.stringify(report, null, 2) + "\n", "utf8");
      console.log(`REPORT_JSON: ${file}`);
    } catch (err) {
      failed = true;
      console.error(`Report write failed: ${err.message}`);
    }
  }
  if (test.cleanupWarnings.length) {
    failed = true;
    for (const warning of test.cleanupWarnings.slice(0, 10)) console.warn(`Cleanup WARNING: ${warning}`);
  }
  if (test.stopped) failed = true;
  if (failed) process.exitCode = 1;
}
