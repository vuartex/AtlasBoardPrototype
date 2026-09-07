import {createHash} from "crypto";
import {FieldValue, getFirestore} from "firebase-admin/firestore";
import {HttpsError} from "firebase-functions/v2/https";

export interface DailyRewardClaimResult {
  applied: boolean;
  idempotentReplay: boolean;
  claimId: string;
  transactionId: string;
  ledgerEntryId: string;
  rewardGold: number;
  balanceBefore: number;
  balanceAfter: number;
  serverDayUtc: string;
}

export interface SeasonalEventSnapshot {
  active: boolean;
  eventId: string;
  titleKey: string;
  subtitleKey: string;
  startsAtEpochMs: number;
  endsAtEpochMs: number;
  ticketCurrencyId: string;
}

export interface SeasonalChallengeSnapshot {
  challengeId: string;
  titleKey: string;
  descriptionKey: string;
  cadence: string;
  target: number;
  progress: number;
  rewardTickets: number;
  rewardXp: number;
  claimed: boolean;
  claimable: boolean;
  periodKey: string;
  sortOrder: number;
}

export interface SeasonalTrackTierSnapshot {
  tierId: string;
  titleKey: string;
  requiredXp: number;
  rewardType: string;
  rewardAmount: number;
  claimed: boolean;
  claimable: boolean;
  sortOrder: number;
}

export interface SeasonalLimitedItemSnapshot {
  itemId: string;
  itemType: string;
  displayName: string;
  ticketPrice: number;
  limitedUntilEpochMs: number;
  owned: boolean;
  sortOrder: number;
}

export interface SeasonalOverviewResult {
  event: SeasonalEventSnapshot;
  ticketBalance: number;
  eventXp: number;
  challenges: SeasonalChallengeSnapshot[];
  trackTiers: SeasonalTrackTierSnapshot[];
  limitedItems: SeasonalLimitedItemSnapshot[];
}

export interface SeasonalClaimResult {
  applied: boolean;
  idempotentReplay: boolean;
  eventId: string;
  referenceId: string;
  transactionId: string;
  ticketBalance: number;
  eventXp: number;
  rewardType: string;
  rewardAmount: number;
}

export interface SeasonalPurchaseResult {
  applied: boolean;
  idempotentReplay: boolean;
  eventId: string;
  itemId: string;
  transactionId: string;
  ticketPrice: number;
  ticketBalance: number;
  owned: boolean;
}

const DAILY_REWARD_GOLD = 250;
const DAILY_SCHEMA_VERSION = 1;
const EVENT_SCHEMA_VERSION = 1;
const SEASONAL_SCHEMA_VERSION = 1;
const MAX_SAFE_BALANCE = Number.MAX_SAFE_INTEGER;

/**
 * Returns a SHA-256 hex digest for a deterministic source string.
 * @param {string} value Source value to hash.
 * @return {string} Lowercase SHA-256 hexadecimal digest.
 */
function sha256(value: string): string {
  return createHash("sha256")
    .update(value, "utf8")
    .digest("hex");
}

/**
 * Formats a Date as the authoritative UTC calendar-day key YYYYMMDD.
 * @param {Date} now Date to format; defaults to the current server time.
 * @return {string} UTC day key in YYYYMMDD form.
 */
function utcDay(now = new Date()): string {
  const year = now.getUTCFullYear();
  const month = String(now.getUTCMonth() + 1).padStart(2, "0");
  const day = String(now.getUTCDate()).padStart(2, "0");
  return `${year}${month}${day}`;
}

/**
 * Returns the Monday-based UTC week key used for weekly challenge claims.
 * @param {Date} now Date to inspect; defaults to the current server time.
 * @return {string} UTC Monday key in YYYYMMDD form.
 */
function utcWeek(now = new Date()): string {
  const copy = new Date(now.getTime());
  const day = copy.getUTCDay();
  const delta = day === 0 ? -6 : 1 - day;
  copy.setUTCDate(copy.getUTCDate() + delta);
  return utcDay(copy);
}

/**
 * Reads a stored non-negative safe integer and rejects invalid state.
 * @param {unknown} value Stored value to validate.
 * @param {string} fieldName Field name included in diagnostic details.
 * @return {number} Validated non-negative safe integer.
 */
function readNonNegativeInteger(
  value: unknown,
  fieldName: string,
): number {
  if (value === undefined) return 0;

  if (
    typeof value !== "number" ||
    !Number.isSafeInteger(value) ||
    value < 0
  ) {
    throw new HttpsError(
      "internal",
      "SEASONAL_STATE_INVALID",
      {
        errorKey: "seasonal.error.state_invalid",
        fieldName,
      },
    );
  }

  return value;
}

/**
 * Reads one required string and rejects malformed server state.
 * @param {unknown} value Stored value to validate.
 * @param {string} fieldName Field name included in diagnostic details.
 * @return {string} Validated non-empty string.
 */
