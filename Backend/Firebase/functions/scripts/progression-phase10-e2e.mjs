import {createHash} from "node:crypto";

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
    try { json = JSON.parse(text); } catch { json = {raw: text}; }
  }
  return {response, json, text};
}

async function postJson(url, body, headers = {}) {
  return requestJson(url, {
    method: "POST",
    headers: {"Content-Type": "application/json", ...headers},
    body: JSON.stringify(body),
  });
}

function callableUrl(name) {
  return `${FUNCTIONS_BASE}/${PROJECT_ID}/${REGION}/${name}`;
}

async function call(name, token, data = {}) {
  return postJson(
    callableUrl(name),
    {data},
    {Authorization: `Bearer ${token}`},
  );
}

function result(callable) {
  return callable.json?.result ?? callable.json?.data;
}

function docUrl(path) {
  return `${FIRESTORE_BASE}/v1/projects/${PROJECT_ID}` +
    `/databases/(default)/documents/${path}`;
}

async function patch(path, fields) {
  const response = await requestJson(docUrl(path), {
    method: "PATCH",
    headers: {"Content-Type": "application/json"},
    body: JSON.stringify({fields}),
  });
  assert(response.response.ok, `Firestore seed failed ${path}: ${response.text}`);
}

async function get(path) {
  return requestJson(docUrl(path));
}

async function remove(path) {
  try {
    await requestJson(docUrl(path), {method: "DELETE"});
  } catch {
    // Best-effort local cleanup only.
  }
}

function s(value) { return {stringValue: value}; }
function i(value) { return {integerValue: String(value)}; }
function b(value) { return {booleanValue: value}; }
function t(value) { return {timestampValue: value}; }

function sha256(value) {
  return createHash("sha256").update(value, "utf8").digest("hex");
}

async function signup(label, nonce) {
  const response = await postJson(
    `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=${API_KEY}`,
    {
      email: `atlasboard.progression.${label}.${nonce}@example.com`,
      password: `AtlasProgression!${label}!${nonce}`,
      returnSecureToken: true,
    },
  );
  assert(response.response.ok, `Auth sign-up failed (${label}): ${response.text}`);
  return {
    token: response.json?.idToken,
    uid: response.json?.localId,
  };
}

console.log("Atlas Board Phase 10 Progression + Achievements E2E");
console.log("Safety: local Auth/Firestore/Functions emulators only.");

const hub = await requestJson(`${HUB_BASE}/emulators`);
assert(hub.response.ok, "Emulator Hub unavailable.");
for (const name of ["auth", "firestore", "functions"]) {
  assert(hub.json?.[name], `Required emulator not running: ${name}`);
}
console.log("[1/14] PASS emulator safety preflight.");

const nonce = `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`;
const host = await signup("host", nonce);
const guest = await signup("guest", nonce);
assert(host.token && host.uid && guest.token && guest.uid, "Auth identities incomplete.");
console.log("[2/14] PASS two authenticated account identities.");

await patch(`public_profiles/${host.uid}`, {
  uid: s(host.uid),
  displayName: s("Phase10 Host"),
  avatarId: s("avatar_host"),
});
await patch(`public_profiles/${guest.uid}`, {
  uid: s(guest.uid),
  displayName: s("Phase10 Guest"),
  avatarId: s("avatar_guest"),
});

const matchId = `phase10_match_${nonce}`;
const lobbyId = `phase10_lobby_${nonce}`;
await patch(`matches/${matchId}`, {
  matchId: s(matchId),
  lobbyId: s(lobbyId),
  hostAccountId: s(host.uid),
  mode: s("online"),
  status: s("running"),
  mapId: s("Turkey"),
  themeId: s("classic_table"),
  roundLimit: i(20),
  maxPlayers: i(2),
});

await patch(`matches/${matchId}/seats/seat_host`, {
  seatId: s("seat_host"),
  slotIndex: i(0),
  active: b(true),
  seatType: s("human"),
  accountId: s(host.uid),
  localOwnerAccountId: s(host.uid),
  displayName: s("Phase10 Host"),
  controllerKind: s("local_human"),
  isHost: b(true),
});
await patch(`matches/${matchId}/seats/seat_guest`, {
  seatId: s("seat_guest"),
  slotIndex: i(1),
  active: b(true),
  seatType: s("human"),
  accountId: s(guest.uid),
  localOwnerAccountId: s(""),
  displayName: s("Phase10 Guest"),
  controllerKind: s("human"),
  isHost: b(false),
});

