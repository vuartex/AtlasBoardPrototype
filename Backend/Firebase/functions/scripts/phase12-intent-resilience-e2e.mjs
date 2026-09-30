import {createHash} from "crypto";

const PROJECT_ID = "atlasboard-usa";
const REGION = "europe-west1";
const AUTH_BASE = "http://127.0.0.1:9099";
const FUNCTIONS_BASE = "http://127.0.0.1:5001";
const FIRESTORE_BASE = "http://127.0.0.1:8080";
const HUB_BASE = "http://127.0.0.1:4400";
const API_KEY = "atlasboard-local-emulator-only";
const TIMEOUT_MS = 10000;

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

async function requestJson(url, options = {}) {
  const response = await fetch(url, {
    ...options,
    signal: AbortSignal.timeout(TIMEOUT_MS),
  });
  const text = await response.text();

  let json = null;

  if (text) {
    try {
      json = JSON.parse(text);
    } catch {
      json = {raw: text};
    }
  }

  return {response, json, text};
}

async function postJson(url, body, headers = {}) {
  return requestJson(url, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...headers,
    },
    body: JSON.stringify(body),
  });
}

function callableUrl(name) {
  return `${FUNCTIONS_BASE}/${PROJECT_ID}/${REGION}/${name}`;
}

async function callFunction(name, token, data) {
  return postJson(
    callableUrl(name),
    {data},
    {Authorization: `Bearer ${token}`},
  );
}

function result(call) {
  return call.json?.result ?? call.json?.data;
}

function errorKey(call) {
  return call.json?.error?.details?.errorKey ?? "";
}

function firestoreUrl(path) {
  return `${FIRESTORE_BASE}/v1/projects/${PROJECT_ID}` +
    `/databases/(default)/documents/${path}`;
}

async function patchDoc(path, fields, updateMask = []) {
  const query =
    updateMask.length > 0 ?
      "?" +
      updateMask
        .map((field) =>
          `updateMask.fieldPaths=${encodeURIComponent(field)}`
        )
        .join("&") :
      "";

  return requestJson(
    firestoreUrl(path) + query,
    {
      method: "PATCH",
      headers: {"Content-Type": "application/json"},
      body: JSON.stringify({fields}),
    },
  );
}

async function getDoc(path) {
  return requestJson(
    firestoreUrl(path),
  );
}

async function deleteDoc(path) {
  try {
    await requestJson(
      firestoreUrl(path),
      {method: "DELETE"},
    );
  } catch {}
}

async function verifyEmulators() {
  const hub =
    await requestJson(`${HUB_BASE}/emulators`);

  assert(
    hub.response.ok,
    "Emulator Hub is not reachable.",
  );

  for (const name of [
    "auth",
    "functions",
    "firestore",
  ]) {
    assert(
      hub.json?.[name],
      `Required emulator not running: ${name}`,
    );
  }
}

async function createAuthUser(label) {
  const nonce =
    `${Date.now()}-${Math.floor(Math.random() * 1000000)}`;

  const email =
    `atlasboard.phase12a.${label}.${nonce}@example.com`;

  const password =
    `AtlasBoardPhase12A!${label}!${nonce}`;

  const call =
    await postJson(
      `${AUTH_BASE}/identitytoolkit.googleapis.com/` +
        `v1/accounts:signUp?key=${API_KEY}`,
      {
        email,
        password,
        returnSecureToken: true,
      },
    );

  assert(
    call.response.ok,
    `${label} Auth signup failed: ${call.text}`,
  );

  return {
    uid: call.json?.localId,
    token: call.json?.idToken,
  };
}

async function deleteAuthUser(user) {
  if (!user?.token) return;

  try {
    await postJson(
      `${AUTH_BASE}/identitytoolkit.googleapis.com/` +
        `v1/accounts:delete?key=${API_KEY}`,
      {idToken: user.token},
    );
  } catch {}
}

function intentDocId(uid, commandId) {
  return createHash("sha256")
    .update(`${uid}:${commandId}`, "utf8")
    .digest("hex");
}

function stringValue(value) {
  return {stringValue: value};
}

function integerValue(value) {
  return {integerValue: String(value)};
}

function booleanValue(value) {
  return {booleanValue: value};
}

let host;
let guest;
const matchId =
  `phase12a-${Date.now()}`;