function readRequiredString(value: unknown, fieldName: string): string {
  if (typeof value !== "string" || value.trim().length === 0) {
    throw new HttpsError(
      "internal",
      "SEASONAL_STATE_INVALID",
      {errorKey: "seasonal.error.state_invalid", fieldName},
    );
  }
  return value;
}

/**
 * Returns a safe optional string.
 * @param {unknown} value Stored value.
 * @return {string} String value or empty string.
 */
function readOptionalString(value: unknown): string {
  return typeof value === "string" ? value : "";
}

/**
 * Claims the account's server-authoritative UTC daily reward exactly once.
 * The client does not supply a day, amount, balance, or transaction id.
 * @param {string} uid Authenticated account id receiving the reward.
 * @param {string} source Trusted caller/source label stored in audit evidence.
 * @return {Promise<DailyRewardClaimResult>} Authoritative claim result.
 */
export async function claimDailyReward(
  uid: string,
  source: string,
): Promise<DailyRewardClaimResult> {
  const db = getFirestore();
  const serverDayUtc = utcDay();
  const claimId = sha256(`${uid}:daily_reward:${serverDayUtc}`);
  const transactionId = `daily_reward:${serverDayUtc}:${claimId}`;
  const ledgerEntryId = sha256(`${uid}:${transactionId}`);

  const claimRef = db
    .collection("daily_reward_claims")
    .doc(uid)
    .collection("days")
    .doc(serverDayUtc);

  const balanceRef = db
    .collection("wallets")
    .doc(uid)
    .collection("balances")
    .doc("gold");

  const ledgerRef = db
    .collection("wallet_ledger")
    .doc(ledgerEntryId);

  return db.runTransaction(async (transaction) => {
    const [claimSnapshot, balanceSnapshot, ledgerSnapshot] =
      await Promise.all([
        transaction.get(claimRef),
        transaction.get(balanceRef),
        transaction.get(ledgerRef),
      ]);

    if (claimSnapshot.exists) {
      const claim = claimSnapshot.data() ?? {};
      return {
        applied: false,
        idempotentReplay: true,
        claimId: String(claim.claimId ?? claimId),
        transactionId: String(claim.transactionId ?? transactionId),
        ledgerEntryId: String(claim.ledgerEntryId ?? ledgerEntryId),
        rewardGold: readNonNegativeInteger(claim.rewardGold, "rewardGold"),
        balanceBefore: readNonNegativeInteger(
          claim.balanceBefore,
          "balanceBefore",
        ),
        balanceAfter: readNonNegativeInteger(
          claim.balanceAfter,
          "balanceAfter",
        ),
        serverDayUtc,
      };
    }

    if (ledgerSnapshot.exists) {
      throw new HttpsError(
        "internal",
        "DAILY_REWARD_PARTIAL_STATE",
        {errorKey: "seasonal.error.partial_state"},
      );
    }

    const balanceBefore = readNonNegativeInteger(
      balanceSnapshot.data()?.amount,
      "wallet.amount",
    );
    const balanceAfter = balanceBefore + DAILY_REWARD_GOLD;

    if (!Number.isSafeInteger(balanceAfter)) {
      throw new HttpsError(
        "out-of-range",
        "DAILY_REWARD_BALANCE_OUT_OF_RANGE",
        {errorKey: "economy.error.balance_out_of_range"},
      );
    }

    const serverTimestamp = FieldValue.serverTimestamp();

    transaction.set(
      balanceRef,
      {
        uid,
        currencyId: "gold",
        amount: balanceAfter,
        schemaVersion: 1,
        updatedAt: serverTimestamp,
      },
      {merge: false},
    );

    transaction.create(ledgerRef, {
      uid,
      currencyId: "gold",
      delta: DAILY_REWARD_GOLD,
      balanceBefore,
      balanceAfter,
      reason: "daily_reward",
      transactionId,
      idempotencyKeyHash: ledgerEntryId,
      actorType: "seasonal_backend",
      actorUid: uid,
      source,
      schemaVersion: 1,
      createdAt: serverTimestamp,
    });

    transaction.create(claimRef, {
      uid,
      claimId,
      transactionId,
      ledgerEntryId,
      rewardGold: DAILY_REWARD_GOLD,
      balanceBefore,
      balanceAfter,
      serverDayUtc,
      source,
      schemaVersion: DAILY_SCHEMA_VERSION,
      createdAt: serverTimestamp,
    });

    return {
      applied: true,
      idempotentReplay: false,
      claimId,
      transactionId,
      ledgerEntryId,
      rewardGold: DAILY_REWARD_GOLD,
      balanceBefore,
      balanceAfter,
      serverDayUtc,
    };
  });
}

/**
 * Reads and validates the active seasonal-event definition.
 * @return {Promise<SeasonalEventSnapshot>} Current event snapshot.
 */
