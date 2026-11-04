/**
 * Atlas Dünya Yolculuğu: the RTDB-backed live Meta Economy extension.
 * No second wallet is created. Existing Firestore emulator probes stay intact.
 * All reward writes are authenticated Admin transactions on the canonical UID.
 */
import {createHash} from "crypto";
import {getDatabaseWithUrl} from "firebase-admin/database";
import {HttpsError} from "firebase-functions/v2/https";

// Use the exact named RTDB. Do not accidentally write to the default US
// endpoint.
const database = () => getDatabaseWithUrl(
  "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app");

export const WORLD_EVENT_ID = "world_journey_2027_s1";
export const WORLD_MAP_ID = "map_world";
export const WORLD_BOARD_ID = "board_atlas_horizon";
export const WORLD_EVENT_START = Date.UTC(2026, 10, 3, 0, 0, 0);
export const WORLD_EVENT_END_EXCLUSIVE = Date.UTC(2027, 6, 1, 0, 0, 0);
export const WORLD_EVENT_TITLE = "Atlas World Journey";

export type RewardType = "gold" | "xp" | "atlas_coin" | "inventory_item";
interface Reward {
  type: RewardType;
  amount: number;
  itemId?: string;
  itemType?: string;
}
interface MissionDefinition {
  id: string; criterion: string; target: number; reward: Reward;
}
const MISSIONS: MissionDefinition[] = [
  {id: "two_matches", criterion: "matches", target: 2,
    reward: {type: "gold", amount: 500}},
  {id: "fifty_rolls", criterion: "diceRolls", target: 50,
    reward: {type: "xp", amount: 500}},
  {id: "ten_developments", criterion: "developments", target: 10,
    reward: {type: "atlas_coin", amount: 100}},
  {id: "three_wins", criterion: "wins", target: 3,
    reward: {type: "gold", amount: 1000}},
  {id: "twenty_turns", criterion: "turns", target: 20,
    reward: {type: "xp", amount: 1000}},
  {id: "three_maps", criterion: "distinctMaps", target: 3,
    reward: {type: "atlas_coin", amount: 250}},
  {id: "networth_5000", criterion: "highestNetWorth", target: 5000,
    reward: {type: "gold", amount: 2000}},
  {id: "world_explorer", criterion: "sevenClaims", target: 7,
    reward: {type: "inventory_item", amount: 1, itemId: WORLD_MAP_ID,
      itemType: "map"}},
];
const BONUS: Reward = {
  type: "inventory_item", amount: 1, itemId: WORLD_BOARD_ID,
  itemType: "board_theme",
};
const safeInt = (value: unknown): number =>
  typeof value === "number" && Number.isSafeInteger(value) &&
  value >= 0 ? value : 0;
const record = (value: unknown): Record<string, unknown> =>
  value && typeof value === "object" && !Array.isArray(value) ?
    value as Record<string, unknown> : {};
const sha = (value: string): string =>
  createHash("sha256").update(value).digest("hex");
const isEventActive = (now = Date.now()): boolean =>
  now >= WORLD_EVENT_START && now < WORLD_EVENT_END_EXCLUSIVE;

/**
 * Validates or processes eventData for the existing backend flow.
 * @param {*} now Validated input.
 * @return {*} Result of the existing operation.
 */
function eventData(now = Date.now()) {
  return {
    active: isEventActive(now), upcoming: now < WORLD_EVENT_START,
    eventId: WORLD_EVENT_ID, eventName: WORLD_EVENT_TITLE,
    titleKey: "world.event.title", subtitleKey: "world.event.subtitle",
    startsAtEpochMs: WORLD_EVENT_START,
    endsAtEpochMs: WORLD_EVENT_END_EXCLUSIVE - 1,
  };
}

/**
 * Progress derived solely from server-accepted signed match claims, never
 * client counters.
 * @param {*} uid Input value.
 */
