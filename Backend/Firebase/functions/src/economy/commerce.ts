import {createHash} from "crypto";
import {FieldValue, getFirestore} from "firebase-admin/firestore";
import {HttpsError} from "firebase-functions/v2/https";
import {
  makeLedgerEntryId,
  WalletCurrencyId,
} from "./wallet";
import {
  InventoryItemType,
  isInventoryItemType,
  makeInventoryEventId,
} from "./inventory";

export type CommerceStatus =
  "pending" |
  "succeeded" |
  "failed" |
  "refunded" |
  "revoked";

export type CommerceEntitlementStatus =
  "pending" |
  "granted" |
  "not_granted" |
  "revoked";

export type CommercePaymentMethod =
  "gold" |
  "atlas_coin" |
  "steam" |
  "google_play" |
  "apple" |
  "promo" |
  "admin_gift";

export interface CommercePurchaseInput {
  uid: string;
  itemId: string;
  paymentMethod: WalletCurrencyId;
  idempotencyKey: string;
  source: string;
}

export interface CommerceRefundInput {
  uid: string;
  transactionId: string;
  idempotencyKey: string;
  source: string;
}

export interface CommerceMutationResult {
  applied: boolean;
  idempotentReplay: boolean;
  transactionId: string;
  status: CommerceStatus;
  entitlementStatus: CommerceEntitlementStatus;
  itemId: string;
  itemType: InventoryItemType;
  paymentMethod: CommercePaymentMethod;
  currencyId: WalletCurrencyId;
  amount: number;
  balanceBefore: number | null;
  balanceAfter: number | null;
  chargeLedgerEntryId: string | null;
  refundLedgerEntryId: string | null;
  grantEventId: string | null;
  revokeEventId: string | null;
  commerceEventId: string;
  failureReason: string | null;
  failureErrorKey: string | null;
}

const COMMERCE_SCHEMA_VERSION = 1;
const WALLET_SCHEMA_VERSION = 1;
const INVENTORY_SCHEMA_VERSION = 1;
const MAX_ABSOLUTE_BALANCE = Number.MAX_SAFE_INTEGER;

/**
 * Builds a deterministic commerce transaction id for an initial purchase.
 * @param {string} uid Authenticated account id.
 * @param {string} idempotencyKey Stable purchase idempotency key.
 * @return {string} SHA-256 commerce transaction document id.
 */
export function makeCommerceTransactionId(
  uid: string,
  idempotencyKey: string,
): string {
  return createHash("sha256")
    .update(`${uid}:commerce_purchase:${idempotencyKey}`, "utf8")
    .digest("hex");
}

/**
 * Builds a deterministic immutable commerce event id.
 * Refund idempotency is intentionally scoped to one transaction.
 * @param {string} uid Authenticated account id.
 * @param {string} transactionId Commerce transaction id.
 * @param {string} eventType Event type.
 * @param {string} idempotencyKey Stable operation idempotency key.
 * @return {string} SHA-256 commerce event document id.
 */
export function makeCommerceEventId(
  uid: string,
  transactionId: string,
  eventType: string,
  idempotencyKey: string,
): string {
  return createHash("sha256")
    .update(
      `${uid}:${transactionId}:${eventType}:${idempotencyKey}`,
      "utf8",
    )
    .digest("hex");
}

/**
 * Reads an existing non-negative wallet integer.
 * @param {unknown} value Stored value.
 * @param {string} fieldName Field name for stable error details.
 * @return {number} Validated non-negative safe integer.
 */
function readNonNegativeInteger(
  value: unknown,
  fieldName: string,
): number {
  if (value === undefined) {
    return 0;
  }

  if (
    typeof value !== "number" ||
    !Number.isSafeInteger(value) ||
    value < 0
  ) {
    throw new HttpsError(
      "internal",
      "COMMERCE_STATE_INVALID",
      {
        errorKey: "commerce.error.state_invalid",
        fieldName,
      },
    );
  }

  return value;
}

