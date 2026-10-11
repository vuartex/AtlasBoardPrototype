/** Full Phase 15J local validation with fresh emulators, no deploy. */
import {spawnSync} from 'node:child_process';
import {dirname, resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const functionsDir = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const firebaseDir = resolve(functionsDir, '..');
const windows = process.platform === 'win32';
const npm = windows ? 'npm.cmd' : 'npm';
const npx = windows ? 'npx.cmd' : 'npx';
function run(label, bin, args, cwd = functionsDir) {
  console.log(`\nPhase 15J | ${label}`);
  const result = spawnSync(bin, args, {cwd, stdio: 'inherit', shell: windows, env: process.env});
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`${label} failed (exit ${result.status})`);
}
try {
  run('TypeScript build', npm, ['run', 'build']);
  run('Focused Steam/App Check ESLint', npx, ['eslint',
    'src/platform/steam-link.ts', 'src/platform/app-check-steam.ts']);
  run('Operations static security audit', 'node', ['scripts/phase15j-security-audit.mjs']);
  run('Environment preflight unit tests', 'node', ['scripts/phase15j-operations-test.mjs']);
  const cmd = 'node functions/scripts/phase15j-runtime-e2e.mjs' +
    ' && node functions/scripts/security-rules-e2e.mjs';
  // Windows cmd.exe requires the joined command quoted as ONE argument.
  const invocation = windows ? `"${cmd}"` : cmd;
  run('Fresh emulator smoke and Firestore rules E2E', npx,
    ['firebase', 'emulators:exec', '--project', 'atlasboard-usa',
      '--only', 'auth,firestore,functions', invocation], firebaseDir);
  console.log('\nPhase 15J combined validation PASS (local/emulator only).');
} catch (error) {
  console.error('\nPhase 15J combined validation FAILED:', error.message);
  process.exitCode = 1;
}