async function verifiedMissionProgress(
  uid: string,
): Promise<Record<string, number>> {
  const snap = await database().ref(`careerClaims/${uid}`).get();
  const claims = record(snap.val());
  const maps = new Set<string>();
  let matches = 0; let wins = 0; let diceRolls = 0;
  let developments = 0; let turns = 0; let highestNetWorth = 0;
  for (const [matchId, raw] of Object.entries(claims)) {
    const item = record(raw);
    if (item.uid !== uid || item.matchId !== matchId ||
        safeInt(item.atMs) < WORLD_EVENT_START ||
        safeInt(item.atMs) >= WORLD_EVENT_END_EXCLUSIVE ||
        !["win", "tie", "loss"].includes(String(item.result ?? ""))) continue;
    matches++;
    if (item.result === "win") wins++;
    diceRolls += Math.min(10000, safeInt(item.diceRolls));
    developments += Math.min(100, safeInt(item.developments));
    turns += Math.min(10000, safeInt(item.turns));
    highestNetWorth = Math.max(
      highestNetWorth, Math.min(100000000, safeInt(item.netWorth)));
    if (typeof item.mapId === "string" && /^[\w-]{3,50}$/.test(item.mapId)) {
      maps.add(item.mapId.toLowerCase().replace(/^map_/, ""));
    }
  }
  return {matches, wins, diceRolls, developments, turns,
    distinctMaps: maps.size, highestNetWorth};
}

/**
 * Validates or processes progressFor for the existing backend flow.
 * @param {*} mission Validated input.
 * @param {*} counters Validated input.
 * @param {*} claimed Validated input.
 * @return {*} Result of the existing operation.
 */
function progressFor(
  mission: MissionDefinition, counters: Record<string, number>,
  claimed: Record<string, unknown>): number {
  if (mission.criterion === "sevenClaims") {
    return MISSIONS.slice(0, 7).filter((m) => claimed[m.id]).length;
  }
  return Math.min(mission.target, safeInt(counters[mission.criterion]));
}

/**
 * Overview persists authoritative per-UID progress; it does not issue
 * rewards.
 * @param {*} uid Input value.
 */
export async function worldJourneyOverview(uid: string) {
  const event = eventData();
  if (!event.active) {
    return {event, missions: [], history: [],
      bonus: {claimed: false, claimable: false},
      completedMissions: 0, claimedRewards: 0};
  }
  const userRef = database().ref(`users/${uid}`);
  const counters = await verifiedMissionProgress(uid);
  const state = record((await userRef.child(
    `seasonalEvents/${WORLD_EVENT_ID}`).get()).val());
  const claimed = record(state.rewards);
  const missions = MISSIONS.map((m) => {
    const progress = progressFor(m, counters, claimed);
    return {missionId: m.id, titleKey: `world.mission.${m.id}`,
      descriptionKey: `world.mission.${m.id}.body`, target: m.target, progress,
      rewardType: m.reward.type, rewardAmount: m.reward.amount,
      rewardItemId: m.reward.itemId ?? "", claimed: Boolean(claimed[m.id]),
      claimable: progress >= m.target && !claimed[m.id]};
  });
  const claimedRewards = missions.filter((m) => m.claimed).length;
  const bonusClaimed = Boolean(record(state.bonus).claimed);
  const bonus = {rewardType: "inventory_item", rewardItemId: WORLD_BOARD_ID,
    claimed: bonusClaimed, claimable: claimedRewards === 8 && !bonusClaimed};
  // Mission progress is evidence derived by backend, never an award.
  const missionUpdate: Record<string, unknown> = {};
  for (const mission of missions) {
    missionUpdate[mission.missionId] = {
      progress: mission.progress, target: mission.target,
      completed: mission.progress >= mission.target,
      updatedAt: Date.now(),
    };
  }
  await userRef.child(`seasonalEvents/${WORLD_EVENT_ID}/missions`)
    .update(missionUpdate);
  const history: Array<Record<string, unknown>> =
    Object.entries(record(state.history)).map(
      ([id, value]): Record<string, unknown> =>
        ({transactionId: id, ...record(value)}))
      .sort((a, b) => safeInt(b["claimedAt"]) - safeInt(a["claimedAt"]))
      .slice(0, 100);
  return {event, missions, history, bonus,
    completedMissions: missions.filter((m) =>
      m.progress >= m.target).length, claimedRewards};
}

/**
 * Validates or processes grantReward for the existing backend flow.
 * @param {*} user Validated input.
 * @param {*} reward Validated input.
 */