const statePath =
  `matches/${matchId}/network/state`;

async function seedMatch() {
  let call =
    await patchDoc(
      `matches/${matchId}`,
      {
        lobbyId: stringValue("phase12a-lobby"),
        hostAccountId: stringValue(host.uid),
        status: stringValue("active"),
        authorityEpoch: integerValue(2),
        networkRevision: integerValue(7),
        schemaVersion: integerValue(1),
      },
    );

  assert(call.response.ok, "Match seed failed.");

  call =
    await patchDoc(
      statePath,
      {
        revision: integerValue(7),
        phase: stringValue("awaiting_roll"),
        turnSeatId: stringValue("seat_2"),
        eventSequence: integerValue(3),
        snapshotJson: stringValue("{}"),
        authorityHostAccountId:
          stringValue(host.uid),
        schemaVersion: integerValue(1),
      },
    );

  assert(call.response.ok, "Network state seed failed.");

  call =
    await patchDoc(
      `matches/${matchId}/seats/seat_1`,
      {
        seatId: stringValue("seat_1"),
        slotIndex: integerValue(0),
        seatMode: stringValue("host_local"),
        displayName: stringValue("Phase 12A Host"),
        isHost: booleanValue(true),
        accountId: stringValue(host.uid),
        localOwnerAccountId:
          stringValue(host.uid),
        controllerKind: stringValue("human"),
        connectionState: stringValue("connected"),
        afkLockedOut: booleanValue(false),
      },
    );

  assert(call.response.ok, "Host seat seed failed.");

  call =
    await patchDoc(
      `matches/${matchId}/seats/seat_2`,
      {
        seatId: stringValue("seat_2"),
        slotIndex: integerValue(1),
        seatMode: stringValue("remote_human"),
        displayName: stringValue("Phase 12A Guest"),
        isHost: booleanValue(false),
        accountId: stringValue(guest.uid),
        localOwnerAccountId: stringValue(""),
        controllerKind: stringValue("human"),
        connectionState: stringValue("connected"),
        afkLockedOut: booleanValue(false),
      },
    );

  assert(call.response.ok, "Guest seat seed failed.");
}

function exactEnvelope(overrides = {}) {
  return {
    matchId,
    intentType: "request_roll",
    payloadJson: "{\"source\":\"phase12a\"}",
    observedRevision: 7,
    observedEventSequence: 3,
    observedAuthorityEpoch: 2,
    observedPhase: "awaiting_roll",
    ...overrides,
  };
}