/**
 * Reads the current inventory ownership flag.
 * @param {FirebaseFirestore.DocumentData} data Inventory item data.
 * @return {boolean} Current ownership state.
 */
function readOwned(
  data: FirebaseFirestore.DocumentData,
): boolean {
  if (data.owned === undefined) {
    return false;
  }

  if (typeof data.owned !== "boolean") {
    throw new HttpsError(
      "internal",
      "COMMERCE_INVENTORY_STATE_INVALID",
      {
        errorKey: "commerce.error.inventory_state_invalid",
      },
    );
  }

  return data.owned;
}

/**
 * Reads one positive integer wallet price from catalog data.
 * @param {FirebaseFirestore.DocumentData} catalogData Catalog document.
 * @param {WalletCurrencyId} currencyId Wallet currency.
 * @return {number} Positive price.
 */
function readCatalogWalletPrice(
  catalogData: FirebaseFirestore.DocumentData,
  currencyId: WalletCurrencyId,
): number {
  const prices = catalogData.prices;

  if (
    prices === null ||
    typeof prices !== "object" ||
    Array.isArray(prices)
  ) {
    throw new HttpsError(
      "failed-precondition",
      "CATALOG_PRICE_MISSING",
      {
        errorKey: "commerce.error.price_missing",
        currencyId,
      },
    );
  }

  const value = (prices as Record<string, unknown>)[currencyId];

  if (
    typeof value !== "number" ||
    !Number.isSafeInteger(value) ||
    value <= 0
  ) {
    throw new HttpsError(
      "failed-precondition",
      "CATALOG_PRICE_INVALID",
      {
        errorKey: "commerce.error.price_invalid",
        currencyId,
      },
    );
  }

  return value;
}

/**
 * Validates an existing commerce transaction against purchase replay input.
 * @param {FirebaseFirestore.DocumentData} existing Existing transaction.
 * @param {CommercePurchaseInput} input Requested purchase.
 * @param {string} expectedTransactionId Deterministic transaction id.
 */
function assertPurchaseReplayMatches(
  existing: FirebaseFirestore.DocumentData,
  input: CommercePurchaseInput,
  expectedTransactionId: string,
): void {
  if (
    existing.uid !== input.uid ||
    existing.transactionId !== expectedTransactionId ||
    existing.itemId !== input.itemId ||
    existing.paymentMethod !== input.paymentMethod ||
    existing.idempotencyKeyHash !== expectedTransactionId
  ) {
    throw new HttpsError(
      "already-exists",
      "COMMERCE_IDEMPOTENCY_CONFLICT",
      {
        errorKey: "commerce.error.idempotency_conflict",
      },
    );
  }
}

/**
 * Converts an existing commerce transaction into a replay response.
 * @param {FirebaseFirestore.DocumentData} data Existing transaction data.
 * @return {CommerceMutationResult} Idempotent result.
 */
function replayFromTransaction(
  data: FirebaseFirestore.DocumentData,
): CommerceMutationResult {
  const status = data.status as CommerceStatus;
  const entitlementStatus =
    data.entitlementStatus as CommerceEntitlementStatus;
  const itemType = data.itemType;

  if (
    ![
      "pending",
      "succeeded",
      "failed",
      "refunded",
      "revoked",
    ].includes(status) ||
    ![
      "pending",
      "granted",
      "not_granted",
      "revoked",
    ].includes(entitlementStatus) ||
    !isInventoryItemType(itemType)
  ) {
    throw new HttpsError(
      "internal",
      "COMMERCE_STATE_INVALID",
      {
        errorKey: "commerce.error.state_invalid",
      },
    );
  }

  return {
    applied: false,
    idempotentReplay: true,
    transactionId: data.transactionId,
    status,
    entitlementStatus,
    itemId: data.itemId,
    itemType,
    paymentMethod: data.paymentMethod,
    currencyId: data.currencyId,
    amount: data.amount,
    balanceBefore:
      typeof data.balanceBefore === "number" ?
        data.balanceBefore :
        null,
    balanceAfter:
      typeof data.balanceAfter === "number" ?
        data.balanceAfter :
        null,
    chargeLedgerEntryId: data.chargeLedgerEntryId ?? null,
    refundLedgerEntryId: data.refundLedgerEntryId ?? null,
    grantEventId: data.grantEventId ?? null,
    revokeEventId: data.revokeEventId ?? null,
    commerceEventId: data.purchaseEventId ?? "purchase",
    failureReason: data.failureReason ?? null,
    failureErrorKey: data.failureErrorKey ?? null,
  };
}

