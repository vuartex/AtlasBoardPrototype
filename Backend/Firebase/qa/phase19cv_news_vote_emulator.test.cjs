'use strict';
// cx: Official Firebase Auth + RTDB emulator test. Does not contact live Firebase.
// One private UID receipt -> one immutable anonymous public option receipt.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const dbHost = process.env.FIREBASE_DATABASE_EMULATOR_HOST;
const authHost = process.env.FIREBASE_AUTH_EMULATOR_HOST;
if (!dbHost || !authHost) {
  console.error('ERROR: Auth + RTDB emulators required. No live calls allowed.');
  process.exit(2);
}
const project = process.env.GCLOUD_PROJECT || process.env.FIREBASE_PROJECT_ID || 'demo-atlasboard';
if (!project.startsWith('demo-')) {
  console.error('ERROR: this test requires a demo-* project, never production.');
  process.exit(2);
}
const instance = project + '-default-rtdb';
const pollId = 'community_feature_priority_2026w46';
const opt = ['maps', 'pawns', 'avatars', 'dice', 'boards', 'profile_frames', 'emotes'];
let passed = 0;
function ok(label, fn) { fn(); passed++; console.log('PASS:', label); }
async function db(method, loc, idToken, body, headers = {}) {
  let url = `http://${dbHost}/${loc}.json?ns=${encodeURIComponent(instance)}`;
  // The emulator's privileged seed path uses a bearer *admin* token.
  // `?auth=owner` is interpreted as a user ID token by REST and may be denied.
  // NEVER send this credential to a non-demo project or production endpoint.
  const isEmulatorAdmin = idToken === 'owner';
  if (idToken && !isEmulatorAdmin)
    url += `&auth=${encodeURIComponent(idToken)}`;
  const result = await fetch(url, {
    method,
    headers: {
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
      ...(isEmulatorAdmin ? { Authorization: 'Bearer owner' } : {}),
      ...headers
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) })
  });
  const raw = await result.text();
  let data;
  try { data = JSON.parse(raw); } catch { data = raw; }
  return { status: result.status, data };
}
// Include the emulator's response details on any failed positive assertion.
// HTTP 400 means malformed JSON and is not a RTDB security-rules DENY.
async function expectOk(request, label) {
  const response = await request;
  assert.equal(response.status, 200,
    `${label}: HTTP ${response.status}, response=${JSON.stringify(response.data)}`);
  return response;
}
async function signup(anonymous = false) {
  const name = crypto.randomBytes(8).toString('hex');
  const body = anonymous ? { returnSecureToken: true } :
    { email: `atlas-cx-${name}@example.invalid`, password: 'test-only-password-123', returnSecureToken: true };
  const result = await fetch(`http://${authHost}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=emulator-key`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body)
  });
  if (!result.ok) throw Error('Auth emulator signup rejected: ' + result.status);
  const data = await result.json();
  return { uid: data.localId, token: data.idToken, alias: crypto.randomBytes(16).toString('hex') };
}
function denied(r) { assert.ok([401, 403, 412].includes(r.status),
  `Expected access denied; received HTTP ${r.status}, response=${JSON.stringify(r.data)}`); }
const receipt = (person, option) => ({ uid: person.uid, pollId, optionId: option,
  publicAlias: person.alias, createdAt: { '.sv': 'timestamp' } });
const vote = (person, dest, option) => db('PUT', `communityVotes/${pollId}/${dest.uid}`,
  person.token, receipt(dest, option), { 'If-Match': 'null_etag' });
const publicRecord = (actor, alias, option, method = 'PUT', content = { optionId: option }) =>
  db(method, `communityPublicVotes/${pollId}/${alias}`, actor.token, content,
    { 'If-Match': 'null_etag' });