const finalFrame = {
  schemaVersion: 7,
  phase: "match_complete",
  activeSlotIndex: 0,
  currentRound: 8,
  roundLimit: 20,
  diceSequence: 7,
  matchResult: {
    valid: true,
    highestNetWorth: 5200,
    winnerSlots: [0],
    participating: [true, true, false, false],
    bankrupt: [false, false, false, false],
    cash: [2400, 1300, 0, 0],
    propertyCount: [2, 1, 0, 0],
    propertyValue: [1600, 900, 0, 0],
    developmentLevels: [3, 1, 0, 0],
    developmentValue: [1200, 400, 0, 0],
    netWorth: [5200, 2600, 0, 0],
  },
};

await patch(`matches/${matchId}/network/state`, {
  revision: i(18),
  phase: s("match_complete"),
  turnSeatId: s("seat_host"),
  eventSequence: i(44),
  snapshotJson: s(JSON.stringify(finalFrame)),
  networkSchemaVersion: i(1),
});
console.log("[3/14] PASS canonical completed match fixture seeded.");

const denied = await call(
  "progressionRecordCompletedMatch",
  guest.token,
  {
    matchId,
    completedTurns: 14,
    telemetry: [],
  },
);
assert(!denied.response.ok, "Non-host progression finalization unexpectedly succeeded.");
assert(
  denied.json?.error?.details?.errorKey === "progression.error.host_required",
  `Unexpected non-host error: ${denied.text}`,
);
console.log("[4/14] PASS non-host finalization rejected.");

const finalize = await call(
  "progressionRecordCompletedMatch",
  host.token,
  {
    matchId,
    completedTurns: 14,
    telemetry: [
      {slotIndex: 0, diceRolls: 4, doublesRolled: 1, totalDiceValue: 30, highestRoll: 10},
      {slotIndex: 1, diceRolls: 3, doublesRolled: 0, totalDiceValue: 17, highestRoll: 8},
    ],
  },
);
assert(finalize.response.ok, `Host finalization failed: ${finalize.text}`);
const finalResult = result(finalize);
assert(finalResult?.ok === true && finalResult?.applied === true,
  "Completed match was not applied.");
assert(finalResult?.affectedAccountCount === 2,
  "Expected both human accounts to receive progression.");
console.log("[5/14] PASS host finalized progression for every human account.");

const replay = await call(
  "progressionRecordCompletedMatch",
  host.token,
  {
    matchId,
    completedTurns: 14,
    telemetry: [],
  },
);
assert(replay.response.ok && result(replay)?.idempotentReplay === true,
  "Completed-match replay was not idempotent.");
console.log("[6/14] PASS completed-match replay is idempotent.");

const hostProfileCall = await call("progressionGetProfile", host.token);
assert(hostProfileCall.response.ok, `Host profile failed: ${hostProfileCall.text}`);
const hostProfile = result(hostProfileCall);
assert(hostProfile?.stats?.matchesPlayed === 1, "Host match count mismatch.");
assert(hostProfile?.stats?.wins === 1 && hostProfile?.stats?.losses === 0,
  "Host result stats mismatch.");
assert(hostProfile?.stats?.diceRolls === 4 && hostProfile?.stats?.doublesRolled === 1,
  "Host dice telemetry mismatch.");
assert(hostProfile?.stats?.propertiesAtFinishTotal === 2,
  "Host property aggregate mismatch.");
assert(hostProfile?.stats?.developmentLevelsAtFinishTotal === 3,
  "Host development aggregate mismatch.");
const turkeyStats = (hostProfile?.mapStats ?? [])
  .find((item) => item?.mapId === "Turkey")?.stats;
assert(turkeyStats?.matchesPlayed === 1 && turkeyStats?.wins === 1,
  "Turkey-scoped result statistics mismatch.");
assert(turkeyStats?.diceRolls === 4 && turkeyStats?.highestNetWorth === 5200,
  "Turkey-scoped gameplay/record statistics mismatch.");
console.log(
  "[7/14] PASS cumulative + per-map player statistics persisted.",
);

assert(hostProfile?.totalXp === 118, `Host XP mismatch: ${hostProfile?.totalXp}`);
assert(hostProfile?.level === 1 && hostProfile?.nextLevelXp === 500,
  "Host level metadata mismatch.");
console.log("[8/14] PASS account XP and player level derived server-side.");

const unlockedIds = new Set(
  (hostProfile?.achievements ?? [])
    .filter((item) => item.unlocked === true)
    .map((item) => item.achievementId),
);
for (const id of ["first_match", "first_win", "turkey_explorer"]) {
  assert(unlockedIds.has(id), `Expected host achievement missing: ${id}`);
}
console.log("[9/14] PASS achievement unlocks derived from completed match stats.");

