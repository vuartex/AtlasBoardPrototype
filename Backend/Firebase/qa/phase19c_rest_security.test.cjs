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
const frames = () => ({frame_halloween: false, frame_new_year: false,
  frame_turkiye: false, frame_usa: false});
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

  process.stdout.write("ATB_PHASE19F_SECURITY_QA PASS assertions=" +
    passed + " guestDenied=true tamperDenied=true purchaseReplayDenied=true\n");
})().catch(error => {
  process.stderr.write("ATB_PHASE19C_SECURITY_QA FAIL " + error.message + "\n");
  process.exitCode = 1;
});
