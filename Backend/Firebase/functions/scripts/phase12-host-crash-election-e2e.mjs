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

function booleanValue(value) {
  return {booleanValue: value};
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
    `atlasboard.phase12d.${label}.${nonce}@example.com`;

  const password =
    `AtlasBoardPhase12D!${label}!${nonce}`;

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

async function seedRecoveryFixture({
  matchId,
  lobbyId,
  host,
  guest2,
  guest3,
  hostHeartbeatAt,
  guest2HeartbeatAt,
  guest3HeartbeatAt,
  authorityEpoch,
  unsafeCurrent,
}) {
  const now = Date.now();

  let call =
    await patchDoc(
      `matches/${matchId}`,
      {
        lobbyId:
          stringValue(lobbyId),
        hostAccountId:
          stringValue(host.uid),
        status:
          stringValue("active"),
        authorityEpoch:
          integerValue(authorityEpoch),
        hostHeartbeatAtEpochMs:
          integerValue(hostHeartbeatAt),
        networkRevision:
          integerValue(10),
        schemaVersion:
          integerValue(1),
      },
    );

  assert(
    call.response.ok,
    `${matchId}: match seed failed.`,
  );

  const stateFields = unsafeCurrent ?
    {
      revision:
        integerValue(10),
      phase:
        stringValue("movement"),
      turnSeatId:
        stringValue("seat_2"),
      eventSequence:
        integerValue(5),
      snapshotJson:
        stringValue('{"checkpoint":"UNSAFE_CURRENT"}'),
      recoveryRevision:
        integerValue(9),
      recoveryPhase:
        stringValue("awaiting_roll"),
      recoveryTurnSeatId:
        stringValue("seat_2"),
      recoveryEventSequence:
        integerValue(4),
      recoverySnapshotJson:
        stringValue('{"checkpoint":"SAFE_RECOVERY"}'),
      authorityHostAccountId:
        stringValue(host.uid),
      schemaVersion:
        integerValue(1),
    } :
    {
      revision:
        integerValue(10),
      phase:
        stringValue("awaiting_roll"),
      turnSeatId:
        stringValue("seat_2"),
      eventSequence:
        integerValue(5),
      snapshotJson:
        stringValue('{"checkpoint":"CURRENT_SAFE"}'),
      authorityHostAccountId:
        stringValue(host.uid),
      schemaVersion:
        integerValue(1),
    };

  call =
    await patchDoc(
      `matches/${matchId}/network/state`,
      stateFields,
    );

  assert(
    call.response.ok,
    `${matchId}: state seed failed.`,
  );

  const seats = [
    {
      id: "seat_1",
      slot: 0,
      accountId: host.uid,
      owner: host.uid,
      isHost: true,
      mode: "host_local",
      controller: "human",
      heartbeat: hostHeartbeatAt,
    },
    {
      id: "seat_2",
      slot: 1,
      accountId: guest2.uid,
      owner: "",
      isHost: false,
      mode: "remote_human",
      controller: "human",
      heartbeat: guest2HeartbeatAt,
    },
    {
      id: "seat_3",
      slot: 2,
      accountId: guest3.uid,
      owner: "",
      isHost: false,
      mode: "remote_human",
      controller: "human",
      heartbeat: guest3HeartbeatAt,
    },
    {
      id: "seat_4",
      slot: 3,
      accountId: "",
      owner: host.uid,
      isHost: false,
      mode: "bot",
      controller: "permanent_bot",
      heartbeat: 0,
    },
  ];

  for (const seat of seats) {
    call =
      await patchDoc(
        `matches/${matchId}/seats/${seat.id}`,
        {
          seatId:
            stringValue(seat.id),
          slotIndex:
            integerValue(seat.slot),
          seatMode:
            stringValue(seat.mode),
          displayName:
            stringValue(
              seat.id === "seat_1" ?
                "Phase 12D Host" :
                seat.id === "seat_2" ?
                  "Phase 12D Guest 2" :
                  seat.id === "seat_3" ?
                    "Phase 12D Guest 3" :
                    "Phase 12D Bot"
            ),
          isHost:
            booleanValue(seat.isHost),
          accountId:
            stringValue(seat.accountId),
          localOwnerAccountId:
            stringValue(seat.owner),
          controllerKind:
            stringValue(seat.controller),
          connectionState:
            stringValue("connected"),
          reconnectExpiresAtEpochMs:
            integerValue(0),
          afkLockedOut:
            booleanValue(false),
          removalReason:
            stringValue(""),
          lastHeartbeatAtEpochMs:
            integerValue(seat.heartbeat),
        },
      );

    assert(
      call.response.ok,
      `${matchId}: ${seat.id} seed failed.`,
    );
  }

  call =
    await patchDoc(
      `lobbies/${lobbyId}`,
      {
        hostAccountId:
          stringValue(host.uid),
        matchId:
          stringValue(matchId),
        lifecycleState:
          stringValue("starting"),
        hostHeartbeatAtEpochMs:
          integerValue(hostHeartbeatAt),
        hostMigrationEpoch:
          integerValue(authorityEpoch),
        schemaVersion:
          integerValue(1),
      },
    );

  assert(
    call.response.ok,
    `${lobbyId}: lobby seed failed.`,
  );

  for (const seat of seats) {
    call =
      await patchDoc(
        `lobbies/${lobbyId}/members/${seat.id}`,
        {
          seatId:
            stringValue(seat.id),
          slotIndex:
            integerValue(seat.slot),
          active:
            booleanValue(true),
          seatMode:
            stringValue(seat.mode),
          seatType:
            stringValue(
              seat.controller === "permanent_bot" ?
                "bot" :
                "human"
            ),
          accountId:
            stringValue(seat.accountId),
          localOwnerAccountId:
            stringValue(seat.owner),
          isHost:
            booleanValue(seat.isHost),
          controllerKind:
            stringValue(seat.controller),
          connectionState:
            stringValue("connected"),
          readyForRevision:
            integerValue(0),
        },
      );

    assert(
      call.response.ok,
      `${lobbyId}: member ${seat.id} seed failed.`,
    );
  }

  return now;
}

async function cleanupFixture(matchId, lobbyId) {
  await deleteDoc(
    `matches/${matchId}/network/state`,
  );

  for (let i = 1; i <= 4; i++) {
    await deleteDoc(
      `matches/${matchId}/seats/seat_${i}`,
    );
    await deleteDoc(
      `lobbies/${lobbyId}/members/seat_${i}`,
    );
  }

  await deleteDoc(`matches/${matchId}`);
  await deleteDoc(`lobbies/${lobbyId}`);
}

let host;
let guest2;
let guest3;

const fixtures = [
  {
    matchId:
      `phase12d-a-${Date.now()}`,
    lobbyId:
      `phase12d-lobby-a-${Date.now()}`,
  },
  {
    matchId:
      `phase12d-b-${Date.now()}`,
    lobbyId:
      `phase12d-lobby-b-${Date.now()}`,
  },
  {
    matchId:
      `phase12d-c-${Date.now()}`,
    lobbyId:
      `phase12d-lobby-c-${Date.now()}`,
  },
];

try {
  console.log(
    "AtlasBoard Phase 12D Host Crash Election Local E2E",
  );

  console.log("[0/11] Emulator safety preflight...");
  await verifyEmulators();
  console.log("[0/11] PASS.");

  console.log("[1/11] Create three real Human accounts...");
  host = await createAuthUser("host");
  guest2 = await createAuthUser("guest2");
  guest3 = await createAuthUser("guest3");
  console.log("[1/11] PASS.");

  console.log(
    "[2/11] Seed stale Host + unsafe current frame + safe recovery frame...",
  );

  const now = Date.now();

  await seedRecoveryFixture({
    ...fixtures[0],
    host,
    guest2,
    guest3,
    hostHeartbeatAt:
      now - 30_000,
    guest2HeartbeatAt:
      now,
    guest3HeartbeatAt:
      now,
    authorityEpoch: 4,
    unsafeCurrent: true,
  });

  console.log("[2/11] PASS.");

  console.log(
    "[3/11] Concurrent recovery probes cannot create split-brain...",
  );

  const [probe2, probe3] =
    await Promise.all([
      callFunction(
        "matchTryRecoverCrashedHost",
        guest2.token,
        {matchId: fixtures[0].matchId},
      ),
      callFunction(
        "matchTryRecoverCrashedHost",
        guest3.token,
        {matchId: fixtures[0].matchId},
      ),
    ]);

  assert(
    probe2.response.ok &&
      probe3.response.ok,
    "Concurrent recovery probe failed unexpectedly.",
  );

  let match =
    await getDoc(
      `matches/${fixtures[0].matchId}`,
    );

  assert(
    match.response.ok &&
      match.json?.fields?.hostAccountId?.stringValue ===
        guest2.uid,
    "Lowest-slot live Human did not win deterministic election.",
  );

  assert(
    Number(
      match.json?.fields?.authorityEpoch?.integerValue ?? 0
    ) === 5,
    "Authority epoch did not advance exactly once.",
  );

  console.log("[3/11] PASS.");

  console.log(
    "[4/11] Unsafe current state rolls back to last safe checkpoint...",
  );

  let state =
    await getDoc(
      `matches/${fixtures[0].matchId}/network/state`,
    );

  assert(
    state.response.ok &&
      state.json?.fields?.phase?.stringValue ===
        "awaiting_roll" &&
      state.json?.fields?.snapshotJson?.stringValue ===
        '{"checkpoint":"SAFE_RECOVERY"}' &&
      state.json?.fields?.authorityHostAccountId?.stringValue ===
        guest2.uid,
    "Crash recovery did not restore the safe checkpoint.",
  );

  console.log("[4/11] PASS.");

  console.log(
    "[5/11] Old Host -> TemporaryBot; winner is sole Host...",
  );

  const seat1 =
    await getDoc(
      `matches/${fixtures[0].matchId}/seats/seat_1`,
    );
  const seat2 =
    await getDoc(
      `matches/${fixtures[0].matchId}/seats/seat_2`,
    );
  const seat3 =
    await getDoc(
      `matches/${fixtures[0].matchId}/seats/seat_3`,
    );
  const seat4 =
    await getDoc(
      `matches/${fixtures[0].matchId}/seats/seat_4`,
    );

  const seats =
    [seat1, seat2, seat3, seat4];

  const hostCount =
    seats.filter((seat) =>
      seat.json?.fields?.isHost?.booleanValue === true
    ).length;

  assert(
    hostCount === 1 &&
      seat1.json?.fields?.controllerKind?.stringValue ===
        "temporary_bot" &&
      seat1.json?.fields?.connectionState?.stringValue ===
        "reconnecting" &&
      seat2.json?.fields?.isHost?.booleanValue === true &&
      seat2.json?.fields?.controllerKind?.stringValue ===
        "human" &&
      seat3.json?.fields?.isHost?.booleanValue === false,
    "Post-recovery seat roles are invalid.",
  );

  console.log("[5/11] PASS.");

  console.log(
    "[6/11] Host-owned local Bot is normalized after Host loss...",
  );

  assert(
    seat4.json?.fields?.controllerKind?.stringValue ===
      "permanent_bot" &&
      seat4.json?.fields?.localOwnerAccountId?.stringValue ===
      "" &&
      seat4.json?.fields?.connectionState?.stringValue ===
      "host_owner_lost",
    "Old Host-owned Bot was not normalized.",
  );

  console.log("[6/11] PASS.");

  console.log(
    "[7/11] Repeated loser probe cannot increment epoch again...",
  );

  const epochBefore =
    Number(
      match.json?.fields?.authorityEpoch?.integerValue ?? 0
    );

  const repeatLoser =
    await callFunction(
      "matchTryRecoverCrashedHost",
      guest3.token,
      {matchId: fixtures[0].matchId},
    );

  assert(
    repeatLoser.response.ok,
    `Repeated loser probe failed: ${repeatLoser.text}`,
  );

  match =
    await getDoc(
      `matches/${fixtures[0].matchId}`,
    );

  assert(
    Number(
      match.json?.fields?.authorityEpoch?.integerValue ?? 0
    ) === epochBefore &&
      match.json?.fields?.hostAccountId?.stringValue ===
        guest2.uid,
    "Repeated loser probe changed authority.",
  );

  console.log("[7/11] PASS.");

  console.log(
    "[8/11] If lowest slot is stale, next live Human wins...",
  );

  const nowB = Date.now();

  await seedRecoveryFixture({
    ...fixtures[1],
    host,
    guest2,
    guest3,
    hostHeartbeatAt:
      nowB - 30_000,
    guest2HeartbeatAt:
      nowB - 30_000,
    guest3HeartbeatAt:
      nowB,
    authorityEpoch: 8,
    unsafeCurrent: false,
  });

  const staleGuest2Probe =
    await callFunction(
      "matchTryRecoverCrashedHost",
      guest2.token,
      {matchId: fixtures[1].matchId},
    );

  assert(
    staleGuest2Probe.response.ok,
    "Stale candidate probe should be harmless.",
  );

  const liveGuest3Probe =
    await callFunction(
      "matchTryRecoverCrashedHost",
      guest3.token,
      {matchId: fixtures[1].matchId},
    );

  assert(
    liveGuest3Probe.response.ok,
    "Live Guest 3 recovery probe failed.",
  );

  match =
    await getDoc(
      `matches/${fixtures[1].matchId}`,
    );

  assert(
    match.json?.fields?.hostAccountId?.stringValue ===
      guest3.uid &&
      Number(
        match.json?.fields?.authorityEpoch?.integerValue ?? 0
      ) === 9,
    "Next eligible live Human did not win election.",
  );

  console.log("[8/11] PASS.");

  console.log(
    "[9/11] Fresh Host heartbeat blocks premature recovery...",
  );

  const nowC = Date.now();

  await seedRecoveryFixture({
    ...fixtures[2],
    host,
    guest2,
    guest3,
    hostHeartbeatAt:
      nowC,
    guest2HeartbeatAt:
      nowC,
    guest3HeartbeatAt:
      nowC,
    authorityEpoch: 12,
    unsafeCurrent: false,
  });

  const [fresh2, fresh3] =
    await Promise.all([
      callFunction(
        "matchTryRecoverCrashedHost",
        guest2.token,
        {matchId: fixtures[2].matchId},
      ),
      callFunction(
        "matchTryRecoverCrashedHost",
        guest3.token,
        {matchId: fixtures[2].matchId},
      ),
    ]);

  assert(
    fresh2.response.ok &&
      fresh3.response.ok,
    "Fresh heartbeat probes should be harmless.",
  );

  match =
    await getDoc(
      `matches/${fixtures[2].matchId}`,
    );

  assert(
    match.json?.fields?.hostAccountId?.stringValue ===
      host.uid &&
      Number(
        match.json?.fields?.authorityEpoch?.integerValue ?? 0
      ) === 12,
    "Recovery happened before Host lease expired.",
  );

  console.log("[9/11] PASS.");

  console.log(
    "[10/11] Lobby Host projection follows recovered authority...",
  );

  const lobby =
    await getDoc(
      `lobbies/${fixtures[0].lobbyId}`,
    );

  assert(
    lobby.response.ok &&
      lobby.json?.fields?.hostAccountId?.stringValue ===
        guest2.uid &&
      Number(
        lobby.json?.fields?.hostMigrationEpoch?.integerValue ?? 0
      ) === 5,
    "Lobby Host projection did not follow recovered match authority.",
  );

  console.log("[10/11] PASS.");

  console.log(
    "[11/11] Recovery state remains active and internally consistent...",
  );

  state =
    await getDoc(
      `matches/${fixtures[0].matchId}/network/state`,
    );

  match =
    await getDoc(
      `matches/${fixtures[0].matchId}`,
    );

  assert(
    match.json?.fields?.status?.stringValue === "active" &&
      state.json?.fields?.authorityHandoffReason?.stringValue ===
        "host_timeout" &&
      Number(
        state.json?.fields?.authorityEpoch?.integerValue ?? 0
      ) ===
        Number(
          match.json?.fields?.authorityEpoch?.integerValue ?? 0
        ),
    "Recovered match/state authority metadata is inconsistent.",
  );

  console.log("[11/11] PASS.");
  console.log("");
  console.log(
    "AtlasBoard Phase 12D Host Crash Election Local E2E PASSED 11/11.",
  );
} catch (error) {
  console.error("");
  console.error(
    "AtlasBoard Phase 12D Host Crash Election Local E2E FAILED.",
  );
  console.error(error);
  process.exitCode = 1;
} finally {
  console.log("[cleanup] Removing Phase 12D fixtures...");

  for (const fixture of fixtures) {
    await cleanupFixture(
      fixture.matchId,
      fixture.lobbyId,
    );
  }

  await deleteAuthUser(host);
  await deleteAuthUser(guest2);
  await deleteAuthUser(guest3);

  console.log("[cleanup] Done.");
}
