import {createHash} from "crypto";
import {
  FieldValue,
  getFirestore,
  Timestamp,
} from "firebase-admin/firestore";
import {HttpsError} from "firebase-functions/v2/https";

const PROGRESSION_SCHEMA_VERSION = 1;
const HISTORY_RETENTION_MS = 3 * 24 * 60 * 60 * 1000;
const HISTORY_MAX_VISIBLE = 10;
const MAX_LEVEL = 100;

export interface ProgressionSlotTelemetryInput {
  slotIndex: number;
  diceRolls: number;
  doublesRolled: number;
  totalDiceValue: number;
  highestRoll: number;
}

export interface RecordCompletedMatchInput {
  uid: string;
  matchId: string;
  completedTurns: number;
  telemetry: ProgressionSlotTelemetryInput[];
}

export interface GetProgressionProfileInput {
  uid: string;
}

interface CanonicalMatchResult {
  valid: boolean;
  highestNetWorth: number;
  winnerSlots: number[];
  participating: boolean[];
  bankrupt: boolean[];
  cash: number[];
  propertyCount: number[];
  propertyValue: number[];
  developmentLevels: number[];
  developmentValue: number[];
  netWorth: number[];
}

interface CanonicalHostFrame {
  schemaVersion: number;
  phase: string;
  currentRound: number;
  roundLimit: number;
  diceSequence: number;
  matchResult: CanonicalMatchResult | null;
}

interface EffectiveSeat {
  slotIndex: number;
  accountId: string;
  displayName: string;
  controllerKind: string;
  seatType: string;
  isHost: boolean;
}

interface CareerStats {
  matchesPlayed: number;
  wins: number;
  losses: number;
  ties: number;
  bankruptcies: number;
  diceRolls: number;
  doublesRolled: number;
  totalDiceValue: number;
  highestRoll: number;
  propertiesAtFinishTotal: number;
  developmentLevelsAtFinishTotal: number;
  totalRounds: number;
  totalTurns: number;
  totalNetWorth: number;
  highestNetWorth: number;
  highestCash: number;
  currentWinStreak: number;
  bestWinStreak: number;
  mapTurkey: number;
  mapColorado: number;
  mapUsa: number;
  mapOther: number;
  mapWinsTurkey: number;
  mapWinsColorado: number;
  mapWinsUsa: number;
  mapWinsOther: number;
}

interface MapCareerStatsEntry {
  mapId: string;
  stats: CareerStats;
}

interface AchievementDefinition {
  id: string;
  titleKey: string;
  descriptionKey: string;
  target: number;
  progress: (stats: CareerStats) => number;
}

const ACHIEVEMENTS: AchievementDefinition[] = [
  {
    id: "first_match",
    titleKey: "progression.achievement.first_match.title",
    descriptionKey: "progression.achievement.first_match.body",
    target: 1,
    progress: (stats) => stats.matchesPlayed,
  },
  {
    id: "first_win",
    titleKey: "progression.achievement.first_win.title",
    descriptionKey: "progression.achievement.first_win.body",
    target: 1,
    progress: (stats) => stats.wins,
  },
  {
    id: "win_streak_3",
    titleKey: "progression.achievement.streak.title",
    descriptionKey: "progression.achievement.streak.body",
    target: 3,
    progress: (stats) => stats.bestWinStreak,
  },
  {
    id: "matches_10",
    titleKey: "progression.achievement.matches_10.title",
    descriptionKey: "progression.achievement.matches_10.body",
    target: 10,
    progress: (stats) => stats.matchesPlayed,
  },
  {
    id: "wins_10",
    titleKey: "progression.achievement.wins_10.title",
    descriptionKey: "progression.achievement.wins_10.body",
    target: 10,
    progress: (stats) => stats.wins,
  },
  {
    id: "dice_100",
    titleKey: "progression.achievement.dice_100.title",
    descriptionKey: "progression.achievement.dice_100.body",
    target: 100,
    progress: (stats) => stats.diceRolls,
  },
  {
    id: "doubles_10",
    titleKey: "progression.achievement.doubles_10.title",
    descriptionKey: "progression.achievement.doubles_10.body",
    target: 10,
    progress: (stats) => stats.doublesRolled,
  },
  {
    id: "properties_25",
    titleKey: "progression.achievement.properties_25.title",
    descriptionKey: "progression.achievement.properties_25.body",
    target: 25,
    progress: (stats) => stats.propertiesAtFinishTotal,
  },
  {
    id: "builder_20",
    titleKey: "progression.achievement.builder_20.title",
    descriptionKey: "progression.achievement.builder_20.body",
    target: 20,
    progress: (stats) => stats.developmentLevelsAtFinishTotal,
  },
  {
    id: "networth_5000",
    titleKey: "progression.achievement.networth.title",
    descriptionKey: "progression.achievement.networth.body",
    target: 5000,
    progress: (stats) => stats.highestNetWorth,
  },
  {
    id: "turkey_explorer",
    titleKey: "progression.achievement.turkey.title",
    descriptionKey: "progression.achievement.turkey.body",
    target: 1,
    progress: (stats) => stats.mapTurkey,
  },
  {
    id: "colorado_explorer",
    titleKey: "progression.achievement.colorado.title",
    descriptionKey: "progression.achievement.colorado.body",
    target: 1,
    progress: (stats) => stats.mapColorado,
  },
  {
    id: "usa_explorer",
    titleKey: "progression.achievement.usa.title",
    descriptionKey: "progression.achievement.usa.body",
    target: 1,
    progress: (stats) => stats.mapUsa,
  },
  {
    id: "atlas_traveler",
    titleKey: "progression.achievement.traveler.title",
    descriptionKey: "progression.achievement.traveler.body",
    target: 3,
    progress: (stats) =>
      Math.min(1, stats.mapTurkey) +
      Math.min(1, stats.mapColorado) +
      Math.min(1, stats.mapUsa),
  },
];

/**
 * Returns a stable callable error for progression input failures.
 * @param {string} fieldName Invalid field name.
 * @return {HttpsError} Stable invalid-argument error.
 */
function invalidRequest(fieldName: string): HttpsError {
  return new HttpsError(
    "invalid-argument",
    "INVALID_PROGRESSION_REQUEST",
    {
      errorKey: "progression.error.invalid_request",
      fieldName,
    },
  );
}

