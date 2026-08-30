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

function eventId(uid, itemId, idempotencyKey) {
  return createHash("sha256")
    .update(`${uid}:${itemId}:${idempotencyKey}`, "utf8")
    .digest("hex");
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

  const firestoreHost =
    `${emulators.firestore.host}:${emulators.firestore.port}`;
  assert(
    firestoreHost === "127.0.0.1:8080" ||
      firestoreHost === "localhost:8080",
    `Unexpected Firestore Emulator address: ${firestoreHost}`,
  );
}

const nonce = `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`;
const email = `atlasboard.inventory.e2e.${nonce}@example.com`;
const password = `AtlasBoardInventoryE2E!${nonce}`;
const itemId = `e2e_pawn_${nonce}`;
const unknownItemId = `e2e_unknown_${nonce}`;

const grantKey = `inventory-e2e-grant-${nonce}`;
const duplicateGrantKey = `inventory-e2e-duplicate-${nonce}`;
const revokeKey = `inventory-e2e-revoke-${nonce}`;
const regrantKey = `inventory-e2e-regrant-${nonce}`;
const unknownKey = `inventory-e2e-unknown-${nonce}`;

const grantTransactionId = `inventory-grant-tx-${nonce}`;
const duplicateTransactionId = `inventory-duplicate-tx-${nonce}`;
const revokeTransactionId = `inventory-revoke-tx-${nonce}`;
const regrantTransactionId = `inventory-regrant-tx-${nonce}`;
const unknownTransactionId = `inventory-unknown-tx-${nonce}`;

let idToken = null;
let localId = null;
let grantEventId = null;
let duplicateGrantEventId = null;
let revokeEventId = null;
let regrantEventId = null;
let unknownEventId = null;
let testPassed = false;

