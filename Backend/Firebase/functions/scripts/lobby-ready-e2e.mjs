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

function firestoreInteger(document, fieldName) {
  const value = document?.fields?.[fieldName]?.integerValue;
  return value === undefined ? undefined : Number.parseInt(value, 10);
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
  const email = `atlasboard.lobby.${label}.${nonce}@example.com`;
  const password = `AtlasBoardLobby!${label}!${nonce}`;

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
    email,
  };
}

async function seedAccount(user, displayName, status = "active") {
  const userDoc = await patchDoc(`users/${user.uid}`, {
    accountStatus: {stringValue: status},
    membershipTier: {stringValue: "normal"},
    schemaVersion: {integerValue: "1"},
  });
  assert(userDoc.response.ok, `users/${user.uid} seed failed.`);

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

const versions = {
  gameVersion: "0.3d-local",
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
  requiredHumanPlayers: 3,
  balancedDevelopment: true,
  doublesEnabled: true,
  tripleDoublePenaltyEnabled: true,
};

let host;
let guest1;
let guest2;
let extra;
let banned;
let lobbyId = null;
let roomCode = null;
let matchId = null;
let joinCodeHash = null;
let testPassed = false;

console.log("AtlasBoard Private Lobby + Ready Local E2E v1");
console.log(
  "Safety: localhost Auth/Firestore/Functions emulators only; " +
  "room codes, accounts, lobby state, and match bootstrap are temporary.",
);

try {
  console.log("[0/14] Verifying emulator safety preflight...");
  await verifyEmulators();
  console.log("[0/14] PASSED. Auth/Firestore/Functions emulators detected.");

  console.log("[1/14] Creating temporary accounts and profiles...");
  host = await createAuthUser("host");
  guest1 = await createAuthUser("guest1");
  guest2 = await createAuthUser("guest2");
  extra = await createAuthUser("extra");
  banned = await createAuthUser("banned");

  await seedAccount(host, "Host");
  await seedAccount(guest1, "Guest One");
  await seedAccount(guest2, "Guest Two");
  await seedAccount(extra, "Extra Guest");
  await seedAccount(banned, "Banned Guest", "banned");

  console.log("[1/14] PASSED. Canonical account state seeded.");

  console.log("[2/14] Creating private room through authenticated backend...");
  const create = await callFunction(
    "lobbyCreatePrivateRoom",
    host.token,
    {
      ...createSettings,
      ...versions,
    },
  );

  assert(
    create.response.ok,
    `Create room failed: HTTP ${create.response.status} ${create.text}`,
  );

  const createResult = result(create);
  roomCode = createResult?.roomCode;
  lobbyId = createResult?.snapshot?.lobbyId;

  assert(/^\d{6}$/.test(roomCode), "Backend did not return a 6-digit code.");
  assert(typeof lobbyId === "string" && lobbyId.length > 10,
    "Backend returned no lobbyId.");
  assert(createResult.snapshot.settingsRevision === 1,
    "Initial settingsRevision must be 1.");
  assert(createResult.snapshot.requiredHumanPlayers === 3,
    "Required human count mismatch.");

  joinCodeHash = codeHash(roomCode);

  const lobbyDoc = await getDoc(`lobbies/${lobbyId}`);
  const joinCodeDoc = await getDoc(`join_codes/${joinCodeHash}`);

  assert(lobbyDoc.response.ok, "Lobby document was not created.");
  assert(joinCodeDoc.response.ok, "Protected join-code lookup was not created.");
  assert(
    lobbyDoc.json?.fields?.roomCode === undefined,
    "Raw room code was persisted in lobby document.",
  );
  assert(
    joinCodeDoc.json?.fields?.roomCode === undefined,
    "Raw room code was persisted in join-code document.",
  );

  console.log(
    "[2/14] PASSED. 6-digit code returned; only protected lookup persisted.",
  );

  console.log("[3/14] Testing invalid room code and attempt accounting...");
  const badJoin = await callFunction(
    "lobbyJoinByCode",
    guest1.token,
    {
      roomCode: roomCode === "000000" ? "999999" : "000000",
      idempotencyKey: `bad-code-${Date.now()}`,
      ...versions,
    },
  );

  await expectError(
    badJoin,
    "NOT_FOUND",
    "lobby.error.invalid_code",
    "Invalid code join",
  );

  const attempts = await getDoc(`join_code_attempts/${guest1.uid}`);
  assert(attempts.response.ok, "Invalid-attempt document was not created.");
  assert(firestoreInteger(attempts.json, "invalidCount") === 1,
    "Expected invalidCount=1.");

  console.log("[3/14] PASSED. Invalid code was rejected and counted.");

  console.log("[4/14] Testing version mismatch with valid room code...");
  const versionMismatch = await callFunction(
    "lobbyJoinByCode",
    guest1.token,
    {
      roomCode,
      idempotencyKey: `version-mismatch-${Date.now()}`,
      ...versions,
      protocolVersion: 999,
    },
  );

  await expectError(
    versionMismatch,
    "FAILED_PRECONDITION",
    "lobby.error.version_mismatch",
    "Version mismatch",
  );

  console.log("[4/14] PASSED. Incompatible client was rejected.");

  console.log("[5/14] Joining guest one and replaying exact request...");
  const joinKey1 = `guest1-join-${Date.now()}`;
  const join1 = await callFunction(
    "lobbyJoinByCode",
    guest1.token,
    {
      roomCode,
      idempotencyKey: joinKey1,
      ...versions,
    },
  );
  assert(join1.response.ok, `Guest one join failed: ${join1.text}`);
  const join1Result = result(join1);
  assert(join1Result?.idempotentReplay === false,
    "First guest join marked as replay.");

  const guest1Seat = join1Result.snapshot.members.find(
    (member) => member.accountId === guest1.uid,
  );
  assert(guest1Seat?.seatId === "seat_2",
    "Guest one did not atomically reserve seat_2.");

  const joinReplay = await callFunction(
    "lobbyJoinByCode",
    guest1.token,
    {
      roomCode,
      idempotencyKey: joinKey1,
      ...versions,
    },
  );
  assert(joinReplay.response.ok, `Join replay failed: ${joinReplay.text}`);
  assert(result(joinReplay)?.idempotentReplay === true,
    "Exact join replay was not idempotent.");

  const duplicateJoin = await callFunction(
    "lobbyJoinByCode",
    guest1.token,
    {
      roomCode,
      idempotencyKey: `guest1-different-${Date.now()}`,
      ...versions,
    },
  );
  await expectError(
    duplicateJoin,
    "ALREADY_EXISTS",
    "lobby.error.already_joined",
    "Duplicate account join",
  );

  console.log(
    "[5/14] PASSED. Seat reservation and exact replay behavior verified.",
  );

  console.log("[6/14] Rejecting banned account...");
  const bannedJoin = await callFunction(
    "lobbyJoinByCode",
    banned.token,
    {
      roomCode,
      idempotencyKey: `banned-${Date.now()}`,
      ...versions,
    },
  );
  await expectError(
    bannedJoin,
    "PERMISSION_DENIED",
    "lobby.error.account_not_active",
    "Banned account join",
  );
  console.log("[6/14] PASSED. Non-active account was rejected.");

  console.log("[7/14] Joining final required human and rejecting overflow...");
  const join2 = await callFunction(
    "lobbyJoinByCode",
    guest2.token,
    {
      roomCode,
      idempotencyKey: `guest2-${Date.now()}`,
      ...versions,
    },
  );
  assert(join2.response.ok, `Guest two join failed: ${join2.text}`);

  const extraJoin = await callFunction(
    "lobbyJoinByCode",
    extra.token,
    {
      roomCode,
      idempotencyKey: `extra-${Date.now()}`,
      ...versions,
    },
  );
  await expectError(
    extraJoin,
    "RESOURCE_EXHAUSTED",
    "lobby.error.full",
    "Overflow join",
  );

  console.log(
    "[7/14] PASSED. Required human seats filled; bot seat was not consumed.",
  );

  console.log("[8/14] Setting two players ready for revision 1...");
  const hostReady1 = await callFunction(
    "lobbySetReady",
    host.token,
    {
      lobbyId,
      expectedSettingsRevision: 1,
      ready: true,
    },
  );
  assert(hostReady1.response.ok, `Host ready failed: ${hostReady1.text}`);
  assert(result(hostReady1)?.started === false,
    "Lobby started before all required humans were ready.");

  const guest1Ready1 = await callFunction(
    "lobbySetReady",
    guest1.token,
    {
      lobbyId,
      expectedSettingsRevision: 1,
      ready: true,
    },
  );
  assert(guest1Ready1.response.ok,
    `Guest one ready failed: ${guest1Ready1.text}`);
  assert(result(guest1Ready1)?.started === false,
    "Lobby started before guest two was ready.");

  console.log("[8/14] PASSED. Partial readiness did not start the room.");

  console.log("[9/14] Host changing rules and invalidating old ready states...");
  const update = await callFunction(
    "lobbyUpdateSettings",
    host.token,
    {
      lobbyId,
      expectedSettingsRevision: 1,
      mapId: "Colorado",
      themeId: "Garden",
      roundLimit: 30,
      balancedDevelopment: true,
      doublesEnabled: true,
      tripleDoublePenaltyEnabled: true,
    },
  );
  assert(update.response.ok, `Host update failed: ${update.text}`);
  const updateResult = result(update);
  assert(updateResult?.applied === true, "Host rule change was not applied.");
  assert(updateResult?.snapshot?.settingsRevision === 2,
    "Host rule change did not increment settingsRevision.");
  assert(updateResult.snapshot.mapId === "Colorado",
    "Updated map was not reflected.");

  const snapshotAfterUpdate = await callFunction(
    "lobbyGetSnapshot",
    host.token,
    {lobbyId},
  );
  assert(snapshotAfterUpdate.response.ok,
    `Snapshot after update failed: ${snapshotAfterUpdate.text}`);

  const hostMember = result(snapshotAfterUpdate).snapshot.members.find(
    (member) => member.accountId === host.uid,
  );
  const guest1Member = result(snapshotAfterUpdate).snapshot.members.find(
    (member) => member.accountId === guest1.uid,
  );

  assert(hostMember.readyForRevision === 1,
    "Host old ready revision was unexpectedly rewritten.");
  assert(guest1Member.readyForRevision === 1,
    "Guest old ready revision was unexpectedly rewritten.");

  console.log(
    "[9/14] PASSED. settingsRevision=2 made revision-1 readiness stale.",
  );

  console.log("[10/14] Enforcing host-only settings authority...");
  const guestUpdate = await callFunction(
    "lobbyUpdateSettings",
    guest1.token,
    {
      lobbyId,
      expectedSettingsRevision: 2,
      mapId: "USA",
      themeId: "Beach",
      roundLimit: 10,
      balancedDevelopment: true,
      doublesEnabled: true,
      tripleDoublePenaltyEnabled: true,
    },
  );
  await expectError(
    guestUpdate,
    "PERMISSION_DENIED",
    "lobby.error.host_only",
    "Guest settings update",
  );
  console.log("[10/14] PASSED. Non-host settings update rejected.");

  console.log("[11/14] Re-readying all required humans for revision 2...");
  for (const user of [host, guest1]) {
    const readyCall = await callFunction(
      "lobbySetReady",
      user.token,
      {
        lobbyId,
        expectedSettingsRevision: 2,
        ready: true,
      },
    );
    assert(readyCall.response.ok, `Revision-2 ready failed: ${readyCall.text}`);
    assert(result(readyCall)?.started === false,
      "Lobby started before the final required human.");
  }

  const finalReady = await callFunction(
    "lobbySetReady",
    guest2.token,
    {
      lobbyId,
      expectedSettingsRevision: 2,
      ready: true,
    },
  );
  assert(finalReady.response.ok, `Final ready failed: ${finalReady.text}`);
  const finalReadyResult = result(finalReady);
  assert(finalReadyResult?.started === true,
    "All-ready state did not transition the lobby.");
  assert(finalReadyResult.snapshot.lifecycleState === "starting",
    "Lobby did not transition waiting -> starting.");

  matchId = finalReadyResult.snapshot.matchId;
  assert(typeof matchId === "string" && matchId.length > 10,
    "Authoritative matchId was not created.");
  assert(
    typeof finalReadyResult.snapshot.startEventId === "string" &&
    finalReadyResult.snapshot.startEventId.length > 10,
    "Authoritative startEventId was not created.",
  );

  console.log(
    "[11/14] PASSED. Final ready produced one authoritative Starting state.",
  );

  console.log("[12/14] Replaying ready after start...");
  const readyReplay = await callFunction(
    "lobbySetReady",
    guest2.token,
    {
      lobbyId,
      expectedSettingsRevision: 2,
      ready: true,
    },
  );
  assert(readyReplay.response.ok, `Ready replay failed: ${readyReplay.text}`);
  const replayResult = result(readyReplay);
  assert(replayResult?.started === true, "Start replay lost started state.");
  assert(replayResult.snapshot.matchId === matchId,
    "Ready replay created or returned a different match.");

  console.log("[12/14] PASSED. No second match/start identity was created.");

  console.log("[13/14] Verifying authoritative Firestore state directly...");
  const finalLobby = await getDoc(`lobbies/${lobbyId}`);
  const finalMatch = await getDoc(`matches/${matchId}`);
  const finalCode = await getDoc(`join_codes/${joinCodeHash}`);

  assert(finalLobby.response.ok, "Final lobby document missing.");
  assert(finalMatch.response.ok, "Match bootstrap document missing.");
  assert(finalCode.response.ok, "Protected join-code document missing.");

  assert(
    firestoreString(finalLobby.json, "lifecycleState") === "starting",
    "Final lobby state is not starting.",
  );
  assert(
    firestoreInteger(finalLobby.json, "settingsRevision") === 2,
    "Final settingsRevision is not 2.",
  );
  assert(
    firestoreString(finalLobby.json, "matchId") === matchId,
    "Lobby matchId mismatch.",
  );
  assert(
    firestoreString(finalMatch.json, "lobbyId") === lobbyId,
    "Match bootstrap lobbyId mismatch.",
  );
  assert(
    firestoreString(finalMatch.json, "status") === "starting",
    "Match bootstrap status mismatch.",
  );
  assert(
    firestoreBoolean(finalCode.json, "active") === true,
    "Protected join-code lookup became inactive unexpectedly.",
  );

  for (const seatId of ["seat_1", "seat_2", "seat_3"]) {
    const seat = await getDoc(`lobbies/${lobbyId}/members/${seatId}`);
    assert(seat.response.ok, `Missing ${seatId}.`);
    assert(firestoreString(seat.json, "seatType") === "human",
      `${seatId} is not a human seat.`);
    assert(firestoreInteger(seat.json, "readyForRevision") === 2,
      `${seatId} is not ready for revision 2.`);
  }

  const botSeat = await getDoc(`lobbies/${lobbyId}/members/seat_4`);
  assert(botSeat.response.ok, "Bot seat missing.");
  assert(firestoreString(botSeat.json, "seatType") === "bot",
    "Seat 4 was not preserved as a bot seat.");
  assert(firestoreString(botSeat.json, "accountId") === "",
    "Bot seat was incorrectly occupied by a human.");

  console.log(
    "[13/14] PASSED. Lobby, members, protected code, and match bootstrap verified.",
  );

  console.log("[14/14] Verifying room is no longer joinable...");
  const postStartJoin = await callFunction(
    "lobbyJoinByCode",
    extra.token,
    {
      roomCode,
      idempotencyKey: `post-start-${Date.now()}`,
      ...versions,
    },
  );
  await expectError(
    postStartJoin,
    "FAILED_PRECONDITION",
    "lobby.error.not_joinable",
    "Post-start join",
  );
  console.log("[14/14] PASSED. Starting lobby rejects new joins.");

  testPassed = true;
} catch (error) {
  console.error("\nAtlasBoard Private Lobby + Ready Local E2E v1 FAILED.");
  console.error(error);
  process.exitCode = 1;
} finally {
  console.log("[cleanup] Cleaning temporary lobby emulator data...");

  if (lobbyId) {
    for (const seatId of ["seat_1", "seat_2", "seat_3", "seat_4"]) {
      await bestEffortDelete(`lobbies/${lobbyId}/members/${seatId}`);
    }
    await bestEffortDelete(`lobbies/${lobbyId}`);
  }

  if (matchId) {
    await bestEffortDelete(`matches/${matchId}`);
  }

  if (joinCodeHash) {
    await bestEffortDelete(`join_codes/${joinCodeHash}`);
  }

  for (const user of [host, guest1, guest2, extra, banned]) {
    if (!user) {
      continue;
    }

    await bestEffortDelete(`join_code_attempts/${user.uid}`);
    await bestEffortDelete(`public_profiles/${user.uid}`);
    await bestEffortDelete(`users/${user.uid}`);
    await deleteAuthUser(user);
  }

  console.log("[cleanup] Cleanup finished.");
}

if (testPassed) {
  console.log(
    "\nAtlasBoard Private Lobby + Ready Local E2E v1 PASSED.",
  );
  console.log(
    "Verified: protected 6-digit code -> atomic seat reservation -> " +
    "version/account/full checks -> readyForRevision -> host settingsRevision " +
    "advance -> stale-ready invalidation -> single Waiting->Starting match " +
    "bootstrap -> direct Firestore verification -> cleanup.",
  );
}