export async function getActiveSeasonalEvent(): Promise<SeasonalEventSnapshot> {
  const db = getFirestore();
  const snapshot = await db
    .collection("seasonal_runtime")
    .doc("active")
    .get();

  if (!snapshot.exists) {
    return {
      active: false,
      eventId: "",
      titleKey: "",
      subtitleKey: "",
      startsAtEpochMs: 0,
      endsAtEpochMs: 0,
      ticketCurrencyId: "",
    };
  }

  const data = snapshot.data() ?? {};
  const now = Date.now();
  const startsAtEpochMs = readNonNegativeInteger(
    data.startsAtEpochMs,
    "startsAtEpochMs",
  );
  const endsAtEpochMs = readNonNegativeInteger(
    data.endsAtEpochMs,
    "endsAtEpochMs",
  );
  const configuredActive = data.active === true;
  const active =
    configuredActive &&
    startsAtEpochMs <= now &&
    now <= endsAtEpochMs;

  if (data.schemaVersion !== EVENT_SCHEMA_VERSION) {
    throw new HttpsError(
      "internal",
      "SEASONAL_EVENT_STATE_INVALID",
      {errorKey: "seasonal.error.state_invalid"},
    );
  }

  return {
    active,
    eventId: readOptionalString(data.eventId),
    titleKey: readOptionalString(data.titleKey),
    subtitleKey: readOptionalString(data.subtitleKey),
    startsAtEpochMs,
    endsAtEpochMs,
    ticketCurrencyId: readOptionalString(data.ticketCurrencyId),
  };
}

/**
 * Returns the claim-period key for one challenge cadence.
 * @param {string} cadence Challenge cadence.
 * @return {string} Canonical period key.
 */
function challengePeriodKey(cadence: string): string {
  if (cadence === "daily") return utcDay();
  if (cadence === "weekly") return utcWeek();
  return "season";
}

/**
 * Computes authoritative progress for one supported challenge criterion.
 * @param {string} uid Authenticated account id.
 * @param {string} criteriaType Configured criterion.
 * @param {number} target Configured target.
 * @return {Promise<number>} Current authoritative progress.
 */
async function challengeProgress(
  uid: string,
  criteriaType: string,
  target: number,
): Promise<number> {
  const db = getFirestore();

  if (criteriaType === "daily_reward_claimed") {
    const snapshot = await db
      .collection("daily_reward_claims")
      .doc(uid)
      .collection("days")
      .doc(utcDay())
      .get();
    return snapshot.exists ? 1 : 0;
  }

  if (criteriaType === "owned_items") {
    const snapshot = await db
      .collection("inventories")
      .doc(uid)
      .collection("items")
      .where("owned", "==", true)
      .limit(Math.max(1, target))
      .get();
    return snapshot.size;
  }

  throw new HttpsError(
    "internal",
    "SEASONAL_CRITERIA_UNSUPPORTED",
    {errorKey: "seasonal.error.criteria_unsupported"},
  );
}

/**
 * Reads one wallet-style balance without mutating it.
 * @param {string} uid Authenticated account id.
 * @param {string} currencyId Currency id to inspect.
 * @return {Promise<number>} Current balance.
 */
async function readBalance(uid: string, currencyId: string): Promise<number> {
  if (!currencyId) return 0;
  const snapshot = await getFirestore()
    .collection("wallets")
    .doc(uid)
    .collection("balances")
    .doc(currencyId)
    .get();
  return readNonNegativeInteger(snapshot.data()?.amount, "wallet.amount");
}

/**
 * Reads a user-facing seasonal overview from authoritative server state.
 * @param {string} uid Authenticated account id.
 * @return {Promise<SeasonalOverviewResult>} Current event overview.
 */
