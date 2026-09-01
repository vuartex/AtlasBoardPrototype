import {createHmac} from "node:crypto";

const PROJECT_ID = "atlasboard-usa";
const REGION = "europe-west1";
const AUTH_BASE = "http://127.0.0.1:9099";
const FUNCTIONS_BASE = "http://127.0.0.1:5001";
const FIRESTORE_BASE = "http://127.0.0.1:8080";
const HUB_BASE = "http://127.0.0.1:4400";
const API_KEY = "atlasboard-local-emulator-only";
const TIMEOUT_MS = 10000;
const LOCAL_JOIN_CODE_PEPPER = "atlasboard-local-emulator-lobby-code-pepper-v1";

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
  return postJson(callableUrl(name), {data}, {Authorization: `Bearer ${token}`});
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

async function verifyEmulators() {
  const hub = await requestJson(`${HUB_BASE}/emulators`);
  assert(hub.response.ok, "Emulator Hub is not reachable.");
  for (const name of ["auth", "functions", "firestore"]) {
    assert(hub.json?.[name], `Required emulator not running: ${name}`);
  }
}

async function createAuthUser(label) {
  const nonce = `${Date.now()}-${Math.floor(Math.random() * 1000000)}`;
  const email = `atlasboard.phase4d.${label}.${nonce}@example.com`;
  const password = `AtlasBoardPhase4D!${label}!${nonce}`;
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
  } catch {}
}

async function cleanupLobby(lobbyId, roomCode = "", users = []) {
  if (!lobbyId) return;
  for (let i = 1; i <= 4; i++) {
    await bestEffortDelete(`lobbies/${lobbyId}/members/seat_${i}`);
  }
  for (const user of users) {
    if (user?.uid) await bestEffortDelete(`lobbies/${lobbyId}/kicks/${user.uid}`);
  }
  await bestEffortDelete(`lobby_discovery/${lobbyId}`);
  await bestEffortDelete(`lobbies/${lobbyId}`);
  if (roomCode) await bestEffortDelete(`join_codes/${codeHash(roomCode)}`);
}

