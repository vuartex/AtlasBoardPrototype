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

function callableResult(call) {
  return call.json?.result ?? call.json?.data;
}

function firestoreDocumentUrl(path) {
  return `${FIRESTORE_BASE}/v1/projects/${PROJECT_ID}` +
    `/databases/(default)/documents/${path}`;
}

async function getFirestoreDocument(path) {
  return requestJson(firestoreDocumentUrl(path));
}

async function patchFirestoreDocument(path, fields) {
  return patchJson(firestoreDocumentUrl(path), {fields});
}

async function bestEffortDeleteFirestore(path) {
  if (!path) {
    return;
  }

  try {
    const result = await deleteUrl(firestoreDocumentUrl(path));
    if (!result.response.ok && result.response.status !== 404) {
      console.warn(
        `Cleanup warning for ${path}: HTTP ${result.response.status}`,
      );
    }
  } catch (error) {
    console.warn(`Cleanup warning for ${path}:`, error);
  }
}

function firestoreInteger(document, fieldName) {
  const value = document?.fields?.[fieldName]?.integerValue;
  return value === undefined ? undefined : Number.parseInt(value, 10);
}

function firestoreBoolean(document, fieldName) {
  return document?.fields?.[fieldName]?.booleanValue;
}

function firestoreString(document, fieldName) {
  return document?.fields?.[fieldName]?.stringValue;
}

function sha256(value) {
  return createHash("sha256")
    .update(value, "utf8")
    .digest("hex");
}

function normalizePromoCode(code) {
  return code.trim().toUpperCase();
}

function promoId(code) {
  return sha256(`atlasboard:promo:${normalizePromoCode(code)}`);
}

function rewardArrayValue(rewards) {
  return {
    arrayValue: {
      values: rewards.map((reward) => ({
        mapValue: {
          fields: Object.fromEntries(
            Object.entries(reward).map(([key, value]) => {
              if (typeof value === "string") {
                return [key, {stringValue: value}];
              }

              if (typeof value === "number") {
                return [key, {integerValue: String(value)}];
              }

              throw new Error(`Unsupported reward seed value for ${key}.`);
            }),
          ),
        },
      })),
    },
  };
}

