/**
 * AtlasBoard Phase 10 progression / achievements E2E.
 *
 * FIX (2026-10-12): Old fixture seeding used unauthenticated Firestore REST
 * PATCH calls. Current deny-by-default Firestore Rules correctly reject those
 * calls (403). Use Admin SDK connected ONLY to the local Firestore emulator to
 * create server-owned fixtures and perform cleanup; retain callable tests
 * using real Auth Emulator ID tokens. Never change Firestore Rules for tests.
 *
 * From Backend/Firebase (with Firebase emulators CLOSED):
 * npx.cmd firebase emulators:exec --project atlasboard-usa --only auth,firestore,functions "node functions/scripts/progression-phase10-e2e.mjs"
 *
 * Offline sanity: node functions/scripts/progression-phase10-e2e.mjs --self-test
 * Single full suite: node functions/scripts/progression-phase10-e2e.mjs --security-suite
 * SECURITY PROBE MUST RUN ONLY VIA LOCAL firebase emulators:exec.
 */
import {createHash, randomBytes} from 'node:crypto';

const PROJECT_ID = 'atlasboard-usa';
const REGION = 'europe-west1';
const HOST = '127.0.0.1';
const AUTH_PORT = 9099;
const FIRESTORE_PORT = 8080;
const FUNCTIONS_PORT = 5001;
const HUB_PORT = 4400;
const AUTH_BASE = `http://${HOST}:${AUTH_PORT}`;
const FUNCTIONS_BASE = `http://${HOST}:${FUNCTIONS_PORT}`;
const HUB_BASE = `http://${HOST}:${HUB_PORT}`;
const FIRESTORE_HOST = `${HOST}:${FIRESTORE_PORT}`;
const API_KEY = 'atlasboard-local-emulator-only';
const TIMEOUT_MS = 15000;
const HEALTH_DOCUMENT_PATH = 'atlasboard_phase10_readonly_health/probe';

function assert(ok, message) {
  if (!ok) throw new Error(message);
}

function sha256(text) {
  return createHash('sha256').update(text, 'utf8').digest('hex');
}

// Firestore reserves IDs matching __.*__, and doc() requires collection/doc pairs.
// Validate BOTH properties before any SDK request; avoids emulator-only failures.
function assertValidFirestoreDocumentPath(path) {
  const segments = typeof path === 'string' ? path.split('/') : [];
  assert(segments.length >= 2 && segments.length % 2 === 0 &&
    segments.every((part) => part.length > 0 && !/^__.*__$/.test(part)),
  'Invalid Firestore document path (must have collection/doc pairs without reserved IDs)');
}

function selfTest() {
  assert(PROJECT_ID === 'atlasboard-usa', 'Project changed');
  assert(FIRESTORE_HOST === '127.0.0.1:8080', 'Firestore target changed');
  assert(AUTH_BASE === 'http://127.0.0.1:9099', 'Auth target changed');
  assert(FUNCTIONS_BASE === 'http://127.0.0.1:5001', 'Functions target changed');
  assert(sha256('a') === 'ca978112ca1bbdcafac231b39a23dc4da786eff8147c4e72b9807785afee48bb', 'Hash fixture');
  assertValidFirestoreDocumentPath(HEALTH_DOCUMENT_PATH);
  for (const invalid of [
    '__atlasboard_phase10_readonly_health__/probe',
    'atlasboard_phase10_readonly_health',
    'atlasboard_phase10_readonly_health/',
  ]) {
    let rejected = false;
    try { assertValidFirestoreDocumentPath(invalid); } catch { rejected = true; }
    assert(rejected, `Self-test failed to reject invalid Firestore path: ${invalid}`);
  }
  console.log('PASS self-test: strict localhost targets, fixed project, deterministic fixture IDs.');
}

async function requestJson(url, options = {}) {
  const response = await fetch(url, {
    ...options,
    signal: AbortSignal.timeout(TIMEOUT_MS),
  });
  const raw = await response.text();
  let json = null;
  if (raw) {
    try { json = JSON.parse(raw); } catch { json = {raw}; }
  }
  return {response, json, text: raw};
}

