import {createHash} from "crypto";
import {FieldValue, getFirestore} from "firebase-admin/firestore";
import {HttpsError} from "firebase-functions/v2/https";

export type InventoryOperation = "grant" | "revoke";

export type InventoryItemType =
  "pawn" |
  "animation_pack" |
  "dice_skin" |
  "board_theme" |
  "profile_frame" |
  "emote";

export interface InventoryMutationInput {
  uid: string;
  itemId: string;
  operation: InventoryOperation;
  reason: string;
  transactionId: string;
  idempotencyKey: string;
  source: string;
}

export interface InventoryMutationResult {
  applied: boolean;
  idempotentReplay: boolean;
  eventId: string;
  itemId: string;
  itemType: InventoryItemType;
  operation: InventoryOperation;
  ownedBefore: boolean;
  ownedAfter: boolean;
  transactionId: string;
}

const INVENTORY_SCHEMA_VERSION = 1;

/**
 * Checks whether a value is a supported inventory operation.
 * @param {unknown} value Value to validate.
 * @return {boolean} True for grant or revoke.
 */
export function isInventoryOperation(
  value: unknown,
): value is InventoryOperation {
  return value === "grant" || value === "revoke";
}

/**
 * Checks whether a catalog item type is supported by inventory v1.
 * @param {unknown} value Value to validate.
 * @return {boolean} True for a supported entitlement item type.
 */
export function isInventoryItemType(
  value: unknown,
): value is InventoryItemType {
  return value === "pawn" ||
    value === "animation_pack" ||
    value === "dice_skin" ||
    value === "board_theme" ||
    value === "profile_frame" ||
    value === "emote";
}

/**
 * Builds a deterministic immutable entitlement-event document id.
 * @param {string} uid Authenticated account id.
 * @param {string} itemId Catalog item id.
 * @param {string} idempotencyKey Stable caller idempotency key.
 * @return {string} SHA-256 entitlement-event document id.
 */
export function makeInventoryEventId(
  uid: string,
  itemId: string,
  idempotencyKey: string,
): string {
  return createHash("sha256")
    .update(`${uid}:${itemId}:${idempotencyKey}`, "utf8")
    .digest("hex");
}

/**
 * Ensures a replayed idempotency key matches the original operation.
 * @param {FirebaseFirestore.DocumentData} existing Existing event data.
 * @param {InventoryMutationInput} input Requested mutation.
 */
function assertIdempotentPayloadMatches(
  existing: FirebaseFirestore.DocumentData,
  input: InventoryMutationInput,
): void {
  if (
    existing.uid !== input.uid ||
    existing.itemId !== input.itemId ||
    existing.operation !== input.operation ||
    existing.reason !== input.reason ||
    existing.transactionId !== input.transactionId
  ) {
    throw new HttpsError(
      "already-exists",
      "IDEMPOTENCY_KEY_CONFLICT",
      {
        errorKey: "inventory.error.idempotency_conflict",
      },
    );
  }
}

/**
 * Reads an existing inventory ownership flag.
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
      "INVENTORY_STATE_INVALID",
      {
        errorKey: "inventory.error.state_invalid",
        fieldName: "owned",
      },
    );
  }

  return data.owned;
}

/**
 * Applies one atomic inventory entitlement mutation.
 * @param {InventoryMutationInput} input Validated mutation input.
 * @return {Promise<InventoryMutationResult>} Mutation result.
 */