export async function getSeasonalOverview(
  uid: string,
): Promise<SeasonalOverviewResult> {
  const db = getFirestore();
  const event = await getActiveSeasonalEvent();

  if (!event.active || !event.eventId) {
    return {
      event,
      ticketBalance: 0,
      eventXp: 0,
      challenges: [],
      trackTiers: [],
      limitedItems: [],
    };
  }

  const ticketBalance = await readBalance(uid, event.ticketCurrencyId);
  const progressSnapshot = await db
    .collection("seasonal_progress")
    .doc(uid)
    .collection("events")
    .doc(event.eventId)
    .get();
  const eventXp = readNonNegativeInteger(
    progressSnapshot.data()?.xp,
    "seasonal_progress.xp",
  );

  const challengeDefinitions = await db
    .collection("seasonal_challenges")
    .doc(event.eventId)
    .collection("definitions")
    .get();

  const challenges: SeasonalChallengeSnapshot[] = [];
  for (const definition of challengeDefinitions.docs) {
    const data = definition.data() ?? {};
    if (data.active !== true) continue;

    const challengeId = readRequiredString(
      data.challengeId ?? definition.id,
      "challengeId",
    );
    const cadence = readRequiredString(data.cadence, "cadence");
    const target = Math.max(
      1,
      readNonNegativeInteger(data.target, "target"),
    );
    const progress = await challengeProgress(
      uid,
      readRequiredString(data.criteriaType, "criteriaType"),
      target,
    );
    const periodKey = challengePeriodKey(cadence);
    const claimId = `${periodKey}_${challengeId}`;
    const claim = await db
      .collection("seasonal_challenge_claims")
      .doc(uid)
      .collection("events")
      .doc(event.eventId)
      .collection("claims")
      .doc(claimId)
      .get();

    challenges.push({
      challengeId,
      titleKey: readOptionalString(data.titleKey),
      descriptionKey: readOptionalString(data.descriptionKey),
      cadence,
      target,
      progress,
      rewardTickets: readNonNegativeInteger(
        data.rewardTickets,
        "rewardTickets",
      ),
      rewardXp: readNonNegativeInteger(data.rewardXp, "rewardXp"),
      claimed: claim.exists,
      claimable: progress >= target && !claim.exists,
      periodKey,
      sortOrder: readNonNegativeInteger(data.sortOrder, "sortOrder"),
    });
  }

  const tierDefinitions = await db
    .collection("seasonal_tracks")
    .doc(event.eventId)
    .collection("tiers")
    .get();
  const tiers: SeasonalTrackTierSnapshot[] = [];
  for (const tierDefinition of tierDefinitions.docs) {
    const data = tierDefinition.data() ?? {};
    if (data.active !== true) continue;
    const tierId = readRequiredString(
      data.tierId ?? tierDefinition.id,
      "tierId",
    );
    const claim = await db
      .collection("seasonal_track_claims")
      .doc(uid)
      .collection("events")
      .doc(event.eventId)
      .collection("tiers")
      .doc(tierId)
      .get();
    const requiredXp = readNonNegativeInteger(
      data.requiredXp,
      "requiredXp",
    );
    tiers.push({
      tierId,
      titleKey: readOptionalString(data.titleKey),
      requiredXp,
      rewardType: readRequiredString(data.rewardType, "rewardType"),
      rewardAmount: readNonNegativeInteger(
        data.rewardAmount,
        "rewardAmount",
      ),
      claimed: claim.exists,
      claimable: eventXp >= requiredXp && !claim.exists,
      sortOrder: readNonNegativeInteger(data.sortOrder, "sortOrder"),
    });
  }

  const catalog = await db.collection("item_catalog").get();
  const limitedItems: SeasonalLimitedItemSnapshot[] = [];
  for (const document of catalog.docs) {
    const data = document.data() ?? {};
    if (
      data.active !== true ||
      data.seasonalEventId !== event.eventId
    ) {
      continue;
    }
    const limitedUntilEpochMs = readNonNegativeInteger(
      data.limitedUntilEpochMs,
      "limitedUntilEpochMs",
    );
    if (limitedUntilEpochMs < Date.now()) continue;
    const itemId = readRequiredString(
      data.itemId ?? document.id,
      "itemId",
    );
    const inventory = await db
      .collection("inventories")
      .doc(uid)
      .collection("items")
      .doc(itemId)
      .get();
    limitedItems.push({
      itemId,
      itemType: readRequiredString(data.itemType, "itemType"),
      displayName: readOptionalString(data.displayName) || itemId,
      ticketPrice: readNonNegativeInteger(
        data.seasonalTicketPrice,
        "seasonalTicketPrice",
      ),
      limitedUntilEpochMs,
      owned: inventory.data()?.owned === true,
      sortOrder: readNonNegativeInteger(data.sortOrder, "sortOrder"),
    });
  }

  challenges.sort((a, b) => a.sortOrder - b.sortOrder);
  tiers.sort((a, b) => a.sortOrder - b.sortOrder);
  limitedItems.sort((a, b) => a.sortOrder - b.sortOrder);

  return {
    event,
    ticketBalance,
    eventXp,
    challenges,
    trackTiers: tiers,
    limitedItems,
  };
}

/**
 * Claims one completed daily/weekly challenge exactly once per period.
 * @param {string} uid Authenticated account id.
 * @param {string} challengeId Challenge definition id.
 * @param {string} source Trusted caller/source label.
 * @return {Promise<SeasonalClaimResult>} Authoritative claim result.
 */