/**
 * Creates one emulator-tested wallet-backed commerce purchase atomically.
 * The catalog price is server-read; the caller never supplies the amount.
 * @param {CommercePurchaseInput} input Validated purchase input.
 * @return {Promise<CommerceMutationResult>} Purchase result.
 */
export async function applyCommercePurchase(
  input: CommercePurchaseInput,
): Promise<CommerceMutationResult> {
  const db = getFirestore();
  const transactionId = makeCommerceTransactionId(
    input.uid,
    input.idempotencyKey,
  );

  const transactionRef = db
    .collection("commerce_transactions")
    .doc(transactionId);

  const purchaseEventId = "purchase";
  const purchaseEventRef = transactionRef
    .collection("events")
    .doc(purchaseEventId);

  const catalogRef = db
    .collection("item_catalog")
    .doc(input.itemId);

  const inventoryRef = db
    .collection("inventories")
    .doc(input.uid)
    .collection("items")
    .doc(input.itemId);

  const balanceRef = db
    .collection("wallets")
    .doc(input.uid)
    .collection("balances")
    .doc(input.paymentMethod);

  const chargeIdempotencyKey =
    `commerce:${transactionId}:charge`;
  const chargeLedgerEntryId = makeLedgerEntryId(
    input.uid,
    chargeIdempotencyKey,
  );
  const chargeLedgerRef = db
    .collection("wallet_ledger")
    .doc(chargeLedgerEntryId);

  const grantIdempotencyKey =
    `commerce:${transactionId}:grant`;
  const grantEventId = makeInventoryEventId(
    input.uid,
    input.itemId,
    grantIdempotencyKey,
  );
  const grantEventRef = inventoryRef
    .collection("events")
    .doc(grantEventId);

  return db.runTransaction(async (transaction) => {
    const existingTransaction =
      await transaction.get(transactionRef);

    if (existingTransaction.exists) {
      const existing = existingTransaction.data() ?? {};
      assertPurchaseReplayMatches(
        existing,
        input,
        transactionId,
      );
      return replayFromTransaction(existing);
    }

    const [
      catalogSnapshot,
      inventorySnapshot,
      balanceSnapshot,
      chargeLedgerSnapshot,
      grantEventSnapshot,
    ] = await Promise.all([
      transaction.get(catalogRef),
      transaction.get(inventoryRef),
      transaction.get(balanceRef),
      transaction.get(chargeLedgerRef),
      transaction.get(grantEventRef),
    ]);

    if (!catalogSnapshot.exists) {
      throw new HttpsError(
        "not-found",
        "CATALOG_ITEM_NOT_FOUND",
        {
          errorKey: "commerce.error.catalog_item_not_found",
          itemId: input.itemId,
        },
      );
    }

    const catalogData = catalogSnapshot.data() ?? {};
    const itemType = catalogData.itemType;

    if (
      catalogData.active !== true ||
      !isInventoryItemType(itemType)
    ) {
      throw new HttpsError(
        "failed-precondition",
        "CATALOG_ITEM_INVALID",
        {
          errorKey: "commerce.error.catalog_item_invalid",
          itemId: input.itemId,
        },
      );
    }

    const amount = readCatalogWalletPrice(
      catalogData,
      input.paymentMethod,
    );

    if (chargeLedgerSnapshot.exists || grantEventSnapshot.exists) {
      throw new HttpsError(
        "internal",
        "COMMERCE_PARTIAL_STATE_DETECTED",
        {
          errorKey: "commerce.error.partial_state",
          transactionId,
        },
      );
    }

    const inventoryData = inventorySnapshot.data() ?? {};
    const ownedBefore = readOwned(inventoryData);
    const balanceData = balanceSnapshot.data() ?? {};
    const balanceBefore = readNonNegativeInteger(
      balanceData.amount,
      "wallet.amount",
    );

    const serverTimestamp = FieldValue.serverTimestamp();

    const makeFailedResult = (
      failureReason: string,
      failureErrorKey: string,
    ): CommerceMutationResult => {
      transaction.create(transactionRef, {
        uid: input.uid,
        transactionId,
        status: "failed",
        paymentMethod: input.paymentMethod,
        currencyId: input.paymentMethod,
        amount,
        itemId: input.itemId,
        itemType,
        providerProductId: null,
        providerTransactionId: null,
        receiptHash: null,
        failureReason,
        failureErrorKey,
        entitlementStatus: "not_granted",
        chargeLedgerEntryId: null,
        refundLedgerEntryId: null,
        grantEventId: null,
        revokeEventId: null,
        purchaseEventId,
        idempotencyKeyHash: transactionId,
        source: input.source,
        schemaVersion: COMMERCE_SCHEMA_VERSION,
        createdAt: serverTimestamp,
        updatedAt: serverTimestamp,
        succeededAt: null,
        failedAt: serverTimestamp,
        refundedAt: null,
        revokedAt: null,
        balanceBefore,
        balanceAfter: balanceBefore,
      });

      transaction.create(purchaseEventRef, {
        uid: input.uid,
        transactionId,
        eventType: "purchase_failed",
        fromStatus: null,
        toStatus: "failed",
        itemId: input.itemId,
        itemType,
        paymentMethod: input.paymentMethod,
        currencyId: input.paymentMethod,
        amount,
        failureReason,
        failureErrorKey,
        source: input.source,
        schemaVersion: COMMERCE_SCHEMA_VERSION,
        createdAt: serverTimestamp,
      });

      return {
        applied: true,
        idempotentReplay: false,
        transactionId,
        status: "failed",
        entitlementStatus: "not_granted",
        itemId: input.itemId,
        itemType,
        paymentMethod: input.paymentMethod,
        currencyId: input.paymentMethod,
        amount,
        balanceBefore,
        balanceAfter: balanceBefore,
        chargeLedgerEntryId: null,
        refundLedgerEntryId: null,
        grantEventId: null,
        revokeEventId: null,
        commerceEventId: purchaseEventId,
        failureReason,
        failureErrorKey,
      };
    };

    if (ownedBefore) {
      return makeFailedResult(
        "already_owned",
        "commerce.error.already_owned",
      );
    }

    if (balanceBefore < amount) {
      return makeFailedResult(
        "insufficient_funds",
        "commerce.error.insufficient_funds",
      );
    }

    const balanceAfter = balanceBefore - amount;

    transaction.set(
      balanceRef,
      {
        uid: input.uid,
        currencyId: input.paymentMethod,
        amount: balanceAfter,
        schemaVersion: WALLET_SCHEMA_VERSION,
        updatedAt: serverTimestamp,
      },
      {merge: false},
    );

    transaction.create(chargeLedgerRef, {
      uid: input.uid,
      currencyId: input.paymentMethod,
      delta: -amount,
      balanceBefore,
      balanceAfter,
      reason: "commerce_purchase",
      transactionId,
      idempotencyKeyHash: chargeLedgerEntryId,
      actorType: "commerce_backend",
      actorUid: input.uid,
      source: input.source,
      schemaVersion: WALLET_SCHEMA_VERSION,
      createdAt: serverTimestamp,
    });

    const firstGrantedAt =
      inventorySnapshot.exists &&
      inventoryData.firstGrantedAt !== undefined ?
        inventoryData.firstGrantedAt :
        serverTimestamp;

    transaction.set(
      inventoryRef,
      {
        uid: input.uid,
        itemId: input.itemId,
        itemType,
        owned: true,
        quantity: 1,
        firstGrantedAt,
        lastGrantedAt: serverTimestamp,
        revokedAt: null,
        lastTransactionId: transactionId,
        lastSource: input.source,
        schemaVersion: INVENTORY_SCHEMA_VERSION,
        updatedAt: serverTimestamp,
      },
      {merge: true},
    );

    transaction.create(grantEventRef, {
      uid: input.uid,
      itemId: input.itemId,
      itemType,
      operation: "grant",
      ownedBefore: false,
      ownedAfter: true,
      reason: "commerce_purchase",
      transactionId,
      idempotencyKeyHash: grantEventId,
      actorType: "commerce_backend",
      actorUid: input.uid,
      source: input.source,
      schemaVersion: INVENTORY_SCHEMA_VERSION,
      createdAt: serverTimestamp,
    });

    transaction.create(transactionRef, {
      uid: input.uid,
      transactionId,
      status: "succeeded",
      paymentMethod: input.paymentMethod,
      currencyId: input.paymentMethod,
      amount,
      itemId: input.itemId,
      itemType,
      providerProductId: null,
      providerTransactionId: null,
      receiptHash: null,
      failureReason: null,
      failureErrorKey: null,
      entitlementStatus: "granted",
      chargeLedgerEntryId,
      refundLedgerEntryId: null,
      grantEventId,
      revokeEventId: null,
      purchaseEventId,
      idempotencyKeyHash: transactionId,
      source: input.source,
      schemaVersion: COMMERCE_SCHEMA_VERSION,
      createdAt: serverTimestamp,
      updatedAt: serverTimestamp,
      succeededAt: serverTimestamp,
      failedAt: null,
      refundedAt: null,
      revokedAt: null,
      balanceBefore,
      balanceAfter,
    });

    transaction.create(purchaseEventRef, {
      uid: input.uid,
      transactionId,
      eventType: "purchase_succeeded",
      fromStatus: null,
      toStatus: "succeeded",
      itemId: input.itemId,
      itemType,
      paymentMethod: input.paymentMethod,
      currencyId: input.paymentMethod,
      amount,
      chargeLedgerEntryId,
      grantEventId,
      source: input.source,
      schemaVersion: COMMERCE_SCHEMA_VERSION,
      createdAt: serverTimestamp,
    });

    return {
      applied: true,
      idempotentReplay: false,
      transactionId,
      status: "succeeded",
      entitlementStatus: "granted",
      itemId: input.itemId,
      itemType,
      paymentMethod: input.paymentMethod,
      currencyId: input.paymentMethod,
      amount,
      balanceBefore,
      balanceAfter,
      chargeLedgerEntryId,
      refundLedgerEntryId: null,
      grantEventId,
      revokeEventId: null,
      commerceEventId: purchaseEventId,
      failureReason: null,
      failureErrorKey: null,
    };
  });
}