/**
 * Clamps an unknown value into a safe non-negative integer.
 * @param {unknown} value Candidate numeric value.
 * @param {number} maximum Maximum accepted integer.
 * @return {number} Sanitized integer.
 */
function nonNegativeInt(value: unknown, maximum: number): number {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    return 0;
  }

  return Math.max(0, Math.min(maximum, Math.trunc(value)));
}

/**
 * Reads one stable string value with a bounded length.
 * @param {unknown} value Candidate value.
 * @param {number} maxLength Maximum string length.
 * @return {string} Sanitized string.
 */
function safeString(value: unknown, maxLength = 80): string {
  return typeof value === "string" ? value.trim().slice(0, maxLength) : "";
}

/**
 * Reads an indexed integer value from a result array.
 * @param {unknown} values Candidate array.
 * @param {number} index Stable player slot.
 * @return {number} Safe integer value.
 */
function indexedInt(values: unknown, index: number): number {
  if (!Array.isArray(values) || index < 0 || index >= values.length) {
    return 0;
  }

  const value = values[index];
  return typeof value === "number" && Number.isFinite(value) ?
    Math.trunc(value) :
    0;
}

/**
 * Reads an indexed boolean value from a result array.
 * @param {unknown} values Candidate array.
 * @param {number} index Stable player slot.
 * @return {boolean} Boolean value.
 */
function indexedBool(values: unknown, index: number): boolean {
  return Array.isArray(values) && values[index] === true;
}

/**
 * Normalizes a map id into aggregate-stat buckets.
 * @param {unknown} value Raw map id.
 * @return {"turkey"|"colorado"|"usa"|"other"} Map bucket.
 */
function mapBucket(
  value: unknown,
): "turkey" | "colorado" | "usa" | "other" {
  const normalized = safeString(value, 64).toLowerCase();
  if (normalized.includes("turkey") || normalized.includes("türkiye")) {
    return "turkey";
  }
  if (normalized.includes("colorado")) {
    return "colorado";
  }
  if (normalized === "usa" || normalized.includes("united_states")) {
    return "usa";
  }
  return "other";
}

const LEVEL_BASE_XP = 500;
const LEVEL_GROWTH_RATE = 1.10;

/**
 * Returns XP needed to advance from one level to the next.
 * Requirement grows by 10% per level to keep progression long-lived
 * without turning higher levels into an extreme grind wall.
 * @param {number} level Current player level.
 * @return {number} XP required for the next level transition.
 */
function levelRequirementXp(level: number): number {
  const normalized =
    Math.max(
      1,
      Math.min(MAX_LEVEL - 1, Math.trunc(level)),
    );

  return Math.round(
    LEVEL_BASE_XP *
    Math.pow(LEVEL_GROWTH_RATE, normalized - 1),
  );
}

/**
 * Converts total account XP into a bounded player level.
 * @param {number} totalXp Persistent account XP.
 * @return {number} Level in the inclusive range 1..100.
 */
export function levelFromXp(totalXp: number): number {
  const safeXp = Math.max(0, Math.trunc(totalXp));
  let level = 1;
  let cumulativeThreshold = 0;

  while (level < MAX_LEVEL) {
    cumulativeThreshold += levelRequirementXp(level);
    if (safeXp < cumulativeThreshold) {
      break;
    }
    level++;
  }

  return level;
}

/**
 * Returns cumulative XP required to enter a level.
 * @param {number} level Player level.
 * @return {number} Cumulative XP floor for the level.
 */
export function levelStartXp(level: number): number {
  const normalized =
    Math.max(
      1,
      Math.min(MAX_LEVEL, Math.trunc(level)),
    );

  let cumulative = 0;
  for (let current = 1; current < normalized; current++) {
    cumulative += levelRequirementXp(current);
  }

  return cumulative;
}

/**
 * Returns cumulative XP required for the next level.
 * @param {number} level Current player level.
 * @return {number} Next-level cumulative XP threshold.
 */
export function nextLevelXp(level: number): number {
  const normalized =
    Math.max(
      1,
      Math.min(MAX_LEVEL, Math.trunc(level)),
    );

  if (normalized >= MAX_LEVEL) {
    return levelStartXp(MAX_LEVEL);
  }

  return levelStartXp(normalized) +
    levelRequirementXp(normalized);
}

/**
 * Builds default cumulative career statistics.
 * @param {FirebaseFirestore.DocumentData} data Existing progression data.
 * @return {CareerStats} Fully-populated career stats.
 */
function readCareerStats(
  data: FirebaseFirestore.DocumentData = {},
): CareerStats {
  const source =
    data.stats && typeof data.stats === "object" ? data.stats : {};

  return {
    matchesPlayed: nonNegativeInt(source.matchesPlayed, 1_000_000),
    wins: nonNegativeInt(source.wins, 1_000_000),
    losses: nonNegativeInt(source.losses, 1_000_000),
    ties: nonNegativeInt(source.ties, 1_000_000),
    bankruptcies: nonNegativeInt(source.bankruptcies, 1_000_000),
    diceRolls: nonNegativeInt(source.diceRolls, 100_000_000),
    doublesRolled: nonNegativeInt(source.doublesRolled, 100_000_000),
    totalDiceValue: nonNegativeInt(source.totalDiceValue, 1_000_000_000),
    highestRoll: nonNegativeInt(source.highestRoll, 12),
    propertiesAtFinishTotal:
      nonNegativeInt(source.propertiesAtFinishTotal, 100_000_000),
    developmentLevelsAtFinishTotal:
      nonNegativeInt(source.developmentLevelsAtFinishTotal, 100_000_000),
    totalRounds: nonNegativeInt(source.totalRounds, 100_000_000),
    totalTurns: nonNegativeInt(source.totalTurns, 100_000_000),
    totalNetWorth:
      nonNegativeInt(source.totalNetWorth, Number.MAX_SAFE_INTEGER),
    highestNetWorth:
      nonNegativeInt(source.highestNetWorth, Number.MAX_SAFE_INTEGER),
    highestCash: nonNegativeInt(source.highestCash, Number.MAX_SAFE_INTEGER),
    currentWinStreak: nonNegativeInt(source.currentWinStreak, 1_000_000),
    bestWinStreak: nonNegativeInt(source.bestWinStreak, 1_000_000),
    mapTurkey: nonNegativeInt(source.mapTurkey, 1_000_000),
    mapColorado: nonNegativeInt(source.mapColorado, 1_000_000),
    mapUsa: nonNegativeInt(source.mapUsa, 1_000_000),
    mapOther: nonNegativeInt(source.mapOther, 1_000_000),
    mapWinsTurkey: nonNegativeInt(source.mapWinsTurkey, 1_000_000),
    mapWinsColorado: nonNegativeInt(source.mapWinsColorado, 1_000_000),
    mapWinsUsa: nonNegativeInt(source.mapWinsUsa, 1_000_000),
    mapWinsOther: nonNegativeInt(source.mapWinsOther, 1_000_000),
  };
}