async function postJson(url, data, headers = {}) {
  return requestJson(url, {
    method: 'POST',
    headers: {'Content-Type': 'application/json', ...headers},
    body: JSON.stringify(data),
  });
}

function callableUrl(name) {
  assert(/^[a-zA-Z][a-zA-Z0-9]*$/.test(name), 'Invalid callable name');
  return `${FUNCTIONS_BASE}/${PROJECT_ID}/${REGION}/${name}`;
}

async function call(name, token, data = {}) {
  return postJson(callableUrl(name), {data}, {
    Authorization: `Bearer ${token}`,
    'X-Firebase-AppCheck': 'atlasboard-local-emulator-development-only',
  });
}

function result(response) {
  return response.json?.result ?? response.json?.data;
}

function emulatorAt(entry, port) {
  const entries = Array.isArray(entry) ? entry : [entry];
  return entries.some((item) => {
    if (!item || typeof item !== 'object') return false;
    return (item.host === '127.0.0.1' || item.host === 'localhost') &&
      Number(item.port) === port;
  });
}

async function preflight() {
  assert(!process.env.GOOGLE_CLOUD_PROJECT || process.env.GOOGLE_CLOUD_PROJECT === PROJECT_ID,
    'GOOGLE_CLOUD_PROJECT is not the expected development project');
  assert(!process.env.GCLOUD_PROJECT || process.env.GCLOUD_PROJECT === PROJECT_ID,
    'GCLOUD_PROJECT is not the expected development project');
  assert(!process.env.FIRESTORE_EMULATOR_HOST ||
    process.env.FIRESTORE_EMULATOR_HOST === FIRESTORE_HOST,
    'Refusing unexpected FIRESTORE_EMULATOR_HOST');
  assert(!process.env.FIREBASE_AUTH_EMULATOR_HOST ||
    process.env.FIREBASE_AUTH_EMULATOR_HOST === `${HOST}:${AUTH_PORT}`,
    'Refusing unexpected FIREBASE_AUTH_EMULATOR_HOST');

  const hub = await requestJson(`${HUB_BASE}/emulators`);
  assert(hub.response.ok, 'Local Firebase Emulator Hub unavailable (127.0.0.1:4400)');
  for (const [name, port] of [
    ['auth', AUTH_PORT], ['firestore', FIRESTORE_PORT], ['functions', FUNCTIONS_PORT],
  ]) {
    assert(emulatorAt(hub.json?.[name], port),
      `Expected ${name} emulator at 127.0.0.1:${port}; refusing to run`);
  }

  // SDK must never be initialized before the emulator destination is verified.
  process.env.FIRESTORE_EMULATOR_HOST = FIRESTORE_HOST;
  process.env.FIREBASE_AUTH_EMULATOR_HOST = `${HOST}:${AUTH_PORT}`;
  const [{initializeApp, getApps}, {getFirestore, Timestamp}] = await Promise.all([
    import('firebase-admin/app'),
    import('firebase-admin/firestore'),
  ]);
  const app = getApps().find((candidate) => candidate.name === 'atlas-progress-test') ??
    initializeApp({projectId: PROJECT_ID}, 'atlas-progress-test');
  const db = getFirestore(app);
  // Read-only probe: valid, unreserved collection/doc path, no writes.
  assertValidFirestoreDocumentPath(HEALTH_DOCUMENT_PATH);
  await db.doc(HEALTH_DOCUMENT_PATH).get();
  return {db, Timestamp};
}

async function signup(label, nonce) {
  const reply = await postJson(
    `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=${API_KEY}`,
    {
      email: `atlasboard.progression.${label}.${nonce}@example.com`,
      password: `AtlasProgression!${label}!${nonce}`,
      returnSecureToken: true,
    },
  );
  assert(reply.response.ok, `Auth sign-up failed (${label}): ${reply.text}`);
  assert(reply.json?.idToken && reply.json?.localId, `Missing Auth credentials (${label})`);
  return {uid: reply.json.localId, token: reply.json.idToken};
}

async function deleteAuthUser(user) {
  if (!user?.token) return;
  const reply = await postJson(
    `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/accounts:delete?key=${API_KEY}`,
    {idToken: user.token},
  );
  assert(reply.response.ok, `Auth fixture cleanup failed: ${reply.text}`);
}

