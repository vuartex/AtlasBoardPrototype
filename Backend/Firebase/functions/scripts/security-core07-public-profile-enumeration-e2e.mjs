/**
 * Security Core 07 — public profile direct-GET versus directory enumeration.
 *
 * Explicit product-policy change: authenticated clients may GET one known UID;
 * they may NOT LIST, paginate, or run a query over public_profiles.
 * Server Admin SDK is unaffected. No user search feature currently depends
 * on collection LIST in AtlasBoardAccountService.
 *
 * Safety: fixed atlasboard-usa local Auth, Firestore and Functions emulators.
 * No deploy, rule publication, live credentials or production data access.
 */
import {randomUUID} from 'node:crypto';
import {readFileSync} from 'node:fs';
import {dirname, join, resolve} from 'node:path';
import {fileURLToPath} from 'node:url';

const PROJECT = 'atlasboard-usa';
const AUTH = process.env.FIREBASE_AUTH_EMULATOR_HOST ?? '';
const FS = process.env.FIRESTORE_EMULATOR_HOST ?? '';
const FUNCTIONS = process.env.ATLAS_FUNCTIONS_EMULATOR_HOST ?? '127.0.0.1:5001';
const HUB = '127.0.0.1:4400';
const AUTH_URL = `http://${AUTH}/identitytoolkit.googleapis.com/v1`;
const FS_URL = `http://${FS}/v1/projects/${PROJECT}/databases/(default)/documents`;
const MAX_CHECKS = 23;

function assert(ok, message) { if (!ok) throw new Error(message); }
function isLoopback(value) {
  return /^(?:127\.0\.0\.1|localhost):\d{2,5}$/.test(value);
}
function assertEmulatorOnly() {
  assert(isLoopback(AUTH) && isLoopback(FS) && isLoopback(FUNCTIONS),
    'SEC07 refuses nonlocal Auth/Firestore/Functions targets.');
  assert(!process.env.GCLOUD_PROJECT || process.env.GCLOUD_PROJECT === PROJECT,
    'SEC07 unexpected GCLOUD_PROJECT.');
  assert(!process.env.GOOGLE_CLOUD_PROJECT ||
    process.env.GOOGLE_CLOUD_PROJECT === PROJECT,
  'SEC07 unexpected GOOGLE_CLOUD_PROJECT.');
}
async function verifyHub() {
  const response = await fetch(`http://${HUB}/emulators`,
    {signal: AbortSignal.timeout(4000)});
  assert(response.ok, 'SEC07 local Firebase Emulator Hub unavailable.');
  const services = await response.json();
  for (const service of ['auth', 'firestore', 'functions']) {
    assert(services[service]?.host === '127.0.0.1',
      `SEC07 ${service} emulator is not loopback-only.`);
  }
}
const str = (v) => ({stringValue: v});
const time = (v) => ({timestampValue: v});
function projection(overrides = {}) {
  return {
    displayName: str('AtlasBoard Profile'),
    avatarId: str(''),
    profileFrameId: str(''),
    updatedAt: time(new Date().toISOString()),
    ...overrides,
  };
}
async function signup(label) {
  const response = await fetch(`${AUTH_URL}/accounts:signUp?key=atlas-sec07-emulator`, {
    method: 'POST',
    headers: {'content-type': 'application/json'},
    body: JSON.stringify({
      email: `sec07-${label}-${randomUUID()}@example.test`,
      password: 'Atlas-Core07-Local-Emulator-Only!', returnSecureToken: true,
    }),
    signal: AbortSignal.timeout(12000),
  });
  const body = await response.json().catch(() => ({}));
  assert(response.ok && body.localId && body.idToken,
    `SEC07 local signup failed: ${response.status} ${JSON.stringify(body)}`);
  return {uid: body.localId, token: body.idToken};
}
async function request(method, path, token = '', fields = null) {
  const headers = {'content-type': 'application/json'};
  if (token) headers.authorization = `Bearer ${token}`;
  const response = await fetch(`${FS_URL}/${path}`, {
    method, headers,
    ...(fields === null ? {} : {body: JSON.stringify({fields})}),
    signal: AbortSignal.timeout(12000),
  });
  const body = await response.json().catch(() => ({}));
  return {status: response.status, body};
}
async function query(token, structuredQuery) {
  const response = await fetch(`${FS_URL}:runQuery`, {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      ...(token ? {authorization: `Bearer ${token}`} : {}),
    },
    body: JSON.stringify({structuredQuery}),
    signal: AbortSignal.timeout(12000),
  });
  const body = await response.json().catch(() => ({}));
  return {status: response.status, body};
}
const baseQuery = (limit, extras = {}) => ({
  from: [{collectionId: 'public_profiles'}], limit, ...extras,
});
const denied = (response) =>
  [401, 403].includes(response.status) ||
  response.body?.error?.status === 'PERMISSION_DENIED' ||
  // Firestore runQuery REST may return an array of streamed responses.
  // An explicit permission error is denial, not a successful document result.
  (Array.isArray(response.body) && response.body.length > 0 &&
    response.body.every((entry) =>
      entry?.error?.status === 'PERMISSION_DENIED' && !entry.document));
