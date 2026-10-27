// REST-only security acceptance. This MUST run with the local demo emulators.
const assert = require("node:assert/strict");
if (!process.env.FIREBASE_DATABASE_EMULATOR_HOST ||
    !process.env.FIREBASE_AUTH_EMULATOR_HOST ||
    process.env.GCLOUD_PROJECT !== "demo-atlasboard") {
  throw new Error("EMULATOR_ONLY: run emulators:exec --project demo-atlasboard --only auth,database");
}

const dbHost = "http://127.0.0.1:9000";
const authHost = "http://127.0.0.1:9099";
const namespace = "demo-atlasboard-default-rtdb";
let counter = 0, passed = 0;
const id = () => (++counter).toString(16).padStart(32, "0");
const newFrames = ["frame_neon_circuit", "frame_royal_crown",
  "frame_ocean_tide", "frame_sakura_blossom", "frame_cosmic_nova",
  "frame_forest_spirit"];
const frames = () => ({frame_halloween: false, frame_new_year: false,
  frame_turkiye: false, frame_usa: false,
  ...Object.fromEntries(newFrames.map(key => [key, false]))});
const init = () => ({schemaVersion: 1,
  wallet: {gold: 0, atlasCoin: 0, starterGrantClaimed: false},
  inventory: {profile_frames: frames()}, equipped: {profileFrameId: ""},
  dailyReward: {lastClaimAt: 0}, dev: {grantCount: 0, resetCount: 0},
  operation: {id: id(), type: "INIT", itemId: "", atMs: Date.now()}});
const next = (old, type, itemId = "") => {
  const state = structuredClone(old);
  state.operation = {id: id(), type, itemId,
    atMs: Math.max(Date.now(), old.operation.atMs + 1)};
  return state;
};
async function signup(label, guest = false) {
  const body = guest ? {returnSecureToken: true} : {
    email: label + "@atlasboard.invalid",
    password: "AtlasBoard-Local-QA-Only!1",
    returnSecureToken: true
  };
  const response = await fetch(authHost +
    "/identitytoolkit.googleapis.com/v1/accounts:signUp?key=local-demo", {
    method: "POST", headers: {"Content-Type": "application/json"},
    body: JSON.stringify(body)});
  const data = await response.json();
  if (!response.ok || !data.localId || !data.idToken)
    throw new Error("AUTH_EMULATOR_HTTP_" + response.status);
  return {uid: data.localId, token: data.idToken, email: body.email || ""};
}
async function db(method, location, token, body, extraHeaders = {}) {
  const url = dbHost + "/" + location + ".json?ns=" + namespace +
    (token === "owner" ? "" : "&auth=" + encodeURIComponent(token));
  const response = await fetch(url, {
    method, headers: {"Content-Type": "application/json",
      ...(token === "owner" ? {Authorization: "Bearer owner"} : {}),
      ...extraHeaders},
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: AbortSignal.timeout(7000)
  });
  const payload = await response.text();
  return {status: response.status, body: payload,
    etag: response.headers.get("etag")};
}
function expect(status, result, label) {
  const allowed = status === "allow"
    ? result.status >= 200 && result.status < 300
    : result.status === 401 || result.status === 403;
  assert(allowed, label + " expected=" + status +
    " actual=" + result.status + " body=" + result.body.slice(0, 160));
  passed++;
}
function profile(user) {
  return {userId: user.uid, playerId: "1234567890123456",
    email: user.email, displayName: "Atlas Tester",
    createdAt: Date.now() - 100000, updatedAt: Date.now(),
    lastDisplayNameChangeAt: Date.now() - 100000,
    general: {schemaVersion: 1, accountType: user.email ? "registered" : "guest",
      language: "en", countryCode: "US", lastSeenAt: Date.now()}};
}

