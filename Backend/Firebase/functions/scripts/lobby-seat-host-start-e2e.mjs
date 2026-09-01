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
  if (!condition) {
    throw new Error(message);
  }
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

async function patchJson(url, body) {
  return requestJson(url, {
    method: "PATCH",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(body),
  });
}

async function deleteUrl(url) {
  return requestJson(url, {method: "DELETE"});
}

function callableUrl(functionName) {
  return `${FUNCTIONS_BASE}/${PROJECT_ID}/${REGION}/${functionName}`;
}

async function callFunction(functionName, idToken, data) {
  return postJson(
    callableUrl(functionName),
    {data},
    {Authorization: `Bearer ${idToken}`},
  );
}

function result(call) {
  return call.json?.result ?? call.json?.data;
}

function firestoreDocumentUrl(path) {
  return `${FIRESTORE_BASE}/v1/projects/${PROJECT_ID}` +
    `/databases/(default)/documents/${path}`;
}

async function getDoc(path) {
  return requestJson(firestoreDocumentUrl(path));
}

async function patchDoc(path, fields) {
  return patchJson(firestoreDocumentUrl(path), {fields});
}

async function bestEffortDelete(path) {
  if (!path) {
    return;
  }

  try {
    const deletion = await deleteUrl(firestoreDocumentUrl(path));
    if (!deletion.response.ok && deletion.response.status !== 404) {
      console.warn(
        `Cleanup warning ${path}: HTTP ${deletion.response.status}`,
      );
    }
  } catch (error) {
    console.warn(`Cleanup warning ${path}:`, error);
  }
}

function firestoreString(document, fieldName) {
  return document?.fields?.[fieldName]?.stringValue;
}

function firestoreBoolean(document, fieldName) {
  return document?.fields?.[fieldName]?.booleanValue;
}

function codeHash(code) {
  return createHmac("sha256", LOCAL_JOIN_CODE_PEPPER)
    .update(`atlasboard:lobby-code:${code}`, "utf8")
    .digest("hex");
}

async function verifyEmulators() {
  const hub = await requestJson(`${HUB_BASE}/emulators`);
  assert(hub.response.ok, "Emulator Hub is not reachable.");

  const emulators = hub.json ?? {};
  for (const name of ["auth", "functions", "firestore"]) {
    assert(emulators[name], `Required emulator not running: ${name}.`);
  }
}