function grantReward(user: Record<string, unknown>, reward: Reward): void {
  const meta = record(user.metaEconomy);
  const wallet = record(meta.wallet);
  const inventory = record(meta.inventory);
  if (reward.type === "gold" || reward.type === "atlas_coin") {
    const key = reward.type === "gold" ? "gold" : "atlasCoin";
    const old = safeInt(wallet[key]);
    if (!Number.isSafeInteger(old + reward.amount)) {
      throw new HttpsError("out-of-range", "REWARD_OVERFLOW");
    }
    wallet[key] = old + reward.amount;
    meta.wallet = wallet;
  } else if (reward.type === "xp") {
    // Bonus XP is kept separate from match claims and included by Career view.
    const bonuses = record(user.seasonalRewards);
    const old = safeInt(bonuses.xp);
    if (!Number.isSafeInteger(old + reward.amount)) {
      throw new HttpsError("out-of-range", "REWARD_OVERFLOW");
    }
    bonuses.xp = old + reward.amount;
    user.seasonalRewards = bonuses;
  } else {
    const category = reward.itemType === "map" ? "maps" :
      reward.itemType === "board_theme" ? "boards" :
        reward.itemType === "profile_frame" ? "profile_frames" :
          reward.itemType === "dice_skin" ? "dice" :
            reward.itemType === "pawn" ? "pawns" :
              reward.itemType === "emote" ? "emotes" : "";
    if (!category || !reward.itemId ||
        !/^[a-zA-Z0-9_]{3,96}$/.test(reward.itemId)) {
      throw new HttpsError("invalid-argument", "REWARD_ITEM_INVALID");
    }
    const items = record(inventory[category]);
    if (items[reward.itemId] === true) {
      throw new HttpsError("already-exists", "REWARD_ALREADY_OWNED");
    }
    items[reward.itemId] = true;
    inventory[category] = items;
    meta.inventory = inventory;
  }
  user.metaEconomy = meta;
}

/**
 * One atomic account-local RTDB transaction; rewards are never auto-claimed.
 * @param {*} uid Authenticated account identifier.
 * @param {*} rewardId Mission reward identifier.
 */
export async function worldJourneyClaim(uid: string, rewardId: string) {
  if (!isEventActive()) {
    throw new HttpsError("failed-precondition", "EVENT_NOT_ACTIVE");
  }
  const mission = MISSIONS.find((m) => m.id === rewardId);
  if (!mission && rewardId !== "completion_bonus") {
    throw new HttpsError("not-found", "REWARD_NOT_FOUND");
  }
  const counters = await verifiedMissionProgress(uid);
  const transactionId = sha(`${uid}:${WORLD_EVENT_ID}:${rewardId}`);
  const now = Date.now();
  const outcome = await database().ref(
    `users/${uid}`).transaction((data: unknown) => {
    if (!data || typeof data !== "object") return;
    const user = data as Record<string, unknown>;
    const eventState = record(user.seasonalEvents);
    const season = record(eventState[WORLD_EVENT_ID]);
    const claimed = record(season.rewards);
    if (mission && claimed[rewardId]) return; // abort without a second grant.
    if (!mission && record(season.bonus).claimed) return;
    if (!isEventActive()) return; // re-check expiration inside transaction.
    const eligible = mission ?
      progressFor(mission, counters, claimed) >= mission.target :
      MISSIONS.every((m) => Boolean(claimed[m.id]));
    if (!eligible) return;
    const reward = mission ? mission.reward : BONUS;
    // Existing ownership is never overwritten by an event or promo grant.
    const inv = record(record(record(user.metaEconomy).inventory)[
      reward.itemType === "map" ? "maps" : "boards"]);
    if (reward.type === "inventory_item" && reward.itemId &&
        inv[reward.itemId] === true) return;
    grantReward(user, reward);
    const history = record(season.history);
    history[transactionId] = {
      eventId: WORLD_EVENT_ID, eventName: WORLD_EVENT_TITLE,
      rewardId, rewardType: reward.type,
      rewardAmount: reward.amount, rewardItemId: reward.itemId ?? "",
      transactionId, claimedAt: now, status: "succeeded",
      source: "world_journey",
    };
    season.history = history;
    if (mission) claimed[rewardId] = {transactionId, claimedAt: now};
    else season.bonus = {claimed: true, transactionId, claimedAt: now};
    season.rewards = claimed;
    season.eventId = WORLD_EVENT_ID;
    season.eventName = WORLD_EVENT_TITLE;
    season.startsAtEpochMs = WORLD_EVENT_START;
    season.endsAtEpochMs = WORLD_EVENT_END_EXCLUSIVE - 1;
    season.updatedAt = now;
    eventState[WORLD_EVENT_ID] = season;
    user.seasonalEvents = eventState;
    return data;
  }, undefined, false);
  const state = record(outcome.snapshot.val());
  const season = record(record(state.seasonalEvents)[WORLD_EVENT_ID]);
  const history = record(season.history);
  if (!history[transactionId]) {
    throw new HttpsError("failed-precondition", "REWARD_LOCKED_OR_OWNED");
  }
  return {applied: outcome.committed, idempotentReplay: !outcome.committed,
    eventId: WORLD_EVENT_ID, referenceId: rewardId, transactionId,
    rewardType: mission ? mission.reward.type : BONUS.type,
    rewardAmount: mission ? mission.reward.amount : BONUS.amount};
}

