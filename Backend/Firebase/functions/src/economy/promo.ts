import {createHash} from "crypto";
import {FieldValue, getFirestore} from "firebase-admin/firestore";
import {HttpsError} from "firebase-functions/v2/https";
import {makeLedgerEntryId} from "./wallet";
import {
  isInventoryItemType,
  makeInventoryEventId,
} from "./inventory";

export type PromoRewardType =
  "gold" |
  "atlas_coin" |
  "inventory_item" |
  "event_currency" |
  "event_ticket";

export interface PromoRedeemInput {
  uid: string;
  code: string;
  idempotencyKey: string;
  source: string;
}

export interface PromoRewardResult {
  type: PromoRewardType;
  currencyId: string | null;
  itemId: string | null;
  amount: number | null;
  balanceBefore: number | null;
  balanceAfter: number | null;
  ledgerEntryId: string | null;
  inventoryEventId: string | null;
}

export interface PromoRedeemResult {
  applied: boolean;
  idempotentReplay: boolean;
  promoId: string;
  redemptionId: string;
  redemptionEventId: string;
  redemptionCount: number;
  globalRedemptionCount: number;
  rewards: PromoRewardResult[];
}

interface ParsedReward {
  type: PromoRewardType;
  currencyId: string | null;
  itemId: string | null;
  amount: number | null;
}

interface RewardRuntime {
  parsed: ParsedReward;
  balanceRef: FirebaseFirestore.DocumentReference | null;
  ledgerRef: FirebaseFirestore.DocumentReference | null;
  inventoryRef: FirebaseFirestore.DocumentReference | null;
  inventoryEventRef: FirebaseFirestore.DocumentReference | null;
  catalogRef: FirebaseFirestore.DocumentReference | null;
  ledgerEntryId: string | null;
  inventoryEventId: string | null;
}

const PROMO_SCHEMA_VERSION = 1;
const WALLET_SCHEMA_VERSION = 1;
const INVENTORY_SCHEMA_VERSION = 1;
const MAX_REWARDS = 8;
const MAX_REWARD_AMOUNT = 1_000_000;
const EVENT_CURRENCY_PATTERN = /^[a-z0-9][a-z0-9_]{2,63}$/;

/**
 * Normalizes a user-entered promo code for deterministic lookup.
 * @param {string} code Raw user-entered code.
 * @return {string} Canonical promo code.
 */
export function normalizePromoCode(code: string): string {
  return code.trim().toUpperCase();
}

/**
 * Creates the deterministic promo document id from normalized code.
 * Raw promo codes do not need to be stored in Firestore.
 * @param {string} normalizedCode Canonical promo code.
 * @return {string} SHA-256 promo id.
 */
export function makePromoId(normalizedCode: string): string {
  return createHash("sha256")
    .update(`atlasboard:promo:${normalizedCode}`, "utf8")
    .digest("hex");
}

/**
 * Creates one deterministic account+promo aggregate redemption id.
 * @param {string} uid Authenticated account id.
 * @param {string} promoId Promo document id.
 * @return {string} SHA-256 redemption aggregate id.
 */
export function makePromoRedemptionId(
  uid: string,
  promoId: string,
): string {
  return createHash("sha256")
    .update(`${uid}:promo_redemption:${promoId}`, "utf8")
    .digest("hex");
}

/**
 * Creates an immutable redemption event id for exact idempotency.
 * @param {string} redemptionId Account+promo aggregate id.
 * @param {string} idempotencyKey Stable caller idempotency key.
 * @return {string} SHA-256 redemption event id.
 */
export function makePromoRedemptionEventId(
  redemptionId: string,
  idempotencyKey: string,
): string {
  return createHash("sha256")
    .update(`${redemptionId}:event:${idempotencyKey}`, "utf8")
    .digest("hex");
}

/**
 * Reads a safe non-negative integer field.
 * @param {unknown} value Stored field value.
 * @param {string} fieldName Stable field name for error details.
 * @return {number} Validated integer.
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
      "PROMO_STATE_INVALID",
      {
        errorKey: "promo.error.state_invalid",
        fieldName,
      },
    );
  }

  return value;
}

/**
 * Reads a positive integer limit.
 * @param {unknown} value Stored value.
 * @param {string} fieldName Stable field name.
 * @return {number} Positive integer.
 */
function readPositiveInteger(
  value: unknown,
  fieldName: string,
): number {
  if (
    typeof value !== "number" ||
    !Number.isSafeInteger(value) ||
    value <= 0
  ) {
    throw new HttpsError(
      "internal",
      "PROMO_STATE_INVALID",
      {
        errorKey: "promo.error.state_invalid",
        fieldName,
      },
    );
  }

  return value;
}

