'use strict';

// Android provider and server-side security readiness gate. Read-only, no
// production network access, writes, Firebase deploy or test-token exchange.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');

const root = path.resolve(__dirname, '../../..');
const cs = (p) => fs.readFileSync(path.join(root, 'Assets/Project/Scripts', p), 'utf8');
const rulesPath = path.join(root, 'Backend/Firebase/database.rules.json');
const app = cs('Online/Firebase/AtlasBoardAppCheckTokenSource.cs');
const transport = cs('Online/Firebase/AtlasBoardAppCheckRequestSecurity.cs');
const steam = cs('Online/Firebase/AtlasBoardSteamAppCheckProvider.cs');
const rulesRaw = fs.readFileSync(rulesPath, 'utf8');
const rules = JSON.parse(rulesRaw).rules;
let checks = 0;
function ok(condition, label) {
  assert.ok(condition, label);
  checks++;
  console.log('PASS ' + label);
}

const android = app.match(/#elif UNITY_ANDROID([\s\S]*?)#elif UNITY_STANDALONE_WIN/);
ok(Boolean(android), 'Android has a dedicated platform-specific provider branch');
ok(/FirebaseAppCheck\.SetAppCheckProviderFactory\s*\(\s*PlayIntegrityProviderFactory\.Instance\s*\)/.test(android[1]), 'Android registers Play Integrity before Firebase service startup');
ok(/androidProviderConfigured\s*=\s*true/.test(android[1]), 'Android provider readiness is recorded only after registration');
ok(/androidProviderConfigured\s*=\s*false/.test(android[1]), 'Android registration failure does not report success');
ok(/#if UNITY_EDITOR[\s\S]*?TryConfigureDebugProviderFromEnvironment\([\s\S]*?#elif UNITY_ANDROID/.test(app), 'Editor debug provider stays independent of Android production provider');
ok((app.match(/!androidProviderConfigured/g) || []).length === 3, 'BeforeSceneLoad and token acquisition honor Android provider readiness');
ok(/RuntimeInitializeLoadType\.SubsystemRegistration/.test(app), 'App Check registration occurs in the early Unity startup path');
ok(/IsLocalEmulatorGuestSession/.test(app) && /IsLiveGuestQaSession/.test(app), 'Existing guest QA startup guards retained');
ok(/#if UNITY_STANDALONE_WIN \|\| UNITY_STANDALONE_OSX \|\| UNITY_STANDALONE_LINUX/.test(app) || /#elif UNITY_STANDALONE_WIN \|\| UNITY_STANDALONE_OSX \|\| UNITY_STANDALONE_LINUX/.test(app), 'Existing Steam desktop platform branch retained');
ok(/AtlasBoardSteamAppCheckProviderFactory/.test(app) && /platformSteamCreateAppCheckToken/.test(steam), 'Existing Steam custom-token flow retained, not asserted to work on Spark');
ok(/GetAppCheckTokenAsync\s*\(/.test(app) && /string\.Empty/.test(app), 'Failed App Check token acquisition does not mint a production replacement');
ok(/#if DEVELOPMENT_BUILD && !UNITY_EDITOR[\s\S]*?IsExplicitLocalGuestCallable/.test(transport), 'Local emulator-only header is guarded from release');
ok(/Uri\.TryCreate/.test(transport) && /loopback/i.test(transport), 'Emulator marker is constrained to loopback address');
ok(/SharedTokenCacheMilliseconds/.test(transport) && /TokenFailureBackoffMilliseconds/.test(transport), 'Shared App Check token cache and failure backoff retained');
ok(/X-Firebase-AppCheck/.test(app), 'App Check header constant retained');
ok(rulesRaw.length <= 262144, 'RTDB Rules under Firebase 256 KiB limit');
ok(rules['.read'] === false && rules['.write'] === false, 'RTDB root read and write remain closed');
ok(rules.users.$uid['.read'].includes('auth.uid==$uid') && rules.users.$uid['.write'] === false, 'UID-specific user reads and non-blanket user writes retained');
ok(rules.publicRoomDirectory['.write'] === false, 'Public directory does not grant blanket write');

// This is intentionally NOT a pass/fail attestation audit. These currently
// direct REST transports do not all send an App Check header. Turning on
// RTDB enforcement before fixing them would break legitimate clients.
const coverageCandidates = [
  'UI/Menu/AtlasBoardNewsCommunityHub.cs',
  'UI/Menu/AtlasBoardReleaseVersionBadge.cs',
  'Online/Firebase/AtlasBoardRtdbCasualLobbyTransport.cs',
  'Online/Firebase/AtlasBoardSparkRtdbMatchTransport.cs'
];
const uncovered = [];
for (const name of coverageCandidates) {
  const code = cs(name);
  const hasRest = /UnityWebRequest\.(?:Get|Put|Post)\s*\(|new UnityWebRequest\s*\(/.test(code);
  const hasAppCheck = /AtlasBoardAppCheckRequestSecurity\.ApplyAsync\s*\(|X-Firebase-AppCheck|AtlasBoardAppCheckTokenSource\.RequestHeaderName/.test(code);
  if (hasRest && !hasAppCheck) uncovered.push(name);
}
console.log('KNOWN_REST_TRANSPORT_GAPS=' + JSON.stringify(uncovered));
console.log('APPCHECK_ANDROID_PROVIDER_SOURCE_GATE=' + checks + '/' + checks + ' PASS');
console.log('ANDROID_REAL_DEVICE_PLAY_INTEGRITY=NOT_RUN');
console.log('RTDB_REST_COVERAGE=' + (uncovered.length ? 'INCOMPLETE' : 'STATIC_CANDIDATES_COVERED_ONLY'));
console.log('STEAM_PRODUCTION_ATTESTATION=NOT_VERIFIED_SPARK');
console.log('RTDB_APPCHECK_PRODUCTION_ENFORCEMENT=UNSAFE_TO_ENABLE');
console.log('LIVE_FIREBASE_CHANGES=NONE');
