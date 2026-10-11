/**
 * EXPLICIT opt-in, read-only deployed Cloud Function verification.
 * Requires pre-provisioned valid Firebase Auth ID token and valid App Check token.
 * Does NOT obtain, print, store, or rotate credentials; sends three health calls.
 * No default project or deployed URL; no local/emulator fallback.
 */
import {readFileSync, writeFileSync, existsSync} from 'node:fs';
import {resolve, basename} from 'node:path';
import {fileURLToPath} from 'node:url';

const arg = (key) => process.argv.find((v) => v.startsWith(`--${key}=`))?.slice(key.length + 3) ?? '';
const dryRun = process.argv.includes('--dry-run');
const acknowledge = process.argv.includes('--ack-live-probes');
function fail(message) { throw new Error(message); }
function tokenFile(file, label) {
  if (!file || !file.toLowerCase().endsWith('.local'))
    fail(`${label} must be an ignored private *.local file; do not paste token into CLI.`);
  if (!existsSync(file)) fail(`${label} file missing.`);
  const text = readFileSync(file, 'utf8').trim();
  if (text.length < 50 || text.length > 16384 || /\s/.test(text))
    fail(`${label} file is empty or is not a single token.`);
  return text;
}
function parseTarget(target, project) {
  let url;
  try { url = new URL(target); } catch { fail('Explicit deployed --target=https://.../economyHealthCheck required.'); }
  if (url.protocol !== 'https:' || url.username || url.password || url.search || url.hash ||
      !url.hostname.endsWith('.cloudfunctions.net') || url.port ||
      !url.pathname.endsWith('/economyHealthCheck')) {
    fail('Only the canonical HTTPS Firebase Cloud Functions health-check URL is permitted.');
  }
  // cloudfunctions.net URL shape: https://<region>-<project>.cloudfunctions.net/economyHealthCheck
  if (!url.hostname.endsWith(`-${project}.cloudfunctions.net`))
    fail('Target URL hostname does not match the explicitly requested project.');
  return url.toString();
}
export function validateInputs({project, appId, target, isLive}) {
  if (!project || project === 'atlasboard-usa' || !/^[a-z][a-z0-9-]{4,40}$/.test(project))
    fail('An explicitly approved non-development Firebase project is required.');
  if (!Number.isSafeInteger(appId) || appId <= 0 || appId === 480)
    fail('A real non-480 Steam AppID is required.');
  if (!isLive) fail('Explicit --ack-live-probes confirmation is required.');
  return parseTarget(target, project);
}
async function post(url, authToken, appcheck) {
  const headers = {'Content-Type': 'application/json', Authorization: `Bearer ${authToken}`};
  if (appcheck !== undefined) headers['X-Firebase-AppCheck'] = appcheck;
  const response = await fetch(url, {method: 'POST', headers,
    body: JSON.stringify({data: {}}), signal: AbortSignal.timeout(12000),
    redirect: 'error'});
  let json = {};
  try { json = await response.json(); } catch { /* never print responses */ }
  return {status: response.status, body: json};
}
async function main() {
  const project = arg('project');
  const appId = Number(arg('steam-app-id'));
  const url = validateInputs({project, appId, target: arg('target'), isLive: acknowledge});
  const idPath = arg('id-token-file');
  const appPath = arg('appcheck-token-file');
  const authToken = tokenFile(idPath, 'Firebase Auth ID token');
  const appCheckToken = tokenFile(appPath, 'App Check token');
  if (dryRun) {
    console.log('Phase 15K deployed probe preflight PASS (dry-run; no network request).');
    return;
  }
  // A positive control is mandatory; otherwise two 401s could just mean
  // an invalid Firebase Auth ID token, NOT proof of App Check enforcement.
  const positive = await post(url, authToken, appCheckToken);
  const valid = positive.body?.result ?? positive.body?.data;
  if (positive.status !== 200 || valid?.ok !== true ||
      valid?.authenticated !== true || valid?.projectId !== project || valid?.mode !== 'cloud') {
    fail('Valid Auth + valid App Check positive control did not return approved cloud project HTTP 200.');
  }
  console.log('PASS deployed valid Firebase Auth + valid App Check token: HTTP 200');
  const missing = await post(url, authToken, undefined);
  if (missing.status !== 401 || missing.body?.error?.status !== 'UNAUTHENTICATED')
    fail('Missing App Check token was not strictly rejected by deployed callable.');
  console.log('PASS deployed missing App Check token: HTTP 401 / UNAUTHENTICATED');
  const invalid = await post(url, authToken, 'atlasboard-phase15k-deliberately-invalid-appcheck');
  if (invalid.status !== 401 || invalid.body?.error?.status !== 'UNAUTHENTICATED')
    fail('Cryptographically invalid App Check token was NOT rejected by deployed callable.');
  console.log('PASS deployed cryptographically invalid App Check token: HTTP 401 / UNAUTHENTICATED');
  const receipt = {
    phase: '15K', type: 'deployed_app_check_cryptographic_negative',
    targetProject: project, targetHostname: new URL(url).hostname,
    steamAppId: appId, checkedAtUtc: new Date().toISOString(),
    positiveHttpStatus: 200, missingHttpStatus: 401, invalidHttpStatus: 401,
    assertion: 'real deployed test confirmed (not emulator)',
    note: 'Does not prove Steam ticket verification, IAM, monitoring, TTL or release approval.',
  };
  const receiptPath = arg('receipt-file');
  if (receiptPath) {
    if (!basename(receiptPath).endsWith('.local'))
      fail('Receipt filename must end with *.local and remain ignored by Git.');
    writeFileSync(resolve(receiptPath), JSON.stringify(receipt, null, 2) + '\n', {flag: 'wx', mode: 0o600});
    console.log('PASS sanitized receipt written to private *.local file (no tokens).');
  }
  console.log('Phase 15K DEPLOYED APP CHECK CRYPTOGRAPHIC PROBE PASS: 3 checks.');
}
if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  main().catch((e) => { console.error('Phase 15K deployed probe BLOCKED/FAILED:', e.message); process.exitCode = 1; });
}
