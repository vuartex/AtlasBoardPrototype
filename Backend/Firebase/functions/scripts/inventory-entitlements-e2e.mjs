/**
 * Phase 3C.4C inventory regression: emulator-only, Admin SDK fixtures.
 * Never relax Firestore Security Rules to seed trusted test data.
 * Run via node scripts/qa.mjs --full (the Firebase CLI starts emulators).
 */
import {createHash, randomBytes} from "node:crypto";

const PROJECT = "atlasboard-usa";
const AUTH = "http://127.0.0.1:9099";
const FUNCTIONS = `http://127.0.0.1:5001/${PROJECT}/europe-west1`;
const HUB = "http://127.0.0.1:4400";
const FIRESTORE = `http://127.0.0.1:8080/v1/projects/${PROJECT}/` +
  "databases/(default)/documents";
const nonce = randomBytes(8).toString("hex");
const itemId = `e2e_pawn_${nonce}`;
const missingId = `e2e_unknown_${nonce}`;
const digest = (value) => createHash("sha256").update(value).digest("hex");
const eventId = (uid, item, key) => digest(`${uid}:${item}:${key}`);
const TOKEN = "atlasboard-inventory-local-emulator-token";
let db;
let auth;
let user;
let checks = 0;

function assert(condition, description) {
  if (!condition) throw new Error(description);
}
function check(description) {
  console.log(`[${++checks}/11] PASSED. ${description}`);
}
async function request(url, options = {}) {
  const response = await fetch(url, {
    ...options,
    signal: AbortSignal.timeout(25000),
  });
  const text = await response.text();
  let body;
  try { body = JSON.parse(text); } catch { body = {raw: text}; }
  return {response, body};
}
async function preflight() {
  // Check ALL runtime boundaries before loading Admin SDK or performing writes.
  assert(process.env.FIRESTORE_EMULATOR_HOST === "127.0.0.1:8080",
    "Firestore Emulator MUST be 127.0.0.1:8080.");
  assert(process.env.FIREBASE_AUTH_EMULATOR_HOST === "127.0.0.1:9099",
    "Auth Emulator MUST be 127.0.0.1:9099.");
  for (const key of ["GCLOUD_PROJECT", "GOOGLE_CLOUD_PROJECT", "ATLAS_FIREBASE_PROJECT_ID"]) {
    assert(!process.env[key] || process.env[key] === PROJECT,
      `${key} must not select a production project.`);
  }
  const hub = await request(`${HUB}/emulators`);
  assert(hub.response.ok, "Local Firebase Emulator Hub is unavailable.");
  for (const name of ["auth", "firestore", "functions"]) {
    assert(hub.body?.[name]?.host === "127.0.0.1",
      `${name} emulator must be bound to 127.0.0.1.`);
  }
  assert(hub.body.auth.port === 9099 && hub.body.firestore.port === 8080 &&
    hub.body.functions.port === 5001,
  "Unexpected emulator port(s).");
  const [{getApps, initializeApp}, {getFirestore}, {getAuth}] = await Promise.all([
    import("firebase-admin/app"),
    import("firebase-admin/firestore"),
    import("firebase-admin/auth"),
  ]);
  if (getApps().length === 0) initializeApp({projectId: PROJECT});
  db = getFirestore();
  auth = getAuth();
  await db.doc("atlasboard_qa_health/inventory_readonly").get();
  console.log("[0/11] PASSED. Auth/Firestore/Functions Emulator isolation.");
}
async function signup() {
  const out = await request(`${AUTH}/identitytoolkit.googleapis.com/v1/` +
    "accounts:signUp?key=atlasboard-local", {
    method: "POST", headers: {"Content-Type": "application/json"},
    body: JSON.stringify({
      email: `atlasboard.inventory.${nonce}@example.test`,
      password: "AtlasBoard-Inventory-Emulator-123!",
      returnSecureToken: true,
    }),
  });
  assert(out.response.ok && typeof out.body.idToken === "string" &&
    typeof out.body.localId === "string", `Auth signup failed: ${JSON.stringify(out.body)}`);
  user = {uid: out.body.localId, token: out.body.idToken};
}
async function call(data) {
  return request(`${FUNCTIONS}/inventoryTestMutation`, {
    method: "POST",
    headers: {"Content-Type": "application/json",
      Authorization: `Bearer ${user.token}`, "X-Firebase-AppCheck": TOKEN},
    body: JSON.stringify({data}),
  });
}
function result(out) { return out.body?.result; }
function errorKey(out) { return out.body?.error?.details?.errorKey; }
function mutation(operation, key) {
  return {
    operation, itemId, reason: `test_${key.split("-")[0]}`,
    transactionId: `inventory-${operation}-tx-${key}`,
    idempotencyKey: `inventory-e2e-${key}`,
  };
}
async function exercise() {
  await preflight();
  await signup();
  check("Authenticated temporary Auth Emulator account created");
  await db.doc(`item_catalog/${itemId}`).create({
    itemId, itemType: "pawn", active: true, schemaVersion: 1,
  });
  check("Server-authorized catalog fixture created without relaxing Rules");
  const grantInput = mutation("grant", `grant-${nonce}`);
  const grantId = eventId(user.uid, itemId, grantInput.idempotencyKey);
  const grant = await call(grantInput);
  assert(grant.response.ok && result(grant)?.ok === true &&
    result(grant)?.applied === true && result(grant)?.idempotentReplay === false &&
    result(grant)?.ownedBefore === false && result(grant)?.ownedAfter === true &&
    result(grant)?.itemType === "pawn" && result(grant)?.eventId === grantId,
  `Grant failed: ${JSON.stringify(grant.body)}`);
  check("Not-owned entitlement granted exactly once by authenticated backend");
  const replay = await call(grantInput);
  assert(replay.response.ok && result(replay)?.applied === false &&
    result(replay)?.idempotentReplay === true && result(replay)?.eventId === grantId,
  "Grant replay was not idempotent.");
  check("Exact grant replay idempotent");
  const duplicateInput = mutation("grant", `duplicate-${nonce}`);
  const duplicate = await call(duplicateInput);
  assert(!duplicate.response.ok &&
    duplicate.body?.error?.status === "ALREADY_EXISTS" &&
    errorKey(duplicate) === "inventory.error.already_owned" &&
    !(await db.doc(`inventories/${user.uid}/items/${itemId}/events/` +
      eventId(user.uid, itemId, duplicateInput.idempotencyKey)).get()).exists,
  `Duplicate grant not rejected safely: ${JSON.stringify(duplicate.body)}`);
  check("Different-key duplicate entitlement denied without event");
  const itemRef = db.doc(`inventories/${user.uid}/items/${itemId}`);
  const item = await itemRef.get();
  const evidence = await itemRef.collection("events").doc(grantId).get();
  assert(item.data()?.uid === user.uid && item.data()?.itemId === itemId &&
    item.data()?.itemType === "pawn" && item.data()?.owned === true &&
    item.data()?.quantity === 1 && evidence.exists &&
    evidence.data()?.operation === "grant" &&
    evidence.data()?.ownedBefore === false && evidence.data()?.ownedAfter === true,
  "Grant item or immutable event evidence mismatch.");
  check("Inventory ownership and immutable grant evidence stored");
  const revokeInput = mutation("revoke", `revoke-${nonce}`);
  const revoke = await call(revokeInput);
  assert(revoke.response.ok && result(revoke)?.applied === true &&
    result(revoke)?.ownedBefore === true && result(revoke)?.ownedAfter === false &&
    result(revoke)?.eventId === eventId(user.uid, itemId, revokeInput.idempotencyKey),
  `Revoke failed: ${JSON.stringify(revoke.body)}`);
  check("Owned entitlement revoked");
  const revokeReplay = await call(revokeInput);
  const revoked = await itemRef.get();
  const revokedEvent = await itemRef.collection("events")
    .doc(eventId(user.uid, itemId, revokeInput.idempotencyKey)).get();
  assert(revokeReplay.response.ok && result(revokeReplay)?.applied === false &&
    result(revokeReplay)?.idempotentReplay === true &&
    revoked.data()?.owned === false && revoked.data()?.quantity === 0 &&
    revokedEvent.data()?.operation === "revoke",
  "Revoke replay or evidence inconsistent.");
  check("Revoke replay idempotent; ownership and audit updated");
  const regrantInput = mutation("grant", `regrant-${nonce}`);
  const regrant = await call(regrantInput);
  assert(regrant.response.ok && result(regrant)?.applied === true &&
    result(regrant)?.ownedBefore === false && result(regrant)?.ownedAfter === true &&
    (await itemRef.collection("events")
      .doc(eventId(user.uid, itemId, regrantInput.idempotencyKey)).get()).exists,
  `Regrant failed: ${JSON.stringify(regrant.body)}`);
  check("Previously revoked item re-granted and audited");
  const unknownInput = {
    ...mutation("grant", `unknown-${nonce}`), itemId: missingId,
  };
  const unknown = await call(unknownInput);
  assert(!unknown.response.ok && unknown.body?.error?.status === "NOT_FOUND" &&
    errorKey(unknown) === "inventory.error.catalog_item_not_found" &&
    !(await db.doc(`inventories/${user.uid}/items/${missingId}`).get()).exists &&
    !(await db.doc(`inventories/${user.uid}/items/${missingId}/events/` +
      eventId(user.uid, missingId, unknownInput.idempotencyKey)).get()).exists,
  `Unknown catalog item accepted: ${JSON.stringify(unknown.body)}`);
  check("Unknown catalog item rejected without partial documents");
  const final = await itemRef.get();
  assert(final.data()?.owned === true && final.data()?.quantity === 1 &&
    final.data()?.lastTransactionId === regrantInput.transactionId,
  "Final ownership state inconsistent.");
  // Authenticated client MUST NOT be able to mutate catalog (403) even when
  // the Admin SDK is authorized to seed this same document.
  const clientPatch = await request(`${FIRESTORE}/item_catalog/${itemId}`, {
    method: "PATCH",
    headers: {"Content-Type": "application/json",
      Authorization: `Bearer ${user.token}`},
    body: JSON.stringify({fields: {active: {booleanValue: false}}}),
  });
  assert(clientPatch.response.status === 403,
    `Authenticated client was able to modify catalog: HTTP ${clientPatch.response.status}`);
  check("Final inventory state correct; direct-client catalog write denied (403)");
  assert(checks === 11, "Inventory 11-check total mismatch.");
  console.log("AtlasBoard Inventory + Entitlements Local E2E v2 PASSED: 11/11.");
}
async function cleanup() {
  const warnings = [];
  const safe = async (label, work) => {
    try { await work(); } catch (e) { warnings.push(`${label}: ${String(e)}`); }
  };
  if (db) {
    await safe("temporary inventory", async () => {
      if (user) await db.recursiveDelete(db.doc(`inventories/${user.uid}`));
    });
    await safe("temporary catalog", () => db.doc(`item_catalog/${itemId}`).delete());
  }
  if (user && auth) await safe("temporary Auth user", () => auth.deleteUser(user.uid));
  if (warnings.length) throw new Error(`Inventory fixture cleanup errors:\n${warnings.join("\n")}`);
  if (db || user) console.log("Inventory local fixture cleanup complete.");
  else console.log("Inventory cleanup not required (preflight denied all access).");
}
if (process.argv.includes("--self-test")) {
  assert(digest("x").length === 64 && eventId("a", "b", "c") === digest("a:b:c"),
    "Inventory deterministic id fixture failed.");
  console.log("Inventory E2E offline self-test PASS.");
} else {
  const failures = [];
  try { await exercise(); } catch (e) { failures.push(e); }
  try { await cleanup(); } catch (e) { failures.push(e); }
  if (failures.length > 0) {
    for (const failure of failures) {
      console.error("AtlasBoard Inventory + Entitlements Local E2E FAILED:", failure);
    }
    process.exitCode = 1;
  }
}