export async function applyInventoryMutation(
  input: InventoryMutationInput,
): Promise<InventoryMutationResult> {
  const db = getFirestore();
  const eventId = makeInventoryEventId(
    input.uid,
    input.itemId,
    input.idempotencyKey,
  );

  const catalogRef = db
    .collection("item_catalog")
    .doc(input.itemId);

  const itemRef = db
    .collection("inventories")
    .doc(input.uid)
    .collection("items")
    .doc(input.itemId);

  const eventRef = itemRef
    .collection("events")
    .doc(eventId);

  return db.runTransaction(async (transaction) => {
    const eventSnapshot = await transaction.get(eventRef);

    if (eventSnapshot.exists) {
      const existingEvent = eventSnapshot.data() ?? {};
      assertIdempotentPayloadMatches(existingEvent, input);

      const itemType = existingEvent.itemType;
      if (!isInventoryItemType(itemType)) {
        throw new HttpsError(
          "internal",
          "INVENTORY_EVENT_INVALID",
          {
            errorKey: "inventory.error.state_invalid",
            fieldName: "itemType",
          },
        );
      }

      return {
        applied: false,
        idempotentReplay: true,
        eventId,
        itemId: input.itemId,
        itemType,
        operation: input.operation,
        ownedBefore: existingEvent.ownedBefore === true,
        ownedAfter: existingEvent.ownedAfter === true,
        transactionId: input.transactionId,
      };
    }

    const [catalogSnapshot, itemSnapshot] = await Promise.all([
      transaction.get(catalogRef),
      transaction.get(itemRef),
    ]);

    const itemData = itemSnapshot.data() ?? {};
    const ownedBefore = readOwned(itemData);

    let itemType: unknown = itemData.itemType;

    if (input.operation === "grant") {
      if (!catalogSnapshot.exists) {
        throw new HttpsError(
          "not-found",
          "CATALOG_ITEM_NOT_FOUND",
          {
            errorKey: "inventory.error.catalog_item_not_found",
            itemId: input.itemId,
          },
        );
      }

      const catalogData = catalogSnapshot.data() ?? {};
      itemType = catalogData.itemType;

      if (!isInventoryItemType(itemType)) {
        throw new HttpsError(
          "failed-precondition",
          "CATALOG_ITEM_TYPE_INVALID",
          {
            errorKey: "inventory.error.catalog_item_invalid",
            itemId: input.itemId,
          },
        );
      }

      if (catalogData.active !== true) {
        throw new HttpsError(
          "failed-precondition",
          "CATALOG_ITEM_INACTIVE",
          {
            errorKey: "inventory.error.catalog_item_inactive",
            itemId: input.itemId,
          },
        );
      }

      if (ownedBefore) {
        throw new HttpsError(
          "already-exists",
          "ENTITLEMENT_ALREADY_OWNED",
          {
            errorKey: "inventory.error.already_owned",
            itemId: input.itemId,
          },
        );
      }
    } else {
      if (!ownedBefore) {
        throw new HttpsError(
          "failed-precondition",
          "ENTITLEMENT_NOT_OWNED",
          {
            errorKey: "inventory.error.not_owned",
            itemId: input.itemId,
          },
        );
      }

      if (!isInventoryItemType(itemType)) {
        const catalogData = catalogSnapshot.data() ?? {};
        itemType = catalogData.itemType;
      }

      if (!isInventoryItemType(itemType)) {
        throw new HttpsError(
          "internal",
          "INVENTORY_STATE_INVALID",
          {
            errorKey: "inventory.error.state_invalid",
            fieldName: "itemType",
          },
        );
      }
    }

    const ownedAfter = input.operation === "grant";
    const serverTimestamp = FieldValue.serverTimestamp();

    if (input.operation === "grant") {
      const firstGrantedAt =
        itemSnapshot.exists && itemData.firstGrantedAt !== undefined ?
          itemData.firstGrantedAt :
          serverTimestamp;

      transaction.set(
        itemRef,
        {
          uid: input.uid,
          itemId: input.itemId,
          itemType,
          owned: true,
          quantity: 1,
          firstGrantedAt,
          lastGrantedAt: serverTimestamp,
          revokedAt: null,
          lastTransactionId: input.transactionId,
          lastSource: input.source,
          schemaVersion: INVENTORY_SCHEMA_VERSION,
          updatedAt: serverTimestamp,
        },
        {merge: true},
      );
    } else {
      transaction.set(
        itemRef,
        {
          uid: input.uid,
          itemId: input.itemId,
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
    }

    transaction.create(eventRef, {
      uid: input.uid,
      itemId: input.itemId,
      itemType,
      operation: input.operation,
      ownedBefore,
      ownedAfter,
      reason: input.reason,
      transactionId: input.transactionId,
      idempotencyKeyHash: eventId,
      actorType: "authenticated_account",
      actorUid: input.uid,
      source: input.source,
      schemaVersion: INVENTORY_SCHEMA_VERSION,
      createdAt: serverTimestamp,
    });

    return {
      applied: true,
      idempotentReplay: false,
      eventId,
      itemId: input.itemId,
      itemType,
      operation: input.operation,
      ownedBefore,
      ownedAfter,
      transactionId: input.transactionId,
    };
  });
}
