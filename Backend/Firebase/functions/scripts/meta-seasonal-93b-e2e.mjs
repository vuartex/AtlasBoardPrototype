const PROJECT_ID = "atlasboard-usa";
const REGION = "europe-west1";
const AUTH_BASE = "http://127.0.0.1:9099";
const FUNCTIONS_BASE = "http://127.0.0.1:5001";
const FIRESTORE_BASE = "http://127.0.0.1:8080";
const HUB_BASE = "http://127.0.0.1:4400";
const API_KEY = "atlasboard-local-emulator-only";
const TIMEOUT_MS = 10000;

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

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

async function postJson(url, body, headers = {}) {
  return requestJson(url, {
    method: "POST",
    headers: {"Content-Type": "application/json", ...headers},
    body: JSON.stringify(body),
  });
}

function callableUrl(name) {
  return `${FUNCTIONS_BASE}/${PROJECT_ID}/${REGION}/${name}`;
}

async function call(name, token, data = {}) {
  return postJson(
    callableUrl(name),
    {data},
    {Authorization: `Bearer ${token}`},
  );
}

function result(callable) {
  return callable.json?.result ?? callable.json?.data;
}

function docUrl(path) {
  return `${FIRESTORE_BASE}/v1/projects/${PROJECT_ID}` +
    `/databases/(default)/documents/${path}`;
}

async function patch(path, fields) {
  const r = await requestJson(docUrl(path), {
    method: "PATCH",
    headers: {"Content-Type": "application/json"},
    body: JSON.stringify({fields}),
  });
  assert(r.response.ok, `Firestore seed failed ${path}: ${r.text}`);
}

async function remove(path) {
  await requestJson(docUrl(path), {method: "DELETE"});
}

function s(value) { return {stringValue: value}; }
function b(value) { return {booleanValue: value}; }
function i(value) { return {integerValue: String(value)}; }

console.log("Atlas Board Phase 9.3B Seasonal Productization E2E");
console.log("Safety: local Auth/Firestore/Functions emulators only.");

const hub = await requestJson(`${HUB_BASE}/emulators`);
assert(hub.response.ok, "Emulator Hub unavailable.");
for (const name of ["auth", "firestore", "functions"]) {
  assert(hub.json?.[name], `Required emulator not running: ${name}`);
}

await import(`./meta-seasonal-93b-seed.mjs?${Date.now()}`);
console.log("[1/10] PASS seasonal definitions seeded.");

const nonce = `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`;
const signup = await postJson(
  `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=${API_KEY}`,
  {
    email: `atlasboard.seasonal.${nonce}@example.com`,
    password: `AtlasSeasonal!${nonce}`,
    returnSecureToken: true,
  },
);
assert(signup.response.ok, `Auth sign-up failed: ${signup.text}`);
const token = signup.json?.idToken;
const uid = signup.json?.localId;
assert(token && uid, "Auth emulator returned no identity.");
console.log("[2/10] PASS authenticated temporary account.");

const overview0 = await call("metaSeasonalGetOverview", token);
assert(overview0.response.ok, `Overview failed: ${overview0.text}`);
const o0 = result(overview0);
assert(o0?.ok === true && o0?.event?.active === true, "Season inactive.");
assert(Array.isArray(o0?.challenges) && o0.challenges.length === 2,
  "Expected 2 seasonal challenges.");
assert(Array.isArray(o0?.trackTiers) && o0.trackTiers.length === 3,
  "Expected 3 reward-track tiers.");
assert(Array.isArray(o0?.limitedItems) && o0.limitedItems.length === 2,
  "Expected 2 limited items.");
console.log("[3/10] PASS overview exposes event/tickets/challenges/track/items.");

const daily = await call("metaDailyRewardClaim", token);
assert(daily.response.ok && result(daily)?.applied === true,
  `Daily reward failed: ${daily.text}`);
console.log("[4/10] PASS daily reward satisfied daily challenge criterion.");

const dailyChallenge = await call(
  "metaSeasonalClaimChallenge",
  token,
  {challengeId: "daily_reward"},
);
assert(dailyChallenge.response.ok, `Daily challenge claim failed: ${dailyChallenge.text}`);
const dc = result(dailyChallenge);
assert(dc?.applied === true && dc?.ticketBalance === 3 && dc?.eventXp === 30,
  "Daily challenge reward mismatch.");
const dailyReplay = await call(
  "metaSeasonalClaimChallenge",
  token,
  {challengeId: "daily_reward"},
);
assert(dailyReplay.response.ok && result(dailyReplay)?.idempotentReplay === true,
  "Daily challenge replay was not idempotent.");
console.log("[5/10] PASS daily challenge granted once and replayed safely.");

for (const itemId of ["e2e_owned_a", "e2e_owned_b"]) {
  await patch(`inventories/${uid}/items/${itemId}`, {
    uid: s(uid),
    itemId: s(itemId),
    itemType: s("profile_frame"),
    owned: b(true),
    quantity: i(1),
    schemaVersion: i(1),
  });
}
const weekly = await call(
  "metaSeasonalClaimChallenge",
  token,
  {challengeId: "collector_weekly"},
);
assert(weekly.response.ok, `Weekly challenge claim failed: ${weekly.text}`);
const wc = result(weekly);
assert(wc?.applied === true && wc?.ticketBalance === 10 && wc?.eventXp === 110,
  "Weekly challenge reward mismatch.");
console.log("[6/10] PASS weekly challenge granted tickets + XP.");

const track = await call(
  "metaSeasonalClaimTrackTier",
  token,
  {tierId: "tier_1"},
);
assert(track.response.ok && result(track)?.applied === true,
  `Track claim failed: ${track.text}`);
assert(result(track)?.rewardType === "gold" && result(track)?.rewardAmount === 300,
  "Track reward mismatch.");
console.log("[7/10] PASS reward-track tier claimed server-authoritatively.");

const purchase = await call(
  "metaSeasonalPurchaseLimitedItem",
  token,
  {
    itemId: "pawn_harvest_fox",
    idempotencyKey: `e2e-seasonal-buy-${nonce}`,
  },
);
assert(purchase.response.ok, `Limited purchase failed: ${purchase.text}`);
const pr = result(purchase);
assert(pr?.applied === true && pr?.ticketPrice === 8 && pr?.ticketBalance === 2,
  "Limited purchase balance mismatch.");
console.log("[8/10] PASS limited item purchased with event tickets.");

const replay = await call(
  "metaSeasonalPurchaseLimitedItem",
  token,
  {
    itemId: "pawn_harvest_fox",
    idempotencyKey: `e2e-seasonal-buy-${nonce}`,
  },
);
assert(replay.response.ok && result(replay)?.idempotentReplay === true,
  "Limited purchase replay was not idempotent.");
console.log("[9/10] PASS limited purchase replay did not double debit.");

const overview1 = await call("metaSeasonalGetOverview", token);
assert(overview1.response.ok, `Final overview failed: ${overview1.text}`);
const o1 = result(overview1);
assert(o1?.ticketBalance === 2 && o1?.eventXp === 110,
  "Final seasonal balance/XP mismatch.");
const purchased = o1?.limitedItems?.find((item) => item.itemId === "pawn_harvest_fox");
assert(purchased?.owned === true, "Purchased seasonal item not restored as owned.");
console.log("[10/10] PASS overview restores account seasonal state.");

for (const itemId of ["e2e_owned_a", "e2e_owned_b"]) {
  await remove(`inventories/${uid}/items/${itemId}`);
}

console.log("Phase 9.3B E2E PASS: 10 checks.");
