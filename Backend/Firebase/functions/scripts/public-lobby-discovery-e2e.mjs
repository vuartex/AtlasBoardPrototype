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

function callableUrl(name) {
  return `${FUNCTIONS_BASE}/${PROJECT_ID}/${REGION}/${name}`;
}

// Phase 15F protects online callables with App Check when explicitly enabled
// in the local Functions Emulator. Its emulator accepts a non-empty placeholder
// in lieu of production attestation, as proven by the existing Phase 15F/15G
// negative/authorization suites. This value is NOT a real App Check token.
// Do not send it to any non-loopback URL or deployed Firebase environment.
const LOCAL_APP_CHECK_PLACEHOLDER = "phase16i-local-emulator-only";

async function callFunction(name, token, data) {
  const url = callableUrl(name);
  const target = new URL(url);
  assert(
    target.protocol === "http:" &&
    target.hostname === "127.0.0.1" &&
    target.port === "5001" &&
    target.pathname.startsWith(`/${PROJECT_ID}/${REGION}/`),
    "Refusing emulator-only App Check header outside local Functions.",
  );
  assert(typeof token === "string" && token.length > 20,
    `Missing emulator Firebase ID token for callable ${name}.`);
  return postJson(
    url,
    {data},
    {
      Authorization: `Bearer ${token}`,
      "X-Firebase-AppCheck": LOCAL_APP_CHECK_PLACEHOLDER,
    },
  );
}

function result(call) {
  return call.json?.result ?? call.json?.data;
}

function firestoreUrl(path) {
  return `${FIRESTORE_BASE}/v1/projects/${PROJECT_ID}` +
    `/databases/(default)/documents/${path}`;
}

// Firestore Security Rules intentionally deny direct document writes. The
// literal `owner` identity is an emulator-only administrative sentinel.
// Restrict it to this fixed loopback Firestore endpoint; never send it to
// deployed services or to Auth / Callable endpoints (those use real ID tokens).
async function firestoreAdminRequest(path, method, fields) {
  const url = firestoreUrl(path);
  const parsed = new URL(url);
  assert(
    parsed.protocol === "http:" &&
    parsed.hostname === "127.0.0.1" &&
    parsed.port === "8080" &&
    parsed.pathname.startsWith(
      `/v1/projects/${PROJECT_ID}/databases/(default)/documents/`,
    ),
    "Refusing to send emulator administrator credential outside local Firestore.",
  );
  return requestJson(url, {
    method,
    headers: {
      Authorization: "Bearer owner",
      ...(fields === undefined ? {} : {"Content-Type": "application/json"}),
    },
    ...(fields === undefined ? {} : {body: JSON.stringify({fields})}),
  });
}

async function getDoc(path) {
  return firestoreAdminRequest(path, "GET");
}

async function patchDoc(path, fields) {
  return firestoreAdminRequest(path, "PATCH", fields);
}