/**
 * Validates or processes safeReward for the existing backend flow.
 * @param {*} data Validated input.
 * @return {*} Result of the existing operation.
 */
function safeReward(data: Record<string, unknown>): Reward {
  const type = data.type;
  const amount = safeInt(data.amount);
  if (!["gold", "xp", "atlas_coin", "inventory_item"].includes(String(type)) ||
      amount < 1 || amount > 5000) {
    throw new HttpsError("failed-precondition", "PROMO_INVALID_REWARD");
  }
  if (type === "inventory_item" &&
      (typeof data.itemId !== "string" || typeof data.itemType !== "string")) {
    throw new HttpsError("failed-precondition", "PROMO_INVALID_ITEM");
  }
  // The World map and Horizon board must remain World Journey mission-only.
  // No promo definition, even an Admin-created one, may grant these exclusives.
  if (type === "inventory_item" &&
      (data.itemId === WORLD_MAP_ID || data.itemId === WORLD_BOARD_ID)) {
    throw new HttpsError("permission-denied", "PROMO_EVENT_EXCLUSIVE_ITEM");
  }
  return {type: type as RewardType, amount,
    itemId: data.itemId as string | undefined,
    itemType: data.itemType as string | undefined};
}

/**
 * Server-only hashed definitions, with UID-bound error classification and
 * audit.
 * @param {*} uid Input value.
 * @param {*} rawCode Input value.
 */