function noRulesBudgetError(response, label) {
  assert(!/maximum of 1000 expressions|evaluation limit/i.test(
    JSON.stringify(response.body)), `${label}: unexpected Firestore Rules evaluator exhaustion.`);
}

async function cleanup(users) {
  assertEmulatorOnly();
  await verifyHub();
  // Privileged SDK must only be loaded AFTER local-only safety checks.
  const {getApps, initializeApp} = await import('firebase-admin/app');
  const {getFirestore} = await import('firebase-admin/firestore');
  const {getAuth} = await import('firebase-admin/auth');
  process.env.GCLOUD_PROJECT = PROJECT;
  if (getApps().length === 0) initializeApp({projectId: PROJECT});
  const db = getFirestore();
  for (const user of users) {
    await db.collection('public_profiles').doc(user.uid).delete();
    await getAuth().deleteUser(user.uid);
  }
}
function selfTest() {
  assert(PROJECT === 'atlasboard-usa', 'SEC07 project contract changed.');
  assert(isLoopback('127.0.0.1:8080') && isLoopback('localhost:9099'),
    'SEC07 loopback check regression.');
  assert(!isLoopback('0.0.0.0:8080') && !isLoopback('firestore.example:8080'),
    'SEC07 accepted a nonlocal endpoint.');
  assert(projection().displayName.stringValue === 'AtlasBoard Profile',
    'SEC07 minimal public projection changed.');
  const rulePath = resolve(dirname(fileURLToPath(import.meta.url)),
    '..', '..', 'firestore.rules');
  const rules = readFileSync(rulePath, 'utf8');
  const block = rules.match(/match \/public_profiles\/\{uid\} \{([\s\S]*?)\n    \}/)?.[1];
  assert(block && /allow get:\s*if signedIn\(\);/.test(block) &&
    /allow list:\s*if false;/.test(block) &&
    /allow create, update:\s*if owns\(uid\)/.test(block),
  'SEC07 Rules must allow authenticated GET, deny LIST and keep owner-only writes.');
  const legacyTest = readFileSync(join(dirname(fileURLToPath(import.meta.url)),
    'security-core05-profile-privacy-e2e.mjs'), 'utf8');
  assert(legacyTest.includes("'signed-in peer cannot LIST public profiles (SEC07 policy)'") &&
    !legacyTest.includes('signed-in peer can LIST public profiles (existing policy)'),
  'SEC07 must explicitly update the earlier SEC05 policy assertion.');
  console.log('Security Core 07 profile-enumeration offline fixture self-test PASS.');
}

