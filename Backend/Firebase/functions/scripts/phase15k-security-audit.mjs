/** Static security invariants spanning Phase 15I, 15J and 15K. No cloud actions. */
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {dirname, resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const functionsDir = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const firebaseDir = resolve(functionsDir, '..');
const read = (path) => readFileSync(resolve(firebaseDir, path), 'utf8');
const link = read('functions/src/platform/steam-link.ts');
const bootstrap = read('functions/src/platform/app-check-steam.ts');
const guards = read('functions/src/security/steam-environment.ts');
const appCheckPolicy = read('functions/src/security/app-check-policy.ts');
const firebase = JSON.parse(read('firebase.json'));
const chat = read('functions/src/chat/chat.ts');
const rules = read('firestore.rules');
let count = 0;
function verify(expression, label) {
  assert.ok(expression, label);
  count++;
  console.log('PASS ' + label);
}
function inFunction(source, name) {
  const start = source.indexOf(`async function ${name}(`);
  assert(start >= 0, `Function not found: ${name}`);
  const end = source.indexOf('\n}', start);
  assert(end > start, `Function not terminated: ${name}`);
  return source.slice(start, end);
}
function beforePublisher(fn, variable) {
  return fn.includes(`assertSteamVerifierEnvironment(${variable});`) &&
    fn.indexOf(`assertSteamVerifierEnvironment(${variable});`) <
      fn.indexOf('const publisherKey =') && fn.indexOf('const publisherKey =') > 0;
}
try {
  verify(guards.includes('appId === 480') &&
    guards.includes('process.env.FUNCTIONS_EMULATOR !== "true"') &&
    guards.includes('STEAM_DEVELOPMENT_APP_ID_FORBIDDEN'),
  'Deployed runtime rejects Steam development AppID 480');
  verify(beforePublisher(inFunction(link, 'verifySteamWebApiTicket'), 'appId'),
  'Returning/link verifier checks environment before Secret Manager read');
  verify(beforePublisher(inFunction(bootstrap, 'verifySteamAppCheckProof'), 'steamAppId'),
  'App Check bootstrap verifier checks environment before Secret Manager read');
  verify(link.includes('secrets: [\n      STEAM_PUBLISHER_WEB_API_KEY,') &&
    bootstrap.includes('secrets: [\n      STEAM_PUBLISHER_WEB_API_KEY,'),
  'Steam publisher secret remains bound to verification callables');
  verify(bootstrap.includes('enforceAppCheck: false') &&
    appCheckPolicy.includes('isFunctionsEmulator &&'),
  'Steam bootstrap exception and staged App Check boundary retained');
  verify(/requireEmulatedFirestore\(\)/.test(link) &&
    /requireEmulatedAuth\(\)/.test(link),
  'Steam dev link and returning sign-in continue to require local emulators');
  verify(['auth','firestore','functions'].every((s) =>
    firebase.emulators?.[s]?.host === '127.0.0.1'),
  'All emulators remain loopback-only');
  verify(!link.includes('steamError:') && !bootstrap.includes('steamError:') &&
    !link.includes('error.message') && !bootstrap.includes('error.message'),
  'Publisher errors remain redacted');
  verify(/moderatorAuditRecord/.test(chat) &&
    /review_required_no_automatic_ttl_v1/.test(chat) &&
    /open_case_hold_v1/.test(chat),
  'Accepted Phase 15I moderation evidence retention preserved');
  verify(rules.includes('allow read, write: if false;'),
  'Firestore direct-client deny-by-default preserved');
  verify(read('functions/scripts/phase15j-release-preflight.mjs').includes(
    'PRODUCTION_BLOCKED_UNTIL_15K'),
  '15J release gate cannot silently be promoted by this package');
  verify(!read('functions/src/security/steam-environment.ts').includes('process.env.STEAM_PUBLISHER_WEB_API_KEY'),
  'Steam production guard never reads/logs publisher secret');
  console.log(`Phase 15K security audit PASS: ${count} checks.`);
} catch (e) {
  console.error('Phase 15K security audit FAILED:', e.message);
  process.exitCode = 1;
}
