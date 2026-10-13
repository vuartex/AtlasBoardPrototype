/**
 * AtlasBoard stable backend/Unity-structure QA entrypoint.
 *
 * Usage (from Backend/Firebase/functions):
 *   node scripts/qa.mjs --check        # offline audit/build and Unity metadata
 *   node scripts/qa.mjs --full         # offline build, integrated security and App Check E2E
 *   node scripts/qa.mjs --ci           # same as --full; suitable for CI
 *   node scripts/qa.mjs --self-test    # no dependencies, no emulators
 *   node scripts/qa.mjs --restore-env  # crash recovery of private env backup
 *
 * Does not deploy, install dependencies, change Git, run a Unity build, or
 * authorize production. Reuses already accepted Phase 15 security tests.
 */
import {spawnSync} from 'node:child_process';
import {
  constants, copyFileSync, existsSync,
  mkdtempSync, readFileSync, rmSync, unlinkSync, writeFileSync,
} from 'node:fs';
import {tmpdir} from 'node:os';
import {dirname, join, resolve} from 'node:path';
import {fileURLToPath} from 'node:url';

const functionsDir = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const firebaseDir = resolve(functionsDir, '..');
const repoDir = resolve(firebaseDir, '..', '..');
const envFile = join(functionsDir, '.env.local');
const backupFile = join(functionsDir, '.qa-env-backup.local');
const createdMarker = join(functionsDir, '.qa-env-created.local');
const secretFile = join(functionsDir, '.secret.local');
const secretMarker = join(functionsDir, '.qa-secret-created.local');
const windows = process.platform === 'win32';
const npm = windows ? 'npm.cmd' : 'npm';
const npx = windows ? 'npx.cmd' : 'npx';
const PROJECT = 'atlasboard-usa';
const scriptBase = 'functions/scripts/';

const integrationScripts = [
  'chat-moderation-e2e.mjs',
  'phase15j-runtime-e2e.mjs',
  'platform-steam-link-e2e.mjs',
  'platform-steam-returning-signin-e2e.mjs',
  'callable-authorization-abuse-e2e.mjs',
  'economy-commerce-security-e2e.mjs',
  // Security Core 03: include the accepted entitlement and promo E2E baselines.
  'inventory-entitlements-e2e.mjs',
  'promo-event-tickets-e2e.mjs',
  'security-rules-e2e.mjs',
  'public-lobby-discovery-e2e.mjs',
  // Run AFTER offline TypeScript build in the SAME full QA invocation.
  // Verifies normal progression and malicious host-result rejection together.
  'progression-phase10-e2e.mjs --security-suite',
  // Security Core 03: disabled track, global promo races, daily and tier replay.
  'security-core03-rewards-e2e.mjs',
];
const enforcementScripts = ['app-check-enforcement-negative-e2e.mjs'];