/**
 * Refunds one previously successful wallet-backed purchase atomically.
 * @param {CommerceRefundInput} input Validated refund input.
 * @return {Promise<CommerceMutationResult>} Refund result.
 */
export async function applyCommerceRefund(
  input: CommerceRefundInput,
): Promise<CommerceMutationResult> {
  const db = getFirestore();

  const transactionRef = db
    .collection("commerce_transactions")
    .doc(input.transactionId);

  const refundEventId = makeCommerceEventId(
    input.uid,
    input.transactionId,
    "refund",
    input.idempotencyKey,
  );
  const refundEventRef = transactionRef
    .collection("events")
    .doc(refundEventId);

  return db.runTransaction(async (transaction) => {
    const [
      commerceSnapshot,
      refundEventSnapshot,
    ] = await Promise.all([
      transaction.get(transactionRef),
      transaction.get(refundEventRef),
    ]);

    if (!commerceSnapshot.exists) {
      throw new HttpsError(
        "not-found",
        "COMMERCE_TRANSACTION_NOT_FOUND",
        {
          errorKey: "commerce.error.transaction_not_found",
          transactionId: input.transactionId,
        },
      );
    }

    const commerceData = commerceSnapshot.data() ?? {};

    if (commerceData.uid !== input.uid) {
      throw new HttpsError(
        "permission-denied",
        "COMMERCE_OWNER_MISMATCH",
        {
          errorKey: "commerce.error.owner_mismatch",
        },
      );
    }

    if (refundEventSnapshot.exists) {
      const eventData = refundEventSnapshot.data() ?? {};

      if (
        eventData.uid !== input.uid ||
        eventData.transactionId !== input.transactionId ||
        eventData.eventType !== "refund_succeeded"
      ) {
        throw new HttpsError(
          "already-exists",
          "COMMERCE_IDEMPOTENCY_CONFLICT",
          {
            errorKey: "commerce.error.idempotency_conflict",
          },
        );
      }

      const replay = replayFromTransaction(commerceData);
      return {
        ...replay,
        balanceBefore:
          typeof eventData.balanceBefore === "number" ?
            eventData.balanceBefore :
            replay.balanceBefore,
        balanceAfter:
          typeof eventData.balanceAfter === "number" ?
            eventData.balanceAfter :
            replay.balanceAfter,
        refundLedgerEntryId:
          eventData.refundLedgerEntryId ??
          replay.refundLedgerEntryId,
        revokeEventId:
          eventData.revokeEventId ??
          replay.revokeEventId,
        commerceEventId: refundEventId,
      };
    }

    if (commerceData.status !== "succeeded") {
      throw new HttpsError(
        "failed-precondition",
        "COMMERCE_TRANSACTION_NOT_REFUNDABLE",
        {
          errorKey: "commerce.error.not_refundable",
          status: commerceData.status ?? null,
        },
      );
    }

    const itemId = commerceData.itemId;
    const itemType = commerceData.itemType;
    const currencyId = commerceData.currencyId;
    const paymentMethod = commerceData.paymentMethod;
    const amount = commerceData.amount;

    if (
      typeof itemId !== "string" ||
      !isInventoryItemType(itemType) ||
      (currencyId !== "gold" && currencyId !== "atlas_coin") ||
      paymentMethod !== currencyId ||
      typeof amount !== "number" ||
      !Number.isSafeInteger(amount) ||
      amount <= 0
    ) {
      throw new HttpsError(
        "internal",
        "COMMERCE_STATE_INVALID",
        {
          errorKey: "commerce.error.state_invalid",
        },
      );
    }

    const balanceRef = db
      .collection("wallets")
      .doc(input.uid)
      .collection("balances")
      .doc(currencyId);

    const inventoryRef = db
      .collection("inventories")
      .doc(input.uid)
      .collection("items")
      .doc(itemId);

    const refundIdempotencyKey =
      `commerce:${input.transactionId}:refund:${input.idempotencyKey}`;
    const refundLedgerEntryId = makeLedgerEntryId(
      input.uid,
      refundIdempotencyKey,
    );
    const refundLedgerRef = db
      .collection("wallet_ledger")
      .doc(refundLedgerEntryId);

    const revokeIdempotencyKey =
      `commerce:${input.transactionId}:revoke:${input.idempotencyKey}`;
    const revokeEventId = makeInventoryEventId(
      input.uid,
      itemId,
      revokeIdempotencyKey,
    );
    const revokeEventRef = inventoryRef
      .collection("events")
      .doc(revokeEventId);

    const [
      balanceSnapshot,
      inventorySnapshot,
      refundLedgerSnapshot,
      revokeEventSnapshot,
    ] = await Promise.all([
      transaction.get(balanceRef),
      transaction.get(inventoryRef),
      transaction.get(refundLedgerRef),
      transaction.get(revokeEventRef),
    ]);

    if (refundLedgerSnapshot.exists || revokeEventSnapshot.exists) {
      throw new HttpsError(
        "internal",
        "COMMERCE_PARTIAL_REFUND_STATE",
        {
          errorKey: "commerce.error.partial_state",
          transactionId: input.transactionId,
        },
      );
    }

    const balanceData = balanceSnapshot.data() ?? {};
    const balanceBefore = readNonNegativeInteger(
      balanceData.amount,
      "wallet.amount",
    );
    const balanceAfter = balanceBefore + amount;

    if (
      !Number.isSafeInteger(balanceAfter) ||
      balanceAfter > MAX_ABSOLUTE_BALANCE
    ) {
      throw new HttpsError(
        "out-of-range",
        "WALLET_BALANCE_OUT_OF_RANGE",
        {
          errorKey: "economy.error.balance_out_of_range",
        },
      );
    }

    const inventoryData = inventorySnapshot.data() ?? {};
    const ownedBefore = readOwned(inventoryData);

    if (!ownedBefore) {
      throw new HttpsError(
        "internal",
        "COMMERCE_ENTITLEMENT_MISSING",
        {
          errorKey: "commerce.error.entitlement_missing",
        },
      );
    }

    const serverTimestamp = FieldValue.serverTimestamp();

    transaction.set(
      balanceRef,
      {
        uid: input.uid,
        currencyId,
        amount: balanceAfter,
        schemaVersion: WALLET_SCHEMA_VERSION,
        updatedAt: serverTimestamp,
      },
      {merge: false},
    );

    transaction.create(refundLedgerRef, {
      uid: input.uid,
      currencyId,
      delta: amount,
      balanceBefore,
      balanceAfter,
      reason: "commerce_refund",
      transactionId: input.transactionId,
      idempotencyKeyHash: refundLedgerEntryId,
      actorType: "commerce_backend",
      actorUid: input.uid,
      source: input.source,
      schemaVersion: WALLET_SCHEMA_VERSION,
      createdAt: serverTimestamp,
    });

    transaction.set(
      inventoryRef,
      {
        uid: input.uid,
        itemId,
        itemType,
        owned: false,
        quantity: 0,
        revokedAt: serverTimestamp,
        lastTransactionId: input.transactionId,
        lastSource: input.source,
        schemaVersion: INVENTORY_SCHEMA_VERSION,
        updatedAt: serverTimestamp,
      },
      {merge: true},
    );

    transaction.create(revokeEventRef, {
      uid: input.uid,
      itemId,
      itemType,
      operation: "revoke",
      ownedBefore: true,
      ownedAfter: false,
      reason: "commerce_refund",
      transactionId: input.transactionId,
      idempotencyKeyHash: revokeEventId,
      actorType: "commerce_backend",
      actorUid: input.uid,
      source: input.source,
      schemaVersion: INVENTORY_SCHEMA_VERSION,
      createdAt: serverTimestamp,
    });

    transaction.update(transactionRef, {
      status: "refunded",
      entitlementStatus: "revoked",
      refundLedgerEntryId,
      revokeEventId,
      refundedAt: serverTimestamp,
      updatedAt: serverTimestamp,
    });

    transaction.create(refundEventRef, {
      uid: input.uid,
      transactionId: input.transactionId,
      eventType: "refund_succeeded",
      fromStatus: "succeeded",
      toStatus: "refunded",
      itemId,
      itemType,
      paymentMethod,
      currencyId,
      amount,
      balanceBefore,
      balanceAfter,
      refundLedgerEntryId,
      revokeEventId,
      source: input.source,
      schemaVersion: COMMERCE_SCHEMA_VERSION,
      createdAt: serverTimestamp,
    });

    return {
      applied: true,
      idempotentReplay: false,
      transactionId: input.transactionId,
      status: "refunded",
      entitlementStatus: "revoked",
      itemId,
      itemType,
      paymentMethod,
      currencyId,
      amount,
      balanceBefore,
      balanceAfter,
      chargeLedgerEntryId:
        commerceData.chargeLedgerEntryId ?? null,
      refundLedgerEntryId,
      grantEventId: commerceData.grantEventId ?? null,
      revokeEventId,
      commerceEventId: refundEventId,
      failureReason: null,
      failureErrorKey: null,
    };
  });
}
