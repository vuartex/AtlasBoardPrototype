/**
 * Phase 17A: deterministic final scoreboard consistency preflight.
 *
 * Mirrors the exact arithmetic and non-bankrupt winner ranking implemented by
 * Unity's MatchResultManager.BuildOnlineResultSnapshot(). These are invariants,
 * not independent evidence of game play: the host still controls the source
 * values. NEVER mint match_result_verifications from this validator.
 */

export interface MatchResultConsistency {
  ok: boolean;
  reason: string;
}

type ValueMap = Record<string, unknown>;

function obj(value: unknown): ValueMap | null {
  return value !== null && typeof value === "object" &&
    !Array.isArray(value) ? value as ValueMap : null;
}

function int(value: unknown, min: number, max: number): value is number {
  return typeof value === "number" && Number.isSafeInteger(value) &&
    value >= min && value <= max;
}

function checkedArray(
  result: ValueMap,
  key: string,
  valueType: "int" | "bool",
): unknown[] | null {
  const value = result[key];
  if (!Array.isArray(value) || value.length !== 4) return null;
  if (!value.every((item: unknown) => valueType === "bool" ?
    typeof item === "boolean" : int(item, -2_000_000_000, 2_000_000_000))) {
    return null;
  }
  return value;
}

/**
 * Check internally consistent final results without granting trusted status.
 * Returns a stable reason key instead of exposing the raw untrusted document.
 * @param {unknown} candidate Raw Unity final frame.
 * @return {MatchResultConsistency} Deterministic validation result.
 */
export function validateMatchResultConsistency(
  candidate: unknown,
): MatchResultConsistency {
  const fail = (reason: string): MatchResultConsistency => ({ok: false, reason});
  const frame = obj(candidate);
  if (!frame || frame.phase !== "match_complete" ||
    !int(frame.schemaVersion, 1, 100) ||
    !int(frame.currentRound, 1, 100_000) ||
    !int(frame.roundLimit, 1, 100_000) ||
    frame.currentRound > (frame.roundLimit as number) + 1 ||
    !int(frame.completedTurns, 1, 1_000_000) ||
    !int(frame.diceSequence, 1, 1_000_000)) {
    return fail("INVALID_FINAL_FRAME");
  }
  const result = obj(frame.matchResult);
  if (!result || result.valid !== true) return fail("RESULT_NOT_VALID");

  const participating = checkedArray(result, "participating", "bool");
  const bankrupt = checkedArray(result, "bankrupt", "bool");
  const cash = checkedArray(result, "cash", "int");
  const propertyCount = checkedArray(result, "propertyCount", "int");
  const propertyValue = checkedArray(result, "propertyValue", "int");
  const developmentLevels = checkedArray(result, "developmentLevels", "int");
  const developmentValue = checkedArray(result, "developmentValue", "int");
  const netWorth = checkedArray(result, "netWorth", "int");

  if (!participating || !bankrupt || !cash || !propertyCount ||
    !propertyValue || !developmentLevels || !developmentValue || !netWorth) {
    return fail("INVALID_RESULT_ARRAYS");
  }
  if (!Array.isArray(result.winnerSlots) ||
    result.winnerSlots.length > 4 ||
    !result.winnerSlots.every((slot: unknown) => int(slot, 0, 3)) ||
    new Set(result.winnerSlots).size !== result.winnerSlots.length) {
    return fail("INVALID_WINNER_SLOTS");
  }
  if (!int(result.highestNetWorth, -2_000_000_000, 2_000_000_000)) {
    return fail("INVALID_HIGHEST_NET_WORTH");
  }
  let participants = 0;
  const eligible: number[] = [];
  for (let slot = 0; slot < 4; slot++) {
    if (participating[slot] !== true) {
      if (bankrupt[slot] !== false || cash[slot] !== 0 ||
        propertyCount[slot] !== 0 || propertyValue[slot] !== 0 ||
        developmentLevels[slot] !== 0 || developmentValue[slot] !== 0 ||
        netWorth[slot] !== 0) {
        return fail("NON_PARTICIPANT_HAS_RESULT");
      }
      continue;
    }
    participants++;
    if ((propertyCount[slot] as number) < 0 ||
      (propertyValue[slot] as number) < 0 ||
      (developmentLevels[slot] as number) < 0 ||
      (developmentValue[slot] as number) < 0) {
      return fail("NEGATIVE_ASSET_VALUES");
    }
    const computed = (cash[slot] as number) +
      (propertyValue[slot] as number) + (developmentValue[slot] as number);
    if (!Number.isSafeInteger(computed) || netWorth[slot] !== computed) {
      return fail("NET_WORTH_MISMATCH");
    }
    if (bankrupt[slot] === false) eligible.push(slot);
  }
  if (participants === 0) return fail("NO_PARTICIPANTS");
  const maxNetWorth = eligible.length ?
    Math.max(...eligible.map((slot) => netWorth[slot] as number)) : 0;
  const expectedWinners = eligible.filter((slot) => netWorth[slot] === maxNetWorth);
  const actualWinners = [...result.winnerSlots as number[]].sort((a, b) => a - b);
  if (result.highestNetWorth !== maxNetWorth ||
    expectedWinners.length !== actualWinners.length ||
    expectedWinners.some((slot, index) => slot !== actualWinners[index])) {
    return fail("WINNER_RANKING_MISMATCH");
  }
  return {ok: true, reason: "CONSISTENT_UNTRUSTED_HOST_RESULT"};
}