/**
 * Checks event currency ids and prevents reserved persistent currencies from
 * being disguised as event rewards.
 * @param {unknown} value Currency id to validate.
 * @param {PromoRewardType} rewardType Event reward type.
 * @return {string} Valid event currency id.
 */
function readEventCurrencyId(
  value: unknown,
  rewardType: PromoRewardType,
): string {
  if (
    typeof value !== "string" ||
    !EVENT_CURRENCY_PATTERN.test(value) ||
    value === "gold" ||
    value === "atlas_coin"
  ) {
    throw new HttpsError(
      "internal",
      "PROMO_REWARD_INVALID",
      {
        errorKey: "promo.error.reward_invalid",
        fieldName: "currencyId",
      },
    );
  }

  if (rewardType === "event_ticket" && !value.startsWith("ticket_")) {
    throw new HttpsError(
      "internal",
      "PROMO_REWARD_INVALID",
      {
        errorKey: "promo.error.reward_invalid",
        fieldName: "currencyId",
      },
    );
  }

  return value;
}

/**
 * Parses and validates the trusted reward configuration stored on the promo.
 * @param {unknown} rawRewards Firestore promo reward array.
 * @return {ParsedReward[]} Validated reward list.
 */
function parseRewards(rawRewards: unknown): ParsedReward[] {
  if (
    !Array.isArray(rawRewards) ||
    rawRewards.length === 0 ||
    rawRewards.length > MAX_REWARDS
  ) {
    throw new HttpsError(
      "internal",
      "PROMO_REWARDS_INVALID",
      {
        errorKey: "promo.error.reward_invalid",
      },
    );
  }

  const parsed: ParsedReward[] = rawRewards.map(
    (raw, index): ParsedReward => {
      if (raw === null || typeof raw !== "object" || Array.isArray(raw)) {
        throw new HttpsError(
          "internal",
          "PROMO_REWARD_INVALID",
          {
            errorKey: "promo.error.reward_invalid",
            rewardIndex: index,
          },
        );
      }

      const reward = raw as Record<string, unknown>;
      const type = reward.type;

      if (
        type !== "gold" &&
      type !== "atlas_coin" &&
      type !== "inventory_item" &&
      type !== "event_currency" &&
      type !== "event_ticket"
      ) {
        throw new HttpsError(
          "internal",
          "PROMO_REWARD_INVALID",
          {
            errorKey: "promo.error.reward_invalid",
            rewardIndex: index,
          },
        );
      }

      if (type === "inventory_item") {
        if (
          typeof reward.itemId !== "string" ||
        reward.itemId.length < 3 ||
        reward.itemId.length > 96
        ) {
          throw new HttpsError(
            "internal",
            "PROMO_REWARD_INVALID",
            {
              errorKey: "promo.error.reward_invalid",
              rewardIndex: index,
            },
          );
        }

        return {
          type,
          currencyId: null,
          itemId: reward.itemId,
          amount: null,
        };
      }

      if (
        typeof reward.amount !== "number" ||
      !Number.isSafeInteger(reward.amount) ||
      reward.amount <= 0 ||
      reward.amount > MAX_REWARD_AMOUNT
      ) {
        throw new HttpsError(
          "internal",
          "PROMO_REWARD_INVALID",
          {
            errorKey: "promo.error.reward_invalid",
            rewardIndex: index,
          },
        );
      }

      const currencyId =
      type === "gold" || type === "atlas_coin" ?
        type :
        readEventCurrencyId(reward.currencyId, type);

      return {
        type,
        currencyId,
        itemId: null,
        amount: reward.amount,
      };
    },
  );

  const currencyIds = new Set<string>();
  const itemIds = new Set<string>();

  for (const reward of parsed) {
    if (reward.currencyId !== null) {
      if (currencyIds.has(reward.currencyId)) {
        throw new HttpsError(
          "internal",
          "PROMO_REWARD_DUPLICATE_TARGET",
          {
            errorKey: "promo.error.reward_invalid",
            currencyId: reward.currencyId,
          },
        );
      }
      currencyIds.add(reward.currencyId);
    }

    if (reward.itemId !== null) {
      if (itemIds.has(reward.itemId)) {
        throw new HttpsError(
          "internal",
          "PROMO_REWARD_DUPLICATE_TARGET",
          {
            errorKey: "promo.error.reward_invalid",
            itemId: reward.itemId,
          },
        );
      }
      itemIds.add(reward.itemId);
    }
  }

  return parsed;
}

