import {createHmac} from "node:crypto";

const PROJECT_ID = "atlasboard-usa";
const REGION = "europe-west1";
const AUTH_BASE = "http://127.0.0.1:9099";
const FUNCTIONS_BASE = "http://127.0.0.1:5001";
const FIRESTORE_BASE = "http://127.0.0.1:8080";
const HUB_BASE = "http://127.0.0.1:4400";
const API_KEY = "atlasboard-local-emulator-only";
const TIMEOUT_MS = 10000;
const LOCAL_JOIN_CODE_PEPPER =
  "atlasboard-local-emulator-lobby-code-pepper-v1";

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

async function patchJson(url, body) {
  return requestJson(url, {
    method: "PATCH",
    headers: {"Content-Type": "application/json"},
    body: JSON.stringify(body),
  });
}

async function deleteUrl(url) {
  return requestJson(url, {method: "DELETE"});
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

function firestoreUrl(path) {
  return `${FIRESTORE_BASE}/v1/projects/${PROJECT_ID}` +
    `/databases/(default)/documents/${path}`;
}

async function getDoc(path) {
  return requestJson(firestoreUrl(path));
}

async function patchDoc(path, fields) {
  return patchJson(firestoreUrl(path), {fields});
}

async function bestEffortDelete(path) {
  if (!path) return;
  try {
    const call = await deleteUrl(firestoreUrl(path));
    if (!call.response.ok && call.response.status !== 404) {
      console.warn(`Cleanup warning ${path}: HTTP ${call.response.status}`);
    }
  } catch (error) {
    console.warn(`Cleanup warning ${path}:`, error);
  }
}

function codeHash(code) {
  return createHmac("sha256", LOCAL_JOIN_CODE_PEPPER)
    .update(`atlasboard:lobby-code:${code}`, "utf8")
    .digest("hex");
}

async function cleanupLobby(lobbyId, roomCode = "") {
  if (!lobbyId) return;

  for (let i = 0; i < 4; i++) {
    await bestEffortDelete(`lobbies/${lobbyId}/members/seat_${i + 1}`);
  }

  for (const user of [remoteA, remoteB]) {
    if (user?.uid) {
      await bestEffortDelete(`lobbies/${lobbyId}/kicks/${user.uid}`);
    }
  }

  await bestEffortDelete(`lobby_discovery/${lobbyId}`);
  await bestEffortDelete(`lobbies/${lobbyId}`);

  if (roomCode) {
    await bestEffortDelete(`join_codes/${codeHash(roomCode)}`);
  }
}

async function cleanupMatch(id) {
  if (!id) return;
  for (let i = 0; i < 4; i++) {
    await bestEffortDelete(`matches/${id}/seats/seat_${i + 1}`);
  }
  await bestEffortDelete(`matches/${id}`);
}

async function verifyEmulators() {
  const hub = await requestJson(`${HUB_BASE}/emulators`);
  assert(hub.response.ok, "Emulator Hub is not reachable.");
  for (const name of ["auth", "functions", "firestore"]) {
    assert(hub.json?.[name], `Required emulator not running: ${name}`);
  }
}

async function createAuthUser(label) {
  const nonce = `${Date.now()}-${Math.floor(Math.random() * 1000000)}`;
  const email = `atlasboard.phase4a.${label}.${nonce}@example.com`;
  const password = `AtlasBoardPhase4A!${label}!${nonce}`;
  const call = await postJson(
    `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=${API_KEY}`,
    {email, password, returnSecureToken: true},
  );
  assert(call.response.ok, `${label} Auth signup failed: ${call.text}`);
  return {uid: call.json?.localId, token: call.json?.idToken};
}

async function seedAccount(user, displayName) {
  let call = await patchDoc(`users/${user.uid}`, {
    accountStatus: {stringValue: "active"},
    membershipTier: {stringValue: "normal"},
    schemaVersion: {integerValue: "1"},
  });
  assert(call.response.ok, `users/${user.uid} seed failed.`);
  call = await patchDoc(`public_profiles/${user.uid}`, {
    displayName: {stringValue: displayName},
    avatarId: {stringValue: ""},
    profileFrameId: {stringValue: ""},
    schemaVersion: {integerValue: "1"},
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
  } catch (error) {
    console.warn("Auth cleanup warning:", error);
  }
}

function rooms(call) {
  return result(call)?.rooms ?? [];
}

function room(cards, lobbyId) {
  return cards.find((card) => card.lobbyId === lobbyId);
}

const versions = {
  gameVersion: "0.4a-public-discovery-local",
  protocolVersion: 1,
  rulesVersion: 1,
  contentVersion: "1",
  regionId: "auto",
};

const settings = {
  mapId: "Turkey",
  themeId: "Classic Table",
  roundLimit: 20,
  maxPlayers: 4,
  balancedDevelopment: true,
  doublesEnabled: true,
  tripleDoublePenaltyEnabled: true,
};

let host;
let remoteA;
let remoteB;
let browser;
let privateLobbyId = "";
let publicLobbyId = "";
let privateRoomCode = "";
let publicRoomCode = "";
let matchId = "";

try {
  console.log("AtlasBoard Phase 4A Public Lobby Discovery Local E2E v1");
  console.log("Safety: localhost Auth/Firestore/Functions emulators only.");

  console.log("[0/11] Verifying emulator safety preflight...");
  await verifyEmulators();
  console.log("[0/11] PASSED. Required emulators detected.");

  console.log("[1/11] Creating temporary canonical accounts...");
  host = await createAuthUser("host");
  remoteA = await createAuthUser("remote-a");
  remoteB = await createAuthUser("remote-b");
  browser = await createAuthUser("browser");
  await seedAccount(host, "Public Host");
  await seedAccount(remoteA, "Remote A");
  await seedAccount(remoteB, "Remote B");
  await seedAccount(browser, "Browser Player");
  console.log("[1/11] PASSED. Accounts/profile state seeded.");

  console.log("[2/11] Proving private rooms never enter discovery...");
  const privateCreate = await callFunction(
    "lobbyCreatePrivateRoom", host.token, {...settings, ...versions});
  assert(privateCreate.response.ok, `Private create failed: ${privateCreate.text}`);
  const privateResult = result(privateCreate);
  privateLobbyId = privateResult.snapshot?.lobbyId;
  privateRoomCode = privateResult.roomCode;
  const listAfterPrivate = await callFunction(
    "lobbyListPublicRooms", browser.token, {...versions, limit: 20});
  assert(listAfterPrivate.response.ok, `Public list failed: ${listAfterPrivate.text}`);
  assert(!room(rooms(listAfterPrivate), privateLobbyId),
    "Private lobby leaked into public discovery.");
  const privateDiscovery = await getDoc(`lobby_discovery/${privateLobbyId}`);
  assert(privateDiscovery.response.status === 404,
    "Private lobby unexpectedly created a discovery document.");
  console.log("[2/11] PASSED. Private lobby is undiscoverable.");

  console.log("[3/11] Creating public lobby and sanitized browser card...");
  const publicCreate = await callFunction(
    "lobbyCreatePublicRoom", host.token, {...settings, ...versions});
  assert(publicCreate.response.ok, `Public create failed: ${publicCreate.text}`);
  const publicResult = result(publicCreate);
  publicLobbyId = publicResult.snapshot?.lobbyId;
  publicRoomCode = publicResult.roomCode;
  assert(publicResult.snapshot?.visibility === "public",
    "Public lobby snapshot visibility is not public.");

  const initialList = await callFunction(
    "lobbyListPublicRooms", browser.token, {...versions, limit: 20});
  assert(initialList.response.ok, `Initial public list failed: ${initialList.text}`);
  const initialCard = room(rooms(initialList), publicLobbyId);
  assert(initialCard, "Public lobby missing from browser list.");
  assert(initialCard.hostDisplayName === "Public Host", "Host display name missing.");
  assert(initialCard.occupiedPlayers === 1, "Initial occupied count should be 1.");
  assert(initialCard.openOnlineSeatCount === 3, "Initial open count should be 3.");
  const serializedCard = JSON.stringify(initialCard);
  for (const forbidden of ["roomCode", "joinCodeHash", "accountId", "readyForRevision"]) {
    assert(!serializedCard.includes(forbidden), `Browser card leaked ${forbidden}.`);
  }
  const discoveryDoc = await getDoc(`lobby_discovery/${publicLobbyId}`);
  assert(discoveryDoc.response.ok, "Public discovery document missing.");
  const discoveryText = JSON.stringify(discoveryDoc.json);
  assert(!discoveryText.includes(publicRoomCode), "Raw room code leaked into discovery doc.");
  assert(!discoveryText.includes("joinCodeHash"), "joinCodeHash leaked into discovery doc.");
  console.log("[3/11] PASSED. Sanitized public browser card created.");

  console.log("[4/11] Host seat policy changes update discovery counts...");
  const configure = await callFunction(
    "lobbyConfigureSeats", host.token,
    {
      lobbyId: publicLobbyId,
      expectedSettingsRevision: 1,
      maxPlayers: 4,
      seatPolicies: ["local_human", "bot", "online", "online"],
    },
  );
  assert(configure.response.ok, `Configure failed: ${configure.text}`);
  const listAfterConfigure = await callFunction(
    "lobbyListPublicRooms", browser.token, {...versions, limit: 20});
  const configuredCard = room(rooms(listAfterConfigure), publicLobbyId);
  assert(configuredCard?.occupiedPlayers === 2, "Bot was not reflected as occupied.");
  assert(configuredCard?.openOnlineSeatCount === 2, "Open seat count did not become 2.");
  assert(configuredCard?.settingsRevision === 2, "Discovery revision did not update.");
  console.log("[4/11] PASSED. Host seat policy projection synchronized.");

  console.log("[5/11] Remote joins update discovery atomically...");
  const joinA = await callFunction(
    "lobbyJoinByCode", remoteA.token,
    {roomCode: publicRoomCode, idempotencyKey: "phase4a-join-a", ...versions},
  );
  assert(joinA.response.ok, `Remote A join failed: ${joinA.text}`);
  const afterJoinA = await callFunction(
    "lobbyListPublicRooms", browser.token, {...versions, limit: 20});
  const joinCard = room(rooms(afterJoinA), publicLobbyId);
  assert(joinCard?.occupiedPlayers === 3, "Remote join occupied count not synchronized.");
  assert(joinCard?.openOnlineSeatCount === 1, "Remote join open count not synchronized.");
  console.log("[5/11] PASSED. Remote join projection synchronized.");

  console.log("[6/11] Full public lobby disappears from joinable browser...");
  const joinB = await callFunction(
    "lobbyJoinByCode", remoteB.token,
    {roomCode: publicRoomCode, idempotencyKey: "phase4a-join-b", ...versions},
  );
  assert(joinB.response.ok, `Remote B join failed: ${joinB.text}`);
  const fullList = await callFunction(
    "lobbyListPublicRooms", browser.token, {...versions, limit: 20});
  assert(!room(rooms(fullList), publicLobbyId),
    "Full lobby remained in joinable browser list.");
  console.log("[6/11] PASSED. Full room hidden from joinable discovery.");

  console.log("[7/11] Voluntary leave makes public room discoverable again...");
  const leaveB = await callFunction(
    "lobbyLeaveRoom", remoteB.token, {lobbyId: publicLobbyId});
  assert(leaveB.response.ok, `Remote B leave failed: ${leaveB.text}`);
  const reopenedList = await callFunction(
    "lobbyListPublicRooms", browser.token, {...versions, limit: 20});
  const reopenedCard = room(rooms(reopenedList), publicLobbyId);
  assert(reopenedCard, "Room did not reappear after an Online seat reopened.");
  assert(reopenedCard.openOnlineSeatCount === 1, "Reopened seat count is incorrect.");
  console.log("[7/11] PASSED. Reopened public room became discoverable.");

  console.log("[8/11] Rule changes update browser metadata...");
  const currentRevision = result(leaveB).snapshot?.settingsRevision;
  const update = await callFunction(
    "lobbyUpdateSettings", host.token,
    {
      lobbyId: publicLobbyId,
      expectedSettingsRevision: currentRevision,
      mapId: "Colorado",
      themeId: "Classic Table",
      roundLimit: 30,
      balancedDevelopment: true,
      doublesEnabled: true,
      tripleDoublePenaltyEnabled: true,
    },
  );
  assert(update.response.ok, `Settings update failed: ${update.text}`);
  const settingsList = await callFunction(
    "lobbyListPublicRooms", browser.token, {...versions, limit: 20});
  const updatedCard = room(rooms(settingsList), publicLobbyId);
  assert(updatedCard?.mapId === "Colorado", "Map projection did not update.");
  assert(updatedCard?.roundLimit === 30, "Round projection did not update.");
  console.log("[8/11] PASSED. Public browser metadata synchronized.");

  console.log("[9/11] Incompatible clients do not see the room...");
  const incompatible = await callFunction(
    "lobbyListPublicRooms", browser.token,
    {...versions, contentVersion: "incompatible-content", limit: 20},
  );
  assert(incompatible.response.ok, `Incompatible list failed: ${incompatible.text}`);
  assert(!room(rooms(incompatible), publicLobbyId),
    "Version-incompatible room leaked into browser results.");
  console.log("[9/11] PASSED. Compatibility filtering works.");

  console.log("[10/11] Ready actual RemoteHuman then authoritative Start...");
  const startRevision = result(update).snapshot?.settingsRevision;
  const readyA = await callFunction(
    "lobbySetReady", remoteA.token,
    {lobbyId: publicLobbyId, expectedSettingsRevision: startRevision, ready: true},
  );
  assert(readyA.response.ok, `Remote A Ready failed: ${readyA.text}`);
  const start = await callFunction(
    "lobbyStartMatch", host.token,
    {lobbyId: publicLobbyId, expectedSettingsRevision: startRevision},
  );
  assert(start.response.ok, `Public Start failed: ${start.text}`);
  matchId = result(start).snapshot?.matchId;
  console.log("[10/11] PASSED. Public lobby entered authoritative Starting.");

  console.log("[11/11] Starting room disappears and discovery doc is removed...");
  const afterStartList = await callFunction(
    "lobbyListPublicRooms", browser.token, {...versions, limit: 20});
  assert(!room(rooms(afterStartList), publicLobbyId),
    "Starting public lobby remained in browser list.");
  const afterStartDoc = await getDoc(`lobby_discovery/${publicLobbyId}`);
  assert(afterStartDoc.response.status === 404,
    "Starting public lobby discovery document was not deleted.");
  console.log("[11/11] PASSED. Starting public lobby removed from discovery.");

  console.log("\nAtlasBoard Phase 4A Public Lobby Discovery Local E2E v1 PASSED.");
  console.log(
    "Verified: private rooms never discoverable; public sanitized card; " +
    "seat/join/leave/settings projection sync; full rooms hidden; compatible " +
    "filtering; Starting removes discovery; no room-code/hash leakage.",
  );
} catch (error) {
  console.error("\nAtlasBoard Phase 4A Public Lobby Discovery Local E2E v1 FAILED.");
  console.error(error);
  process.exitCode = 1;
} finally {
  console.log("[cleanup] Cleaning temporary Phase 4A emulator data...");
  await cleanupMatch(matchId);
  await cleanupLobby(publicLobbyId, publicRoomCode);
  await cleanupLobby(privateLobbyId, privateRoomCode);
  await bestEffortDelete(host?.uid ? `users/${host.uid}` : "");
  await bestEffortDelete(host?.uid ? `public_profiles/${host.uid}` : "");
  await bestEffortDelete(remoteA?.uid ? `users/${remoteA.uid}` : "");
  await bestEffortDelete(remoteA?.uid ? `public_profiles/${remoteA.uid}` : "");
  await bestEffortDelete(remoteB?.uid ? `users/${remoteB.uid}` : "");
  await bestEffortDelete(remoteB?.uid ? `public_profiles/${remoteB.uid}` : "");
  await bestEffortDelete(browser?.uid ? `users/${browser.uid}` : "");
  await bestEffortDelete(browser?.uid ? `public_profiles/${browser.uid}` : "");
  await deleteAuthUser(host);
  await deleteAuthUser(remoteA);
  await deleteAuthUser(remoteB);
  await deleteAuthUser(browser);
  console.log("[cleanup] Cleanup finished.");
}
