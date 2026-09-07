import {initializeApp} from "firebase-admin/app";
import {getAuth} from "firebase-admin/auth";
import {getFirestore} from "firebase-admin/firestore";

const PROJECT_ID = "atlasboard-usa";
const REGION = "europe-west1";
const AUTH_BASE = "http://127.0.0.1:9099";
const FUNCTIONS_BASE = "http://127.0.0.1:5001";
const HUB_BASE = "http://127.0.0.1:4400";
const API_KEY = "atlasboard-local-emulator-only";
const TIMEOUT_MS = 15000;

process.env.FIREBASE_AUTH_EMULATOR_HOST = "127.0.0.1:9099";
process.env.FIRESTORE_EMULATOR_HOST = "127.0.0.1:8080";

const app = initializeApp({projectId: PROJECT_ID});
const auth = getAuth(app);
const db = getFirestore(app);

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
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

function callableUrl(functionName) {
  return `${FUNCTIONS_BASE}/${PROJECT_ID}/${REGION}/${functionName}`;
}

async function callFunction(functionName, token, data) {
  return postJson(
    callableUrl(functionName),
    {data},
    {Authorization: `Bearer ${token}`},
  );
}

function result(call) {
  return call.json?.result ?? call.json?.data;
}

async function expectError(call, errorKey, label) {
  assert(!call.response.ok, `${label} unexpectedly succeeded.`);
  const actual = call.json?.error?.details?.errorKey;
  assert(
    actual === errorKey,
    `${label}: expected ${errorKey}, got ${actual}. ${call.text}`,
  );
}

async function verifyEmulators() {
  const hub = await requestJson(`${HUB_BASE}/emulators`);
  assert(hub.response.ok, "Emulator Hub is not reachable.");

  const emulators = hub.json ?? {};
  for (const name of ["auth", "functions", "firestore"]) {
    assert(emulators[name], `Required emulator not running: ${name}.`);
  }
}

async function createUser(label, claims = {}) {
  const nonce = `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`;
  const user = await auth.createUser({
    email: `atlas.chat.${label}.${nonce}@example.com`,
    displayName: label,
  });

  if (Object.keys(claims).length > 0) {
    await auth.setCustomUserClaims(user.uid, claims);
  }

  const customToken = await auth.createCustomToken(user.uid);
  const exchange = await postJson(
    `${AUTH_BASE}/identitytoolkit.googleapis.com/v1/` +
      `accounts:signInWithCustomToken?key=${API_KEY}`,
    {
      token: customToken,
      returnSecureToken: true,
    },
  );

  assert(exchange.response.ok, `Token exchange failed for ${label}.`);
  await db.collection("public_profiles").doc(user.uid).set({
    displayName: label,
    schemaVersion: 1,
  });

  return {
    uid: user.uid,
    token: exchange.json?.idToken,
    label,
  };
}

async function seedScope(lobbyId, matchId, users) {
  await db.collection("lobbies").doc(lobbyId).set({
    hostAccountId: users.host.uid,
    lifecycleState: "waiting",
  });

  let slot = 0;
  for (const user of Object.values(users)) {
    await db.collection("lobbies")
      .doc(lobbyId)
      .collection("members")
      .doc(`seat-${slot}`)
      .set({
        active: true,
        accountId: user.uid,
        localOwnerAccountId: user.uid,
        displayName: user.label,
      });
    slot++;
  }

  await db.collection("matches").doc(matchId).set({
    hostAccountId: users.host.uid,
    lifecycleState: "in_match",
  });

  slot = 0;
  for (const user of Object.values(users)) {
    await db.collection("matches")
      .doc(matchId)
      .collection("seats")
      .doc(`seat-${slot}`)
      .set({
        active: true,
        accountId: user.uid,
        localOwnerAccountId: user.uid,
        displayName: user.label,
        connectionState: "connected",
      });
    slot++;
  }
}

async function clearRate(scope, uid) {
  await scope.collection("chat_rate_limits").doc(uid).delete();
}

async function ageRate(scope, uid) {
  await scope.collection("chat_rate_limits").doc(uid).set(
    {lastSentAtEpochMs: Date.now() - 11000},
    {merge: true},
  );
}

