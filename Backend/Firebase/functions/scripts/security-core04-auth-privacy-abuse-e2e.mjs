/**
 * Security Core 04: incremental callable discovery abuse regression.
 *
 * Reuses the existing Public Lobby E2E for valid signed/tampered cursors,
 * privacy projections and join flow. Here we check only the previously
 * untested shared read budget, typed cursor rejection and caller identity.
 * Runs ONLY under the local atlasboard-usa Firebase Emulator topology.
 */
import {createHash} from 'node:crypto';

const PROJECT = 'atlasboard-usa';
const REGION = 'europe-west1';
const AUTH = process.env.FIREBASE_AUTH_EMULATOR_HOST ?? '127.0.0.1:9099';
const FIRESTORE = process.env.FIRESTORE_EMULATOR_HOST ?? '';
const FUNCTIONS = process.env.ATLAS_FUNCTIONS_EMULATOR_HOST ?? '127.0.0.1:5001';
const HUB = '127.0.0.1:4400';
const MAX_PER_WINDOW = 60;
const SCOPE = 'lobby_discovery';

function assert(ok, message) {
  if (!ok) throw new Error(message);
}
function loopback(host) {
  return /^(?:127\.0\.0\.1|localhost):\d{2,5}$/.test(host);
}
function preflight() {
  assert(!process.env.GCLOUD_PROJECT || process.env.GCLOUD_PROJECT === PROJECT,
    'Wrong GCLOUD_PROJECT: production fixtures forbidden.');
  assert(!process.env.GOOGLE_CLOUD_PROJECT ||
    process.env.GOOGLE_CLOUD_PROJECT === PROJECT,
  'Wrong GOOGLE_CLOUD_PROJECT: production fixtures forbidden.');
  assert(loopback(AUTH) && loopback(FIRESTORE) && loopback(FUNCTIONS),
    'Auth, Firestore and Functions must all be local loopback emulators.');
  assert(PROJECT === 'atlasboard-usa', 'Unexpected project constant.');
}
function url(name) {
  return `http://${FUNCTIONS}/${PROJECT}/${REGION}/${name}`;
}
const versions = Object.freeze({
  gameVersion: '0.1.0-dev',
  protocolVersion: 1,
  rulesVersion: 1,
  contentVersion: '1',
  regionId: 'eur3',
  limit: 1,
});

async function callable(name, token, data = {}) {
  const headers = {
    'content-type': 'application/json',
    'X-Firebase-AppCheck': 'sec04-local-emulator-only-placeholder',
  };
  if (token) headers.authorization = `Bearer ${token}`;
  const response = await fetch(url(name), {
    method: 'POST', headers, body: JSON.stringify({data}),
    signal: AbortSignal.timeout(12000),
  });
  const json = await response.json().catch(() => ({}));
  return {status: response.status, json};
}
function errorStatus(call) {
  return call.json?.error?.status ?? '';
}
function isExhausted(call) {
  return call.status === 429 || errorStatus(call) === 'RESOURCE_EXHAUSTED';
}
async function createUser(label) {
  const nonce = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  const response = await fetch(
    `http://${AUTH}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=sec04-local`,
    {method: 'POST', headers: {'content-type': 'application/json'},
      body: JSON.stringify({
        email: `sec04-${label}-${nonce}@example.test`,
        password: 'Atlas-Local-Sec04-User-123!', returnSecureToken: true,
      })},
  );
  const user = await response.json();
  assert(response.ok && user.localId && user.idToken,
    'Auth Emulator signup failed: ' + JSON.stringify(user));
  const created = await callable('lobbyDevEnsureAccount', user.idToken,
    {displayName: `Sec04 ${label}`});
  assert(created.status === 200,
    'Local account bootstrap failed: ' + JSON.stringify(created.json));
  return {uid: user.localId, token: user.idToken};
}
async function verifyHub() {
  const response = await fetch(`http://${HUB}/emulators`,
    {signal: AbortSignal.timeout(3000)});
  assert(response.ok, 'Local Firebase Emulator Hub unavailable.');
  const emulators = await response.json();
  for (const service of ['auth', 'firestore', 'functions']) {
    assert(emulators[service]?.host === '127.0.0.1',
      `Expected local ${service} emulator bound to 127.0.0.1.`);
  }
}
/**
 * Uses the local Admin SDK to establish a precise fixed-window boundary.
 * This is a QA fixture only; the real client remains forbidden from
 * modifying security_callable_rate_limits by Firestore Security Rules.
 */