export async function claimSeasonalChallenge(
  uid: string,
  challengeId: string,
  source: string,
): Promise<SeasonalClaimResult> {
  const db = getFirestore();
  const event = await getActiveSeasonalEvent();
  if (!event.active || !event.eventId) {
    throw new HttpsError(
      "failed-precondition",
      "SEASONAL_EVENT_INACTIVE",
      {errorKey: "seasonal.error.event_inactive"},
    );
  }

  const definitionRef = db
    .collection("seasonal_challenges")
    .doc(event.eventId)
    .collection("definitions")
    .doc(challengeId);
  const definitionSnapshot = await definitionRef.get();
  if (!definitionSnapshot.exists) {
    throw new HttpsError(
      "not-found",
      "SEASONAL_CHALLENGE_NOT_FOUND",
      {errorKey: "seasonal.error.challenge_not_found"},
    );
  }
  const definition = definitionSnapshot.data() ?? {};
  if (definition.active !== true) {
    throw new HttpsError(
      "failed-precondition",
      "SEASONAL_CHALLENGE_INACTIVE",
      {errorKey: "seasonal.error.challenge_inactive"},
    );
  }

  const cadence = readRequiredString(definition.cadence, "cadence");
  const target = Math.max(
    1,
    readNonNegativeInteger(definition.target, "target"),
  );
  const currentProgress = await challengeProgress(
    uid,
    readRequiredString(definition.criteriaType, "criteriaType"),
    target,
  );
  if (currentProgress < target) {
    throw new HttpsError(
      "failed-precondition",
      "SEASONAL_CHALLENGE_NOT_COMPLETE",
      {
        errorKey: "seasonal.error.challenge_not_complete",
        progress: currentProgress,
        target,
      },
    );
  }

  const rewardTickets = readNonNegativeInteger(
    definition.rewardTickets,
    "rewardTickets",
  );
  const rewardXp = readNonNegativeInteger(definition.rewardXp, "rewardXp");
  const periodKey = challengePeriodKey(cadence);
  const referenceId = `${periodKey}_${challengeId}`;
  const transactionId = sha256(
    `${uid}:${event.eventId}:challenge:${referenceId}`,
  );
  const ledgerEntryId = sha256(`${uid}:${transactionId}:ticket`);
  const claimRef = db
    .collection("seasonal_challenge_claims")
    .doc(uid)
    .collection("events")
    .doc(event.eventId)
    .collection("claims")
    .doc(referenceId);
  const ticketRef = db
    .collection("wallets")
    .doc(uid)
    .collection("balances")
    .doc(event.ticketCurrencyId);
  const progressRef = db
    .collection("seasonal_progress")
    .doc(uid)
    .collection("events")
    .doc(event.eventId);
  const ledgerRef = db.collection("wallet_ledger").doc(ledgerEntryId);

  return db.runTransaction(async (transaction) => {
    const [claimSnapshot, ticketSnapshot, progressSnapshot, ledgerSnapshot] =
      await Promise.all([
        transaction.get(claimRef),
        transaction.get(ticketRef),
        transaction.get(progressRef),
        transaction.get(ledgerRef),
      ]);

    if (claimSnapshot.exists) {
      const data = claimSnapshot.data() ?? {};
      return {
        applied: false,
        idempotentReplay: true,
        eventId: event.eventId,
        referenceId,
        transactionId: String(data.transactionId ?? transactionId),
        ticketBalance: readNonNegativeInteger(
          data.ticketBalanceAfter,
          "ticketBalanceAfter",
        ),
        eventXp: readNonNegativeInteger(data.eventXpAfter, "eventXpAfter"),
        rewardType: "challenge",
        rewardAmount: rewardTickets,
      };
    }

    if (ledgerSnapshot.exists && rewardTickets > 0) {
      throw new HttpsError(
        "internal",
        "SEASONAL_PARTIAL_STATE",
        {errorKey: "seasonal.error.partial_state"},
      );
    }

    const ticketBefore = readNonNegativeInteger(
      ticketSnapshot.data()?.amount,
      "ticketBalance",
    );
    const xpBefore = readNonNegativeInteger(
      progressSnapshot.data()?.xp,
      "seasonal_progress.xp",
    );
    const ticketAfter = ticketBefore + rewardTickets;
    const xpAfter = xpBefore + rewardXp;
    if (
      ticketAfter > MAX_SAFE_BALANCE ||
      xpAfter > MAX_SAFE_BALANCE
    ) {
      throw new HttpsError(
        "out-of-range",
        "SEASONAL_BALANCE_OUT_OF_RANGE",
        {errorKey: "seasonal.error.balance_out_of_range"},
      );
    }

    const serverTimestamp = FieldValue.serverTimestamp();
    transaction.set(
      ticketRef,
      {
        uid,
        currencyId: event.ticketCurrencyId,
        amount: ticketAfter,
        schemaVersion: SEASONAL_SCHEMA_VERSION,
        updatedAt: serverTimestamp,
      },
      {merge: false},
    );
    transaction.set(
      progressRef,
      {
        uid,
        eventId: event.eventId,
        xp: xpAfter,
        schemaVersion: SEASONAL_SCHEMA_VERSION,
        updatedAt: serverTimestamp,
      },
      {merge: true},
    );
    if (rewardTickets > 0) {
      transaction.create(ledgerRef, {
        uid,
        currencyId: event.ticketCurrencyId,
        delta: rewardTickets,
        balanceBefore: ticketBefore,
        balanceAfter: ticketAfter,
        reason: "seasonal_challenge",
        transactionId,
        idempotencyKeyHash: ledgerEntryId,
        actorType: "seasonal_backend",
        actorUid: uid,
        source,
        schemaVersion: SEASONAL_SCHEMA_VERSION,
        createdAt: serverTimestamp,
      });
    }
    transaction.create(claimRef, {
      uid,
      eventId: event.eventId,
      challengeId,
      cadence,
      periodKey,
      transactionId,
      rewardTickets,
      rewardXp,
      ticketBalanceBefore: ticketBefore,
      ticketBalanceAfter: ticketAfter,
      eventXpBefore: xpBefore,
      eventXpAfter: xpAfter,
      source,
      schemaVersion: SEASONAL_SCHEMA_VERSION,
      createdAt: serverTimestamp,
    });

    return {
      applied: true,
      idempotentReplay: false,
      eventId: event.eventId,
      referenceId,
      transactionId,
      ticketBalance: ticketAfter,
      eventXp: xpAfter,
      rewardType: "challenge",
      rewardAmount: rewardTickets,
    };
  });
}

