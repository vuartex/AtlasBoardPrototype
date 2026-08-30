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

async function patchJson(url, body) {
  return requestJson(url, {
    method: "PATCH",
    headers: {
      "Content-Type": "application/json",
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

async function getFirestoreDocument(path) {
  return requestJson(firestoreDocumentUrl(path));
}

async function patchFirestoreDocument(path, fields) {
  return patchJson(
    firestoreDocumentUrl(path),
    {fields},
  );
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

function firestoreString(document, fieldName) {
  return document?.fields?.[fieldName]?.stringValue;
}

function firestoreBoolean(document, fieldName) {
  return document?.fields?.[fieldName]?.booleanValue;
}

function firestoreInteger(document, fieldName) {
  const value = document?.fields?.[fieldName]?.integerValue;
  return value === undefined ? undefined : Number.parseInt(value, 10);
}

function sha256(value) {
  return createHash("sha256")
    .update(value, "utf8")
    .digest("hex");
}

function commerceTransactionId(uid, idempotencyKey) {
  return sha256(`${uid}:commerce_purchase:${idempotencyKey}`);
}

function commerceRefundEventId(uid, transactionId, idempotencyKey) {
  return sha256(
    `${uid}:${transactionId}:refund:${idempotencyKey}`,
  );
}

function walletLedgerId(uid, idempotencyKey) {
  return sha256(`${uid}:${idempotencyKey}`);
}

function inventoryEventId(uid, itemId, idempotencyKey) {
  return sha256(`${uid}:${itemId}:${idempotencyKey}`);
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
      `Required emulator is not running: ${name}.`,
    );
  }

  const firestoreHost =
    `${emulators.firestore.host}:${emulators.firestore.port}`;

  assert(
    firestoreHost === "127.0.0.1:8080" ||
      firestoreHost === "localhost:8080",
    `Unexpected Firestore Emulator address: ${firestoreHost}`,
  );
}

const nonce = `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`;
const email = `atlasboard.commerce.e2e.${nonce}@example.com`;
const password = `AtlasBoardCommerceE2E!${nonce}`;

const itemId = `e2e_dice_${nonce}`;
const expensiveItemId = `e2e_board_${nonce}`;

const seedWalletTransactionId = `commerce-seed-wallet-${nonce}`;
const seedWalletKey = `commerce-seed-wallet-key-${nonce}`;

const purchaseKey = `commerce-purchase-${nonce}`;
const failedPurchaseKey = `commerce-failed-${nonce}`;
const refundKey = `commerce-refund-${nonce}`;

let idToken = null;
let localId = null;
let purchaseTransactionId = null;
let failedTransactionId = null;
let purchaseChargeLedgerId = null;
let purchaseGrantEventId = null;
let refundLedgerId = null;
let refundRevokeEventId = null;
let refundCommerceEventId = null;
let seedLedgerId = null;
let testPassed = false;

console.log("AtlasBoard Commerce + Purchase History Local E2E v1");
console.log(
  "Safety: localhost Auth/Firestore/Functions emulators only; " +
  "no production provider, receipt, wallet, inventory, or commerce data.",
);

try {
  console.log("[0/11] Verifying emulator safety preflight...");
  await verifyEmulatorPreflight();
  console.log("[0/11] PASSED. Auth/Firestore/Functions emulators detected.");

  console.log("[1/11] Creating temporary Auth Emulator user...");
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
    `Auth Emulator sign-up failed: HTTP ${signUp.response.status} ` +
    signUp.text,
  );

  idToken = signUp.json?.idToken;
  localId = signUp.json?.localId;

  assert(
    typeof idToken === "string" && idToken.length > 20,
    "Auth Emulator returned no valid idToken.",
  );
  assert(
    typeof localId === "string" && localId.length > 0,
    "Auth Emulator returned no localId.",
  );

  purchaseTransactionId = commerceTransactionId(localId, purchaseKey);
  failedTransactionId = commerceTransactionId(
    localId,
    failedPurchaseKey,
  );

  purchaseChargeLedgerId = walletLedgerId(
    localId,
    `commerce:${purchaseTransactionId}:charge`,
  );
  purchaseGrantEventId = inventoryEventId(
    localId,
    itemId,
    `commerce:${purchaseTransactionId}:grant`,
  );
  refundLedgerId = walletLedgerId(
    localId,
    `commerce:${purchaseTransactionId}:refund:${refundKey}`,
  );
  refundRevokeEventId = inventoryEventId(
    localId,
    itemId,
    `commerce:${purchaseTransactionId}:revoke:${refundKey}`,
  );
  refundCommerceEventId = commerceRefundEventId(
    localId,
    purchaseTransactionId,
    refundKey,
  );
  seedLedgerId = walletLedgerId(localId, seedWalletKey);

  console.log(`[1/11] PASSED. Local UID=${localId}`);

  console.log("[2/11] Seeding temporary catalog items...");
  const normalCatalog = await patchFirestoreDocument(
    `item_catalog/${itemId}`,
    {
      itemId: {stringValue: itemId},
      itemType: {stringValue: "dice_skin"},
      active: {booleanValue: true},
      prices: {
        mapValue: {
          fields: {
            gold: {integerValue: "200"},
          },
        },
      },
      schemaVersion: {integerValue: "1"},
    },
  );

  assert(
    normalCatalog.response.ok,
    `Normal catalog seed failed: ${normalCatalog.text}`,
  );

  const expensiveCatalog = await patchFirestoreDocument(
    `item_catalog/${expensiveItemId}`,
    {
      itemId: {stringValue: expensiveItemId},
      itemType: {stringValue: "board_theme"},
      active: {booleanValue: true},
      prices: {
        mapValue: {
          fields: {
            gold: {integerValue: "1000"},
          },
        },
      },
      schemaVersion: {integerValue: "1"},
    },
  );

  assert(
    expensiveCatalog.response.ok,
    `Expensive catalog seed failed: ${expensiveCatalog.text}`,
  );

  console.log("[2/11] PASSED. Catalog prices are 200 and 1000 Gold.");

  console.log("[3/11] Seeding +500 Gold through existing wallet backend...");
  const seedWallet = await callFunction(
    "walletTestMutation",
    idToken,
    {
      currencyId: "gold",
      delta: 500,
      reason: "commerce_test_seed",
      transactionId: seedWalletTransactionId,
      idempotencyKey: seedWalletKey,
    },
  );

  assert(
    seedWallet.response.ok,
    `Wallet seed failed: HTTP ${seedWallet.response.status} ` +
    seedWallet.text,
  );

  const seedResult = getCallableResult(seedWallet);
  assert(seedResult?.balanceAfter === 500,
    `Expected seeded Gold=500, got ${seedResult?.balanceAfter}`);

  console.log("[3/11] PASSED. Starting persistent wallet balance is 500 Gold.");

  console.log("[4/11] Buying 200-Gold item through commerce backend...");
  const purchase = await callFunction(
    "commerceTestPurchase",
    idToken,
    {
      itemId,
      paymentMethod: "gold",
      idempotencyKey: purchaseKey,
    },
  );

  assert(
    purchase.response.ok,
    `Purchase failed: HTTP ${purchase.response.status} ${purchase.text}`,
  );

  const purchaseResult = getCallableResult(purchase);

  assert(purchaseResult?.ok === true, "Purchase returned ok != true.");
  assert(purchaseResult?.applied === true, "Purchase was not applied.");
  assert(
    purchaseResult?.idempotentReplay === false,
    "First purchase was incorrectly marked as replay.",
  );
  assert(
    purchaseResult?.transactionId === purchaseTransactionId,
    "Deterministic commerce transaction ID mismatch.",
  );
  assert(purchaseResult?.status === "succeeded",
    "Purchase status is not succeeded.");
  assert(purchaseResult?.entitlementStatus === "granted",
    "Purchase entitlement status is not granted.");
  assert(purchaseResult?.amount === 200,
    `Expected amount=200, got ${purchaseResult?.amount}`);
  assert(purchaseResult?.balanceBefore === 500,
    "Purchase expected balanceBefore=500.");
  assert(purchaseResult?.balanceAfter === 300,
    "Purchase expected balanceAfter=300.");
  assert(
    purchaseResult?.chargeLedgerEntryId === purchaseChargeLedgerId,
    "Purchase charge ledger ID mismatch.",
  );
  assert(
    purchaseResult?.grantEventId === purchaseGrantEventId,
    "Purchase grant event ID mismatch.",
  );

  console.log(
    "[4/11] PASSED. 500 -> 300 Gold, ledger + entitlement + history committed.",
  );

  console.log("[5/11] Replaying exact purchase idempotency key...");
  const replay = await callFunction(
    "commerceTestPurchase",
    idToken,
    {
      itemId,
      paymentMethod: "gold",
      idempotencyKey: purchaseKey,
    },
  );

  assert(
    replay.response.ok,
    `Purchase replay failed: HTTP ${replay.response.status} ${replay.text}`,
  );

  const replayResult = getCallableResult(replay);
  assert(replayResult?.applied === false,
    "Purchase replay applied again.");
  assert(replayResult?.idempotentReplay === true,
    "Purchase replay not identified as idempotent.");
  assert(replayResult?.status === "succeeded",
    "Purchase replay changed transaction status.");

  const balanceAfterReplay = await getFirestoreDocument(
    `wallets/${localId}/balances/gold`,
  );
  assert(balanceAfterReplay.response.ok,
    "Gold balance missing after replay.");
  assert(firestoreInteger(balanceAfterReplay.json, "amount") === 300,
    "Exact purchase replay changed Gold balance.");

  console.log(
    "[5/11] PASSED. Duplicate callback/replay did not charge or grant twice.",
  );

  console.log("[6/11] Verifying succeeded purchase directly in Firestore...");
  const purchaseDoc = await getFirestoreDocument(
    `commerce_transactions/${purchaseTransactionId}`,
  );
  assert(purchaseDoc.response.ok,
    "Succeeded commerce transaction document is missing.");
  assert(firestoreString(purchaseDoc.json, "uid") === localId,
    "Commerce transaction owner UID mismatch.");
  assert(firestoreString(purchaseDoc.json, "status") === "succeeded",
    "Commerce transaction status mismatch.");
  assert(firestoreString(purchaseDoc.json, "paymentMethod") === "gold",
    "Commerce payment method mismatch.");
  assert(firestoreInteger(purchaseDoc.json, "amount") === 200,
    "Commerce amount mismatch.");
  assert(
    firestoreString(purchaseDoc.json, "entitlementStatus") === "granted",
    "Commerce entitlement status mismatch.",
  );

  const purchaseEvent = await getFirestoreDocument(
    `commerce_transactions/${purchaseTransactionId}/events/purchase`,
  );
  assert(purchaseEvent.response.ok,
    "Immutable purchase event is missing.");
  assert(
    firestoreString(purchaseEvent.json, "eventType") === "purchase_succeeded",
    "Purchase event type mismatch.",
  );

  const chargeLedger = await getFirestoreDocument(
    `wallet_ledger/${purchaseChargeLedgerId}`,
  );
  assert(chargeLedger.response.ok,
    "Commerce charge ledger is missing.");
  assert(firestoreInteger(chargeLedger.json, "delta") === -200,
    "Commerce charge ledger delta mismatch.");

  const ownedItem = await getFirestoreDocument(
    `inventories/${localId}/items/${itemId}`,
  );
  assert(ownedItem.response.ok,
    "Purchased inventory entitlement is missing.");
  assert(firestoreBoolean(ownedItem.json, "owned") === true,
    "Purchased item is not owned.");

  const grantEvent = await getFirestoreDocument(
    `inventories/${localId}/items/${itemId}/events/${purchaseGrantEventId}`,
  );
  assert(grantEvent.response.ok,
    "Commerce inventory grant event is missing.");

  console.log(
    "[6/11] PASSED. Purchase history, ledger, entitlement, and events verified.",
  );

  console.log("[7/11] Testing insufficient-funds purchase history...");
  const failedPurchase = await callFunction(
    "commerceTestPurchase",
    idToken,
    {
      itemId: expensiveItemId,
      paymentMethod: "gold",
      idempotencyKey: failedPurchaseKey,
    },
  );

  assert(!failedPurchase.response.ok,
    "Insufficient-funds purchase unexpectedly succeeded.");
  assert(
    failedPurchase.json?.error?.status === "FAILED_PRECONDITION",
    `Expected FAILED_PRECONDITION, got ` +
    `${failedPurchase.json?.error?.status}.`,
  );
  assert(
    failedPurchase.json?.error?.details?.errorKey ===
      "commerce.error.insufficient_funds",
    "Unexpected insufficient-funds errorKey.",
  );
  assert(
    failedPurchase.json?.error?.details?.transactionId ===
      failedTransactionId,
    "Failed purchase transactionId mismatch.",
  );

  const failedDoc = await getFirestoreDocument(
    `commerce_transactions/${failedTransactionId}`,
  );
  assert(failedDoc.response.ok,
    "Failed commerce attempt was not recorded.");
  assert(firestoreString(failedDoc.json, "status") === "failed",
    "Failed commerce history status mismatch.");
  assert(
    firestoreString(failedDoc.json, "failureReason") ===
      "insufficient_funds",
    "Failed commerce history reason mismatch.",
  );
  assert(
    firestoreString(failedDoc.json, "entitlementStatus") ===
      "not_granted",
    "Failed commerce entitlement status mismatch.",
  );

  const balanceAfterFailure = await getFirestoreDocument(
    `wallets/${localId}/balances/gold`,
  );
  assert(
    firestoreInteger(balanceAfterFailure.json, "amount") === 300,
    "Failed purchase changed wallet balance.",
  );

  const expensiveInventory = await getFirestoreDocument(
    `inventories/${localId}/items/${expensiveItemId}`,
  );
  assert(
    expensiveInventory.response.status === 404,
    "Failed purchase unexpectedly granted inventory.",
  );

  console.log(
    "[7/11] PASSED. Failure recorded; wallet and inventory stayed unchanged.",
  );

  console.log("[8/11] Refunding the successful purchase...");
  const refund = await callFunction(
    "commerceTestRefund",
    idToken,
    {
      transactionId: purchaseTransactionId,
      idempotencyKey: refundKey,
    },
  );

  assert(
    refund.response.ok,
    `Refund failed: HTTP ${refund.response.status} ${refund.text}`,
  );

  const refundResult = getCallableResult(refund);
  assert(refundResult?.applied === true,
    "Refund was not applied.");
  assert(refundResult?.idempotentReplay === false,
    "First refund incorrectly marked as replay.");
  assert(refundResult?.status === "refunded",
    "Refund status mismatch.");
  assert(refundResult?.entitlementStatus === "revoked",
    "Refund entitlement status mismatch.");
  assert(refundResult?.balanceBefore === 300,
    "Refund expected balanceBefore=300.");
  assert(refundResult?.balanceAfter === 500,
    "Refund expected balanceAfter=500.");
  assert(refundResult?.refundLedgerEntryId === refundLedgerId,
    "Refund ledger ID mismatch.");
  assert(refundResult?.revokeEventId === refundRevokeEventId,
    "Refund revoke event ID mismatch.");
  assert(refundResult?.commerceEventId === refundCommerceEventId,
    "Refund commerce event ID mismatch.");

  console.log(
    "[8/11] PASSED. Refund restored Gold and revoked entitlement atomically.",
  );

  console.log("[9/11] Replaying exact refund idempotency key...");
  const refundReplay = await callFunction(
    "commerceTestRefund",
    idToken,
    {
      transactionId: purchaseTransactionId,
      idempotencyKey: refundKey,
    },
  );

  assert(
    refundReplay.response.ok,
    `Refund replay failed: HTTP ${refundReplay.response.status} ` +
    refundReplay.text,
  );

  const refundReplayResult = getCallableResult(refundReplay);
  assert(refundReplayResult?.applied === false,
    "Refund replay applied again.");
  assert(refundReplayResult?.idempotentReplay === true,
    "Refund replay not identified as idempotent.");
  assert(refundReplayResult?.status === "refunded",
    "Refund replay changed final transaction status.");

  const balanceAfterRefundReplay = await getFirestoreDocument(
    `wallets/${localId}/balances/gold`,
  );
  assert(
    firestoreInteger(balanceAfterRefundReplay.json, "amount") === 500,
    "Refund replay credited Gold twice.",
  );

  console.log("[9/11] PASSED. Refund replay did not credit or revoke twice.");

  console.log("[10/11] Verifying final purchase history and linked records...");
  const refundedDoc = await getFirestoreDocument(
    `commerce_transactions/${purchaseTransactionId}`,
  );
  assert(refundedDoc.response.ok,
    "Refunded transaction document is missing.");
  assert(firestoreString(refundedDoc.json, "status") === "refunded",
    "Final commerce status is not refunded.");
  assert(
    firestoreString(refundedDoc.json, "entitlementStatus") === "revoked",
    "Final entitlement status is not revoked.",
  );
  assert(
    firestoreString(refundedDoc.json, "refundLedgerEntryId") ===
      refundLedgerId,
    "Transaction refund ledger reference mismatch.",
  );
  assert(
    firestoreString(refundedDoc.json, "revokeEventId") ===
      refundRevokeEventId,
    "Transaction revoke event reference mismatch.",
  );

  const refundEvent = await getFirestoreDocument(
    `commerce_transactions/${purchaseTransactionId}/events/` +
    refundCommerceEventId,
  );
  assert(refundEvent.response.ok,
    "Immutable refund commerce event is missing.");
  assert(
    firestoreString(refundEvent.json, "eventType") === "refund_succeeded",
    "Refund commerce event type mismatch.",
  );

  const refundLedger = await getFirestoreDocument(
    `wallet_ledger/${refundLedgerId}`,
  );
  assert(refundLedger.response.ok,
    "Refund wallet ledger is missing.");
  assert(firestoreInteger(refundLedger.json, "delta") === 200,
    "Refund ledger delta mismatch.");

  const finalInventory = await getFirestoreDocument(
    `inventories/${localId}/items/${itemId}`,
  );
  assert(finalInventory.response.ok,
    "Final inventory document is missing.");
  assert(firestoreBoolean(finalInventory.json, "owned") === false,
    "Refunded entitlement is still owned.");

  const revokeEvent = await getFirestoreDocument(
    `inventories/${localId}/items/${itemId}/events/${refundRevokeEventId}`,
  );
  assert(revokeEvent.response.ok,
    "Refund revoke entitlement event is missing.");

  console.log(
    "[10/11] PASSED. Purchase/refund history and linked immutable records verified.",
  );

  console.log("[11/11] Cleaning temporary emulator data...");
  testPassed = true;
} catch (error) {
  console.error("\nAtlasBoard Commerce + Purchase History Local E2E v1 FAILED.");
  console.error(error);
  process.exitCode = 1;
} finally {
  if (localId) {
    const paths = [
      refundCommerceEventId ?
        `commerce_transactions/${purchaseTransactionId}/events/` +
          refundCommerceEventId :
        null,
      purchaseTransactionId ?
        `commerce_transactions/${purchaseTransactionId}/events/purchase` :
        null,
      failedTransactionId ?
        `commerce_transactions/${failedTransactionId}/events/purchase` :
        null,
      purchaseTransactionId ?
        `commerce_transactions/${purchaseTransactionId}` :
        null,
      failedTransactionId ?
        `commerce_transactions/${failedTransactionId}` :
        null,
      refundRevokeEventId ?
        `inventories/${localId}/items/${itemId}/events/` +
          refundRevokeEventId :
        null,
      purchaseGrantEventId ?
        `inventories/${localId}/items/${itemId}/events/` +
          purchaseGrantEventId :
        null,
      `inventories/${localId}/items/${itemId}`,
      `inventories/${localId}/items/${expensiveItemId}`,
      refundLedgerId ? `wallet_ledger/${refundLedgerId}` : null,
      purchaseChargeLedgerId ?
        `wallet_ledger/${purchaseChargeLedgerId}` :
        null,
      seedLedgerId ? `wallet_ledger/${seedLedgerId}` : null,
      `wallets/${localId}/balances/gold`,
      `item_catalog/${itemId}`,
      `item_catalog/${expensiveItemId}`,
    ].filter(Boolean);

    for (const path of paths) {
      await bestEffortDeleteFirestore(path);
    }
  }

  if (idToken) {
    try {
      const deletion = await postJson(
        `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/accounts:delete?key=${API_KEY}`,
        {idToken},
      );

      if (!deletion.response.ok) {
        console.warn(
          `Auth cleanup warning: HTTP ${deletion.response.status} ` +
          deletion.text,
        );
      }
    } catch (error) {
      console.warn("Auth cleanup warning:", error);
    }
  }

  console.log("[cleanup] Cleanup finished.");
}

if (testPassed) {
  console.log(
    "\nAtlasBoard Commerce + Purchase History Local E2E v1 PASSED.",
  );
  console.log(
    "Verified: trusted catalog price -> atomic wallet debit + immutable " +
    "ledger + entitlement grant + purchase history -> idempotent replay -> " +
    "failed-attempt history -> atomic refund + entitlement revoke -> cleanup.",
  );
}
