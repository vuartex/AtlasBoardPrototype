const PROJECT_ID = "atlasboard-usa";
const REGION = "europe-west1";
const AUTH_BASE = "http://127.0.0.1:9099";
const FUNCTIONS_BASE = "http://127.0.0.1:5001";
const API_KEY = "atlasboard-local-emulator-only";
const TIMEOUT_MS = 10000;

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

async function postJson(url, body, headers = {}) {
  const response = await fetch(url, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...headers,
    },
    body: JSON.stringify(body),
    signal: AbortSignal.timeout(TIMEOUT_MS),
  });

  const text = await response.text();
  let json = null;

  if (text) {
    try {
      json = JSON.parse(text);
    } catch {
      json = {raw: text};
    }
  }

  return {response, json, text};
}

const nonce = `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`;
const email = `atlasboard.local.e2e.${nonce}@example.com`;
const password = `AtlasBoardLocalE2E!${nonce}`;
let idToken = null;
let localId = null;

console.log("AtlasBoard Backend Local E2E v1");
console.log("Safety: localhost emulators only; no production Firestore/economy writes.");

try {
  console.log("[1/4] Creating temporary user through Auth Emulator REST API...");

  const signUpUrl =
    `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=${API_KEY}`;

  const signUp = await postJson(signUpUrl, {
    email,
    password,
    returnSecureToken: true,
  });

  assert(
    signUp.response.ok,
    `Auth Emulator sign-up failed: HTTP ${signUp.response.status} ${signUp.text}`,
  );

  idToken = signUp.json?.idToken;
  localId = signUp.json?.localId;

  assert(typeof idToken === "string" && idToken.length > 20,
    "Auth Emulator returned no valid idToken.");
  assert(typeof localId === "string" && localId.length > 0,
    "Auth Emulator returned no localId.");

  console.log(`[1/4] PASSED. Local UID=${localId}`);

  console.log("[2/4] Calling authenticated economyHealthCheck on Functions Emulator...");

  const callableUrl =
    `${FUNCTIONS_BASE}/${PROJECT_ID}/${REGION}/economyHealthCheck`;

  const callable = await postJson(
    callableUrl,
    {
      data: {
        probe: "backend-local-e2e",
        client: "node22",
      },
    },
    {
      Authorization: `Bearer ${idToken}`,
    },
  );

  assert(
    callable.response.ok,
    `Functions Emulator call failed: HTTP ${callable.response.status} ${callable.text}`,
  );

  const result = callable.json?.result ?? callable.json?.data;
  assert(result && typeof result === "object",
    `Callable response did not contain result/data: ${callable.text}`);

  console.log("[2/4] PASSED. Functions Emulator returned a callable response.");

  console.log("[3/4] Verifying authenticated backend response...");

  assert(result.ok === true, "Backend returned ok != true.");
  assert(result.authenticated === true,
    "Backend returned authenticated != true.");
  assert(result.accountId === localId,
    `UID mismatch. Expected ${localId}, got ${result.accountId}`);
  assert(result.projectId === PROJECT_ID,
    `Project mismatch. Expected ${PROJECT_ID}, got ${result.projectId}`);
  assert(result.region === REGION,
    `Region mismatch. Expected ${REGION}, got ${result.region}`);
  assert(result.backendSchemaVersion === 1,
    `Backend schema mismatch: ${result.backendSchemaVersion}`);
  assert(result.protocolVersion === 1,
    `Protocol mismatch: ${result.protocolVersion}`);
  assert(result.service === "economy",
    `Service mismatch: ${result.service}`);
  assert(result.mode === "emulator",
    `Expected emulator mode, got ${result.mode}`);

  console.log("[3/4] PASSED. UID/project/region/schema/protocol were verified.");

  console.log("[4/4] Deleting temporary Auth Emulator user...");

  const deleteUrl =
    `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/accounts:delete?key=${API_KEY}`;

  const deletion = await postJson(deleteUrl, {idToken});

  assert(
    deletion.response.ok,
    `Temporary user cleanup failed: HTTP ${deletion.response.status} ${deletion.text}`,
  );

  console.log("[4/4] PASSED. Temporary local user deleted.");
  console.log("");
  console.log("AtlasBoard Backend Local E2E v1 PASSED.");
  console.log("Verified: Auth Emulator -> ID token -> authenticated Callable Functions Emulator -> validated response.");
  process.exitCode = 0;
} catch (error) {
  console.error("");
  console.error("AtlasBoard Backend Local E2E v1 FAILED.");
  console.error(error instanceof Error ? error.stack ?? error.message : error);
  console.error("");
  console.error("Do not mark Phase 3C.4A Runtime/Local E2E as passed.");
  process.exitCode = 1;
}