export async function worldJourneyRedeem(uid: string, rawCode: string) {
  const code = (rawCode ?? "").trim().toUpperCase();
  const fail = (status:
    "invalid-argument" | "not-found" | "failed-precondition" |
    "permission-denied" | "already-exists" |
    "resource-exhausted" | "unavailable", message: string,
  errorKey: string): never => {
    throw new HttpsError(status, message, {errorKey});
  };
  if (!/^[A-Z0-9-]{8,48}$/.test(code)) {
    fail("invalid-argument", "PROMO_CODE_INVALID", "promo.error.invalid_code");
  }
  const promoId = sha(`atlasboard:promo:${code}`);
  const db = database();
  // Normalized plaintext codes are the RTDB child keys so an administrator can
  // add a new campaign manually under /promoCodes without a hash calculator.
  // Rules deny all normal client reads and writes; the callable owns lookups.
  const def = record((await db.ref(`promoCodes/${code}`).get()).val());
  const now = Date.now();
  if (Object.keys(def).length === 0 || def.code !== code ||
      def.schemaVersion !== 2) {
    fail("not-found", "PROMO_NOT_FOUND", "promo.error.not_found");
  }
  if (def.active !== true) {
    fail("failed-precondition", "PROMO_DISABLED", "promo.error.disabled");
  }
  if (now < safeInt(def.startsAtEpochMs)) {
    fail("failed-precondition", "PROMO_NOT_STARTED", "promo.error.not_started");
  }
  if (now > safeInt(def.endsAtEpochMs)) {
    fail("failed-precondition", "PROMO_EXPIRED", "promo.error.expired");
  }
  if (def.scope === "owner_qa") {
    const allowed = (await db.ref(`devWalletTesters/${uid}`).get()).val();
    if (allowed !== true) {
      fail("permission-denied", "PROMO_QA_ACCESS_DENIED",
        "promo.error.qa_denied");
    }
  } else if (def.scope !== "public") {
    fail("failed-precondition", "PROMO_SCOPE_INVALID",
      "promo.error.invalid_reward");
  }
  // Quantity records the number of units granted. Currency quantity and amount
  // must match. Cosmetics are non-stackable and may only be granted once.
  let reward: Reward;
  try {
    const raw = record(def.reward);
    reward = safeReward(raw);
    const quantity = safeInt(def.quantity);
    if (quantity !== reward.amount ||
        (reward.type === "inventory_item" && quantity !== 1)) {
      throw new Error("PROMO_QUANTITY_MISMATCH");
    }
  } catch {
    return fail("failed-precondition", "PROMO_INVALID_REWARD",
      "promo.error.invalid_reward");
  }
  const transactionId = sha(`${uid}:promo:${promoId}`);
  const userRef = db.ref(`users/${uid}`);
  // Check UID-specific receipt before transacting, then recheck in transaction
  // to close the race window. Never infer redemption from a client cache.
  const existing = (await userRef.child(
    `promoRedemptions/${promoId}`).get()).val();
  if (existing != null) {
    fail("already-exists", "PROMO_ALREADY_REDEEMED",
      "promo.error.already_redeemed");
  }
  const result = await userRef.transaction((data: unknown) => {
    if (!data || typeof data !== "object") return;
    const user = data as Record<string, unknown>;
    const promo = record(user.promoRedemptions);
    if (promo[promoId]) return;
    const inv = record(record(record(user.metaEconomy).inventory)[
      reward.itemType === "map" ? "maps" :
        reward.itemType === "board_theme" ? "boards" :
          reward.itemType === "profile_frame" ? "profile_frames" :
            reward.itemType === "dice_skin" ? "dice" :
              reward.itemType === "pawn" ? "pawns" : "emotes"]);
    if (reward.type === "inventory_item" && reward.itemId &&
        inv[reward.itemId] === true) return;
    grantReward(user, reward);
    promo[promoId] = {
      transactionId, promoId, code, claimedAt: now, source: "redeem_code",
      rewardType: reward.type, rewardAmount: reward.amount,
      rewardItemId: reward.itemId ?? "", status: "succeeded",
    };
    user.promoRedemptions = promo;
    return data;
  }, undefined, false);
  if (!result.committed) {
    const finalState = record(result.snapshot.val());
    if (record(finalState.promoRedemptions)[promoId]) {
      fail("already-exists", "PROMO_ALREADY_REDEEMED",
        "promo.error.already_redeemed");
    }
    if (Object.keys(finalState).length === 0) {
      fail("failed-precondition", "PROMO_ACCOUNT_NOT_READY",
        "promo.error.account_not_ready");
    }
    if (reward.type === "inventory_item" && reward.itemId) {
      const invKey = reward.itemType === "map" ? "maps" :
        reward.itemType === "board_theme" ? "boards" :
          reward.itemType === "profile_frame" ? "profile_frames" :
            reward.itemType === "dice_skin" ? "dice" :
              reward.itemType === "pawn" ? "pawns" : "emotes";
      const currentInventory = record(
        record(record(finalState.metaEconomy).inventory)[invKey]);
      if (currentInventory[reward.itemId] === true) {
        fail("already-exists", "PROMO_REWARD_ALREADY_OWNED",
          "promo.error.item_already_owned");
      }
    }
    fail("unavailable", "PROMO_CLAIM_NOT_CONFIRMED",
      "meta.error.service_unavailable");
  }
  // The transaction is committed before claiming success; the receipt is
  // immutable and reused by the existing Store history view.
  return {applied: true, idempotentReplay: false,
    promoId, transactionId, redemptionId: promoId,
    redemptionEventId: transactionId,
    rewards: [{type: reward.type,
      currencyId: reward.type === "gold" ||
        reward.type === "atlas_coin" ? reward.type : "",
      itemId: reward.itemId ?? "", amount: reward.amount}],
    rewardType: reward.type, rewardAmount: reward.amount,
    rewardItemId: reward.itemId ?? ""};
}
