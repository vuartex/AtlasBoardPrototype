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

async function patchJson(url, body) {
  return requestJson(url, {
    method: "PATCH",
    headers: {"Content-Type": "application/json"},
    body: JSON.stringify(body),
  });
}

function functionUrl(name) {
  return `${FUNCTIONS_BASE}/${PROJECT_ID}/${REGION}/${name}`;
}

async function callFunction(name, token, data = {}) {
  return postJson(
    functionUrl(name),
    {data},
    {Authorization: `Bearer ${token}`},
  );
}

function firestoreUrl(path) {
  return `${FIRESTORE_BASE}/v1/projects/${PROJECT_ID}` +
    `/databases/(default)/documents/${path}`;
}

async function getDoc(path) {
  return requestJson(firestoreUrl(path));
}

function integerField(doc, name) {
  const value = doc?.json?.fields?.[name]?.integerValue;
  return value === undefined ? undefined : Number.parseInt(value, 10);
}

async function preflight() {
  const hub = await requestJson(`${HUB_BASE}/emulators`);
  assert(hub.response.ok, "Firebase Emulator Hub is not reachable.");
  for (const name of ["auth", "firestore", "functions"]) {
    assert(hub.json?.[name], `Required emulator is not running: ${name}`);
  }
}

const nonce = `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`;
const email = `atlasboard.seasonal.${nonce}@example.com`;
const password = `AtlasBoardSeasonal!${nonce}`;

console.log("Atlas Board Phase 9.3A Seasonal + Daily Reward E2E");
console.log("Safety: local Auth/Firestore/Functions emulators only.");

await preflight();

const signup = await postJson(
  `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=${API_KEY}`,
  {email, password, returnSecureToken: true},
);
assert(signup.response.ok, `Auth signup failed: ${signup.text}`);
const token = signup.json?.idToken;
const uid = signup.json?.localId;
assert(token && uid, "Auth emulator returned no identity.");
console.log("[1/8] PASS authenticated temporary account.");

const now = Date.now();
const eventSeed = await patchJson(
  firestoreUrl("seasonal_runtime/active"),
  {
    fields: {
      active: {booleanValue: true},
      eventId: {stringValue: "founders_preview_2026"},
      titleKey: {stringValue: "season.founders.title"},
      startsAtEpochMs: {integerValue: String(now - 60_000)},
      endsAtEpochMs: {integerValue: String(now + 86_400_000)},
      ticketCurrencyId: {stringValue: "ticket_founders_preview"},
      schemaVersion: {integerValue: "1"},
    },
  },
);
assert(eventSeed.response.ok, `Seasonal seed failed: ${eventSeed.text}`);
console.log("[2/8] PASS active seasonal event seeded.");

const eventRead = await callFunction("metaSeasonalGetActive", token);
assert(eventRead.response.ok, `Seasonal read failed: ${eventRead.text}`);
const eventResult = eventRead.json?.result ?? eventRead.json?.data;
assert(eventResult?.ok === true && eventResult?.active === true,
  "Active seasonal snapshot did not return active=true.");
assert(eventResult?.eventId === "founders_preview_2026",
  "Seasonal event id mismatch.");
console.log("[3/8] PASS server-authoritative seasonal snapshot.");

const first = await callFunction("metaDailyRewardClaim", token);
assert(first.response.ok, `Daily claim failed: ${first.text}`);
const firstResult = first.json?.result ?? first.json?.data;
assert(firstResult?.ok === true && firstResult?.applied === true,
  "First daily claim was not applied.");
assert(firstResult?.rewardGold === 250 && firstResult?.balanceAfter === 250,
  "First daily claim did not grant exactly 250 Gold.");
console.log("[4/8] PASS first daily reward granted exactly once.");

const second = await callFunction("metaDailyRewardClaim", token);
assert(second.response.ok, `Daily replay failed: ${second.text}`);
const secondResult = second.json?.result ?? second.json?.data;
assert(secondResult?.applied === false && secondResult?.idempotentReplay === true,
  "Second daily claim was not an idempotent replay.");
assert(secondResult?.balanceAfter === 250,
  "Daily replay changed the wallet balance.");
console.log("[5/8] PASS same-day replay did not double grant.");

const wallet = await getDoc(`wallets/${uid}/balances/gold`);
assert(wallet.response.ok && integerField(wallet, "amount") === 250,
  "Wallet Gold balance is not 250 after one daily claim.");
console.log("[6/8] PASS authoritative wallet balance persisted.");

const serverDayUtc = firstResult?.serverDayUtc;
assert(typeof serverDayUtc === "string" && /^\d{8}$/.test(serverDayUtc),
  "Server UTC day is missing or invalid.");
const claim = await getDoc(`daily_reward_claims/${uid}/days/${serverDayUtc}`);
assert(claim.response.ok, "Daily claim evidence document is missing.");
assert(integerField(claim, "rewardGold") === 250,
  "Daily claim evidence reward mismatch.");
console.log("[7/8] PASS immutable daily claim evidence persisted.");

const ledgerId = firstResult?.ledgerEntryId;
assert(typeof ledgerId === "string" && ledgerId.length > 20,
  "Daily reward ledger id is missing.");
const ledger = await getDoc(`wallet_ledger/${ledgerId}`);
assert(ledger.response.ok && integerField(ledger, "delta") === 250,
  "Daily reward wallet ledger evidence is missing.");
console.log("[8/8] PASS immutable wallet ledger evidence persisted.");

console.log("Phase 9.3A E2E PASS: 8 checks.");