/**
 * Returns a stable case-insensitive key for one map id.
 * @param {unknown} value Raw map id.
 * @return {string} Normalized map key.
 */
function mapStatsKey(value: unknown): string {
  return safeString(value, 64)
    .toLowerCase()
    .replace(/\s+/g, "_");
}

/**
 * Reads persistent per-map career-stat buckets.
 * New maps remain data-driven and appear after their first completed match.
 * @param {FirebaseFirestore.DocumentData} data Existing progression data.
 * @return {MapCareerStatsEntry[]} Sanitized per-map statistics.
 */
function readMapCareerStats(
  data: FirebaseFirestore.DocumentData = {},
): MapCareerStatsEntry[] {
  const raw = Array.isArray(data.mapStats) ? data.mapStats : [];
  const byKey = new Map<string, MapCareerStatsEntry>();

  for (const item of raw) {
    if (!item || typeof item !== "object") {
      continue;
    }

    const row = item as Record<string, unknown>;
    const mapId = safeString(row.mapId, 64);
    const key = mapStatsKey(mapId);
    if (!mapId || !key || byKey.has(key)) {
      continue;
    }

    byKey.set(key, {
      mapId,
      stats: readCareerStats({stats: row.stats}),
    });
  }

  return Array.from(byKey.values())
    .sort((left, right) =>
      left.mapId.localeCompare(right.mapId));
}

/**
 * Applies one completed match to its persistent map-specific stat bucket.
 * @param {MapCareerStatsEntry[]} entries Existing per-map entries.
 * @param {string} mapId Completed match map id.
 * @param {string} kind Result classification.
 * @param {boolean} bankrupt Whether the player finished bankrupt.
 * @param {ProgressionSlotTelemetryInput} telemetry Safe dice telemetry.
 * @param {number} propertyCount Final property count.
 * @param {number} developmentLevels Final development level count.
 * @param {number} rounds Final round number.
 * @param {number} turns Match-wide completed turn count.
 * @param {number} netWorth Final net worth.
 * @param {number} cash Final cash.
 * @return {MapCareerStatsEntry[]} Updated per-map entries.
 */
function applyMatchToMapStats(
  entries: MapCareerStatsEntry[],
  mapId: string,
  kind: "win" | "loss" | "tie",
  bankrupt: boolean,
  telemetry: ProgressionSlotTelemetryInput,
  propertyCount: number,
  developmentLevels: number,
  rounds: number,
  turns: number,
  netWorth: number,
  cash: number,
): MapCareerStatsEntry[] {
  const safeMapId = safeString(mapId, 64) || "Unknown";
  const key = mapStatsKey(safeMapId);
  const next = entries.map((entry) => ({
    mapId: entry.mapId,
    stats: {...entry.stats},
  }));
  let target = next.find((entry) =>
    mapStatsKey(entry.mapId) === key);

  if (!target) {
    target = {
      mapId: safeMapId,
      stats: readCareerStats(),
    };
    next.push(target);
  }

  target.stats = applyMatchToStats(
    target.stats,
    kind,
    bankrupt,
    telemetry,
    propertyCount,
    developmentLevels,
    rounds,
    turns,
    netWorth,
    cash,
    safeMapId,
  );

  return next.sort((left, right) =>
    left.mapId.localeCompare(right.mapId));
}

/**
 * Parses the host-published canonical match result frame.
 * @param {unknown} snapshotJson Stored network snapshot JSON.
 * @return {CanonicalHostFrame} Parsed and validated host frame.
 */
function parseCanonicalHostFrame(snapshotJson: unknown): CanonicalHostFrame {
  if (typeof snapshotJson !== "string" || snapshotJson.length < 2) {
    throw new HttpsError(
      "failed-precondition",
      "MATCH_RESULT_NOT_READY",
      {errorKey: "progression.error.match_not_final"},
    );
  }

  let parsed: Record<string, unknown>;
  try {
    parsed = JSON.parse(snapshotJson) as Record<string, unknown>;
  } catch {
    throw new HttpsError(
      "failed-precondition",
      "MATCH_RESULT_INVALID",
      {errorKey: "progression.error.match_not_final"},
    );
  }

  const rawResult = parsed.matchResult;
  if (
    parsed.phase !== "match_complete" ||
    !rawResult ||
    typeof rawResult !== "object" ||
    (rawResult as Record<string, unknown>).valid !== true
  ) {
    throw new HttpsError(
      "failed-precondition",
      "MATCH_RESULT_NOT_READY",
      {errorKey: "progression.error.match_not_final"},
    );
  }

  const result = rawResult as Record<string, unknown>;
  return {
    schemaVersion: nonNegativeInt(parsed.schemaVersion, 1000),
    phase: "match_complete",
    currentRound: Math.max(1, nonNegativeInt(parsed.currentRound, 10_000)),
    roundLimit: Math.max(1, nonNegativeInt(parsed.roundLimit, 10_000)),
    diceSequence: nonNegativeInt(parsed.diceSequence, 10_000_000),
    matchResult: {
      valid: true,
      highestNetWorth:
        nonNegativeInt(
          result.highestNetWorth,
          Number.MAX_SAFE_INTEGER,
        ),
      winnerSlots: Array.isArray(result.winnerSlots) ?
        result.winnerSlots
          .filter((item) => Number.isInteger(item))
          .map((item) => Math.trunc(item as number)) :
        [],
      participating: Array.isArray(result.participating) ?
        result.participating.map((item) => item === true) :
        [],
      bankrupt: Array.isArray(result.bankrupt) ?
        result.bankrupt.map((item) => item === true) :
        [],
      cash: Array.isArray(result.cash) ?
        result.cash.map(
          (item) => nonNegativeInt(item, Number.MAX_SAFE_INTEGER),
        ) :
        [],
      propertyCount: Array.isArray(result.propertyCount) ?
        result.propertyCount.map((item) => nonNegativeInt(item, 10_000)) :
        [],
      propertyValue: Array.isArray(result.propertyValue) ?
        result.propertyValue.map(
          (item) => nonNegativeInt(item, Number.MAX_SAFE_INTEGER),
        ) :
        [],
      developmentLevels: Array.isArray(result.developmentLevels) ?
        result.developmentLevels.map(
          (item) => nonNegativeInt(item, 100_000),
        ) :
        [],
      developmentValue: Array.isArray(result.developmentValue) ?
        result.developmentValue.map(
          (item) => nonNegativeInt(item, Number.MAX_SAFE_INTEGER),
        ) :
        [],
      netWorth: Array.isArray(result.netWorth) ?
        result.netWorth.map(
          (item) => nonNegativeInt(item, Number.MAX_SAFE_INTEGER),
        ) :
        [],
    },
  };
}

