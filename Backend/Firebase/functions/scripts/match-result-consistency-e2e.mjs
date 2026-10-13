/**
 * Phase 17A / 17A.3 integrity gates: unit checks, callable E2E, and
 * finalization/receipt adversarial concurrency tests.
 * This is NOT a replay verifier. Consistent fabricated results remain untrusted
 * and cannot mint rewards without independently created protected proof.
 *
 * Run via scripts/qa.mjs --full, or --self-test after the TS build.
 */
import assert from 'node:assert/strict';
import {randomBytes} from 'node:crypto';
import {join, dirname, resolve} from 'node:path';
import {fileURLToPath, pathToFileURL} from 'node:url';

const PROJECT = 'atlasboard-usa';
const HOST = '127.0.0.1';
const AUTH = `http://${HOST}:9099`;
const HUB = `http://${HOST}:4400`;
const FIRESTORE = `${HOST}:8080`;
const FUNCTIONS = `http://${HOST}:5001/${PROJECT}/europe-west1`;
const LIB = resolve(dirname(fileURLToPath(import.meta.url)), '../lib/security/match-result-consistency.js');

let validator;
async function getValidator() {
  if (!validator) {
    const module = await import(pathToFileURL(LIB).href);
    assert.equal(typeof module.validateMatchResultConsistency, 'function',
      'Compiled real TypeScript validator unavailable; run npm run build first.');
    validator = module.validateMatchResultConsistency;
  }
  return validator;
}

function frame() {
  return {
    schemaVersion: 9, phase: 'match_complete', currentRound: 8,
    roundLimit: 20, completedTurns: 14, diceSequence: 7,
    matchResult: {
      valid: true, highestNetWorth: 5200, winnerSlots: [0],
      participating: [true, true, false, false],
      bankrupt: [false, false, false, false],
      cash: [2400, 1300, 0, 0],
      propertyCount: [2, 1, 0, 0],
      propertyValue: [1600, 900, 0, 0],
      developmentLevels: [3, 1, 0, 0],
      developmentValue: [1200, 400, 0, 0],
      netWorth: [5200, 2600, 0, 0],
    },
  };
}
function changed(mutator) {
  const data = frame();
  mutator(data);
  return data;
}

async function selfTest() {
  const validate = await getValidator();
  let total = 0;
  function check(input, expected, reason, label) {
    const output = validate(input);
    assert.equal(output.ok, expected, `${label}: ${JSON.stringify(output)}`);
    if (reason) assert.equal(output.reason, reason, label);
    total++;
  }
  check(frame(), true, 'CONSISTENT_UNTRUSTED_HOST_RESULT', 'baseline');
  const cases = [
    [changed(f => { f.matchResult.netWorth[0] += 1; }), 'NET_WORTH_MISMATCH', 'forged wealth'],
    [changed(f => { f.matchResult.winnerSlots = [1]; }), 'WINNER_RANKING_MISMATCH', 'wrong winner'],
    [changed(f => { f.matchResult.winnerSlots = [0, 0]; }), 'INVALID_WINNER_SLOTS', 'duplicate winner'],
    [changed(f => { f.matchResult.highestNetWorth += 1; }), 'WINNER_RANKING_MISMATCH', 'false highest'],
    [changed(f => { f.matchResult.winnerSlots = [0, 3]; }), 'WINNER_RANKING_MISMATCH', 'inactive winner'],
    [changed(f => { f.matchResult.netWorth = [5200, 2600]; }), 'INVALID_RESULT_ARRAYS', 'missing slots'],
    [changed(f => { f.matchResult.cash[0] = '2400'; }), 'INVALID_RESULT_ARRAYS', 'type confusion'],
    [changed(f => { f.matchResult.participating[2] = false; f.matchResult.cash[2] = 1; }), 'NON_PARTICIPANT_HAS_RESULT', 'inactive data'],
    [changed(f => { f.matchResult.propertyValue[0] = -3; }), 'NEGATIVE_ASSET_VALUES', 'negative asset'],
    [changed(f => { f.matchResult.valid = false; }), 'RESULT_NOT_VALID', 'invalid marker'],
    [changed(f => { f.completedTurns = 0; }), 'INVALID_FINAL_FRAME', 'zero turns'],
    [changed(f => { f.diceSequence = 0; }), 'INVALID_FINAL_FRAME', 'zero dice'],
    [changed(f => { f.currentRound = -1; }), 'INVALID_FINAL_FRAME', 'negative round'],
    [changed(f => { f.matchResult.participating = [false, false, false, false]; f.matchResult.winnerSlots = []; f.matchResult.highestNetWorth = 0; }), 'NON_PARTICIPANT_HAS_RESULT', 'nonempty inactive fields'],
    [null, 'INVALID_FINAL_FRAME', 'null frame'],
    [changed(f => { f.matchResult.winnerSlots = [4]; }), 'INVALID_WINNER_SLOTS', 'out-of-range winner'],
  ];
  for (const [input, reason, label] of cases) check(input, false, reason, label);
  // The source must remain strict about ties, without assuming a unique winner.
  check(changed(f => {
    f.matchResult.cash[1] = 3900;
    f.matchResult.netWorth[1] = 5200;
    f.matchResult.winnerSlots = [0, 1];
  }), true, 'CONSISTENT_UNTRUSTED_HOST_RESULT', 'valid tie');
  check(changed(f => {
    f.matchResult.bankrupt[1] = true;
    f.matchResult.winnerSlots = [0];
  }), true, 'CONSISTENT_UNTRUSTED_HOST_RESULT', 'bankruptcy ignored in rank');
  console.log(`Phase 17A compiled validator UNIT PASS: ${total}/${total} cases. No emulator calls.`);
}

