import {spawn} from "node:child_process";
import {fileURLToPath} from "node:url";
import {dirname, join} from "node:path";

const HUB_BASE = "http://127.0.0.1:4400";
const TIMEOUT_MS = 10000;

const currentFile = fileURLToPath(import.meta.url);
const scriptsDir = dirname(currentFile);

const tests = [
  ["1/6", "Secure Backend Health Check", "economy-health-check-e2e.mjs"],
  ["2/6", "Wallet + Immutable Ledger", "wallet-ledger-e2e.mjs"],
  ["3/6", "Inventory + Entitlements", "inventory-entitlements-e2e.mjs"],
  ["4/6", "Commerce + Purchase History", "commerce-purchase-history-e2e.mjs"],
  ["5/6", "Promo Codes + Event Tickets", "promo-event-tickets-e2e.mjs"],
  ["6/6", "Integrated Economy Flow", "economy-integrated-flow-e2e.mjs"],
];

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

async function verifyEmulators() {
  const response = await fetch(`${HUB_BASE}/emulators`, {
    signal: AbortSignal.timeout(TIMEOUT_MS),
  });

  assert(response.ok, "Firebase Emulator Hub is not reachable on port 4400.");
  const emulators = await response.json();

  for (const name of ["auth", "functions", "firestore"]) {
    assert(
      emulators[name],
      `Required emulator is not running: ${name}.`,
    );
  }
}

function runScript(scriptName) {
  return new Promise((resolve, reject) => {
    const fullPath = join(scriptsDir, scriptName);
    const startedAt = Date.now();

    const child = spawn(
      process.execPath,
      [fullPath],
      {
        cwd: dirname(scriptsDir),
        stdio: "inherit",
        windowsHide: false,
      },
    );

    child.on("error", reject);

    child.on("exit", (code, signal) => {
      const durationMs = Date.now() - startedAt;

      if (code === 0) {
        resolve(durationMs);
        return;
      }

      reject(
        new Error(
          `${scriptName} failed with code=${code}, signal=${signal}.`,
        ),
      );
    });
  });
}

console.log("AtlasBoard Economy Dev Validation Suite v1");
console.log(
  "Runs every existing backend Local E2E plus one integrated cross-module " +
  "transaction flow. Local emulators only.",
);

try {
  console.log("[preflight] Verifying Auth/Functions/Firestore emulators...");
  await verifyEmulators();
  console.log("[preflight] PASSED.");

  const results = [];

  for (const [step, label, script] of tests) {
    console.log(`\n[${step}] START ${label}`);
    const durationMs = await runScript(script);
    results.push({label, durationMs});
    console.log(
      `[${step}] PASSED ${label} (${(durationMs / 1000).toFixed(2)}s)`,
    );
  }

  console.log("\nValidation summary:");
  for (const result of results) {
    console.log(
      `  PASS  ${result.label}  ${(result.durationMs / 1000).toFixed(2)}s`,
    );
  }

  console.log(
    "\nAtlasBoard Economy Dev Validation Suite v1 PASSED.",
  );
  console.log(
    "Verified all Phase 3C.4A-F local economy validation paths in one " +
    "repeatable harness.",
  );
} catch (error) {
  console.error("\nAtlasBoard Economy Dev Validation Suite v1 FAILED.");
  console.error(error);
  process.exitCode = 1;
}
