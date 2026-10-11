/**
 * Guarded test-only repair for historical Steam Phase 11C/11D E2Es.
 * These tests used unsigned Firestore REST to inspect server-only mappings;
 * Phase 15A deny-by-default Firestore rules correctly block those reads.
 * Keep production rules unchanged; read fixtures via emulator-only Admin SDK.
 */
import {readFileSync, writeFileSync} from "node:fs";
import {dirname, resolve} from "node:path";
import {fileURLToPath} from "node:url";

const scriptsDir = dirname(fileURLToPath(import.meta.url));
const importLine = 'import {getEmulatorDoc, deleteEmulatorDoc} from "./phase15k-emulator-docs.mjs";';
const specs = [
  {
    file: "platform-steam-link-e2e.mjs",
    old: [
      [
        'async function getDoc(path) {\n  return requestJson(docUrl(path));\n}',
        'async function getDoc(path) {\n  return getEmulatorDoc(path);\n}',
      ],
      [
        'async function remove(path) {\n  await requestJson(\n    docUrl(path),\n    {method: "DELETE"},\n  );\n}',
        'async function remove(path) {\n  await deleteEmulatorDoc(path);\n}',
      ],
    ],
  },
  {
    file: "platform-steam-returning-signin-e2e.mjs",
    old: [
      [
        'async function remove(path) {\n  await requestJson(\n    docUrl(path),\n    {method: "DELETE"},\n  );\n}',
        'async function remove(path) {\n  await deleteEmulatorDoc(path);\n}',
      ],
      [
        'const providerDoc =\n  await requestJson(\n    docUrl(\n      `platform_identity_links/steam_${steamId}`,\n    ),\n  );',
        'const providerDoc =\n  await getEmulatorDoc(\n    `platform_identity_links/steam_${steamId}`,\n  );',
      ],
    ],
  },
];

function once(haystack, needle, label) {
  if (!haystack.includes(needle) || haystack.indexOf(needle) !== haystack.lastIndexOf(needle)) {
    throw new Error(`${label}: expected exact unique source anchor; refusing to overwrite.`);
  }
}

export function patchScript(input, spec) {
  const unix = input.replace(/\r\n/g, "\n");
  const allNew = spec.old.every(([_, added]) => unix.includes(added));
  if (unix.includes(importLine) && allNew) return unix;
  if (unix.includes(importLine) || spec.old.some(([_, added]) => unix.includes(added))) {
    throw new Error(`${spec.file}: partial or unexpected changes; refusing to patch.`);
  }
  // Avoid silently modifying a previously patched or drifting E2E script.
  for (const [from] of spec.old) once(unix, from, spec.file);
  let out = unix;
  for (const [from, to] of spec.old) out = out.replace(from, to);
  const importAnchor = spec.file.includes("steam-link") ?
    'import {createHash} from "node:crypto";' :
    'const PROJECT_ID = "atlasboard-usa";';
  once(out, importAnchor, spec.file + " import");
  out = out.replace(importAnchor, spec.file.includes("steam-link") ?
    `${importAnchor}\n${importLine}` : `${importLine}\n\n${importAnchor}`);
  return out;
}

function main() {
  const dry = process.argv.includes("--dry-run");
  const plans = [];
  for (const spec of specs) {
    const full = resolve(scriptsDir, spec.file);
    const original = readFileSync(full, "utf8");
    const updated = patchScript(original, spec);
    if (updated === original.replace(/\r\n/g, "\n")) {
      console.log(`ALREADY FIXED ${spec.file}`);
      continue;
    }
    plans.push({full, original, updated: original.includes("\r\n") ?
      updated.replace(/\n/g, "\r\n") : updated, spec});
    console.log(`READY ${spec.file}`);
  }
  if (dry) {
    console.log(`Phase 15K Steam E2E fix dry-run PASS: ${plans.length} files.`);
    return;
  }
  try {
    for (const p of plans) {
      writeFileSync(p.full, p.updated, "utf8");
      console.log(`PATCHED ${p.spec.file}`);
    }
  } catch (e) {
    for (const p of plans) writeFileSync(p.full, p.original, "utf8");
    throw e;
  }
  console.log("Phase 15K Steam E2E compatibility fix PASS. Server/Firestore rules unchanged.");
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {main();} catch (e) {
    console.error("Phase 15K Steam E2E fix FAILED:", e.message);
    process.exitCode = 1;
  }
}