async function json(url, method = 'GET', body = null, headers = {}, timeoutMs = 20000) {
  const response = await fetch(url, {
    method, signal: AbortSignal.timeout(timeoutMs),
    headers: {'Content-Type': 'application/json', ...headers},
    ...(body === null ? {} : {body: JSON.stringify(body)}),
  });
  const text = await response.text();
  let data;
  try { data = JSON.parse(text); } catch { data = {raw: text}; }
  return {ok: response.ok, status: response.status, data, text};
}

async function preflight() {
  for (const key of ['GOOGLE_CLOUD_PROJECT', 'GCLOUD_PROJECT']) {
    assert.ok(!process.env[key] || process.env[key] === PROJECT, `Unsafe ${key}`);
  }
  assert.ok(!process.env.FIRESTORE_EMULATOR_HOST ||
    process.env.FIRESTORE_EMULATOR_HOST === FIRESTORE, 'Unsafe Firestore target');
  assert.ok(!process.env.FIREBASE_AUTH_EMULATOR_HOST ||
    process.env.FIREBASE_AUTH_EMULATOR_HOST === `${HOST}:9099`, 'Unsafe Auth target');
  const hub = await json(`${HUB}/emulators`);
  assert.ok(hub.ok, 'Local Firebase Emulator Hub missing');
  for (const [name, port] of [['auth', 9099], ['firestore', 8080], ['functions', 5001]]) {
    const items = Array.isArray(hub.data?.[name]) ? hub.data[name] : [hub.data?.[name]];
    assert.ok(items.some(x => x && ['127.0.0.1', 'localhost'].includes(x.host) &&
      Number(x.port) === port), `Wrong ${name} emulator`);
  }
  process.env.FIRESTORE_EMULATOR_HOST = FIRESTORE;
  process.env.FIREBASE_AUTH_EMULATOR_HOST = `${HOST}:9099`;
  const [{initializeApp}, {getFirestore}] = await Promise.all([
    import('firebase-admin/app'), import('firebase-admin/firestore'),
  ]);
  const app = initializeApp({projectId: PROJECT}, 'atlas-phase17a-test');
  const db = getFirestore(app);
  await db.doc('atlasboard_phase17a_readonly_health/probe').get();
  return {db, app};
}

async function signup(label, nonce) {
  const out = await json(`${AUTH}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=atlasboard-local-only`, 'POST', {
    email: `atlas.phase17a.${label}.${nonce}@example.com`,
    password: `Atlas17A_${label}_${nonce}`,
    returnSecureToken: true,
  });
  assert.ok(out.ok && out.data?.idToken && out.data?.localId, `Auth signup ${label}: ${out.text}`);
  return {uid: out.data.localId, token: out.data.idToken};
}

async function call(name, user, data, timeoutMs = 20000) {
  return json(`${FUNCTIONS}/${name}`, 'POST', {data}, {
    Authorization: `Bearer ${user.token}`,
    'X-Firebase-AppCheck': 'atlasboard-local-emulator-development-only',
  }, timeoutMs);
}