assert(Array.isArray(hostProfile?.recentMatches) && hostProfile.recentMatches.length === 1,
  "Expected one retained host match.");
const retained = hostProfile.recentMatches[0];
assert(retained.result === "win" && retained.place === 1,
  "Host history result/place mismatch.");
assert(retained.netWorth === 5200 && retained.xpAwarded === 118,
  "Host history economy/XP mismatch.");
assert(Array.isArray(retained.participants) && retained.participants.length === 2,
  "Match details did not retain both participants.");
console.log("[10/14] PASS match history keeps result, lobby/rules and detail evidence.");

const guestProfileCall = await call("progressionGetProfile", guest.token);
assert(guestProfileCall.response.ok, `Guest profile failed: ${guestProfileCall.text}`);
const guestProfile = result(guestProfileCall);
assert(guestProfile?.stats?.matchesPlayed === 1 && guestProfile?.stats?.losses === 1,
  "Guest match/loss stats mismatch.");
assert(guestProfile?.recentMatches?.[0]?.result === "loss",
  "Guest history result mismatch.");
console.log("[11/14] PASS every participating account receives its own result/history.");

const now = Date.now();
const oldId = `phase10_old_${nonce}`;
await patch(`account_match_history/${host.uid}/matches/${oldId}`, {
  uid: s(host.uid),
  matchId: s(oldId),
  result: s("loss"),
  finishedAt: t(new Date(now - (4 * 24 * 60 * 60 * 1000)).toISOString()),
});

const fakeIds = [];
for (let index = 0; index < 11; index++) {
  const id = `phase10_recent_${index}_${nonce}`;
  fakeIds.push(id);
  await patch(`account_match_history/${host.uid}/matches/${id}`, {
    uid: s(host.uid),
    matchId: s(id),
    result: s(index % 2 === 0 ? "win" : "loss"),
    finishedAt: t(new Date(now - (index + 1) * 60_000).toISOString()),
  });
}

const prunedProfileCall = await call("progressionGetProfile", host.token);
assert(prunedProfileCall.response.ok, `Pruned profile failed: ${prunedProfileCall.text}`);
const prunedProfile = result(prunedProfileCall);
assert(prunedProfile?.recentMatches?.length === 10,
  `History cap mismatch: ${prunedProfile?.recentMatches?.length}`);
const oldLookup = await get(`account_match_history/${host.uid}/matches/${oldId}`);
assert(oldLookup.response.status === 404, "History older than 72 hours was not deleted.");
console.log("[12/14] PASS 72-hour history retention enforced even below/above cap.");

const allReturned = prunedProfile.recentMatches.map((item) => item.matchId);
assert(allReturned.length === 10 && new Set(allReturned).size === 10,
  "Newest-ten history response is not unique/capped.");
console.log("[13/14] PASS user-facing history is limited to newest 10 records.");

const marker = await get(`progression_match_events/${matchId}`);
const hostHistory = await get(`account_match_history/${host.uid}/matches/${matchId}`);
const guestHistory = await get(`account_match_history/${guest.uid}/matches/${matchId}`);
assert(marker.response.ok, "Immutable progression match event missing.");
assert(hostHistory.response.status === 404 || hostHistory.response.ok,
  "Unexpected host history lookup state after retention pruning.");
assert(guestHistory.response.ok, "Guest retained match history missing.");
console.log("[14/14] PASS canonical progression evidence and account history persisted.");

// Best-effort cleanup of this E2E's known fixture documents.
for (const id of [oldId, ...fakeIds, matchId]) {
  await remove(`account_match_history/${host.uid}/matches/${id}`);
}
await remove(`account_match_history/${guest.uid}/matches/${matchId}`);
await remove(`account_progression/${host.uid}`);
await remove(`account_progression/${guest.uid}`);
await remove(`progression_match_events/${matchId}`);
await remove(`public_profiles/${host.uid}`);
await remove(`public_profiles/${guest.uid}`);
for (const uid of [host.uid, guest.uid]) {
  for (const achievementId of [
    "first_match",
    "first_win",
    "networth_5000",
    "turkey_explorer",
  ]) {
    await remove(
      `achievement_events/${sha256(`${matchId}:${uid}:${achievementId}`)}`,
    );
  }
}
await remove(`matches/${matchId}/network/state`);
await remove(`matches/${matchId}/seats/seat_host`);
await remove(`matches/${matchId}/seats/seat_guest`);
await remove(`matches/${matchId}`);

console.log("Phase 10 Progression + Achievements E2E PASS: 14 checks.");
console.log("Cleanup: temporary Phase 10 E2E fixtures removed.");