const countVisible = (data) => {
  const counts = Object.fromEntries(opt.map(id => [id, 0]));
  for (const [alias, entry] of Object.entries(data || {})) {
    if (/^[a-f0-9]{32}$/.test(alias) && entry && Object.hasOwn(counts, entry.optionId))
      counts[entry.optionId]++;
  }
  return { counts, total: Object.values(counts).reduce((a, b) => a+b, 0) };
};
(async () => {
  const alice = await signup();
  const bob = await signup();
  const eve = await signup();
  const guest = await signup(true);
  // Fail fast if the emulator administration bootstrap itself is not trusted.
  // The actual voter assertions below always use their individual Auth ID tokens.
  await expectOk(db('GET', `users/${alice.uid}/general/accountType`, 'owner'),
    'RTDB demo emulator owner-bearer bootstrap probe');
  console.log('PASS: demo-only owner authorization for test fixtures');
  for (const person of [alice, bob, eve])
    await expectOk(db('PUT', `users/${person.uid}/general/accountType`, 'owner', 'registered'),
      'Set registered account type in demo RTDB');
  const now = Date.now();
  const manifest = JSON.parse(fs.readFileSync(path.join(__dirname, '..',
    'communityPolls_initial.json'), 'utf8'));
  const poll = manifest[pollId];
  poll.startsAtMs = now - 60000;
  poll.endsAtMs = now + 600000;
  await expectOk(db('PUT', `communityPolls/${pollId}`, 'owner', poll),
    'Publish demo-only community poll');
  ok('Only one published poll with exactly seven choices', () =>
    assert.deepEqual(Object.keys(poll.options), opt));
  denied(await vote(alice, bob, 'maps'));
  ok('Foreign UID cannot write another player\'s receipt', () => {});
  denied(await vote(alice, alice, 'hidden_feature'));
  ok('Unknown option is rejected', () => {});
  denied(await vote(guest, guest, 'dice'));
  ok('Anonymous account cannot vote', () => {});
  const extraVote = { ...receipt(alice, 'maps'), bonusGold: 1000 };
  denied(await db('PUT', `communityVotes/${pollId}/${alice.uid}`, alice.token,
    extraVote, { 'If-Match': 'null_etag' }));
  ok('Private receipt may not contain extra fields', () => {});
  await expectOk(vote(alice, alice, 'maps'), 'Alice private vote');
  denied(await vote(alice, alice, 'dice'));
  denied(await db('DELETE', `communityVotes/${pollId}/${alice.uid}`, alice.token));
  ok('Alice vote immutable including deletion and option switching', () => {});
  denied(await db('GET', `communityVotes/${pollId}/${alice.uid}`, bob.token));
  denied(await db('GET', `communityVotes/${pollId}`, bob.token));
  ok('No cross-UID or bulk private receipt read', () => {});
  denied(await publicRecord(bob, alice.alias, 'maps'));
  ok('Another player cannot publish Alice\'s anonymous alias', () => {});
  denied(await publicRecord(alice, alice.alias, 'pawns'));
  denied(await publicRecord(alice, crypto.randomBytes(16).toString('hex'), 'maps'));
  ok('Publication must match Alice\'s exact private option AND alias', () => {});
  denied(await publicRecord(alice, alice.alias, 'maps', 'PUT', { optionId: 'maps', uid: alice.uid }));
  ok('Public vote receipt never accepts a UID or extra fields', () => {});
  await expectOk(publicRecord(alice, alice.alias, 'maps'), 'Alice public alias');
  denied(await publicRecord(alice, alice.alias, 'maps'));
  denied(await db('DELETE', `communityPublicVotes/${pollId}/${alice.alias}`, alice.token));
  ok('Public alias receipt is immutable and cannot be republished or removed', () => {});
  const savedAlice = await db('GET', `communityPublicVotes/${pollId}`, bob.token);
  ok('Public result is anonymous and contains exactly one verified option', () => {
    assert.equal(savedAlice.status, 200);
    const counted = countVisible(savedAlice.data);
    assert.equal(counted.total, 1);
    assert.equal(counted.counts.maps, 1);
    assert.ok(!JSON.stringify(savedAlice.data).includes(alice.uid));
  });
  denied(await publicRecord(eve, eve.alias, 'boards'));
  ok('A public result without a verified private vote cannot be forged', () => {});
  denied(await db('PUT', `communityPublicVotes/${pollId}`, bob.token,
    { hacked: { optionId: 'dice' } }));
  ok('Bulk public results cannot be replaced by players', () => {});
  await expectOk(vote(bob, bob, 'emotes'), 'Bob private vote');
  await expectOk(publicRecord(bob, bob.alias, 'emotes'), 'Bob public alias');
  const afterTwo = await db('GET', `communityPublicVotes/${pollId}`, alice.token);
  ok('Two distinct UIDs count exactly 2 with per-option totals', () => {
    const counted = countVisible(afterTwo.data);
    assert.equal(counted.total, 2);
    assert.equal(counted.counts.maps, 1);
    assert.equal(counted.counts.emotes, 1);
  });
  denied(await db('PATCH', `communityPublicVotes/${pollId}`, alice.token,
    { [bob.alias]: null, [alice.alias]: { optionId: 'maps' } }));
  ok('An attacker cannot delete a different anonymous vote to change results', () => {});
  poll.endsAtMs = now - 1;
  await expectOk(db('PUT', `communityPolls/${pollId}`, 'owner', poll),
    'Expire the demo-only poll');
  denied(await vote(eve, eve, 'boards'));
  ok('New votes after server-authoritative expiry are denied', () => {});
  console.log(`FIREBASE_AUTH_RTDB_EMULATOR=${passed}/${passed} PASS`);
})().catch(error => { console.error('EMULATOR TEST FAIL:', error.stack || error.message); process.exit(1); });