/**
 * Parses a stored reward result array from an immutable replay event.
 * @param {unknown} rawRewards Existing event reward result array.
 * @return {PromoRewardResult[]} Validated replay rewards.
 */
function parseReplayRewards(
  rawRewards: unknown,
): PromoRewardResult[] {
  if (!Array.isArray(rawRewards)) {
    throw new HttpsError(
      "internal",
      "PROMO_REDEMPTION_EVENT_INVALID",
      {
        errorKey: "promo.error.state_invalid",
      },
    );
  }

  return rawRewards.map((raw) => {
    if (raw === null || typeof raw !== "object" || Array.isArray(raw)) {
      throw new HttpsError(
        "internal",
        "PROMO_REDEMPTION_EVENT_INVALID",
        {
          errorKey: "promo.error.state_invalid",
        },
      );
    }

    const reward = raw as Record<string, unknown>;
    const type = reward.type;

    if (
      type !== "gold" &&
      type !== "atlas_coin" &&
      type !== "inventory_item" &&
      type !== "event_currency" &&
      type !== "event_ticket"
    ) {
      throw new HttpsError(
        "internal",
        "PROMO_REDEMPTION_EVENT_INVALID",
        {
          errorKey: "promo.error.state_invalid",
        },
      );
    }

    const numberOrNull = (value: unknown): number | null =>
      typeof value === "number" && Number.isSafeInteger(value) ?
        value :
        null;

    const stringOrNull = (value: unknown): string | null =>
      typeof value === "string" ? value : null;

    return {
      type,
      currencyId: stringOrNull(reward.currencyId),
      itemId: stringOrNull(reward.itemId),
      amount: numberOrNull(reward.amount),
      balanceBefore: numberOrNull(reward.balanceBefore),
      balanceAfter: numberOrNull(reward.balanceAfter),
      ledgerEntryId: stringOrNull(reward.ledgerEntryId),
      inventoryEventId: stringOrNull(reward.inventoryEventId),
    };
  });
}

/**
 * Applies one promo redemption atomically across all configured rewards.
 * @param {PromoRedeemInput} input Authenticated promo redeem request.
 * @return {Promise<PromoRedeemResult>} Redemption result.
 */
