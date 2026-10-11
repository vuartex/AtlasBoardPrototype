import {initializeApp} from "firebase-admin/app";
import {getAuth} from "firebase-admin/auth";
import {getFirestore, Timestamp} from "firebase-admin/firestore";

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
    {
      ...(token ? {Authorization: `Bearer ${token}`} : {}),
      ...(process.env.ATLAS_TEST_APP_CHECK_TOKEN ? {
        "X-Firebase-AppCheck": process.env.ATLAS_TEST_APP_CHECK_TOKEN,
      } : {}),
    },
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
  const outsider = await createUser("Outside Player");
  const genericRole = await createUser("Generic Role", {
    role: "moderator", moderator: true,
  });
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
  assert(!reportDoc.data()?.expiresAt,
    "Open reports must not have an automatic TTL.");
  assert(reportDoc.data()?.retentionPolicy === "open_case_hold_v1",
    "Open report hold policy is missing.");
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

  // Phase 15I security regression: authorization cannot be selected by client.
  for (const callable of ["lobbyChatSend", "lobbyChatList",
    "matchChatSend", "matchChatList"]) {
    const authless = await callFunction(callable, null, {scopeId: lobbyId});
    await expectError(authless, "chat.error.authentication_required",
      `Unauthenticated ${callable}`);
    passes++;
  }

  for (const [callable, scopeId] of [
    ["lobbyChatSend", lobbyId], ["lobbyChatList", lobbyId],
    ["matchChatSend", matchId], ["matchChatList", matchId],
  ]) {
    const denied = await callFunction(callable, outsider.token, {
      scopeId, body: "Outsider message", senderUtcOffsetMinutes: 0,
    });
    await expectError(denied, "chat.error.member_only",
      `Outsider ${callable}`);
    passes++;
  }

  const forbiddenActions = [
    ["lobbyChatSend", {operation: "moderator_set_sanction",
      targetAccountId: users.host.uid, action: "ban"}],
    ["matchChatSend", {operation: "moderator_set_sanction",
      targetAccountId: users.host.uid, action: "mute"}],
    ["lobbyChatSend", {operation: "moderator_update_config",
      blockExternalLinks: false}],
    ["lobbyChatSend", {operation: "moderator_remove_message",
      scopeId: lobbyId, messageId: hostMessageId}],
    ["lobbyChatList", {operation: "moderator_reports"}],
    ["matchChatList", {operation: "moderator_reports"}],
    ["lobbyChatSend", {operation: "moderator_review_report",
      reportId: result(messageReport).reportId, action: "dismiss"}],
  ];
  for (const [callable, payload] of forbiddenActions) {
    const denied = await callFunction(callable, users.guest.token, payload);
    await expectError(denied, "chat.error.moderator_required",
      `Normal-user privilege escalation ${callable}/${payload.operation}`);
    passes++;
  }
  const genericDenied = await callFunction("lobbyChatSend",
    genericRole.token, {
      operation: "moderator_set_sanction",
      targetAccountId: users.host.uid, action: "ban",
    });
  await expectError(genericDenied, "chat.error.moderator_required",
    "Generic unrelated role does not grant chat moderator power");
  passes++;

  const hostSanction = await db.collection("chat_sanctions")
    .doc(users.host.uid).get();
  assert(!hostSanction.exists,
    "Non-moderator requests managed to create a host sanction.");
  passes++;

  // Report targets must belong to the requested scope, not any other lobby.
  await clearReportRate(users.guest.uid);
  const badTarget = await callFunction("lobbyChatSend", users.guest.token, {
    operation: "report_player", scopeId: lobbyId,
    targetAccountId: outsider.uid, reasonCode: "spam",
  });
  await expectError(badTarget, "chat.error.target_not_found",
    "Report target outside lobby");
  passes++;

  const oldMemberRef = lobbyRef.collection("members").doc("seat-2");
  await oldMemberRef.update({active: false});
  const inactiveTarget = await callFunction("lobbyChatSend",
    users.guest.token, {
      operation: "report_player", scopeId: lobbyId,
      targetAccountId: users.moderator.uid, reasonCode: "spam",
    });
  await expectError(inactiveTarget, "chat.error.target_not_found",
    "Inactive lobby target");
  await oldMemberRef.update({active: true});
  passes++;

  await db.collection("matches").doc(matchId)
    .collection("seats").doc("seat-2")
    .update({connectionState: "kicked"});
  const kickedTarget = await callFunction("matchChatSend",
    users.guest.token, {
      operation: "report_player", scopeId: matchId,
      targetAccountId: users.moderator.uid, reasonCode: "spam",
    });
  await expectError(kickedTarget, "chat.error.target_not_found",
    "Kicked match target");
  await db.collection("matches").doc(matchId)
    .collection("seats").doc("seat-2")
    .update({connectionState: "connected"});
  passes++;

  await clearReportRate(users.guest.uid);
  const wrongScope = await callFunction("matchChatSend",
    users.guest.token, {
      operation: "report_message", scopeId: matchId,
      messageId: hostMessageId, reasonCode: "abuse",
    });
  await expectError(wrongScope, "chat.error.message_not_found",
    "Message id from different scope");
  passes++;

  const attemptedSpoof = await callFunction("lobbyChatSend",
    users.guest.token, {
      operation: "report_message", scopeId: lobbyId,
      messageId: hostMessageId, targetAccountId: users.guest.uid,
      reasonCode: "abuse",
    });
  assert(attemptedSpoof.response.ok,
    `Authoritative report target check failed: ${attemptedSpoof.text}`);
  const spoofedReport = await db.collection("chat_reports")
    .doc(result(attemptedSpoof).reportId).get();
  assert(spoofedReport.data()?.targetAccountId === users.host.uid,
    "Untrusted targetAccountId overrode stored message author.");
  passes++;

  // Moderator deletion must atomically preserve evidence in a backend audit.
  const audits = await db.collection("chat_moderation_audit")
    .where("moderatorAccountId", "==", users.moderator.uid).get();
  const removalAudit = audits.docs.find((d) =>
    d.data().action === "remove_message" &&
    d.data().details?.messageId === hostMessageId);
  assert(removalAudit, "No audit for moderator message removal.");
  assert(removalAudit.data().details.evidence.body === "Hello from host",
    "Deleted message body was not preserved in audit evidence.");
  assert(!removalAudit.data().expiresAt,
    "Moderation audit evidence must not have automatic TTL.");
  const erased = await lobbyRef.collection("chat_messages")
    .doc(hostMessageId).get();
  assert(erased.data()?.body === "" && erased.data()?.removed === true,
    "Removed message still has public body.");
  passes++;

  const repeatRemoval = await callFunction("lobbyChatSend",
    users.moderator.token, {
      operation: "moderator_remove_message", scopeId: lobbyId,
      messageId: hostMessageId, comment: "repeat removal",
    });
  assert(repeatRemoval.response.ok && result(repeatRemoval).alreadyRemoved,
    "Duplicate moderator removal should be idempotent.");
  const repeatAudits = await db.collection("chat_moderation_audit")
    .where("moderatorAccountId", "==", users.moderator.uid).get();
  assert(repeatAudits.docs.filter((d) =>
    d.data().action === "remove_message" &&
    d.data().details?.messageId === hostMessageId).length === 1,
  "Replay created an extra removal audit.");
  passes++;

  // Application-level expiration must deny reads even when TTL lags.
  const matchMessageId = result(matchSend)?.message?.messageId;
  assert(matchMessageId, "Expected match message for expiry test.");
  await db.collection("matches").doc(matchId)
    .collection("chat_messages").doc(matchMessageId)
    .update({expiresAt: Timestamp.fromMillis(Date.now() - 1000)});
  const expiredList = await callFunction("matchChatList", users.guest.token,
    {scopeId: matchId});
  assert(expiredList.response.ok &&
    !result(expiredList)?.messages?.some((m) => m.messageId === matchMessageId),
  "Expired message remained visible before TTL background cleanup.");
  passes++;

  await clearReportRate(users.guest.uid);
  const expiredReport = await callFunction("matchChatSend", users.guest.token, {
    operation: "report_message", scopeId: matchId,
    messageId: matchMessageId, reasonCode: "spam",
  });
  await expectError(expiredReport, "chat.error.message_not_found",
    "Expired message report");
  passes++;

  await clearReportRate(users.guest.uid);
  const repeatReport = await callFunction("lobbyChatSend", users.guest.token, {
    operation: "report_player", scopeId: lobbyId,
    targetAccountId: users.host.uid, reasonCode: "spam",
  });
  assert(repeatReport.response.ok, "First report for rate-check failed.");
  const tooFastReport = await callFunction("lobbyChatSend",
    users.guest.token, {
      operation: "report_player", scopeId: lobbyId,
      targetAccountId: users.host.uid, reasonCode: "spam",
    });
  await expectError(tooFastReport, "chat.error.report_rate_limited",
    "Report cooldown");
  passes++;

  await clearRate(lobbyRef, users.guest.uid);
  const newSend = await callFunction("lobbyChatSend", users.guest.token, {
    scopeId: lobbyId, body: "Cooldown control", senderUtcOffsetMinutes: 0,
  });
  assert(newSend.response.ok, `Cooldown baseline failed: ${newSend.text}`);
  const tooFastSend = await callFunction("lobbyChatSend",
    users.guest.token, {
      scopeId: lobbyId, body: "Cooldown control two",
        senderUtcOffsetMinutes: 0,
    });
  await expectError(tooFastSend, "chat.error.rate_limited",
    "Chat message cooldown");
  passes++;

  // Open reports stay on hold. Only moderator can close/reopen a report.
  const reviewId = result(messageReport).reportId;
  const resolveReport = await callFunction("lobbyChatSend",
    users.moderator.token, {
      operation: "moderator_review_report", reportId: reviewId,
      action: "resolve", comment: "Evidence reviewed",
    });
  assert(resolveReport.response.ok &&
    result(resolveReport)?.status === "resolved",
  `Moderator report resolution failed: ${resolveReport.text}`);
  let reviewed = await db.collection("chat_reports").doc(reviewId).get();
  assert(reviewed.data()?.expiresAt?.toMillis() > Date.now(),
    "Closed report did not receive a future retention deadline.");
  passes++;

  const reopen = await callFunction("lobbyChatSend", users.moderator.token,
    {operation: "moderator_review_report", reportId: reviewId,
      action: "reopen", comment: "Reopened for investigation"});
  assert(reopen.response.ok && result(reopen)?.status === "open",
    `Report reopening failed: ${reopen.text}`);
  reviewed = await db.collection("chat_reports").doc(reviewId).get();
  assert(!reviewed.data()?.expiresAt,
    "Reopened report still has TTL and could be destroyed prematurely.");
  passes++;

  const reviewAudit = await db.collection("chat_moderation_audit")
    .where("moderatorAccountId", "==", users.moderator.uid).get();
  assert(reviewAudit.docs.some((d) => d.data().action === "report_resolve" &&
    d.data().details.reportId === reviewId),
  "Report resolution audit missing.");
  assert(reviewAudit.docs.some((d) => d.data().action === "report_reopen" &&
    d.data().details.reportId === reviewId),
  "Report reopening audit missing.");
  passes++;

  console.log(`Phase 15I combined chat/moderation E2E PASS: ${passes} checks.`);
  console.log("Security, authorization, evidence, spam and lifecycle verified.");
}

main().catch((error) => {
  console.error("Chat moderation E2E FAILED:", error);
  process.exitCode = 1;
});