/**
 * Sanitizes host-observed per-slot dice telemetry against canonical totals.
 * Detailed counters are useful for player-facing stats, but are not intended
 * to become ranked-authoritative until they are event-sourced server-side.
 * @param {ProgressionSlotTelemetryInput[]} telemetry Raw slot telemetry.
 * @param {number} canonicalDiceSequence Canonical total dice sequence.
 * @return {Map<number, ProgressionSlotTelemetryInput>} Safe slot telemetry.
 */
function sanitizeTelemetry(
  telemetry: ProgressionSlotTelemetryInput[],
  canonicalDiceSequence: number,
): Map<number, ProgressionSlotTelemetryInput> {
  const output = new Map<number, ProgressionSlotTelemetryInput>();
  let totalAcceptedRolls = 0;

  for (const item of Array.isArray(telemetry) ? telemetry : []) {
    if (!item || !Number.isInteger(item.slotIndex)) {
      continue;
    }

    const slotIndex = Math.trunc(item.slotIndex);
    if (slotIndex < 0 || slotIndex > 3 || output.has(slotIndex)) {
      continue;
    }

    const diceRolls = nonNegativeInt(item.diceRolls, canonicalDiceSequence);
    if (totalAcceptedRolls + diceRolls > canonicalDiceSequence) {
      continue;
    }

    const doublesRolled = Math.min(
      diceRolls,
      nonNegativeInt(item.doublesRolled, diceRolls),
    );
    const totalDiceValue = Math.min(
      diceRolls * 12,
      nonNegativeInt(item.totalDiceValue, diceRolls * 12),
    );
    const highestRoll = Math.min(
      12,
      nonNegativeInt(item.highestRoll, 12),
    );

    output.set(slotIndex, {
      slotIndex,
      diceRolls,
      doublesRolled,
      totalDiceValue,
      highestRoll,
    });
    totalAcceptedRolls += diceRolls;
  }

  return output;
}

/**
 * Calculates a stable place number from final net worth.
 * @param {CanonicalMatchResult} result Canonical result snapshot.
 * @param {number} slotIndex Stable player slot.
 * @return {number} One-based final place.
 */
function finalPlace(result: CanonicalMatchResult, slotIndex: number): number {
  const target = indexedInt(result.netWorth, slotIndex);
  let ahead = 0;

  for (let slot = 0; slot < result.participating.length; slot++) {
    if (!indexedBool(result.participating, slot) || slot === slotIndex) {
      continue;
    }
    if (indexedInt(result.netWorth, slot) > target) {
      ahead++;
    }
  }

  return ahead + 1;
}

/**
 * Returns win/loss/tie for a stable slot.
 * @param {CanonicalMatchResult} result Canonical result snapshot.
 * @param {number} slotIndex Stable player slot.
 * @return {"win"|"loss"|"tie"} Result classification.
 */
function resultKind(
  result: CanonicalMatchResult,
  slotIndex: number,
): "win" | "loss" | "tie" {
  const won = result.winnerSlots.includes(slotIndex);
  if (!won) {
    return "loss";
  }
  return result.winnerSlots.length > 1 ? "tie" : "win";
}

/**
 * Calculates bounded account XP from an authoritative match result.
 * @param {string} kind Result classification.
 * @param {number} propertyCount Final property count.
 * @param {number} developmentLevels Final development levels.
 * @param {number} completedRounds Completed authoritative match rounds.
 * @return {number} XP award.
 */
function calculateXpAward(
  kind: "win" | "loss" | "tie",
  propertyCount: number,
  developmentLevels: number,
  completedRounds: number,
): number {
  const completion = 25;
  const resultBonus =
    kind === "win" ? 50 :
      kind === "tie" ? 20 :
        0;
  const roundBonus =
    Math.min(100, Math.max(0, completedRounds) * 3);
  const propertyBonus =
    Math.min(50, Math.max(0, propertyCount) * 5);
  const developmentBonus =
    Math.min(75, Math.max(0, developmentLevels) * 3);

  return completion +
    resultBonus +
    roundBonus +
    propertyBonus +
    developmentBonus;
}

/**
 * Applies one match outcome to cumulative career stats.
 * @param {CareerStats} stats Existing career statistics.
 * @param {string} kind Result classification.
 * @param {boolean} bankrupt Whether the player finished bankrupt.
 * @param {ProgressionSlotTelemetryInput} telemetry Safe dice telemetry.
 * @param {number} propertyCount Final property count.
 * @param {number} developmentLevels Final development level count.
 * @param {number} rounds Final round number.
 * @param {number} turns Match-wide completed turn count.
 * @param {number} netWorth Final net worth.
 * @param {number} cash Final cash.
 * @param {string} mapId Match map id.
 * @return {CareerStats} Updated cumulative career stats.
 */