/**
 * Claims one unlocked seasonal reward-track tier exactly once.
 * @param {string} uid Authenticated account id.
 * @param {string} tierId Reward-track tier id.
 * @param {string} source Trusted caller/source label.
 * @return {Promise<SeasonalClaimResult>} Authoritative claim result.
 */
export async function claimSeasonalTrackTier(
  uid: string,
  tierId: string,
  source: string,
): Promise<SeasonalClaimResult> {
  const db = getFirestore();
  const event = await getActiveSeasonalEvent();
  if (!event.active || !event.eventId) {
    throw new HttpsError(
      "failed-precondition",
      "SEASONAL_EVENT_INACTIVE",
      {errorKey: "seasonal.error.event_inactive"},
    );
  }

  const tierRef = db
    .collection("seasonal_tracks")
    .doc(event.eventId)
    .collection("tiers")
    .doc(tierId);
  const tierSnapshot = await tierRef.get();
  if (!tierSnapshot.exists) {
    throw new HttpsError(
      "not-found",
      "SEASONAL_TRACK_TIER_NOT_FOUND",
      {errorKey: "seasonal.error.track_not_found"},
    );
  }
  const tier = tierSnapshot.data() ?? {};
  const requiredXp = readNonNegativeInteger(tier.requiredXp, "requiredXp");
  const rewardType = readRequiredString(tier.rewardType, "rewardType");
  const rewardAmount = readNonNegativeInteger(
    tier.rewardAmount,
    "rewardAmount",
  );
  if (!["gold", "atlas_coin", "event_ticket"].includes(rewardType)) {
    throw new HttpsError(
      "internal",
      "SEASONAL_TRACK_REWARD_INVALID",
      {errorKey: "seasonal.error.state_invalid"},
    );
  }

  const progressRef = db
    .collection("seasonal_progress")
    .doc(uid)
    .collection("events")
    .doc(event.eventId);
  const progressSnapshot = await progressRef.get();
  const xp = readNonNegativeInteger(
    progressSnapshot.data()?.xp,
    "seasonal_progress.xp",
  );
  if (xp < requiredXp) {
    throw new HttpsError(
      "failed-precondition",
      "SEASONAL_TRACK_LOCKED",
      {
        errorKey: "seasonal.error.track_locked",
        requiredXp,
        currentXp: xp,
      },
    );
  }

  const currencyId = rewardType === "event_ticket" ?
    event.ticketCurrencyId :
    rewardType;
  const claimRef = db
    .collection("seasonal_track_claims")
    .doc(uid)
    .collection("events")
    .doc(event.eventId)
    .collection("tiers")
    .doc(tierId);
  const balanceRef = db
    .collection("wallets")
    .doc(uid)
    .collection("balances")
    .doc(currencyId);
  const transactionId = sha256(
    `${uid}:${event.eventId}:track:${tierId}`,
  );
  const ledgerEntryId = sha256(`${uid}:${transactionId}`);
  const ledgerRef = db.collection("wallet_ledger").doc(ledgerEntryId);

  return db.runTransaction(async (transaction) => {
    const [claimSnapshot, balanceSnapshot, ledgerSnapshot] =
      await Promise.all([
        transaction.get(claimRef),
        transaction.get(balanceRef),
        transaction.get(ledgerRef),
      ]);

    if (claimSnapshot.exists) {
      const data = claimSnapshot.data() ?? {};
      return {
        applied: false,
        idempotentReplay: true,
        eventId: event.eventId,
        referenceId: tierId,
        transactionId: String(data.transactionId ?? transactionId),
        ticketBalance: rewardType === "event_ticket" ?
          readNonNegativeInteger(data.balanceAfter, "balanceAfter") :
          -1,
        eventXp: xp,
        rewardType,
        rewardAmount,
      };
    }

    if (ledgerSnapshot.exists) {
      throw new HttpsError(
        "internal",
        "SEASONAL_PARTIAL_STATE",
        {errorKey: "seasonal.error.partial_state"},
      );
    }

    const balanceBefore = readNonNegativeInteger(
      balanceSnapshot.data()?.amount,
      "wallet.amount",
    );
    const balanceAfter = balanceBefore + rewardAmount;
    if (balanceAfter > MAX_SAFE_BALANCE) {
      throw new HttpsError(
        "out-of-range",
        "SEASONAL_BALANCE_OUT_OF_RANGE",
        {errorKey: "seasonal.error.balance_out_of_range"},
      );
    }
    const serverTimestamp = FieldValue.serverTimestamp();
    transaction.set(
      balanceRef,
      {
        uid,
        currencyId,
        amount: balanceAfter,
        schemaVersion: SEASONAL_SCHEMA_VERSION,
        updatedAt: serverTimestamp,
      },
      {merge: false},
    );
    transaction.create(ledgerRef, {
      uid,
      currencyId,
      delta: rewardAmount,
      balanceBefore,
      balanceAfter,
      reason: "seasonal_track",
      transactionId,
      idempotencyKeyHash: ledgerEntryId,
      actorType: "seasonal_backend",
      actorUid: uid,
      source,
      schemaVersion: SEASONAL_SCHEMA_VERSION,
      createdAt: serverTimestamp,
    });
    transaction.create(claimRef, {
      uid,
      eventId: event.eventId,
      tierId,
      requiredXp,
      rewardType,
      currencyId,
      rewardAmount,
      balanceBefore,
      balanceAfter,
      transactionId,
      source,
      schemaVersion: SEASONAL_SCHEMA_VERSION,
      createdAt: serverTimestamp,
    });

    return {
      applied: true,
      idempotentReplay: false,
      eventId: event.eventId,
      referenceId: tierId,
      transactionId,
      ticketBalance: rewardType === "event_ticket" ?
        balanceAfter :
        -1,
      eventXp: xp,
      rewardType,
      rewardAmount,
    };
  });
}

