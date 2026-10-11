/**
 * AtlasBoard Phase 15J guarded local source edits.
 * Does not deploy, commit, push, or read/copy credential values.
 * Refuses unknown source revisions instead of overwriting local work.
 */
import {createHash} from 'node:crypto';
import {readFileSync, writeFileSync, renameSync, unlinkSync} from 'node:fs';
import {resolve, dirname} from 'node:path';
import {fileURLToPath} from 'node:url';

const scriptsDir = dirname(fileURLToPath(import.meta.url));
const functionsDir = resolve(scriptsDir, '..');
const firebaseDir = resolve(functionsDir, '..');
const expected = new Map([
  ['functions/src/platform/steam-link.ts', '6274512843120a89fefbd6f341b1806a58cb0f66'],
  ['functions/src/platform/app-check-steam.ts', '3901ce3e14dd1195942e45c04f83c9433dfe8911'],
  ['firebase.json', 'bd264a105f427023c67dd623523e068131e83fd4'],
  ['.gitignore', 'b17f6310755422d220e2c975ca4387ab465c95a0'],
]);

function blobSha(content) {
  const bytes = Buffer.from(content, 'utf8');
  return createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex');
}

function replaceOne(source, before, after, label) {
  if (!source.includes(before)) {
    throw new Error(`Missing anchor for ${label}; refusing to patch unknown source.`);
  }
  if (source.split(before).length !== 2) {
    throw new Error(`Non-unique anchor for ${label}; refusing to patch.`);
  }
  return source.replace(before, after);
}

function reduceSensitiveLogFields(source, title) {
  const before = `      "${title}",`;
  const start = source.indexOf(before);
  if (start < 0 || source.indexOf(before, start + 1) >= 0) {
    throw new Error(`Could not uniquely identify log: ${title}`);
  }
  const end = source.indexOf('    );', start);
  if (end < 0) throw new Error(`No log terminator for ${title}`);
  const block = source.slice(start, end);
  if (!block.match(/^\s+accountId: [^\n]+,\n/m) ||
      !block.match(/^\s+steamId(?:\s*:\s*[^\n]+)?,\n/m)) {
    throw new Error(`Unexpected log structure for ${title}`);
  }
  const result = block
    .replace(/^\s+accountId: [^\n]+,\n/m, '')
    .replace(/^\s+steamId(?:\s*:\s*[^\n]+)?,\n/m, '');
  return source.slice(0, start) + result + source.slice(end);
}

function scrubTransportError(source, title, failureClass) {
  const before = `  } catch (error) {\n    logger.error(\n      "${title}",\n      {\n        error:\n          error instanceof Error ?\n            error.message :\n            String(error),\n      },\n    );`;
  const after = `  } catch {\n    logger.error(\n      "${title}",\n      {failureClass: "${failureClass}"},\n    );`;
  return replaceOne(source, before, after, title);
}

function scrubSteamRejected(source, title) {
  const before = `      "${title}",\n      {\n        httpStatus: response.status,\n        steamError:\n          payload.response?.error?.errordesc ??\n          "",\n      },`;
  const after = `      "${title}",\n      {\n        httpStatus: response.status,\n        failureClass: "steam_publisher_rejected",\n      },`;
  return replaceOne(source, before, after, title);
}

function updateSteamLink(source) {
  if (source.includes('failureClass: "steam_publisher_transport"') &&
      source.includes('failureClass: "steam_publisher_rejected"')) return source;
  source = scrubTransportError(source,
    'AtlasBoard Steam ticket verification request failed.',
    'steam_publisher_transport');
  source = scrubSteamRejected(source, 'AtlasBoard Steam ticket rejected.');
  for (const title of [
    'AtlasBoard verified Steam account link completed.',
    'AtlasBoard emulator Steam link proof completed.',
    'AtlasBoard returning Steam sign-in token issued.',
    'AtlasBoard emulator returning Steam sign-in issued.',
  ]) source = reduceSensitiveLogFields(source, title);
  return source;
}

