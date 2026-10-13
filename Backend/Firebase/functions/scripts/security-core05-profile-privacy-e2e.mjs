/**
 * Security Core 05 — least-privilege account/profile/preferences Firestore E2E.
 *
 * SEC07 explicit product-policy migration: targeted profile GET for signed-in
 * users is retained, but all client collection LIST scans are now denied.
 * Real private documents, rate-limit state and server authority must remain
 * inaccessible to another authenticated user or an anonymous client.
 *
 * All network calls are confined to the fixed atlasboard-usa Firebase emulators.
 * No deployed project, service account, production URL or Firestore rules are
 * modified. Admin SDK is used solely to remove verified local test fixtures.
 */
import {randomUUID} from 'node:crypto';

const PROJECT = 'atlasboard-usa';
const AUTH = process.env.FIREBASE_AUTH_EMULATOR_HOST ?? '';
const FIRESTORE = process.env.FIRESTORE_EMULATOR_HOST ?? '';
const FUNCTIONS = process.env.ATLAS_FUNCTIONS_EMULATOR_HOST ?? '127.0.0.1:5001';
const HUB = '127.0.0.1:4400';
const AUTH_URL = `http://${AUTH}/identitytoolkit.googleapis.com/v1`;
const FS_URL = `http://${FIRESTORE}/v1/projects/${PROJECT}/databases/(default)/documents`;

function assert(ok, message) {
  if (!ok) throw new Error(message);
}
function isLoopback(value) {
  return /^(?:127\.0\.0\.1|localhost):\d{2,5}$/.test(value);
}
function preflight() {
  assert(isLoopback(AUTH) && isLoopback(FIRESTORE) && isLoopback(FUNCTIONS),
    'Refusing tests: only local Auth/Firestore/Functions emulators allowed.');
  assert(!process.env.GCLOUD_PROJECT || process.env.GCLOUD_PROJECT === PROJECT,
    'Unexpected GCLOUD_PROJECT; refusing Admin SDK fixture operations.');
  assert(!process.env.GOOGLE_CLOUD_PROJECT || process.env.GOOGLE_CLOUD_PROJECT === PROJECT,
    'Unexpected GOOGLE_CLOUD_PROJECT; refusing Admin SDK fixture operations.');
}
async function verifyHub() {
  const r = await fetch(`http://${HUB}/emulators`,
    {signal: AbortSignal.timeout(4000)});
  assert(r.ok, 'Firebase local Emulator Hub not available.');
  const services = await r.json();
  for (const service of ['auth', 'firestore', 'functions']) {
    assert(services[service]?.host === '127.0.0.1',
      `${service} emulator must bind to 127.0.0.1, never an external IP.`);
  }
}
const str = (value) => ({stringValue: value});
const num = (value) => ({integerValue: String(value)});
const time = (value) => ({timestampValue: value});
const map = (fields = {}) => ({mapValue: {fields}});