async function bestEffortDelete(path) {
  if (!path) return;
  try {
    const call = await firestoreAdminRequest(path, "DELETE");
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
  assert(call.response.ok,
    `users/${user.uid} seed failed: HTTP ${call.response.status}; ${call.text}`);
  call = await patchDoc(`public_profiles/${user.uid}`, {
    displayName: {stringValue: displayName},
    avatarId: {stringValue: ""},
    profileFrameId: {stringValue: ""},
    schemaVersion: {integerValue: "1"},
  });
  assert(call.response.ok,
    `public_profiles/${user.uid} seed failed: HTTP ${call.response.status}; ${call.text}`);
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

function discoveryFields({
  lobbyId,
  gameVersion,
  regionId,
  createdAtEpochMs,
  hostHeartbeatAtEpochMs,
  joinable = true,
}) {
  return {
    schemaVersion: {integerValue: "1"},
    lobbyId: {stringValue: lobbyId},
    visibility: {stringValue: "public"},
    lifecycleState: {stringValue: "waiting"},
    joinable: {booleanValue: joinable},
    hostDisplayName: {stringValue: `Scale ${lobbyId.slice(-8)}`},
    mapId: {stringValue: "Turkey"},
    themeId: {stringValue: "Classic Table"},
    roundLimit: {integerValue: "20"},
    maxPlayers: {integerValue: "4"},
    occupiedPlayers: {integerValue: "1"},
    openOnlineSeatCount: {integerValue: "3"},
    regionId: {stringValue: regionId},
    gameVersion: {stringValue: gameVersion},
    protocolVersion: {integerValue: "1"},
    rulesVersion: {integerValue: "1"},
    contentVersion: {stringValue: "1"},
    settingsRevision: {integerValue: "1"},
    hasPassword: {booleanValue: false},
    createdAtEpochMs: {integerValue: String(createdAtEpochMs)},
    hostHeartbeatAtEpochMs: {integerValue: String(hostHeartbeatAtEpochMs)},
    updatedAtEpochMs: {integerValue: String(hostHeartbeatAtEpochMs)},
  };
}

function cursorOf(call) {
  return result(call)?.nextCursor ?? "";
}

function hasMore(call) {
  return result(call)?.hasMore === true;
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
let closingHost;
let privateLobbyId = "";
let publicLobbyId = "";
let privateRoomCode = "";
let publicRoomCode = "";
let matchId = "";
let closingLobbyId = "";
let closingRoomCode = "";
const syntheticDiscoveryIds = [];

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

  console.log("[16I-1/6] Seeding >50 ordered discovery docs for cursor scan...");
  // The backend enforces a 32-character gameVersion maximum.
  const scaleRun = `${Date.now().toString(36)}-` +
    Math.floor(Math.random() * 1000000).toString(36);
  const scaleVersions = {
    gameVersion: `16i-${scaleRun}`,
    protocolVersion: 1,
    rulesVersion: 1,
    contentVersion: "1",
    regionId: "eur3",
  };
  const incompatibleVersion = `16i-bad-${scaleRun}`;
  const baseCreatedAt = Date.now() + 100000;
  // Artificial projections have no living host. Future-dated heartbeat keeps
  // them stable across slower Windows machines; the separate stale fixture
  // below uses an explicitly expired timestamp.
  const heartbeatNow = Date.now() + 5 * 60 * 1000;
  const expectedCompatible = [];

  for (let index = 0; index < 60; index++) {
    const lobbyId = `phase16i-${scaleRun}-incompatible-${String(index).padStart(3, "0")}`;
    syntheticDiscoveryIds.push(lobbyId);
    const write = await patchDoc(
      `lobby_discovery/${lobbyId}`,
      discoveryFields({
        lobbyId,
        gameVersion: incompatibleVersion,
        regionId: "eur3",
        createdAtEpochMs: baseCreatedAt - index,
        hostHeartbeatAtEpochMs: heartbeatNow,
      }),
    );
    assert(write.response.ok, `Synthetic incompatible seed failed: ${lobbyId}`);
  }

  for (let index = 0; index < 3; index++) {
    const lobbyId = `phase16i-${scaleRun}-wrong-region-${String(index).padStart(3, "0")}`;
    syntheticDiscoveryIds.push(lobbyId);
    const write = await patchDoc(
      `lobby_discovery/${lobbyId}`,
      discoveryFields({
        lobbyId,
        gameVersion: scaleVersions.gameVersion,
        regionId: "usa",
        createdAtEpochMs: baseCreatedAt - 1000 - index,
        hostHeartbeatAtEpochMs: heartbeatNow,
      }),
    );
    assert(write.response.ok, `Synthetic region seed failed: ${lobbyId}`);
  }

  const staleLobbyId = `phase16i-${scaleRun}-stale`;
  syntheticDiscoveryIds.push(staleLobbyId);
  const staleWrite = await patchDoc(
    `lobby_discovery/${staleLobbyId}`,
    discoveryFields({
      lobbyId: staleLobbyId,
      gameVersion: scaleVersions.gameVersion,
      regionId: "eur3",
      createdAtEpochMs: baseCreatedAt - 1100,
      hostHeartbeatAtEpochMs: Date.now() - 120000,
    }),
  );
  assert(staleWrite.response.ok, "Synthetic stale seed failed.");

  for (let index = 0; index < 23; index++) {
    const lobbyId = `phase16i-${scaleRun}-compatible-${String(index).padStart(3, "0")}`;
    syntheticDiscoveryIds.push(lobbyId);
    const createdAtEpochMs = index < 2
      ? baseCreatedAt - 2000
      : baseCreatedAt - 2000 - index;
    expectedCompatible.push({lobbyId, createdAtEpochMs});
    const write = await patchDoc(
      `lobby_discovery/${lobbyId}`,
      discoveryFields({
        lobbyId,
        gameVersion: scaleVersions.gameVersion,
        regionId: "eur3",
        createdAtEpochMs,
        hostHeartbeatAtEpochMs: heartbeatNow,
      }),
    );
    assert(write.response.ok, `Synthetic compatible seed failed: ${lobbyId}`);
  }

  expectedCompatible.sort((left, right) =>
    right.createdAtEpochMs - left.createdAtEpochMs ||
    right.lobbyId.localeCompare(left.lobbyId));
  console.log("[16I-1/6] PASSED. Scale dataset seeded.");

  console.log("[16I-2/6] Proving first page scans past the old raw-50 boundary...");
  const firstPage = await callFunction(
    "lobbyListPublicRoomsPage",
    browser.token,
    {...scaleVersions, limit: 7, cursor: ""},
  );
  assert(firstPage.response.ok, `First cursor page failed: ${firstPage.text}`);
  const firstIds = rooms(firstPage).map((card) => card.lobbyId);
  assert(firstIds.length === 7, "Cursor first page did not return 7 compatible rooms.");
  assert(firstIds.every((id) => id.includes("-compatible-")),
    "Incompatible/region/stale document leaked into cursor page.");
  assert((result(firstPage)?.diagnostics?.scannedDocuments ?? 0) > 50,
    "Cursor discovery did not scan beyond the old first-50 raw boundary.");
  assert(hasMore(firstPage) && cursorOf(firstPage),
    "First cursor page did not advertise a continuation cursor.");

  const firstPageRepeat = await callFunction(
    "lobbyListPublicRoomsPage",
    browser.token,
    {...scaleVersions, limit: 7, cursor: ""},
  );
  assert(firstPageRepeat.response.ok, `Repeated first page failed: ${firstPageRepeat.text}`);
  assert(JSON.stringify(firstIds) ===
    JSON.stringify(rooms(firstPageRepeat).map((card) => card.lobbyId)),
  "Stable dataset first page ordering is not deterministic.");
  console.log("[16I-2/6] PASSED. Old raw-50 invisibility removed deterministically.");

  console.log("[16I-3/6] Walking every cursor page with no duplicate or stable-data skip...");
  const collectedIds = [];
  let page = firstPage;
  let pageGuard = 0;
  while (true) {
    for (const card of rooms(page)) {
      collectedIds.push(card.lobbyId);
    }
    if (!hasMore(page)) break;
    assert(cursorOf(page), "hasMore=true without nextCursor.");
    page = await callFunction(
      "lobbyListPublicRoomsPage",
      browser.token,
      {...scaleVersions, limit: 7, cursor: cursorOf(page)},
    );
    assert(page.response.ok, `Next cursor page failed: ${page.text}`);
    pageGuard++;
    assert(pageGuard < 20, "Cursor page walk exceeded safety guard.");
  }

  const uniqueCollected = new Set(collectedIds);
  const expectedIds = expectedCompatible.map((entry) => entry.lobbyId);
  assert(uniqueCollected.size === collectedIds.length,
    "Duplicate LobbyId returned across cursor pages.");
  assert(JSON.stringify(collectedIds) === JSON.stringify(expectedIds),
    "Cursor pagination skipped or reordered stable compatible rooms.");
  assert(result(page)?.hasMore === false,
    "Final cursor page did not return hasMore=false.");
  console.log("[16I-3/6] PASSED. Stable pagination has no duplicate/skip.");

  console.log("[16I-4/6] Rejecting tampered cursor and filtering stale/region state...");
  const validCursor = cursorOf(firstPage);
  const cursorParts = validCursor.split(".");
  assert(cursorParts.length === 2, "Valid cursor shape was unexpected.");
  const tamperedSignature =
    (cursorParts[1].startsWith("A") ? "B" : "A") +
    cursorParts[1].slice(1);
  const tamperedCursor = `${cursorParts[0]}.${tamperedSignature}`;
  const tampered = await callFunction(
    "lobbyListPublicRoomsPage",
    browser.token,
    {...scaleVersions, limit: 7, cursor: tamperedCursor},
  );
  assert(!tampered.response.ok,
    "Tampered public-lobby cursor was unexpectedly accepted.");
  assert(tampered.json?.error?.details?.errorKey === "lobby.error.invalid_request",
    `Tampered cursor returned wrong public error: ${tampered.text}`);
  assert(!collectedIds.includes(staleLobbyId),
    "Stale discovery document leaked into paginated results.");
  const staleAfterRead = await getDoc(`lobby_discovery/${staleLobbyId}`);
  assert(staleAfterRead.response.status === 404,
    "Stale discovery cleanup did not remove orphaned stale projection.");
  console.log("[16I-4/6] PASSED. Cursor validation, region and stale filtering are safe.");

  console.log("[16I-5/6] Concurrent first-page reads remain deterministic...");
  const concurrent = await Promise.all(
    Array.from({length: 8}, () =>
      callFunction(
        "lobbyListPublicRoomsPage",
        browser.token,
        {...scaleVersions, limit: 7, cursor: ""},
      )),
  );
  for (const call of concurrent) {
    assert(call.response.ok, `Concurrent discovery failed: ${call.text}`);
    assert(JSON.stringify(rooms(call).map((card) => card.lobbyId)) ===
      JSON.stringify(firstIds),
    "Concurrent first-page result was not deterministic.");
  }
  console.log("[16I-5/6] PASSED. Concurrent discovery reads are deterministic locally.");

  console.log("[16I-6/6] Closed-between-page-and-Join is rejected authoritatively...");
  closingHost = await createAuthUser("closing-host");
  await seedAccount(closingHost, "Closing Host");
  const closingCreate = await callFunction(
    "lobbyCreatePublicRoom",
    closingHost.token,
    {...settings, ...versions},
  );
  assert(closingCreate.response.ok, `Closing-room create failed: ${closingCreate.text}`);
  closingLobbyId = result(closingCreate).snapshot?.lobbyId;
  closingRoomCode = result(closingCreate).roomCode;

  const beforeClose = await callFunction(
    "lobbyListPublicRoomsPage",
    browser.token,
    {...versions, limit: 20, cursor: ""},
  );
  assert(beforeClose.response.ok, `Pre-close cursor list failed: ${beforeClose.text}`);
  assert(room(rooms(beforeClose), closingLobbyId),
    "Closing-room fixture was not visible before close.");

  // The canonical host action is lobbyCloseRoom. lobbyLeaveRoom intentionally
  // rejects host exits because host migration is a separate operation.
  const close = await callFunction(
    "lobbyCloseRoom",
    closingHost.token,
    {lobbyId: closingLobbyId},
  );
  assert(close.response.ok, `Host close failed: ${close.text}`);
  assert(result(close)?.applied === true &&
    result(close)?.snapshot?.lifecycleState === "closed",
  "Host close did not transition the authoritative lobby to Closed.");

  const discoveryAfterClose = await getDoc(`lobby_discovery/${closingLobbyId}`);
  assert(discoveryAfterClose.response.status === 404,
    "Closed lobby projection still exists in discovery.");
  const listAfterClose = await callFunction(
    "lobbyListPublicRoomsPage",
    browser.token,
    {...versions, limit: 20, cursor: ""},
  );
  assert(listAfterClose.response.ok, `Post-close cursor list failed: ${listAfterClose.text}`);
  assert(!room(rooms(listAfterClose), closingLobbyId),
    "Closed room remained visible after authoritative host close.");

  const joinClosed = await callFunction(
    "lobbyJoinPublicRoom",
    browser.token,
    {
      lobbyId: closingLobbyId,
      password: "",
      idempotencyKey: `phase16i-closed-join-${scaleRun}`,
      ...versions,
    },
  );
  assert(!joinClosed.response.ok,
    "Browser card remained authoritative after room closed.");
  assert(joinClosed.json?.error?.status === "FAILED_PRECONDITION" &&
    joinClosed.json?.error?.details?.errorKey === "lobby.error.not_joinable",
  `Closed-room Join returned unexpected error: ${joinClosed.text}`);
  console.log("[16I-6/6] PASSED. Join transaction rejects changed/closed room state.");

  console.log("\nAtlasBoard Phase 4A + Phase 16I Public Lobby Discovery E2E PASSED.");
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
  await cleanupLobby(closingLobbyId, closingRoomCode);
  for (const lobbyId of syntheticDiscoveryIds) {
    await bestEffortDelete(`lobby_discovery/${lobbyId}`);
  }
  await bestEffortDelete(host?.uid ? `users/${host.uid}` : "");
  await bestEffortDelete(host?.uid ? `public_profiles/${host.uid}` : "");
  await bestEffortDelete(remoteA?.uid ? `users/${remoteA.uid}` : "");
  await bestEffortDelete(remoteA?.uid ? `public_profiles/${remoteA.uid}` : "");
  await bestEffortDelete(remoteB?.uid ? `users/${remoteB.uid}` : "");
  await bestEffortDelete(remoteB?.uid ? `public_profiles/${remoteB.uid}` : "");
  await bestEffortDelete(browser?.uid ? `users/${browser.uid}` : "");
  await bestEffortDelete(browser?.uid ? `public_profiles/${browser.uid}` : "");
  await bestEffortDelete(closingHost?.uid ? `users/${closingHost.uid}` : "");
  await bestEffortDelete(closingHost?.uid ? `public_profiles/${closingHost.uid}` : "");
  await deleteAuthUser(host);
  await deleteAuthUser(remoteA);
  await deleteAuthUser(remoteB);
  await deleteAuthUser(browser);
  await deleteAuthUser(closingHost);
  console.log("[cleanup] Cleanup finished.");
}