function applyMatchToStats(
  stats: CareerStats,
  kind: "win" | "loss" | "tie",
  bankrupt: boolean,
  telemetry: ProgressionSlotTelemetryInput,
  propertyCount: number,
  developmentLevels: number,
  rounds: number,
  turns: number,
  netWorth: number,
  cash: number,
  mapId: string,
): CareerStats {
  const next = {...stats};
  next.matchesPlayed++;
  if (kind === "win") next.wins++;
  if (kind === "loss") next.losses++;
  if (kind === "tie") next.ties++;
  if (bankrupt) next.bankruptcies++;

  next.diceRolls += telemetry.diceRolls;
  next.doublesRolled += telemetry.doublesRolled;
  next.totalDiceValue += telemetry.totalDiceValue;
  next.highestRoll = Math.max(next.highestRoll, telemetry.highestRoll);
  next.propertiesAtFinishTotal += propertyCount;
  next.developmentLevelsAtFinishTotal += developmentLevels;
  next.totalRounds += rounds;
  next.totalTurns += turns;
  next.totalNetWorth += netWorth;
  next.highestNetWorth = Math.max(next.highestNetWorth, netWorth);
  next.highestCash = Math.max(next.highestCash, cash);

  if (kind === "win") {
    next.currentWinStreak++;
    next.bestWinStreak = Math.max(next.bestWinStreak, next.currentWinStreak);
  } else {
    next.currentWinStreak = 0;
  }

  const bucket = mapBucket(mapId);
  if (bucket === "turkey") {
    next.mapTurkey++;
    if (kind === "win") next.mapWinsTurkey++;
  } else if (bucket === "colorado") {
    next.mapColorado++;
    if (kind === "win") next.mapWinsColorado++;
  } else if (bucket === "usa") {
    next.mapUsa++;
    if (kind === "win") next.mapWinsUsa++;
  } else {
    next.mapOther++;
    if (kind === "win") next.mapWinsOther++;
  }

  return next;
}

/**
 * Returns newly unlocked achievement ids after a stat update.
 * @param {CareerStats} stats Updated stats.
 * @param {string[]} existing Existing unlocked achievement ids.
 * @return {string[]} Newly unlocked achievement ids.
 */
function newlyUnlockedAchievements(
  stats: CareerStats,
  existing: string[],
): string[] {
  const unlocked = new Set(existing);
  return ACHIEVEMENTS
    .filter((definition) =>
      !unlocked.has(definition.id) &&
      definition.progress(stats) >= definition.target)
    .map((definition) => definition.id);
}

/**
 * Builds a stable effective account list from canonical match seats.
 * Multiple local seats may share one account; only the lowest stable slot for
 * that account contributes account progression once.
 * @param {FirebaseFirestore.QuerySnapshot} seatsSnapshot Match seat snapshot.
 * @return {EffectiveSeat[]} Human account seats deduplicated by account id.
 */
function buildEffectiveSeats(
  seatsSnapshot: FirebaseFirestore.QuerySnapshot,
): EffectiveSeat[] {
  const byAccount = new Map<string, EffectiveSeat>();

  for (const doc of seatsSnapshot.docs) {
    const data = doc.data() ?? {};
    const seatType = safeString(data.seatType, 24);
    const controllerKind = safeString(data.controllerKind, 32);
    const accountId =
      safeString(data.accountId, 128) ||
      safeString(data.localOwnerAccountId, 128);
    const slotIndex = nonNegativeInt(data.slotIndex, 3);

    if (!accountId || seatType === "bot" || controllerKind === "bot") {
      continue;
    }

    const candidate: EffectiveSeat = {
      slotIndex,
      accountId,
      displayName: safeString(data.displayName, 40) || "Player",
      controllerKind,
      seatType,
      isHost: data.isHost === true,
    };

    const existing = byAccount.get(accountId);
    if (!existing || candidate.slotIndex < existing.slotIndex) {
      byAccount.set(accountId, candidate);
    }
  }

  return Array.from(byAccount.values())
    .sort((left, right) => left.slotIndex - right.slotIndex);
}

/**
 * Produces sanitized participant rows for user-facing match details.
 * @param {FirebaseFirestore.QuerySnapshot} seatsSnapshot Canonical seats.
 * @param {CanonicalMatchResult} result Canonical result snapshot.
 * @return {Array<Record<string, unknown>>} Participant detail rows.
 */
function buildParticipantRows(
  seatsSnapshot: FirebaseFirestore.QuerySnapshot,
  result: CanonicalMatchResult,
): Array<Record<string, unknown>> {
  const rows: Array<Record<string, unknown>> = [];

  for (const doc of seatsSnapshot.docs) {
    const data = doc.data() ?? {};
    const slotIndex = nonNegativeInt(data.slotIndex, 3);
    if (!indexedBool(result.participating, slotIndex)) {
      continue;
    }

    rows.push({
      slotIndex,
      displayName:
        safeString(data.displayName, 40) ||
        `Player ${slotIndex + 1}`,
      controllerKind: safeString(data.controllerKind, 32),
      isHost: data.isHost === true,
      bankrupt: indexedBool(result.bankrupt, slotIndex),
      place: finalPlace(result, slotIndex),
      result: resultKind(result, slotIndex),
      cash: indexedInt(result.cash, slotIndex),
      propertyCount: indexedInt(result.propertyCount, slotIndex),
      propertyValue: indexedInt(result.propertyValue, slotIndex),
      developmentLevels: indexedInt(result.developmentLevels, slotIndex),
      developmentValue: indexedInt(result.developmentValue, slotIndex),
      netWorth: indexedInt(result.netWorth, slotIndex),
    });
  }

  return rows.sort((left, right) =>
    Number(left.slotIndex) - Number(right.slotIndex));
}

/**
 * Produces one deterministic event id for immutable progression evidence.
 * @param {string} matchId Canonical match id.
 * @param {string} uid Account id.
 * @param {string} suffix Evidence suffix.
 * @return {string} SHA-256 document id.
 */
function evidenceId(matchId: string, uid: string, suffix: string): string {
  return createHash("sha256")
    .update(`${matchId}:${uid}:${suffix}`, "utf8")
    .digest("hex");
}

