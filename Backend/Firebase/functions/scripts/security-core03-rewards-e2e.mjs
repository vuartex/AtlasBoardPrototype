/**
 * Security Core 03: promo/daily/seasonal tier abuse regression.
 * Local Firebase Auth/Firestore/Functions emulators ONLY, no live network.
 * Invoked by scripts/qa.mjs --full after the TypeScript compile.
 */
import {createHash, randomBytes} from "node:crypto";

const PROJECT = "atlasboard-usa";
const REGION = "europe-west1";
const HOST = "127.0.0.1";
const AUTH = `http://${HOST}:9099`;
const FUNCTIONS = `http://${HOST}:5001`;
const FIRESTORE = `http://${HOST}:8080`;
const HUB = `http://${HOST}:4400`;
const TIMEOUT = 25000;
const nonce = randomBytes(7).toString("hex");
const eventId = `core03_season_${nonce}`;
const ticketId = `ticket_core03_${nonce}`;
const promoCode = `CORE03RACE${nonce.toUpperCase()}`;
const badCode = `CORE03DUP${nonce.toUpperCase()}`;
const promoId = (code) => hash(`atlasboard:promo:${code}`);
const hash = (str) => createHash("sha256").update(str).digest("hex");
const users = [];
const promoIds = [];
let db;
let auth;
let originalRuntime;
let runtimeSaved = false;
let checks = 0;
const count = (label) => console.log(`CORE03 [${++checks}/15] PASS ${label}`);

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

async function request(url, options = {}) {
  const response = await fetch(url, {
    ...options,
    signal: AbortSignal.timeout(TIMEOUT),
  });
  let body = {};
  try { body = await response.json(); } catch { /* Retain empty diagnostics. */ }
  return {response, body};
}

async function preflight() {
  // No Admin SDK reads/writes until all safety conditions pass.
  assert(process.env.FIRESTORE_EMULATOR_HOST === "127.0.0.1:8080",
    "Firestore must be 127.0.0.1:8080 emulator.");
  assert(process.env.FIREBASE_AUTH_EMULATOR_HOST === "127.0.0.1:9099",
    "Auth must be 127.0.0.1:9099 emulator.");
  assert(!process.env.GCLOUD_PROJECT || process.env.GCLOUD_PROJECT === PROJECT,
    "Unexpected Firebase project. Production is forbidden.");
  assert(!process.env.GOOGLE_CLOUD_PROJECT ||
    process.env.GOOGLE_CLOUD_PROJECT === PROJECT,
  "Unexpected Google project.");
  const hub = await request(`${HUB}/emulators`);
  assert(hub.response.ok && hub.body.auth &&
    hub.body.firestore && hub.body.functions,
  "Auth, Firestore and Functions emulators must all be running locally.");
  assert(hub.body.auth.host === HOST &&
    hub.body.firestore.host === HOST && hub.body.functions.host === HOST,
  "All emulator host bindings must be loopback.");
  const [{getApps, initializeApp}, {getFirestore}, {getAuth}] =
    await Promise.all([
      import("firebase-admin/app"),
      import("firebase-admin/firestore"),
      import("firebase-admin/auth"),
    ]);
  if (getApps().length === 0) initializeApp({projectId: PROJECT});
  db = getFirestore();
  auth = getAuth();
  count("emulator-only preflight and project isolation");
}

async function createUser(label) {
  const signup = await request(`${AUTH}/identitytoolkit.googleapis.com/v1/` +
    "accounts:signUp?key=atlasboard-emulator", {
    method: "POST",
    headers: {"Content-Type": "application/json"},
    body: JSON.stringify({
      email: `atlas.sec03.${label}.${nonce}@example.test`,
      password: "AtlasBoard-Security-Core03-Local-123!",
      returnSecureToken: true,
    }),
  });
  assert(signup.response.ok && signup.body.idToken && signup.body.localId,
    `Auth Emulator signup failed: ${JSON.stringify(signup.body)}`);
  const person = {uid: signup.body.localId, token: signup.body.idToken};
  users.push(person);
  return person;
}

