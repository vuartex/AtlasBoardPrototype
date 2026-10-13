/**
 * AtlasBoard SECURITY-CORE-02: economy, commerce, wallet and entitlement E2E.
 *
 * Included by the existing `node scripts/qa.mjs --full` integration batch.
 * Requires a fresh LOCAL Firebase emulators:exec session; never deploys.
 * No privileged fixture writes occur before all emulator safety checks pass.
 *
 * One-shot offline preflight:
 *   node scripts/economy-commerce-security-e2e.mjs --self-test
 */
import {randomBytes} from "node:crypto";

const PROJECT_ID = "atlasboard-usa";
const REGION = "europe-west1";
const LOOPBACK = "127.0.0.1";
const AUTH_HOST = `${LOOPBACK}:9099`;
const FIRESTORE_HOST = `${LOOPBACK}:8080`;
const FUNCTIONS_HOST = `${LOOPBACK}:5001`;
const HUB_URL = `http://${LOOPBACK}:4400/emulators`;
const TEST_TIMEOUT_MS = 30000;

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function allowedLocalHost(host, port) {
  return host === `${LOOPBACK}:${port}`;
}

function assertEmulatorEnvironment(env) {
  assert(env.FIRESTORE_EMULATOR_HOST === FIRESTORE_HOST,
    "SEC02: Firestore emulator must be explicit local 127.0.0.1:8080");
  assert(env.FIREBASE_AUTH_EMULATOR_HOST === AUTH_HOST,
    "SEC02: Auth emulator must be explicit local 127.0.0.1:9099");
  if (env.GCLOUD_PROJECT || env.GCP_PROJECT) {
    assert((env.GCLOUD_PROJECT ?? env.GCP_PROJECT) === PROJECT_ID,
      "SEC02: unexpected Firebase project, refusing privileged fixture writes");
  }
  assert(allowedLocalHost(FUNCTIONS_HOST, 5001),
    "SEC02: Functions target must be loopback-only");
}

function assertCallableFailure(response, tag) {
  assert(!response.ok,
    `${tag}: call was unexpectedly successful: ${JSON.stringify(response.body)}`);
}

async function httpJson(url, options = {}) {
  const response = await fetch(url, {
    ...options,
    signal: AbortSignal.timeout(TEST_TIMEOUT_MS),
  });
  const text = await response.text();
  let body;
  try { body = text ? JSON.parse(text) : null; } catch { body = {raw: text}; }
  return {ok: response.ok, status: response.status, body, text};
}

async function preflight() {
  assertEmulatorEnvironment(process.env);
  const hub = await httpJson(HUB_URL);
  assert(hub.ok, "SEC02: Firebase Emulator Hub not reachable");
  for (const emulator of ["auth", "firestore", "functions"]) {
    assert(hub.body?.[emulator], `SEC02: ${emulator} Emulator missing`);
  }
  // Prove the Firestore REST target is an emulator, not a live endpoint.
  const probe = await httpJson(
    `http://${FIRESTORE_HOST}/v1/projects/${PROJECT_ID}` +
    "/databases/(default)/documents/atlas_security_core02_probe/unset",
  );
  assert(probe.status === 404 || probe.status === 403,
    `SEC02: unexpected local Firestore probe response ${probe.status}`);
  console.log("SEC02 [1] PASS local-only Auth/Firestore/Functions preflight.");
}

async function createUser(label, nonce) {
  const response = await httpJson(
    `http://${AUTH_HOST}/identitytoolkit.googleapis.com/v1/` +
    "accounts:signUp?key=atlas-core02-local",
    {
      method: "POST",
      headers: {"Content-Type": "application/json"},
      body: JSON.stringify({
        email: `core02.${label}.${nonce}@example.test`,
        password: `AtlasCore02-${label}-Local-123!`,
        returnSecureToken: true,
      }),
    },
  );
  assert(response.ok && response.body?.idToken && response.body?.localId,
    `SEC02: local Auth signup failed (${label}): ${response.text}`);
  return {uid: response.body.localId, token: response.body.idToken};
}

