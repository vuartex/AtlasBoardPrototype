import {createHash} from "node:crypto";

const PROJECT_ID = "atlasboard-usa";
const FIRESTORE_BASE = "http://127.0.0.1:8080";
const HUB_BASE = "http://127.0.0.1:4400";
const TIMEOUT_MS = 10000;
const EVENT_ID = "atlas_harvest_2026";
const TICKET_ID = "ticket_atlas_harvest_2026";

async function requestJson(url, options = {}) {
  const response = await fetch(url, {
    ...options,
    signal: AbortSignal.timeout(TIMEOUT_MS),
  });
  const text = await response.text();
  let json = null;
  if (text) {
    try { json = JSON.parse(text); } catch { json = {raw: text}; }
  }
  return {response, json, text};
}

function docUrl(path) {
  return `${FIRESTORE_BASE}/v1/projects/${PROJECT_ID}` +
    `/databases/(default)/documents/${path}`;
}

async function patch(path, fields) {
  const result = await requestJson(docUrl(path), {
    method: "PATCH",
    headers: {"Content-Type": "application/json"},
    body: JSON.stringify({fields}),
  });
  if (!result.response.ok) {
    throw new Error(`Seed failed for ${path}: ${result.text}`);
  }
}

function s(value) { return {stringValue: value}; }
function i(value) { return {integerValue: String(value)}; }
function b(value) { return {booleanValue: value}; }
function map(fields) { return {mapValue: {fields}}; }

const hub = await requestJson(`${HUB_BASE}/emulators`);
if (!hub.response.ok || !hub.json?.firestore) {
  throw new Error("Firestore emulator is not running.");
}

const now = Date.now();
const starts = now - 60_000;
const ends = now + 1000 * 60 * 60 * 24 * 30;

await patch("seasonal_runtime/active", {
  active: b(true),
  eventId: s(EVENT_ID),
  titleKey: s("seasonal.harvest.title"),
  subtitleKey: s("seasonal.harvest.subtitle"),
  startsAtEpochMs: i(starts),
  endsAtEpochMs: i(ends),
  ticketCurrencyId: s(TICKET_ID),
  schemaVersion: i(1),
});

const challenges = [
  {
    id: "daily_reward",
    cadence: "daily",
    criteriaType: "daily_reward_claimed",
    target: 1,
    rewardTickets: 3,
    rewardXp: 30,
    sortOrder: 10,
    titleKey: "seasonal.challenge.daily.title",
    descriptionKey: "seasonal.challenge.daily.body",
  },
  {
    id: "collector_weekly",
    cadence: "weekly",
    criteriaType: "owned_items",
    target: 2,
    rewardTickets: 7,
    rewardXp: 80,
    sortOrder: 20,
    titleKey: "seasonal.challenge.weekly.title",
    descriptionKey: "seasonal.challenge.weekly.body",
  },
];

for (const c of challenges) {
  await patch(`seasonal_challenges/${EVENT_ID}/definitions/${c.id}`, {
    active: b(true),
    challengeId: s(c.id),
    cadence: s(c.cadence),
    criteriaType: s(c.criteriaType),
    target: i(c.target),
    rewardTickets: i(c.rewardTickets),
    rewardXp: i(c.rewardXp),
    sortOrder: i(c.sortOrder),
    titleKey: s(c.titleKey),
    descriptionKey: s(c.descriptionKey),
    schemaVersion: i(1),
  });
}

const tiers = [
  {id: "tier_1", xp: 30, type: "gold", amount: 300, order: 10},
  {id: "tier_2", xp: 80, type: "event_ticket", amount: 5, order: 20},
  {id: "tier_3", xp: 110, type: "atlas_coin", amount: 10, order: 30},
];
for (const tier of tiers) {
  await patch(`seasonal_tracks/${EVENT_ID}/tiers/${tier.id}`, {
    active: b(true),
    tierId: s(tier.id),
    titleKey: s(`seasonal.track.${tier.id}`),
    requiredXp: i(tier.xp),
    rewardType: s(tier.type),
    rewardAmount: i(tier.amount),
    sortOrder: i(tier.order),
    schemaVersion: i(1),
  });
}

const limitedItems = [
  {
    itemId: "pawn_harvest_fox",
    itemType: "pawn",
    displayName: "Harvest Fox Pawn",
    ticketPrice: 8,
    sortOrder: 810,
  },
  {
    itemId: "dice_harvest_leaf",
    itemType: "dice_skin",
    displayName: "Harvest Leaf Dice",
    ticketPrice: 6,
    sortOrder: 820,
  },
];

for (const item of limitedItems) {
  await patch(`item_catalog/${item.itemId}`, {
    itemId: s(item.itemId),
    itemType: s(item.itemType),
    displayName: s(item.displayName),
    active: b(true),
    prices: map({gold: i(0), atlas_coin: i(0)}),
    seasonalEventId: s(EVENT_ID),
    seasonalTicketPrice: i(item.ticketPrice),
    limitedUntilEpochMs: i(ends),
    sortOrder: i(item.sortOrder),
    catalogVersion: i(1),
    schemaVersion: i(1),
  });
}

console.log("Atlas Board Phase 9.3B seasonal seed PASS.");
console.log(`Event=${EVENT_ID} Ticket=${TICKET_ID}`);
console.log("Seeded 2 challenges, 3 reward-track tiers, 2 limited items.");
