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

function normalizePromoCode(code) {
  return code.trim().toUpperCase();
}

function promoId(code) {
  return sha256(`atlasboard:promo:${normalizePromoCode(code)}`);
}

function redemptionId(uid, promoDocumentId) {
  return sha256(`${uid}:promo_redemption:${promoDocumentId}`);
}

function redemptionEventId(redemptionDocumentId, idempotencyKey) {
  return sha256(`${redemptionDocumentId}:event:${idempotencyKey}`);
}

function walletLedgerId(uid, idempotencyKey) {
  return sha256(`${uid}:${idempotencyKey}`);
}

function inventoryEventId(uid, itemId, idempotencyKey) {
  return sha256(`${uid}:${itemId}:${idempotencyKey}`);
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

async function seedPromo(
  code,
  {
    active,
    startsAtEpochMs,
    endsAtEpochMs,
    globalLimit,
    redemptionCount,
    perAccountLimit,
    rewards,
  },
) {
  const id = promoId(code);

  const result = await patchFirestoreDocument(
    `promo_codes/${id}`,
    {
      promoId: {stringValue: id},
      codeHash: {stringValue: id},
      active: {booleanValue: active},
      startsAtEpochMs: {integerValue: String(startsAtEpochMs)},
      endsAtEpochMs: {integerValue: String(endsAtEpochMs)},
      globalLimit: {integerValue: String(globalLimit)},
      redemptionCount: {integerValue: String(redemptionCount)},
      perAccountLimit: {integerValue: String(perAccountLimit)},
      rewards: rewardArrayValue(rewards),
      schemaVersion: {integerValue: "1"},
    },
  );

  assert(
    result.response.ok,
    `Promo seed failed for ${code}: ${result.text}`,
  );

  return id;
}

async function expectCallableError(
  call,
  expectedStatus,
  expectedErrorKey,
  label,
) {
  assert(
    !call.response.ok,
    `${label} unexpectedly succeeded.`,
  );
  assert(
    call.json?.error?.status === expectedStatus,
    `${label}: expected ${expectedStatus}, got ` +
      `${call.json?.error?.status}.`,
  );
  assert(
    call.json?.error?.details?.errorKey === expectedErrorKey,
    `${label}: expected errorKey=${expectedErrorKey}, got ` +
      `${call.json?.error?.details?.errorKey}.`,
  );
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
const compactNonce = Date.now().toString(36).toUpperCase();
const email = `atlasboard.promo.e2e.${nonce}@example.com`;
const password = `AtlasBoardPromoE2E!${nonce}`;

const validCode = `ATLAS-${compactNonce}`;
const expiredCode = `OLD-${compactNonce}`;
const disabledCode = `OFF-${compactNonce}`;
const futureCode = `NEXT-${compactNonce}`;
const globalFullCode = `FULL-${compactNonce}`;

const itemId = `e2e_frame_${nonce}`;
const ticketCurrencyId = "ticket_winter_2027";
const redeemKey = `promo-redeem-${nonce}`;
const accountLimitKey = `promo-account-limit-${nonce}`;

let idToken = null;
let localId = null;
let validPromoId = null;
let expiredPromoId = null;
let disabledPromoId = null;
let futurePromoId = null;
let globalFullPromoId = null;
let validRedemptionId = null;
let validRedemptionEventId = null;
let goldLedgerId = null;
let atlasCoinLedgerId = null;
let ticketLedgerId = null;
let frameEventId = null;
let testPassed = false;

console.log("AtlasBoard Promo Codes + Event Tickets Local E2E v1");
console.log(
  "Safety: localhost Auth/Firestore/Functions emulators only; " +
  "promo, wallet, event-ticket, entitlement, and audit data are temporary.",
);

try {
  console.log("[0/13] Verifying emulator safety preflight...");
  await verifyEmulatorPreflight();
  console.log("[0/13] PASSED. Auth/Firestore/Functions emulators detected.");

  console.log("[1/13] Creating temporary Auth Emulator user...");
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

  console.log(`[1/13] PASSED. Local UID=${localId}`);

  console.log("[2/13] Seeding catalog item and promo definitions...");
  const catalogSeed = await patchFirestoreDocument(
    `item_catalog/${itemId}`,
    {
      itemId: {stringValue: itemId},
      itemType: {stringValue: "profile_frame"},
      active: {booleanValue: true},
      schemaVersion: {integerValue: "1"},
    },
  );
  assert(
    catalogSeed.response.ok,
    `Catalog seed failed: ${catalogSeed.text}`,
  );

  const now = Date.now();
  const validRewards = [
    {type: "gold", amount: 250},
    {type: "atlas_coin", amount: 7},
    {
      type: "event_ticket",
      currencyId: ticketCurrencyId,
      amount: 2,
    },
    {type: "inventory_item", itemId},
  ];

  validPromoId = await seedPromo(
    validCode,
    {
      active: true,
      startsAtEpochMs: now - 60_000,
      endsAtEpochMs: now + 3_600_000,
      globalLimit: 10,
      redemptionCount: 0,
      perAccountLimit: 1,
      rewards: validRewards,
    },
  );

  expiredPromoId = await seedPromo(
    expiredCode,
    {
      active: true,
      startsAtEpochMs: now - 120_000,
      endsAtEpochMs: now - 60_000,
      globalLimit: 10,
      redemptionCount: 0,
      perAccountLimit: 1,
      rewards: [{type: "gold", amount: 1}],
    },
  );

  disabledPromoId = await seedPromo(
    disabledCode,
    {
      active: false,
      startsAtEpochMs: now - 60_000,
      endsAtEpochMs: now + 3_600_000,
      globalLimit: 10,
      redemptionCount: 0,
      perAccountLimit: 1,
      rewards: [{type: "gold", amount: 1}],
    },
  );

  futurePromoId = await seedPromo(
    futureCode,
    {
      active: true,
      startsAtEpochMs: now + 3_600_000,
      endsAtEpochMs: now + 7_200_000,
      globalLimit: 10,
      redemptionCount: 0,
      perAccountLimit: 1,
      rewards: [{type: "gold", amount: 1}],
    },
  );

  globalFullPromoId = await seedPromo(
    globalFullCode,
    {
      active: true,
      startsAtEpochMs: now - 60_000,
      endsAtEpochMs: now + 3_600_000,
      globalLimit: 1,
      redemptionCount: 1,
      perAccountLimit: 1,
      rewards: [{type: "gold", amount: 1}],
    },
  );

  validRedemptionId = redemptionId(localId, validPromoId);
  validRedemptionEventId = redemptionEventId(
    validRedemptionId,
    redeemKey,
  );

  goldLedgerId = walletLedgerId(
    localId,
    `promo:${validPromoId}:${validRedemptionEventId}:wallet:0`,
  );
  atlasCoinLedgerId = walletLedgerId(
    localId,
    `promo:${validPromoId}:${validRedemptionEventId}:wallet:1`,
  );
  ticketLedgerId = walletLedgerId(
    localId,
    `promo:${validPromoId}:${validRedemptionEventId}:wallet:2`,
  );
  frameEventId = inventoryEventId(
    localId,
    itemId,
    `promo:${validPromoId}:${validRedemptionEventId}:inventory:3`,
  );

  console.log("[2/13] PASSED. Valid/expired/disabled/future/full promos seeded.");

  console.log("[3/13] Redeeming valid multi-reward promo...");
  const redeem = await callFunction(
    "promoTestRedeem",
    idToken,
    {
      code: validCode.toLowerCase(),
      idempotencyKey: redeemKey,
    },
  );

  assert(
    redeem.response.ok,
    `Valid promo redemption failed: HTTP ${redeem.response.status} ` +
      redeem.text,
  );

  const redeemResult = getCallableResult(redeem);
  assert(redeemResult?.ok === true, "Promo redemption returned ok != true.");
  assert(redeemResult?.applied === true, "Promo redemption was not applied.");
  assert(
    redeemResult?.idempotentReplay === false,
    "First promo redemption was incorrectly marked replay.",
  );
  assert(
    redeemResult?.promoId === validPromoId,
    "Promo id mismatch.",
  );
  assert(
    redeemResult?.redemptionId === validRedemptionId,
    "Promo redemption id mismatch.",
  );
  assert(
    redeemResult?.redemptionEventId === validRedemptionEventId,
    "Promo redemption event id mismatch.",
  );
  assert(
    redeemResult?.redemptionCount === 1,
    "Expected account redemption count=1.",
  );
  assert(
    redeemResult?.globalRedemptionCount === 1,
    "Expected global redemption count=1.",
  );
  assert(
    Array.isArray(redeemResult?.rewards) &&
      redeemResult.rewards.length === 4,
    "Expected exactly four promo rewards.",
  );

  console.log(
    "[3/13] PASSED. Gold + Atlas Coin + event ticket + profile frame granted.",
  );

  console.log("[4/13] Replaying exact promo idempotency key...");
  const replay = await callFunction(
    "promoTestRedeem",
    idToken,
    {
      code: validCode,
      idempotencyKey: redeemKey,
    },
  );

  assert(
    replay.response.ok,
    `Promo replay failed: HTTP ${replay.response.status} ${replay.text}`,
  );

  const replayResult = getCallableResult(replay);
  assert(replayResult?.applied === false,
    "Promo replay applied rewards again.");
  assert(replayResult?.idempotentReplay === true,
    "Promo replay not identified as idempotent.");
  assert(replayResult?.redemptionCount === 1,
    "Promo replay changed account redemption count.");
  assert(replayResult?.globalRedemptionCount === 1,
    "Promo replay changed global redemption count.");

  console.log("[4/13] PASSED. Exact replay did not duplicate any reward.");

  console.log("[5/13] Verifying wallet, ticket, and inventory rewards...");
  const goldBalance = await getFirestoreDocument(
    `wallets/${localId}/balances/gold`,
  );
  const atlasBalance = await getFirestoreDocument(
    `wallets/${localId}/balances/atlas_coin`,
  );
  const ticketBalance = await getFirestoreDocument(
    `wallets/${localId}/balances/${ticketCurrencyId}`,
  );
  const inventory = await getFirestoreDocument(
    `inventories/${localId}/items/${itemId}`,
  );

  assert(goldBalance.response.ok, "Gold promo balance missing.");
  assert(atlasBalance.response.ok, "Atlas Coin promo balance missing.");
  assert(ticketBalance.response.ok, "Event ticket promo balance missing.");
  assert(inventory.response.ok, "Promo inventory entitlement missing.");

  assert(firestoreInteger(goldBalance.json, "amount") === 250,
    "Expected Gold balance=250.");
  assert(firestoreInteger(atlasBalance.json, "amount") === 7,
    "Expected Atlas Coin balance=7.");
  assert(firestoreInteger(ticketBalance.json, "amount") === 2,
    "Expected event ticket balance=2.");
  assert(
    firestoreString(ticketBalance.json, "currencyId") === ticketCurrencyId,
    "Event ticket currency id mismatch.",
  );
  assert(
    firestoreString(ticketBalance.json, "currencyKind") === "event",
    "Event ticket currency kind mismatch.",
  );
  assert(firestoreBoolean(inventory.json, "owned") === true,
    "Promo profile frame is not owned.");

  console.log("[5/13] PASSED. All four reward states verified directly.");

  console.log("[6/13] Verifying immutable reward and redemption audit records...");
  const goldLedger = await getFirestoreDocument(
    `wallet_ledger/${goldLedgerId}`,
  );
  const atlasLedger = await getFirestoreDocument(
    `wallet_ledger/${atlasCoinLedgerId}`,
  );
  const ticketLedger = await getFirestoreDocument(
    `wallet_ledger/${ticketLedgerId}`,
  );
  const inventoryEvent = await getFirestoreDocument(
    `inventories/${localId}/items/${itemId}/events/${frameEventId}`,
  );
  const redemption = await getFirestoreDocument(
    `promo_redemptions/${validRedemptionId}`,
  );
  const redemptionEvent = await getFirestoreDocument(
    `promo_redemptions/${validRedemptionId}/events/${validRedemptionEventId}`,
  );
  const validPromo = await getFirestoreDocument(
    `promo_codes/${validPromoId}`,
  );

  assert(goldLedger.response.ok, "Gold promo ledger missing.");
  assert(atlasLedger.response.ok, "Atlas Coin promo ledger missing.");
  assert(ticketLedger.response.ok, "Ticket promo ledger missing.");
  assert(inventoryEvent.response.ok, "Promo inventory event missing.");
  assert(redemption.response.ok, "Promo redemption aggregate missing.");
  assert(redemptionEvent.response.ok, "Promo redemption event missing.");
  assert(validPromo.response.ok, "Valid promo document missing.");

  assert(firestoreInteger(goldLedger.json, "delta") === 250,
    "Gold promo ledger delta mismatch.");
  assert(firestoreInteger(atlasLedger.json, "delta") === 7,
    "Atlas Coin promo ledger delta mismatch.");
  assert(firestoreInteger(ticketLedger.json, "delta") === 2,
    "Ticket promo ledger delta mismatch.");
  assert(
    firestoreString(inventoryEvent.json, "reason") === "promo_redemption",
    "Promo inventory event reason mismatch.",
  );
  assert(firestoreInteger(redemption.json, "redemptionCount") === 1,
    "Account promo redemption count mismatch.");
  assert(
    firestoreString(redemptionEvent.json, "promoId") === validPromoId,
    "Promo redemption event promo id mismatch.",
  );
  assert(firestoreInteger(validPromo.json, "redemptionCount") === 1,
    "Global promo redemption count mismatch.");

  console.log(
    "[6/13] PASSED. Wallet ledger + inventory event + redemption audit verified.",
  );

  console.log("[7/13] Testing per-account redemption limit...");
  const accountLimit = await callFunction(
    "promoTestRedeem",
    idToken,
    {
      code: validCode,
      idempotencyKey: accountLimitKey,
    },
  );
  await expectCallableError(
    accountLimit,
    "RESOURCE_EXHAUSTED",
    "promo.error.account_limit_reached",
    "Account-limit redemption",
  );
  console.log("[7/13] PASSED. Per-account limit rejected second redemption.");

  console.log("[8/13] Testing expired promo rejection...");
  const expired = await callFunction(
    "promoTestRedeem",
    idToken,
    {
      code: expiredCode,
      idempotencyKey: `expired-${nonce}`,
    },
  );
  await expectCallableError(
    expired,
    "FAILED_PRECONDITION",
    "promo.error.expired",
    "Expired promo",
  );
  console.log("[8/13] PASSED. Expired promo rejected safely.");

  console.log("[9/13] Testing disabled promo rejection...");
  const disabled = await callFunction(
    "promoTestRedeem",
    idToken,
    {
      code: disabledCode,
      idempotencyKey: `disabled-${nonce}`,
    },
  );
  await expectCallableError(
    disabled,
    "FAILED_PRECONDITION",
    "promo.error.disabled",
    "Disabled promo",
  );
  console.log("[9/13] PASSED. Disabled promo rejected safely.");

  console.log("[10/13] Testing not-started promo rejection...");
  const future = await callFunction(
    "promoTestRedeem",
    idToken,
    {
      code: futureCode,
      idempotencyKey: `future-${nonce}`,
    },
  );
  await expectCallableError(
    future,
    "FAILED_PRECONDITION",
    "promo.error.not_started",
    "Future promo",
  );
  console.log("[10/13] PASSED. Future promo rejected before start time.");

  console.log("[11/13] Testing exhausted global redemption limit...");
  const globalFull = await callFunction(
    "promoTestRedeem",
    idToken,
    {
      code: globalFullCode,
      idempotencyKey: `global-${nonce}`,
    },
  );
  await expectCallableError(
    globalFull,
    "RESOURCE_EXHAUSTED",
    "promo.error.global_limit_reached",
    "Global-limit promo",
  );
  console.log("[11/13] PASSED. Exhausted global promo rejected safely.");

  console.log("[12/13] Verifying rejected attempts caused no reward mutation...");
  const finalGold = await getFirestoreDocument(
    `wallets/${localId}/balances/gold`,
  );
  const finalAtlas = await getFirestoreDocument(
    `wallets/${localId}/balances/atlas_coin`,
  );
  const finalTicket = await getFirestoreDocument(
    `wallets/${localId}/balances/${ticketCurrencyId}`,
  );
  const finalPromo = await getFirestoreDocument(
    `promo_codes/${validPromoId}`,
  );
  const rejectedPromoIds = [
    expiredPromoId,
    disabledPromoId,
    futurePromoId,
    globalFullPromoId,
  ];

  assert(firestoreInteger(finalGold.json, "amount") === 250,
    "Rejected promo changed Gold balance.");
  assert(firestoreInteger(finalAtlas.json, "amount") === 7,
    "Rejected promo changed Atlas Coin balance.");
  assert(firestoreInteger(finalTicket.json, "amount") === 2,
    "Rejected promo changed event ticket balance.");
  assert(firestoreInteger(finalPromo.json, "redemptionCount") === 1,
    "Rejected/replayed promo changed valid global count.");

  for (const id of rejectedPromoIds) {
    const rejectedRedemption = redemptionId(localId, id);
    const rejectedDoc = await getFirestoreDocument(
      `promo_redemptions/${rejectedRedemption}`,
    );
    assert(
      rejectedDoc.response.status === 404,
      `Rejected promo unexpectedly created redemption state: ${id}`,
    );
  }

  console.log(
    "[12/13] PASSED. Replays/rejections left balances and audit counts stable.",
  );

  console.log("[13/13] Cleaning temporary emulator data...");
  testPassed = true;
} catch (error) {
  console.error("\nAtlasBoard Promo Codes + Event Tickets Local E2E v1 FAILED.");
  console.error(error);
  process.exitCode = 1;
} finally {
  const cleanupPaths = [];

  if (localId) {
    cleanupPaths.push(
      `wallets/${localId}/balances/gold`,
      `wallets/${localId}/balances/atlas_coin`,
      `wallets/${localId}/balances/${ticketCurrencyId}`,
      `inventories/${localId}/items/${itemId}/events/${frameEventId}`,
      `inventories/${localId}/items/${itemId}`,
    );
  }

  if (goldLedgerId) {
    cleanupPaths.push(`wallet_ledger/${goldLedgerId}`);
  }
  if (atlasCoinLedgerId) {
    cleanupPaths.push(`wallet_ledger/${atlasCoinLedgerId}`);
  }
  if (ticketLedgerId) {
    cleanupPaths.push(`wallet_ledger/${ticketLedgerId}`);
  }

  if (validRedemptionId && validRedemptionEventId) {
    cleanupPaths.push(
      `promo_redemptions/${validRedemptionId}/events/${validRedemptionEventId}`,
      `promo_redemptions/${validRedemptionId}`,
    );
  }

  if (localId) {
    for (const id of [
      expiredPromoId,
      disabledPromoId,
      futurePromoId,
      globalFullPromoId,
    ]) {
      if (id) {
        cleanupPaths.push(
          `promo_redemptions/${redemptionId(localId, id)}`,
        );
      }
    }
  }

  cleanupPaths.push(`item_catalog/${itemId}`);

  for (const id of [
    validPromoId,
    expiredPromoId,
    disabledPromoId,
    futurePromoId,
    globalFullPromoId,
  ]) {
    if (id) {
      cleanupPaths.push(`promo_codes/${id}`);
    }
  }

  for (const path of cleanupPaths) {
    await bestEffortDeleteFirestore(path);
  }

  if (idToken) {
    try {
      const deleteAccount = await postJson(
        `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/accounts:delete?key=${API_KEY}`,
        {idToken},
      );
      if (!deleteAccount.response.ok) {
        console.warn(
          "Temporary Auth Emulator user cleanup warning: " +
            deleteAccount.text,
        );
      }
    } catch (error) {
      console.warn("Temporary Auth Emulator user cleanup warning:", error);
    }
  }

  console.log("[cleanup] Cleanup finished.");

  if (testPassed) {
    console.log(
      "\nAtlasBoard Promo Codes + Event Tickets Local E2E v1 PASSED.",
    );
    console.log(
      "Verified: hashed promo lookup -> active/time/limit enforcement -> " +
      "atomic Gold + Atlas Coin + event ticket + entitlement rewards -> " +
      "idempotent replay -> immutable audit -> rejected-attempt safety -> cleanup.",
    );
  }
}