async function call(name, token, data) {
  const headers = {
    "Content-Type": "application/json",
    "X-Firebase-AppCheck": "atlas-core02-local-emulator-token",
  };
  if (token) headers.Authorization = `Bearer ${token}`;
  return httpJson(
    `http://${FUNCTIONS_HOST}/${PROJECT_ID}/${REGION}/${name}`,
    {method: "POST", headers, body: JSON.stringify({data})},
  );
}

function result(callResponse) {
  return callResponse.body?.result ?? null;
}

async function clientPatch(token, path, fields) {
  const uri = `http://${FIRESTORE_HOST}/v1/projects/${PROJECT_ID}` +
    `/databases/(default)/documents/${path}`;
  return httpJson(uri, {
    method: "PATCH",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({fields}),
  });
}

async function walletBalance(db, uid, currency = "gold") {
  const snapshot = await db.collection("wallets").doc(uid)
    .collection("balances").doc(currency).get();
  return Number(snapshot.data()?.amount ?? 0);
}

async function seedWallet(db, uid, amount = 1000) {
  await db.collection("wallets").doc(uid)
    .collection("balances").doc("gold").set({
      uid, currencyId: "gold", amount, schemaVersion: 1,
    });
}

async function seedCatalog(db, itemId, {active = true, price = 125} = {}) {
  await db.collection("item_catalog").doc(itemId).set({
    itemId, itemType: "pawn", active, prices: {gold: price},
    schemaVersion: 1,
  });
}

async function inventory(db, uid, itemId) {
  return (await db.collection("inventories").doc(uid)
    .collection("items").doc(itemId).get()).data() ?? {};
}

async function ledgerCount(db, transactionId, reason) {
  const docs = await db.collection("wallet_ledger")
    .where("transactionId", "==", transactionId).get();
  return docs.docs.filter((d) => d.data()?.reason === reason).length;
}

function isConflict(response) {
  return response.status === 409 ||
    response.body?.error?.status === "ALREADY_EXISTS" ||
    response.body?.error?.status === "FAILED_PRECONDITION";
}

function assertSuccess(response, tag) {
  assert(response.ok && result(response)?.ok === true,
    `${tag}: ${JSON.stringify(response.body)}`);
  return result(response);
}

async function cleanup(db, auth, uids, catalogIds, nonce) {
  let issues = 0;
  const safe = async (label, action) => {
    try { await action(); } catch (error) {
      issues++;
      console.warn(`SEC02 cleanup warning (${label}): ${error.message}`);
    }
  };
  // Only collections dedicated to these fresh local test accounts are touched.
  for (const uid of uids) {
    for (const name of ["wallets", "inventories"]) {
      await safe(`${name}/${uid}`, async () => {
        await db.recursiveDelete(db.collection(name).doc(uid));
      });
    }
    await safe(`Auth/${uid}`, async () => auth.deleteUser(uid));
  }
  for (const id of catalogIds) {
    await safe(`item_catalog/${id}`, async () =>
      db.collection("item_catalog").doc(id).delete());
  }
  for (const name of ["commerce_transactions", "wallet_ledger"]) {
    await safe(`cleanup ${name}`, async () => {
      for (const uid of uids) {
        const refs = await db.collection(name).where("uid", "==", uid).get();
        for (const doc of refs.docs) {
          await db.recursiveDelete(doc.ref);
        }
      }
    });
  }
  console.log(`SEC02 cleanup ${nonce}: ${issues === 0 ? "PASS" : `${issues} warnings`}.`);
  assert(issues === 0, "SEC02: local emulator cleanup incomplete");
}

