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
async function callFunction(name, token, data) {
  return postJson(callableUrl(name), {data}, {Authorization: `Bearer ${token}`});
}
function result(call) { return call.json?.result ?? call.json?.data; }
function errorKey(call) { return call.json?.error?.details?.errorKey ?? ""; }
function firestoreUrl(path) {
  return `${FIRESTORE_BASE}/v1/projects/${PROJECT_ID}/databases/(default)/documents/${path}`;
}
async function patchDoc(path, fields, updateMask = []) {
  const query = updateMask.length ? "?" + updateMask.map(
    f => `updateMask.fieldPaths=${encodeURIComponent(f)}`
  ).join("&") : "";
  return requestJson(firestoreUrl(path) + query, {
    method: "PATCH",
    headers: {"Content-Type": "application/json"},
    body: JSON.stringify({fields}),
  });
}
async function getDoc(path) { return requestJson(firestoreUrl(path)); }
async function deleteDoc(path) {
  try { await requestJson(firestoreUrl(path), {method: "DELETE"}); } catch {}
}
const stringValue = value => ({stringValue: value});
const integerValue = value => ({integerValue: String(value)});

async function verifyEmulators() {
  const hub = await requestJson(`${HUB_BASE}/emulators`);
  assert(hub.response.ok, "Emulator Hub is not reachable.");
  for (const name of ["auth", "functions", "firestore"]) {
    assert(hub.json?.[name], `Required emulator not running: ${name}`);
  }
}

async function createAuthUser(label) {
  const nonce = `${Date.now()}-${Math.floor(Math.random() * 1000000)}`;
  const email = `atlasboard.phase12b.${label}.${nonce}@example.com`;
  const password = `AtlasBoardPhase12B!${label}!${nonce}`;
  const call = await postJson(
    `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=${API_KEY}`,
    {email, password, returnSecureToken: true},
  );
  assert(call.response.ok, `${label} Auth signup failed: ${call.text}`);
  return {uid: call.json?.localId, token: call.json?.idToken};
}

async function seedAccount(user, displayName) {
  let call = await patchDoc(`users/${user.uid}`, {
    accountStatus: stringValue("active"),
    membershipTier: stringValue("normal"),
    schemaVersion: integerValue(1),
  });
  assert(call.response.ok, `users/${user.uid} seed failed.`);
  call = await patchDoc(`public_profiles/${user.uid}`, {
    displayName: stringValue(displayName),
    avatarId: stringValue(""),
    profileFrameId: stringValue(""),
    schemaVersion: integerValue(1),
  });
  assert(call.response.ok, `public_profiles/${user.uid} seed failed.`);
}

async function deleteAuthUser(user) {
  if (!user?.token) return;
  try {
    await postJson(
      `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/accounts:delete?key=${API_KEY}`,
      {idToken: user.token},
    );
  } catch {}
}

const versions = {
  gameVersion: "0.12b-reconnect-expiry",
  protocolVersion: 1,
  rulesVersion: 1,
  contentVersion: "1",
  regionId: "auto",
};
const settings = {
  mapId: "Turkey",
  themeId: "Classic Table",
  roundLimit: 20,
  maxPlayers: 2,
  balancedDevelopment: true,
  doublesEnabled: true,
  tripleDoublePenaltyEnabled: true,
};

let host, guest, lobbyId = "", matchId = "", roomCode = "";

