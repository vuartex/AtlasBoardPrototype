import {spawnSync} from "node:child_process";
import {dirname, resolve} from "node:path";
import {fileURLToPath} from "node:url";

const functionsDir = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const firebaseDir = resolve(functionsDir, "..");
const windows = process.platform === "win32";
const npm = windows ? "npm.cmd" : "npm";
const npx = windows ? "npx.cmd" : "npx";

function run(label, executable, args, cwd) {
  console.log(`\nPhase 15I | ${label}`);
  const result = spawnSync(executable, args, {
    cwd,
    stdio: "inherit",
    shell: windows,
    env: process.env,
  });
  if (result.error) throw result.error;
  if (result.status !== 0) {
    throw new Error(`${label} failed (exit ${result.status})`);
  }
}

try {
  run("TypeScript build", npm, ["run", "build"], functionsDir);
  run("Focused chat ESLint", npx, ["eslint", "src/chat/chat.ts"],
    functionsDir);
  run("Moderation static audit", "node",
    ["scripts/moderation-security-audit.mjs"], functionsDir);
  // emulators:exec starts NEW emulators after the build, guaranteeing
  // that Functions is not serving a stale compiled copy from another run.
  // Shut down any existing emulator instance before invoking this command.
  const e2eCommand = "node functions/scripts/chat-moderation-e2e.mjs" +
    " && node functions/scripts/security-rules-e2e.mjs";
  // spawnSync({shell: true}) on Windows does not automatically preserve
  // an argument containing spaces or cmd.exe's && operator. Without explicit
  // quotation Firebase receives several positional args and exits with
  // "Too many arguments" before the emulators or tests can start.
  // POSIX uses shell: false and must receive the unquoted argument.
  const firebaseScriptArg = windows ? `"${e2eCommand}"` : e2eCommand;
  run("Fresh-emulator moderation and security-rules E2E", npx, [
    "firebase", "emulators:exec", "--project", "atlasboard-usa",
    "--only", "auth,firestore,functions", firebaseScriptArg,
  ], firebaseDir);
  console.log("\nPhase 15I combined validation PASS (local/emulator only).");
} catch (error) {
  console.error("\nPhase 15I combined validation FAILED:", error.message);
  process.exitCode = 1;
}
