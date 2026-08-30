import {createHash} from "node:crypto";

const PROJECT_ID = "atlasboard-usa";
const REGION = "europe-west1";
const AUTH_BASE = "http://127.0.0.1:9099";
const FUNCTIONS_BASE = "http://127.0.0.1:5001";
const FIRESTORE_BASE = "http://127.0.0.1:8080";
const HUB_BASE = "http://127.0.0.1:4400";
const API_KEY = "atlasboard-local-emulator-only";
const TIMEOUT_MS = 10000;

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

async function requestJson(url, options = {}) {
  const response = await fetch(url, {
    ...options,
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

async function postJson(url, body, headers = {}) {
  return requestJson(url, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...headers,
    },
    body: JSON.stringify(body),
  });
}

async function deleteUrl(url) {
  return requestJson(url, {method: "DELETE"});
}

function callableUrl(functionName) {
  return `${FUNCTIONS_BASE}/${PROJECT_ID}/${REGION}/${functionName}`;
}

async function callFunction(functionName, idToken, data) {
  return postJson(
    callableUrl(functionName),
    {data},
    {Authorization: `Bearer ${idToken}`},
  );
}

function getCallableResult(callable) {
  return callable.json?.result ?? callable.json?.data;
}

function firestoreDocumentUrl(path) {
  return `${FIRESTORE_BASE}/v1/projects/${PROJECT_ID}` +
    `/databases/(default)/documents/${path}`;
}

function ledgerEntryId(uid, idempotencyKey) {
  return createHash("sha256")
    .update(`${uid}:${idempotencyKey}`, "utf8")
    .digest("hex");
}

function firestoreString(document, fieldName) {
  return document?.fields?.[fieldName]?.stringValue;
}

function firestoreInteger(document, fieldName) {
  const value = document?.fields?.[fieldName]?.integerValue;
  return value === undefined ? undefined : Number.parseInt(value, 10);
}

async function getFirestoreDocument(path) {
  return requestJson(firestoreDocumentUrl(path));
}

async function bestEffortDeleteFirestore(path) {
  try {
    const deletion = await deleteUrl(firestoreDocumentUrl(path));
    if (!deletion.response.ok && deletion.response.status !== 404) {
      console.warn(
        `Cleanup warning for ${path}: HTTP ${deletion.response.status}`,
      );
    }
  } catch (error) {
    console.warn(`Cleanup warning for ${path}:`, error);
  }
}

async function verifyEmulatorPreflight() {
  const hub = await requestJson(`${HUB_BASE}/emulators`);

  assert(
    hub.response.ok,
    "Emulator Hub is not reachable on 127.0.0.1:4400.",
  );

  const emulators = hub.json ?? {};
  const required = ["auth", "functions", "firestore"];

  for (const name of required) {
    assert(
      emulators[name],
      `Required emulator is not running: ${name}. ` +
      "Start auth,firestore,functions together before this test.",
    );
  }

  const firestoreHost = `${emulators.firestore.host}:${emulators.firestore.port}`;
  assert(
    firestoreHost === "127.0.0.1:8080" ||
      firestoreHost === "localhost:8080",
    `Unexpected Firestore Emulator address: ${firestoreHost}`,
  );
}

const nonce = `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`;
const email = `atlasboard.wallet.e2e.${nonce}@example.com`;
const password = `AtlasBoardWalletE2E!${nonce}`;
const grantKey = `wallet-e2e-grant-${nonce}`;
const debitKey = `wallet-e2e-debit-${nonce}`;
const overdrawKey = `wallet-e2e-overdraw-${nonce}`;
const grantTransactionId = `wallet-e2e-grant-tx-${nonce}`;
const debitTransactionId = `wallet-e2e-debit-tx-${nonce}`;
const overdrawTransactionId = `wallet-e2e-overdraw-tx-${nonce}`;

let idToken = null;
let localId = null;
let grantLedgerId = null;
let debitLedgerId = null;
let overdrawLedgerId = null;
let testPassed = false;

console.log("AtlasBoard Wallet + Immutable Ledger Local E2E v1");
console.log(
  "Safety: requires local Auth + Firestore + Functions emulators. " +
  "The backend refuses wallet mutation without FIRESTORE_EMULATOR_HOST.",
);

try {
  console.log("[0/8] Verifying emulator safety preflight...");
  await verifyEmulatorPreflight();
  console.log("[0/8] PASSED. Auth/Firestore/Functions emulators detected.");

  console.log("[1/8] Creating temporary Auth Emulator user...");
  const signUp = await postJson(
    `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=${API_KEY}`,
    {
      email,
      password,
      returnSecureToken: true,
    },
  );

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

  grantLedgerId = ledgerEntryId(localId, grantKey);
  debitLedgerId = ledgerEntryId(localId, debitKey);
  overdrawLedgerId = ledgerEntryId(localId, overdrawKey);
  console.log(`[1/8] PASSED. Local UID=${localId}`);

  console.log("[2/8] Applying +500 Gold grant through authenticated backend...");
  const grant = await callFunction("walletTestMutation", idToken, {
    currencyId: "gold",
    delta: 500,
    reason: "test_grant",
    transactionId: grantTransactionId,
    idempotencyKey: grantKey,
  });

  assert(
    grant.response.ok,
    `Grant failed: HTTP ${grant.response.status} ${grant.text}`,
  );

  const grantResult = getCallableResult(grant);
  assert(grantResult?.ok === true, "Grant returned ok != true.");
  assert(grantResult?.applied === true, "Grant was not applied.");
  assert(grantResult?.idempotentReplay === false,
    "First grant was incorrectly marked as replay.");
  assert(grantResult?.balanceBefore === 0,
    `Grant balanceBefore expected 0, got ${grantResult?.balanceBefore}`);
  assert(grantResult?.balanceAfter === 500,
    `Grant balanceAfter expected 500, got ${grantResult?.balanceAfter}`);
  assert(grantResult?.ledgerEntryId === grantLedgerId,
    "Grant ledger ID mismatch.");
  console.log("[2/8] PASSED. Gold 0 -> 500 and ledger entry created.");

  console.log("[3/8] Replaying the exact same idempotency key...");
  const replay = await callFunction("walletTestMutation", idToken, {
    currencyId: "gold",
    delta: 500,
    reason: "test_grant",
    transactionId: grantTransactionId,
    idempotencyKey: grantKey,
  });

  assert(
    replay.response.ok,
    `Idempotency replay failed: HTTP ${replay.response.status} ${replay.text}`,
  );

  const replayResult = getCallableResult(replay);
  assert(replayResult?.applied === false,
    "Replay incorrectly applied a second mutation.");
  assert(replayResult?.idempotentReplay === true,
    "Replay was not identified as idempotent.");
  assert(replayResult?.balanceAfter === 500,
    `Replay changed balance: ${replayResult?.balanceAfter}`);
  assert(replayResult?.ledgerEntryId === grantLedgerId,
    "Replay did not resolve to the original ledger entry.");
  console.log("[3/8] PASSED. Duplicate grant did not add Gold twice.");

  console.log("[4/8] Applying -125 Gold debit through authenticated backend...");
  const debit = await callFunction("walletTestMutation", idToken, {
    currencyId: "gold",
    delta: -125,
    reason: "test_debit",
    transactionId: debitTransactionId,
    idempotencyKey: debitKey,
  });

  assert(
    debit.response.ok,
    `Debit failed: HTTP ${debit.response.status} ${debit.text}`,
  );

  const debitResult = getCallableResult(debit);
  assert(debitResult?.applied === true, "Debit was not applied.");
  assert(debitResult?.balanceBefore === 500,
    `Debit balanceBefore expected 500, got ${debitResult?.balanceBefore}`);
  assert(debitResult?.balanceAfter === 375,
    `Debit balanceAfter expected 375, got ${debitResult?.balanceAfter}`);
  console.log("[4/8] PASSED. Gold 500 -> 375 and debit ledger created.");

  console.log("[5/8] Verifying insufficient-funds protection...");
  const overdraw = await callFunction("walletTestMutation", idToken, {
    currencyId: "gold",
    delta: -1000,
    reason: "test_overdraw",
    transactionId: overdrawTransactionId,
    idempotencyKey: overdrawKey,
  });

  const errorStatus = overdraw.json?.error?.status;
  const errorKey = overdraw.json?.error?.details?.errorKey;
  assert(!overdraw.response.ok,
    "Overdraw unexpectedly returned a successful HTTP response.");
  assert(errorStatus === "FAILED_PRECONDITION",
    `Expected FAILED_PRECONDITION, got ${errorStatus}. Body=${overdraw.text}`);
  assert(errorKey === "economy.error.insufficient_funds",
    `Unexpected overdraw errorKey: ${errorKey}`);
  console.log("[5/8] PASSED. -1000 debit rejected as insufficient funds.");

  console.log("[6/8] Reading Firestore Emulator documents directly...");
  const balanceDoc = await getFirestoreDocument(
    `wallets/${localId}/balances/gold`,
  );
  assert(balanceDoc.response.ok,
    `Gold balance document missing: ${balanceDoc.text}`);
  assert(firestoreString(balanceDoc.json, "uid") === localId,
    "Balance document UID mismatch.");
  assert(firestoreString(balanceDoc.json, "currencyId") === "gold",
    "Balance document currency mismatch.");
  assert(firestoreInteger(balanceDoc.json, "amount") === 375,
    `Persisted Gold expected 375, got ${firestoreInteger(balanceDoc.json, "amount")}`);

  const grantLedger = await getFirestoreDocument(
    `wallet_ledger/${grantLedgerId}`,
  );
  assert(grantLedger.response.ok, "Grant ledger document is missing.");
  assert(firestoreInteger(grantLedger.json, "delta") === 500,
    "Grant ledger delta mismatch.");
  assert(firestoreInteger(grantLedger.json, "balanceBefore") === 0,
    "Grant ledger balanceBefore mismatch.");
  assert(firestoreInteger(grantLedger.json, "balanceAfter") === 500,
    "Grant ledger balanceAfter mismatch.");
  assert(firestoreString(grantLedger.json, "transactionId") ===
    grantTransactionId, "Grant ledger transactionId mismatch.");

  const debitLedger = await getFirestoreDocument(
    `wallet_ledger/${debitLedgerId}`,
  );
  assert(debitLedger.response.ok, "Debit ledger document is missing.");
  assert(firestoreInteger(debitLedger.json, "delta") === -125,
    "Debit ledger delta mismatch.");
  assert(firestoreInteger(debitLedger.json, "balanceBefore") === 500,
    "Debit ledger balanceBefore mismatch.");
  assert(firestoreInteger(debitLedger.json, "balanceAfter") === 375,
    "Debit ledger balanceAfter mismatch.");

  const rejectedLedger = await getFirestoreDocument(
    `wallet_ledger/${overdrawLedgerId}`,
  );
  assert(rejectedLedger.response.status === 404,
    "Rejected overdraw unexpectedly created a ledger document.");

  const atlasCoinDoc = await getFirestoreDocument(
    `wallets/${localId}/balances/atlas_coin`,
  );
  assert(atlasCoinDoc.response.status === 404,
    "Gold test unexpectedly mutated Atlas Coin balance.");

  console.log(
    "[6/8] PASSED. Balance and immutable ledger data verified directly " +
    "in Firestore Emulator.",
  );

  console.log("[7/8] Verifying final balance after rejected overdraw...");
  const finalBalance = await getFirestoreDocument(
    `wallets/${localId}/balances/gold`,
  );
  assert(finalBalance.response.ok, "Final Gold balance document missing.");
  assert(firestoreInteger(finalBalance.json, "amount") === 375,
    "Rejected overdraw changed the final Gold balance.");
  console.log("[7/8] PASSED. Final Gold balance remains 375.");

  testPassed = true;
} catch (error) {
  console.error("");
  console.error("AtlasBoard Wallet + Immutable Ledger Local E2E v1 FAILED.");
  console.error(error instanceof Error ? error.stack ?? error.message : error);
  process.exitCode = 1;
} finally {
  console.log("[8/8] Cleaning temporary emulator data...");

  if (localId) {
    await bestEffortDeleteFirestore(`wallets/${localId}/balances/gold`);
  }
  if (grantLedgerId) {
    await bestEffortDeleteFirestore(`wallet_ledger/${grantLedgerId}`);
  }
  if (debitLedgerId) {
    await bestEffortDeleteFirestore(`wallet_ledger/${debitLedgerId}`);
  }
  if (overdrawLedgerId) {
    await bestEffortDeleteFirestore(`wallet_ledger/${overdrawLedgerId}`);
  }

  if (idToken) {
    try {
      const deletion = await postJson(
        `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/accounts:delete?key=${API_KEY}`,
        {idToken},
      );
      if (!deletion.response.ok) {
        console.warn(
          `Auth cleanup warning: HTTP ${deletion.response.status} ${deletion.text}`,
        );
      }
    } catch (error) {
      console.warn("Auth cleanup warning:", error);
    }
  }

  console.log("[8/8] Cleanup finished.");
}

if (testPassed) {
  console.log("");
  console.log("AtlasBoard Wallet + Immutable Ledger Local E2E v1 PASSED.");
  console.log(
    "Verified: authenticated backend mutation -> atomic Firestore balance + " +
    "ledger -> idempotency -> debit -> overdraft rejection -> direct data " +
    "verification -> cleanup.",
  );
  process.exitCode = 0;
} else {
  console.error("");
  console.error("Do not mark Phase 3C.4B Local E2E as passed.");
}