/**
 * Records a canonical completed online match for every human account in it.
 * The caller must be the match host. Outcome/economy/development fields come
 * from the final host-published canonical network frame. Dice counters are
 * bounded host telemetry and are intentionally excluded from future ranked
 * authority until they are event-sourced on the backend.
 * @param {RecordCompletedMatchInput} input Authenticated finalize request.
 * @return {Promise<Record<string, unknown>>} Finalization result.
 */
export async function recordCompletedMatch(
  input: RecordCompletedMatchInput,
): Promise<Record<string, unknown>> {
  const db = getFirestore();
  const matchId = safeString(input.matchId, 128);
  if (!matchId) {
    throw invalidRequest("matchId");
  }

  const matchRef = db.collection("matches").doc(matchId);
  const networkRef = matchRef.collection("network").doc("state");
  const [matchSnapshot, networkSnapshot, seatsSnapshot] = await Promise.all([
    matchRef.get(),
    networkRef.get(),
    matchRef.collection("seats").get(),
  ]);

  if (!matchSnapshot.exists || !networkSnapshot.exists) {
    throw new HttpsError(
      "not-found",
      "MATCH_NOT_FOUND",
      {errorKey: "progression.error.match_not_found"},
    );
  }

  const match = matchSnapshot.data() ?? {};
  if (safeString(match.hostAccountId, 128) !== input.uid) {
    throw new HttpsError(
      "permission-denied",
      "MATCH_HOST_REQUIRED",
      {errorKey: "progression.error.host_required"},
    );
  }

  const network = networkSnapshot.data() ?? {};
  const frame = parseCanonicalHostFrame(network.snapshotJson);
  if (safeString(network.phase, 32) !== "match_complete") {
    throw new HttpsError(
      "failed-precondition",
      "MATCH_RESULT_NOT_READY",
      {errorKey: "progression.error.match_not_final"},
    );
  }

  const result = frame.matchResult;
  if (!result) {
    throw new HttpsError(
      "failed-precondition",
      "MATCH_RESULT_NOT_READY",
      {errorKey: "progression.error.match_not_final"},
    );
  }

  const effectiveSeats = buildEffectiveSeats(seatsSnapshot);
  if (effectiveSeats.length === 0) {
    throw new HttpsError(
      "failed-precondition",
      "NO_ACCOUNT_SEATS",
      {errorKey: "progression.error.no_account_seats"},
    );
  }

  const telemetry = sanitizeTelemetry(
    input.telemetry ?? [],
    frame.diceSequence,
  );
  const completedTurns = nonNegativeInt(input.completedTurns, 1_000_000);
  const participantRows = buildParticipantRows(seatsSnapshot, result);
  const mapId = safeString(match.mapId, 64);
  const themeId = safeString(match.themeId, 64);
  const lobbyId = safeString(match.lobbyId, 128);
  const roundLimit = Math.max(1, nonNegativeInt(match.roundLimit, 10_000));
  const maxPlayers = Math.max(2, nonNegativeInt(match.maxPlayers, 4));
  const mode = safeString(match.mode, 32) || "online";
  const markerRef = db.collection("progression_match_events").doc(matchId);

  const transactionResult = await db.runTransaction(async (transaction) => {
    const markerSnapshot = await transaction.get(markerRef);
    if (markerSnapshot.exists) {
      return {
        applied: false,
        idempotentReplay: true,
        affectedAccountIds:
          Array.isArray(markerSnapshot.data()?.affectedAccountIds) ?
            markerSnapshot.data()?.affectedAccountIds :
            [],
      };
    }

    const progressionRefs = effectiveSeats.map((seat) =>
      db.collection("account_progression").doc(seat.accountId));
    const progressionSnapshots = [];
    for (const ref of progressionRefs) {
      progressionSnapshots.push(await transaction.get(ref));
    }

    const serverTimestamp = FieldValue.serverTimestamp();
    const affectedAccountIds: string[] = [];
    const accountResultEvidence: Array<Record<string, unknown>> = [];

    for (let index = 0; index < effectiveSeats.length; index++) {
      const seat = effectiveSeats[index];
      if (!indexedBool(result.participating, seat.slotIndex)) {
        continue;
      }

      const progressionRef = progressionRefs[index];
      const existingData = progressionSnapshots[index].data() ?? {};
      const statsBefore = readCareerStats(existingData);
      const mapStatsBefore = readMapCareerStats(existingData);
      const kind = resultKind(result, seat.slotIndex);
      const slotTelemetry = telemetry.get(seat.slotIndex) ?? {
        slotIndex: seat.slotIndex,
        diceRolls: 0,
        doublesRolled: 0,
        totalDiceValue: 0,
        highestRoll: 0,
      };
      const propertyCount = indexedInt(result.propertyCount, seat.slotIndex);
      const developmentLevels =
        indexedInt(result.developmentLevels, seat.slotIndex);
      const netWorth = indexedInt(result.netWorth, seat.slotIndex);
      const cash = indexedInt(result.cash, seat.slotIndex);
      const bankrupt = indexedBool(result.bankrupt, seat.slotIndex);
      const statsAfter = applyMatchToStats(
        statsBefore,
        kind,
        bankrupt,
        slotTelemetry,
        propertyCount,
        developmentLevels,
        frame.currentRound,
        completedTurns,
        netWorth,
        cash,
        mapId,
      );

      const mapStatsAfter = applyMatchToMapStats(
        mapStatsBefore,
        mapId,
        kind,
        bankrupt,
        slotTelemetry,
        propertyCount,
        developmentLevels,
        frame.currentRound,
        completedTurns,
        netWorth,
        cash,
      );

      const oldXp =
        nonNegativeInt(
          existingData.totalXp,
          Number.MAX_SAFE_INTEGER,
        );
      const completedRoundsForXp =
        Math.min(
          roundLimit,
          Math.max(1, frame.currentRound),
        );
      const xpAwarded =
        calculateXpAward(
          kind,
          propertyCount,
          developmentLevels,
          completedRoundsForXp,
        );
      const totalXp =
        Math.min(
          Number.MAX_SAFE_INTEGER,
          oldXp + xpAwarded,
        );
      const level = levelFromXp(totalXp);
      const existingUnlocked =
        Array.isArray(existingData.unlockedAchievements) ?
          existingData.unlockedAchievements
            .filter((item: unknown) => typeof item === "string") :
          [];
      const newlyUnlocked = newlyUnlockedAchievements(
        statsAfter,
        existingUnlocked,
      );
      const unlockedAchievements = Array.from(
        new Set([...existingUnlocked, ...newlyUnlocked]),
      ).sort();

      transaction.set(
        progressionRef,
        {
          uid: seat.accountId,
          totalXp,
          level,
          stats: statsAfter,
          mapStats: mapStatsAfter,
          unlockedAchievements,
          lastMatchId: matchId,
          lastMatchResult: kind,
          lastMatchXpAwarded: xpAwarded,
          schemaVersion: PROGRESSION_SCHEMA_VERSION,
          updatedAt: serverTimestamp,
        },
        {merge: false},
      );

      const historyRef = db
        .collection("account_match_history")
        .doc(seat.accountId)
        .collection("matches")
        .doc(matchId);

      transaction.set(historyRef, {
        uid: seat.accountId,
        matchId,
        lobbyId,
        hostAccountId: safeString(match.hostAccountId, 128),
        mode,
        mapId,
        themeId,
        roundLimit,
        maxPlayers,
        result: kind,
        place: finalPlace(result, seat.slotIndex),
        playerCount: participantRows.length,
        localSlotIndex: seat.slotIndex,
        bankrupt,
        finalCash: cash,
        propertyCount,
        propertyValue: indexedInt(result.propertyValue, seat.slotIndex),
        developmentLevels,
        developmentValue:
          indexedInt(result.developmentValue, seat.slotIndex),
        netWorth,
        highestMatchNetWorth: result.highestNetWorth,
        completedRounds: frame.currentRound,
        completedTurns,
        diceRolls: slotTelemetry.diceRolls,
        doublesRolled: slotTelemetry.doublesRolled,
        totalDiceValue: slotTelemetry.totalDiceValue,
        highestRoll: slotTelemetry.highestRoll,
        xpAwarded,
        levelAfter: level,
        participants: participantRows,
        resultAuthority: "canonical_network_match_result",
        telemetryAuthority: "bounded_host_client_telemetry",
        networkRevision: nonNegativeInt(network.revision, 1_000_000_000),
        networkSchemaVersion:
          nonNegativeInt(network.networkSchemaVersion, 10_000),
        progressionSchemaVersion: PROGRESSION_SCHEMA_VERSION,
        finishedAt: serverTimestamp,
      });

      for (const achievementId of newlyUnlocked) {
        const eventRef = db.collection("achievement_events").doc(
          evidenceId(matchId, seat.accountId, achievementId),
        );
        transaction.create(eventRef, {
          uid: seat.accountId,
          achievementId,
          matchId,
          schemaVersion: PROGRESSION_SCHEMA_VERSION,
          unlockedAt: serverTimestamp,
        });
      }

      affectedAccountIds.push(seat.accountId);
      accountResultEvidence.push({
        uid: seat.accountId,
        slotIndex: seat.slotIndex,
        result: kind,
        place: finalPlace(result, seat.slotIndex),
        netWorth,
        xpAwarded,
        levelAfter: level,
      });
    }

    transaction.create(markerRef, {
      matchId,
      lobbyId,
      hostAccountId: input.uid,
      mode,
      mapId,
      themeId,
      roundLimit,
      affectedAccountIds,
      accountResults: accountResultEvidence,
      participantCount: participantRows.length,
      resultAuthority: "canonical_network_match_result",
      networkRevision: nonNegativeInt(network.revision, 1_000_000_000),
      schemaVersion: PROGRESSION_SCHEMA_VERSION,
      createdAt: serverTimestamp,
    });

    transaction.set(
      matchRef,
      {
        status: "completed",
        progressionFinalized: true,
        progressionFinalizedAt: serverTimestamp,
      },
      {merge: true},
    );

    return {
      applied: true,
      idempotentReplay: false,
      affectedAccountIds,
    };
  });

  for (const uid of transactionResult.affectedAccountIds as string[]) {
    await pruneMatchHistory(uid);
  }

  return {
    ok: true,
    matchId,
    applied: transactionResult.applied,
    idempotentReplay: transactionResult.idempotentReplay,
    affectedAccountCount:
      (transactionResult.affectedAccountIds as string[]).length,
    affectedAccountIds: transactionResult.affectedAccountIds,
    schemaVersion: PROGRESSION_SCHEMA_VERSION,
  };
}