try {
  console.log(
    "AtlasBoard Phase 12A Intent Epoch/Stale Command Local E2E",
  );

  console.log("[0/12] Emulator safety preflight...");
  await verifyEmulators();
  console.log("[0/12] PASS.");

  console.log("[1/12] Create canonical test accounts...");
  host = await createAuthUser("host");
  guest = await createAuthUser("guest");
  console.log("[1/12] PASS.");

  console.log("[2/12] Seed active match + seats + revision 7...");
  await seedMatch();
  console.log("[2/12] PASS.");

  console.log("[3/12] Exact snapshot envelope accepts command...");
  let call =
    await callFunction(
      "matchSubmitIntent",
      guest.token,
      exactEnvelope({
        clientCommandId:
          "phase12a-exact-command-0001",
      }),
    );

  assert(
    call.response.ok &&
      result(call).idempotentReplay === false,
    `Exact command failed: ${call.text}`,
  );
  console.log("[3/12] PASS.");

  console.log("[4/12] Exact command replay is idempotent...");
  call =
    await callFunction(
      "matchSubmitIntent",
      guest.token,
      exactEnvelope({
        clientCommandId:
          "phase12a-exact-command-0001",
      }),
    );

  assert(
    call.response.ok &&
      result(call).idempotentReplay === true,
    `Replay was not idempotent: ${call.text}`,
  );
  console.log("[4/12] PASS.");

  console.log("[5/12] Wrong authority epoch is rejected...");
  call =
    await callFunction(
      "matchSubmitIntent",
      guest.token,
      exactEnvelope({
        clientCommandId:
          "phase12a-stale-epoch-0001",
        observedAuthorityEpoch: 1,
      }),
    );

  assert(
    !call.response.ok &&
      errorKey(call) ===
        "match.error.stale_client_state",
    `Stale authority epoch was not rejected: ${call.text}`,
  );
  console.log("[5/12] PASS.");

  console.log("[6/12] Wrong revision is rejected...");
  call =
    await callFunction(
      "matchSubmitIntent",
      guest.token,
      exactEnvelope({
        clientCommandId:
          "phase12a-stale-revision-0001",
        observedRevision: 6,
      }),
    );

  assert(
    !call.response.ok &&
      errorKey(call) ===
        "match.error.stale_client_state",
    `Stale revision was not rejected: ${call.text}`,
  );
  console.log("[6/12] PASS.");

  console.log("[7/12] Wrong event sequence/phase are rejected...");
  call =
    await callFunction(
      "matchSubmitIntent",
      guest.token,
      exactEnvelope({
        clientCommandId:
          "phase12a-stale-sequence-0001",
        observedEventSequence: 2,
      }),
    );

  assert(
    !call.response.ok &&
      errorKey(call) ===
        "match.error.stale_client_state",
    `Stale event sequence was not rejected: ${call.text}`,
  );

  call =
    await callFunction(
      "matchSubmitIntent",
      guest.token,
      exactEnvelope({
        clientCommandId:
          "phase12a-stale-phase-0001",
        observedPhase: "movement",
      }),
    );

  assert(
    !call.response.ok &&
      errorKey(call) ===
        "match.error.stale_client_state",
    `Stale phase was not rejected: ${call.text}`,
  );
  console.log("[7/12] PASS.");

  console.log("[8/12] TemporaryBot cannot submit a fresh command...");
  call =
    await patchDoc(
      `matches/${matchId}/seats/seat_2`,
      {
        controllerKind:
          stringValue("temporary_bot"),
        connectionState:
          stringValue("reconnecting"),
      },
      [
        "controllerKind",
        "connectionState",
      ],
    );

  assert(call.response.ok, "TemporaryBot patch failed.");

  call =
    await callFunction(
      "matchSubmitIntent",
      guest.token,
      exactEnvelope({
        clientCommandId:
          "phase12a-bot-command-0001",
      }),
    );

  assert(
    !call.response.ok &&
      errorKey(call) ===
        "match.error.seat_not_active_human",
    `TemporaryBot command was not rejected: ${call.text}`,
  );

  await patchDoc(
    `matches/${matchId}/seats/seat_2`,
    {
      controllerKind:
        stringValue("human"),
      connectionState:
        stringValue("connected"),
    },
    [
      "controllerKind",
      "connectionState",
    ],
  );
  console.log("[8/12] PASS.");

  console.log("[9/12] Pending old-revision command becomes stale...");
  const oldCommand =
    "phase12a-pending-old-revision-0001";

  call =
    await callFunction(
      "matchSubmitIntent",
      guest.token,
      exactEnvelope({
        clientCommandId: oldCommand,
      }),
    );

  assert(call.response.ok, "Old pending seed failed.");

  await patchDoc(
    statePath,
    {
      revision: integerValue(8),
      eventSequence: integerValue(4),
      phase: stringValue("awaiting_roll"),
    },
    [
      "revision",
      "eventSequence",
      "phase",
    ],
  );

  await patchDoc(
    `matches/${matchId}`,
    {
      networkRevision: integerValue(8),
    },
    ["networkRevision"],
  );

  call =
    await callFunction(
      "matchHostListPendingIntents",
      host.token,
      {matchId},
    );

  assert(
    call.response.ok &&
      (result(call).intents ?? []).length === 0,
    `Stale pending intent leaked to Host: ${call.text}`,
  );

  const oldDoc =
    await getDoc(
      `matches/${matchId}/intents/` +
      intentDocId(guest.uid, oldCommand),
    );

  assert(
    oldDoc.response.ok &&
      oldDoc.json?.fields?.status?.stringValue === "stale",
    "Old-revision pending intent was not marked stale.",
  );
  console.log("[9/12] PASS.");

  console.log("[10/12] Current revision command reaches Host queue...");
  const currentCommand =
    "phase12a-current-revision-0001";

  call =
    await callFunction(
      "matchSubmitIntent",
      guest.token,
      exactEnvelope({
        clientCommandId: currentCommand,
        observedRevision: 8,
        observedEventSequence: 4,
      }),
    );

  assert(call.response.ok, `Current intent failed: ${call.text}`);

  call =
    await callFunction(
      "matchHostListPendingIntents",
      host.token,
      {matchId},
    );

  const currentIntents =
    result(call).intents ?? [];

  assert(
    call.response.ok &&
      currentIntents.length === 1 &&
      currentIntents[0].clientCommandId ===
        currentCommand,
    `Current command not isolated in queue: ${call.text}`,
  );
  console.log("[10/12] PASS.");

  console.log("[11/12] Seat transition stales an already-pending command...");
  const seatTransitionCommand =
    "phase12a-seat-transition-0001";

  call =
    await callFunction(
      "matchSubmitIntent",
      guest.token,
      exactEnvelope({
        clientCommandId:
          seatTransitionCommand,
        observedRevision: 8,
        observedEventSequence: 4,
      }),
    );

  assert(call.response.ok, "Seat transition seed failed.");

  await patchDoc(
    `matches/${matchId}/seats/seat_2`,
    {
      controllerKind:
        stringValue("temporary_bot"),
      connectionState:
        stringValue("reconnecting"),
    },
    [
      "controllerKind",
      "connectionState",
    ],
  );

  call =
    await callFunction(
      "matchHostListPendingIntents",
      host.token,
      {matchId},
    );

  // The earlier currentCommand may still be current; ACK it first if present.
  const remaining =
    result(call).intents ?? [];

  assert(
    !remaining.some((item) =>
      item.clientCommandId ===
        seatTransitionCommand
    ),
    "Intent from a seat that became TemporaryBot reached Host.",
  );

  const seatTransitionDoc =
    await getDoc(
      `matches/${matchId}/intents/` +
      intentDocId(
        guest.uid,
        seatTransitionCommand,
      ),
    );

  assert(
    seatTransitionDoc.response.ok &&
      seatTransitionDoc.json?.fields?.status?.stringValue === "stale",
    "Seat-transition intent was not marked stale.",
  );
  console.log("[11/12] PASS.");

  console.log("[12/12] Host ACK remains bounded/idempotent...");
  if (remaining.length > 0) {
    call =
      await callFunction(
        "matchHostAcknowledgeIntents",
        host.token,
        {
          matchId,
          intentIds:
            remaining.map((item) => item.intentId),
        },
      );

    assert(
      call.response.ok,
      `ACK failed: ${call.text}`,
    );
  }

  call =
    await callFunction(
      "matchHostListPendingIntents",
      host.token,
      {matchId},
    );

  assert(
    call.response.ok &&
      (result(call).intents ?? []).length === 0,
    "Pending queue was not clean after ACK/stale filtering.",
  );

  console.log("[12/12] PASS.");
  console.log("");
  console.log(
    "AtlasBoard Phase 12A Intent Epoch/Stale Command Local E2E PASSED 12/12.",
  );
} catch (error) {
  console.error("");
  console.error(
    "AtlasBoard Phase 12A Intent Epoch/Stale Command Local E2E FAILED.",
  );
  console.error(error);
  process.exitCode = 1;
} finally {
  console.log("[cleanup] Removing Phase 12A fixtures...");

  // Known command ids used by this test.
  const commands = [
    "phase12a-exact-command-0001",
    "phase12a-stale-epoch-0001",
    "phase12a-stale-revision-0001",
    "phase12a-stale-sequence-0001",
    "phase12a-stale-phase-0001",
    "phase12a-bot-command-0001",
    "phase12a-pending-old-revision-0001",
    "phase12a-current-revision-0001",
    "phase12a-seat-transition-0001",
  ];

  if (guest?.uid) {
    for (const command of commands) {
      await deleteDoc(
        `matches/${matchId}/intents/` +
        intentDocId(guest.uid, command),
      );
    }
  }

  await deleteDoc(
    `matches/${matchId}/network/state`,
  );
  await deleteDoc(
    `matches/${matchId}/seats/seat_1`,
  );
  await deleteDoc(
    `matches/${matchId}/seats/seat_2`,
  );
  await deleteDoc(
    `matches/${matchId}`,
  );

  await deleteAuthUser(host);
  await deleteAuthUser(guest);

  console.log("[cleanup] Done.");
}
