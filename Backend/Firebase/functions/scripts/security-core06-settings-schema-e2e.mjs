/**
 * Security Core 06 v2 — isolated per-platform documents and shared preferences E2E.
 *
 * Core 05's accepted privacy tests remain in qa.mjs. This suite enforces
 * bounded shape, type and value bounds for shared Settings and each OS;
 * the account registration document shape remains backward-compatible.
 * Negative client writes must be denied and leave stored data unchanged.
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
      email: `sec06-${label}-${randomUUID()}@example.test`,
      password: 'Atlas-Sec06-Local-Emulator-Only!',
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
    const pref = db.collection('preferences').doc(user.uid);
    for (const platform of ['android','windows','ios','macos','linux']) {
      await pref.collection('platforms').doc(platform).delete();
    }
    for (const collection of ['users', 'public_profiles', 'preferences']) {
      await db.collection(collection).doc(user.uid).delete();
    }
    await getAuth().deleteUser(user.uid);
  }
}

const bool = (value) => ({booleanValue: value});
const real = (value) => ({doubleValue: value});
const arr = (values = []) => ({arrayValue: {values}});
const clone = (value) => structuredClone(value);
const validAudio = () => map({
  master: real(0.8), mainMusic: real(0.4), theme: real(0.8),
  dice: real(0.7), effects: real(0.3), muted: bool(false),
});
const validGlobal = () => map({
  botTurnSpeed: num(50), uiHints: bool(true), gameplayConfirmations: bool(true),
});
const validGraphics = () => map({
  resolutionWidth: num(1920), resolutionHeight: num(1080),
  refreshRate: num(144), displayMode: num(1),
  qualityPreset: num(2), vsync: bool(true), fpsLimit: num(60),
  shadowQuality: num(3), antiAliasing: num(2), showFps: bool(false),
});
const validCamera = () => map({
  cameraSensitivity: num(50), cameraZoomSensitivity: num(50),
  cameraPanSensitivity: num(50), reduceCameraMotion: bool(false),
});
function sharedFields() {
  return preferenceFields({
    audio: validAudio(), gameplay: map({global: validGlobal()}),
  });
}
function platformFields() {
  return {schemaVersion: num(1), graphics: validGraphics(),
    gameplay: validCamera(), updatedAt: time(new Date().toISOString())};
}
function replaceNested(source, section, key, value) {
  const result = clone(source);
  result[section].mapValue.fields[key] = value;
  return result;
}
function budgetSafe(response, label) {
  const json = JSON.stringify(response.json);
  assert(!/maximum of 1000 expressions|evaluation limit|maximum memory usage of 1000/i.test(json),
    `${label}: Firestore evaluator exhaustion is NOT an accepted deny: ${json.slice(0, 500)}`);
  return json;
}

async function main() {
  if (process.argv.includes('--self-test')) {
    assert(process.argv.length === 3 && isLoopback('127.0.0.1:8080') &&
      !isLoopback('0.0.0.0:8080'), 'Self-test is localhost-only.');
    const {readFile} = await import('node:fs/promises');
    const rules = await readFile(new URL('../../firestore.rules', import.meta.url), 'utf8');
    const sync = await readFile(new URL('../../../../Assets/Project/Scripts/Online/Firebase/AtlasBoardCloudPreferencesSync.cs', import.meta.url), 'utf8').catch(async () =>
      readFile(new URL('../../../../../Assets/Project/Scripts/Online/Firebase/AtlasBoardCloudPreferencesSync.cs', import.meta.url), 'utf8'));
    assert(rules.includes('match /platforms/{platform}') &&
      rules.includes('validPlatformSettingsDocument(request.resource.data)') &&
      rules.includes('validRootGameplayDelta(') &&
      !rules.includes('changed.difference([\'global\'].toSet())'),
      'Per-platform rules separation missing or old expensive validators remain.');
    assert(sync.includes('PlatformDocument(string uid)') &&
      sync.includes('SetOptions.MergeAll') &&
      sync.includes('batch.Set(PlatformDocument(uid), platform, SetOptions.Overwrite)') &&
      sync.includes('TryGetNestedMap(root.Graphics, platformKey') &&
      sync.includes('TryGetNestedMap(root.Gameplay, platformKey'),
      'Unity current-platform document write or v1 migration fallback missing.');
    assert(Object.keys(platformFields()).length === 4 &&
      Object.keys(sharedFields().gameplay.mapValue.fields).length === 1,
      'Canonical fixture unexpectedly includes non-current platform maps.');
    console.log('Security Core 06 v2 document-isolation offline self-test PASS.');
    return;
  }
  assert(process.argv.length === 2, 'Only --self-test argument supported.');
  preflight();
  await verifyHub();
  const users = [];
  let passed = 0;
  const total = 28;
  async function expect(label, desired, operation) {
    const response = await operation();
    const diagnostic = budgetSafe(response, label);
    const accepted = desired === 'deny' ? failedStatus(response) :
      response.status === desired;
    assert(accepted, `${label}: expected ${desired}, got HTTP ${response.status}: `+
      diagnostic.slice(0, 500));
    passed++;
    console.log(`SEC06 [${passed}/${total}] PASS ${label}`);
    return response;
  }
  try {
    const alice = await signUp('android-owner'); users.push(alice);
    const bob = await signUp('other-account'); users.push(bob);
    const root = `preferences/${alice.uid}`;
    const child = (platform) => `${root}/platforms/${platform}`;
    const write = (path, fields, token = alice.token) =>
      firestore('PATCH', path, token, fields);
    const original = preferenceFields();
    const shared = sharedFields();
    const device = platformFields();

    await expect('canonical registration creates original empty root', 200,
      () => write(root, original));
    await expect('owner reads registration root', 200,
      () => firestore('GET', root, alice.token));
    await expect('owner saves shared audio and global gameplay', 200,
      () => write(root, shared));
    await expect('owner reads shared settings without platform data', 200,
      async () => {
        const response = await firestore('GET', root, alice.token);
        if (response.status === 200)
          assert(Object.keys(response.json.fields.graphics.mapValue.fields ?? {}).length === 0 &&
            Object.keys(response.json.fields.gameplay.mapValue.fields ?? {}).length === 1,
          'Root erroneously stores graphics or platform gameplay.');
        return response;
      });
    await expect('Android-first platform document created', 200,
      () => write(child('android'), device));
    await expect('Android document readable by its owner', 200,
      () => firestore('GET', child('android'), alice.token));
    await expect('Windows independent platform document created', 200,
      () => write(child('windows'), device));
    await expect('iOS independent platform document created', 200,
      () => write(child('ios'), device));
    await expect('macOS independent platform document created', 200,
      () => write(child('macos'), device));
    await expect('Linux independent platform document created', 200,
      () => write(child('linux'), device));
    await expect('all five platform documents persist without overwrites', 200,
      async () => {
        for (const platform of ['android','windows','ios','macos','linux']) {
          const doc = await firestore('GET', child(platform), alice.token);
          assert(doc.status === 200 && doc.json.fields.schemaVersion.integerValue === '1',
            `${platform} lost or changed schema version`);
        }
        return firestore('GET', root, alice.token);
      });
    await expect('Android graphics update does not modify iOS', 200,
      async () => {
        const android = replaceNested(device, 'graphics',
          'fpsLimit', num(90));
        const result = await write(child('android'), android);
        assert(result.status === 200, 'Android platform graphics update rejected');
        const ios = await firestore('GET', child('ios'), alice.token);
        assert(ios.status === 200 &&
          ios.json.fields.graphics.mapValue.fields.fpsLimit.integerValue === '60',
          'Android update corrupted iOS graphics');
        return result;
      });
    await expect('owner changes crossplay in shared root', 200,
      () => write(root, {...clone(shared), crossplayPreference: str('disabled')}));
    await expect('owner changes language in shared root', 200,
      () => write(root, {...clone(shared), language: str('tr')}));

    await expect('unrecognized shared root field denied', 'deny', () =>
      write(root, {...clone(shared), admin: bool(true)}));
    await expect('legacy root graphics injection denied', 'deny', () =>
      write(root, {...clone(shared), graphics: map({android: validGraphics()})}));
    await expect('legacy root platform gameplay injection denied', 'deny', () =>
      write(root, {...clone(shared), gameplay: map({global: validGlobal(),
        android: validCamera()})}));
    await expect('negative shared audio level denied', 'deny', () =>
      write(root, {...clone(shared), audio: map({master: real(-0.1)})}));
    await expect('non-numeric shared audio level denied', 'deny', () =>
      write(root, {...clone(shared), audio: map({master: str('high')})}));
    await expect('invalid global gameplay flag denied', 'deny', () =>
      write(root, {...clone(shared), gameplay: map({global: map({
        uiHints: str('enabled')})})}));
    await expect('unregistered platform ID rejected', 'deny', () =>
      write(child('unknown-console'), device));
    await expect('unrecognized platform field rejected', 'deny', () =>
      write(child('ios'), {...clone(device), debugToken: str('secret')}));
    await expect('out-of-range Android quality rejected', 'deny', () =>
      write(child('android'), replaceNested(device, 'graphics',
        'qualityPreset', num(999))));
    await expect('Android graphics string type rejected', 'deny', () =>
      write(child('android'), replaceNested(device, 'graphics',
        'resolutionWidth', str('1920'))));
    await expect('Android camera sensitivity out of range rejected', 'deny', () =>
      write(child('android'), replaceNested(device, 'gameplay',
        'cameraSensitivity', num(999))));
    await expect('large nested unknown graphics key rejected', 'deny', () =>
      write(child('android'), replaceNested(device, 'graphics',
        'shaderBlob', str('A'.repeat(12000)))));
    await expect('other account cannot edit Android preferences', 'deny', () =>
      write(child('android'), device, bob.token));
    await expect('other account cannot read shared or platform settings', 'deny',
      async () => {
        const get = await firestore('GET', child('android'), bob.token);
        assert(failedStatus(get), 'Other account read was allowed.');
        return firestore('GET', root, bob.token);
      });

    assert(passed === total, `Expected ${total} checks, got ${passed}`);
    // Extra invariants (not counted): no platform deletion or collection
    // scanning; denied writes do not corrupt Android's most recent settings.
    for (const path of [root, child('android')]) {
      const unauthenticated = await firestore('GET', path);
      assert(failedStatus(unauthenticated), 'Anonymous settings read allowed.');
    }
    const ownerList = await firestore('GET', `${root}/platforms?pageSize=5`, alice.token);
    assert(failedStatus(ownerList) &&
      !/maximum of 1000 expressions/i.test(JSON.stringify(ownerList.json)),
      'Owner should not be able to enumerate platform documents.');
    const hostileList = await firestore('GET', `${root}/platforms?pageSize=5`, bob.token);
    assert(failedStatus(hostileList),
      'Other account must not enumerate platform documents.');
    const android = await firestore('GET', child('android'), alice.token);
    assert(android.status === 200 &&
      android.json.fields.graphics.mapValue.fields.fpsLimit.integerValue === '90',
      'Denied writes modified valid Android document.');
    console.log(`ATLASBOARD_SECURITY_CORE_06_SETTINGS_SCHEMA_PASS: ${total}/${total} checks (emulator only).`);
    console.log('Android-first separate platform docs; v1 legacy root preserved until Unity migration.');
  } finally {
    await cleanup(users);
    console.log('SEC06 v2 cleanup: temporary root/platform/Auth fixtures removed.');
  }
}

main().catch((error) => {
  console.error('ATLASBOARD_SECURITY_CORE_06_SETTINGS_SCHEMA_FAIL:', error.message);
  process.exitCode = 1;
});
