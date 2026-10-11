import assert from "node:assert/strict";
import {patchScript} from "./phase15k-steam-e2e-fix.mjs";

const base = [
  'import {createHash} from "node:crypto";',
  'async function getDoc(path) {\n  return requestJson(docUrl(path));\n}',
  'async function remove(path) {\n  await requestJson(\n    docUrl(path),\n    {method: "DELETE"},\n  );\n}',
].join("\n");
const returns = [
  'const PROJECT_ID = "atlasboard-usa";',
  'async function remove(path) {\n  await requestJson(\n    docUrl(path),\n    {method: "DELETE"},\n  );\n}',
  'const providerDoc =\n  await requestJson(\n    docUrl(\n      `platform_identity_links/steam_${steamId}`,\n    ),\n  );',
].join("\n");
const specs = [
  {file: "platform-steam-link-e2e.mjs", old: [
    ['async function getDoc(path) {\n  return requestJson(docUrl(path));\n}', 'async function getDoc(path) {\n  return getEmulatorDoc(path);\n}'],
    ['async function remove(path) {\n  await requestJson(\n    docUrl(path),\n    {method: "DELETE"},\n  );\n}', 'async function remove(path) {\n  await deleteEmulatorDoc(path);\n}'],
  ]},
  {file: "platform-steam-returning-signin-e2e.mjs", old: [
    ['async function remove(path) {\n  await requestJson(\n    docUrl(path),\n    {method: "DELETE"},\n  );\n}', 'async function remove(path) {\n  await deleteEmulatorDoc(path);\n}'],
    ['const providerDoc =\n  await requestJson(\n    docUrl(\n      `platform_identity_links/steam_${steamId}`,\n    ),\n  );', 'const providerDoc =\n  await getEmulatorDoc(\n    `platform_identity_links/steam_${steamId}`,\n  );'],
  ]},
];
for (const [input, spec] of [[base,specs[0]],[returns,specs[1]]]) {
  const out = patchScript(input,spec);
  assert(out.includes('import {getEmulatorDoc, deleteEmulatorDoc}'));
  assert(out.includes('getEmulatorDoc('));
  assert(out.includes('deleteEmulatorDoc('));
  assert.equal(patchScript(out,spec),out);
  assert.throws(() => patchScript(input.replace('await requestJson(', 'await other('),spec),/anchor|partial/);
}
console.log('Phase 15K Steam E2E fix static unit PASS: both historical scripts, idempotence, source drift.');