/**
 * Purchases one active limited seasonal cosmetic with event tickets.
 * @param {string} uid Authenticated account id.
 * @param {string} itemId Catalog item id.
 * @param {string} idempotencyKey Stable caller operation id.
 * @param {string} source Trusted caller/source label.
 * @return {Promise<SeasonalPurchaseResult>} Purchase result.
 */
export async function purchaseSeasonalItem(
  uid: string,
  itemId: string,
  idempotencyKey: string,
  source: string,
): Promise<SeasonalPurchaseResult> {
  const db = getFirestore();
  const event = await getActiveSeasonalEvent();
  if (!event.active || !event.eventId) {
    throw new HttpsError(
      "failed-precondition",
      "SEASONAL_EVENT_INACTIVE",
      {errorKey: "seasonal.error.event_inactive"},
    );
  }
  if (!itemId || !idempotencyKey) {
    throw new HttpsError(
      "invalid-argument",
      "SEASONAL_PURCHASE_INVALID",
      {errorKey: "seasonal.error.invalid_purchase"},
    );
  }

  const catalogRef = db.collection("item_catalog").doc(itemId);
  const catalogSnapshot = await catalogRef.get();
  if (!catalogSnapshot.exists) {
    throw new HttpsError(
      "not-found",
      "SEASONAL_ITEM_NOT_FOUND",
      {errorKey: "seasonal.error.item_not_found"},
    );
  }
  const catalog = catalogSnapshot.data() ?? {};
  const limitedUntilEpochMs = readNonNegativeInteger(
    catalog.limitedUntilEpochMs,
    "limitedUntilEpochMs",
  );
  if (
    catalog.active !== true ||
    catalog.seasonalEventId !== event.eventId ||
    limitedUntilEpochMs < Date.now()
  ) {
    throw new HttpsError(
      "failed-precondition",
      "SEASONAL_ITEM_UNAVAILABLE",
      {errorKey: "seasonal.error.item_unavailable"},
    );
  }
  const ticketPrice = readNonNegativeInteger(
    catalog.seasonalTicketPrice,
    "seasonalTicketPrice",
  );
  if (ticketPrice <= 0) {
    throw new HttpsError(
      "internal",
      "SEASONAL_ITEM_PRICE_INVALID",
      {errorKey: "seasonal.error.state_invalid"},
    );
  }
  const itemType = readRequiredString(catalog.itemType, "itemType");
  const transactionId = sha256(
    `${uid}:${event.eventId}:seasonal_purchase:${idempotencyKey}`,
  );
  const purchaseRef = db
    .collection("seasonal_purchases")
    .doc(transactionId);
  const inventoryRef = db
    .collection("inventories")
    .doc(uid)
    .collection("items")
    .doc(itemId);
  const ticketRef = db
    .collection("wallets")
    .doc(uid)
    .collection("balances")
    .doc(event.ticketCurrencyId);
  const ledgerEntryId = sha256(`${uid}:${transactionId}:ticket`);
  const ledgerRef = db.collection("wallet_ledger").doc(ledgerEntryId);
  const inventoryEventId = sha256(`${uid}:${itemId}:${transactionId}`);
  const inventoryEventRef = inventoryRef
    .collection("events")
    .doc(inventoryEventId);

  return db.runTransaction(async (transaction) => {
    const [
      purchaseSnapshot,
      inventorySnapshot,
      ticketSnapshot,
      ledgerSnapshot,
      inventoryEventSnapshot,
    ] = await Promise.all([
      transaction.get(purchaseRef),
      transaction.get(inventoryRef),
      transaction.get(ticketRef),
      transaction.get(ledgerRef),
      transaction.get(inventoryEventRef),
    ]);

    if (purchaseSnapshot.exists) {
      const data = purchaseSnapshot.data() ?? {};
      if (data.uid !== uid || data.itemId !== itemId) {
        throw new HttpsError(
          "already-exists",
          "SEASONAL_IDEMPOTENCY_CONFLICT",
          {errorKey: "seasonal.error.idempotency_conflict"},
        );
      }
      return {
        applied: false,
        idempotentReplay: true,
        eventId: event.eventId,
        itemId,
        transactionId,
        ticketPrice,
        ticketBalance: readNonNegativeInteger(
          data.ticketBalanceAfter,
          "ticketBalanceAfter",
        ),
        owned: true,
      };
    }

    if (inventorySnapshot.data()?.owned === true) {
      throw new HttpsError(
        "already-exists",
        "SEASONAL_ITEM_ALREADY_OWNED",
        {errorKey: "seasonal.error.already_owned"},
      );
    }
    if (ledgerSnapshot.exists || inventoryEventSnapshot.exists) {
      throw new HttpsError(
        "internal",
        "SEASONAL_PARTIAL_STATE",
        {errorKey: "seasonal.error.partial_state"},
      );
    }

    const ticketBefore = readNonNegativeInteger(
      ticketSnapshot.data()?.amount,
      "ticketBalance",
    );
    if (ticketBefore < ticketPrice) {
      throw new HttpsError(
        "failed-precondition",
        "SEASONAL_INSUFFICIENT_TICKETS",
        {
          errorKey: "seasonal.error.insufficient_tickets",
          required: ticketPrice,
          available: ticketBefore,
        },
      );
    }
    const ticketAfter = ticketBefore - ticketPrice;
    const serverTimestamp = FieldValue.serverTimestamp();

    transaction.set(
      ticketRef,
      {
        uid,
        currencyId: event.ticketCurrencyId,
        amount: ticketAfter,
        schemaVersion: SEASONAL_SCHEMA_VERSION,
        updatedAt: serverTimestamp,
      },
      {merge: false},
    );
    transaction.create(ledgerRef, {
      uid,
      currencyId: event.ticketCurrencyId,
      delta: -ticketPrice,
      balanceBefore: ticketBefore,
      balanceAfter: ticketAfter,
      reason: "seasonal_purchase",
      transactionId,
      idempotencyKeyHash: ledgerEntryId,
      actorType: "seasonal_backend",
      actorUid: uid,
      source,
      schemaVersion: SEASONAL_SCHEMA_VERSION,
      createdAt: serverTimestamp,
    });
    transaction.set(
      inventoryRef,
      {
        uid,
        itemId,
        itemType,
        owned: true,
        quantity: 1,
        lastGrantedAt: serverTimestamp,
        firstGrantedAt: serverTimestamp,
        lastTransactionId: transactionId,
        lastSource: source,
        schemaVersion: SEASONAL_SCHEMA_VERSION,
        updatedAt: serverTimestamp,
      },
      {merge: true},
    );
    transaction.create(inventoryEventRef, {
      uid,
      itemId,
      itemType,
      operation: "grant",
      ownedBefore: false,
      ownedAfter: true,
      reason: "seasonal_purchase",
      transactionId,
      idempotencyKeyHash: inventoryEventId,
      actorType: "seasonal_backend",
      actorUid: uid,
      source,
      schemaVersion: SEASONAL_SCHEMA_VERSION,
      createdAt: serverTimestamp,
    });
    transaction.create(purchaseRef, {
      uid,
      eventId: event.eventId,
      itemId,
      itemType,
      transactionId,
      ticketCurrencyId: event.ticketCurrencyId,
      ticketPrice,
      ticketBalanceBefore: ticketBefore,
      ticketBalanceAfter: ticketAfter,
      inventoryEventId,
      source,
      schemaVersion: SEASONAL_SCHEMA_VERSION,
      createdAt: serverTimestamp,
    });

    return {
      applied: true,
      idempotentReplay: false,
      eventId: event.eventId,
      itemId,
      transactionId,
      ticketPrice,
      ticketBalance: ticketAfter,
      owned: true,
    };
  });
}
