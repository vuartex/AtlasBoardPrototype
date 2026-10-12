/**
 * AtlasBoard Phase 17A — read-only Steam local readiness inventory.
 * Never authenticates to Steam, Firebase, or Valve; never reads secret stores,
 * writes project files, or performs network calls. Runtime proof is MANUAL.
 * Node 18+; intended Windows PowerShell entry point in functions directory.
 */
import assert from 'node:assert/strict';
import {existsSync, readFileSync, readdirSync, statSync, writeFileSync} from 'node:fs';
import {tmpdir, platform} from 'node:os';
import {basename, dirname, extname, isAbsolute, join, resolve} from 'node:path';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '../../../..');
const argv = process.argv.slice(2);
const getOpt = (key) => {
  const matches = argv.filter((a) => a.startsWith(`--${key}=`));
  if (matches.length > 1) throw new Error(`Duplicate option --${key}`);
  return matches.length ? matches[0].slice(key.length + 3) : null;
};
const VALID = new Set(['--self-test', '--help']);
for (const item of argv) {
  if (VALID.has(item)) continue;
  if (!['--app-id=', '--build='].some((prefix) => item.startsWith(prefix))) {
    throw new Error('Unknown argument. Use --help for supported options.');
  }
}

function parseAppId(value) {
  if (typeof value !== 'string' || !/^[0-9]{1,10}$/.test(value.trim())) return null;
  const n = Number(value.trim());
  return Number.isSafeInteger(n) && n > 0 && n <= 4294967295 ? n : null;
}
function classifyAppId(id) {
  if (id === null) return 'UNKNOWN';
  if (id === 480) return 'DEV_SPACEWAR_NOT_RELEASE_PROOF';
  return 'NON_480_UNVERIFIED_OWNERSHIP';
}
function readAppIdFile(path) {
  try {
    const stat = statSync(path);
    if (!stat.isFile() || stat.size > 128) return {kind: 'INVALID'};
    const value = readFileSync(path, 'utf8').trim();
    const id = parseAppId(value);
    return id ? {kind: classifyAppId(id), id} : {kind: 'INVALID'};
  } catch {return null;}
}
function readableSource(relPath) {
  try {
    const file = resolve(root, relPath);
    if (!existsSync(file) || statSync(file).size > 500000) return null;
    return readFileSync(file, 'utf8');
  } catch {return null;}
}
function steamProcessStatus() {
  if (platform() !== 'win32') return 'UNAVAILABLE_NON_WINDOWS';
  try {
    const raw = execFileSync('tasklist.exe', ['/FI', 'IMAGENAME eq steam.exe', '/FO', 'CSV', '/NH'],
      {encoding: 'utf8', timeout: 6000, windowsHide: true, maxBuffer: 128000});
    return /(?:^|[\r\n])\s*"steam\.exe"\s*,/im.test(raw) ? 'RUNNING' : 'NOT_DETECTED';
  } catch {return 'NOT_DETERMINED';}
}
function discoverBuilds() {
  const dirNames = [root, join(root, 'Builds'), join(root, 'Build'),
    join(root, 'AtlasBoard_3D3C_GuestBuild'), join(root, 'WindowsBuild')];
  for (const parent of [join(root, 'Builds'), join(root, 'Build')]) {
    if (!existsSync(parent)) continue;
    try {
      for (const d of readdirSync(parent, {withFileTypes: true}).slice(0, 60)) {
        if (d.isDirectory()) dirNames.push(join(parent, d.name));
      }
    } catch { /* directory not accessible */ }
  }
  const candidates = [];
  const seen = new Set();
  for (const dir of dirNames) {
    if (!existsSync(dir)) continue;
    let entries = [];
    try {entries = readdirSync(dir, {withFileTypes: true});} catch {continue;}
    for (const entry of entries) {
      if (!entry.isFile() || extname(entry.name).toLowerCase() !== '.exe' ||
        !/atlasboard/i.test(entry.name) || seen.has(join(dir, entry.name))) continue;
      const file = join(dir, entry.name);
      seen.add(file);
      candidates.push({exe: file, filename: entry.name});
    }
  }
  return candidates;
}
function evidenceForBuild(path) {
  if (!path) return null;
  if (!isAbsolute(path)) throw new Error('--build must be an absolute path to a Windows .exe.');
  if (extname(path).toLowerCase() !== '.exe') throw new Error('--build must point to an .exe.');
  if (!existsSync(path) || !statSync(path).isFile()) throw new Error('--build .exe not found.');
  return {exe: resolve(path), filename: basename(path)};
}
function safeAddUnique(items, item) {
  if (!items.some((x) => x.filename === item.filename && x.appId === item.appId)) items.push(item);
}
function analyze({sources, steamRunning, builds, explicitAppId, projectAppId}) {
  const checks = [
    {id: 'unity_platform_adapter_source', status: sources.adapter?.includes('SteamAPI.Init()') && sources.adapter.includes('GetAppID()') ? 'PRESENT_NOT_RUNTIME_PROOF' : 'MISSING_OR_UNRECOGNIZED'},
    {id: 'unity_steam_diagnostics_source', status: sources.diagnostics?.includes('Phase 11 Diagnostics') ? 'PRESENT' : 'MISSING_OR_UNRECOGNIZED'},
    {id: 'steam_account_link_source', status: sources.link?.includes('Steam') ? 'PRESENT' : 'MISSING_OR_UNRECOGNIZED'},
    {id: 'backend_development_appid_guard', status: sources.steamGuard?.includes('STEAM_DEVELOPMENT_APP_ID_FORBIDDEN') ? 'PRESENT_NOT_DEPLOYED_PROOF' : 'MISSING_OR_UNRECOGNIZED'},
    {id: 'local_guest_steam_bypass', status: sources.platform?.includes('IsLocalEmulatorGuestSession') ? 'PRESENT_INTENTIONAL' : 'NOT_DETECTED'},
    {id: 'steam_desktop_process', status: steamRunning},
    {id: 'windows_standalone_build', status: builds.length ? 'FOUND_NOT_RELEASE_PROOF' : 'NOT_FOUND'},
    {id: 'declared_steam_appid', status: classifyAppId(explicitAppId ?? projectAppId)},
  ];
  const blockers = [];
  if (!sources.adapter || !sources.diagnostics || !sources.link) blockers.push('LOCAL_STEAM_SOURCE_OR_DIAGNOSTICS_MISSING');
  if (steamRunning !== 'RUNNING') blockers.push('STEAM_CLIENT_NOT_VERIFIED_RUNNING');
  if (explicitAppId === null && projectAppId === null) blockers.push('STEAM_APP_ID_NOT_PROVIDED_OR_FOUND');
  if ((explicitAppId ?? projectAppId) === 480) blockers.push('APP_ID_480_IS_DEVELOPMENT_ONLY');
  if (!builds.length) blockers.push('STANDALONE_BUILD_NOT_DISCOVERED');
  // Even with a non-480 AppID, static checking cannot prove publisher access or authentication.
  blockers.push('REAL_STEAM_IDENTITY_RUNTIME_EVIDENCE_PENDING');
  blockers.push('PUBLISHER_TICKET_VERIFICATION_PENDING');
  blockers.push('STEAM_CUSTOM_APPCHECK_DEPLOYED_EVIDENCE_PENDING');
  return {checks, blockers, status: 'PHASE17A_READINESS_INVENTORY_COMPLETE_PRODUCTION_NOT_VERIFIED'};
}
function selfTest() {
  assert.equal(parseAppId('480'), 480);
  assert.equal(parseAppId('123456'), 123456);
  for (const bad of ['', '0', '-480', '1.5', 'a', '4294967296', '123 456']) assert.equal(parseAppId(bad), null);
  assert.equal(classifyAppId(480), 'DEV_SPACEWAR_NOT_RELEASE_PROOF');
  assert.equal(classifyAppId(123456), 'NON_480_UNVERIFIED_OWNERSHIP');
  assert.equal(classifyAppId(null), 'UNKNOWN');
  const x = analyze({sources: {}, steamRunning: 'NOT_DETECTED', builds: [], explicitAppId: 480, projectAppId: null});
  assert.equal(x.status, 'PHASE17A_READINESS_INVENTORY_COMPLETE_PRODUCTION_NOT_VERIFIED');
  assert(x.blockers.includes('APP_ID_480_IS_DEVELOPMENT_ONLY'));
  const y = analyze({sources: {adapter: 'SteamAPI.Init() GetAppID()', diagnostics: 'Phase 11 Diagnostics', link: 'Steam', steamGuard: 'STEAM_DEVELOPMENT_APP_ID_FORBIDDEN', platform: 'IsLocalEmulatorGuestSession'}, steamRunning: 'RUNNING', builds: [{filename: 'AtlasBoard.exe'}], explicitAppId: 123456, projectAppId: null});
  assert(!y.blockers.includes('APP_ID_480_IS_DEVELOPMENT_ONLY'));
  assert(y.blockers.includes('PUBLISHER_TICKET_VERIFICATION_PENDING'));
  assert(y.blockers.includes('REAL_STEAM_IDENTITY_RUNTIME_EVIDENCE_PENDING'));
  console.log('PASS Phase17A self-test: AppID bounds, local-only classifications, zero-unearned production acceptance.');
}
function main() {
  if (argv.includes('--help')) {
    console.log('Usage: node scripts\\phase17a-steam-readiness.mjs [--app-id=123456] [--build="C:\\...\\AtlasBoard.exe"]');
    console.log('Only inventories local files/process; never uses any Steam, Firebase, or network API. AppID is not a secret; never supply publisher secrets.');
    return;
  }
  if (argv.includes('--self-test')) { selfTest(); return; }
  if (!existsSync(join(root, 'Assets', 'Project')) || !existsSync(join(root, 'Backend', 'Firebase', 'functions'))) {
    throw new Error('Project root not found relative to test script. Extract ZIP into AtlasBoardPrototype root.');
  }
  const rawExplicit = getOpt('app-id');
  const explicitAppId = rawExplicit === null ? null : parseAppId(rawExplicit);
  if (rawExplicit !== null && explicitAppId === null) throw new Error('Invalid --app-id: must be a positive integer <= 4294967295.');
  const suppliedBuild = evidenceForBuild(getOpt('build'));
  const discovered = discoverBuilds();
  if (suppliedBuild && !discovered.some((x) => x.exe === suppliedBuild.exe)) discovered.unshift(suppliedBuild);
  const sources = {
    adapter: readableSource('Assets/Project/Scripts/Platform/AtlasBoardSteamPlatformAdapter.cs'),
    diagnostics: readableSource('Assets/Project/Editor/AtlasBoardPlatformDiagnosticsWindow.cs'),
    link: readableSource('Assets/Project/Scripts/Platform/AtlasBoardSteamAccountLinkBridge.cs'),
    platform: readableSource('Assets/Project/Scripts/Platform/AtlasBoardPlatformRuntime.cs'),
    steamGuard: readableSource('Backend/Firebase/functions/src/security/steam-environment.ts'),
  };
  const appIds = [];
  const appidPaths = [join(root, 'steam_appid.txt'), join(root, 'Assets', 'steam_appid.txt')];
  for (const b of discovered) appidPaths.push(join(dirname(b.exe), 'steam_appid.txt'));
  for (const p of appidPaths) {
    const info = readAppIdFile(p);
    if (info) safeAddUnique(appIds, {filename: p.includes('AtlasBoard_3D3C_GuestBuild') ? 'GuestBuild/steam_appid.txt' : basename(dirname(p)) + '/steam_appid.txt', appId: info.id ?? null, status: info.kind});
  }
  const uniqueIDs = [...new Set(appIds.map((x) => x.appId).filter((id) => id !== null))];
  const projectAppId = uniqueIDs.length === 1 ? uniqueIDs[0] : null;
  const steamRunning = steamProcessStatus();
  const report = {
    label: 'AtlasBoard Phase17A Steam readiness — READ ONLY',
    generatedAtUtc: new Date().toISOString(),
    safety: {projectFilesModified: false, networkRequests: 0, secretsRead: false, tokensRequested: false, productionApproval: false},
    environment: {isWindows: platform() === 'win32', steamDesktopProcess: steamRunning, appIdCandidateFromCLI: explicitAppId, appIdCandidatesFound: appIds, conflictingAppIdsDetected: uniqueIDs.length > 1,
      standaloneCandidates: discovered.map((x) => ({filename: x.filename, isGuestBuild: /guest/i.test(x.filename)}))},
    ...analyze({sources, steamRunning, builds: discovered, explicitAppId, projectAppId}),
    manualEvidence: {
      unitySteamPlatformInitialized: 'NOT_EXECUTED_BY_SCRIPT',
      actualSteamAppIdMatchesApprovedPublisherAppId: 'NOT_VERIFIED',
      editorReturningSignInWithVerifiedPublisherTicket: 'NOT_VERIFIED',
      publisherTicketVerifiedByValve: 'NOT_VERIFIED',
      productionSteamCustomAppCheck: 'NOT_VERIFIED',
      realSteamMultiplayer: 'NOT_VERIFIED',
    },
    notes: [
      'A Unity Guest local-emulator build can deliberately bypass Steam; Guest multiplayer tests are not production Steam identity proof.',
      'Spacewar AppID 480 is development-only, never AtlasBoard publisher proof.',
      'Non-480 Steam AppID input does not prove ownership or Steamworks partner permissions.',
      'No SteamID, persona, API key, ticket or token values are read, recorded or printed.',
      'Use Unity Phase 11 Diagnostics in Play Mode for Steam client smoke test; publisher AppCheck and returning sign-in need a separate approved environment.',
    ],
  };
  const safeId = new Date().toISOString().replace(/[-:.TZ]/g, '').slice(0, 14);
  const filename = join(tmpdir(), `AtlasBoard_Phase17A_SteamReadiness_${safeId}.json`);
  writeFileSync(filename, JSON.stringify(report, null, 2) + '\n', {encoding:'utf8', flag:'wx', mode: 0o600});
  console.log('AtlasBoard Phase17A Steam readiness (READ ONLY; no login or network calls).');
  for (const item of report.checks) console.log('  ' + item.id + ': ' + item.status);
  console.log('Deployed Steam ticket and custom App Check: NOT VERIFIED (expected until genuine publisher setup).');
  console.log(`Outstanding readiness gates: ${report.blockers.join(', ')}`);
  console.log('INVENTORY COMPLETE. This output is NOT a Phase17 production PASS.');
  console.log('REPORT_JSON: ' + filename);
}
try { main(); } catch (e) { console.error('Phase17A readiness diagnostic failed: ' + e.message); process.exitCode = 1; }
