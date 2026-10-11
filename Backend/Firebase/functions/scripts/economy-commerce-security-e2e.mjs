process.env.FIRESTORE_EMULATOR_HOST =
  process.env.FIRESTORE_EMULATOR_HOST ??
  "127.0.0.1:8080";

import {getApps, initializeApp} from "firebase-admin/app";
import {getFirestore} from "firebase-admin/firestore";

const projectId =
  process.env.ATLAS_FIREBASE_PROJECT_ID ??
  "atlasboard-usa";

const region =
  process.env.ATLAS_FIREBASE_REGION ??
  "europe-west1";

const authHost =
  process.env.FIREBASE_AUTH_EMULATOR_HOST ??
  "127.0.0.1:9099";

const functionsHost =
  process.env.ATLAS_FUNCTIONS_EMULATOR_HOST ??
  "127.0.0.1:5001";

if (getApps().length === 0) {
  initializeApp({
    projectId,
  });
}

const db =
  getFirestore();

function assert(
  condition,
  message,
) {
  if (!condition) {
    throw new Error(
      message,
    );
  }
}

function url(name) {
  return `http://${functionsHost}/${projectId}/${region}/${name}`;
}

async function createUser(label) {
  const stamp =
    `${Date.now()}-` +
    Math.floor(
      Math.random() * 100000,
    );

  const response =
    await fetch(
      `http://${authHost}/identitytoolkit.googleapis.com/v1/` +
      "accounts:signUp?key=phase15h-local",
      {
        method:
          "POST",
        headers: {
          "Content-Type":
            "application/json",
        },
        body:
          JSON.stringify({
            email:
              `phase15h-${label}-${stamp}@example.test`,
            password:
              "Phase15H-Local-Only-123!",
            returnSecureToken:
              true,
          }),
      },
    );

  const body =
    await response.json();

  assert(
    response.ok &&
    typeof body.idToken === "string",
    "Auth Emulator user creation failed: " +
    JSON.stringify(body),
  );

  return {
    uid:
      body.localId,
    token:
      body.idToken,
  };
}

async function call(
  name,
  token,
  data,
) {
  const response =
    await fetch(
      url(name),
      {
        method:
          "POST",
        headers: {
          "Content-Type":
            "application/json",
          Authorization:
            `Bearer ${token}`,
          "X-Firebase-AppCheck":
            "phase15h-local-emulator-token",
        },
        body:
          JSON.stringify({
            data,
          }),
      },
    );

  let json = null;

  try {
    json =
      await response.json();
  } catch {
    // Keep null for diagnostics.
  }

  return {
    response,
    json,
  };
}

function result(value) {
  return value.json?.result ??
    null;
}

async function balance(
  uid,
  currencyId,
) {
  const snapshot =
    await db
      .collection("wallets")
      .doc(uid)
      .collection("balances")
      .doc(currencyId)
      .get();

  return Number(
    snapshot.data()?.amount ??
    0,
  );
}

async function seedCatalog(
  itemId,
  active,
  price,
) {
  await db
    .collection("item_catalog")
    .doc(itemId)
    .set({
      itemId,
      itemType:
        "pawn",
      active,
      prices: {
        gold:
          price,
      },
      schemaVersion:
        1,
    });
}