async function seedNearLimit(uid) {
  preflight();
  await verifyHub();
  const {getApps, initializeApp} = await import('firebase-admin/app');
  const {getFirestore, FieldValue} = await import('firebase-admin/firestore');
  process.env.GCLOUD_PROJECT = PROJECT;
  if (getApps().length === 0) initializeApp({projectId: PROJECT});
  const key = createHash('sha256').update(`${SCOPE}:${uid}`, 'utf8')
    .digest('hex');
  const ref = getFirestore().collection('security_callable_rate_limits').doc(key);
  const now = Date.now();
  await ref.set({
    uid, scope: SCOPE, requestCount: MAX_PER_WINDOW - 1,
    windowStartedAtEpochMs: now,
    blockedUntilEpochMs: 0,
    expiresAtEpochMs: now + 120000,
    updatedAt: FieldValue.serverTimestamp(),
    schemaVersion: 1,
  });
  const seeded = (await ref.get()).data();
  assert(seeded?.uid === uid &&
    seeded?.scope === SCOPE &&
    seeded?.requestCount === MAX_PER_WINDOW - 1 &&
    seeded?.windowStartedAtEpochMs === now,
  'Near-limit seed did not persist under the required local UID/scope.');
  return ref;
}

async function cleanup(users) {
  // Dynamically import credentials-free Admin SDK only after loopback preflight.
  const {getApps, initializeApp} = await import('firebase-admin/app');
  const {getAuth} = await import('firebase-admin/auth');
  const {getFirestore} = await import('firebase-admin/firestore');
  process.env.GCLOUD_PROJECT = PROJECT;
  if (getApps().length === 0) initializeApp({projectId: PROJECT});
  const db = getFirestore();
  for (const user of users) {
    const id = createHash('sha256').update(`${SCOPE}:${user.uid}`, 'utf8')
      .digest('hex');
    await Promise.all([
      db.collection('security_callable_rate_limits').doc(id).delete(),
      db.collection('public_profiles').doc(user.uid).delete(),
      db.collection('preferences').doc(user.uid).delete(),
      db.collection('users').doc(user.uid).delete(),
    ]);
    await getAuth().deleteUser(user.uid);
  }
}

