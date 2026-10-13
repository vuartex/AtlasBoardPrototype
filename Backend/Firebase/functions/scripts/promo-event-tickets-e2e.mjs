/**
 * Phase 3C.4E promo/event tickets E2E, local emulator-only Admin fixtures.
 * Replaces legacy unsigned Firestore REST writes blocked by Rules (403).
 * The callable is ALWAYS exercised as the authenticated temporary user.
 */
import {createHash, randomBytes} from "node:crypto";

const PROJECT = "atlasboard-usa";
const AUTH = "http://127.0.0.1:9099";
const FUNCTIONS = `http://127.0.0.1:5001/${PROJECT}/europe-west1`;
const HUB = "http://127.0.0.1:4400";
const FIRESTORE = `http://127.0.0.1:8080/v1/projects/${PROJECT}/` +
  "databases/(default)/documents";
const nonce = randomBytes(8).toString("hex");
const itemId = `e2e_frame_${nonce}`;
const ticketId = `ticket_e2e_${nonce}`;
const prefix = `PROMOE2E${nonce.toUpperCase()}`;
const code = {
  valid: `${prefix}GOOD`,
  expired: `${prefix}EXPIRED`,
  disabled: `${prefix}DISABLED`,
  future: `${prefix}FUTURE`,
  full: `${prefix}FULL`,
};
const digest = (x) => createHash("sha256").update(x).digest("hex");
const promoId = (value) => digest(`atlasboard:promo:${value.toUpperCase()}`);
const redemptionId = (uid, pid) => digest(`${uid}:promo_redemption:${pid}`);
const redemptionEventId = (rid, key) => digest(`${rid}:event:${key}`);
const walletLedgerId = (uid, key) => digest(`${uid}:${key}`);
const inventoryEventId = (uid, item, key) => digest(`${uid}:${item}:${key}`);
const redeemKey = `promo-redeem-${nonce}`;
let db;
let auth;
let user;
let checks = 0;
const promoIds = [];
const ledgers = [];
let completed = false;

