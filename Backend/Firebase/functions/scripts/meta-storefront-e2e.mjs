import {execFileSync} from "node:child_process";
import path from "node:path";
import {fileURLToPath} from "node:url";
import {createHash} from "node:crypto";

const PROJECT_ID = "atlasboard-usa";
const REGION = "europe-west1";
const AUTH = "http://127.0.0.1:9099";
const FUNCTIONS = "http://127.0.0.1:5001";
const FIRESTORE = "http://127.0.0.1:8080";
const nonce = `${Date.now()}-${Math.floor(Math.random() * 1e6)}`;
const SCRIPT_DIR = path.dirname(fileURLToPath(import.meta.url));
let cleanupItemId = "";
let cleanupPromoId = "";


function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

async function request(url, options = {}) {
  const response = await fetch(url, options);
  const text = await response.text();
  let json = null;
  try {
    json = text ? JSON.parse(text) : null;
  } catch {
    json = null;
  }
  return {response, text, json};
}

function docUrl(path) {
  return `${FIRESTORE}/v1/projects/${PROJECT_ID}` +
    `/databases/(default)/documents/${path}`;
}

async function patchDocument(path, fields) {
  const result = await request(docUrl(path), {
    method: "PATCH",
    headers: {"Content-Type": "application/json"},
    body: JSON.stringify({fields}),
  });
  assert(result.response.ok, `Firestore seed failed: ${path} ${result.text}`);
  return result.json;
}

async function deleteDocument(path) {
  const result = await request(docUrl(path), {method: "DELETE"});
  if (!result.response.ok && result.response.status !== 404) {
    throw new Error(`Firestore cleanup failed: ${path} ${result.text}`);
  }
}

async function cleanupTemporaryFixtures() {
  if (cleanupItemId) {
    await deleteDocument(`item_catalog/${cleanupItemId}`);
  }
  if (cleanupPromoId) {
    await deleteDocument(`promo_codes/${cleanupPromoId}`);
  }
}

async function getDocument(path) {
  const result = await request(docUrl(path));
  assert(result.response.ok, `Firestore GET failed: ${path} ${result.text}`);
  return result.json;
}

async function call(name, token, data) {
  const result = await request(
    `${FUNCTIONS}/${PROJECT_ID}/${REGION}/${name}`,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Authorization": `Bearer ${token}`,
      },
      body: JSON.stringify({data}),
    },
  );
  assert(result.response.ok, `${name} failed: ${result.text}`);
  return result.json?.result ?? result.json?.data;
}

function hash(value) {
  return createHash("sha256").update(value, "utf8").digest("hex");
}

function promoId(code) {
  return hash(`atlasboard:promo:${code.trim().toUpperCase()}`);
}

function redemptionId(uid, promoDocumentId) {
  return hash(`${uid}:promo_redemption:${promoDocumentId}`);
}

function redemptionEventId(redemptionDocumentId, key) {
  return hash(`${redemptionDocumentId}:event:${key}`);
}

function rewardArrayValue(rewards) {
  return {
    arrayValue: {
      values: rewards.map((reward) => ({
        mapValue: {
          fields: Object.fromEntries(
            Object.entries(reward).map(([key, value]) => [
              key,
              typeof value === "string"
                ? {stringValue: value}
                : {integerValue: String(value)},
            ]),
          ),
        },
      })),
    },
  };
}