async function clearReportRate(uid) {
  await db.collection("chat_report_rate_limits").doc(uid).delete();
}

async function main() {
  await verifyEmulators();

  const nonce = `${Date.now()}-${Math.floor(Math.random() * 100000)}`;
  const lobbyId = `chat-lobby-${nonce}`;
  const matchId = `chat-match-${nonce}`;
  const users = {
    host: await createUser("Host Player"),
    guest: await createUser("Guest Player"),
    moderator: await createUser(
      "Chat Moderator",
      {atlasChatModerator: true},
    ),
  };

  await seedScope(lobbyId, matchId, users);
  const lobbyRef = db.collection("lobbies").doc(lobbyId);
  let passes = 0;

  const hostSend = await callFunction(
    "lobbyChatSend",
    users.host.token,
    {
      scopeId: lobbyId,
      body: "Hello from host",
      senderUtcOffsetMinutes: -360,
    },
  );
  assert(hostSend.response.ok, `Baseline send failed: ${hostSend.text}`);
  const hostMessageId = result(hostSend)?.message?.messageId;
  assert(hostMessageId, "Baseline send did not return a message id.");
  passes++;

  const storedMessage = await lobbyRef
    .collection("chat_messages")
    .doc(hostMessageId)
    .get();
  assert(storedMessage.data()?.expiresAt, "Message TTL field is missing.");
  passes++;

  const guestList = await callFunction(
    "lobbyChatList",
    users.guest.token,
    {scopeId: lobbyId},
  );
  assert(guestList.response.ok, "Guest list failed.");
  assert(
    result(guestList)?.messages?.some(
      (item) => item.messageId === hostMessageId,
    ),
    "Guest did not receive the baseline host message.",
  );
  passes++;

  await clearRate(lobbyRef, users.guest.uid);
  const profanity = await callFunction(
    "lobbyChatSend",
    users.guest.token,
    {
      scopeId: lobbyId,
      body: "f.u.c.k",
      senderUtcOffsetMinutes: 180,
    },
  );
  await expectError(
    profanity,
    "chat.error.blocked_language",
    "Obfuscated profanity",
  );
  passes++;

  await clearRate(lobbyRef, users.guest.uid);
  const urlSpam = await callFunction(
    "lobbyChatSend",
    users.guest.token,
    {
      scopeId: lobbyId,
      body: "join discord.gg/atlasboard",
      senderUtcOffsetMinutes: 180,
    },
  );
  await expectError(
    urlSpam,
    "chat.error.external_link_blocked",
    "External invite",
  );
  passes++;

  const configUpdate = await callFunction(
    "lobbyChatSend",
    users.moderator.token,
    {
      operation: "moderator_update_config",
      scopeId: lobbyId,
      extraBlockedTerms: ["atlasbadword"],
      blockExternalLinks: true,
      messageRetentionDays: 30,
      reportRetentionDays: 180,
      maxBurstPerMinute: 5,
      maxDuplicateRepeats: 2,
      duplicateWindowSeconds: 120,
    },
  );
  assert(configUpdate.response.ok, "Moderator config update failed.");
  passes++;

  await clearRate(lobbyRef, users.guest.uid);
  const dynamicProfanity = await callFunction(
    "lobbyChatSend",
    users.guest.token,
    {
      scopeId: lobbyId,
      body: "atlasbadword",
      senderUtcOffsetMinutes: 180,
    },
  );
  await expectError(
    dynamicProfanity,
    "chat.error.blocked_language",
    "Dynamic profanity",
  );
  passes++;

  await clearRate(lobbyRef, users.guest.uid);
  const firstDuplicate = await callFunction(
    "lobbyChatSend",
    users.guest.token,
    {
      scopeId: lobbyId,
      body: "repeat me",
      senderUtcOffsetMinutes: 180,
    },
  );
  assert(firstDuplicate.response.ok, "First duplicate send failed.");
  await ageRate(lobbyRef, users.guest.uid);

  const secondDuplicate = await callFunction(
    "lobbyChatSend",
    users.guest.token,
    {
      scopeId: lobbyId,
      body: "repeat me",
      senderUtcOffsetMinutes: 180,
    },
  );
  assert(secondDuplicate.response.ok, "Second duplicate send failed.");
  await ageRate(lobbyRef, users.guest.uid);

  const thirdDuplicate = await callFunction(
    "lobbyChatSend",
    users.guest.token,
    {
      scopeId: lobbyId,
      body: "repeat me",
      senderUtcOffsetMinutes: 180,
    },
  );
  await expectError(
    thirdDuplicate,
    "chat.error.duplicate_spam",
    "Duplicate spam",
  );
  passes++;

  const muteHost = await callFunction(
    "lobbyChatSend",
    users.guest.token,
    {
      operation: "set_muted",
      scopeId: lobbyId,
      targetAccountId: users.host.uid,
      muted: true,
    },
  );
  assert(muteHost.response.ok, "Mute operation failed.");

  const mutedList = await callFunction(
    "lobbyChatList",
    users.guest.token,
    {scopeId: lobbyId},
  );
  assert(mutedList.response.ok, "Muted list failed.");
  assert(
    !result(mutedList)?.messages?.some(
      (item) => item.accountId === users.host.uid,
    ),
    "Muted host messages are still visible.",
  );
  passes++;

  await callFunction(
    "lobbyChatSend",
    users.guest.token,
    {
      operation: "set_muted",
      scopeId: lobbyId,
      targetAccountId: users.host.uid,
      muted: false,
    },
  );

  const blockHost = await callFunction(
    "lobbyChatSend",
    users.guest.token,
    {
      operation: "set_blocked",
      scopeId: lobbyId,
      targetAccountId: users.host.uid,
      muted: true,
    },
  );
  assert(blockHost.response.ok, "Block operation failed.");

  const blockedList = await callFunction(
    "lobbyChatList",
    users.guest.token,
    {scopeId: lobbyId},
  );
  assert(blockedList.response.ok, "Blocked list failed.");
  assert(
    !result(blockedList)?.messages?.some(
      (item) => item.accountId === users.host.uid,
    ),
    "Blocked host messages are still visible.",
  );
  passes++;

  await callFunction(
    "lobbyChatSend",
    users.guest.token,
    {
      operation: "set_blocked",
      scopeId: lobbyId,
      targetAccountId: users.host.uid,
      muted: false,
    },
  );

  await clearReportRate(users.guest.uid);
  const playerReport = await callFunction(
    "lobbyChatSend",
    users.guest.token,
    {
      operation: "report_player",
      scopeId: lobbyId,
      targetAccountId: users.host.uid,
      reasonCode: "harassment",
      comment: "E2E player report",
    },
  );
  assert(playerReport.response.ok, "Player report failed.");
  passes++;

  await clearReportRate(users.guest.uid);
  const messageReport = await callFunction(
    "lobbyChatSend",
    users.guest.token,
    {
      operation: "report_message",
      scopeId: lobbyId,
      messageId: hostMessageId,
      reasonCode: "abuse",
      comment: "E2E message report",
    },
  );
  assert(messageReport.response.ok, "Message report failed.");
  const reportDoc = await db.collection("chat_reports")
    .doc(result(messageReport)?.reportId)
    .get();
  assert(reportDoc.data()?.expiresAt, "Report TTL field is missing.");
  assert(
    Array.isArray(reportDoc.data()?.contextMessages) &&
      reportDoc.data().contextMessages.length > 0,
    "Report server-side chat context snapshot is missing.",
  );
  assert(
    reportDoc.data().contextMessages.some(
      (item) => item.messageId === hostMessageId,
    ),
    "Report context snapshot does not contain the reported message.",
  );
  passes++;

  const guestSafety = await callFunction(
    "lobbyChatList",
    users.guest.token,
    {
      operation: "safety_state",
      scopeId: lobbyId,
    },
  );
  assert(guestSafety.response.ok, "Guest safety state failed.");
  assert(
    result(guestSafety)?.canModerate === false,
    "Normal user unexpectedly has moderator authority.",
  );

  const modSafety = await callFunction(
    "lobbyChatList",
    users.moderator.token,
    {
      operation: "safety_state",
      scopeId: lobbyId,
    },
  );
  assert(modSafety.response.ok, "Moderator safety state failed.");
  assert(
    result(modSafety)?.canModerate === true,
    "Moderator claim was not recognized.",
  );
  passes++;

  const muteGuest = await callFunction(
    "lobbyChatSend",
    users.moderator.token,
    {
      operation: "moderator_set_sanction",
      scopeId: lobbyId,
      targetAccountId: users.guest.uid,
      action: "mute",
      durationMinutes: 10,
      comment: "E2E mute",
    },
  );
  assert(muteGuest.response.ok, "Moderator mute failed.");
  await clearRate(lobbyRef, users.guest.uid);

  const mutedSend = await callFunction(
    "lobbyChatSend",
    users.guest.token,
    {
      scopeId: lobbyId,
      body: "This should be muted",
      senderUtcOffsetMinutes: 180,
    },
  );
  await expectError(mutedSend, "chat.error.muted", "Moderator mute gate");
  passes++;

  await callFunction(
    "lobbyChatSend",
    users.moderator.token,
    {
      operation: "moderator_set_sanction",
      scopeId: lobbyId,
      targetAccountId: users.guest.uid,
      action: "unmute",
      comment: "E2E unmute",
    },
  );

  const disableGuest = await callFunction(
    "lobbyChatSend",
    users.moderator.token,
    {
      operation: "moderator_set_sanction",
      scopeId: lobbyId,
      targetAccountId: users.guest.uid,
      action: "disable",
      comment: "E2E parental hook",
    },
  );
  assert(disableGuest.response.ok, "Chat disable failed.");

  const disabledList = await callFunction(
    "lobbyChatList",
    users.guest.token,
    {scopeId: lobbyId},
  );
  await expectError(
    disabledList,
    "chat.error.disabled",
    "Parental/ops chat disable hook",
  );
  passes++;

  await callFunction(
    "lobbyChatSend",
    users.moderator.token,
    {
      operation: "moderator_set_sanction",
      scopeId: lobbyId,
      targetAccountId: users.guest.uid,
      action: "enable",
      comment: "E2E enable",
    },
  );

  const removeMessage = await callFunction(
    "lobbyChatSend",
    users.moderator.token,
    {
      operation: "moderator_remove_message",
      scopeId: lobbyId,
      messageId: hostMessageId,
      comment: "E2E removal",
    },
  );
  assert(removeMessage.response.ok, "Moderator removal failed.");

  const afterRemoval = await callFunction(
    "lobbyChatList",
    users.guest.token,
    {scopeId: lobbyId},
  );
  assert(afterRemoval.response.ok, "List after removal failed.");
  assert(
    !result(afterRemoval)?.messages?.some(
      (item) => item.messageId === hostMessageId,
    ),
    "Removed message is still visible.",
  );
  passes++;

  const modReports = await callFunction(
    "lobbyChatList",
    users.moderator.token,
    {
      operation: "moderator_reports",
      scopeId: lobbyId,
      limit: 10,
    },
  );
  assert(modReports.response.ok, "Moderator report list failed.");
  assert(
    (result(modReports)?.reports?.length ?? 0) >= 2,
    "Moderator report list did not contain E2E reports.",
  );
  passes++;

  await clearRate(
    db.collection("matches").doc(matchId),
    users.host.uid,
  );
  const matchSend = await callFunction(
    "matchChatSend",
    users.host.token,
    {
      scopeId: matchId,
      body: "Match moderation route works",
      senderUtcOffsetMinutes: -360,
    },
  );
  assert(matchSend.response.ok, "Match chat moderation route failed.");
  passes++;

  console.log(`Chat moderation E2E PASS: ${passes} checks.`);
  console.log(
    "Phase 6B.1/6B.2/6B.3 backend acceptance + report context passed.",
  );
}

main().catch((error) => {
  console.error("Chat moderation E2E FAILED:", error);
  process.exitCode = 1;
});