async function main() {
  if (process.argv.length > 3 ||
      (process.argv[2] && process.argv[2] !== "--self-test")) {
    throw new Error("SEC02: only --self-test or no flag is supported");
  }
  if (process.argv[2] === "--self-test") {
    for (const value of ["localhost:8080", "10.0.0.1:8080", "example.com:8080", "127.0.0.1:8081"]) {
      assert(!allowedLocalHost(value, 8080), `SEC02 bad host passed: ${value}`);
    }
    assert(allowedLocalHost("127.0.0.1:8080", 8080), "SEC02 good host rejected");
    assertCallableFailure({ok: false, body: {}}, "SEC02 self-test");
    console.log("SEC02 self-test PASS: fixed loopback/project and failure helpers.");
    return;
  }

  await preflight();
  process.env.FIRESTORE_EMULATOR_HOST = FIRESTORE_HOST;
  const [{getApps, initializeApp}, {getFirestore}, {getAuth}] = await Promise.all([
    import("firebase-admin/app"),
    import("firebase-admin/firestore"),
    import("firebase-admin/auth"),
  ]);
  if (getApps().length === 0) initializeApp({projectId: PROJECT_ID});
  const db = getFirestore();
  const auth = getAuth();
  const nonce = `${Date.now()}-${randomBytes(5).toString("hex")}`;
  const catalogIds = [];
  const uids = [];
  const id = (suffix) => `core02_${suffix}_${nonce}`;
  const key = (suffix) => `core02-idem-${suffix}-${nonce}`;
  const item = async (suffix, opts) => {
    const itemId = id(suffix);
    catalogIds.push(itemId);
    await seedCatalog(db, itemId, opts);
    return itemId;
  };
  let checks = 1;
  const pass = (description) => {
    checks++;
    console.log(`SEC02 [${checks}] PASS ${description}`);
  };
  let attempted = false;
  try {
    const owner = await createUser("owner", nonce);
    const outsider = await createUser("outsider", nonce);
    const raceOwner = await createUser("parallel", nonce);
    uids.push(owner.uid, outsider.uid, raceOwner.uid);
    await Promise.all([seedWallet(db, owner.uid), seedWallet(db, raceOwner.uid)]);
    pass("isolated authenticated identities and server-seeded wallets");

    const productA = await item("product_a", {price: 125});
    const productB = await item("product_b", {price: 200});
    const inactive = await item("inactive", {active: false, price: 50});
    const invalidPrice = await item("invalid_price", {price: 0});
    const expensive = await item("expensive", {price: 2000});
    const concurrency1 = await item("same_key_race", {price: 150});
    const concurrency2 = await item("diff_key_race", {price: 180});
    const concurrency3 = await item("refund_race", {price: 160});
    pass("server-owned catalog fixtures ready");

    const directWallet = await clientPatch(owner.token,
      `wallets/${owner.uid}/balances/gold`, {amount: {integerValue: "999999"}});
    assert(directWallet.status === 403,
      `SEC02 direct wallet mutation not denied: ${JSON.stringify(directWallet.body)}`);
    const directCatalog = await clientPatch(owner.token,
      `item_catalog/${productA}`, {prices: {mapValue: {fields: {
        gold: {integerValue: "1"},
      }}}});
    assert(directCatalog.status === 403, "SEC02 direct catalog mutation not denied");
    pass("direct-client wallet and catalog writes denied by Firestore Rules");

    const purchaseKey = key("basic_purchase");
    const purchase = assertSuccess(await call("commerceTestPurchase", owner.token, {
      itemId: productA, paymentMethod: "gold", idempotencyKey: purchaseKey,
      amount: 1, price: 1, uid: outsider.uid,
    }), "SEC02 server-authoritative purchase");
    assert(purchase.amount === 125 && purchase.accountId === owner.uid,
      "SEC02 purchase price/uid spoof was accepted");
    assert(await walletBalance(db, owner.uid) === 875,
      "SEC02 purchase wallet debited incorrectly");
    assert((await inventory(db, owner.uid, productA)).owned === true,
      "SEC02 purchased entitlement not granted");
    assert(await ledgerCount(db, purchase.transactionId, "commerce_purchase") === 1,
      "SEC02 purchase ledger not unique");
    pass("client-supplied price/uid ignored; debit, grant and ledger atomic");

    const replay = assertSuccess(await call("commerceTestPurchase", owner.token, {
      itemId: productA, paymentMethod: "gold", idempotencyKey: purchaseKey,
    }), "SEC02 replay");
    assert(replay.idempotentReplay === true && replay.applied === false &&
      await walletBalance(db, owner.uid) === 875,
    "SEC02 purchase replay charged twice");
    pass("purchase replay does not charge twice");

    const conflict = await call("commerceTestPurchase", owner.token, {
      itemId: productB, paymentMethod: "gold", idempotencyKey: purchaseKey,
    });
    assert(isConflict(conflict) && await walletBalance(db, owner.uid) === 875,
      "SEC02 conflicting idempotency payload accepted");
    pass("same idempotency key cannot buy a different item");

    const wrongOwner = await call("commerceTestRefund", outsider.token, {
      transactionId: purchase.transactionId, idempotencyKey: key("wrong_owner"),
    });
    assert(wrongOwner.status === 403,
      `SEC02 outsider refunded someone else's purchase: ${JSON.stringify(wrongOwner.body)}`);
    pass("cross-account refund blocked");

    const refundKey = key("basic_refund");
    const refund = assertSuccess(await call("commerceTestRefund", owner.token, {
      transactionId: purchase.transactionId, idempotencyKey: refundKey,
    }), "SEC02 owner refund");
    assert(refund.applied === true && await walletBalance(db, owner.uid) === 1000 &&
      (await inventory(db, owner.uid, productA)).owned === false &&
      await ledgerCount(db, purchase.transactionId, "commerce_refund") === 1,
    "SEC02 owner refund failed to restore balance/revoke entitlement exactly once");
    pass("refund credits exact amount and revokes entitlement once");

    const refundReplay = assertSuccess(await call("commerceTestRefund", owner.token, {
      transactionId: purchase.transactionId, idempotencyKey: refundKey,
    }), "SEC02 owner refund replay");
    assert(refundReplay.idempotentReplay === true &&
      await walletBalance(db, owner.uid) === 1000,
    "SEC02 duplicate refund replay changed funds");
    pass("refund same-key replay idempotent");

    const secondRefund = await call("commerceTestRefund", owner.token, {
      transactionId: purchase.transactionId, idempotencyKey: key("second_refund"),
    });
    assertCallableFailure(secondRefund, "SEC02 second refund distinct key");
    assert(await walletBalance(db, owner.uid) === 1000 &&
      await ledgerCount(db, purchase.transactionId, "commerce_refund") === 1,
    "SEC02 different refund key credited funds twice");
    pass("different refund key cannot create a second refund");

    const inactiveCall = await call("commerceTestPurchase", owner.token, {
      itemId: inactive, paymentMethod: "gold", idempotencyKey: key("inactive"),
    });
    assertCallableFailure(inactiveCall, "SEC02 inactive catalog");
    const invalidPriceCall = await call("commerceTestPurchase", owner.token, {
      itemId: invalidPrice, paymentMethod: "gold", idempotencyKey: key("invalid_price"),
    });
    assertCallableFailure(invalidPriceCall, "SEC02 invalid catalog price");
    pass("inactive catalog and zero-price purchases rejected");

    const invalidCurrency = await call("commerceTestPurchase", owner.token, {
      itemId: productB, paymentMethod: "steam", idempotencyKey: key("steam_invalid"),
    });
    assertCallableFailure(invalidCurrency, "SEC02 unsupported payment method");
    const noAuthPurchase = await call("commerceTestPurchase", null, {
      itemId: productB, paymentMethod: "gold", idempotencyKey: key("missing_auth"),
    });
    assert(noAuthPurchase.status === 401,
      `SEC02 purchase without Auth unexpectedly accepted: ${noAuthPurchase.status}`);
    pass("invalid payment provider and unauthenticated purchase rejected");

    const insufficient = await call("commerceTestPurchase", owner.token, {
      itemId: expensive, paymentMethod: "gold", idempotencyKey: key("insufficient"),
    });
    assertCallableFailure(insufficient, "SEC02 insufficient funds");
    assert(await walletBalance(db, owner.uid) === 1000 &&
      (await inventory(db, owner.uid, expensive)).owned !== true,
    "SEC02 insufficient-funds purchase mutated wallet/inventory");
    pass("insufficient funds cannot mint entitlement or debit wallet");

    // Parallel same request: only the first transaction may actually charge.
    const sameKey = key("parallel_same");
    const sameRequests = await Promise.all(Array.from({length: 3}, () =>
      call("commerceTestPurchase", raceOwner.token, {
        itemId: concurrency1, paymentMethod: "gold", idempotencyKey: sameKey,
      })));
    const sameResults = sameRequests.map((r, ix) =>
      assertSuccess(r, `SEC02 same-key concurrent ${ix}`));
    assert(sameResults.filter((r) => r.applied === true).length === 1 &&
      sameResults.filter((r) => r.idempotentReplay === true).length === 2 &&
      await walletBalance(db, raceOwner.uid) === 850,
    "SEC02 concurrent same-key purchase was not exactly-once");
    assert(await ledgerCount(db, sameResults[0].transactionId,
      "commerce_purchase") === 1,
    "SEC02 concurrent same-key ledger duplicated");
    pass("three simultaneous same-key purchases charge exactly once");

    // Parallel different keys for SAME item: stock/entitlement is unique.
    const diffRequests = await Promise.all(Array.from({length: 3}, (_, ix) =>
      call("commerceTestPurchase", raceOwner.token, {
        itemId: concurrency2, paymentMethod: "gold",
        idempotencyKey: key(`parallel_diff_${ix}`),
      })));
    assert(diffRequests.filter((r) => r.ok).length === 1 &&
      diffRequests.filter((r) => !r.ok && isConflict(r)).length === 2 &&
      await walletBalance(db, raceOwner.uid) === 670 &&
      (await inventory(db, raceOwner.uid, concurrency2)).owned === true,
    `SEC02 same-item concurrent double-purchase: ${JSON.stringify(diffRequests)}`);
    pass("three concurrent distinct-key purchases grant item only once");

    const racePurchase = assertSuccess(await call("commerceTestPurchase",
      raceOwner.token, {itemId: concurrency3, paymentMethod: "gold",
        idempotencyKey: key("refund_purchase")}),
    "SEC02 refundable purchase");
    assert(await walletBalance(db, raceOwner.uid) === 510,
      "SEC02 refundable purchase balance incorrect");
    const sharedRefundKey = key("parallel_refund_same");
    const sameRefunds = await Promise.all(Array.from({length: 3}, () =>
      call("commerceTestRefund", raceOwner.token, {
        transactionId: racePurchase.transactionId,
        idempotencyKey: sharedRefundKey,
      })));
    const sameRefundResults = sameRefunds.map((r, ix) =>
      assertSuccess(r, `SEC02 same-refund concurrent ${ix}`));
    assert(sameRefundResults.filter((r) => r.applied === true).length === 1 &&
      sameRefundResults.filter((r) => r.idempotentReplay === true).length === 2 &&
      await walletBalance(db, raceOwner.uid) === 670 &&
      await ledgerCount(db, racePurchase.transactionId, "commerce_refund") === 1,
    "SEC02 concurrent identical refunds credited funds twice");
    pass("three simultaneous same-key refunds credit once");

    const distinctRefunds = await Promise.all(Array.from({length: 3}, (_, ix) =>
      call("commerceTestRefund", raceOwner.token, {
        transactionId: racePurchase.transactionId,
        idempotencyKey: key(`parallel_refund_other_${ix}`),
      })));
    assert(distinctRefunds.every((r) => !r.ok) &&
      await walletBalance(db, raceOwner.uid) === 670 &&
      await ledgerCount(db, racePurchase.transactionId, "commerce_refund") === 1,
    "SEC02 alternate refund keys credited funds twice");
    pass("different refund keys cannot bypass refunded status");

    const directInventory = await clientPatch(owner.token,
      `inventories/${owner.uid}/items/${productA}`,
      {owned: {booleanValue: true}});
    const directTransaction = await clientPatch(owner.token,
      `commerce_transactions/${purchase.transactionId}`,
      {status: {stringValue: "succeeded"}});
    assert(directInventory.status === 403 && directTransaction.status === 403,
    "SEC02 client directly changed an entitlement or purchase record");
    pass("direct-client inventory and commerce writes denied");

    attempted = true;
    console.log(`ATLASBOARD_SECURITY_CORE_02_ECONOMY_PASS: ${checks}/${checks} checks`);
    console.log("Verified: price authority, immutable ledger, identity isolation, " +
      "parallel purchases, double-refund resistance, client write denial.");
  } finally {
    await cleanup(db, auth, uids, catalogIds, nonce);
    if (!attempted) console.warn(`SEC02 stopped before success at check ${checks}.`);
  }
}

main().catch((error) => {
  console.error("ATLASBOARD_SECURITY_CORE_02_ECONOMY_FAIL:", error);
  process.exitCode = 1;
});