function updateAppCheck(source) {
  if (source.includes('failureClass: "steam_app_check_transport"') &&
      source.includes('failureClass: "app_check_token_mint"')) return source;
  source = scrubTransportError(source,
    'AtlasBoard Steam App Check verification request failed.',
    'steam_app_check_transport');
  source = scrubSteamRejected(source,
    'AtlasBoard Steam App Check proof rejected.');
  const before = `    } catch (error) {\n      logger.error(\n        "AtlasBoard could not mint a custom App Check token.",\n        {\n          error:\n            error instanceof Error ?\n              error.message :\n              String(error),\n        },\n      );`;
  const after = `    } catch {\n      logger.error(\n        "AtlasBoard could not mint a custom App Check token.",\n        {failureClass: "app_check_token_mint"},\n      );`;
  return replaceOne(source, before, after, 'App Check token mint error');
}

function updateFirebase(source) {
  const conf = JSON.parse(source);
  for (const name of ['auth', 'firestore', 'functions']) {
    if (!conf.emulators?.[name]) throw new Error(`Missing emulator configuration for ${name}`);
    conf.emulators[name].host = '127.0.0.1';
  }
  return JSON.stringify(conf, null, 2) + '\n';
}

function updateGitignore(source) {
  if (source.includes('# Phase 15J: local credentials')) return source;
  return source.replace(/\s*$/, '\n') + `\n# Phase 15J: local credentials and publisher secrets must never be committed.\n.env.*\n!.env.example\n!.env.*.example\n**/*service-account*.json\n**/*serviceAccount*.json\n**/*credentials*.json\n**/*.pem\n**/*.p12\n**/*.key\n`;
}

const updates = [
  ['functions/src/platform/steam-link.ts', updateSteamLink],
  ['functions/src/platform/app-check-steam.ts', updateAppCheck],
  ['firebase.json', updateFirebase],
  ['.gitignore', updateGitignore],
];
const plans = [];

try {
  for (const [path, transform] of updates) {
    const abs = resolve(firebaseDir, path);
    const original = readFileSync(abs, 'utf8');
    const lineEnding = original.includes('\r\n') ? '\r\n' : '\n';
    const normalized = original.replace(/\r\n/g, '\n');
    const desired = transform(normalized);
    // Idempotent repeat is allowed if an installed Phase 15J source is unchanged.
    if (normalized === desired) {
      console.log(`ALREADY PATCHED ${path}`);
      continue;
    }
    if (blobSha(normalized) !== expected.get(path)) {
      throw new Error(`${path} is not the verified 15F-H GitHub baseline. ` +
        'Preserve local work and review the file manually before applying 15J.');
    }
    plans.push({path, abs, original, desired: desired.replace(/\n/g, lineEnding)});
    console.log(`READY ${path}`);
  }
  if (process.argv.includes('--dry-run')) {
    console.log(`Phase 15J dry-run PASS: ${plans.length} guarded replacements proposed.`);
    process.exit(0);
  }
  const completed = [];
  try {
    for (const plan of plans) {
      const temp = `${plan.abs}.phase15j-temp`;
      writeFileSync(temp, plan.desired, 'utf8');
      renameSync(temp, plan.abs);
      completed.push(plan);
      console.log(`PATCHED ${plan.path}`);
    }
  } catch (error) {
    for (const plan of completed.reverse()) writeFileSync(plan.abs, plan.original, 'utf8');
    throw error;
  }
  console.log('Phase 15J guarded application PASS. No deployment or Git mutation.');
} catch (error) {
  for (const plan of plans) {
    try { unlinkSync(`${plan.abs}.phase15j-temp`); } catch { /* no temp */ }
  }
  console.error('Phase 15J application FAILED:', error.message);
  process.exitCode = 1;
}