async function main() {
  if (process.argv.length > 3 ||
    (process.argv[2] && !['--self-test', '--security-suite'].includes(process.argv[2]))) {
    throw new Error('Use without arguments, --self-test, --security-suite, or --host-integrity-audit');
  }
  if (process.argv[2] === '--self-test') {
    selfTest();
    return;
  }

  console.log('Atlas Board Phase 10 Progression + Achievements E2E (protected-fixture fix)');
  console.log('Safety: local Auth/Firestore/Functions emulators ONLY; no production calls.');
  const {db, Timestamp} = await preflight();
  console.log('[1/14] PASS emulator safety preflight and Admin SDK local routing.');

  const nonce = `${Date.now()}-${randomBytes(5).toString('hex')}`;
  const matchId = `phase10_match_${nonce}`;
  const lobbyId = `phase10_lobby_${nonce}`;
  const host = {uid: '', token: ''};
  const guest = {uid: '', token: ''};
  const cleanupWarnings = [];

  const ref = (path) => db.doc(path);
  const seed = (path, data) => ref(path).set(data);
  const get = (path) => ref(path).get();

  try {
    Object.assign(host, await signup('host', nonce));
    Object.assign(guest, await signup('guest', nonce));
    console.log('[2/14] PASS two authenticated account identities.');

    // Server-owned fixture data, seeded by the test runner's Admin SDK ONLY.
    // Real players cannot write these collections under Firestore Rules.
    await seed(`public_profiles/${host.uid}`, {
      uid: host.uid, displayName: 'Phase10 Host', avatarId: 'avatar_host',
    });
    await seed(`public_profiles/${guest.uid}`, {
      uid: guest.uid, displayName: 'Phase10 Guest', avatarId: 'avatar_guest',
    });
    await seed(`matches/${matchId}`, {
      matchId, lobbyId, hostAccountId: host.uid, mode: 'online',
      status: 'running', mapId: 'Turkey', themeId: 'classic_table',
      roundLimit: 20, maxPlayers: 2,
    });
    await seed(`matches/${matchId}/seats/seat_host`, {
      seatId: 'seat_host', slotIndex: 0, active: true, seatType: 'human',
      accountId: host.uid, localOwnerAccountId: host.uid,
      displayName: 'Phase10 Host', controllerKind: 'local_human', isHost: true,
    });
    await seed(`matches/${matchId}/seats/seat_guest`, {
      seatId: 'seat_guest', slotIndex: 1, active: true, seatType: 'human',
      accountId: guest.uid, localOwnerAccountId: '',
      displayName: 'Phase10 Guest', controllerKind: 'human', isHost: false,
    });

    const finalFrame = {
      schemaVersion: 7,
      phase: 'match_complete',
      activeSlotIndex: 0,
      currentRound: 8,
      roundLimit: 20,
      diceSequence: 7,
      matchResult: {
        valid: true, highestNetWorth: 5200, winnerSlots: [0],
        participating: [true, true, false, false],
        bankrupt: [false, false, false, false],
        cash: [2400, 1300, 0, 0], propertyCount: [2, 1, 0, 0],
        propertyValue: [1600, 900, 0, 0],
        developmentLevels: [3, 1, 0, 0],
        developmentValue: [1200, 400, 0, 0],
        netWorth: [5200, 2600, 0, 0],
      },
    };
    const approvedSnapshotJson = JSON.stringify(finalFrame);
    await seed(`matches/${matchId}/network/state`, {
      revision: 18, phase: 'match_complete', turnSeatId: 'seat_host',
      eventSequence: 44, snapshotJson: approvedSnapshotJson,
      networkSchemaVersion: 1,
    });
    // Only a future *independent* backend verifier can mint this receipt in
    // real gameplay. Admin SDK creates it here strictly as a local QA fixture.
    // Ordinary clients cannot write this protected collection.
    await ref(`matches/${matchId}`).update({status: 'complete'});
    await seed(`match_result_verifications/${matchId}`, {
      schemaVersion: 1, verificationStatus: 'verified',
      verificationMethod: 'server_authoritative_replay_v1',
      verifiedBy: 'atlasboard-backend-verifier-v1',
      matchId, hostAccountId: host.uid, networkPhase: 'match_complete',
      networkRevision: 18, networkEventSequence: 44,
      snapshotSha256: sha256(approvedSnapshotJson),
      verifiedAt: Timestamp.fromMillis(Date.now() - 1000),
    });
    console.log('[3/14] PASS canonical completed match + local-only trusted verifier fixture seeded.');

    const denied = await call('progressionRecordCompletedMatch', guest.token, {
      matchId, completedTurns: 14, telemetry: [],
    });
    assert(!denied.response.ok, 'Non-host progression finalization unexpectedly succeeded');
    assert(denied.json?.error?.details?.errorKey === 'progression.error.host_required',
      `Unexpected non-host error: ${denied.text}`);
    console.log('[4/14] PASS non-host finalization rejected.');

    const finalize = await call('progressionRecordCompletedMatch', host.token, {
      matchId, completedTurns: 14,
      telemetry: [
        {slotIndex: 0, diceRolls: 4, doublesRolled: 1, totalDiceValue: 30, highestRoll: 10},
        {slotIndex: 1, diceRolls: 3, doublesRolled: 0, totalDiceValue: 17, highestRoll: 8},
      ],
    });
    assert(finalize.response.ok, `Host finalization failed: ${finalize.text}`);
    const finalResult = result(finalize);
    assert(finalResult?.ok === true && finalResult?.applied === true,
      'Completed match was not applied');
    assert(finalResult?.affectedAccountCount === 2,
      'Expected both human accounts to receive progression');
    console.log('[5/14] PASS host finalized progression for every human account.');

    const replay = await call('progressionRecordCompletedMatch', host.token, {
      matchId, completedTurns: 14, telemetry: [],
    });
    assert(replay.response.ok && result(replay)?.idempotentReplay === true,
      'Completed-match replay was not idempotent');
    console.log('[6/14] PASS completed-match replay is idempotent.');

    const hostProfileCall = await call('progressionGetProfile', host.token);
    assert(hostProfileCall.response.ok, `Host profile failed: ${hostProfileCall.text}`);
    const hostProfile = result(hostProfileCall);
    assert(hostProfile?.stats?.matchesPlayed === 1, 'Host match count mismatch');
    assert(hostProfile?.stats?.wins === 1 && hostProfile?.stats?.losses === 0,
      'Host result stats mismatch');
    assert(hostProfile?.stats?.diceRolls === 4 && hostProfile?.stats?.doublesRolled === 1,
      'Host dice telemetry mismatch');
    assert(hostProfile?.stats?.propertiesAtFinishTotal === 2,
      'Host property aggregate mismatch');
    assert(hostProfile?.stats?.developmentLevelsAtFinishTotal === 3,
      'Host development aggregate mismatch');
    const turkeyStats = (hostProfile?.mapStats ?? [])
      .find((item) => item?.mapId === 'Turkey')?.stats;
    assert(turkeyStats?.matchesPlayed === 1 && turkeyStats?.wins === 1,
      'Turkey-scoped result statistics mismatch');
    assert(turkeyStats?.diceRolls === 4 && turkeyStats?.highestNetWorth === 5200,
      'Turkey-scoped gameplay/record statistics mismatch');
    console.log('[7/14] PASS cumulative + per-map player statistics persisted.');

    assert(hostProfile?.totalXp === 118,
      `Host XP mismatch: ${hostProfile?.totalXp}`);
    assert(hostProfile?.level === 1 && hostProfile?.nextLevelXp === 500,
      'Host level metadata mismatch');
    console.log('[8/14] PASS account XP and player level derived server-side.');

    const unlockedIds = new Set(
      (hostProfile?.achievements ?? [])
        .filter((item) => item.unlocked === true)
        .map((item) => item.achievementId),
    );
    for (const id of ['first_match', 'first_win', 'turkey_explorer']) {
      assert(unlockedIds.has(id), `Expected host achievement missing: ${id}`);
    }
    console.log('[9/14] PASS achievement unlocks derived from completed match stats.');

    assert(Array.isArray(hostProfile?.recentMatches) && hostProfile.recentMatches.length === 1,
      'Expected one retained host match');
    const retained = hostProfile.recentMatches[0];
    assert(retained.result === 'win' && retained.place === 1,
      'Host history result/place mismatch');
    assert(retained.netWorth === 5200 && retained.xpAwarded === 118,
      'Host history economy/XP mismatch');
    assert(Array.isArray(retained.participants) && retained.participants.length === 2,
      'Match details did not retain both participants');
    console.log('[10/14] PASS match history keeps result, lobby/rules and detail evidence.');

    const guestProfileCall = await call('progressionGetProfile', guest.token);
    assert(guestProfileCall.response.ok, `Guest profile failed: ${guestProfileCall.text}`);
    const guestProfile = result(guestProfileCall);
    assert(guestProfile?.stats?.matchesPlayed === 1 && guestProfile?.stats?.losses === 1,
      'Guest match/loss stats mismatch');
    assert(guestProfile?.recentMatches?.[0]?.result === 'loss',
      'Guest history result mismatch');
    console.log('[11/14] PASS every participating account receives its own result/history.');

    const now = Date.now();
    const oldId = `phase10_old_${nonce}`;
    await seed(`account_match_history/${host.uid}/matches/${oldId}`, {
      uid: host.uid, matchId: oldId, result: 'loss',
      finishedAt: Timestamp.fromDate(new Date(now - 4 * 24 * 60 * 60 * 1000)),
    });
    for (let idx = 0; idx < 11; idx++) {
      const id = `phase10_recent_${idx}_${nonce}`;
      await seed(`account_match_history/${host.uid}/matches/${id}`, {
        uid: host.uid, matchId: id, result: idx % 2 === 0 ? 'win' : 'loss',
        finishedAt: Timestamp.fromDate(new Date(now - (idx + 1) * 60_000)),
      });
    }
    const prunedProfileCall = await call('progressionGetProfile', host.token);
    assert(prunedProfileCall.response.ok,
      `Pruned profile failed: ${prunedProfileCall.text}`);
    const prunedProfile = result(prunedProfileCall);
    assert(prunedProfile?.recentMatches?.length === 10,
      `History cap mismatch: ${prunedProfile?.recentMatches?.length}`);
    const oldLookup = await get(`account_match_history/${host.uid}/matches/${oldId}`);
    assert(!oldLookup.exists, 'History older than 72 hours was not deleted');
    console.log('[12/14] PASS 72-hour history retention enforced even below/above cap.');

    const returned = prunedProfile.recentMatches.map((item) => item.matchId);
    assert(returned.length === 10 && new Set(returned).size === 10,
      'Newest-ten history response is not unique/capped');
    console.log('[13/14] PASS user-facing history is limited to newest 10 records.');

    const marker = await get(`progression_match_events/${matchId}`);
    const guestHistory = await get(`account_match_history/${guest.uid}/matches/${matchId}`);
    assert(marker.exists, 'Immutable progression match event missing');
    assert(guestHistory.exists, 'Guest retained match history missing');
    console.log('[14/14] PASS canonical progression evidence and account history persisted.');

    console.log('Phase 10 Progression + Achievements E2E PASS: 14 checks.');
  } finally {
    // Clean ONLY documents scoped to this test's unique nonce/accounts.
    console.log('Cleanup: removing this run\'s created fixtures from LOCAL emulators...');
    const remove = async (path) => {
      try { await ref(path).delete(); }
      catch (error) { cleanupWarnings.push(`${path}: ${error.message}`); }
    };
    if (host.uid) {
      // recursiveDelete removes only this newly-created account's history tree.
      try { await db.recursiveDelete(ref(`account_match_history/${host.uid}`)); }
      catch (error) { cleanupWarnings.push(`host history: ${error.message}`); }
      await remove(`account_progression/${host.uid}`);
      await remove(`public_profiles/${host.uid}`);
    }
    if (guest.uid) {
      try { await db.recursiveDelete(ref(`account_match_history/${guest.uid}`)); }
      catch (error) { cleanupWarnings.push(`guest history: ${error.message}`); }
      await remove(`account_progression/${guest.uid}`);
      await remove(`public_profiles/${guest.uid}`);
    }
    await remove(`match_result_verifications/${matchId}`);
    await remove(`progression_match_events/${matchId}`);
    if (host.uid && guest.uid) {
      for (const uid of [host.uid, guest.uid]) {
        for (const achievementId of [
          'first_match', 'first_win', 'networth_5000',
          'turkey_explorer', 'colorado_explorer', 'usa_explorer',
        ]) {
          await remove(`achievement_events/${sha256(`${matchId}:${uid}:${achievementId}`)}`);
        }
      }
    }
    try { await db.recursiveDelete(ref(`matches/${matchId}`)); }
    catch (error) { cleanupWarnings.push(`match: ${error.message}`); }

    for (const account of [host, guest]) {
      if (!account.token) continue;
      try { await deleteAuthUser(account); }
      catch (error) { cleanupWarnings.push(`auth: ${error.message}`); }
    }
    if (cleanupWarnings.length) {
      for (const warning of cleanupWarnings) console.warn(`CLEANUP WARNING: ${warning}`);
      process.exitCode = 1;
    } else {
      console.log('Cleanup: temporary Phase 10 emulator fixtures removed without warnings.');
    }
  }
}