function assert(condition, message) {
  if (!condition) throw new Error(message);
}
function run(label, executable, args, cwd = functionsDir) {
  console.log(`\nAtlasBoard QA | ${label}`);
  const out = spawnSync(executable, args, {
    cwd,
    stdio: 'inherit',
    shell: windows,
    env: {
      ...process.env,
      FUNCTIONS_DISCOVERY_TIMEOUT: '90',
    },
  });
  if (out.error) throw out.error;
  assert(out.status === 0, `${label} failed (exit ${out.status ?? 'unknown'}).`);
}
function text(path) {
  assert(existsSync(path), `Missing required project file: ${path}`);
  return readFileSync(path, 'utf8');
}
function enabledScenePaths(buildSettings) {
  return [...buildSettings.matchAll(
    /^\s*- enabled: 1\s*\r?\n\s*path: (.+)\s*$/gm,
  )].map((match) => match[1].trim());
}
function unityPreflight(root = repoDir) {
  const settings = text(join(root, 'ProjectSettings', 'EditorBuildSettings.asset'));
  const version = text(join(root, 'ProjectSettings', 'ProjectVersion.txt'));
  const project = text(join(root, 'ProjectSettings', 'ProjectSettings.asset'));
  const manifest = JSON.parse(text(join(root, 'Packages', 'manifest.json')));
  const scenes = enabledScenePaths(settings);
  const gameScene = 'Assets/Board_Prototype.unity';
  assert(scenes.includes(gameScene), 'Board_Prototype must be an enabled Build Settings scene.');
  assert(existsSync(join(root, gameScene)), 'Board_Prototype scene asset is missing.');
  assert(/^m_EditorVersion: 6000\.5\.6f1\s*$/m.test(version),
    'Unexpected Unity version. Review upgrade and update QA baselines intentionally.');
  assert(/^\s*productName: Atlas Board\s*$/m.test(project),
    'Unity product name unexpectedly changed.');
  const versionMatch = /^\s*bundleVersion: ([^\r\n]+)\s*$/m.exec(project);
  assert(versionMatch && /^\d+\.\d+\.\d+(?:[-+][\w.-]+)?$/.test(versionMatch[1].trim()),
    'A semantic application bundleVersion is required.');
  for (const dependency of [
    'com.unity.inputsystem',
    'com.unity.test-framework',
    'com.unity.render-pipelines.universal',
    'com.rlabrecque.steamworks.net',
  ]) {
    assert(typeof manifest.dependencies?.[dependency] === 'string',
      `Unity package ${dependency} is missing.`);
  }
  console.log(`PASS Unity ${version.match(/m_EditorVersion: ([^\s]+)/)[1]}; product, version, scene, 4 dependencies.`);
  if (scenes[0] !== gameScene) {
    console.warn('NOTE: EditorBuildSettings starts with another scene. ' +
      'The existing Two-Client Guest Test uses the CURRENT active Unity scene as startup. ' +
      'Open Board_Prototype before building the guest; no Build Settings were changed.');
  }
  console.log('NOTE: Unity compilation, rendering, fonts, input and physical multiplayer are NOT proven by this static check.');
}
function requireLocalSuiteFiles() {
  const all = [...integrationScripts, ...enforcementScripts, 'phase15k-validation.mjs'];
  for (const scriptCommand of all) {
    const file = scriptCommand.split(' ')[0];
    assert(existsSync(join(functionsDir, 'scripts', file)),
      `Missing accepted security regression: scripts/${file}. ` +
      'Apply the accepted 15I/15J/15K local packages first.');
  }
  const fcfg = JSON.parse(text(join(firebaseDir, 'firebase.json')));
  for (const name of ['auth', 'functions', 'firestore']) {
    assert(fcfg.emulators?.[name]?.host === '127.0.0.1',
      `${name} emulator must be bound to loopback before QA.`);
  }
}
function setSetting(content, key, value) {
  const regex = new RegExp(`^${key}=[^\\r\\n]*$`, 'gm');
  return regex.test(content) ?
    content.replace(new RegExp(`^${key}=[^\\r\\n]*$`, 'gm'), `${key}=${value}`) :
    `${content.trimEnd()}\n${key}=${value}\n`;
}
function recoverEnv() {
  if (existsSync(backupFile)) {
    copyFileSync(backupFile, envFile);
    unlinkSync(backupFile);
    console.log('Restored original .env.local from private QA backup.');
  } else if (existsSync(createdMarker)) {
    if (existsSync(envFile)) unlinkSync(envFile);
    unlinkSync(createdMarker);
    console.log('Removed temporary QA .env.local.');
  }
  if (existsSync(secretMarker)) {
    if (existsSync(secretFile)) unlinkSync(secretFile);
    unlinkSync(secretMarker);
    console.log('Removed temporary emulator-only placeholder secret.');
  }
}
function beginEmulatorConfig() {
  assert(!existsSync(backupFile) && !existsSync(createdMarker) &&
    !existsSync(secretMarker),
  'Existing QA private backup marker detected. Run node scripts/qa.mjs --restore-env first.');
  if (existsSync(envFile)) {
    copyFileSync(envFile, backupFile, constants.COPYFILE_EXCL);
  } else {
    writeFileSync(createdMarker, 'created-by-atlasboard-qa\n');
    writeFileSync(envFile, '', 'utf8');
  }
  if (!existsSync(secretFile)) {
    writeFileSync(secretMarker, 'created-by-atlasboard-qa\n');
    writeFileSync(secretFile,
      'STEAM_PUBLISHER_WEB_API_KEY=atlas-emulator-placeholder-never-production\n', 'utf8');
  }
}
function setEmulatorEnforcement(enabled) {
  let source = text(envFile);
  source = setSetting(source, 'STEAM_APP_ID', '480');
  source = setSetting(source, 'ATLAS_ENFORCE_APP_CHECK', enabled ? 'true' : 'false');
  writeFileSync(envFile, source, 'utf8');
  process.env.ATLAS_ENFORCE_APP_CHECK = enabled ? 'true' : 'false';
}
function emulator(label, files) {
  const command = files.map((filename) =>
    `node ${scriptBase}${filename}`).join(' && ');
  // Windows firebase emulators:exec requires the command as one quoted arg.
  const singleCommandArgument = windows ? `"${command}"` : command;
  run(label, npx, [
    'firebase', 'emulators:exec', '--project', PROJECT,
    '--only', 'auth,firestore,functions', singleCommandArgument,
  ], firebaseDir);
}
function offlineChecks() {
  console.log('AtlasBoard QA | offline source and Unity metadata preflight');
  requireLocalSuiteFiles();
  unityPreflight();
  run('Accepted security static regression / TypeScript build / ESLint',
    'node', ['scripts/phase15k-validation.mjs', '--static-only']);
  // Regression fixtures are tested before paying the cost of starting emulators.
  // These modes never load Firebase Admin SDK or open network connections.
  for (const script of [
    'inventory-entitlements-e2e.mjs',
    'promo-event-tickets-e2e.mjs',
    'security-core03-rewards-e2e.mjs',
  ]) {
    run(`Offline E2E fixture self-test: ${script}`,
      'node', [`scripts/${script}`, '--self-test']);
  }
  console.log('\nAtlasBoard QA OFFLINE PASS (no Unity Editor compile performed).');
}
function assertCompiledSecurityGate() {
  const entry = text(join(functionsDir, 'lib', 'entry.js'));
  const gate = text(join(functionsDir, 'lib', 'security', 'match-integrity-gates.js'));
  assert(entry.includes('security/match-integrity-gates') &&
    entry.includes('matchHostPublishState') &&
    entry.includes('progressionRecordCompletedMatch'),
  'Compiled Firebase entry is missing the protected security-gate exports. ' +
    'Check the current src/entry.ts and rerun TypeScript compilation.');
  const seasonal = text(join(functionsDir, 'lib', 'security',
    'seasonal-track-integrity.js'));
  assert(entry.includes('security/seasonal-track-integrity') &&
    entry.includes('metaSeasonalClaimTrackTier') &&
    seasonal.includes('SEASONAL_TRACK_INACTIVE') &&
    seasonal.includes('currentTier.active'),
  'Compiled seasonal tier security gate is missing or stale.');
  assert(gate.includes('pending_authoritative_verification') &&
    gate.includes('match_result_verifications'),
  'Compiled match-integrity security gate is stale or incomplete.');
  console.log('PASS compiled security gate and progression fail-closed contract.');
}
function runFull() {
  offlineChecks();
  assertCompiledSecurityGate();
  beginEmulatorConfig();
  try {
    setEmulatorEnforcement(false);
    emulator('Fresh emulator integration / authorization / economy / moderation', integrationScripts);
    setEmulatorEnforcement(true);
    emulator('Fresh emulator App Check enforcement and exceptions', enforcementScripts);
  } finally {
    recoverEnv();
  }
  console.log('\nAtlasBoard QA FULL LOCAL PASS (emulators only). ' +
    'Deployed Steam/App Check, live TTL, Unity build and physical multiplayer remain unverified.');
}
function selfTest() {
  const root = mkdtempSync(join(tmpdir(), 'atlas-qa-self-'));
  try {
    const file = join(root, 'marker.txt');
    writeFileSync(file, 'STEAM_APP_ID=10\nATLAS_ENFORCE_APP_CHECK=true\n');
    const start = readFileSync(file, 'utf8');
    const changed = setSetting(start, 'ATLAS_ENFORCE_APP_CHECK', 'false');
    assert(changed.includes('STEAM_APP_ID=10'), 'Setting replacement changed unrelated content.');
    assert(changed.includes('ATLAS_ENFORCE_APP_CHECK=false'), 'Setting replacement failed.');
    assert(!changed.includes('ATLAS_ENFORCE_APP_CHECK=true'), 'Old setting remained.');
    assert(setSetting(changed, 'STEAM_APP_ID', '480').includes('STEAM_APP_ID=480'),
      'Emulator AppID injection failed.');
    assert(enabledScenePaths('m_Scenes:\n  - enabled: 1\n    path: Assets/Board_Prototype.unity\n')
      .includes('Assets/Board_Prototype.unity'), 'Scene parsing failed.');
    assert(enabledScenePaths('m_Scenes:\n  - enabled: 0\n    path: Assets/Board_Prototype.unity\n')
      .length === 0, 'Disabled scenes should not pass.');
    assert(integrationScripts.includes('progression-phase10-e2e.mjs --security-suite'),
      'Host result security audit must be in full QA.');
    assert(integrationScripts.includes('security-core03-rewards-e2e.mjs') &&
      integrationScripts.at(-1) === 'security-core03-rewards-e2e.mjs',
    'Security Core 03 must run after integrated regressions.');
    assert(integrationScripts.indexOf('inventory-entitlements-e2e.mjs') >= 0 &&
      integrationScripts.indexOf('promo-event-tickets-e2e.mjs') >= 0 &&
      integrationScripts.indexOf('inventory-entitlements-e2e.mjs') <
        integrationScripts.indexOf('promo-event-tickets-e2e.mjs') &&
      integrationScripts.indexOf('promo-event-tickets-e2e.mjs') <
        integrationScripts.indexOf('security-core03-rewards-e2e.mjs'),
    'Admin-backed inventory and promo E2E must precede Security Core 03.');
    console.log('AtlasBoard QA self-test PASS: settings, Unity scenes and integrated host security suite.');
  } finally {
    rmSync(root, {recursive: true, force: true});
  }
}
const args = process.argv.slice(2);
try {
  assert(args.length <= 1, 'Choose one mode: --check, --full, --ci, --self-test or --restore-env.');
  const mode = args[0] ?? '--check';
  if (mode === '--help') {
    console.log('AtlasBoard QA: --check (offline), --full/--ci (emulators), ' +
      '--self-test, --restore-env. Run from Backend/Firebase/functions.');
  } else if (mode === '--restore-env') {
    recoverEnv();
  } else if (mode === '--self-test') {
    selfTest();
  } else if (mode === '--check') {
    offlineChecks();
  } else if (mode === '--full' || mode === '--ci') {
    runFull();
  } else {
    throw new Error(`Unknown QA mode: ${mode}`);
  }
} catch (error) {
  console.error('AtlasBoard QA FAILED:', error.message);
  process.exitCode = 1;
}