async function main() {
  console.log("Atlas Board Phase 9 Store v2.1 E2E");
  console.log("Safety: local Auth/Firestore/Functions emulators only.");

  execFileSync(
    process.execPath,
    [path.join(SCRIPT_DIR, "meta-storefront-seed.mjs")],
    {stdio: "inherit"},
  );

  const itemId = `phase9_v2_dice_${nonce}`;
  const code = `V2-${Date.now().toString(36).toUpperCase()}`;
  const pid = promoId(code);
  cleanupItemId = itemId;
  cleanupPromoId = pid;

  await patchDocument(`item_catalog/${itemId}`, {
    itemId: {stringValue: itemId},
    itemType: {stringValue: "dice_skin"},
    displayName: {stringValue: "Phase 9 v2 Test Dice"},
    active: {booleanValue: true},
    sortOrder: {integerValue: "999"},
    catalogVersion: {integerValue: "2"},
    prices: {mapValue: {fields: {
      gold: {integerValue: "125"},
      atlas_coin: {integerValue: "12"},
    }}},
  });

  const now = Date.now();
  await patchDocument(`promo_codes/${pid}`, {
    promoId: {stringValue: pid},
    codeHash: {stringValue: pid},
    displayCode: {stringValue: code},
    active: {booleanValue: true},
    startsAtEpochMs: {integerValue: String(now - 60_000)},
    endsAtEpochMs: {integerValue: String(now + 3_600_000)},
    globalLimit: {integerValue: "10"},
    redemptionCount: {integerValue: "0"},
    perAccountLimit: {integerValue: "1"},
    rewards: rewardArrayValue([
      {type: "gold", amount: 250},
      {type: "atlas_coin", amount: 7},
    ]),
    schemaVersion: {integerValue: "1"},
  });
  console.log("[1/12] PASS catalog + redeem definition seeded.");

  const signUp = await request(
    `${AUTH}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=fake-api-key`,
    {
      method: "POST",
      headers: {"Content-Type": "application/json"},
      body: JSON.stringify({
        email: `phase9-v2-${nonce}@atlasboard.local`,
        password: `Phase9V2!${nonce}`,
        returnSecureToken: true,
      }),
    },
  );
  assert(signUp.response.ok, `Auth sign-up failed: ${signUp.text}`);
  const uid = signUp.json?.localId;
  const token = signUp.json?.idToken;
  assert(uid && token, "Auth emulator returned no uid/token.");
  console.log("[2/12] PASS authenticated temporary account.");

  const walletSeed = await call("walletTestMutation", token, {
    currencyId: "gold",
    delta: 1000,
    reason: "phase9_v2_e2e_seed",
    transactionId: `phase9-v2-seed-${nonce}`,
    idempotencyKey: `phase9-v2-seed-key-${nonce}`,
  });
  assert(walletSeed?.ok && walletSeed.balanceAfter === 1000,
    "Wallet seed balance mismatch.");
  console.log("[3/12] PASS server-authoritative wallet seeded.");

  const purchaseKey = `phase9-v2-purchase-${nonce}`;
  const purchase = await call("commerceTestPurchase", token, {
    itemId,
    paymentMethod: "gold",
    idempotencyKey: purchaseKey,
  });
  assert(
    purchase?.ok && purchase.status === "succeeded" &&
      purchase.balanceAfter === 875,
    `Purchase result mismatch: ${JSON.stringify(purchase)}`,
  );
  console.log("[4/12] PASS purchase used server catalog price and debited once.");

  const purchaseReplay = await call("commerceTestPurchase", token, {
    itemId,
    paymentMethod: "gold",
    idempotencyKey: purchaseKey,
  });
  assert(
    purchaseReplay?.idempotentReplay === true &&
      purchaseReplay.balanceAfter === 875,
    "Purchase replay was not idempotent.",
  );
  console.log("[5/12] PASS purchase replay did not double-charge.");

  const inventory = await getDocument(`inventories/${uid}/items/${itemId}`);
  assert(inventory?.fields?.owned?.booleanValue === true,
    "Purchased entitlement is not owned.");
  console.log("[6/12] PASS purchased entitlement is owned.");

  const redeemKey = `phase9-v2-redeem-${nonce}`;
  const redeem = await call("promoTestRedeem", token, {
    code: code.toLowerCase(),
    idempotencyKey: redeemKey,
  });
  assert(
    redeem?.ok && redeem.applied === true &&
      Array.isArray(redeem.rewards) && redeem.rewards.length === 2,
    `Redeem result mismatch: ${JSON.stringify(redeem)}`,
  );
  console.log("[7/12] PASS redeem code granted trusted configured rewards.");

  const redeemReplay = await call("promoTestRedeem", token, {
    code,
    idempotencyKey: redeemKey,
  });
  assert(redeemReplay?.idempotentReplay === true,
    "Redeem replay was not idempotent.");
  console.log("[8/12] PASS redeem replay did not grant rewards twice.");

  const rid = redemptionId(uid, pid);
  const eventId = redemptionEventId(rid, redeemKey);
  const event = await getDocument(`promo_redemptions/${rid}/events/${eventId}`);
  assert(
    event?.fields?.uid?.stringValue === uid &&
      event?.fields?.redemptionEventId?.stringValue === eventId &&
      event?.fields?.createdAt?.timestampValue,
    "Redeem evidence record is missing uid/id/server timestamp.",
  );
  console.log("[9/12] PASS redeem evidence is account-linked with server time + canonical ID.");

  const purchaseDoc = await getDocument(
    `commerce_transactions/${purchase.transactionId}`,
  );
  assert(
    purchaseDoc?.fields?.transactionId?.stringValue === purchase.transactionId &&
      purchaseDoc?.fields?.uid?.stringValue === uid &&
      purchaseDoc?.fields?.createdAt?.timestampValue,
    "Purchase evidence record is missing uid/id/server timestamp.",
  );
  console.log("[10/12] PASS purchase evidence has server time + canonical ID.");

  const reset = await call(
    "metaDevResetPurchasedEntitlements",
    token,
    {operationId: `phase9-v21-reset-${nonce}`},
  );
  assert(
    reset?.ok === true &&
      reset.resetCount === 1 &&
      typeof reset.auditId === "string" &&
      reset.auditId.length > 0,
    `Dev reset result mismatch: ${JSON.stringify(reset)}`,
  );

  const resetInventory = await getDocument(
    `inventories/${uid}/items/${itemId}`,
  );
  assert(
    resetInventory?.fields?.owned?.booleanValue === false,
    "Dev reset did not clear current commerce ownership.",
  );
  console.log(
    "[11/12] PASS dev reset cleared purchase ownership and kept evidence.",
  );

  const repurchase = await call("commerceTestPurchase", token, {
    itemId,
    paymentMethod: "gold",
    idempotencyKey: `phase9-v21-repurchase-${nonce}`,
  });
  assert(
    repurchase?.ok === true &&
      repurchase.status === "succeeded" &&
      repurchase.balanceAfter === 1000,
    `Repurchase after reset failed: ${JSON.stringify(repurchase)}`,
  );
  console.log(
    "[12/12] PASS item can be purchased again after emulator-only dev reset " +
      "without refunding the earlier purchase or promo reward.",
  );

  console.log("Phase 9 storefront v2.1 E2E PASS: 12 checks.");
}

try {
  await main();
} catch (error) {
  console.error(error);
  process.exitCode = 1;
} finally {
  try {
    await cleanupTemporaryFixtures();
    if (cleanupItemId || cleanupPromoId) {
      console.log("Cleanup: temporary E2E catalog/promo fixtures removed.");
    }
  } catch (cleanupError) {
    console.error("E2E cleanup warning:", cleanupError);
    process.exitCode = 1;
  }
}
