import {createHash} from "node:crypto";

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
    try {
      json = JSON.parse(text);
    } catch {
      json = {raw: text};
    }
  }
  return {response, json, text};
}

async function postJson(url, body, headers = {}) {
  return requestJson(url, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...headers,
    },
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

async function getDoc(path) {
  return requestJson(docUrl(path));
}

async function remove(path) {
  await requestJson(
    docUrl(path),
    {method: "DELETE"},
  );
}

async function signup(label, nonce) {
  const response = await postJson(
    `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/` +
      `accounts:signUp?key=${API_KEY}`,
    {
      email: `atlasboard.steam.${label}.${nonce}@example.com`,
      password: `AtlasSteam!${nonce}${label}`,
      returnSecureToken: true,
    },
  );

  assert(
    response.response.ok,
    `Auth sign-up failed (${label}): ${response.text}`,
  );

  return {
    token: response.json?.idToken,
    uid: response.json?.localId,
  };
}

function eventId(uid, steamId, mode) {
  return createHash("sha256")
    .update(
      `atlasboard:platform-link:${uid}:${steamId}:${mode}`,
      "utf8",
    )
    .digest("hex");
}

console.log("Atlas Board Phase 11C Steam Account Link E2E");
console.log("Safety: local Auth/Firestore/Functions emulators only.");

const hub = await requestJson(`${HUB_BASE}/emulators`);
assert(hub.response.ok, "Emulator Hub unavailable.");

for (const name of ["auth", "firestore", "functions"]) {
  assert(hub.json?.[name], `Required emulator not running: ${name}`);
}

console.log("[1/10] PASS emulator safety preflight.");

const nonce =
  `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`;

const accountA = await signup("a", nonce);
const accountB = await signup("b", nonce);

assert(
  accountA.token &&
  accountA.uid &&
  accountB.token &&
  accountB.uid,
  "Auth emulator did not return both identities.",
);

console.log("[2/10] PASS two authenticated Atlas accounts.");

const status0 =
  await call(
    "platformSteamGetLinkStatus",
    accountA.token,
  );

assert(
  status0.response.ok &&
  result(status0)?.linked === false,
  `Initial Steam status mismatch: ${status0.text}`,
);

console.log("[3/10] PASS initial Steam link state is empty.");

const steamA =
  "765611980000" +
  String(Math.floor(Math.random() * 90000) + 10000);

const steamB =
  "765611981000" +
  String(Math.floor(Math.random() * 90000) + 10000);

const linkA =
  await call(
    "platformSteamDevLinkCurrentAccount",
    accountA.token,
    {
      steamId: steamA,
      appId: 480,
    },
  );

const linkedA = result(linkA);

assert(
  linkA.response.ok &&
  linkedA?.linked === true &&
  linkedA?.verified === false &&
  linkedA?.developmentOnly === true &&
  linkedA?.applied === true,
  `DEV Steam link failed: ${linkA.text}`,
);

console.log("[4/10] PASS emulator Steam identity linked once.");

const status1 =
  await call(
    "platformSteamGetLinkStatus",
    accountA.token,
  );

const statusA = result(status1);

assert(
  status1.response.ok &&
  statusA?.linked === true &&
  statusA?.steamId === steamA &&
  statusA?.verificationMode === "emulator_dev_proof",
  `Linked status did not restore: ${status1.text}`,
);

console.log("[5/10] PASS account link status restores canonically.");

const replay =
  await call(
    "platformSteamDevLinkCurrentAccount",
    accountA.token,
    {
      steamId: steamA,
      appId: 480,
    },
  );

assert(
  replay.response.ok &&
  result(replay)?.idempotentReplay === true &&
  result(replay)?.applied === false,
  `Steam link replay was not idempotent: ${replay.text}`,
);

console.log("[6/10] PASS same account/Steam replay is idempotent.");

const collision =
  await call(
    "platformSteamDevLinkCurrentAccount",
    accountB.token,
    {
      steamId: steamA,
      appId: 480,
    },
  );

assert(
  !collision.response.ok,
  "Second Atlas account incorrectly claimed an existing SteamID.",
);

console.log("[7/10] PASS SteamID cannot link to two Atlas accounts.");

const secondSteam =
  await call(
    "platformSteamDevLinkCurrentAccount",
    accountA.token,
    {
      steamId: steamB,
      appId: 480,
    },
  );

assert(
  !secondSteam.response.ok,
  "Atlas account incorrectly linked a second SteamID.",
);

console.log("[8/10] PASS Atlas account cannot link a second SteamID.");

const providerDoc =
  await getDoc(
    `platform_identity_links/steam_${steamA}`,
  );

const accountDoc =
  await getDoc(
    `account_platform_links/${accountA.uid}_steam`,
  );

assert(
  providerDoc.response.ok &&
  accountDoc.response.ok,
  "Bidirectional Steam link documents were not persisted.",
);

const providerFields =
  providerDoc.json?.fields ?? {};

const accountFields =
  accountDoc.json?.fields ?? {};

assert(
  providerFields.accountId?.stringValue === accountA.uid &&
  accountFields.providerUserId?.stringValue === steamA &&
  providerFields.verified?.booleanValue === false &&
  accountFields.developmentOnly?.booleanValue === true,
  "Steam link documents contain inconsistent authority state.",
);

console.log("[9/10] PASS bidirectional link mapping persisted safely.");

const auditId =
  eventId(
    accountA.uid,
    steamA,
    "emulator_dev_proof",
  );

const audit =
  await getDoc(
    `platform_link_events/${auditId}`,
  );

assert(
  audit.response.ok &&
  audit.json?.fields?.providerUserId?.stringValue === steamA &&
  audit.json?.fields?.proofHash?.stringValue &&
  !JSON.stringify(audit.json).toLowerCase().includes("ticket"),
  "Steam link audit evidence is missing or leaked raw ticket material.",
);

console.log("[10/10] PASS immutable link audit evidence persisted.");

await remove(
  `platform_link_events/${auditId}`,
);

await remove(
  `platform_identity_links/steam_${steamA}`,
);

await remove(
  `account_platform_links/${accountA.uid}_steam`,
);

console.log("Phase 11C Steam Account Link E2E PASS: 10 checks.");
console.log("Cleanup: temporary Phase 11C link fixtures removed.");