async function createAuthUser(label) {
  const nonce = `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`;
  const email = `atlasboard.lobby3d3a.${label}.${nonce}@example.com`;
  const password = `AtlasBoardLobby3D3A!${label}!${nonce}`;

  const call = await postJson(
    `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=${API_KEY}`,
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

async function seedAccount(user, displayName, status = "active") {
  const userDoc = await patchDoc(`users/${user.uid}`, {
    accountStatus: {stringValue: status},
    membershipTier: {stringValue: "normal"},
    schemaVersion: {integerValue: "1"},
  });

  assert(
    userDoc.response.ok,
    `users/${user.uid} seed failed.`,
  );

  const profileDoc = await patchDoc(`public_profiles/${user.uid}`, {
    displayName: {stringValue: displayName},
    avatarId: {stringValue: ""},
    profileFrameId: {stringValue: ""},
    schemaVersion: {integerValue: "1"},
  });

  assert(
    profileDoc.response.ok,
    `public_profiles/${user.uid} seed failed.`,
  );
}

async function deleteAuthUser(user) {
  if (!user?.token) {
    return;
  }

  try {
    await postJson(
      `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/accounts:delete?key=${API_KEY}`,
      {idToken: user.token},
    );
  } catch (error) {
    console.warn("Auth cleanup warning:", error);
  }
}

async function expectError(call, status, errorKey, label) {
  assert(!call.response.ok, `${label} unexpectedly succeeded.`);
  assert(
    call.json?.error?.status === status,
    `${label}: expected ${status}, got ${call.json?.error?.status}. ` +
      call.text,
  );
  assert(
    call.json?.error?.details?.errorKey === errorKey,
    `${label}: expected errorKey=${errorKey}, got ` +
      `${call.json?.error?.details?.errorKey}.`,
  );
}

function member(snapshot, seatId) {
  return snapshot?.members?.find((item) => item.seatId === seatId);
}


const versions = {
  gameVersion: "0.3d3b-lobby-safety-local",
  protocolVersion: 1,
  rulesVersion: 1,
  contentVersion: "1",
  regionId: "auto",
};

const createSettings = {
  mapId: "Turkey",
  themeId: "Classic Table",
  roundLimit: 20,
  maxPlayers: 4,
  balancedDevelopment: true,
  doublesEnabled: true,
  tripleDoublePenaltyEnabled: true,
};

let host;
let remote;
let extra;
const cleanupLobbies = [];
const cleanupMatches = [];
let testPassed = false;

async function cleanupLobby(lobbyId, roomCode = "") {
  if (!lobbyId) {
    return;
  }

  for (let i = 0; i < 4; i++) {
    await bestEffortDelete(`lobbies/${lobbyId}/members/seat_${i + 1}`);
  }

  if (remote?.uid) {
    await bestEffortDelete(`lobbies/${lobbyId}/kicks/${remote.uid}`);
  }

  if (extra?.uid) {
    await bestEffortDelete(`lobbies/${lobbyId}/kicks/${extra.uid}`);
  }

  await bestEffortDelete(`lobbies/${lobbyId}`);

  if (roomCode) {
    await bestEffortDelete(`join_codes/${codeHash(roomCode)}`);
  }
}

async function cleanupMatch(matchId) {
  if (!matchId) {
    return;
  }

  for (let i = 0; i < 4; i++) {
    await bestEffortDelete(`matches/${matchId}/seats/seat_${i + 1}`);
  }

  await bestEffortDelete(`matches/${matchId}`);
}

console.log("AtlasBoard Lobby Safety + Start Fill Local E2E v1");
console.log(
  "Safety: localhost Auth/Firestore/Functions emulators only; " +
  "all room, account, kick, seat, and match data are temporary.",
);

try {
  console.log("[0/16] Verifying emulator safety preflight...");
  await verifyEmulators();
  console.log("[0/16] PASSED. Auth/Firestore/Functions emulators detected.");

  console.log("[1/16] Creating temporary canonical accounts...");
  host = await createAuthUser("host");
  remote = await createAuthUser("remote");
  extra = await createAuthUser("extra");
  await seedAccount(host, "Host Player");
  await seedAccount(remote, "Remote Player");
  await seedAccount(extra, "Extra Player");
  console.log("[1/16] PASSED. Accounts/profile state seeded.");

  console.log("[2/16] Creating first room with an unresolved Online seat...");
  const firstCreateCall = await callFunction(
    "lobbyCreatePrivateRoom",
    host.token,
    {
      ...createSettings,
      ...versions,
    },
  );
  assert(firstCreateCall.response.ok, `Create failed: ${firstCreateCall.text}`);
  const firstCreate = result(firstCreateCall);
  const firstLobbyId = firstCreate.snapshot?.lobbyId;
  const firstRoomCode = firstCreate.roomCode;
  cleanupLobbies.push({lobbyId: firstLobbyId, roomCode: firstRoomCode});
  assert(firstLobbyId && /^\d{6}$/.test(firstRoomCode),
    "First room identity/code invalid.");
  console.log("[2/16] PASSED. First private room created.");

  console.log("[3/16] Configuring Local + Bot + OpenOnline roster...");
  const firstConfigureCall = await callFunction(
    "lobbyConfigureSeats",
    host.token,
    {
      lobbyId: firstLobbyId,
      expectedSettingsRevision: 1,
      maxPlayers: 4,
      seatPolicies: ["local_human", "local_human", "bot", "online"],
    },
  );
  assert(firstConfigureCall.response.ok,
    `Configure failed: ${firstConfigureCall.text}`);
  const firstConfigured = result(firstConfigureCall);
  assert(firstConfigured.snapshot?.settingsRevision === 2,
    "First configure revision mismatch.");
  assert(member(firstConfigured.snapshot, "seat_4")?.seatMode === "open_online",
    "seat_4 should be OpenOnline before Start.");
  console.log("[3/16] PASSED. Mixed roster stored with one unresolved slot.");

  console.log("[4/16] Starting without waiting for unresolved Online slot...");
  const firstStartCall = await callFunction(
    "lobbyStartMatch",
    host.token,
    {
      lobbyId: firstLobbyId,
      expectedSettingsRevision: 2,
    },
  );
  assert(firstStartCall.response.ok,
    `Start-with-open failed: ${firstStartCall.text}`);
  const firstStart = result(firstStartCall);
  assert(firstStart.started === true, "First Start did not report started.");
  assert(firstStart.snapshot?.lifecycleState === "starting",
    "First lobby did not enter Starting.");
  assert(member(firstStart.snapshot, "seat_4")?.seatMode === "bot",
    "Unresolved seat_4 was not auto-filled as Bot.");
  assert(
    Number.isSafeInteger(firstStart.snapshot?.startCountdownEndsAtEpochMs) &&
      firstStart.snapshot.startCountdownEndsAtEpochMs > Date.now() &&
      firstStart.snapshot.startCountdownEndsAtEpochMs <= Date.now() + 10000,
    "Backend shared countdown deadline is invalid.",
  );
  cleanupMatches.push(firstStart.snapshot?.matchId);
  console.log("[4/16] PASSED. OpenOnline auto-filled as Bot at Start.");

  console.log("[5/16] Verifying auto-filled match seat and code closure...");
  const firstMatchSeat4 = await getDoc(
    `matches/${firstStart.snapshot.matchId}/seats/seat_4`,
  );
  assert(firstMatchSeat4.response.ok, "Auto-filled match seat_4 missing.");
  assert(firestoreString(firstMatchSeat4.json, "seatMode") === "bot",
    "Auto-filled match seat_4 mode is not bot.");
  const firstCodeDoc = await getDoc(`join_codes/${codeHash(firstRoomCode)}`);
  assert(firstCodeDoc.response.ok, "First join-code doc missing.");
  assert(firestoreBoolean(firstCodeDoc.json, "active") === true,
    "Protected room-code lookup did not survive Start.");
  assert(firestoreBoolean(firstCodeDoc.json, "lookupActive") === true,
    "Protected reconnect lookup is not active after Start.");
  assert(firestoreBoolean(firstCodeDoc.json, "joinOpen") === false,
    "Normal lobby Join remained open after Start.");
  assert(
    firestoreString(firstCodeDoc.json, "matchId") ===
      firstStart.snapshot.matchId,
    "Started join-code lookup was not linked to the authoritative matchId.",
  );

  const startedNormalJoin = await callFunction(
    "lobbyJoinByCode",
    extra.token,
    {
      roomCode: firstRoomCode,
      idempotencyKey: "normal-join-after-start-must-fail",
      ...versions,
    },
  );
  await expectError(
    startedNormalJoin,
    "FAILED_PRECONDITION",
    "lobby.error.not_joinable",
    "Normal Join against started room",
  );

  console.log(
    "[5/16] PASSED. Match bootstrap + closed normal Join + retained reconnect lookup verified.",
  );

  console.log("[6/16] Creating second room for remote-seat guard/kick...");
  const secondCreateCall = await callFunction(
    "lobbyCreatePrivateRoom",
    host.token,
    {
      ...createSettings,
      ...versions,
    },
  );
  assert(secondCreateCall.response.ok,
    `Second create failed: ${secondCreateCall.text}`);
  const secondCreate = result(secondCreateCall);
  const secondLobbyId = secondCreate.snapshot?.lobbyId;
  const secondRoomCode = secondCreate.roomCode;
  cleanupLobbies.push({lobbyId: secondLobbyId, roomCode: secondRoomCode});
  console.log("[6/16] PASSED. Second private room created.");

  console.log("[7/16] Reserving a high slot for a real RemoteHuman...");
  const secondConfigureCall = await callFunction(
    "lobbyConfigureSeats",
    host.token,
    {
      lobbyId: secondLobbyId,
      expectedSettingsRevision: 1,
      maxPlayers: 4,
      seatPolicies: ["local_human", "bot", "online", "online"],
    },
  );
  assert(secondConfigureCall.response.ok,
    `Second configure failed: ${secondConfigureCall.text}`);
  const secondConfigured = result(secondConfigureCall);
  const joinCall = await callFunction(
    "lobbyJoinByCode",
    remote.token,
    {
      roomCode: secondRoomCode,
      idempotencyKey: "remote-join-high-slot",
      ...versions,
    },
  );
  assert(joinCall.response.ok, `Remote join failed: ${joinCall.text}`);
  const joined = result(joinCall);
  assert(member(joined.snapshot, "seat_3")?.seatMode === "remote_human",
    "Remote did not reserve expected seat_3.");
  assert(member(joined.snapshot, "seat_3")?.accountId === remote.uid,
    "Remote account mismatch on seat_3.");
  console.log("[7/16] PASSED. Real remote player occupies removable range.");

  console.log("[8/16] Verifying unready remote still blocks Start...");
  const unreadyStart = await callFunction(
    "lobbyStartMatch",
    host.token,
    {
      lobbyId: secondLobbyId,
      expectedSettingsRevision: 2,
    },
  );
  await expectError(
    unreadyStart,
    "FAILED_PRECONDITION",
    "lobby.error.remote_player_not_ready",
    "Unready remote Start",
  );
  console.log("[8/16] PASSED. Remote Ready gate preserved.");

  console.log("[9/16] Rejecting Player Count shrink that would remove remote...");
  const illegalShrink = await callFunction(
    "lobbyConfigureSeats",
    host.token,
    {
      lobbyId: secondLobbyId,
      expectedSettingsRevision: 2,
      maxPlayers: 2,
      seatPolicies: ["local_human", "bot", "inactive", "inactive"],
    },
  );
  await expectError(
    illegalShrink,
    "FAILED_PRECONDITION",
    "lobby.error.remote_seat_occupied",
    "Shrink through occupied RemoteHuman",
  );
  console.log("[9/16] PASSED. Occupied remote seat cannot be silently removed.");

  console.log("[10/16] Enforcing host-only kick authority...");
  const guestKick = await callFunction(
    "lobbyKickMember",
    remote.token,
    {
      lobbyId: secondLobbyId,
      expectedSettingsRevision: 2,
      slotIndex: 2,
    },
  );
  await expectError(
    guestKick,
    "PERMISSION_DENIED",
    "lobby.error.host_only",
    "Guest kick",
  );
  console.log("[10/16] PASSED. Guest cannot kick.");

  console.log("[11/16] Host removes remote and clears concrete seat...");
  const hostKick = await callFunction(
    "lobbyKickMember",
    host.token,
    {
      lobbyId: secondLobbyId,
      expectedSettingsRevision: 2,
      slotIndex: 2,
    },
  );
  assert(hostKick.response.ok, `Host kick failed: ${hostKick.text}`);
  const kicked = result(hostKick);
  assert(kicked.snapshot?.settingsRevision === 3,
    "Kick did not increment settingsRevision.");
  assert(member(kicked.snapshot, "seat_3")?.seatMode === "open_online",
    "Kicked seat_3 did not reset to OpenOnline.");
  assert(!member(kicked.snapshot, "seat_3")?.accountId,
    "Kicked seat retained accountId.");
  console.log("[11/16] PASSED. Remote data removed and seat reopened.");

  console.log("[12/16] Kicked account receives stable removal state...");
  const kickedSnapshot = await callFunction(
    "lobbyGetSnapshot",
    remote.token,
    {lobbyId: secondLobbyId},
  );
  await expectError(
    kickedSnapshot,
    "PERMISSION_DENIED",
    "lobby.error.kicked",
    "Kicked snapshot",
  );
  const kickedRejoin = await callFunction(
    "lobbyJoinByCode",
    remote.token,
    {
      roomCode: secondRoomCode,
      idempotencyKey: "kicked-rejoin",
      ...versions,
    },
  );
  await expectError(
    kickedRejoin,
    "PERMISSION_DENIED",
    "lobby.error.kicked",
    "Kicked rejoin",
  );
  console.log("[12/16] PASSED. Kicked account cannot silently rejoin.");

  console.log("[13/16] Shrinking after kick removes Bot/Open slot data...");
  const legalShrink = await callFunction(
    "lobbyConfigureSeats",
    host.token,
    {
      lobbyId: secondLobbyId,
      expectedSettingsRevision: 3,
      maxPlayers: 2,
      seatPolicies: ["local_human", "bot", "inactive", "inactive"],
    },
  );
  assert(legalShrink.response.ok, `Legal shrink failed: ${legalShrink.text}`);
  const shrunk = result(legalShrink);
  assert(shrunk.snapshot?.settingsRevision === 4,
    "Legal shrink revision mismatch.");
  assert(member(shrunk.snapshot, "seat_3")?.seatMode === "inactive",
    "Removed seat_3 is not inactive.");
  assert(member(shrunk.snapshot, "seat_4")?.seatMode === "inactive",
    "Removed seat_4 is not inactive.");
  console.log("[13/16] PASSED. Non-remote removed slots cleared from Firebase state.");

  console.log("[14/16] Starting final Host + Bot roster...");
  const secondStartCall = await callFunction(
    "lobbyStartMatch",
    host.token,
    {
      lobbyId: secondLobbyId,
      expectedSettingsRevision: 4,
    },
  );
  assert(secondStartCall.response.ok,
    `Final Start failed: ${secondStartCall.text}`);
  const secondStart = result(secondStartCall);
  assert(secondStart.started === true, "Final Start did not start.");
  assert(secondStart.snapshot?.lifecycleState === "starting",
    "Final lobby did not enter Starting.");
  cleanupMatches.push(secondStart.snapshot?.matchId);
  console.log("[14/16] PASSED. Host + Bot roster starts without waiting.");

  testPassed = true;
} catch (error) {
  console.error("\nAtlasBoard Lobby Safety + Start Fill Local E2E v1 FAILED.");
  console.error(error);
  process.exitCode = 1;
} finally {
  console.log("[15/16] Creating third room for voluntary guest leave...");
  const thirdCreateCall = await callFunction(
    "lobbyCreatePrivateRoom",
    host.token,
    {
      ...createSettings,
      ...versions,
    },
  );
  assert(thirdCreateCall.response.ok,
    `Third create failed: ${thirdCreateCall.text}`);
  const thirdCreate = result(thirdCreateCall);
  const thirdLobbyId = thirdCreate.snapshot?.lobbyId;
  const thirdRoomCode = thirdCreate.roomCode;
  cleanupLobbies.push({lobbyId: thirdLobbyId, roomCode: thirdRoomCode});

  const thirdJoinCall = await callFunction(
    "lobbyJoinByCode",
    extra.token,
    {
      roomCode: thirdRoomCode,
      idempotencyKey: "voluntary-leave-first-join",
      ...versions,
    },
  );
  assert(thirdJoinCall.response.ok,
    `Voluntary leave setup join failed: ${thirdJoinCall.text}`);
  assert(member(result(thirdJoinCall).snapshot, "seat_2")?.accountId === extra.uid,
    "Voluntary leave setup remote did not occupy seat_2.");
  console.log("[15/16] PASSED. Voluntary-leave room remote joined.");

  console.log("[16/16] Voluntary leave clears presence and allows normal rejoin...");
  const leaveCall = await callFunction(
    "lobbyLeaveRoom",
    extra.token,
    {lobbyId: thirdLobbyId},
  );
  assert(leaveCall.response.ok,
    `Voluntary leave failed: ${leaveCall.text}`);
  const left = result(leaveCall);
  assert(left.applied === true,
    "Voluntary leave did not report applied.");
  assert(member(left.snapshot, "seat_2")?.seatMode === "open_online",
    "Voluntary leave did not reopen seat_2.");
  assert(member(left.snapshot, "seat_2")?.accountId === "",
    "Voluntary leave retained account data.");

  const voluntaryRejoin = await callFunction(
    "lobbyJoinByCode",
    extra.token,
    {
      roomCode: thirdRoomCode,
      idempotencyKey: "voluntary-leave-second-join",
      ...versions,
    },
  );
  assert(voluntaryRejoin.response.ok,
    `Voluntary rejoin should be allowed: ${voluntaryRejoin.text}`);
  assert(member(result(voluntaryRejoin).snapshot, "seat_2")?.accountId === extra.uid,
    "Voluntary rejoin did not reclaim seat_2.");
  console.log("[16/16] PASSED. Voluntary leave and rejoin contract verified.");

  console.log("[cleanup] Cleaning temporary lobby emulator data...");

  for (const matchId of cleanupMatches) {
    await cleanupMatch(matchId);
  }

  for (const item of cleanupLobbies) {
    await cleanupLobby(item.lobbyId, item.roomCode);
  }

  for (const user of [host, remote, extra]) {
    if (user?.uid) {
      await bestEffortDelete(`public_profiles/${user.uid}`);
      await bestEffortDelete(`users/${user.uid}`);
    }
    await deleteAuthUser(user);
  }

  console.log("[cleanup] Cleanup finished.");
}

if (testPassed) {
  console.log(
    "\nAtlasBoard Lobby Safety + Start Fill Local E2E v1 PASSED.",
  );
  console.log(
    "Verified: unresolved OpenOnline -> Bot at authoritative Start; " +
    "remote Ready gate preserved; Player Count cannot remove an occupied " +
    "RemoteHuman; host-only kick clears seat/account state; kicked account " +
    "receives stable removal/rejoin rejection; Bot/Open truncation becomes " +
    "inactive; final Host + Bot roster starts; cleanup completed.",
  );
}
