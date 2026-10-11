/**
 * Phase 15K combined LOCAL security regression. Builds, audits, runs auth/chat/
 * economy/Steam emulator suites, and locally staged App Check enforcement.
 * Temporarily edits only .env.local; guarantees normal-exit restoration.
 * If abruptly terminated, recover using `node scripts/phase15k-validation.mjs --restore-env`.
 * Never contacts Steam live, deploys, changes IAM, or commits Git changes.
 */
import {spawnSync} from 'node:child_process';
import {existsSync, readFileSync, writeFileSync, copyFileSync, unlinkSync} from 'node:fs';
import {dirname, resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const funcs = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const firebase = resolve(funcs, '..');
const envFile = resolve(funcs, '.env.local');
const backupFile = envFile + '.phase15k-backup';
const windows = process.platform === 'win32';
const npm = windows ? 'npm.cmd' : 'npm';
const npx = windows ? 'npx.cmd' : 'npx';
const staticOnly = process.argv.includes('--static-only');

function run(label, bin, args, cwd = funcs) {
  console.log(`\nPhase 15K | ${label}`);
  const result = spawnSync(bin, args, {
    cwd, stdio: 'inherit', shell: windows,
    env: {...process.env, FUNCTIONS_DISCOVERY_TIMEOUT: '90'},
  });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`${label} failed (exit ${result.status ?? 'unknown'})`);
}
function setLocalAppCheck(enabled) {
  // Never print or embed the original .env.local contents in output/artifacts.
  const original = readFileSync(envFile, 'utf8');
  const setting = `ATLAS_ENFORCE_APP_CHECK=${enabled ? 'true' : 'false'}`;
  const next = /(?:^|\r?\n)ATLAS_ENFORCE_APP_CHECK=[^\r\n]*/m.test(original) ?
    original.replace(/(^|\r?\n)ATLAS_ENFORCE_APP_CHECK=[^\r\n]*/gm,
      (_, prefix) => prefix + setting) :
    original.replace(/\s*$/, '') + '\n' + setting + '\n';
  writeFileSync(envFile, next, 'utf8');
}
function emulator(label, command) {
  // Windows firebase emulators:exec requires ONE quoted command argument.
  const invocation = windows ? `"${command}"` : command;
  run(label, npx, ['firebase', 'emulators:exec',
    '--project', 'atlasboard-usa',
    '--only', 'auth,firestore,functions', invocation], firebase);
}
function restoreBackup() {
  if (!existsSync(backupFile)) throw new Error('No Phase 15K .env.local backup found.');
  copyFileSync(backupFile, envFile);
  unlinkSync(backupFile);
  console.log('Original .env.local restored from private backup.');
}
try {
  if (process.argv.includes('--restore-env')) {
    restoreBackup();
  } else {
    run('Guarded patch unit', 'node', ['scripts/phase15k-apply-test.mjs']);
    run('TypeScript build', npm, ['run', 'build']);
    run('Focused TypeScript ESLint', npx, ['eslint',
      'src/security/steam-environment.ts', 'src/platform/steam-link.ts',
      'src/platform/app-check-steam.ts', 'src/chat/chat.ts']);
    run('Steam runtime environment unit', 'node', ['scripts/phase15k-steam-environment-test.mjs']);
    run('15K security audit', 'node', ['scripts/phase15k-security-audit.mjs']);
    run('15F App Check callable inventory', 'node', ['scripts/app-check-policy-audit.mjs']);
    run('15I moderation audit', 'node', ['scripts/moderation-security-audit.mjs']);
    run('15J operations audit', 'node', ['scripts/phase15j-security-audit.mjs']);
    run('15J environment unit', 'node', ['scripts/phase15j-operations-test.mjs']);
    run('Production release remains blocked', 'node', ['scripts/phase15k-production-gate-test.mjs']);
    run('Deployed probe input safety', 'node', ['scripts/phase15k-deployed-probe-test.mjs']);
    if (!staticOnly) {
      if (!existsSync(envFile)) throw new Error('.env.local missing; preserve Steam dev config and retry.');
      if (existsSync(backupFile)) throw new Error('Private Phase 15K backup exists. ' +
        'Restore it using --restore-env BEFORE rerunning.');
      copyFileSync(envFile, backupFile, 1); // COPYFILE_EXCL; no overwrite
      try {
        setLocalAppCheck(false);
        const combined = [
          'chat-moderation-e2e.mjs',
          'phase15j-runtime-e2e.mjs',
          'platform-steam-link-e2e.mjs',
          'platform-steam-returning-signin-e2e.mjs',
          'callable-authorization-abuse-e2e.mjs',
          'economy-commerce-security-e2e.mjs',
          'security-rules-e2e.mjs',
        ].map((name) => `node functions/scripts/${name}`).join(' && ');
        emulator('Fresh emulator full security regression (App Check OFF for integration)', combined);
        setLocalAppCheck(true);
        emulator('Fresh emulator App Check enforcement negative/exception regression',
          'node functions/scripts/app-check-enforcement-negative-e2e.mjs');
      } finally {
        restoreBackup();
      }
    }
    if (!staticOnly) {
      run('Append-only roadmap local acceptance checkpoint', 'node',
        ['scripts/phase15k-roadmap-checkpoint.mjs']);
    }
    console.log(`\nPhase 15K combined LOCAL validation PASS${staticOnly ? ' (static-only)' : ' (two emulator modes)'}; production remains BLOCKED.`);
  }
} catch (e) {
  console.error('Phase 15K combined LOCAL validation FAILED:', e.message);
  process.exitCode = 1;
}