async function main() {
  const owner =
    await createUser(
      "owner",
    );

  const outsider =
    await createUser(
      "outsider",
    );

  await db
    .collection("wallets")
    .doc(owner.uid)
    .collection("balances")
    .doc("gold")
    .set({
      uid:
        owner.uid,
      currencyId:
        "gold",
      amount:
        1000,
      schemaVersion:
        1,
    });

  const itemA =
    `phase15h_item_a_${Date.now()}`;

  const itemB =
    `phase15h_item_b_${Date.now()}`;

  const inactiveItem =
    `phase15h_item_inactive_${Date.now()}`;

  await seedCatalog(
    itemA,
    true,
    125,
  );

  await seedCatalog(
    itemB,
    true,
    200,
  );

  await seedCatalog(
    inactiveItem,
    false,
    50,
  );

  let checks =
    0;

  const purchaseKey =
    `phase15h-purchase-${Date.now()}`;

  const purchase =
    await call(
      "commerceTestPurchase",
      owner.token,
      {
        itemId:
          itemA,
        paymentMethod:
          "gold",
        idempotencyKey:
          purchaseKey,
      },
    );

  assert(
    purchase.response.ok &&
    result(purchase)?.applied === true &&
    result(purchase)?.amount === 125,
    "Authoritative-price purchase failed: " +
    JSON.stringify(purchase.json),
  );

  assert(
    await balance(
      owner.uid,
      "gold",
    ) === 875,
    "Server catalog price was not debited exactly once.",
  );

  checks++;

  const replay =
    await call(
      "commerceTestPurchase",
      owner.token,
      {
        itemId:
          itemA,
        paymentMethod:
          "gold",
        idempotencyKey:
          purchaseKey,
      },
    );

  assert(
    replay.response.ok &&
    result(replay)?.idempotentReplay === true &&
    await balance(owner.uid, "gold") === 875,
    "Purchase replay changed balance or was not idempotent: " +
    JSON.stringify(replay.json),
  );

  checks++;

  const conflict =
    await call(
      "commerceTestPurchase",
      owner.token,
      {
        itemId:
          itemB,
        paymentMethod:
          "gold",
        idempotencyKey:
          purchaseKey,
      },
    );

  assert(
    conflict.response.status === 409 ||
    conflict.json?.error?.status ===
      "ALREADY_EXISTS",
    "Conflicting idempotency payload was not rejected: " +
    JSON.stringify(conflict.json),
  );

  checks++;

  const transactionId =
    result(purchase)
      .transactionId;

  const wrongOwnerRefund =
    await call(
      "commerceTestRefund",
      outsider.token,
      {
        transactionId,
        idempotencyKey:
          `phase15h-wrong-owner-${Date.now()}`,
      },
    );

  assert(
    wrongOwnerRefund.response.status === 403 ||
    wrongOwnerRefund.json?.error?.status ===
      "PERMISSION_DENIED",
    "Non-owner refund was not rejected: " +
    JSON.stringify(wrongOwnerRefund.json),
  );

  checks++;

  const refundKey =
    `phase15h-refund-${Date.now()}`;

  const refund =
    await call(
      "commerceTestRefund",
      owner.token,
      {
        transactionId,
        idempotencyKey:
          refundKey,
      },
    );

  assert(
    refund.response.ok &&
    result(refund)?.applied === true &&
    await balance(owner.uid, "gold") === 1000,
    "Owner refund did not restore the exact wallet amount: " +
    JSON.stringify(refund.json),
  );

  const refundReplay =
    await call(
      "commerceTestRefund",
      owner.token,
      {
        transactionId,
        idempotencyKey:
          refundKey,
      },
    );

  assert(
    refundReplay.response.ok &&
    result(refundReplay)?.idempotentReplay === true &&
    await balance(owner.uid, "gold") === 1000,
    "Refund replay changed balance or was not idempotent: " +
    JSON.stringify(refundReplay.json),
  );

  checks++;

  const inactive =
    await call(
      "commerceTestPurchase",
      owner.token,
      {
        itemId:
          inactiveItem,
        paymentMethod:
          "gold",
        idempotencyKey:
          `phase15h-inactive-${Date.now()}`,
      },
    );

  assert(
    !inactive.response.ok,
    "Inactive catalog item was purchasable.",
  );

  checks++;

  console.log(
    "Phase 15H economy/commerce E2E PASS. " +
    `Checks=${checks}/6. ` +
    "Server catalog price, purchase/refund idempotency, owner-only refund, " +
    "idempotency conflict, and inactive-catalog rejection were verified.",
  );
}

main().catch(
  (error) => {
    console.error(
      "Phase 15H economy/commerce E2E FAIL.",
      error,
    );
    process.exit(1);
  },
);