console.log("AtlasBoard Inventory + Entitlements Local E2E v1");
console.log(
  "Safety: localhost emulators only; catalog and entitlement test data " +
  "are temporary and production data is not touched.",
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

  grantEventId = eventId(localId, itemId, grantKey);
  duplicateGrantEventId =
    eventId(localId, itemId, duplicateGrantKey);
  revokeEventId = eventId(localId, itemId, revokeKey);
  regrantEventId = eventId(localId, itemId, regrantKey);
  unknownEventId = eventId(localId, unknownItemId, unknownKey);

  console.log(`[1/11] PASSED. Local UID=${localId}`);

  console.log("[2/11] Seeding one temporary active catalog item...");
  const catalogSeed = await patchFirestoreDocument(
    `item_catalog/${itemId}`,
    {
      itemId: {stringValue: itemId},
      itemType: {stringValue: "pawn"},
      active: {booleanValue: true},
      schemaVersion: {integerValue: "1"},
    },
  );

  assert(
    catalogSeed.response.ok,
    `Catalog seed failed: HTTP ${catalogSeed.response.status} ` +
    catalogSeed.text,
  );
  console.log("[2/11] PASSED. Temporary pawn catalog item created.");

  console.log("[3/11] Granting entitlement through authenticated backend...");
  const grant = await callFunction("inventoryTestMutation", idToken, {
    operation: "grant",
    itemId,
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
  assert(
    grantResult?.idempotentReplay === false,
    "First grant was incorrectly marked as replay.",
  );
  assert(grantResult?.ownedBefore === false,
    "First grant expected ownedBefore=false.");
  assert(grantResult?.ownedAfter === true,
    "First grant expected ownedAfter=true.");
  assert(grantResult?.itemType === "pawn",
    `Expected pawn item type, got ${grantResult?.itemType}`);
  assert(grantResult?.eventId === grantEventId,
    "Grant event ID mismatch.");

  console.log("[3/11] PASSED. Entitlement changed not-owned -> owned.");

  console.log("[4/11] Replaying the exact grant idempotency key...");
  const replay = await callFunction("inventoryTestMutation", idToken, {
    operation: "grant",
    itemId,
    reason: "test_grant",
    transactionId: grantTransactionId,
    idempotencyKey: grantKey,
  });

  assert(
    replay.response.ok,
    `Grant replay failed: HTTP ${replay.response.status} ${replay.text}`,
  );

  const replayResult = getCallableResult(replay);
  assert(replayResult?.applied === false,
    "Grant replay incorrectly applied again.");
  assert(replayResult?.idempotentReplay === true,
    "Grant replay was not identified as idempotent.");
  assert(replayResult?.ownedAfter === true,
    "Grant replay changed final ownership.");
  assert(replayResult?.eventId === grantEventId,
    "Grant replay did not resolve to original event.");

  console.log("[4/11] PASSED. Exact replay did not duplicate entitlement.");

  console.log("[5/11] Trying a second grant with a new idempotency key...");
  const duplicateGrant = await callFunction(
    "inventoryTestMutation",
    idToken,
    {
      operation: "grant",
      itemId,
      reason: "test_duplicate_grant",
      transactionId: duplicateTransactionId,
      idempotencyKey: duplicateGrantKey,
    },
  );

  const duplicateStatus = duplicateGrant.json?.error?.status;
  const duplicateErrorKey =
    duplicateGrant.json?.error?.details?.errorKey;

  assert(!duplicateGrant.response.ok,
    "Duplicate grant unexpectedly succeeded.");
  assert(
    duplicateStatus === "ALREADY_EXISTS",
    `Expected ALREADY_EXISTS, got ${duplicateStatus}. ` +
    `Body=${duplicateGrant.text}`,
  );
  assert(
    duplicateErrorKey === "inventory.error.already_owned",
    `Unexpected duplicate-grant errorKey: ${duplicateErrorKey}`,
  );

  const duplicateEvent = await getFirestoreDocument(
    `inventories/${localId}/items/${itemId}/events/` +
    duplicateGrantEventId,
  );
  assert(
    duplicateEvent.response.status === 404,
    "Rejected duplicate grant unexpectedly created an event.",
  );

  console.log("[5/11] PASSED. Already-owned item cannot be granted twice.");

  console.log("[6/11] Verifying owned item and grant event directly...");
  const ownedItem = await getFirestoreDocument(
    `inventories/${localId}/items/${itemId}`,
  );
  assert(ownedItem.response.ok,
    `Owned inventory item missing: ${ownedItem.text}`);
  assert(firestoreString(ownedItem.json, "uid") === localId,
    "Inventory UID mismatch.");
  assert(firestoreString(ownedItem.json, "itemId") === itemId,
    "Inventory itemId mismatch.");
  assert(firestoreString(ownedItem.json, "itemType") === "pawn",
    "Inventory itemType mismatch.");
  assert(firestoreBoolean(ownedItem.json, "owned") === true,
    "Inventory owned flag is not true.");
  assert(firestoreInteger(ownedItem.json, "quantity") === 1,
    "Owned entitlement quantity expected 1.");

  const grantEvent = await getFirestoreDocument(
    `inventories/${localId}/items/${itemId}/events/${grantEventId}`,
  );
  assert(grantEvent.response.ok, "Grant entitlement event is missing.");
  assert(firestoreString(grantEvent.json, "operation") === "grant",
    "Grant event operation mismatch.");
  assert(firestoreBoolean(grantEvent.json, "ownedBefore") === false,
    "Grant event ownedBefore mismatch.");
  assert(firestoreBoolean(grantEvent.json, "ownedAfter") === true,
    "Grant event ownedAfter mismatch.");

  console.log(
    "[6/11] PASSED. Current ownership and immutable grant event verified.",
  );

  console.log("[7/11] Revoking entitlement through authenticated backend...");
  const revoke = await callFunction("inventoryTestMutation", idToken, {
    operation: "revoke",
    itemId,
    reason: "test_revoke",
    transactionId: revokeTransactionId,
    idempotencyKey: revokeKey,
  });

  assert(
    revoke.response.ok,
    `Revoke failed: HTTP ${revoke.response.status} ${revoke.text}`,
  );

  const revokeResult = getCallableResult(revoke);
  assert(revokeResult?.applied === true, "Revoke was not applied.");
  assert(revokeResult?.ownedBefore === true,
    "Revoke expected ownedBefore=true.");
  assert(revokeResult?.ownedAfter === false,
    "Revoke expected ownedAfter=false.");
  assert(revokeResult?.eventId === revokeEventId,
    "Revoke event ID mismatch.");

  console.log("[7/11] PASSED. Entitlement changed owned -> revoked.");

  console.log("[8/11] Replaying revoke and verifying revoked state...");
  const revokeReplay = await callFunction(
    "inventoryTestMutation",
    idToken,
    {
      operation: "revoke",
      itemId,
      reason: "test_revoke",
      transactionId: revokeTransactionId,
      idempotencyKey: revokeKey,
    },
  );

  assert(
    revokeReplay.response.ok,
    `Revoke replay failed: HTTP ${revokeReplay.response.status} ` +
    revokeReplay.text,
  );

  const revokeReplayResult = getCallableResult(revokeReplay);
  assert(revokeReplayResult?.applied === false,
    "Revoke replay incorrectly applied again.");
  assert(revokeReplayResult?.idempotentReplay === true,
    "Revoke replay was not identified as idempotent.");

  const revokedItem = await getFirestoreDocument(
    `inventories/${localId}/items/${itemId}`,
  );
  assert(revokedItem.response.ok, "Revoked inventory item is missing.");
  assert(firestoreBoolean(revokedItem.json, "owned") === false,
    "Revoked item still reports owned=true.");
  assert(firestoreInteger(revokedItem.json, "quantity") === 0,
    "Revoked entitlement quantity expected 0.");

  const revokeEvent = await getFirestoreDocument(
    `inventories/${localId}/items/${itemId}/events/${revokeEventId}`,
  );
  assert(revokeEvent.response.ok, "Revoke entitlement event is missing.");
  assert(firestoreString(revokeEvent.json, "operation") === "revoke",
    "Revoke event operation mismatch.");

  console.log("[8/11] PASSED. Revoke replay is idempotent and state is false.");

  console.log("[9/11] Re-granting the revoked entitlement...");
  const regrant = await callFunction("inventoryTestMutation", idToken, {
    operation: "grant",
    itemId,
    reason: "test_regrant",
    transactionId: regrantTransactionId,
    idempotencyKey: regrantKey,
  });

  assert(
    regrant.response.ok,
    `Re-grant failed: HTTP ${regrant.response.status} ${regrant.text}`,
  );

  const regrantResult = getCallableResult(regrant);
  assert(regrantResult?.applied === true, "Re-grant was not applied.");
  assert(regrantResult?.ownedBefore === false,
    "Re-grant expected ownedBefore=false.");
  assert(regrantResult?.ownedAfter === true,
    "Re-grant expected ownedAfter=true.");

  const regrantEvent = await getFirestoreDocument(
    `inventories/${localId}/items/${itemId}/events/${regrantEventId}`,
  );
  assert(regrantEvent.response.ok, "Re-grant event is missing.");

  console.log("[9/11] PASSED. Revoked entitlement can be granted again.");

  console.log("[10/11] Rejecting grant for an unknown catalog item...");
  const unknownGrant = await callFunction(
    "inventoryTestMutation",
    idToken,
    {
      operation: "grant",
      itemId: unknownItemId,
      reason: "test_unknown_item",
      transactionId: unknownTransactionId,
      idempotencyKey: unknownKey,
    },
  );

  const unknownStatus = unknownGrant.json?.error?.status;
  const unknownErrorKey =
    unknownGrant.json?.error?.details?.errorKey;

  assert(!unknownGrant.response.ok,
    "Unknown catalog item unexpectedly granted.");
  assert(
    unknownStatus === "NOT_FOUND",
    `Expected NOT_FOUND, got ${unknownStatus}. Body=${unknownGrant.text}`,
  );
  assert(
    unknownErrorKey === "inventory.error.catalog_item_not_found",
    `Unexpected unknown-item errorKey: ${unknownErrorKey}`,
  );

  const unknownItem = await getFirestoreDocument(
    `inventories/${localId}/items/${unknownItemId}`,
  );
  assert(unknownItem.response.status === 404,
    "Unknown item unexpectedly created an inventory document.");

  const unknownEvent = await getFirestoreDocument(
    `inventories/${localId}/items/${unknownItemId}/events/` +
    unknownEventId,
  );
  assert(unknownEvent.response.status === 404,
    "Unknown item unexpectedly created an entitlement event.");

  console.log("[10/11] PASSED. Unknown catalog item was rejected safely.");

  console.log("[11/11] Verifying final inventory state...");
  const finalItem = await getFirestoreDocument(
    `inventories/${localId}/items/${itemId}`,
  );
  assert(finalItem.response.ok, "Final inventory item is missing.");
  assert(firestoreBoolean(finalItem.json, "owned") === true,
    "Final entitlement should be owned after re-grant.");
  assert(firestoreInteger(finalItem.json, "quantity") === 1,
    "Final entitlement quantity expected 1.");
  assert(
    firestoreString(finalItem.json, "lastTransactionId") ===
      regrantTransactionId,
    "Final inventory transaction reference mismatch.",
  );

  console.log(
    "[11/11] PASSED. Final state is owned with verified lifecycle history.",
  );

  testPassed = true;
} catch (error) {
  console.error("");
  console.error("AtlasBoard Inventory + Entitlements Local E2E v1 FAILED.");
  console.error(error instanceof Error ? error.stack ?? error.message : error);
  process.exitCode = 1;
} finally {
  console.log("[cleanup] Cleaning temporary emulator data...");

  if (localId) {
    if (grantEventId) {
      await bestEffortDeleteFirestore(
        `inventories/${localId}/items/${itemId}/events/${grantEventId}`,
      );
    }
    if (duplicateGrantEventId) {
      await bestEffortDeleteFirestore(
        `inventories/${localId}/items/${itemId}/events/` +
        duplicateGrantEventId,
      );
    }
    if (revokeEventId) {
      await bestEffortDeleteFirestore(
        `inventories/${localId}/items/${itemId}/events/${revokeEventId}`,
      );
    }
    if (regrantEventId) {
      await bestEffortDeleteFirestore(
        `inventories/${localId}/items/${itemId}/events/${regrantEventId}`,
      );
    }
    if (unknownEventId) {
      await bestEffortDeleteFirestore(
        `inventories/${localId}/items/${unknownItemId}/events/` +
        unknownEventId,
      );
    }

    await bestEffortDeleteFirestore(
      `inventories/${localId}/items/${itemId}`,
    );
    await bestEffortDeleteFirestore(
      `inventories/${localId}/items/${unknownItemId}`,
    );
  }

  await bestEffortDeleteFirestore(`item_catalog/${itemId}`);

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
  console.log("");
  console.log("AtlasBoard Inventory + Entitlements Local E2E v1 PASSED.");
  console.log(
    "Verified: catalog-backed grant -> idempotent replay -> duplicate " +
    "ownership rejection -> immutable event verification -> revoke -> " +
    "revoke replay -> re-grant -> unknown-item rejection -> cleanup.",
  );
  process.exitCode = 0;
} else {
  console.error("");
  console.error("Do not mark Phase 3C.4C Local E2E as passed.");
}
