/**
 * Phase 15K guarded additive patch for accepted 15J Steam verifier sources.
 * Never deploys, pushes, reads environment secrets, or overwrites unknown anchors.
 */
import {readFileSync, writeFileSync, renameSync, unlinkSync} from 'node:fs';
import {dirname, resolve} from 'node:path';
import {fileURLToPath} from 'node:url';

const scriptsDir = dirname(fileURLToPath(import.meta.url));
const functionsDir = resolve(scriptsDir, '..');
const patchImport = 'import {assertSteamVerifierEnvironment} from "../security/steam-environment";';
const paths = [
  {name: 'Steam account verifier', path: 'src/platform/steam-link.ts',
    fn: 'async function verifySteamWebApiTicket(', variable: 'appId',
    version: 'failureClass: "steam_publisher_transport"',
    importAnchor: 'import {assertAccountRateLimit} from "../security/callable-abuse";'},
  {name: 'Steam App Check bootstrap verifier', path: 'src/platform/app-check-steam.ts',
    fn: 'async function verifySteamAppCheckProof(', variable: 'steamAppId',
    version: 'failureClass: "steam_app_check_transport"',
    importAnchor: 'const REGION = "europe-west1";'},
];

function replaceExactlyOnce(source, from, to, label) {
  if (!source.includes(from) || source.indexOf(from) !== source.lastIndexOf(from)) {
    throw new Error(`Unknown or non-unique anchor (${label}); preserve local changes.`);
  }
  return source.replace(from, to);
}

export function patchSource(input, cfg) {
  const normalized = input.replace(/\r\n/g, '\n');
  if (!normalized.includes(cfg.version)) {
    throw new Error(`${cfg.name}: accepted Phase 15J privacy patch not found.`);
  }
  const call = `  assertSteamVerifierEnvironment(${cfg.variable});`;
  const fnStart = normalized.indexOf(cfg.fn);
  if (fnStart < 0 || normalized.indexOf(cfg.fn, fnStart + 1) >= 0) {
    throw new Error(`${cfg.name}: verifier function anchor changed.`);
  }
  const nextFunction = normalized.indexOf('\n}', fnStart);
  if (nextFunction < 0) throw new Error(`${cfg.name}: verifier function is incomplete.`);
  const inside = normalized.slice(fnStart, nextFunction);
  if (normalized.includes(patchImport) && inside.includes(call)) return normalized;
  if (normalized.includes(patchImport) !== inside.includes(call)) {
    throw new Error(`${cfg.name}: partial guard installation detected.`);
  }
  if (inside.includes('assertSteamVerifierEnvironment(')) {
    throw new Error(`${cfg.name}: unknown existing guard; refusing to duplicate.`);
  }
  const publisherAnchor = '  const publisherKey =\n';
  if (!inside.includes(publisherAnchor) ||
      inside.indexOf(publisherAnchor) !== inside.lastIndexOf(publisherAnchor)) {
    throw new Error(`${cfg.name}: publisher key anchor changed.`);
  }
  // Keep the guard immediately before any publisher secret is read.
  const next = inside.replace(publisherAnchor, `${call}\n\n${publisherAnchor}`);
  let patched = normalized.slice(0, fnStart) + next + normalized.slice(nextFunction);
  if (cfg.path.endsWith('steam-link.ts')) {
    patched = replaceExactlyOnce(patched, cfg.importAnchor,
      cfg.importAnchor + '\n' + patchImport, cfg.name + ' import');
  } else {
    patched = replaceExactlyOnce(patched, cfg.importAnchor,
      patchImport + '\n\n' + cfg.importAnchor, cfg.name + ' import');
  }
  return patched;
}

function main() {
  const dry = process.argv.includes('--dry-run');
  const plans = [];
  for (const cfg of paths) {
    const abs = resolve(functionsDir, cfg.path);
    const input = readFileSync(abs, 'utf8');
    const out = patchSource(input, cfg);
    if (out === input.replace(/\r\n/g, '\n')) {
      console.log(`ALREADY GUARDED ${cfg.path}`);
      continue;
    }
    const desired = input.includes('\r\n') ? out.replace(/\n/g, '\r\n') : out;
    plans.push({cfg, abs, original: input, desired});
    console.log(`READY ${cfg.path}`);
  }
  if (dry) {
    console.log(`Phase 15K dry-run PASS: ${plans.length} safe verifier edits.`);
    return;
  }
  const done = [];
  try {
    for (const plan of plans) {
      const temp = plan.abs + '.phase15k-temp';
      writeFileSync(temp, plan.desired, 'utf8');
      renameSync(temp, plan.abs);
      done.push(plan);
      console.log(`PATCHED ${plan.cfg.path}`);
    }
  } catch (err) {
    for (const p of done.reverse()) writeFileSync(p.abs, p.original, 'utf8');
    for (const p of plans) {
      try { unlinkSync(p.abs + '.phase15k-temp'); } catch { /* none */ }
    }
    throw err;
  }
  console.log('Phase 15K guarded Steam runtime safety patch PASS (no deploy/Git changes).');
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { main(); } catch (e) {
    console.error('Phase 15K apply FAILED:', e.message);
    process.exitCode = 1;
  }
}
