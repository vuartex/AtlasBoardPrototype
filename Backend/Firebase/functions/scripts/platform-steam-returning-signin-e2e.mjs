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

async function call(name, data = {}, token = "") {
  const headers = {};

  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  return postJson(
    callableUrl(name),
    {data},
    headers,
  );
}

function result(callable) {
  return callable.json?.result ?? callable.json?.data;
}

async function signup(label, nonce) {
  const response = await postJson(
    `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/` +
      `accounts:signUp?key=${API_KEY}`,
    {
      email: `atlasboard.return.${label}.${nonce}@example.com`,
      password: `AtlasReturn!${label}!${nonce}`,
      returnSecureToken: true,
    },
  );

  assert(
    response.response.ok,
    `Auth signup failed (${label}): ${response.text}`,
  );

  return {
    token: response.json?.idToken,
    uid: response.json?.localId,
  };
}

async function signInWithCustomToken(customToken) {
  return postJson(
    `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/` +
      `accounts:signInWithCustomToken?key=${API_KEY}`,
    {
      token: customToken,
      returnSecureToken: true,
    },
  );
}

function decodeCustomTokenUid(token) {
  const parts = token.split(".");
  assert(parts.length === 3, "Custom token is not a JWT.");

  const payload =
    JSON.parse(
      Buffer.from(
        parts[1],
        "base64url",
      ).toString("utf8"),
    );

  return payload.uid;
}

function decodeFirebaseIdTokenUid(token) {
  const parts = token.split(".");
  assert(
    parts.length === 3,
    "Firebase ID token is not a JWT.",
  );

  const payload =
    JSON.parse(
      Buffer.from(
        parts[1],
        "base64url",
      ).toString("utf8"),
    );

  return payload.user_id ?? payload.sub ?? "";
}

function docUrl(path) {
  return `${FIRESTORE_BASE}/v1/projects/${PROJECT_ID}` +
    `/databases/(default)/documents/${path}`;
}

async function remove(path) {
  await requestJson(
    docUrl(path),
    {method: "DELETE"},
  );
}

console.log("Atlas Board Phase 11D Returning Steam Sign-In E2E");
console.log("Safety: local Auth/Firestore/Functions emulators only.");

const hub =
  await requestJson(
    `${HUB_BASE}/emulators`,
  );

assert(
  hub.response.ok,
  "Emulator Hub unavailable.",
);

for (const name of [
  "auth",
  "firestore",
  "functions",
]) {
  assert(
    hub.json?.[name],
    `Required emulator not running: ${name}`,
  );
}

console.log("[1/10] PASS emulator safety preflight.");

const nonce =
  `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`;

const account =
  await signup(
    "linked",
    nonce,
  );

assert(
  account.token &&
  account.uid,
  "Auth emulator did not return the linked account.",
);

console.log("[2/10] PASS canonical Atlas account created.");

const steamId =
  "765611982000" +
  String(
    Math.floor(
      Math.random() * 90000,
    ) + 10000,
  );

const link =
  await call(
    "platformSteamDevLinkCurrentAccount",
    {
      steamId,
      appId: 480,
    },
    account.token,
  );

assert(
  link.response.ok &&
  result(link)?.linked === true,
  `Steam dev link failed: ${link.text}`,
);

console.log("[3/10] PASS Steam identity linked to Atlas account.");

const returning =
  await call(
    "platformSteamDevReturningSignIn",
    {
      steamId,
    },
  );

const returningResult =
  result(returning);

assert(
  returning.response.ok &&
  returningResult?.ok === true &&
  returningResult?.accountId === account.uid &&
  returningResult?.steamId === steamId &&
  returningResult?.developmentOnly === true &&
  returningResult?.customToken,
  `Returning sign-in token failed: ${returning.text}`,
);

console.log("[4/10] PASS unauthenticated returning lookup recovered account.");

assert(
  decodeCustomTokenUid(
    returningResult.customToken,
  ) === account.uid,
  "Custom token UID does not match the linked Atlas account.",
);

console.log("[5/10] PASS custom token targets the canonical Atlas UID.");

const authSignIn =
  await signInWithCustomToken(
    returningResult.customToken,
  );

assert(
  authSignIn.response.ok &&
  authSignIn.json?.idToken &&
  decodeFirebaseIdTokenUid(
    authSignIn.json.idToken,
  ) === account.uid,
  `Auth emulator custom-token sign-in failed: ${authSignIn.text}`,
);

console.log("[6/10] PASS Firebase Auth restored exact linked UID.");

const restoredStatus =
  await call(
    "platformSteamGetLinkStatus",
    {},
    authSignIn.json.idToken,
  );

assert(
  restoredStatus.response.ok &&
  result(restoredStatus)?.linked === true &&
  result(restoredStatus)?.accountId === account.uid &&
  result(restoredStatus)?.steamId === steamId,
  `Recovered session did not restore link authority: ${restoredStatus.text}`,
);

console.log("[7/10] PASS recovered ID token restores linked authority.");

const replay =
  await call(
    "platformSteamDevReturningSignIn",
    {
      steamId,
    },
  );

assert(
  replay.response.ok &&
  result(replay)?.accountId === account.uid &&
  result(replay)?.customToken,
  `Returning sign-in replay failed: ${replay.text}`,
);

console.log("[8/10] PASS returning lookup is replay-safe and non-mutating.");

const unknownSteamId =
  "76561198999999999";

const missing =
  await call(
    "platformSteamDevReturningSignIn",
    {
      steamId: unknownSteamId,
    },
  );

assert(
  !missing.response.ok,
  "Unknown SteamID incorrectly received an Atlas custom token.",
);

console.log("[9/10] PASS unknown SteamID cannot recover an account.");

const providerDoc =
  await requestJson(
    docUrl(
      `platform_identity_links/steam_${steamId}`,
    ),
  );

assert(
  providerDoc.response.ok &&
  providerDoc.json?.fields?.accountId?.stringValue === account.uid &&
  providerDoc.json?.fields?.developmentOnly?.booleanValue === true,
  "Returning sign-in modified the canonical mapping unexpectedly.",
);

console.log("[10/10] PASS canonical link mapping remains unchanged.");

await remove(
  `platform_identity_links/steam_${steamId}`,
);

await remove(
  `account_platform_links/${account.uid}_steam`,
);

console.log("Phase 11D Returning Steam Sign-In E2E PASS: 10 checks.");
console.log("Cleanup: temporary Phase 11D link fixtures removed.");
