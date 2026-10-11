import assert from 'node:assert/strict';
import {patchSource} from './phase15k-apply.mjs';

const config = {
  name: 'Test Steam verifier', path: 'src/platform/steam-link.ts',
  fn: 'async function verifySteamWebApiTicket(', variable: 'appId',
  version: 'failureClass: "steam_publisher_transport"',
  importAnchor: 'import {assertAccountRateLimit} from "../security/callable-abuse";',
};
const source = `import {assertAccountRateLimit} from "../security/callable-abuse";\n` +
  `const marker = 'failureClass: "steam_publisher_transport"';\n` +
  `async function verifySteamWebApiTicket(ticket) {\n` +
  `  const appId = 480;\n` +
  `  if (!appId) throw Error();\n` +
  `  const publisherKey =\n    readPublisherSecret();\n` +
  `  return publisherKey;\n}\n`;
const updated = patchSource(source, config);
assert(updated.includes('assertSteamVerifierEnvironment(appId);'));
assert(updated.indexOf('assertSteamVerifierEnvironment(appId);') <
  updated.indexOf('  const publisherKey ='));
assert.equal(patchSource(updated, config), updated, 'Patch must be idempotent');
let refused = false;
try { patchSource(source.replace('failureClass: "steam_publisher_transport"', 'old'), config); }
catch { refused = true; }
assert(refused, 'Unknown/old Phase 15J source must be rejected');
refused = false;
try { patchSource(source.replace('const publisherKey', 'const secretKey'), config); }
catch { refused = true; }
assert(refused, 'Unexpected verifier source must be rejected');
console.log('Phase 15K guarded patch unit tests PASS: 4 checks.');

const appCfg = {
  name: 'App Check verifier fixture', path: 'src/platform/app-check-steam.ts',
  fn: 'async function verifySteamAppCheckProof(', variable: 'steamAppId',
  version: 'failureClass: "steam_app_check_transport"',
  importAnchor: 'const REGION = "europe-west1";',
};
const appFixture = `const REGION = "europe-west1";\n` +
  `const marker = 'failureClass: "steam_app_check_transport"';\n` +
  `async function verifySteamAppCheckProof(ticket) {\n` +
  `  const steamAppId = 480;\n` +
  `  const publisherKey =\n    readPublisherSecret();\n` +
  `  return publisherKey;\n}\n`;
const appUpdated = patchSource(appFixture, appCfg);
assert(appUpdated.includes('assertSteamVerifierEnvironment(steamAppId);'));
assert.equal(patchSource(appUpdated, appCfg), appUpdated);
console.log('Phase 15K App Check guard patch fixtures PASS: 2 checks.');