const versions = {
  gameVersion: "0.4d-public-join-password-local",
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
let guestA;
let guestB;
let privateHost;
const lobbies = [];

try {
  console.log("AtlasBoard Phase 4C/4D Public Join + Password Local E2E v1");
  console.log("Safety: localhost Auth/Firestore/Functions emulators only.");

  console.log("[0/12] Verifying emulator safety preflight...");
  await verifyEmulators();
  console.log("[0/12] PASSED. Required emulators detected.");

  console.log("[1/12] Creating temporary canonical accounts...");
  host = await createAuthUser("host");
  guestA = await createAuthUser("guest-a");
  guestB = await createAuthUser("guest-b");
  privateHost = await createAuthUser("private-host");
  await seedAccount(host, "Password Host");
  await seedAccount(guestA, "Guest A");
  await seedAccount(guestB, "Guest B");
  await seedAccount(privateHost, "Private Host");
  console.log("[1/12] PASSED. Accounts/profile state seeded.");

  console.log("[2/12] Creating public room and enabling password protection...");
  let call = await callFunction("lobbyCreatePublicRoom", host.token, {...settings, ...versions});
  assert(call.response.ok, `Public create failed: ${call.text}`);
  let created = result(call);
  const publicLobbyId = created.snapshot.lobbyId;
  const publicRoomCode = created.roomCode;
  lobbies.push({id: publicLobbyId, code: publicRoomCode});

  call = await callFunction("lobbyUpdatePassword", host.token, {
    lobbyId: publicLobbyId,
    expectedSettingsRevision: created.snapshot.settingsRevision,
    password: "4827",
  });
  assert(call.response.ok, `Password update failed: ${call.text}`);
  const locked = result(call);
  assert(locked.snapshot.hasPassword === true, "Snapshot did not report password protection.");
  console.log("[2/12] PASSED. Public room password stored as protected access state.");

  console.log("[3/12] Verifying discovery exposes only password STATUS...");
  call = await callFunction("lobbyListPublicRooms", guestA.token, {...versions, limit: 20});
  assert(call.response.ok, `Public list failed: ${call.text}`);
  const card = result(call).rooms.find((item) => item.lobbyId === publicLobbyId);
  assert(card, "Locked public room missing from discovery.");
  assert(card.hasPassword === true, "Browser card did not expose hasPassword=true.");
  const cardText = JSON.stringify(card);
  assert(!cardText.includes("4827"), "Raw password leaked into browser card.");
  assert(!cardText.includes("passwordHash"), "Password hash leaked into browser card.");
  const discovery = await getDoc(`lobby_discovery/${publicLobbyId}`);
  const discoveryText = JSON.stringify(discovery.json);
  assert(!discoveryText.includes("4827"), "Raw password leaked into discovery document.");
  assert(!discoveryText.includes("passwordHash"), "Password hash leaked into discovery document.");
  console.log("[3/12] PASSED. Discovery status is sanitized.");

  console.log("[4/12] Rejecting public JOIN without password...");
  call = await callFunction("lobbyJoinPublicRoom", guestA.token, {
    lobbyId: publicLobbyId,
    password: "",
    idempotencyKey: "public-join-no-password",
    ...versions,
  });
  assert(!call.response.ok, "Passwordless JOIN unexpectedly succeeded.");
  assert(errorKey(call) === "lobby.error.password_required",
    `Unexpected missing-password error: ${call.text}`);
  console.log("[4/12] PASSED. Password required.");

  console.log("[5/12] Rejecting wrong public room password...");
  call = await callFunction("lobbyJoinPublicRoom", guestA.token, {
    lobbyId: publicLobbyId,
    password: "9999",
    idempotencyKey: "public-join-wrong-password",
    ...versions,
  });
  assert(!call.response.ok, "Wrong password unexpectedly succeeded.");
  assert(errorKey(call) === "lobby.error.password_incorrect",
    `Unexpected wrong-password error: ${call.text}`);
  console.log("[5/12] PASSED. Wrong password rejected.");

  console.log("[6/12] Correct public JOIN reserves one seat and returns invite code...");
  call = await callFunction("lobbyJoinPublicRoom", guestA.token, {
    lobbyId: publicLobbyId,
    password: "4827",
    idempotencyKey: "public-join-correct",
    ...versions,
  });
  assert(call.response.ok, `Correct public JOIN failed: ${call.text}`);
  const joined = result(call);
  assert(joined.roomCode === publicRoomCode,
    "Public browser member did not receive the same protected invite room code.");
  assert(joined.snapshot.remoteHumanCount === 1, "RemoteHuman count was not updated.");
  console.log("[6/12] PASSED. Correct password JOIN + shared room code verified.");

  console.log("[7/12] Removing password updates discovery and invalidates Ready revision...");
  call = await callFunction("lobbyUpdatePassword", host.token, {
    lobbyId: publicLobbyId,
    expectedSettingsRevision: joined.snapshot.settingsRevision,
    password: "",
  });
  assert(call.response.ok, `Password removal failed: ${call.text}`);
  const unlocked = result(call);
  assert(unlocked.snapshot.hasPassword === false, "Password removal did not clear access state.");
  call = await callFunction("lobbyListPublicRooms", guestB.token, {...versions, limit: 20});
  const unlockedCard = result(call).rooms.find((item) => item.lobbyId === publicLobbyId);
  assert(unlockedCard?.hasPassword === false, "Discovery did not update to OPEN.");
  console.log("[7/12] PASSED. Password removal projected to browser.");

  console.log("[8/12] Private room code JOIN enforces the same password contract...");
  call = await callFunction("lobbyCreatePrivateRoom", privateHost.token, {...settings, ...versions});
  assert(call.response.ok, `Private create failed: ${call.text}`);
  const privateCreated = result(call);
  lobbies.push({id: privateCreated.snapshot.lobbyId, code: privateCreated.roomCode});
  call = await callFunction("lobbyUpdatePassword", privateHost.token, {
    lobbyId: privateCreated.snapshot.lobbyId,
    expectedSettingsRevision: privateCreated.snapshot.settingsRevision,
    password: "ABCD",
  });
  assert(call.response.ok, `Private password set failed: ${call.text}`);

  call = await callFunction("lobbyJoinByCode", guestB.token, {
    roomCode: privateCreated.roomCode,
    password: "",
    idempotencyKey: "private-no-password",
    ...versions,
  });
  assert(!call.response.ok && errorKey(call) === "lobby.error.password_required",
    "Private code JOIN did not require password.");

  call = await callFunction("lobbyJoinByCode", guestB.token, {
    roomCode: privateCreated.roomCode,
    password: "ABCD",
    idempotencyKey: "private-correct-password",
    ...versions,
  });
  assert(call.response.ok, `Private password JOIN failed: ${call.text}`);
  console.log("[8/12] PASSED. Private/Public password model is shared.");

  console.log("[9/12] Host close removes public discovery and deactivates room lookup...");
  call = await callFunction("lobbyCloseRoom", host.token, {lobbyId: publicLobbyId});
  assert(call.response.ok, `Host close failed: ${call.text}`);
  const discoveryAfterClose = await getDoc(`lobby_discovery/${publicLobbyId}`);
  assert(discoveryAfterClose.response.status === 404, "Closed public lobby remains discoverable.");
  const codeAfterClose = await getDoc(`join_codes/${codeHash(publicRoomCode)}`);
  assert(codeAfterClose.response.ok, "Join code document missing after close.");
  const activeValue = codeAfterClose.json?.fields?.active?.booleanValue;
  assert(activeValue === false, "Closed lobby join code remained active.");
  console.log("[9/12] PASSED. Host close clears discoverability/access.");

  console.log("[10/12] New public room automatically closes older waiting public room for same host...");
  let first = await callFunction("lobbyCreatePublicRoom", host.token, {...settings, ...versions});
  assert(first.response.ok, `First replacement create failed: ${first.text}`);
  const firstResult = result(first);
  lobbies.push({id: firstResult.snapshot.lobbyId, code: firstResult.roomCode});
  let second = await callFunction("lobbyCreatePublicRoom", host.token, {...settings, maxPlayers: 2, ...versions});
  assert(second.response.ok, `Second replacement create failed: ${second.text}`);
  const secondResult = result(second);
  lobbies.push({id: secondResult.snapshot.lobbyId, code: secondResult.roomCode});
  const firstDiscovery = await getDoc(`lobby_discovery/${firstResult.snapshot.lobbyId}`);
  assert(firstDiscovery.response.status === 404, "Older waiting public room was not auto-closed.");
  console.log("[10/12] PASSED. Public-room accumulation prevented.");

  console.log("[11/12] Concurrent JOIN race reserves the last seat only once...");
  const raceA = callFunction("lobbyJoinPublicRoom", guestA.token, {
    lobbyId: secondResult.snapshot.lobbyId,
    password: "",
    idempotencyKey: "race-a",
    ...versions,
  });
  const raceB = callFunction("lobbyJoinPublicRoom", guestB.token, {
    lobbyId: secondResult.snapshot.lobbyId,
    password: "",
    idempotencyKey: "race-b",
    ...versions,
  });
  const raceResults = await Promise.all([raceA, raceB]);
  const successes = raceResults.filter((item) => item.response.ok);
  const failures = raceResults.filter((item) => !item.response.ok);
  assert(successes.length === 1 && failures.length === 1,
    `Expected exactly one race winner. Results=${raceResults.map((x) => x.text).join(" | ")}`);
  assert(errorKey(failures[0]) === "lobby.error.full",
    `Race loser was not rejected as full: ${failures[0].text}`);
  console.log("[11/12] PASSED. Atomic final-seat reservation verified.");

  console.log("[12/12] Verifying final browser projection is coherent...");
  call = await callFunction("lobbyListPublicRooms", privateHost.token, {...versions, limit: 20});
  assert(call.response.ok, `Final list failed: ${call.text}`);
  assert(!result(call).rooms.some((room) => room.lobbyId === secondResult.snapshot.lobbyId),
    "Full 2-player public room remained joinable in browser.");
  console.log("[12/12] PASSED. Full room hidden from joinable discovery.");

  console.log("\nAtlasBoard Phase 4C/4D Public Join + Password Local E2E v1 PASSED.");
  console.log("Verified: sanitized password status; public JOIN password gate; same room code returned to public-browser member; shared Private/Public password contract; host close; old-room cleanup; atomic final-seat race; full-room discovery removal.");
} catch (error) {
  console.error("\nAtlasBoard Phase 4C/4D Public Join + Password Local E2E v1 FAILED.");
  console.error(error);
  process.exitCode = 1;
} finally {
  console.log("[cleanup] Cleaning temporary Phase 4C/4D emulator data...");
  for (const lobby of lobbies) {
    await cleanupLobby(lobby.id, lobby.code, [guestA, guestB]);
  }
  for (const user of [host, guestA, guestB, privateHost]) {
    if (user?.uid) {
      await bestEffortDelete(`users/${user.uid}`);
      await bestEffortDelete(`public_profiles/${user.uid}`);
    }
    await deleteAuthUser(user);
  }
  console.log("[cleanup] Cleanup finished.");
}