/**
 * Separate local-only negative security probe for a malicious MATCH HOST.
 *
 * This does NOT modify application code, Firestore rules or real accounts.
 * Host and guest are temporary emulator Auth users; all server-owned fixtures
 * are created via Admin SDK after the strict localhost emulator preflight.
 *
 * The probe intentionally requests a fabricated immediate victory WITHOUT
 * game turns, then checks whether progression/XP actually persists.
 * A confirmed finding returns exit code 2, NOT a misleading green PASS.
 */
async function runHostIntegrityAudit() {
  console.log('AtlasBoard Security Audit | HOST_RESULT_INTEGRITY_NEGATIVE');
  console.log('Safety: isolated LOCAL Firebase Auth/Firestore/Functions emulators ONLY.');
  const {db} = await preflight();
  const nonce = `${Date.now()}-${randomBytes(5).toString('hex')}`;
  const matchId = `phase10_integrity_${nonce}`;
  const lobbyId = `phase10_integrity_lobby_${nonce}`;
  const host = {uid: '', token: ''};
  const guest = {uid: '', token: ''};
  const cleanupWarnings = [];
  const ref = (path) => db.doc(path);
  const seed = (path, data) => ref(path).set(data);
  const get = (path) => ref(path).get();

  try {
    Object.assign(host, await signup('integrityhost', nonce));
    Object.assign(guest, await signup('integrityguest', nonce));
    console.log('AUDIT_SETUP temporary host and guest emulator accounts created.');

    await seed(`public_profiles/${host.uid}`, {
      uid: host.uid, displayName: 'Security Audit Host', avatarId: '',
    });
    await seed(`public_profiles/${guest.uid}`, {
      uid: guest.uid, displayName: 'Security Audit Guest', avatarId: '',
    });
    await seed(`matches/${matchId}`, {
      matchId, lobbyId, hostAccountId: host.uid,
      mode: 'online', status: 'running', mapId: 'Turkey',
      themeId: 'classic_table', roundLimit: 20, maxPlayers: 2,
    });
    await seed(`matches/${matchId}/seats/seat_host`, {
      seatId: 'seat_host', slotIndex: 0, active: true,
      seatType: 'human', controllerKind: 'local_human',
      accountId: host.uid, localOwnerAccountId: host.uid,
      displayName: 'Security Audit Host', isHost: true,
    });
    await seed(`matches/${matchId}/seats/seat_guest`, {
      seatId: 'seat_guest', slotIndex: 1, active: true,
      seatType: 'human', controllerKind: 'human',
      accountId: guest.uid, localOwnerAccountId: '',
      displayName: 'Security Audit Guest', isHost: false,
    });
    // Only a just-started state exists. No turns, dice or economy events.
    await seed(`matches/${matchId}/network/state`, {
      revision: 0, eventSequence: 0, phase: 'starting_order',
      turnSeatId: 'seat_host', snapshotJson: '{"phase":"starting_order"}',
      schemaVersion: 1,
    });
    console.log('AUDIT_SETUP match has revision=0, eventSequence=0 and NO played turns.');

    const fakeResult = {
      schemaVersion: 7,
      phase: 'match_complete',
      activeSlotIndex: 0,
      currentRound: 20,
      roundLimit: 20,
      diceSequence: 0,
      matchResult: {
        valid: true, highestNetWorth: 9000000,
        winnerSlots: [0], participating: [true, true, false, false],
        bankrupt: [false, true, false, false],
        cash: [1000000, 0, 0, 0],
        propertyCount: [100, 0, 0, 0],
        propertyValue: [7000000, 0, 0, 0],
        developmentLevels: [100, 0, 0, 0],
        developmentValue: [1000000, 0, 0, 0],
        netWorth: [9000000, 0, 0, 0],
      },
    };

    // Positive sanity: a normal remote player cannot act as the host.
    const guestAttempt = await call('matchHostPublishState', guest.token, {
      matchId, expectedRevision: 0, phase: 'match_complete',
      turnSeatId: 'seat_host', eventSequence: 0,
      snapshotJson: JSON.stringify(fakeResult),
    });
    assert(!guestAttempt.response.ok &&
      guestAttempt.json?.error?.details?.errorKey === 'match.error.host_only',
      `Guest publish authority unexpected: ${guestAttempt.text}`);
    console.log('PASS non-host cannot publish a match result.');

    // Attack A: zero-turn fabricated completion MUST be rejected.
    const hostAttempt = await call('matchHostPublishState', host.token, {
      matchId, expectedRevision: 0, phase: 'match_complete',
      turnSeatId: 'seat_host', eventSequence: 0,
      snapshotJson: JSON.stringify(fakeResult),
    });
    assert(!hostAttempt.response.ok &&
      hostAttempt.json?.error?.details?.errorKey === 'match.error.premature_completion',
      `FAIL: zero-turn host publish not blocked: ${hostAttempt.text}`);
    console.log('PASS host cannot complete a zero-turn match.');

    // Attack B: simulate an older already-forged match state. Even if the
    // current transport validation is bypassed, NO persistent progression may
    // be minted without an independent server-verifier receipt.
    await ref(`matches/${matchId}`).update({status: 'complete'});
    await seed(`matches/${matchId}/network/state`, {
      revision: 1, eventSequence: 0, phase: 'match_complete',
      turnSeatId: 'seat_host', snapshotJson: JSON.stringify(fakeResult),
      schemaVersion: 1,
    });
    const pending = await call('progressionRecordCompletedMatch', host.token, {
      matchId, completedTurns: 0, telemetry: [],
    });
    assert(pending.response.ok && result(pending)?.applied === false &&
      result(pending)?.verificationStatus === 'pending_authoritative_verification' &&
      result(pending)?.affectedAccountCount === 0,
      `FAIL: forged unverified result created progression: ${pending.text}`);
    console.log('PASS forged host result cannot mint XP without server verification.');

    // A logged-in player must not be able to mint the trusted receipt via
    // direct Firestore REST; only privileged server/Admin writes are allowed.
    const forbiddenReceipt = await requestJson(
      `http://${FIRESTORE_HOST}/v1/projects/${PROJECT_ID}` +
        `/databases/(default)/documents/match_result_verifications/${matchId}`,
      {
        method: 'PATCH',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${host.token}`,
        },
        body: JSON.stringify({fields: {
          verificationStatus: {stringValue: 'verified'},
        }}),
      },
    );
    assert(forbiddenReceipt.response.status === 403,
      `FAIL: client can write server verification receipt: ${forbiddenReceipt.text}`);
    console.log('PASS authenticated clients cannot mint result receipts (Firestore 403).');

    const profileCall = await call('progressionGetProfile', host.token);
    assert(profileCall.response.ok, `Progression read failed: ${profileCall.text}`);
    const profile = result(profileCall);
    const marker = await get(`progression_match_events/${matchId}`);
    assert(!marker.exists && profile.totalXp === 0 &&
      profile.stats?.wins === 0 &&
      !(profile.achievements ?? []).some((item) =>
        item.achievementId === 'first_win' && item.unlocked),
    `FAIL: unverified persistent rewards found: ${profileCall.text}`);
    console.log('PASS XP=0, wins=0, first_win=false, progression marker absent.');

    // Attack C: a fake server receipt with a mismatched digest must not work.
    await seed(`match_result_verifications/${matchId}`, {
      schemaVersion: 1, verificationStatus: 'verified',
      verificationMethod: 'server_authoritative_replay_v1',
      verifiedBy: 'atlasboard-backend-verifier-v1',
      matchId, hostAccountId: host.uid, networkPhase: 'match_complete',
      networkRevision: 1, networkEventSequence: 0,
      snapshotSha256: '0'.repeat(64),
      verifiedAt: new Date(Date.now() - 1000),
    });
    const mismatched = await call('progressionRecordCompletedMatch', host.token, {
      matchId, completedTurns: 0, telemetry: [],
    });
    assert(mismatched.response.ok && result(mismatched)?.applied === false &&
      result(mismatched)?.verificationStatus === 'pending_authoritative_verification',
    `FAIL: forged receipt checksum allowed XP: ${mismatched.text}`);
    console.log('PASS a mismatched server verification receipt cannot mint rewards.');

    // Attack D: a host is still forbidden from publishing after completion.
    const rewrite = await call('matchHostPublishState', host.token, {
      matchId, expectedRevision: 1, phase: 'match_complete',
      turnSeatId: 'seat_host', eventSequence: 4,
      snapshotJson: JSON.stringify(fakeResult),
    });
    assert(!rewrite.response.ok &&
      rewrite.json?.error?.details?.errorKey === 'match.error.already_complete',
    `FAIL: host overwrote a completed match: ${rewrite.text}`);
    console.log('PASS host cannot rewrite a completed match state.');

    console.log('HOST_INTEGRITY_SECURITY_AUDIT_PASS: no fabricated persistent rewards.');
    console.log('NOTE: independent real-match server verifier remains unimplemented.');
  } finally {
    // Always remove only the unique fixture docs and temporary Auth users.
    console.log("AUDIT_CLEANUP removing isolated emulator fixtures...");
    const remove = async (path) => {
      try { await ref(path).delete(); }
      catch (error) { cleanupWarnings.push(`${path}: ${error.message}`); }
    };
    for (const account of [host, guest]) {
      if (!account.uid) continue;
      try { await db.recursiveDelete(ref(`account_match_history/${account.uid}`)); }
      catch (error) { cleanupWarnings.push(`history: ${error.message}`); }
      await remove(`account_progression/${account.uid}`);
      await remove(`public_profiles/${account.uid}`);
      for (const id of [
        'first_match', 'first_win', 'networth_5000',
        'turkey_explorer', 'colorado_explorer', 'usa_explorer',
      ]) {
        await remove(`achievement_events/${sha256(`${matchId}:${account.uid}:${id}`)}`);
      }
    }
    await remove(`progression_match_events/${matchId}`);
    await remove(`match_result_verifications/${matchId}`);
    try { await db.recursiveDelete(ref(`matches/${matchId}`)); }
    catch (error) { cleanupWarnings.push(`match: ${error.message}`); }
    for (const account of [host, guest]) {
      if (!account.token) continue;
      try { await deleteAuthUser(account); }
      catch (error) { cleanupWarnings.push(`auth: ${error.message}`); }
    }
    if (cleanupWarnings.length) {
      for (const warning of cleanupWarnings) console.warn(`AUDIT_CLEANUP_WARNING ${warning}`);
      process.exitCode = 1;
    } else {
      console.log('AUDIT_CLEANUP complete; no warnings.');
    }
  }
}

try {
  if (process.argv[2] === '--host-integrity-audit') {
    assert(process.argv.length === 3, 'Audit mode accepts no additional arguments');
    await runHostIntegrityAudit();
  } else if (process.argv[2] === '--security-suite') {
    assert(process.argv.length === 3, 'Security suite accepts no additional arguments');
    selfTest();
    await main();
    assert(process.exitCode !== 1, 'Positive progression suite cleanup failed');
    await runHostIntegrityAudit();
    console.log('ATLASBOARD_SECURITY_CORE_01_INTEGRATED_PASS (emulator only).');
  } else {
    await main();
  }
} catch (error) {
  console.error('Phase 10 security/progression test FAILED:', error.stack ?? error.message);
  process.exitCode = 1;
}