async function verifyEmulatorPreflight() {
  const hub = await requestJson(`${HUB_BASE}/emulators`);

  assert(
    hub.response.ok,
    "Emulator Hub is not reachable on 127.0.0.1:4400.",
  );

  const emulators = hub.json ?? {};
  for (const name of ["auth", "functions", "firestore"]) {
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

async function expectCallableError(
  call,
  expectedStatus,
  expectedErrorKey,
  label,
) {
  assert(!call.response.ok, `${label} unexpectedly succeeded.`);
  assert(
    call.json?.error?.status === expectedStatus,
    `${label}: expected status=${expectedStatus}, got ` +
      `${call.json?.error?.status}. Body=${call.text}`,
  );
  assert(
    call.json?.error?.details?.errorKey === expectedErrorKey,
    `${label}: expected errorKey=${expectedErrorKey}, got ` +
      `${call.json?.error?.details?.errorKey}.`,
  );
}

const nonce = `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`;
const compactNonce = Date.now().toString(36).toUpperCase();

const email = `atlasboard.economy.flow.${nonce}@example.com`;
const password = `AtlasBoardEconomyFlow!${nonce}`;

const promoCode = `FLOW-${compactNonce}`;
const promoFrameId = `e2e_flow_frame_${nonce}`;
const shopDiceId = `e2e_flow_dice_${nonce}`;
const directEmoteId = `e2e_flow_emote_${nonce}`;
const ticketCurrencyId = "ticket_validation_2027";

const promoKey = `flow-promo-${nonce}`;
const purchaseKey = `flow-purchase-${nonce}`;
const refundKey = `flow-refund-${nonce}`;
const debitKey = `flow-debit-${nonce}`;
const debitTransactionId = `flow-debit-tx-${nonce}`;
const overdrawKey = `flow-overdraw-${nonce}`;
const overdrawTransactionId = `flow-overdraw-tx-${nonce}`;
const emoteGrantKey = `flow-emote-grant-${nonce}`;
const emoteGrantTransactionId = `flow-emote-grant-tx-${nonce}`;
const emoteRevokeKey = `flow-emote-revoke-${nonce}`;
const emoteRevokeTransactionId = `flow-emote-revoke-tx-${nonce}`;

let idToken = null;
let localId = null;
let promoDocumentId = null;
let promoResult = null;
let purchaseResult = null;
let refundResult = null;
let debitResult = null;
let emoteGrantResult = null;
let emoteRevokeResult = null;
let testPassed = false;

console.log("AtlasBoard Economy Integrated Flow Local E2E v1");
console.log(
  "Safety: localhost Auth/Firestore/Functions emulators only. " +
  "No production account, wallet, inventory, commerce, or promo data.",
);

try {
  console.log("[0/12] Verifying emulator safety preflight...");
  await verifyEmulatorPreflight();
  console.log("[0/12] PASSED. Auth/Firestore/Functions emulators detected.");

  console.log("[1/12] Creating one temporary authenticated account...");
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
    `Auth sign-up failed: HTTP ${signUp.response.status} ${signUp.text}`,
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

  console.log(`[1/12] PASSED. Local UID=${localId}`);

  console.log("[2/12] Seeding catalog items and one multi-reward promo...");
  const frameSeed = await patchFirestoreDocument(
    `item_catalog/${promoFrameId}`,
    {
      itemId: {stringValue: promoFrameId},
      itemType: {stringValue: "profile_frame"},
      active: {booleanValue: true},
      schemaVersion: {integerValue: "1"},
    },
  );
  assert(frameSeed.response.ok, `Frame catalog seed failed: ${frameSeed.text}`);

  const diceSeed = await patchFirestoreDocument(
    `item_catalog/${shopDiceId}`,
    {
      itemId: {stringValue: shopDiceId},
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
  assert(diceSeed.response.ok, `Dice catalog seed failed: ${diceSeed.text}`);

  const emoteSeed = await patchFirestoreDocument(
    `item_catalog/${directEmoteId}`,
    {
      itemId: {stringValue: directEmoteId},
      itemType: {stringValue: "emote"},
      active: {booleanValue: true},
      schemaVersion: {integerValue: "1"},
    },
  );
  assert(emoteSeed.response.ok, `Emote catalog seed failed: ${emoteSeed.text}`);

  promoDocumentId = promoId(promoCode);
  const now = Date.now();
  const promoSeed = await patchFirestoreDocument(
    `promo_codes/${promoDocumentId}`,
    {
      promoId: {stringValue: promoDocumentId},
      codeHash: {stringValue: promoDocumentId},
      active: {booleanValue: true},
      startsAtEpochMs: {integerValue: String(now - 60_000)},
      endsAtEpochMs: {integerValue: String(now + 3_600_000)},
      globalLimit: {integerValue: "10"},
      redemptionCount: {integerValue: "0"},
      perAccountLimit: {integerValue: "1"},
      rewards: rewardArrayValue([
        {type: "gold", amount: 300},
        {type: "atlas_coin", amount: 5},
        {
          type: "event_ticket",
          currencyId: ticketCurrencyId,
          amount: 1,
        },
        {type: "inventory_item", itemId: promoFrameId},
      ]),
      schemaVersion: {integerValue: "1"},
    },
  );
  assert(promoSeed.response.ok, `Promo seed failed: ${promoSeed.text}`);

  console.log("[2/12] PASSED. Integrated test data seeded.");

  console.log("[3/12] Redeeming promo to establish cross-system state...");
  const promoCall = await callFunction("promoTestRedeem", idToken, {
    code: promoCode.toLowerCase(),
    idempotencyKey: promoKey,
  });

  assert(
    promoCall.response.ok,
    `Promo redeem failed: HTTP ${promoCall.response.status} ${promoCall.text}`,
  );

  promoResult = callableResult(promoCall);
  assert(promoResult?.applied === true, "Promo redemption was not applied.");
  assert(promoResult?.idempotentReplay === false,
    "First promo redemption was marked as replay.");

  const goldPromoReward = promoResult?.rewards?.find(
    (reward) => reward.type === "gold",
  );
  const atlasReward = promoResult?.rewards?.find(
    (reward) => reward.type === "atlas_coin",
  );
  const ticketReward = promoResult?.rewards?.find(
    (reward) => reward.type === "event_ticket",
  );
  const frameReward = promoResult?.rewards?.find(
    (reward) => reward.type === "inventory_item",
  );

  assert(goldPromoReward?.balanceAfter === 300,
    "Promo expected Gold balance 300.");
  assert(atlasReward?.balanceAfter === 5,
    "Promo expected Atlas Coin balance 5.");
  assert(ticketReward?.balanceAfter === 1,
    "Promo expected event ticket balance 1.");
  assert(frameReward?.itemId === promoFrameId,
    "Promo frame entitlement result mismatch.");

  console.log(
    "[3/12] PASSED. Promo granted Gold + Atlas Coin + ticket + frame.",
  );

  console.log("[4/12] Replaying promo and proving rewards are idempotent...");
  const promoReplay = await callFunction("promoTestRedeem", idToken, {
    code: promoCode,
    idempotencyKey: promoKey,
  });
  assert(promoReplay.response.ok,
    `Promo replay failed: ${promoReplay.text}`);
  const promoReplayResult = callableResult(promoReplay);
  assert(promoReplayResult?.applied === false,
    "Promo replay applied rewards twice.");
  assert(promoReplayResult?.idempotentReplay === true,
    "Promo replay not recognized as idempotent.");
  console.log("[4/12] PASSED. Promo replay did not duplicate rewards.");

  console.log("[5/12] Purchasing a catalog item using promo-granted Gold...");
  const purchase = await callFunction("commerceTestPurchase", idToken, {
    itemId: shopDiceId,
    paymentMethod: "gold",
    idempotencyKey: purchaseKey,
  });

  assert(
    purchase.response.ok,
    `Commerce purchase failed: HTTP ${purchase.response.status} ` +
      purchase.text,
  );

  purchaseResult = callableResult(purchase);
  assert(purchaseResult?.applied === true,
    "Integrated purchase was not applied.");
  assert(purchaseResult?.balanceBefore === 300,
    "Purchase expected Gold balanceBefore=300.");
  assert(purchaseResult?.balanceAfter === 100,
    "Purchase expected Gold balanceAfter=100.");
  assert(purchaseResult?.entitlementStatus === "granted",
    "Purchase entitlement was not granted.");

  console.log(
    "[5/12] PASSED. Promo-funded commerce purchase changed Gold 300 -> 100.",
  );

  console.log("[6/12] Replaying commerce purchase...");
  const purchaseReplay = await callFunction(
    "commerceTestPurchase",
    idToken,
    {
      itemId: shopDiceId,
      paymentMethod: "gold",
      idempotencyKey: purchaseKey,
    },
  );

  assert(purchaseReplay.response.ok,
    `Purchase replay failed: ${purchaseReplay.text}`);
  const purchaseReplayResult = callableResult(purchaseReplay);
  assert(purchaseReplayResult?.applied === false,
    "Commerce replay charged twice.");
  assert(purchaseReplayResult?.idempotentReplay === true,
    "Commerce replay not identified as idempotent.");

  console.log("[6/12] PASSED. Purchase replay did not charge twice.");

  console.log("[7/12] Applying direct wallet debit after commerce...");
  const debit = await callFunction("walletTestMutation", idToken, {
    currencyId: "gold",
    delta: -25,
    reason: "integrated_flow_debit",
    transactionId: debitTransactionId,
    idempotencyKey: debitKey,
  });

  assert(debit.response.ok, `Wallet debit failed: ${debit.text}`);
  debitResult = callableResult(debit);
  assert(debitResult?.balanceBefore === 100,
    "Integrated debit expected balanceBefore=100.");
  assert(debitResult?.balanceAfter === 75,
    "Integrated debit expected balanceAfter=75.");

  console.log("[7/12] PASSED. Wallet module interoperated: Gold 100 -> 75.");

  console.log("[8/12] Granting standalone entitlement...");
  const emoteGrant = await callFunction(
    "inventoryTestMutation",
    idToken,
    {
      operation: "grant",
      itemId: directEmoteId,
      reason: "integrated_flow_grant",
      transactionId: emoteGrantTransactionId,
      idempotencyKey: emoteGrantKey,
    },
  );

  assert(emoteGrant.response.ok,
    `Standalone entitlement grant failed: ${emoteGrant.text}`);
  emoteGrantResult = callableResult(emoteGrant);
  assert(emoteGrantResult?.ownedAfter === true,
    "Standalone entitlement was not owned after grant.");

  console.log("[8/12] PASSED. Inventory module granted standalone emote.");

  console.log("[9/12] Refunding purchase after additional wallet activity...");
  const refund = await callFunction("commerceTestRefund", idToken, {
    transactionId: purchaseResult.transactionId,
    idempotencyKey: refundKey,
  });

  assert(refund.response.ok, `Commerce refund failed: ${refund.text}`);
  refundResult = callableResult(refund);
  assert(refundResult?.applied === true, "Refund was not applied.");
  assert(refundResult?.balanceBefore === 75,
    "Refund expected current Gold balanceBefore=75.");
  assert(refundResult?.balanceAfter === 275,
    "Refund expected Gold balanceAfter=275.");
  assert(refundResult?.entitlementStatus === "revoked",
    "Refund did not revoke purchased entitlement.");

  console.log(
    "[9/12] PASSED. Refund restored 200 Gold without disturbing other state.",
  );

  console.log("[10/12] Revoking standalone entitlement and testing overdraft...");
  const emoteRevoke = await callFunction(
    "inventoryTestMutation",
    idToken,
    {
      operation: "revoke",
      itemId: directEmoteId,
      reason: "integrated_flow_revoke",
      transactionId: emoteRevokeTransactionId,
      idempotencyKey: emoteRevokeKey,
    },
  );

  assert(emoteRevoke.response.ok,
    `Standalone entitlement revoke failed: ${emoteRevoke.text}`);
  emoteRevokeResult = callableResult(emoteRevoke);
  assert(emoteRevokeResult?.ownedAfter === false,
    "Standalone entitlement remained owned after revoke.");

  const overdraw = await callFunction("walletTestMutation", idToken, {
    currencyId: "gold",
    delta: -500,
    reason: "integrated_flow_overdraw",
    transactionId: overdrawTransactionId,
    idempotencyKey: overdrawKey,
  });

  await expectCallableError(
    overdraw,
    "FAILED_PRECONDITION",
    "economy.error.insufficient_funds",
    "Integrated overdraw",
  );

  console.log(
    "[10/12] PASSED. Inventory revoke worked and overdraft was rejected.",
  );

  console.log("[11/12] Verifying final cross-module Firestore state...");
  const [
    goldDoc,
    atlasDoc,
    ticketDoc,
    frameDoc,
    diceDoc,
    emoteDoc,
    commerceDoc,
    promoRedemptionDoc,
  ] = await Promise.all([
    getFirestoreDocument(`wallets/${localId}/balances/gold`),
    getFirestoreDocument(`wallets/${localId}/balances/atlas_coin`),
    getFirestoreDocument(
      `wallets/${localId}/balances/${ticketCurrencyId}`,
    ),
    getFirestoreDocument(
      `inventories/${localId}/items/${promoFrameId}`,
    ),
    getFirestoreDocument(
      `inventories/${localId}/items/${shopDiceId}`,
    ),
    getFirestoreDocument(
      `inventories/${localId}/items/${directEmoteId}`,
    ),
    getFirestoreDocument(
      `commerce_transactions/${purchaseResult.transactionId}`,
    ),
    getFirestoreDocument(
      `promo_redemptions/${promoResult.redemptionId}`,
    ),
  ]);

  assert(goldDoc.response.ok, "Final Gold document missing.");
  assert(firestoreInteger(goldDoc.json, "amount") === 275,
    "Final Gold balance expected 275.");
  assert(atlasDoc.response.ok, "Final Atlas Coin document missing.");
  assert(firestoreInteger(atlasDoc.json, "amount") === 5,
    "Final Atlas Coin balance expected 5.");
  assert(ticketDoc.response.ok, "Final event-ticket document missing.");
  assert(firestoreInteger(ticketDoc.json, "amount") === 1,
    "Final event-ticket balance expected 1.");

  assert(frameDoc.response.ok, "Promo frame entitlement document missing.");
  assert(firestoreBoolean(frameDoc.json, "owned") === true,
    "Promo frame should remain owned.");

  assert(diceDoc.response.ok, "Purchased dice entitlement document missing.");
  assert(firestoreBoolean(diceDoc.json, "owned") === false,
    "Refunded dice entitlement should be revoked.");

  assert(emoteDoc.response.ok, "Standalone emote document missing.");
  assert(firestoreBoolean(emoteDoc.json, "owned") === false,
    "Standalone emote should be revoked.");

  assert(commerceDoc.response.ok, "Commerce transaction document missing.");
  assert(firestoreString(commerceDoc.json, "status") === "refunded",
    "Commerce transaction final status expected refunded.");

  assert(promoRedemptionDoc.response.ok,
    "Promo redemption aggregate document missing.");
  assert(firestoreInteger(promoRedemptionDoc.json, "redemptionCount") === 1,
    "Promo redemption count expected 1.");

  console.log(
    "[11/12] PASSED. Final wallet/inventory/commerce/promo state is coherent.",
  );

  console.log("[12/12] Verifying rejected overdraft caused no mutation...");
  const finalGold = await getFirestoreDocument(
    `wallets/${localId}/balances/gold`,
  );
  assert(finalGold.response.ok, "Final Gold balance document missing.");
  assert(firestoreInteger(finalGold.json, "amount") === 275,
    "Rejected overdraft changed Gold balance.");

  console.log(
    "[12/12] PASSED. Rejected operation left final state unchanged.",
  );

  testPassed = true;
} catch (error) {
  console.error("\nAtlasBoard Economy Integrated Flow Local E2E v1 FAILED.");
  console.error(error);
  process.exitCode = 1;
} finally {
  console.log("[cleanup] Cleaning integrated-flow emulator data...");

  if (localId) {
    const promoRewards = promoResult?.rewards ?? [];

    const ledgerIds = [
      ...promoRewards
        .map((reward) => reward.ledgerEntryId)
        .filter(Boolean),
      purchaseResult?.chargeLedgerEntryId,
      refundResult?.refundLedgerEntryId,
      debitResult?.ledgerEntryId,
    ].filter(Boolean);

    const paths = [
      promoResult?.redemptionEventId ?
        `promo_redemptions/${promoResult.redemptionId}/events/` +
          promoResult.redemptionEventId :
        null,
      promoResult?.redemptionId ?
        `promo_redemptions/${promoResult.redemptionId}` :
        null,

      refundResult?.commerceEventId ?
        `commerce_transactions/${purchaseResult?.transactionId}/events/` +
          refundResult.commerceEventId :
        null,
      purchaseResult?.commerceEventId ?
        `commerce_transactions/${purchaseResult.transactionId}/events/` +
          purchaseResult.commerceEventId :
        null,
      purchaseResult?.transactionId ?
        `commerce_transactions/${purchaseResult.transactionId}` :
        null,

      refundResult?.revokeEventId ?
        `inventories/${localId}/items/${shopDiceId}/events/` +
          refundResult.revokeEventId :
        null,
      purchaseResult?.grantEventId ?
        `inventories/${localId}/items/${shopDiceId}/events/` +
          purchaseResult.grantEventId :
        null,

      emoteRevokeResult?.eventId ?
        `inventories/${localId}/items/${directEmoteId}/events/` +
          emoteRevokeResult.eventId :
        null,
      emoteGrantResult?.eventId ?
        `inventories/${localId}/items/${directEmoteId}/events/` +
          emoteGrantResult.eventId :
        null,

      ...promoRewards
        .filter((reward) => reward.inventoryEventId && reward.itemId)
        .map(
          (reward) =>
            `inventories/${localId}/items/${reward.itemId}/events/` +
            reward.inventoryEventId,
        ),

      `inventories/${localId}/items/${promoFrameId}`,
      `inventories/${localId}/items/${shopDiceId}`,
      `inventories/${localId}/items/${directEmoteId}`,

      ...ledgerIds.map((id) => `wallet_ledger/${id}`),

      `wallets/${localId}/balances/gold`,
      `wallets/${localId}/balances/atlas_coin`,
      `wallets/${localId}/balances/${ticketCurrencyId}`,

      promoDocumentId ? `promo_codes/${promoDocumentId}` : null,

      `item_catalog/${promoFrameId}`,
      `item_catalog/${shopDiceId}`,
      `item_catalog/${directEmoteId}`,
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
    "\nAtlasBoard Economy Integrated Flow Local E2E v1 PASSED.",
  );
  console.log(
    "Verified: promo rewards -> wallet-funded commerce -> wallet mutation -> " +
    "inventory lifecycle -> refund after intervening balance change -> " +
    "overdraft rejection -> coherent direct Firestore state -> cleanup.",
  );
}