async function call(name, person, data = {}) {
  return request(`${FUNCTIONS}/${PROJECT}/${REGION}/${name}`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...(person ? {Authorization: `Bearer ${person.token}`} : {}),
      "X-Firebase-AppCheck": "atlasboard-core03-local-token",
    },
    body: JSON.stringify({data}),
  });
}
const value = (x) => x.body?.result ?? null;
const success = (x) => x.response.ok && x.body?.result?.ok === true;
const denied = (x) => !x.response.ok || x.body?.error;

async function balance(uid, currency) {
  const doc = await db.doc(`wallets/${uid}/balances/${currency}`).get();
  return doc.exists ? doc.data()?.amount : 0;
}

async function unsafeFirestorePatch(person, path) {
  const url = `${FIRESTORE}/v1/projects/${PROJECT}` +
    `/databases/(default)/documents/${path}`;
  return request(url, {
    method: "PATCH",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${person.token}`,
    },
    body: JSON.stringify({fields: {active: {booleanValue: true}}}),
  });
}

async function seedPromo(code, rewards, globalLimit) {
  const now = Date.now();
  const id = promoId(code);
  promoIds.push(id);
  await db.doc(`promo_codes/${id}`).set({
    promoId: id,
    codeHash: id,
    active: true,
    startsAtEpochMs: now - 60_000,
    endsAtEpochMs: now + 60 * 60 * 1000,
    globalLimit,
    redemptionCount: 0,
    perAccountLimit: 1,
    rewards,
    schemaVersion: 1,
  });
  return id;
}

async function exercise() {
  await preflight();
  const owner = await createUser("owner");
  const challenger = await createUser("challenger");
  const loser = await createUser("loser");
  count("three temporary authenticated emulator identities");

  const forbidden = await Promise.all([
    unsafeFirestorePatch(owner, `promo_codes/${promoId(promoCode)}`),
    unsafeFirestorePatch(owner, "seasonal_runtime/active"),
    unsafeFirestorePatch(owner, `wallets/${owner.uid}/balances/gold`),
    unsafeFirestorePatch(owner, `seasonal_track_claims/${owner.uid}`),
  ]);
  assert(forbidden.every((r) => r.response.status === 403),
    "Direct Firestore client mutation was not denied.");
  count("client cannot modify promo, runtime, wallet or claim documents");

  const dailyClaims = await Promise.all(Array.from({length: 3},
    () => call("metaDailyRewardClaim", owner)));
  assert(dailyClaims.every(success) &&
    dailyClaims.filter((x) => value(x)?.applied === true).length === 1 &&
    dailyClaims.filter((x) => value(x)?.idempotentReplay === true).length === 2 &&
    await balance(owner.uid, "gold") === 250,
  `Concurrent daily claim anomaly: ${JSON.stringify(dailyClaims.map((x) => x.body))}`);
  const day = value(dailyClaims[0]).serverDayUtc;
  const dayClaim = await db.doc(`daily_reward_claims/${owner.uid}/days/${day}`).get();
  assert(dayClaim.exists && dayClaim.data()?.rewardGold === 250,
    "Daily claim evidence missing or inconsistent.");
  count("three concurrent daily claims grant exactly 250 Gold and one claim");

  const challengerDaily = await call("metaDailyRewardClaim", challenger);
  assert(success(challengerDaily) && value(challengerDaily)?.applied === true &&
    await balance(challenger.uid, "gold") === 250,
  "Daily reward is not isolated by authenticated account.");
  count("different account receives independent daily reward");

  const racePromoId = await seedPromo(promoCode,
    [{type: "gold", amount: 80}, {type: "atlas_coin", amount: 5}], 1);
  const race = await Promise.all([
    call("promoTestRedeem", challenger, {code: promoCode, idempotencyKey: `race-${nonce}-a`}),
    call("promoTestRedeem", loser, {code: promoCode, idempotencyKey: `race-${nonce}-b`}),
  ]);
  assert(race.filter(success).length === 1 && race.filter(denied).length === 1,
    `Global-limit concurrent redeem accepted !=1: ${JSON.stringify(race.map((x) => x.body))}`);
  const winner = success(race[0]) ? challenger : loser;
  const blocked = winner.uid === challenger.uid ? loser : challenger;
  const expectedGold = winner.uid === challenger.uid ? 330 : 80;
  assert(await balance(winner.uid, "gold") === expectedGold &&
    await balance(winner.uid, "atlas_coin") === 5 &&
    await balance(blocked.uid, "atlas_coin") === 0 &&
    (await db.doc(`promo_codes/${racePromoId}`).get()).data()?.redemptionCount === 1,
  "Concurrent promo race changed wallets or global counters incorrectly.");
  count("two concurrent accounts cannot exceed promo global redemption limit");

  const winnerKey = winner.uid === challenger.uid ? `race-${nonce}-a` : `race-${nonce}-b`;
  const replay = await call("promoTestRedeem", winner, {code: promoCode, idempotencyKey: winnerKey});
  assert(success(replay) && value(replay)?.idempotentReplay === true &&
    await balance(winner.uid, "gold") === expectedGold,
  "Promo replay minted Gold.");
  count("promo same-key replay is idempotent and cannot mint Gold");

  const newKey = await call("promoTestRedeem", winner,
    {code: promoCode, idempotencyKey: `second-${nonce}`});
  assert(denied(newKey) &&
    (await db.doc(`promo_codes/${racePromoId}`).get()).data()?.redemptionCount === 1,
  "Promo second key bypassed global/per-account limits.");
  count("promo alternative-key replay cannot bypass redemption limits");

  const invalidPromoId = await seedPromo(badCode,
    [{type: "gold", amount: 100}, {type: "gold", amount: 200}], 5);
  const duplicateTarget = await call("promoTestRedeem", loser,
    {code: badCode, idempotencyKey: `dup-${nonce}`});
  assert(denied(duplicateTarget) &&
    (await db.doc(`promo_codes/${invalidPromoId}`).get()).data()?.redemptionCount === 0,
  "Duplicate-target promo incorrectly applied.");
  count("promo with duplicate currency targets fails without partial rewards");

  const runtimeRef = db.doc("seasonal_runtime/active");
  originalRuntime = await runtimeRef.get();
  runtimeSaved = true;
  const now = Date.now();
  await runtimeRef.set({
    active: true,
    eventId,
    titleKey: "seasonal.security.core03",
    subtitleKey: "seasonal.security.core03.subtitle",
    startsAtEpochMs: now - 60_000,
    endsAtEpochMs: now + 60 * 60 * 1000,
    ticketCurrencyId: ticketId,
    schemaVersion: 1,
  });
  const tierId = `tier_disabled_${nonce}`;
  const tierRef = db.doc(`seasonal_tracks/${eventId}/tiers/${tierId}`);
  await tierRef.set({
    active: false,
    tierId,
    requiredXp: 30,
    rewardType: "gold",
    rewardAmount: 400,
    schemaVersion: 1,
  });
  await db.doc(`seasonal_progress/${owner.uid}/events/${eventId}`)
    .set({uid: owner.uid, eventId, xp: 30, schemaVersion: 1});
  count("disabled-tier, active-season and eligible-XP fixtures seeded");

  const disabled = await call("metaSeasonalClaimTrackTier", owner, {tierId});
  assert(denied(disabled) &&
    await balance(owner.uid, "gold") === 250 &&
    !(await db.doc(`seasonal_track_claims/${owner.uid}/events/${eventId}/tiers/${tierId}`).get()).exists,
  `Disabled seasonal tier granted a reward: ${JSON.stringify(disabled.body)}`);
  count("disabled seasonal tier rejected despite sufficient account XP");

  await tierRef.update({active: true});
  const locked = await call("metaSeasonalClaimTrackTier", challenger, {tierId});
  assert(denied(locked), "Account without seasonal XP claimed a locked tier.");
  count("enabled but XP-locked seasonal tier cannot mint a reward");

  const tierClaims = await Promise.all(Array.from({length: 3},
    () => call("metaSeasonalClaimTrackTier", owner, {tierId})));
  assert(tierClaims.every(success) &&
    tierClaims.filter((x) => value(x)?.applied === true).length === 1 &&
    tierClaims.filter((x) => value(x)?.idempotentReplay === true).length === 2 &&
    await balance(owner.uid, "gold") === 650,
  `Concurrent seasonal tier claims minted inconsistent reward: ${JSON.stringify(tierClaims.map((x) => x.body))}`);
  count("three simultaneous active tier claims credit Gold exactly once");

  const proof = await db.doc(`seasonal_track_claims/${owner.uid}/events/${eventId}/tiers/${tierId}`).get();
  const ledgerId = hash(`${owner.uid}:${hash(`${owner.uid}:${eventId}:track:${tierId}`)}`);
  const ledger = await db.doc(`wallet_ledger/${ledgerId}`).get();
  assert(proof.exists && proof.data()?.rewardAmount === 400 &&
    ledger.exists && ledger.data()?.delta === 400,
  "Seasonal tier claim and ledger evidence inconsistent.");
  count("seasonal claim ledger and immutable evidence persisted once");
  assert(await balance(loser.uid, "gold") === (winner.uid === loser.uid ? 80 : 0),
    "Cross-account seasonal or promo credit leaked.");
  count("no cross-account wallet credit from rewards");
  assert(checks === 15, `Security suite check count mismatch: ${checks}`);
  console.log("ATLASBOARD_SECURITY_CORE_03_REWARDS_PASS: 15/15 checks");
}

async function cleanup() {
  if (!db) return;
  const warnings = [];
  async function safe(action, label) {
    try { await action(); } catch (err) { warnings.push(`${label}: ${String(err)}`); }
  }
  // Only test-created documents; restore the previous seasonal event state.
  if (runtimeSaved) await safe(async () => {
    const ref = db.doc("seasonal_runtime/active");
    if (originalRuntime.exists) await ref.set(originalRuntime.data());
    else await ref.delete();
  }, "seasonal runtime restore");
  await safe(() => db.recursiveDelete(db.collection("seasonal_tracks").doc(eventId)),
    "test seasonal track");
  for (const id of promoIds) {
    await safe(() => db.recursiveDelete(db.collection("promo_codes").doc(id)), `promo ${id}`);
  }
  for (const person of users) {
    const uid = person.uid;
    for (const collection of [
      "daily_reward_claims", "wallets", "inventories", "seasonal_progress",
      "seasonal_track_claims", "seasonal_challenge_claims",
    ]) {
      await safe(() => db.recursiveDelete(db.collection(collection).doc(uid)),
        `${collection}/${uid}`);
    }
    for (const collection of ["wallet_ledger", "promo_redemptions", "seasonal_purchases"]) {
      await safe(async () => {
        const snapshots = await db.collection(collection).where("uid", "==", uid).get();
        for (const record of snapshots.docs) await db.recursiveDelete(record.ref);
      }, `${collection} user ${uid}`);
    }
    await safe(() => auth.deleteUser(uid), `auth/${uid}`);
  }
  if (warnings.length > 0) throw new Error(`Fixture cleanup warnings:\n${warnings.join("\n")}`);
  console.log("CORE03 cleanup: PASS (local fixtures removed, seasonal runtime restored).");
}

if (process.argv.includes("--self-test")) {
  assert(promoId("CODE") === hash("atlasboard:promo:CODE"), "Promo digest mismatch.");
  assert(hash("a").length === 64 && ticketId.startsWith("ticket_"),
    "Deterministic security identifiers invalid.");
  console.log("Security Core 03 offline self-test PASS.");
} else {
  let failed = null;
  try { await exercise(); } catch (error) { failed = error; }
  try { await cleanup(); } catch (error) { failed = failed ?? error; }
  if (failed) {
    console.error("ATLASBOARD_SECURITY_CORE_03_REWARDS_FAIL", failed);
    process.exitCode = 1;
  }
}