export async function applyPromoRedemption(
  input: PromoRedeemInput,
): Promise<PromoRedeemResult> {
  const normalizedCode = normalizePromoCode(input.code);

  if (
    normalizedCode.length < 4 ||
    normalizedCode.length > 32 ||
    !/^[A-Z0-9_-]+$/.test(normalizedCode)
  ) {
    throw new HttpsError(
      "invalid-argument",
      "PROMO_CODE_INVALID",
      {
        errorKey: "promo.error.invalid_code",
      },
    );
  }

  const promoId = makePromoId(normalizedCode);
  const redemptionId = makePromoRedemptionId(input.uid, promoId);
  const redemptionEventId = makePromoRedemptionEventId(
    redemptionId,
    input.idempotencyKey,
  );

  const db = getFirestore();

  const promoRef = db
    .collection("promo_codes")
    .doc(promoId);

  const redemptionRef = db
    .collection("promo_redemptions")
    .doc(redemptionId);

  const redemptionEventRef = redemptionRef
    .collection("events")
    .doc(redemptionEventId);

  return db.runTransaction(async (transaction) => {
    const [
      promoSnapshot,
      redemptionSnapshot,
      redemptionEventSnapshot,
    ] = await Promise.all([
      transaction.get(promoRef),
      transaction.get(redemptionRef),
      transaction.get(redemptionEventRef),
    ]);

    if (!promoSnapshot.exists) {
      throw new HttpsError(
        "not-found",
        "PROMO_CODE_NOT_FOUND",
        {
          errorKey: "promo.error.not_found",
        },
      );
    }

    const promoData = promoSnapshot.data() ?? {};

    if (
      promoData.codeHash !== promoId ||
      promoData.schemaVersion !== PROMO_SCHEMA_VERSION
    ) {
      throw new HttpsError(
        "internal",
        "PROMO_STATE_INVALID",
        {
          errorKey: "promo.error.state_invalid",
        },
      );
    }

    if (redemptionEventSnapshot.exists) {
      const eventData = redemptionEventSnapshot.data() ?? {};

      if (
        eventData.uid !== input.uid ||
        eventData.promoId !== promoId ||
        eventData.redemptionId !== redemptionId ||
        eventData.idempotencyKeyHash !== redemptionEventId
      ) {
        throw new HttpsError(
          "already-exists",
          "PROMO_IDEMPOTENCY_CONFLICT",
          {
            errorKey: "promo.error.idempotency_conflict",
          },
        );
      }

      return {
        applied: false,
        idempotentReplay: true,
        promoId,
        redemptionId,
        redemptionEventId,
        redemptionCount: readNonNegativeInteger(
          eventData.accountRedemptionCount,
          "accountRedemptionCount",
        ),
        globalRedemptionCount: readNonNegativeInteger(
          eventData.globalRedemptionCount,
          "globalRedemptionCount",
        ),
        rewards: parseReplayRewards(eventData.rewards),
      };
    }

    if (promoData.active !== true) {
      throw new HttpsError(
        "failed-precondition",
        "PROMO_DISABLED",
        {
          errorKey: "promo.error.disabled",
        },
      );
    }

    const now = Date.now();
    const startsAtEpochMs = promoData.startsAtEpochMs;
    const endsAtEpochMs = promoData.endsAtEpochMs;

    if (
      typeof startsAtEpochMs !== "number" ||
      !Number.isSafeInteger(startsAtEpochMs) ||
      typeof endsAtEpochMs !== "number" ||
      !Number.isSafeInteger(endsAtEpochMs) ||
      startsAtEpochMs >= endsAtEpochMs
    ) {
      throw new HttpsError(
        "internal",
        "PROMO_STATE_INVALID",
        {
          errorKey: "promo.error.state_invalid",
          fieldName: "promo_window",
        },
      );
    }

    if (now < startsAtEpochMs) {
      throw new HttpsError(
        "failed-precondition",
        "PROMO_NOT_STARTED",
        {
          errorKey: "promo.error.not_started",
        },
      );
    }

    if (now > endsAtEpochMs) {
      throw new HttpsError(
        "failed-precondition",
        "PROMO_EXPIRED",
        {
          errorKey: "promo.error.expired",
        },
      );
    }

    const globalLimit = readPositiveInteger(
      promoData.globalLimit,
      "globalLimit",
    );
    const perAccountLimit = readPositiveInteger(
      promoData.perAccountLimit,
      "perAccountLimit",
    );
    const globalRedemptionCount = readNonNegativeInteger(
      promoData.redemptionCount,
      "redemptionCount",
    );

    if (globalRedemptionCount >= globalLimit) {
      throw new HttpsError(
        "resource-exhausted",
        "PROMO_GLOBAL_LIMIT_REACHED",
        {
          errorKey: "promo.error.global_limit_reached",
        },
      );
    }

    const redemptionData = redemptionSnapshot.data() ?? {};

    if (
      redemptionSnapshot.exists &&
      (
        redemptionData.uid !== input.uid ||
        redemptionData.promoId !== promoId
      )
    ) {
      throw new HttpsError(
        "internal",
        "PROMO_REDEMPTION_STATE_INVALID",
        {
          errorKey: "promo.error.state_invalid",
        },
      );
    }

    const accountRedemptionCount = readNonNegativeInteger(
      redemptionData.redemptionCount,
      "accountRedemptionCount",
    );

    if (accountRedemptionCount >= perAccountLimit) {
      throw new HttpsError(
        "resource-exhausted",
        "PROMO_ACCOUNT_LIMIT_REACHED",
        {
          errorKey: "promo.error.account_limit_reached",
        },
      );
    }

    const rewards = parseRewards(promoData.rewards);
    const rewardRuntimes: RewardRuntime[] = rewards.map(
      (reward, index) => {
        if (reward.type === "inventory_item") {
          const itemId = reward.itemId as string;
          const inventoryRef = db
            .collection("inventories")
            .doc(input.uid)
            .collection("items")
            .doc(itemId);
          const inventoryIdempotencyKey =
            `promo:${promoId}:${redemptionEventId}:inventory:${index}`;
          const inventoryEventId = makeInventoryEventId(
            input.uid,
            itemId,
            inventoryIdempotencyKey,
          );

          return {
            parsed: reward,
            balanceRef: null,
            ledgerRef: null,
            inventoryRef,
            inventoryEventRef: inventoryRef
              .collection("events")
              .doc(inventoryEventId),
            catalogRef: db.collection("item_catalog").doc(itemId),
            ledgerEntryId: null,
            inventoryEventId,
          };
        }

        const currencyId = reward.currencyId as string;
        const walletIdempotencyKey =
          `promo:${promoId}:${redemptionEventId}:wallet:${index}`;
        const ledgerEntryId = makeLedgerEntryId(
          input.uid,
          walletIdempotencyKey,
        );

        return {
          parsed: reward,
          balanceRef: db
            .collection("wallets")
            .doc(input.uid)
            .collection("balances")
            .doc(currencyId),
          ledgerRef: db
            .collection("wallet_ledger")
            .doc(ledgerEntryId),
          inventoryRef: null,
          inventoryEventRef: null,
          catalogRef: null,
          ledgerEntryId,
          inventoryEventId: null,
        };
      },
    );

    const rewardSnapshots = await Promise.all(
      rewardRuntimes.flatMap((runtime) => {
        if (runtime.parsed.type === "inventory_item") {
          return [
            transaction.get(
              runtime.catalogRef as FirebaseFirestore.DocumentReference,
            ),
            transaction.get(
              runtime.inventoryRef as FirebaseFirestore.DocumentReference,
            ),
            transaction.get(
              runtime.inventoryEventRef as FirebaseFirestore.DocumentReference,
            ),
          ];
        }

        return [
          transaction.get(
            runtime.balanceRef as FirebaseFirestore.DocumentReference,
          ),
          transaction.get(
            runtime.ledgerRef as FirebaseFirestore.DocumentReference,
          ),
        ];
      }),
    );

    let snapshotIndex = 0;
    const serverTimestamp = FieldValue.serverTimestamp();
    const rewardResults: PromoRewardResult[] = [];

    for (let index = 0; index < rewardRuntimes.length; index += 1) {
      const runtime = rewardRuntimes[index];
      const reward = runtime.parsed;

      if (reward.type === "inventory_item") {
        const catalogSnapshot = rewardSnapshots[snapshotIndex];
        const inventorySnapshot = rewardSnapshots[snapshotIndex + 1];
        const eventSnapshot = rewardSnapshots[snapshotIndex + 2];
        snapshotIndex += 3;

        if (!catalogSnapshot.exists) {
          throw new HttpsError(
            "failed-precondition",
            "PROMO_REWARD_ITEM_NOT_FOUND",
            {
              errorKey: "promo.error.reward_item_not_found",
              itemId: reward.itemId,
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
            "PROMO_REWARD_ITEM_INVALID",
            {
              errorKey: "promo.error.reward_item_invalid",
              itemId: reward.itemId,
            },
          );
        }

        if (eventSnapshot.exists) {
          throw new HttpsError(
            "internal",
            "PROMO_PARTIAL_STATE_DETECTED",
            {
              errorKey: "promo.error.partial_state",
            },
          );
        }

        const inventoryData = inventorySnapshot.data() ?? {};
        const ownedBefore =
          inventoryData.owned === undefined ?
            false :
            inventoryData.owned;

        if (typeof ownedBefore !== "boolean") {
          throw new HttpsError(
            "internal",
            "PROMO_INVENTORY_STATE_INVALID",
            {
              errorKey: "promo.error.state_invalid",
            },
          );
        }

        if (ownedBefore) {
          throw new HttpsError(
            "already-exists",
            "PROMO_REWARD_ALREADY_OWNED",
            {
              errorKey: "promo.error.reward_already_owned",
              itemId: reward.itemId,
            },
          );
        }

        const firstGrantedAt =
          inventorySnapshot.exists &&
          inventoryData.firstGrantedAt !== undefined ?
            inventoryData.firstGrantedAt :
            serverTimestamp;

        transaction.set(
          runtime.inventoryRef as FirebaseFirestore.DocumentReference,
          {
            uid: input.uid,
            itemId: reward.itemId,
            itemType,
            owned: true,
            quantity: 1,
            firstGrantedAt,
            lastGrantedAt: serverTimestamp,
            revokedAt: null,
            lastTransactionId: redemptionEventId,
            lastSource: input.source,
            schemaVersion: INVENTORY_SCHEMA_VERSION,
            updatedAt: serverTimestamp,
          },
          {merge: true},
        );

        transaction.create(
          runtime.inventoryEventRef as FirebaseFirestore.DocumentReference,
          {
            uid: input.uid,
            itemId: reward.itemId,
            itemType,
            operation: "grant",
            ownedBefore: false,
            ownedAfter: true,
            reason: "promo_redemption",
            transactionId: redemptionEventId,
            idempotencyKeyHash: runtime.inventoryEventId,
            actorType: "promo_backend",
            actorUid: input.uid,
            source: input.source,
            schemaVersion: INVENTORY_SCHEMA_VERSION,
            createdAt: serverTimestamp,
          },
        );

        rewardResults.push({
          type: reward.type,
          currencyId: null,
          itemId: reward.itemId,
          amount: null,
          balanceBefore: null,
          balanceAfter: null,
          ledgerEntryId: null,
          inventoryEventId: runtime.inventoryEventId,
        });

        continue;
      }

      const balanceSnapshot = rewardSnapshots[snapshotIndex];
      const ledgerSnapshot = rewardSnapshots[snapshotIndex + 1];
      snapshotIndex += 2;

      if (ledgerSnapshot.exists) {
        throw new HttpsError(
          "internal",
          "PROMO_PARTIAL_STATE_DETECTED",
          {
            errorKey: "promo.error.partial_state",
          },
        );
      }

      const balanceData = balanceSnapshot.data() ?? {};
      const balanceBefore = readNonNegativeInteger(
        balanceData.amount,
        "wallet.amount",
      );
      const amount = reward.amount as number;
      const balanceAfter = balanceBefore + amount;

      if (
        !Number.isSafeInteger(balanceAfter) ||
        balanceAfter < 0
      ) {
        throw new HttpsError(
          "out-of-range",
          "PROMO_BALANCE_OUT_OF_RANGE",
          {
            errorKey: "promo.error.balance_out_of_range",
          },
        );
      }

      transaction.set(
        runtime.balanceRef as FirebaseFirestore.DocumentReference,
        {
          uid: input.uid,
          currencyId: reward.currencyId,
          amount: balanceAfter,
          currencyKind:
            reward.type === "event_currency" ||
            reward.type === "event_ticket" ?
              "event" :
              "persistent",
          schemaVersion: WALLET_SCHEMA_VERSION,
          updatedAt: serverTimestamp,
        },
        {merge: false},
      );

      transaction.create(
        runtime.ledgerRef as FirebaseFirestore.DocumentReference,
        {
          uid: input.uid,
          currencyId: reward.currencyId,
          delta: amount,
          balanceBefore,
          balanceAfter,
          reason: "promo_redemption",
          transactionId: redemptionEventId,
          promoId,
          redemptionId,
          rewardType: reward.type,
          idempotencyKeyHash: runtime.ledgerEntryId,
          actorType: "promo_backend",
          actorUid: input.uid,
          source: input.source,
          schemaVersion: WALLET_SCHEMA_VERSION,
          createdAt: serverTimestamp,
        },
      );

      rewardResults.push({
        type: reward.type,
        currencyId: reward.currencyId,
        itemId: null,
        amount,
        balanceBefore,
        balanceAfter,
        ledgerEntryId: runtime.ledgerEntryId,
        inventoryEventId: null,
      });
    }

    const nextAccountRedemptionCount = accountRedemptionCount + 1;
    const nextGlobalRedemptionCount = globalRedemptionCount + 1;

    transaction.set(
      redemptionRef,
      {
        uid: input.uid,
        promoId,
        redemptionId,
        redemptionCount: nextAccountRedemptionCount,
        lastRedemptionEventId: redemptionEventId,
        firstRedeemedAt:
          redemptionSnapshot.exists &&
          redemptionData.firstRedeemedAt !== undefined ?
            redemptionData.firstRedeemedAt :
            serverTimestamp,
        lastRedeemedAt: serverTimestamp,
        schemaVersion: PROMO_SCHEMA_VERSION,
        updatedAt: serverTimestamp,
      },
      {merge: true},
    );

    transaction.create(
      redemptionEventRef,
      {
        uid: input.uid,
        promoId,
        redemptionId,
        redemptionEventId,
        idempotencyKeyHash: redemptionEventId,
        accountRedemptionCount: nextAccountRedemptionCount,
        globalRedemptionCount: nextGlobalRedemptionCount,
        rewards: rewardResults,
        source: input.source,
        schemaVersion: PROMO_SCHEMA_VERSION,
        createdAt: serverTimestamp,
      },
    );

    transaction.update(
      promoRef,
      {
        redemptionCount: nextGlobalRedemptionCount,
        updatedAt: serverTimestamp,
      },
    );

    return {
      applied: true,
      idempotentReplay: false,
      promoId,
      redemptionId,
      redemptionEventId,
      redemptionCount: nextAccountRedemptionCount,
      globalRedemptionCount: nextGlobalRedemptionCount,
      rewards: rewardResults,
    };
  });
}
