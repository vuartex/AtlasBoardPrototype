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

async function patchDoc(path, fields) {
  return requestJson(firestoreUrl(path), {
    method: "PATCH",
    headers: {"Content-Type": "application/json"},
    body: JSON.stringify({fields}),
  });
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
    `atlasboard.phase5a.${label}.${nonce}@example.com`;

  const password =
    `AtlasBoardPhase5A!${label}!${nonce}`;

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

async function seedAccount(user, displayName) {
  let call =
    await patchDoc(
      `users/${user.uid}`,
      {
        accountStatus:
          {stringValue: "active"},
        membershipTier:
          {stringValue: "normal"},
        schemaVersion:
          {integerValue: "1"},
      },
    );

  assert(
    call.response.ok,
    `users/${user.uid} seed failed.`,
  );

  call =
    await patchDoc(
      `public_profiles/${user.uid}`,
      {
        displayName:
          {stringValue: displayName},
        avatarId:
          {stringValue: ""},
        profileFrameId:
          {stringValue: ""},
        schemaVersion:
          {integerValue: "1"},
      },
    );

  assert(
    call.response.ok,
    `public_profiles/${user.uid} seed failed.`,
  );
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

const versions = {
  gameVersion: "0.5a-match-network-local",
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

let host;
let guest;
let outsider;
let lobbyId = "";
let matchId = "";
let guestIntentId = "";

try {
  console.log(
    "AtlasBoard Phase 5A Authoritative Match Transport Local E2E v1",
  );

  console.log(
    "Safety: localhost Auth/Firestore/Functions emulators only.",
  );

  console.log(
    "[0/12] Verifying emulator safety preflight...",
  );
  await verifyEmulators();
  console.log("[0/12] PASSED. Required emulators detected.");

  console.log("[1/12] Creating temporary canonical accounts...");
  host = await createAuthUser("host");
  guest = await createAuthUser("guest");
  outsider = await createAuthUser("outsider");

  await seedAccount(host, "Phase 5A Host");
  await seedAccount(guest, "Phase 5A Guest");
  await seedAccount(outsider, "Phase 5A Outsider");

  console.log("[1/12] PASSED. Accounts/profile state seeded.");

  console.log("[2/12] Creating two-seat private lobby...");
  let call =
    await callFunction(
      "lobbyCreatePrivateRoom",
      host.token,
      {
        ...settings,
        ...versions,
      },
    );

  assert(
    call.response.ok,
    `Lobby create failed: ${call.text}`,
  );

  let snapshot = result(call).snapshot;
  const roomCode = result(call).roomCode;
  lobbyId = snapshot.lobbyId;

  call =
    await callFunction(
      "lobbyJoinByCode",
      guest.token,
      {
        roomCode,
        password: "",
        idempotencyKey:
          "phase5a-guest-join",
        ...versions,
      },
    );

  assert(
    call.response.ok,
    `Guest join failed: ${call.text}`,
  );

  snapshot = result(call).snapshot;
  console.log("[2/12] PASSED. Host + RemoteHuman lobby ready.");

  console.log("[3/12] Guest Readies and host starts match...");
  call =
    await callFunction(
      "lobbySetReady",
      guest.token,
      {
        lobbyId,
        expectedSettingsRevision:
          snapshot.settingsRevision,
        ready: true,
      },
    );

  assert(
    call.response.ok,
    `Guest Ready failed: ${call.text}`,
  );

  call =
    await callFunction(
      "lobbyStartMatch",
      host.token,
      {
        lobbyId,
        expectedSettingsRevision:
          result(call).snapshot.settingsRevision,
      },
    );

  assert(
    call.response.ok,
    `Host Start failed: ${call.text}`,
  );

  matchId =
    result(call).snapshot.matchId;

  assert(matchId, "No matchId returned.");
  console.log("[3/12] PASSED. Match bootstrap created.");

  console.log("[4/12] Host and guest read network revision 0...");
  const hostSnapshotCall =
    await callFunction(
      "matchGetSnapshot",
      host.token,
      {matchId},
    );

  const guestSnapshotCall =
    await callFunction(
      "matchGetSnapshot",
      guest.token,
      {matchId},
    );

  assert(
    hostSnapshotCall.response.ok,
    `Host network snapshot failed: ${hostSnapshotCall.text}`,
  );

  assert(
    guestSnapshotCall.response.ok,
    `Guest network snapshot failed: ${guestSnapshotCall.text}`,
  );

  assert(
    result(hostSnapshotCall).snapshot.revision === 0,
    "Initial network revision was not 0.",
  );

  assert(
    result(guestSnapshotCall).snapshot.localSeatId === "seat_2",
    "Guest did not resolve its authoritative seat.",
  );

  console.log("[4/12] PASSED. Member bootstrap snapshot verified.");

  console.log("[5/12] Outsider is rejected...");
  call =
    await callFunction(
      "matchGetSnapshot",
      outsider.token,
      {matchId},
    );

  assert(
    !call.response.ok,
    "Outsider unexpectedly read match state.",
  );

  assert(
    errorKey(call) === "match.error.member_only",
    `Unexpected outsider error: ${call.text}`,
  );

  console.log("[5/12] PASSED. Match membership enforced.");

  console.log("[6/12] Guest submits idempotent roll intent...");
  call =
    await callFunction(
      "matchSubmitIntent",
      guest.token,
      {
        matchId,
        clientCommandId:
          "phase5a-roll-command-0001",
        intentType:
          "request_roll",
        payloadJson:
          "{\"source\":\"e2e\"}",
      },
    );

  assert(
    call.response.ok,
    `Intent submit failed: ${call.text}`,
  );

  const firstIntent = result(call);
  guestIntentId = firstIntent.intentId;

  assert(
    firstIntent.idempotentReplay === false,
    "First intent was incorrectly a replay.",
  );

  call =
    await callFunction(
      "matchSubmitIntent",
      guest.token,
      {
        matchId,
        clientCommandId:
          "phase5a-roll-command-0001",
        intentType:
          "request_roll",
        payloadJson:
          "{\"source\":\"e2e\"}",
      },
    );

  assert(
    call.response.ok &&
      result(call).idempotentReplay === true,
    "Intent replay was not idempotent.",
  );

  console.log("[6/12] PASSED. Client intent idempotency verified.");

  console.log("[7/12] Guest cannot read host pending-intent queue...");
  call =
    await callFunction(
      "matchHostListPendingIntents",
      guest.token,
      {matchId},
    );

  assert(
    !call.response.ok,
    "Guest unexpectedly read host intent queue.",
  );

  assert(
    errorKey(call) === "match.error.host_only",
    `Unexpected host-only error: ${call.text}`,
  );

  console.log("[7/12] PASSED. Host intent authority enforced.");

  console.log("[8/12] Host receives exactly one pending intent...");
  call =
    await callFunction(
      "matchHostListPendingIntents",
      host.token,
      {matchId},
    );

  assert(
    call.response.ok,
    `Host intent list failed: ${call.text}`,
  );

  const intents = result(call).intents ?? [];

  assert(
    intents.filter((item) =>
      item.intentId === guestIntentId
    ).length === 1,
    "Host did not receive exactly one idempotent intent.",
  );

  console.log("[8/12] PASSED. Host pending queue verified.");

  console.log("[9/12] Guest cannot publish authoritative state...");
  call =
    await callFunction(
      "matchHostPublishState",
      guest.token,
      {
        matchId,
        expectedRevision: 0,
        phase: "starting_order",
        turnSeatId: "seat_2",
        eventSequence: 1,
        snapshotJson:
          "{\"startingOrder\":true}",
      },
    );

  assert(
    !call.response.ok,
    "Guest unexpectedly published authoritative state.",
  );

  assert(
    errorKey(call) === "match.error.host_only",
    `Unexpected publish error: ${call.text}`,
  );

  console.log("[9/12] PASSED. Host publication authority enforced.");

  console.log("[10/12] Host publishes revision 1...");
  call =
    await callFunction(
      "matchHostPublishState",
      host.token,
      {
        matchId,
        expectedRevision: 0,
        phase: "starting_order",
        turnSeatId: "seat_2",
        eventSequence: 1,
        snapshotJson:
          "{\"startingOrder\":true}",
      },
    );

  assert(
    call.response.ok,
    `Host publish failed: ${call.text}`,
  );

  assert(
    result(call).state.revision === 1,
    "Network revision did not advance to 1.",
  );

  console.log("[10/12] PASSED. Revision 0 -> 1 published.");

  console.log("[11/12] Stale host publish is rejected...");
  call =
    await callFunction(
      "matchHostPublishState",
      host.token,
      {
        matchId,
        expectedRevision: 0,
        phase: "awaiting_roll",
        turnSeatId: "seat_2",
        eventSequence: 2,
        snapshotJson:
          "{\"stale\":true}",
      },
    );

  assert(
    !call.response.ok,
    "Stale host publish unexpectedly succeeded.",
  );

  assert(
    errorKey(call) === "match.error.revision_mismatch",
    `Unexpected stale-revision error: ${call.text}`,
  );

  console.log("[11/12] PASSED. Revision race protection verified.");

  console.log("[12/12] Guest receives revision 1; host ACKs intent...");
  call =
    await callFunction(
      "matchGetSnapshot",
      guest.token,
      {matchId},
    );

  assert(
    call.response.ok,
    `Guest refreshed snapshot failed: ${call.text}`,
  );

  const finalSnapshot =
    result(call).snapshot;

  assert(
    finalSnapshot.revision === 1 &&
      finalSnapshot.phase === "starting_order" &&
      finalSnapshot.turnSeatId === "seat_2",
    "Guest did not receive host-published state.",
  );

  call =
    await callFunction(
      "matchHostAcknowledgeIntents",
      host.token,
      {
        matchId,
        intentIds: [guestIntentId],
      },
    );

  assert(
    call.response.ok &&
      result(call).acknowledged === 1,
    `Intent ACK failed: ${call.text}`,
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
    "Consumed intent remained pending.",
  );

  console.log(
    "[12/12] PASSED. Guest state propagation + host ACK verified.",
  );

  console.log("");
  console.log(
    "AtlasBoard Phase 5A Authoritative Match Transport Local E2E v1 PASSED.",
  );

  console.log(
    "Verified: member-only match snapshots; host-authoritative revision " +
      "publication; idempotent remote intent queue; host-only intent " +
      "consumption; stale revision rejection; remote snapshot propagation.",
  );
} catch (error) {
  console.error("");
  console.error(
    "AtlasBoard Phase 5A Authoritative Match Transport Local E2E v1 FAILED.",
  );
  console.error(error);
  process.exitCode = 1;
} finally {
  console.log("[cleanup] Cleaning temporary Phase 5A emulator data...");

  if (matchId) {
    if (guestIntentId) {
      await deleteDoc(
        `matches/${matchId}/intents/${guestIntentId}`,
      );
    }

    await deleteDoc(
      `matches/${matchId}/network/state`,
    );

    for (let i = 1; i <= 4; i++) {
      await deleteDoc(
        `matches/${matchId}/seats/seat_${i}`,
      );
    }

    await deleteDoc(`matches/${matchId}`);
  }

  if (lobbyId) {
    for (let i = 1; i <= 4; i++) {
      await deleteDoc(
        `lobbies/${lobbyId}/members/seat_${i}`,
      );
    }

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

  if (outsider?.uid) {
    await deleteDoc(`users/${outsider.uid}`);
    await deleteDoc(`public_profiles/${outsider.uid}`);
  }

  await deleteAuthUser(host);
  await deleteAuthUser(guest);
  await deleteAuthUser(outsider);

  console.log("[cleanup] Cleanup finished.");
}