(async () => {
  const user = await signup("registered-phase19c");
  const guest = await signup("guest-phase19c", true);
  const stranger = await signup("stranger-phase19c");
  const wallet = "users/" + user.uid + "/metaEconomy";
  const otherWallet = "users/" + stranger.uid + "/metaEconomy";
  expect("allow", await db("PUT", "users/" + user.uid, "owner", profile(user)),
    "admin seed profile");
  expect("allow", await db("PUT", "users/" + stranger.uid, "owner", profile(stranger)),
    "admin seed other");
  expect("allow", await db("PUT", "users/" + guest.uid, "owner", profile(guest)),
    "admin seed guest");
  expect("allow", await db("PATCH", "users/" + guest.uid, guest.token,
    {general: profile(guest).general}), "anonymous profile stays guest");
  expect("deny", await db("PATCH", "users/" + guest.uid, guest.token,
    {general: {...profile(guest).general, accountType: "registered"}}),
    "anonymous cannot spoof registered");
  expect("allow", await db("PUT", "devWalletTesters/" + user.uid, "owner", true),
    "admin allowlist");

  expect("allow", await db("PATCH", "users/" + user.uid, user.token,
    {updatedAt: Date.now(), displayName: "Atlas Tester"}), "profile mirror patch");
  const start = init();
  expect("allow", await db("PUT", wallet, user.token, start), "init");
  const guestWallet = "users/" + guest.uid + "/metaEconomy";
  const guestStart = init();
  expect("allow", await db("PUT", guestWallet,
    guest.token, guestStart), "anonymous zero wallet init");
  expect("deny", await db("PUT", guestWallet + "/wallet/gold",
    guest.token, 100000), "anonymous forged gold denied");
  const guestUnapprovedDaily = next(guestStart, "DAILY");
  guestUnapprovedDaily.wallet.gold += 50;
  guestUnapprovedDaily.dailyReward.lastClaimAt = guestUnapprovedDaily.operation.atMs;
  expect("deny", await db("PUT", guestWallet, guest.token, guestUnapprovedDaily),
    "unapproved anonymous daily denied");
  expect("deny", await db("PUT", wallet, stranger.token, init()), "foreign wallet");
  expect("deny", await db("PUT", wallet + "/wallet/gold",
    user.token, 999999), "tamper gold");
  expect("deny", await db("PUT", "devWalletTesters/" + user.uid,
    user.token, true), "self allowlist");

  const dev = next(start, "DEV_GRANT");
  dev.wallet.gold = 5000;
  dev.wallet.atlasCoin = 500;
  dev.dev.grantCount = 1;
  expect("allow", await db("PUT", wallet, user.token, dev), "dev grant");

  const forged = next(dev, "DAILY");
  forged.wallet.gold += 99999;
  forged.dailyReward.lastClaimAt = forged.operation.atMs;
  expect("deny", await db("PUT", wallet, user.token, forged), "forged gold");
  const daily = next(dev, "DAILY");
  daily.wallet.gold += 50;
  daily.dailyReward.lastClaimAt = daily.operation.atMs;
  expect("allow", await db("PUT", wallet, user.token, daily), "daily");
  const replay = next(daily, "DAILY");
  replay.wallet.gold += 50;
  replay.dailyReward.lastClaimAt = replay.operation.atMs;
  expect("deny", await db("PUT", wallet, user.token, replay), "daily replay");

  const buy = next(daily, "PURCHASE", "frame_halloween");
  buy.wallet.gold -= 350;
  buy.inventory.profile_frames.frame_halloween = true;
  const beforeBuy = await db("GET", wallet, user.token, undefined,
    {"X-Firebase-ETag": "true"});
  assert(beforeBuy.etag, "Firebase REST ETag must exist");
  expect("allow", await db("PUT", wallet, user.token, buy,
    {"if-match": beforeBuy.etag}), "buy frame with ETag");
  const stale = next(daily, "PURCHASE", "frame_new_year");
  stale.wallet.gold -= 350;
  stale.inventory.profile_frames.frame_new_year = true;
  const staleResult = await db("PUT", wallet, user.token, stale,
    {"if-match": beforeBuy.etag});
  assert.equal(staleResult.status, 412, "concurrent stale ETag must fail");
  passed++;
  const duplicate = next(buy, "PURCHASE", "frame_halloween");
  duplicate.wallet.gold -= 350;
  expect("deny", await db("PUT", wallet, user.token, duplicate), "buy replay");
  const steal = next(buy, "PURCHASE", "frame_new_year");
  steal.wallet.gold -= 350;
  steal.inventory.profile_frames.frame_new_year = true;
  steal.inventory.profile_frames.frame_turkiye = true;
  expect("deny", await db("PUT", wallet, user.token, steal), "two items for one");

  const publicProfile = frame => ({displayName: "Atlas Tester", avatarId: "",
    profileFrameId: frame, updatedAt: Date.now()});
  expect("deny", await db("PUT", "publicProfiles/" + user.uid, user.token,
    publicProfile("frame_halloween")), "frame before equip");
  const equip = next(buy, "EQUIP", "frame_halloween");
  equip.equipped.profileFrameId = "frame_halloween";
  expect("allow", await db("PUT", wallet, user.token, equip), "equip owned");
  expect("allow", await db("PUT", "publicProfiles/" + user.uid, user.token,
    publicProfile("frame_halloween")), "public frame");

  const reset = next(equip, "RESET_PURCHASES");
  reset.wallet.gold += 350;
  reset.inventory.profile_frames = frames();
  reset.equipped.profileFrameId = "";
  reset.dev.resetCount = 1;
  expect("allow", await db("PUT", wallet, user.token, reset), "refund and reset");
  const repeatReset = next(reset, "RESET_PURCHASES");
  repeatReset.wallet.gold += 350;
  repeatReset.dev.resetCount = 2;
  expect("deny", await db("PUT", wallet, user.token, repeatReset),
    "double refund");

  // Phase 19I: six additional paid frames. Each must be server-priced,
  // individually owned, equip-authorized and refundable without replay.
  const neonBuy = next(reset, "PURCHASE", "frame_neon_circuit");
  neonBuy.wallet.gold -= 450;
  neonBuy.inventory.profile_frames.frame_neon_circuit = true;
  expect("allow", await db("PUT", wallet, user.token, neonBuy),
    "neon circuit purchase exact 450 Gold");
  const cheapRoyal = next(neonBuy, "PURCHASE", "frame_royal_crown");
  cheapRoyal.wallet.gold -= 1;
  cheapRoyal.inventory.profile_frames.frame_royal_crown = true;
  expect("deny", await db("PUT", wallet, user.token, cheapRoyal),
    "royal crown undercharge blocked");
  const newReplay = next(neonBuy, "PURCHASE", "frame_neon_circuit");
  newReplay.wallet.gold -= 450;
  expect("deny", await db("PUT", wallet, user.token, newReplay),
    "new frame purchase replay blocked");
  const dualNew = next(neonBuy, "PURCHASE", "frame_royal_crown");
  dualNew.wallet.gold -= 500;
  dualNew.inventory.profile_frames.frame_royal_crown = true;
  dualNew.inventory.profile_frames.frame_forest_spirit = true;
  expect("deny", await db("PUT", wallet, user.token, dualNew),
    "two new entitlements for one payment blocked");

  let completeNewCatalog = neonBuy;
  for (const [newId, goldPrice] of [
    ["frame_royal_crown", 500], ["frame_ocean_tide", 450],
    ["frame_sakura_blossom", 475], ["frame_cosmic_nova", 550],
    ["frame_forest_spirit", 475]]) {
    const underpaid = next(completeNewCatalog, "PURCHASE", newId);
    underpaid.wallet.gold -= goldPrice - 1;
    underpaid.inventory.profile_frames[newId] = true;
    expect("deny", await db("PUT", wallet, user.token, underpaid),
      "cannot underpay " + newId);
    const fullPrice = next(completeNewCatalog, "PURCHASE", newId);
    fullPrice.wallet.gold -= goldPrice;
    fullPrice.inventory.profile_frames[newId] = true;
    expect("allow", await db("PUT", wallet, user.token, fullPrice),
      "exact server-verified price for " + newId);
    completeNewCatalog = fullPrice;
  }
  const neonEquip = next(completeNewCatalog, "EQUIP", "frame_neon_circuit");
  neonEquip.equipped.profileFrameId = "frame_neon_circuit";
  expect("allow", await db("PUT", wallet, user.token, neonEquip),
    "equipping owned new frame");
  expect("allow", await db("PUT", "publicProfiles/" + user.uid,
    user.token, publicProfile("frame_neon_circuit")),
    "new frame visible in verified public profile");
  const neonReset = next(neonEquip, "RESET_PURCHASES");
  neonReset.wallet.gold += 2900;
  neonReset.inventory.profile_frames = frames();
  neonReset.equipped.profileFrameId = "";
  neonReset.dev.resetCount++;
  expect("allow", await db("PUT", wallet, user.token, neonReset),
    "all six new frames reset refunds exact 2900 Gold");

  // Owner wallets created in v5.49.9 had four fields, not ten. A legitimate
  // DAILY / PURCHASE must be able to add six missing false flags atomically;
  // this must not allow another entitlement to change in the same operation.
  const legacy = await signup("legacy-phase19i");
  expect("allow", await db("PUT", "users/" + legacy.uid,
    "owner", profile(legacy)), "seed legacy profile");
  const legacyWallet = "users/" + legacy.uid + "/metaEconomy";
  const legacyStart = init();
  legacyStart.wallet.gold = 1000; // Admin-only fixture funding.
  legacyStart.inventory.profile_frames.frame_halloween = true;
  legacyStart.equipped.profileFrameId = "frame_halloween";
  for (const key of newFrames) delete legacyStart.inventory.profile_frames[key];
  expect("allow", await db("PUT", legacyWallet, "owner", legacyStart),
    "admin seeds old-schema player wallet");
  const migratedDaily = next(legacyStart, "DAILY");
  migratedDaily.wallet.gold += 50;
  migratedDaily.dailyReward.lastClaimAt = migratedDaily.operation.atMs;
  for (const key of newFrames) migratedDaily.inventory.profile_frames[key] = false;
  expect("allow", await db("PUT", legacyWallet, legacy.token, migratedDaily),
    "existing 4-frame wallet upgrades during normal daily without loss");
  const legacyBuy = next(migratedDaily, "PURCHASE", "frame_ocean_tide");
  legacyBuy.wallet.gold -= 450;
  legacyBuy.inventory.profile_frames.frame_ocean_tide = true;
  expect("allow", await db("PUT", legacyWallet, legacy.token, legacyBuy),
    "migrated owner buys Ocean Tide without losing Halloween ownership");
  const legacyTamper = next(legacyBuy, "EQUIP", "frame_ocean_tide");
  legacyTamper.equipped.profileFrameId = "frame_ocean_tide";
  legacyTamper.inventory.profile_frames.frame_cosmic_nova = true;
  expect("deny", await db("PUT", legacyWallet, legacy.token, legacyTamper),
    "migration cannot invent another owned new cosmetic");

  // Admin-approved anonymous UID: same permission used by the owner Steam
  // QA account. No Steam client flag can self-authorize this server state.
  expect("allow", await db("PUT", "devWalletTesters/" + guest.uid,
    "owner", true), "admin approve anonymous test UID");
  const guestDev = next(guestStart, "DEV_GRANT");
  guestDev.wallet.gold += 5000;
  guestDev.wallet.atlasCoin += 500;
  guestDev.dev.grantCount++;
  expect("allow", await db("PUT", guestWallet, guest.token, guestDev),
    "allowlisted anonymous developer grant");
  const guestDaily = next(guestDev, "DAILY");
  guestDaily.wallet.gold += 50;
  guestDaily.dailyReward.lastClaimAt = guestDaily.operation.atMs;
  expect("allow", await db("PUT", guestWallet, guest.token, guestDaily),
    "allowlisted anonymous first daily");
  const guestDoubleDaily = next(guestDaily, "DAILY");
  guestDoubleDaily.wallet.gold += 50;
  guestDoubleDaily.dailyReward.lastClaimAt = guestDoubleDaily.operation.atMs;
  expect("deny", await db("PUT", guestWallet, guest.token, guestDoubleDaily),
    "allowlisted anonymous duplicate daily denied");
  const guestBuy = next(guestDaily, "PURCHASE", "frame_halloween");
  guestBuy.wallet.gold -= 350;
  guestBuy.inventory.profile_frames.frame_halloween = true;
  expect("allow", await db("PUT", guestWallet, guest.token, guestBuy),
    "allowlisted anonymous profile purchase");
  // Steam QA uses an admin-approved Firebase anonymous UID: purchased frames
  // must be both equippable in the existing wallet and publicly selectable.
  const guestEquip = next(guestBuy, "EQUIP", "frame_halloween");
  guestEquip.equipped.profileFrameId = "frame_halloween";
  expect("allow", await db("PUT", guestWallet, guest.token, guestEquip),
    "allowlisted Steam-style anonymous account equips owned frame");
  expect("allow", await db("PUT", "publicProfiles/" + guest.uid, guest.token,
    publicProfile("frame_halloween")),
    "Steam-style anonymous account syncs equipped public profile");

  const otherInit = init();
  expect("allow", await db("PUT", otherWallet, stranger.token, otherInit),
    "other init");
  const notDev = next(otherInit, "DEV_GRANT");
  notDev.wallet.gold = 5000;
  notDev.wallet.atlasCoin = 500;
  notDev.dev.grantCount = 1;
  expect("deny", await db("PUT", otherWallet, stranger.token, notDev),
    "unallowlisted dev");

  process.stdout.write("ATB_PHASE19I_SECURITY_QA PASS assertions=" +
    passed + " guestDenied=true tamperDenied=true purchaseReplayDenied=true\n");
})().catch(error => {
  process.stderr.write("ATB_PHASE19C_SECURITY_QA FAIL " + error.message + "\n");
  process.exitCode = 1;
});