async function emulatorTest() {
  await selfTest();
  const {db, app} = await preflight();
  const nonce = `${Date.now()}_${randomBytes(4).toString('hex')}`;
  const id = `phase17a_match_${nonce}`;
  const raceId = `phase17a3_match_${nonce}`;
  const host = await signup('host', nonce);
  const peer = await signup('peer', nonce);
  const refs = [
    db.doc(`matches/${id}/network/state`),
    db.doc(`matches/${id}/seats/host`),
    db.doc(`matches/${id}/seats/peer`),
    db.doc(`matches/${id}`),
  ];
  // Secondary isolated match: test transaction-level revision contention and
  // forged server-receipt attempts without changing Phase 17A's accepted fixture.
  const raceRefs = [
    db.doc(`matches/${raceId}/network/state`),
    db.doc(`matches/${raceId}/seats/host`),
    db.doc(`matches/${raceId}/seats/peer`),
    db.doc(`matches/${raceId}`),
    db.doc(`match_result_verifications/${raceId}`),
  ];
  let total = 0;
  const check = (label) => console.log(`PHASE17A [${++total}/12] PASS ${label}`);
  let raceCount = 0;
  const raceCheck = (label) => console.log(`PHASE17A3 [${++raceCount}/9] PASS ${label}`);
  try {
    await refs[3].set({matchId: id, hostAccountId: host.uid, status: 'active',
      lobbyId: `phase17a_lobby_${nonce}`, roundLimit: 20, maxPlayers: 2});
    await refs[1].set({seatId: 'host', slotIndex: 0, accountId: host.uid,
      localOwnerAccountId: host.uid, seatType: 'human', controllerKind: 'local_human'});
    await refs[2].set({seatId: 'peer', slotIndex: 1, accountId: peer.uid,
      seatType: 'human', controllerKind: 'human'});
    await refs[0].set({revision: 3, eventSequence: 3, phase: 'awaiting_roll',
      snapshotJson: JSON.stringify({phase: 'awaiting_roll'}), turnSeatId: 'host'});
    check('local-only canonical host/peer and active network fixture');
    const request = (f, user = host) => call('matchHostPublishState', user, {
      matchId: id, expectedRevision: 3, phase: 'match_complete',
      turnSeatId: 'host', eventSequence: 7, snapshotJson: JSON.stringify(f),
    });
    const invalidCases = [
      ['false net worth', changed(f => { f.matchResult.netWorth[0] = 9200; })],
      ['forged winner', changed(f => { f.matchResult.winnerSlots = [1]; })],
      ['oversized asset', changed(f => { f.matchResult.propertyValue[0] = -5; })],
      ['inactive slot data', changed(f => { f.matchResult.cash[3] = 30; })],
      ['duplicate winner', changed(f => { f.matchResult.winnerSlots = [0, 0]; })],
      ['noninteger cash', changed(f => { f.matchResult.cash[1] = 123.45; })],
      ['invalid turn count', changed(f => { f.completedTurns = 0; })],
    ];
    for (const [label, candidate] of invalidCases) {
      const response = await request(candidate);
      assert.equal(response.ok, false, `${label} accepted: ${response.text}`);
      assert.equal(response.data?.error?.details?.errorKey,
        'match.error.invalid_result_consistency', `${label}: ${response.text}`);
      const state = (await refs[0].get()).data();
      assert.equal(state.revision, 3, `${label} mutated network`);
      assert.equal(state.phase, 'awaiting_roll', `${label} mutated phase`);
      check(`${label} denied without state mutation`);
    }
    const outsider = await request(frame(), peer);
    assert.equal(outsider.ok, false, 'Guest completed host match');
    check('non-host cannot publish valid-looking completion');
    const accepted = await request(frame());
    assert.equal(accepted.ok, true, `Consistent final scoreboard rejected: ${accepted.text}`);
    assert.equal((await refs[0].get()).data().revision, 4);
    check('consistent host result may finish gameplay, not claim proof');
    const progression = await call('progressionRecordCompletedMatch', host, {
      matchId: id, completedTurns: 14,
      telemetry: [{slotIndex: 0, diceRolls: 100000, doublesRolled: 100000}],
    });
    assert.equal(progression.ok, true, `Progression pending response failed: ${progression.text}`);
    assert.equal(progression.data?.result?.applied, false, `Forged XP granted: ${progression.text}`);
    assert.equal(progression.data?.result?.verificationStatus, 'pending_authoritative_verification');
    assert.equal((await db.doc(`match_result_verifications/${id}`).get()).exists, false);
    assert.equal((await db.doc(`progression_match_events/${id}`).get()).exists, false);
    check('even consistent host result + inflated telemetry cannot mint XP without verifier');
    const rewrite = await request(frame());
    assert.equal(rewrite.ok, false, 'Host rewrote completed match');
    check('completed result cannot be rewritten by host');
    assert.equal(total, 12);
    console.log(`ATLASBOARD_PHASE_17A_CONSISTENCY_PASS: ${total}/12 (emulator only).`);
    console.log('NOTE: no independent game replay verifier; all permanent XP remains gated.');

    // Phase 17A.3: simulated malicious host sends four final frames at once.
    // Exactly one may commit the revision increment; all others must fail.
    await raceRefs[3].set({matchId: raceId, hostAccountId: host.uid, status: 'active',
      lobbyId: `phase17a3_lobby_${nonce}`, roundLimit: 20, maxPlayers: 2});
    await raceRefs[1].set({seatId: 'host', slotIndex: 0, accountId: host.uid,
      localOwnerAccountId: host.uid, seatType: 'human', controllerKind: 'local_human'});
    await raceRefs[2].set({seatId: 'peer', slotIndex: 1, accountId: peer.uid,
      seatType: 'human', controllerKind: 'human'});
    await raceRefs[0].set({revision: 3, eventSequence: 3, phase: 'awaiting_roll',
      snapshotJson: JSON.stringify({phase: 'awaiting_roll'}), turnSeatId: 'host'});
    raceCheck('isolated local host/peer fixture ready');

    const finalFrame = JSON.stringify(frame());
    const finalRequest = {
      matchId: raceId, expectedRevision: 3, phase: 'match_complete',
      turnSeatId: 'host', eventSequence: 7, snapshotJson: finalFrame,
    };
    const raceStart = Date.now();
    const competing = await Promise.all(Array.from({length: 4}, async (_, index) => {
      const start = Date.now();
      try {
        const response = await call('matchHostPublishState', host, finalRequest, 30000);
        return {...response, requestIndex: index + 1, elapsedMs: Date.now() - start};
      } catch (error) {
        return {ok: false, error: String(error), requestIndex: index + 1,
          elapsedMs: Date.now() - start};
      }
    }));
    const raceWallMs = Date.now() - raceStart;
    console.log('PHASE17A3 4-way completion contention (ms): ' +
      competing.map(x => `#${x.requestIndex}=${x.elapsedMs}`).join(', ') +
      `; wallMs=${raceWallMs}`);
    if (competing.some(x => x.elapsedMs > 5000)) {
      console.warn('PHASE17A3 PERFORMANCE OPEN: slow local emulator calls; ' +
        'Android/staging latency remains unverified.');
    }
    const successes = competing.filter(response => response.ok);
    assert.equal(successes.length, 1,
      `Expected exactly one successful finalization, got ${successes.length}: ` +
      JSON.stringify(competing).slice(0, 2400));
    assert.equal(competing.filter(response => !response.ok).length, 3);
    raceCheck('four simultaneous finalizations commit exactly once');

    const [completedState, completedMatch] = await Promise.all([
      raceRefs[0].get(), raceRefs[3].get(),
    ]);
    assert.equal(completedState.data()?.revision, 4);
    assert.equal(completedState.data()?.eventSequence, 7);
    assert.equal(completedState.data()?.phase, 'match_complete');
    assert.equal(completedState.data()?.snapshotJson, finalFrame);
    assert.equal(completedMatch.data()?.status, 'complete');
    raceCheck('single immutable completed revision and snapshot persisted');

    const rollback = await call('matchHostPublishState', host, {
      ...finalRequest, expectedRevision: 4, eventSequence: 8,
      phase: 'awaiting_roll', snapshotJson: JSON.stringify({phase: 'awaiting_roll'}),
    });
    assert.equal(rollback.ok, false, 'Host rolled completed match back to active');
    assert.equal((await raceRefs[0].get()).data()?.revision, 4);
    raceCheck('completed match cannot be reopened through host publication');

    const duplicate = await call('matchHostPublishState', host, {
      ...finalRequest, expectedRevision: 4, eventSequence: 8,
    });
    assert.equal(duplicate.ok, false, 'Host re-finalized completed match');
    assert.equal((await raceRefs[0].get()).data()?.revision, 4);
    raceCheck('duplicate completed frame denied without revision change');

    // A real Firebase-authenticated client must not mint its own receipt.
    // This deliberately targets ONLY localhost Firestore Emulator.
    const proofPath = `${HOST}:8080/v1/projects/${PROJECT}/databases/(default)/documents/` +
      `match_result_verifications/${raceId}`;
    const clientProof = await json(`http://${proofPath}`, 'PATCH', {
      fields: {verificationStatus: {stringValue: 'verified'}},
    }, {Authorization: `Bearer ${host.token}`});
    assert.equal(clientProof.status, 403,
      `Ordinary Auth client wrote protected receipt: HTTP ${clientProof.status}`);
    assert.equal((await raceRefs[4].get()).exists, false);
    raceCheck('direct client creation of verification receipt denied by Rules');

    // Concurrent retry of progression must remain transport-OK/pending with
    // zero persistent progression writes before an independent verifier exists.
    const finalizeProgress = () => call('progressionRecordCompletedMatch', host, {
      matchId: raceId, completedTurns: 14,
      telemetry: [{slotIndex: 0, diceRolls: 999999, doublesRolled: 999999}],
    });
    const pending = await Promise.all([finalizeProgress(), finalizeProgress()]);
    for (const response of pending) {
      assert.equal(response.ok, true, `Pending finalize failed: ${response.text}`);
      assert.equal(response.data?.result?.applied, false);
      assert.equal(response.data?.result?.verificationStatus,
        'pending_authoritative_verification');
    }
    assert.equal((await db.doc(`progression_match_events/${raceId}`).get()).exists, false);
    assert.equal((await db.doc(`account_progression/${host.uid}`).get()).exists, false);
    assert.equal((await db.doc(`account_progression/${peer.uid}`).get()).exists, false);
    raceCheck('parallel unverified progression requests mint zero XP or wins');

    // Emulator-only Admin SDK malicious fixture tests digest mismatches.
    // This does NOT create a real verifier or an accepted reward receipt.
    await raceRefs[4].set({
      schemaVersion: 1, verificationStatus: 'verified',
      verificationMethod: 'server_authoritative_replay_v1',
      verifiedBy: 'atlasboard-backend-verifier-v1',
      matchId: raceId, hostAccountId: host.uid,
      networkPhase: 'match_complete', networkRevision: 4,
      networkEventSequence: 7, snapshotSha256: '0'.repeat(64),
      verifiedAt: new Date(),
    });
    const forged = await finalizeProgress();
    assert.equal(forged.ok, true, `Mismatched proof rejected at transport: ${forged.text}`);
    assert.equal(forged.data?.result?.applied, false);
    assert.equal(forged.data?.result?.verificationStatus,
      'pending_authoritative_verification');
    assert.equal((await db.doc(`progression_match_events/${raceId}`).get()).exists, false);
    raceCheck('mismatched privileged receipt cannot issue permanent rewards');

    const afterProof = await call('matchHostPublishState', host, {
      ...finalRequest, expectedRevision: 4, eventSequence: 9,
    });
    assert.equal(afterProof.ok, false, 'Host rewrote a proof-protected result');
    assert.equal((await raceRefs[0].get()).data()?.revision, 4);
    raceCheck('protected receipt keeps completed host result frozen');
    assert.equal(raceCount, 9);
    console.log(`ATLASBOARD_PHASE_17A3_FINALIZATION_RACE_PASS: ${raceCount}/9 (emulator only).`);
    console.log('NOTE: finite tests do NOT prove absence of every TOCTOU race. ' +
      'Atomic receipt locking and independent replay verification remain OPEN.');
  } finally {
    for (const ref of [...raceRefs, ...refs]) await ref.delete().catch(() => {});
    for (const user of [host, peer]) {
      await json(`${AUTH}/identitytoolkit.googleapis.com/v1/accounts:delete?key=atlasboard-local-only`,
        'POST', {idToken: user.token}).catch(() => {});
    }
    await app.delete();
    console.log('PHASE17A/17A3 cleanup: temporary local-only fixtures removed.');
  }
}

const mode = process.argv[2] ?? '--emulator';
try {
  if (mode === '--self-test') await selfTest();
  else if (mode === '--emulator') await emulatorTest();
  else throw new Error('Use --self-test or --emulator (inside local Firebase emulators only).');
} catch (error) {
  console.error(`ATLASBOARD_PHASE_17A_CONSISTENCY_FAIL: ${error.stack ?? error}`);
  process.exitCode = 1;
}
