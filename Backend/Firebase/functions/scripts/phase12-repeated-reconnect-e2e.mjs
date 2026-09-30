const PROJECT_ID = "atlasboard-usa";
const REGION = "europe-west1";
const AUTH_BASE = "http://127.0.0.1:9099";
const FUNCTIONS_BASE = "http://127.0.0.1:5001";
const FIRESTORE_BASE = "http://127.0.0.1:8080";
const HUB_BASE = "http://127.0.0.1:4400";
const API_KEY = "atlasboard-local-emulator-only";
const TIMEOUT_MS = 10000;
const RECONNECT_CYCLES = 5;

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
  return requestJson(firestoreUrl(path));
}

async function listCollection(path) {
  return requestJson(
    `${firestoreUrl(path)}?pageSize=100`,
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

function stringValue(value) {
  return {stringValue: value};
}

function integerValue(value) {
  return {integerValue: String(value)};
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
    `atlasboard.phase12c.${label}.${nonce}@example.com`;

  const password =
    `AtlasBoardPhase12C!${label}!${nonce}`;

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
          stringValue("active"),
        membershipTier:
          stringValue("normal"),
        schemaVersion:
          integerValue(1),
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
          stringValue(displayName),
        avatarId:
          stringValue(""),
        profileFrameId:
          stringValue(""),
        schemaVersion:
          integerValue(1),
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
  gameVersion: "0.12c-reconnect-cycle",
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
let lobbyId = "";
let matchId = "";
let roomCode = "";
let canonicalSeatId = "";

try {
  console.log(
    "AtlasBoard Phase 12C Repeated Reconnect Local E2E",
  );

  console.log("[0/9] Emulator safety preflight...");
  await verifyEmulators();
  console.log("[0/9] PASS.");

  console.log("[1/9] Create accounts/profiles...");
  host = await createAuthUser("host");
  guest = await createAuthUser("guest");
  await seedAccount(host, "Phase 12C Host");
  await seedAccount(guest, "Phase 12C Guest");
  console.log("[1/9] PASS.");

  console.log("[2/9] Create/join/start real two-seat match...");
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

  roomCode = result(call).roomCode;
  lobbyId = result(call).snapshot.lobbyId;

  call =
    await callFunction(
      "lobbyJoinByCode",
      guest.token,
      {
        roomCode,
        password: "",
        idempotencyKey:
          "phase12c-initial-join",
        ...versions,
      },
    );

  assert(
    call.response.ok,
    `Guest join failed: ${call.text}`,
  );

  canonicalSeatId =
    result(call).snapshot.members
      .find((member) =>
        member.accountId === guest.uid
      )?.seatId ?? "";

  assert(
    canonicalSeatId,
    "Could not resolve canonical Guest SeatId.",
  );

  call =
    await callFunction(
      "lobbySetReady",
      guest.token,
      {
        lobbyId,
        expectedSettingsRevision:
          result(call).snapshot.settingsRevision,
        ready: true,
      },
    );

  assert(
    call.response.ok,
    `Guest ready failed: ${call.text}`,
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
    `Start failed: ${call.text}`,
  );

  matchId =
    result(call).snapshot.matchId;

  assert(matchId, "No matchId.");
  console.log(
    `[2/9] PASS. Canonical Guest SeatId=${canonicalSeatId}`,
  );

  console.log(
    `[3/9] Run ${RECONNECT_CYCLES} leave/rejoin cycles...`,
  );

  for (
    let cycle = 1;
    cycle <= RECONNECT_CYCLES;
    cycle++
  ) {
    call =
      await callFunction(
        "matchLeaveActive",
        guest.token,
        {matchId},
      );

    assert(
      call.response.ok,
      `Cycle ${cycle}: leave failed: ${call.text}`,
    );

    let seat =
      await getDoc(
        `matches/${matchId}/seats/${canonicalSeatId}`,
      );

    const expiresAt =
      Number(
        seat.json?.fields
          ?.reconnectExpiresAtEpochMs
          ?.integerValue ?? 0
      );

    assert(
      seat.response.ok &&
        seat.json?.fields?.accountId?.stringValue ===
          guest.uid &&
        seat.json?.fields?.controllerKind?.stringValue ===
          "temporary_bot" &&
        seat.json?.fields?.connectionState?.stringValue ===
          "reconnecting" &&
        expiresAt > Date.now(),
      `Cycle ${cycle}: TemporaryBot reservation invalid.`,
    );

    call =
      await callFunction(
        "lobbyJoinByCode",
        guest.token,
        {
          roomCode,
          password: "",
          idempotencyKey:
            `phase12c-rejoin-${cycle}`,
          ...versions,
        },
      );

    assert(
      call.response.ok,
      `Cycle ${cycle}: rejoin failed: ${call.text}`,
    );

    const member =
      result(call).snapshot.members
        .find((item) =>
          item.accountId === guest.uid
        );

    assert(
      member?.seatId === canonicalSeatId,
      `Cycle ${cycle}: SeatId changed after reclaim.`,
    );

    seat =
      await getDoc(
        `matches/${matchId}/seats/${canonicalSeatId}`,
      );

    assert(
      seat.response.ok &&
        seat.json?.fields?.accountId?.stringValue ===
          guest.uid &&
        seat.json?.fields?.controllerKind?.stringValue ===
          "human" &&
        seat.json?.fields?.connectionState?.stringValue ===
          "connected" &&
        Number(
          seat.json?.fields
            ?.reconnectExpiresAtEpochMs
            ?.integerValue ?? 0
        ) === 0,
      `Cycle ${cycle}: Human reclaim state invalid.`,
    );

    // A repeated Join after successful reclaim must stay idempotent.
    call =
      await callFunction(
        "lobbyJoinByCode",
        guest.token,
        {
          roomCode,
          password: "",
          idempotencyKey:
            `phase12c-rejoin-duplicate-${cycle}`,
          ...versions,
        },
      );

    assert(
      call.response.ok &&
        result(call).idempotentReplay === true,
      `Cycle ${cycle}: duplicate connected join was not idempotent.`,
    );

    console.log(
      `[3/9] Cycle ${cycle}/${RECONNECT_CYCLES} PASS.`,
    );
  }

  console.log("[4/9] Verify no duplicate match seats...");
  let collection =
    await listCollection(
      `matches/${matchId}/seats`,
    );

  const seatDocs =
    collection.json?.documents ?? [];

  assert(
    collection.response.ok &&
      seatDocs.length === 2,
    `Expected exactly 2 match seats, found ${seatDocs.length}.`,
  );
  console.log("[4/9] PASS.");

  console.log("[5/9] Verify fixed four-slot lobby member model...");
  collection =
    await listCollection(
      `lobbies/${lobbyId}/members`,
    );

  const memberDocs =
    collection.json?.documents ?? [];

  // Lobby storage intentionally keeps four concrete member documents
  // (seat_1..seat_4) even when maxPlayers=2. Seats above maxPlayers remain
  // inactive. The anti-duplication invariant is therefore "exactly the four
  // canonical slot docs", not "document count == active player count".
  assert(
    collection.response.ok &&
      memberDocs.length === 4,
    `Expected exactly 4 canonical lobby slot documents, found ${memberDocs.length}.`,
  );

  const bySeatId =
    new Map(
      memberDocs.map((doc) => {
        const seatId =
          doc.fields?.seatId?.stringValue ?? "";

        return [
          seatId,
          doc.fields ?? {},
        ];
      }),
    );

  for (const seatId of [
    "seat_1",
    "seat_2",
    "seat_3",
    "seat_4",
  ]) {
    assert(
      bySeatId.has(seatId),
      `Missing canonical lobby member ${seatId}.`,
    );
  }

  assert(
    bySeatId.get("seat_1")?.active?.booleanValue === true &&
      bySeatId.get("seat_2")?.active?.booleanValue === true,
    "The two configured lobby seats are not active.",
  );

  assert(
    bySeatId.get("seat_3")?.active?.booleanValue === false &&
      bySeatId.get("seat_4")?.active?.booleanValue === false &&
      bySeatId.get("seat_3")?.seatMode?.stringValue === "inactive" &&
      bySeatId.get("seat_4")?.seatMode?.stringValue === "inactive",
    "Seats above maxPlayers are not preserved as inactive canonical slots.",
  );

  console.log("[5/9] PASS.");

  console.log("[6/9] Verify Host authority did not drift...");
  const match =
    await getDoc(
      `matches/${matchId}`,
    );

  assert(
    match.response.ok &&
      match.json?.fields?.hostAccountId?.stringValue ===
        host.uid,
    "Guest reconnect cycles unexpectedly changed Host authority.",
  );
  console.log("[6/9] PASS.");

  console.log("[7/9] Verify Guest still owns canonical SeatId...");
  const finalSeat =
    await getDoc(
      `matches/${matchId}/seats/${canonicalSeatId}`,
    );

  assert(
    finalSeat.response.ok &&
      finalSeat.json?.fields?.accountId?.stringValue ===
        guest.uid &&
      finalSeat.json?.fields?.controllerKind?.stringValue ===
        "human" &&
      finalSeat.json?.fields?.connectionState?.stringValue ===
        "connected",
    "Final canonical seat ownership/control state is invalid.",
  );
  console.log("[7/9] PASS.");

  console.log("[8/9] Verify room code still resolves same active match...");
  call =
    await callFunction(
      "lobbyJoinByCode",
      guest.token,
      {
        roomCode,
        password: "",
        idempotencyKey:
          "phase12c-final-idempotent-join",
        ...versions,
      },
    );

  assert(
    call.response.ok &&
      result(call).snapshot.matchId === matchId,
    "Room code no longer resolves the same active match.",
  );
  console.log("[8/9] PASS.");

  console.log("[9/9] Verify lifecycle is still playable/reclaim-clean...");
  const finalMember =
    result(call).snapshot.members
      .find((item) =>
        item.accountId === guest.uid
      );

  assert(
    finalMember?.seatId === canonicalSeatId &&
      finalMember?.controllerKind === "human" &&
      finalMember?.connectionState === "connected",
    "Final lobby projection is not Human/Connected on canonical SeatId.",
  );

  console.log("[9/9] PASS.");
  console.log("");
  console.log(
    "AtlasBoard Phase 12C Repeated Reconnect Local E2E PASSED 9/9.",
  );
} catch (error) {
  console.error("");
  console.error(
    "AtlasBoard Phase 12C Repeated Reconnect Local E2E FAILED.",
  );
  console.error(error);
  process.exitCode = 1;
} finally {
  console.log("[cleanup] Removing Phase 12C fixtures...");

  if (matchId) {
    await deleteDoc(
      `matches/${matchId}/network/state`,
    );

    for (let i = 1; i <= 4; i++) {
      await deleteDoc(
        `matches/${matchId}/seats/seat_${i}`,
      );
    }

    await deleteDoc(
      `matches/${matchId}`,
    );
  }

  if (lobbyId) {
    for (let i = 1; i <= 4; i++) {
      await deleteDoc(
        `lobbies/${lobbyId}/members/seat_${i}`,
      );
    }

    await deleteDoc(
      `lobbies/${lobbyId}`,
    );
  }

  if (host?.uid) {
    await deleteDoc(`users/${host.uid}`);
    await deleteDoc(
      `public_profiles/${host.uid}`,
    );
  }

  if (guest?.uid) {
    await deleteDoc(`users/${guest.uid}`);
    await deleteDoc(
      `public_profiles/${guest.uid}`,
    );
  }

  await deleteAuthUser(host);
  await deleteAuthUser(guest);

  console.log("[cleanup] Done.");
}