function assert(value, message) { if (!value) throw new Error(message); }
function check(label) { console.log(`[${++checks}/13] PASSED. ${label}`); }
async function request(url, options = {}) {
  const response = await fetch(url, {...options,
    signal: AbortSignal.timeout(25000)});
  const text = await response.text();
  let body;
  try { body = JSON.parse(text); } catch { body = {raw: text}; }
  return {response, body};
}
async function preflight() {
  assert(process.env.FIRESTORE_EMULATOR_HOST === "127.0.0.1:8080",
    "Refusing Admin SDK access without local Firestore Emulator.");
  assert(process.env.FIREBASE_AUTH_EMULATOR_HOST === "127.0.0.1:9099",
    "Refusing Auth access without local Auth Emulator.");
  for (const key of ["GCLOUD_PROJECT", "GOOGLE_CLOUD_PROJECT", "ATLAS_FIREBASE_PROJECT_ID"]) {
    assert(!process.env[key] || process.env[key] === PROJECT,
      `Unapproved project from ${key}.`);
  }
  const response = await request(`${HUB}/emulators`);
  assert(response.response.ok, "Firebase Emulator Hub not available.");
  for (const name of ["auth", "firestore", "functions"]) {
    assert(response.body?.[name]?.host === "127.0.0.1",
      `Unexpected ${name} emulator bind address.`);
  }
  assert(response.body.auth.port === 9099 &&
    response.body.firestore.port === 8080 &&
    response.body.functions.port === 5001,
  "Unexpected local emulator port.");
  // Admin SDK is deliberately loaded AFTER emulator checks.
  const [{getApps, initializeApp}, {getFirestore}, {getAuth}] = await Promise.all([
    import("firebase-admin/app"), import("firebase-admin/firestore"),
    import("firebase-admin/auth"),
  ]);
  if (getApps().length === 0) initializeApp({projectId: PROJECT});
  db = getFirestore();
  auth = getAuth();
  console.log("[0/13] PASSED. Auth/Firestore/Functions Emulators verified; Admin SDK LOCAL.");
}
async function signup() {
  const value = await request(`${AUTH}/identitytoolkit.googleapis.com/v1/` +
    "accounts:signUp?key=atlasboard-local", {
    method: "POST", headers: {"Content-Type": "application/json"},
    body: JSON.stringify({
      email: `atlasboard.promo.${nonce}@example.test`,
      password: "AtlasBoard-Promo-Emulator-123!", returnSecureToken: true,
    }),
  });
  assert(value.response.ok && typeof value.body.idToken === "string" &&
    typeof value.body.localId === "string",
  `Emulator Auth signup failed: ${JSON.stringify(value.body)}`);
  user = {uid: value.body.localId, token: value.body.idToken};
}
async function callable(value, key = redeemKey) {
  return request(`${FUNCTIONS}/promoTestRedeem`, {
    method: "POST",
    headers: {"Content-Type": "application/json",
      Authorization: `Bearer ${user.token}`,
      "X-Firebase-AppCheck": "atlasboard-promo-local-token"},
    body: JSON.stringify({data: {code: value, idempotencyKey: key}}),
  });
}
const result = (x) => x.body?.result;
function assertError(x, status, errorKey) {
  assert(!x.response.ok && x.body?.error?.status === status &&
    x.body?.error?.details?.errorKey === errorKey,
  `Expected ${status}/${errorKey}; got HTTP ${x.response.status}: ` +
    JSON.stringify(x.body));
}
async function seedPromo(value, {active, starts, ends, globalLimit,
  redemptionCount, perAccountLimit, rewards}) {
  const id = promoId(value);
  await db.doc(`promo_codes/${id}`).create({
    promoId: id, codeHash: id, active,
    startsAtEpochMs: starts, endsAtEpochMs: ends,
    globalLimit, redemptionCount, perAccountLimit,
    rewards, schemaVersion: 1,
  });
  promoIds.push(id);
  return id;
}
async function data(path) {
  const doc = await db.doc(path).get();
  return doc.exists ? doc.data() : null;
}
async function exercise() {
  await preflight();
  await signup();
  check("Temporary authenticated account created");
  await db.doc(`item_catalog/${itemId}`).create({
    itemId, itemType: "profile_frame", active: true, schemaVersion: 1,
  });
  const now = Date.now();
  const active = {active: true, starts: now - 60000, ends: now + 3600000,
    globalLimit: 10, redemptionCount: 0, perAccountLimit: 1};
  const validId = await seedPromo(code.valid, {...active, rewards: [
    {type: "gold", amount: 250},
    {type: "atlas_coin", amount: 7},
    {type: "event_ticket", currencyId: ticketId, amount: 2},
    {type: "inventory_item", itemId},
  ]});
  const expiredId = await seedPromo(code.expired, {...active,
    starts: now - 120000, ends: now - 60000,
    rewards: [{type: "gold", amount: 1}]});
  const disabledId = await seedPromo(code.disabled, {...active,
    active: false, rewards: [{type: "gold", amount: 1}]});
  const futureId = await seedPromo(code.future, {...active,
    starts: now + 3600000, ends: now + 7200000,
    rewards: [{type: "gold", amount: 1}]});
  const fullId = await seedPromo(code.full, {...active,
    globalLimit: 1, redemptionCount: 1,
    rewards: [{type: "gold", amount: 1}]});
  check("Catalog and valid/expired/disabled/future/full promos seeded with Admin SDK");

  const rid = redemptionId(user.uid, validId);
  const eid = redemptionEventId(rid, redeemKey);
  // Register deterministic ledger cleanup BEFORE the mutation can succeed.
  // Even a test assertion failure after a real credit cannot leak fixtures.
  const rewardIds = [
    walletLedgerId(user.uid, `promo:${validId}:${eid}:wallet:0`),
    walletLedgerId(user.uid, `promo:${validId}:${eid}:wallet:1`),
    walletLedgerId(user.uid, `promo:${validId}:${eid}:wallet:2`),
  ];
  ledgers.push(...rewardIds);
  const response = await callable(code.valid.toLowerCase());
  assert(response.response.ok && result(response)?.ok === true &&
    result(response)?.applied === true &&
    result(response)?.idempotentReplay === false &&
    result(response)?.promoId === validId &&
    result(response)?.redemptionId === rid &&
    result(response)?.redemptionEventId === eid &&
    result(response)?.redemptionCount === 1 &&
    result(response)?.globalRedemptionCount === 1 &&
    result(response)?.rewards?.length === 4,
  `Valid multi-reward redeem failed: ${JSON.stringify(response.body)}`);
  check("Gold/Atlas Coin/event ticket/frame rewards granted by backend");

  const replay = await callable(code.valid);
  assert(replay.response.ok && result(replay)?.applied === false &&
    result(replay)?.idempotentReplay === true &&
    result(replay)?.redemptionCount === 1 &&
    result(replay)?.globalRedemptionCount === 1,
  "Exact promo replay mutated account/global counters.");
  check("Same-key promo replay does not apply twice");

  const [gold, atlas, ticket, inventory] = await Promise.all([
    data(`wallets/${user.uid}/balances/gold`),
    data(`wallets/${user.uid}/balances/atlas_coin`),
    data(`wallets/${user.uid}/balances/${ticketId}`),
    data(`inventories/${user.uid}/items/${itemId}`),
  ]);
  assert(gold?.amount === 250 && atlas?.amount === 7 &&
    ticket?.amount === 2 && ticket.currencyId === ticketId &&
    ticket.currencyKind === "event" && inventory?.owned === true,
  "Promo balance/entitlement state mismatch.");
  check("All four wallet/currency/inventory reward states match exactly");

  const invEventId = inventoryEventId(user.uid, itemId,
    `promo:${validId}:${eid}:inventory:3`);
  const [goldLedger, atlasLedger, ticketLedger, invEvent, redemption,
    redemptionEvent, promo] = await Promise.all([
    data(`wallet_ledger/${rewardIds[0]}`),
    data(`wallet_ledger/${rewardIds[1]}`),
    data(`wallet_ledger/${rewardIds[2]}`),
    data(`inventories/${user.uid}/items/${itemId}/events/${invEventId}`),
    data(`promo_redemptions/${rid}`),
    data(`promo_redemptions/${rid}/events/${eid}`),
    data(`promo_codes/${validId}`),
  ]);
  assert(goldLedger?.delta === 250 && atlasLedger?.delta === 7 &&
    ticketLedger?.delta === 2 &&
    invEvent?.reason === "promo_redemption" &&
    redemption?.redemptionCount === 1 &&
    redemptionEvent?.promoId === validId && promo?.redemptionCount === 1,
  "Missing/invalid immutable promo ledger or redemption evidence.");
  check("Ledger, inventory event and redemption audit consistent");

  assertError(await callable(code.valid, `account-${nonce}`),
    "RESOURCE_EXHAUSTED", "promo.error.account_limit_reached");
  check("Per-account redemption limit enforced");
  assertError(await callable(code.expired, `expired-${nonce}`),
    "FAILED_PRECONDITION", "promo.error.expired");
  check("Expired promo rejected");
  assertError(await callable(code.disabled, `disabled-${nonce}`),
    "FAILED_PRECONDITION", "promo.error.disabled");
  check("Disabled promo rejected");
  assertError(await callable(code.future, `future-${nonce}`),
    "FAILED_PRECONDITION", "promo.error.not_started");
  check("Not-yet-started promo rejected");
  assertError(await callable(code.full, `full-${nonce}`),
    "RESOURCE_EXHAUSTED", "promo.error.global_limit_reached");
  check("Global redemption limit enforced");

  assert((await data(`wallets/${user.uid}/balances/gold`))?.amount === 250 &&
    (await data(`wallets/${user.uid}/balances/atlas_coin`))?.amount === 7 &&
    (await data(`wallets/${user.uid}/balances/${ticketId}`))?.amount === 2 &&
    (await data(`promo_codes/${validId}`))?.redemptionCount === 1,
  "Rejected promo attempts mutated balances/global counters.");
  for (const id of [expiredId, disabledId, futureId, fullId]) {
    assert(!(await db.doc(`promo_redemptions/${redemptionId(user.uid, id)}`).get()).exists,
      `Rejected promo unexpectedly created redemption record: ${id}`);
  }
  // Authenticated client still forbidden to write a catalog/promo definition.
  const unauthorized = await request(`${FIRESTORE}/promo_codes/${validId}`, {
    method: "PATCH", headers: {"Content-Type": "application/json",
      Authorization: `Bearer ${user.token}`},
    body: JSON.stringify({fields: {active: {booleanValue: false}}}),
  });
  assert(unauthorized.response.status === 403,
    `Authenticated client was able to modify promo: HTTP ${unauthorized.response.status}`);
  check("Replays/rejections have no side effects; direct promo writes denied");
  assert(checks === 12, "Promo pre-cleanup count mismatch.");
  completed = true;
}
async function cleanup() {
  const warnings = [];
  const safe = async (label, work) => {
    try { await work(); } catch (error) { warnings.push(`${label}: ${String(error)}`); }
  };
  if (db) {
    if (user) {
      for (const base of ["wallets", "inventories"]) {
        await safe(`cleanup ${base}`, () =>
          db.recursiveDelete(db.doc(`${base}/${user.uid}`)));
      }
      const rid = redemptionId(user.uid, promoId(code.valid));
      await safe("cleanup redemption", () =>
        db.recursiveDelete(db.doc(`promo_redemptions/${rid}`)));
    }
    for (const id of promoIds) {
      await safe(`cleanup promo ${id}`, () =>
        db.doc(`promo_codes/${id}`).delete());
    }
    for (const id of ledgers) {
      await safe(`cleanup ledger ${id}`, () =>
        db.doc(`wallet_ledger/${id}`).delete());
    }
    await safe("cleanup catalog", () => db.doc(`item_catalog/${itemId}`).delete());
  }
  if (user && auth) await safe("cleanup Auth account", () => auth.deleteUser(user.uid));
  if (warnings.length) throw new Error(`Promo cleanup warnings:\n${warnings.join("\n")}`);
  if (db || user) console.log("Promo local fixture cleanup complete.");
  else console.log("Promo cleanup not required (preflight denied all access).");
}
if (process.argv.includes("--self-test")) {
  assert(promoId("TEST") === digest("atlasboard:promo:TEST") &&
    redemptionId("x", "y") === digest("x:promo_redemption:y") &&
    ticketId.startsWith("ticket_"),
  "Promo id/fixture offline self-test failed.");
  console.log("Promo E2E offline self-test PASS.");
} else {
  const failures = [];
  try { await exercise(); } catch (error) { failures.push(error); }
  try { await cleanup(); } catch (error) { failures.push(error); }
  if (failures.length > 0) {
    for (const failure of failures) {
      console.error("AtlasBoard Promo Codes + Event Tickets Local E2E FAILED:", failure);
    }
    process.exitCode = 1;
  } else if (completed) {
    assert(checks === 12, "Promo checks unexpectedly changed before cleanup.");
    console.log("[13/13] PASSED. Local fixtures and Auth account cleaned.");
    console.log("AtlasBoard Promo Codes + Event Tickets Local E2E v2 PASSED: 13/13.");
  }
}