async function main() {
  if (process.argv.includes('--self-test')) {
    assert(process.argv.length === 3, 'SEC07 --self-test only.');
    selfTest(); return;
  }
  assert(process.argv.length === 2, 'SEC07 accepts only --self-test.');
  assertEmulatorOnly();
  await verifyHub();
  let passed = 0;
  function pass(label) {
    passed++;
    console.log(`SEC07 [${passed}/${MAX_CHECKS}] PASS ${label}`);
  }
  async function expect(label, want, operation) {
    const res = await operation();
    noRulesBudgetError(res, label);
    const ok = want === 'deny' ? denied(res) : res.status === want;
    assert(ok, `${label}: expected ${want}, received ${res.status}, ` +
      JSON.stringify(res.body).slice(0, 550));
    pass(label);
    return res;
  }
  const users = [];
  try {
    pass('fixed local project, Auth/Firestore/Functions emulator preflight');
    const owner = await signup('owner'); users.push(owner);
    const peer = await signup('peer'); users.push(peer);
    pass('isolated owner and peer Firebase Auth identities');
    const path = `public_profiles/${owner.uid}`;
    await expect('owner creates minimized public projection', 200,
      () => request('PATCH', path, owner.token, projection()));
    await expect('authenticated peer GET by known UID preserved', 200,
      () => request('GET', path, peer.token));
    await expect('owner GET by known UID preserved', 200,
      () => request('GET', path, owner.token));
    await expect('anonymous GET of a known UID remains denied', 'deny',
      () => request('GET', path));
    await expect('peer cannot enumerate public collection with pageSize=1', 'deny',
      () => request('GET', 'public_profiles?pageSize=1', peer.token));
    await expect('peer cannot enumerate public collection with pageSize=50', 'deny',
      () => request('GET', 'public_profiles?pageSize=50', peer.token));
    await expect('peer cannot enumerate public collection with pageSize=500', 'deny',
      () => request('GET', 'public_profiles?pageSize=500', peer.token));
    await expect('owner cannot enumerate public collection', 'deny',
      () => request('GET', 'public_profiles?pageSize=1', owner.token));
    await expect('anonymous cannot enumerate public collection', 'deny',
      () => request('GET', 'public_profiles?pageSize=1'));
    await expect('peer runQuery limit=1 cannot bypass LIST denial', 'deny',
      () => query(peer.token, baseQuery(1)));
    await expect('owner runQuery limit=1 cannot bypass LIST denial', 'deny',
      () => query(owner.token, baseQuery(1)));
    await expect('peer runQuery filter cannot bypass LIST denial', 'deny',
      () => query(peer.token, baseQuery(1, {
        where: {fieldFilter: {
          field: {fieldPath: 'displayName'}, op: 'EQUAL',
          value: str('AtlasBoard Profile'),
        }},
      })));
    await expect('owner may update public display name', 200,
      () => request('PATCH', path, owner.token,
        projection({displayName: str('Updated Atlas Profile')})));
    const peerRead = await expect('peer targeted GET sees updated public name', 200,
      () => request('GET', path, peer.token));
    assert(peerRead.body.fields?.displayName?.stringValue === 'Updated Atlas Profile',
      'Public profile GET returned stale/malformed owner projection.');
    await expect('peer cannot overwrite another profile', 'deny',
      () => request('PATCH', path, peer.token, projection()));
    await expect('peer cannot delete another profile', 'deny',
      () => request('DELETE', path, peer.token));
    await expect('owner cannot delete canonical profile', 'deny',
      () => request('DELETE', path, owner.token));
    await expect('owner cannot inject Steam ID into public projection', 'deny',
      () => request('PATCH', path, owner.token,
        projection({steamId: str('privacy-violation')})));
    await expect('owner cannot inject huge avatar ID', 'deny',
      () => request('PATCH', path, owner.token,
        projection({avatarId: str('x'.repeat(129))})));
    await expect('owner cannot use overlong display name', 'deny',
      () => request('PATCH', path, owner.token,
        projection({displayName: str('x'.repeat(33))})));
    const finalGet = await expect('rejected writes leave projection unchanged', 200,
      () => request('GET', path, owner.token));
    assert(finalGet.body.fields?.displayName?.stringValue === 'Updated Atlas Profile' &&
      !finalGet.body.fields?.steamId,
    'Malformed or cross-account write changed public profile projection.');
    assert(passed === MAX_CHECKS, `SEC07 expected ${MAX_CHECKS}, got ${passed}.`);
    console.log(`ATLASBOARD_SECURITY_CORE_07_PUBLIC_PROFILE_PASS: ${MAX_CHECKS}/${MAX_CHECKS} checks (emulator only).`);
    console.log('Authenticated targeted GET maintained; client LIST/runQuery denied; Admin SDK unaffected.');
  } finally {
    await cleanup(users);
    console.log('SEC07 cleanup: temporary local public_profiles/Auth fixtures removed.');
  }
}

main().catch((err) => {
  console.error('ATLASBOARD_SECURITY_CORE_07_PUBLIC_PROFILE_FAIL:', err.message);
  process.exitCode = 1;
});