async function main() {
  if (process.argv.includes('--self-test')) {
    assert(loopback('127.0.0.1:8080') && loopback('localhost:5001'),
      'Loopback predicate rejected safe hosts.');
    assert(!loopback('evil.example:8080') && !loopback('0.0.0.0:8080'),
      'Loopback predicate accepted unsafe hosts.');
    assert(MAX_PER_WINDOW === 60 && SCOPE === 'lobby_discovery',
      'Unexpected security budget contract.');
    const expectedKey = createHash('sha256')
      .update(`${SCOPE}:offline-uid`, 'utf8').digest('hex');
    assert(expectedKey.length === 64 && /^[0-9a-f]{64}$/.test(expectedKey),
      'Rate-limit document key contract changed.');
    assert(MAX_PER_WINDOW - 1 === 59, 'Near-limit fixture boundary changed.');
    console.log('Security Core 04 offline fixture self-test PASS.');
    return;
  }
  assert(process.argv.length === 2, 'Do not supply arguments to live E2E.');
  preflight();
  await verifyHub();
  const users = [];
  let pass = 0;
  try {
    const alice = await createUser('alice'); users.push(alice);
    const bob = await createUser('bob'); users.push(bob);

    for (const endpoint of ['lobbyListPublicRooms', 'lobbyListPublicRoomsPage']) {
      const anonymous = await callable(endpoint, '', versions);
      assert(anonymous.status === 401 || errorStatus(anonymous) === 'UNAUTHENTICATED',
        `Anonymous ${endpoint} unexpectedly authenticated.`);
      pass++;
    }

    const wrongCursor = await callable('lobbyListPublicRoomsPage', alice.token,
      {...versions, cursor: {malicious: true}});
    assert(wrongCursor.status === 400 &&
      wrongCursor.json?.error?.details?.fieldName === 'cursor',
    'Object-valued cursor must be invalid, not coerced to first page.');
    pass++;

    const forgedCursor = await callable('lobbyListPublicRoomsPage', alice.token,
      {...versions, cursor: 'unsigned.payload'});
    assert(forgedCursor.status === 400,
      'Unsigned cursor must be rejected.');
    pass++;

    const normalPage = await callable('lobbyListPublicRoomsPage', alice.token, versions);
    assert(normalPage.status === 200 && Array.isArray(normalPage.json?.result?.rooms),
      'Authenticated page contract failed.');
    pass++;
    const normalLegacy = await callable('lobbyListPublicRooms', alice.token, versions);
    assert(normalLegacy.status === 200 && Array.isArray(normalLegacy.json?.result?.rooms),
      'Legacy browser contract failed.');
    pass++;

    // Fixed-window limiter QA must be deterministic. A sequence of 56 HTTP
    // batches can exceed the server's 60-second window on a slow Windows
    // emulator and silently reset the count, producing false negatives.
    // Seed ONLY this disposable local Auth UID's server-owned counter at
    // 59/60 through Admin SDK after strict loopback/emulator preflight.
    // Never seed this fixture into a deployed project or client code.
    const rateRef = await seedNearLimit(alice.uid);
    const lastAllowed = await callable('lobbyListPublicRoomsPage',
      alice.token, {...versions, uid: bob.uid});
    assert(lastAllowed.status === 200 &&
      Array.isArray(lastAllowed.json?.result?.rooms),
    'Boundary request 60/60 failed, or caller data UID changed auth identity.');
    const boundaryDoc = (await rateRef.get()).data();
    assert(boundaryDoc?.requestCount === MAX_PER_WINDOW,
      'Boundary accounting did not reach exactly 60/60.');
    pass++;

    // Three simultaneous calls across BOTH endpoints must all reject.
    // Spoofing Bob's UID in request.data must not alter Alice's Auth UID,
    // and the shared transactional limiter must survive concurrency.
    const overLimit = await Promise.all([
      callable('lobbyListPublicRooms', alice.token, {...versions, uid: bob.uid}),
      callable('lobbyListPublicRoomsPage', alice.token, {...versions, uid: bob.uid}),
      callable('lobbyListPublicRooms', alice.token, {...versions, uid: bob.uid}),
    ]);
    assert(overLimit.every(isExhausted),
      'Cross-endpoint shared limiter failed or concurrent bypass succeeded: ' +
      JSON.stringify(overLimit.map((v) => ({status: v.status, error: errorStatus(v)}))));
    const blockedDoc = (await rateRef.get()).data();
    assert(blockedDoc?.requestCount > MAX_PER_WINDOW &&
      blockedDoc?.blockedUntilEpochMs > Date.now(),
    'Blocked request did not persist the server-owned cooldown.');
    pass++;

    const independent = await callable('lobbyListPublicRoomsPage', bob.token, versions);
    assert(independent.status === 200,
      'One caller exhausting the discovery budget affected a different UID.');
    pass++;

    console.log(`ATLASBOARD_SECURITY_CORE_04_DISCOVERY_PASS: ${pass}/9 checks (emulator only).`);
  } finally {
    if (users.length) await cleanup(users);
  }
}

main().catch((error) => {
  console.error('ATLASBOARD_SECURITY_CORE_04_DISCOVERY_FAIL:', error.message);
  process.exitCode = 1;
});