try {
  console.log("AtlasBoard Phase 12B TemporaryBot Expiry Local E2E");
  console.log("[0/10] Emulator safety preflight...");
  await verifyEmulators();
  console.log("[0/10] PASS.");

  console.log("[1/10] Create accounts/profiles...");
  host = await createAuthUser("host");
  guest = await createAuthUser("guest");
  await seedAccount(host, "Phase 12B Host");
  await seedAccount(guest, "Phase 12B Guest");
  console.log("[1/10] PASS.");

  console.log("[2/10] Create/join/start real two-seat match...");
  let call = await callFunction("lobbyCreatePrivateRoom", host.token, {...settings, ...versions});
  assert(call.response.ok, `Lobby create failed: ${call.text}`);
  roomCode = result(call).roomCode;
  lobbyId = result(call).snapshot.lobbyId;

  call = await callFunction("lobbyJoinByCode", guest.token, {
    roomCode, password: "", idempotencyKey: "phase12b-join-0001", ...versions,
  });
  assert(call.response.ok, `Guest join failed: ${call.text}`);

  call = await callFunction("lobbySetReady", guest.token, {
    lobbyId, expectedSettingsRevision: result(call).snapshot.settingsRevision, ready: true,
  });
  assert(call.response.ok, `Guest ready failed: ${call.text}`);

  call = await callFunction("lobbyStartMatch", host.token, {
    lobbyId, expectedSettingsRevision: result(call).snapshot.settingsRevision,
  });
  assert(call.response.ok, `Start failed: ${call.text}`);
  matchId = result(call).snapshot.matchId;
  assert(matchId, "No matchId.");
  console.log("[2/10] PASS.");

  console.log("[3/10] Voluntary leave creates TemporaryBot lease...");
  call = await callFunction("matchLeaveActive", guest.token, {matchId});
  assert(call.response.ok, `Guest leave failed: ${call.text}`);
  let seat = await getDoc(`matches/${matchId}/seats/seat_2`);
  assert(
    seat.response.ok &&
    seat.json?.fields?.controllerKind?.stringValue === "temporary_bot" &&
    seat.json?.fields?.connectionState?.stringValue === "reconnecting",
    "Leave did not create TemporaryBot reservation."
  );
  console.log("[3/10] PASS.");

  console.log("[4/10] Valid non-expired reservation reclaims same seat...");
  await patchDoc(`matches/${matchId}/seats/seat_2`, {
    reconnectExpiresAtEpochMs: integerValue(Date.now() + 60_000),
  }, ["reconnectExpiresAtEpochMs"]);
  call = await callFunction("lobbyJoinByCode", guest.token, {
    roomCode, password: "", idempotencyKey: "phase12b-reclaim-valid-0001", ...versions,
  });
  assert(call.response.ok, `Valid reclaim failed: ${call.text}`);
  seat = await getDoc(`matches/${matchId}/seats/seat_2`);
  assert(
    seat.json?.fields?.controllerKind?.stringValue === "human" &&
    seat.json?.fields?.connectionState?.stringValue === "connected",
    "Valid reclaim did not restore Human control."
  );
  console.log("[4/10] PASS.");

  console.log("[5/10] Expired join is rejected AND conversion commits...");
  call = await callFunction("matchLeaveActive", guest.token, {matchId});
  assert(call.response.ok, `Second leave failed: ${call.text}`);
  await patchDoc(`matches/${matchId}/seats/seat_2`, {
    reconnectExpiresAtEpochMs: integerValue(Date.now() - 1000),
  }, ["reconnectExpiresAtEpochMs"]);
  call = await callFunction("lobbyJoinByCode", guest.token, {
    roomCode, password: "", idempotencyKey: "phase12b-reclaim-expired-0001", ...versions,
  });
  assert(!call.response.ok && errorKey(call) === "match.error.reconnect_expired",
    `Expired reclaim was not rejected: ${call.text}`);
  seat = await getDoc(`matches/${matchId}/seats/seat_2`);
  let member = await getDoc(`lobbies/${lobbyId}/members/seat_2`);
  assert(
    seat.json?.fields?.controllerKind?.stringValue === "permanent_bot" &&
    seat.json?.fields?.connectionState?.stringValue === "reconnect_expired" &&
    member.json?.fields?.controllerKind?.stringValue === "permanent_bot" &&
    member.json?.fields?.connectionState?.stringValue === "reconnect_expired",
    "Expired join did not persist PermanentBot on match+lobby."
  );
  console.log("[5/10] PASS.");

  console.log("[6/10] PermanentBot cannot be reclaimed later...");
  call = await callFunction("lobbyJoinByCode", guest.token, {
    roomCode, password: "", idempotencyKey: "phase12b-reclaim-after-expiry-0001", ...versions,
  });
  assert(!call.response.ok && errorKey(call) === "match.error.reconnect_expired",
    `PermanentBot was incorrectly reclaimable: ${call.text}`);
  console.log("[6/10] PASS.");

  console.log("[7/10] Host expiry sweep mirrors match + lobby...");
  await patchDoc(`matches/${matchId}/seats/seat_2`, {
    controllerKind: stringValue("temporary_bot"),
    connectionState: stringValue("reconnecting"),
    reconnectExpiresAtEpochMs: integerValue(Date.now() - 1000),
    removalReason: stringValue("voluntary_leave"),
  }, ["controllerKind", "connectionState", "reconnectExpiresAtEpochMs", "removalReason"]);
  await patchDoc(`lobbies/${lobbyId}/members/seat_2`, {
    controllerKind: stringValue("temporary_bot"),
    connectionState: stringValue("reconnecting"),
  }, ["controllerKind", "connectionState"]);
  call = await callFunction("matchHostExpireReconnects", host.token, {matchId});
  assert(call.response.ok, `Host expiry sweep failed: ${call.text}`);
  seat = await getDoc(`matches/${matchId}/seats/seat_2`);
  member = await getDoc(`lobbies/${lobbyId}/members/seat_2`);
  assert(
    seat.json?.fields?.controllerKind?.stringValue === "permanent_bot" &&
    member.json?.fields?.controllerKind?.stringValue === "permanent_bot",
    "Host sweep did not keep match/lobby lifecycle in sync."
  );
  console.log("[7/10] PASS.");

  console.log("[8/10] Host sweep preserves non-expired reservation...");
  await patchDoc(`matches/${matchId}/seats/seat_2`, {
    controllerKind: stringValue("temporary_bot"),
    connectionState: stringValue("reconnecting"),
    reconnectExpiresAtEpochMs: integerValue(Date.now() + 60_000),
    removalReason: stringValue("voluntary_leave"),
  }, ["controllerKind", "connectionState", "reconnectExpiresAtEpochMs", "removalReason"]);
  await patchDoc(`lobbies/${lobbyId}/members/seat_2`, {
    controllerKind: stringValue("temporary_bot"),
    connectionState: stringValue("reconnecting"),
  }, ["controllerKind", "connectionState"]);
  call = await callFunction("matchHostExpireReconnects", host.token, {matchId});
  assert(call.response.ok, `Future sweep failed: ${call.text}`);
  seat = await getDoc(`matches/${matchId}/seats/seat_2`);
  assert(seat.json?.fields?.controllerKind?.stringValue === "temporary_bot",
    "Non-expired reservation was prematurely expired.");
  console.log("[8/10] PASS.");

  console.log("[9/10] Valid reservation remains reclaimable after sweep...");
  call = await callFunction("lobbyJoinByCode", guest.token, {
    roomCode, password: "", idempotencyKey: "phase12b-reclaim-after-sweep-0001", ...versions,
  });
  assert(call.response.ok, `Valid reclaim after sweep failed: ${call.text}`);
  seat = await getDoc(`matches/${matchId}/seats/seat_2`);
  assert(seat.json?.fields?.controllerKind?.stringValue === "human",
    "Seat did not reclaim after safe future sweep.");
  console.log("[9/10] PASS.");

  console.log("[10/10] Already-connected Human join is idempotent...");
  call = await callFunction("lobbyJoinByCode", guest.token, {
    roomCode, password: "", idempotencyKey: "phase12b-duplicate-connected-0001", ...versions,
  });
  assert(call.response.ok && result(call).idempotentReplay === true,
    `Connected Human replay failed: ${call.text}`);
  console.log("[10/10] PASS.");

  console.log("");
  console.log("AtlasBoard Phase 12B TemporaryBot Expiry Local E2E PASSED 10/10.");
} catch (error) {
  console.error("");
  console.error("AtlasBoard Phase 12B TemporaryBot Expiry Local E2E FAILED.");
  console.error(error);
  process.exitCode = 1;
} finally {
  console.log("[cleanup] Removing Phase 12B fixtures...");
  if (matchId) {
    await deleteDoc(`matches/${matchId}/network/state`);
    for (let i = 1; i <= 4; i++) await deleteDoc(`matches/${matchId}/seats/seat_${i}`);
    await deleteDoc(`matches/${matchId}`);
  }
  if (lobbyId) {
    for (let i = 1; i <= 4; i++) await deleteDoc(`lobbies/${lobbyId}/members/seat_${i}`);
    await deleteDoc(`lobbies/${lobbyId}`);
  }
  if (host?.uid) {
    await deleteDoc(`users/${host.uid}`);
    await deleteDoc(`public_profiles/${host.uid}`);
  }
  if (guest?.uid) {
    await deleteDoc(`users/${guest.uid}`);
    await deleteDoc(`public_profiles/${guest.uid}`);
  }
  await deleteAuthUser(host);
  await deleteAuthUser(guest);
  console.log("[cleanup] Done.");
}