/**
 * Deletes user-facing match history outside the retention contract.
 * A match is removed when it is older than 72 hours OR when it falls outside
 * the newest 10 records, whichever comes first.
 * @param {string} uid Account id.
 * @return {Promise<number>} Number of deleted match-history documents.
 */
export async function pruneMatchHistory(uid: string): Promise<number> {
  const db = getFirestore();
  const collection = db
    .collection("account_match_history")
    .doc(uid)
    .collection("matches");
  const snapshot = await collection.get();
  const cutoffMs = Date.now() - HISTORY_RETENTION_MS;

  const docs = snapshot.docs
    .map((doc) => {
      const value = doc.data()?.finishedAt;
      const finishedAtMs = value instanceof Timestamp ? value.toMillis() : 0;
      return {doc, finishedAtMs};
    })
    .sort((left, right) => right.finishedAtMs - left.finishedAtMs);

  const deleteRefs: FirebaseFirestore.DocumentReference[] = [];
  docs.forEach((entry, index) => {
    if (
      entry.finishedAtMs <= 0 ||
      entry.finishedAtMs < cutoffMs ||
      index >= HISTORY_MAX_VISIBLE
    ) {
      deleteRefs.push(entry.doc.ref);
    }
  });

  if (deleteRefs.length === 0) {
    return 0;
  }

  const batch = db.batch();
  for (const ref of deleteRefs.slice(0, 400)) {
    batch.delete(ref);
  }
  await batch.commit();
  return Math.min(deleteRefs.length, 400);
}

/**
 * Converts one Firestore timestamp-like value into epoch milliseconds.
 * @param {unknown} value Firestore timestamp candidate.
 * @return {number} Epoch milliseconds or zero.
 */
function timestampEpochMs(value: unknown): number {
  return value instanceof Timestamp ? value.toMillis() : 0;
}

/**
 * Produces a plain callable-safe participant detail row.
 * @param {unknown} value Stored participant row.
 * @return {Record<string, unknown>} Sanitized participant row.
 */
