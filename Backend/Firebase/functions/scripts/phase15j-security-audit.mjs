/** Phase 15J fail-closed static operations/security audit; no cloud writes. */
import {readFileSync} from 'node:fs';
import {dirname, resolve} from 'node:path';
import {fileURLToPath} from 'node:url';

const scriptsDir = dirname(fileURLToPath(import.meta.url));
const functionsDir = resolve(scriptsDir, '..');
const firebaseDir = resolve(functionsDir, '..');
const read = (relative) => readFileSync(resolve(firebaseDir, relative), 'utf8');
const cfg = JSON.parse(read('firebase.json'));
const steam = read('functions/src/platform/steam-link.ts');
const appcheck = read('functions/src/platform/app-check-steam.ts');
const policy = read('functions/src/security/app-check-policy.ts');
const rules = read('firestore.rules');
const ignore = read('.gitignore');
const fnIgnore = read('functions/.gitignore');
let count = 0;
function assert(test, title) {
  if (!test) throw new Error(`FAIL ${title}`);
  count++;
  console.log(`PASS ${title}`);
}

try {
  assert(['auth', 'firestore', 'functions'].every((name) =>
    cfg.emulators?.[name]?.host === '127.0.0.1'),
  'All Firebase emulators bound to loopback (not LAN)');
  assert(cfg.emulators?.ui?.host === '127.0.0.1',
    'Emulator UI bound to loopback');
  assert(cfg.functions?.[0]?.disallowLegacyRuntimeConfig === true,
    'Functions legacy runtime config disabled');
  assert(ignore.includes('.env.*') && fnIgnore.includes('*.local') &&
    ignore.includes('service-account') && ignore.includes('*.pem'),
  'Local env/credentials excluded from Git');
  assert(/defineSecret\(\s*["']STEAM_PUBLISHER_WEB_API_KEY["']/.test(steam) &&
    /secrets:\s*\[\s*STEAM_PUBLISHER_WEB_API_KEY/.test(steam),
  'Steam publisher key is a bound Functions secret');
  assert(/defineSecret\(\s*["']STEAM_PUBLISHER_WEB_API_KEY["']/.test(appcheck) &&
    /secrets:\s*\[\s*STEAM_PUBLISHER_WEB_API_KEY/.test(appcheck),
  'App Check bootstrap uses bound publisher secret');
  assert(!steam.includes('error.message') && !steam.includes('String(error)') &&
    !steam.includes('steamError:'),
  'Steam verifier logs omit raw failure strings');
  assert(!appcheck.includes('error.message') && !appcheck.includes('String(error)') &&
    !appcheck.includes('steamError:'),
  'App Check verifier logs omit raw failure strings');
  assert((steam.match(/failureClass:/g) ?? []).length >= 2 &&
    (appcheck.match(/failureClass:/g) ?? []).length >= 3,
  'Steam/App Check errors use bounded failure classes');
  for (const title of [
    'AtlasBoard verified Steam account link completed.',
    'AtlasBoard emulator Steam link proof completed.',
    'AtlasBoard returning Steam sign-in token issued.',
    'AtlasBoard emulator returning Steam sign-in issued.',
  ]) {
    const start = steam.indexOf(`"${title}"`);
    const end = steam.indexOf('    );', start);
    assert(start >= 0 && end > start &&
      !/\baccountId\s*:|\bsteamId\s*:/.test(steam.slice(start, end)),
    `Steam operational log redacts account/Steam identifiers: ${title}`);
  }
  assert(/isFunctionsEmulator\s*&&\s*localEnforcementRequested/.test(policy),
    'App Check staging gate unchanged; production rollout deferred');
  assert(/match \/\{document=\*\*\}/.test(rules) &&
    /allow read, write: if false/.test(rules),
  'Firestore default deny preserved');
  const commands = read('functions/scripts/phase15j-release-preflight.mjs');
  assert(commands.includes('PRODUCTION_BLOCKED_UNTIL_15K'),
    'Production preflight is fail-closed until Phase 15K');
  console.log(`Phase 15J operations security audit PASS: ${count} checks.`);
} catch (error) {
  console.error('Phase 15J operations security audit FAILED:', error.message);
  process.exitCode = 1;
}