function userFields(uid, createdAt, overrides = {}) {
  return {
    uid: str(uid), schemaVersion: num(1),
    accountStatus: str('active'), membershipTier: str('normal'),
    countryCode: str('US'), preferredLanguage: str('en'),
    createdAt: time(createdAt), ...overrides,
  };
}
function profileFields(overrides = {}) {
  return {
    displayName: str('Atlas Privacy Tester'), avatarId: str(''),
    profileFrameId: str(''), updatedAt: time(new Date().toISOString()),
    ...overrides,
  };
}
function preferenceFields(overrides = {}) {
  return {
    language: str('en'), audio: map(), graphics: map(),
    controls: map(), gameplay: map(),
    crossplayPreference: str('enabled'),
    updatedAt: time(new Date().toISOString()), ...overrides,
  };
}
async function signUp(label) {
  const response = await fetch(`${AUTH_URL}/accounts:signUp?key=atlas-local-sec05`, {
    method: 'POST', headers: {'content-type': 'application/json'},
    body: JSON.stringify({
      email: `sec05-${label}-${randomUUID()}@example.test`,
      password: 'Atlas-Sec05-Local-Emulator-Only!',
      returnSecureToken: true,
    }), signal: AbortSignal.timeout(12000),
  });
  const json = await response.json();
  assert(response.ok && json.localId && json.idToken,
    `Local Auth signup failed: ${response.status} ${JSON.stringify(json)}`);
  return {uid: json.localId, token: json.idToken};
}
async function firestore(method, path, token = '', fields = null) {
  const headers = {'content-type': 'application/json'};
  if (token) headers.authorization = `Bearer ${token}`;
  const response = await fetch(`${FS_URL}/${path}`, {
    method, headers,
    ...(fields === null ? {} : {body: JSON.stringify({fields})}),
    signal: AbortSignal.timeout(12000),
  });
  const json = await response.json().catch(() => ({}));
  return {status: response.status, json};
}
function failedStatus(response) {
  return [401, 403].includes(response.status) ||
    response.json?.error?.status === 'PERMISSION_DENIED';
}
async function cleanup(users) {
  // Never import the privileged SDK before local emulator preflight succeeds.
  preflight();
  await verifyHub();
  const {getApps, initializeApp} = await import('firebase-admin/app');
  const {getFirestore} = await import('firebase-admin/firestore');
  const {getAuth} = await import('firebase-admin/auth');
  process.env.GCLOUD_PROJECT = PROJECT;
  if (getApps().length === 0) initializeApp({projectId: PROJECT});
  const db = getFirestore();
  for (const user of users) {
    for (const collection of ['users', 'public_profiles', 'preferences']) {
      await db.collection(collection).doc(user.uid).delete();
    }
    await getAuth().deleteUser(user.uid);
  }
}
async function main() {
  if (process.argv.includes('--self-test')) {
    assert(isLoopback('127.0.0.1:8080') && isLoopback('localhost:9099'),
      'Expected loopback host rejected.');
    assert(!isLoopback('0.0.0.0:8080') && !isLoopback('my-server:8080'),
      'External host unexpectedly accepted.');
    assert(PROJECT === 'atlasboard-usa', 'Wrong QA project.');
    assert(userFields('u', '2026-01-01T00:00:00Z').membershipTier.stringValue === 'normal',
      'Canonical nonprivileged signup fixture changed.');
    console.log('Security Core 05 privacy offline fixture self-test PASS.');
    return;
  }
  assert(process.argv.length === 2, 'Only --self-test argument supported.');
  preflight();
  await verifyHub();
  const users = [];
  let passed = 0;
  async function expect(label, want, op) {
    const response = await op();
    const accepted = typeof want === 'number' ? response.status === want :
      want === 'deny' && failedStatus(response);
    assert(accepted, `${label}: expected ${want}, got ${response.status}, ` +
      JSON.stringify(response.json).slice(0, 280));
    passed++;
    console.log(`SEC05 [${passed}/20] PASS ${label}`);
    return response;
  }
  try {
    const alice = await signUp('alice'); users.push(alice);
    const bob = await signUp('bob'); users.push(bob);
    const created = new Date().toISOString();
    const user = `users/${alice.uid}`;
    const profile = `public_profiles/${alice.uid}`;
    const prefs = `preferences/${alice.uid}`;

    await expect('owner creates canonical private user', 200,
      () => firestore('PATCH', user, alice.token, userFields(alice.uid, created)));
    await expect('owner can read canonical private user', 200,
      () => firestore('GET', user, alice.token));
    await expect('other account cannot read canonical user', 'deny',
      () => firestore('GET', user, bob.token));
    await expect('authenticated collection listing of private users denied', 'deny',
      () => firestore('GET', 'users?pageSize=1', bob.token));
    await expect('owner cannot elevate membership tier', 'deny',
      () => firestore('PATCH', user, alice.token,
        userFields(alice.uid, created, {membershipTier: str('premium')})));
    await expect('owner cannot change canonical identity', 'deny',
      () => firestore('PATCH', user, alice.token,
        userFields(alice.uid, created, {uid: str(bob.uid)})));
    await expect('owner creates public profile', 200,
      () => firestore('PATCH', profile, alice.token, profileFields()));
    await expect('authenticated peer can GET projected public profile', 200,
      () => firestore('GET', profile, bob.token));
    await expect('anonymous cannot GET public profile', 'deny',
      () => firestore('GET', profile));
    await expect('signed-in peer cannot LIST public profiles (SEC07 policy)',
      'deny', () => firestore('GET', 'public_profiles?pageSize=50', bob.token));
    await expect('peer cannot overwrite another user public profile', 'deny',
      () => firestore('PATCH', profile, bob.token, profileFields()));
    await expect('owner cannot inject extra private field into public profile', 'deny',
      () => firestore('PATCH', profile, alice.token,
        profileFields({steamId: str('should-never-leak')})));
    await expect('owner creates private settings document', 200,
      () => firestore('PATCH', prefs, alice.token, preferenceFields()));
    await expect('owner GET private settings permitted', 200,
      () => firestore('GET', prefs, alice.token));
    await expect('peer GET private settings denied', 'deny',
      () => firestore('GET', prefs, bob.token));
    await expect('anonymous GET private settings denied', 'deny',
      () => firestore('GET', prefs));
    await expect('authenticated LIST of private preferences denied', 'deny',
      () => firestore('GET', 'preferences?pageSize=1', bob.token));
    await expect('cross-account write to preferences denied', 'deny',
      () => firestore('PATCH', prefs, bob.token, preferenceFields()));
    await expect('direct rate-limit state overwrite denied', 'deny',
      () => firestore('PATCH', 'security_callable_rate_limits/spoofed-key',
        alice.token, {requestCount: num(0)}));
    await expect('direct server-owned match result receipt minting denied', 'deny',
      () => firestore('PATCH', 'match_result_verifications/forged',
        alice.token, {verified: str('true')}));
    assert(passed === 20, `Security Core 05 expected 20 checks, got ${passed}`);
    console.log('ATLASBOARD_SECURITY_CORE_05_PROFILE_PRIVACY_PASS: 20/20 checks (emulator only).');
    console.log('POLICY: SEC07 explicitly denies authenticated public_profiles LIST; targeted authenticated GET remains supported.');
    console.log('NOTE: arbitrary nested preferences shape/size is NOT closed by this E2E.');
  } finally {
    await cleanup(users);
    console.log('SEC05 cleanup: temporary emulator profile/preferences/Auth data removed.');
  }
}

main().catch((error) => {
  console.error('ATLASBOARD_SECURITY_CORE_05_PROFILE_PRIVACY_FAIL:', error.message);
  process.exitCode = 1;
});