function serializeParticipant(value: unknown): Record<string, unknown> {
  const data = value && typeof value === "object" ?
    value as Record<string, unknown> :
    {};
  return {
    slotIndex: nonNegativeInt(data.slotIndex, 3),
    displayName: safeString(data.displayName, 40),
    controllerKind: safeString(data.controllerKind, 32),
    isHost: data.isHost === true,
    bankrupt: data.bankrupt === true,
    place: Math.max(1, nonNegativeInt(data.place, 4)),
    result: safeString(data.result, 16),
    cash: nonNegativeInt(data.cash, Number.MAX_SAFE_INTEGER),
    propertyCount: nonNegativeInt(data.propertyCount, 10_000),
    propertyValue: nonNegativeInt(data.propertyValue, Number.MAX_SAFE_INTEGER),
    developmentLevels: nonNegativeInt(data.developmentLevels, 100_000),
    developmentValue:
      nonNegativeInt(data.developmentValue, Number.MAX_SAFE_INTEGER),
    netWorth: nonNegativeInt(data.netWorth, Number.MAX_SAFE_INTEGER),
  };
}

/**
 * Returns the authenticated player's aggregate progression, current level,
 * achievements and retained recent match history.
 * @param {GetProgressionProfileInput} input Authenticated account request.
 * @return {Promise<Record<string, unknown>>} Progression snapshot.
 */
export async function getProgressionProfile(
  input: GetProgressionProfileInput,
): Promise<Record<string, unknown>> {
  const db = getFirestore();
  await pruneMatchHistory(input.uid);

  const progressionRef = db.collection("account_progression").doc(input.uid);
  const profileRef = db.collection("public_profiles").doc(input.uid);
  const historyRef = db
    .collection("account_match_history")
    .doc(input.uid)
    .collection("matches");

  const [progressionSnapshot, publicProfileSnapshot, historySnapshot] =
    await Promise.all([
      progressionRef.get(),
      profileRef.get(),
      historyRef.orderBy("finishedAt", "desc").limit(HISTORY_MAX_VISIBLE).get(),
    ]);

  const data = progressionSnapshot.data() ?? {};
  const publicProfile = publicProfileSnapshot.data() ?? {};
  const stats = readCareerStats(data);
  const mapStats = readMapCareerStats(data);
  const totalXp = nonNegativeInt(data.totalXp, Number.MAX_SAFE_INTEGER);
  const level = levelFromXp(totalXp);
  const unlocked = new Set(
    Array.isArray(data.unlockedAchievements) ?
      data.unlockedAchievements.filter(
        (item: unknown) => typeof item === "string",
      ) as string[] :
      [],
  );

  const achievements = ACHIEVEMENTS.map((definition) => ({
    achievementId: definition.id,
    titleKey: definition.titleKey,
    descriptionKey: definition.descriptionKey,
    target: definition.target,
    progress: Math.min(
      definition.target,
      nonNegativeInt(definition.progress(stats), Number.MAX_SAFE_INTEGER),
    ),
    unlocked: unlocked.has(definition.id),
  }));

  const recentMatches = historySnapshot.docs.map((doc) => {
    const item = doc.data() ?? {};
    return {
      matchId: safeString(item.matchId, 128) || doc.id,
      lobbyId: safeString(item.lobbyId, 128),
      hostAccountId: safeString(item.hostAccountId, 128),
      mode: safeString(item.mode, 32),
      mapId: safeString(item.mapId, 64),
      themeId: safeString(item.themeId, 64),
      roundLimit: nonNegativeInt(item.roundLimit, 10_000),
      maxPlayers: nonNegativeInt(item.maxPlayers, 4),
      result: safeString(item.result, 16),
      place: Math.max(1, nonNegativeInt(item.place, 4)),
      playerCount: nonNegativeInt(item.playerCount, 4),
      localSlotIndex: nonNegativeInt(item.localSlotIndex, 3),
      bankrupt: item.bankrupt === true,
      finalCash: nonNegativeInt(item.finalCash, Number.MAX_SAFE_INTEGER),
      propertyCount: nonNegativeInt(item.propertyCount, 10_000),
      propertyValue:
        nonNegativeInt(item.propertyValue, Number.MAX_SAFE_INTEGER),
      developmentLevels: nonNegativeInt(item.developmentLevels, 100_000),
      developmentValue:
        nonNegativeInt(item.developmentValue, Number.MAX_SAFE_INTEGER),
      netWorth: nonNegativeInt(item.netWorth, Number.MAX_SAFE_INTEGER),
      highestMatchNetWorth:
        nonNegativeInt(item.highestMatchNetWorth, Number.MAX_SAFE_INTEGER),
      completedRounds: nonNegativeInt(item.completedRounds, 10_000),
      completedTurns: nonNegativeInt(item.completedTurns, 1_000_000),
      diceRolls: nonNegativeInt(item.diceRolls, 1_000_000),
      doublesRolled: nonNegativeInt(item.doublesRolled, 1_000_000),
      totalDiceValue: nonNegativeInt(item.totalDiceValue, 12_000_000),
      highestRoll: nonNegativeInt(item.highestRoll, 12),
      xpAwarded: nonNegativeInt(item.xpAwarded, 10_000),
      levelAfter: Math.max(1, nonNegativeInt(item.levelAfter, MAX_LEVEL)),
      finishedAtEpochMs: timestampEpochMs(item.finishedAt),
      participants: Array.isArray(item.participants) ?
        item.participants.map(serializeParticipant) :
        [],
    };
  });

  return {
    ok: true,
    accountId: input.uid,
    displayName: safeString(publicProfile.displayName, 40) || "Player",
    avatarId: safeString(publicProfile.avatarId, 64),
    totalXp,
    level,
    levelStartXp: levelStartXp(level),
    nextLevelXp: nextLevelXp(level),
    maxLevel: MAX_LEVEL,
    stats,
    mapStats,
    achievements,
    recentMatches,
    historyRetentionHours: HISTORY_RETENTION_MS / (60 * 60 * 1000),
    historyLimit: HISTORY_MAX_VISIBLE,
    schemaVersion: PROGRESSION_SCHEMA_VERSION,
  };
}
