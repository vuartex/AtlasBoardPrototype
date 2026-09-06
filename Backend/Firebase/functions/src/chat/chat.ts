import {FieldValue, getFirestore} from "firebase-admin/firestore";
import {HttpsError, onCall} from "firebase-functions/v2/https";
import {containsBlockedProfanity} from "./profanity";

const REGION = "europe-west1";
const CHAT_SCHEMA_VERSION = 1;
const MAX_MESSAGE_CODEPOINTS = 120;
const SEND_COOLDOWN_MS = 10_000;
const MAX_LIST_MESSAGES = 50;

type ChatScopeType = "lobby" | "match";

interface ChatMemberContext {
  scopeType: ChatScopeType;
  scopeId: string;
  displayName: string;
  rootRef: FirebaseFirestore.DocumentReference;
}

interface ChatSendData {
  scopeId?: unknown;
  body?: unknown;
  senderUtcOffsetMinutes?: unknown;
}

interface ChatListData {
  scopeId?: unknown;
  afterEpochMs?: unknown;
}

/**
 * Throws a stable callable chat error.
 * @param {string} code Firebase callable error code.
 * @param {string} technical Stable technical error identifier.
 * @param {string} errorKey Localized client error key.
 * @param {Record<string, unknown>} extra Optional structured error data.
 */
function error(
  code: "invalid-argument" | "not-found" | "permission-denied" |
    "failed-precondition" | "resource-exhausted" | "unauthenticated",
  technical: string,
  errorKey: string,
  extra?: Record<string, unknown>,
): never {
  throw new HttpsError(
    code,
    technical,
    {
      errorKey,
      ...(extra ?? {}),
    },
  );
}

/**
 * Returns the authenticated UID for a chat callable.
 * @param {Object} request Minimal callable authentication shape.
 * @return {string} Authenticated account id.
 */
function requireUid(request: {auth?: {uid: string}}): string {
  if (!request.auth || !request.auth.uid) {
    error(
      "unauthenticated",
      "CHAT_AUTH_REQUIRED",
      "chat.error.authentication_required",
    );
  }

  return request.auth.uid;
}

/**
 * Validates a lobby or match id supplied to chat.
 * @param {unknown} value Candidate scope id.
 * @return {string} Validated scope id.
 */
function readScopeId(value: unknown): string {
  if (
    typeof value !== "string" ||
    value.length < 8 ||
    value.length > 128
  ) {
    error(
      "invalid-argument",
      "INVALID_CHAT_SCOPE",
      "chat.error.invalid_scope",
    );
  }

  return value;
}

/**
 * Validates an optional chat pagination cursor.
 * @param {unknown} value Candidate epoch-millisecond cursor.
 * @return {number} Validated cursor or zero when omitted.
 */
function readAfterEpochMs(value: unknown): number {
  if (value === undefined || value === null) return 0;

  if (
    typeof value !== "number" ||
    !Number.isSafeInteger(value) ||
    value < 0
  ) {
    error(
      "invalid-argument",
      "INVALID_CHAT_CURSOR",
      "chat.error.invalid_request",
    );
  }

  return value;
}

/**
 * Validates the sender UTC offset used for local-time rendering.
 * @param {unknown} value Candidate UTC offset in minutes.
 * @return {number} Validated UTC offset in minutes.
 */
function readUtcOffsetMinutes(value: unknown): number {
  if (
    typeof value !== "number" ||
    !Number.isSafeInteger(value) ||
    value < -14 * 60 ||
    value > 14 * 60
  ) {
    error(
      "invalid-argument",
      "INVALID_CHAT_TIMEZONE_OFFSET",
      "chat.error.invalid_timezone",
    );
  }

  return value;
}

/**
 * Sanitizes and validates one outgoing chat message body.
 * @param {unknown} value Candidate message body.
 * @return {string} Sanitized message body.
 */
function readMessageBody(value: unknown): string {
  if (typeof value !== "string") {
    error(
      "invalid-argument",
      "INVALID_CHAT_MESSAGE",
      "chat.error.invalid_message",
    );
  }

  const body = value
    .replace(/[\r\n\t]+/g, " ")
    .trim();

  if (body.length === 0) {
    error(
      "invalid-argument",
      "EMPTY_CHAT_MESSAGE",
      "chat.error.empty_message",
    );
  }

  const codePointLength = Array.from(body).length;
  if (codePointLength > MAX_MESSAGE_CODEPOINTS) {
    error(
      "invalid-argument",
      "CHAT_MESSAGE_TOO_LONG",
      "chat.error.message_too_long",
      {
        maxLength: MAX_MESSAGE_CODEPOINTS,
      },
    );
  }

  if (containsBlockedProfanity(body)) {
    error(
      "failed-precondition",
      "CHAT_PROFANITY_BLOCKED",
      "chat.error.blocked_language",
    );
  }

  return body;
}

/**
 * Converts stored display-name data into a safe chat label.
 * @param {unknown} value Candidate display name.
 * @return {string} Safe display name with a Player fallback.
 */
function safeDisplayName(value: unknown): string {
  if (typeof value !== "string") return "Player";
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed.slice(0, 40) : "Player";
}

/**
 * Resolves lobby membership and display identity for chat access.
 * @param {string} uid Authenticated account id.
 * @param {string} lobbyId Lobby id.
 * @return {Promise<ChatMemberContext>} Resolved lobby member context.
 */
async function readLobbyMemberContext(
  uid: string,
  lobbyId: string,
): Promise<ChatMemberContext> {
  const db = getFirestore();
  const lobbyRef = db.collection("lobbies").doc(lobbyId);
  const [lobbySnap, membersSnap] = await Promise.all([
    lobbyRef.get(),
    lobbyRef.collection("members").get(),
  ]);

  if (!lobbySnap.exists) {
    error(
      "not-found",
      "CHAT_LOBBY_NOT_FOUND",
      "chat.error.scope_not_found",
    );
  }

  const lobby = lobbySnap.data() ?? {};
  const hostAccountId =
    typeof lobby.hostAccountId === "string" ? lobby.hostAccountId : "";

  let displayName = "";
  let memberFound = hostAccountId === uid;

  for (const member of membersSnap.docs) {
    const data = member.data();
    if (data.active !== true) continue;

    const accountId =
      typeof data.accountId === "string" ? data.accountId : "";
    const localOwnerAccountId =
      typeof data.localOwnerAccountId === "string" ?
        data.localOwnerAccountId : "";

    if (accountId === uid || localOwnerAccountId === uid) {
      memberFound = true;
      displayName = safeDisplayName(data.displayName);
      break;
    }
  }

  if (!memberFound) {
    error(
      "permission-denied",
      "CHAT_LOBBY_MEMBER_ONLY",
      "chat.error.member_only",
    );
  }

  if (!displayName) {
    const profile = await db.collection("public_profiles").doc(uid).get();
    displayName = safeDisplayName(profile.data()?.displayName);
  }

  return {
    scopeType: "lobby",
    scopeId: lobbyId,
    displayName,
    rootRef: lobbyRef,
  };
}

/**
 * Resolves match membership and display identity for chat access.
 * @param {string} uid Authenticated account id.
 * @param {string} matchId Match id.
 * @return {Promise<ChatMemberContext>} Resolved match member context.
 */
async function readMatchMemberContext(
  uid: string,
  matchId: string,
): Promise<ChatMemberContext> {
  const db = getFirestore();
  const matchRef = db.collection("matches").doc(matchId);
  const [matchSnap, seatsSnap] = await Promise.all([
    matchRef.get(),
    matchRef.collection("seats").get(),
  ]);

  if (!matchSnap.exists) {
    error(
      "not-found",
      "CHAT_MATCH_NOT_FOUND",
      "chat.error.scope_not_found",
    );
  }

  const match = matchSnap.data() ?? {};
  const hostAccountId =
    typeof match.hostAccountId === "string" ? match.hostAccountId : "";

  let displayName = "";
  let memberFound = hostAccountId === uid;

  for (const seat of seatsSnap.docs) {
    const data = seat.data();
    const accountId =
      typeof data.accountId === "string" ? data.accountId : "";
    const localOwnerAccountId =
      typeof data.localOwnerAccountId === "string" ?
        data.localOwnerAccountId : "";
    const connectionState =
      typeof data.connectionState === "string" ? data.connectionState : "";

    if (
      accountId === uid ||
      localOwnerAccountId === uid
    ) {
      if (
        connectionState === "kicked" ||
        connectionState === "afk_removed"
      ) {
        error(
          "permission-denied",
          "CHAT_MATCH_ACCESS_REMOVED",
          "chat.error.member_only",
        );
      }

      memberFound = true;
      displayName = safeDisplayName(data.displayName);
      break;
    }
  }

  if (!memberFound) {
    error(
      "permission-denied",
      "CHAT_MATCH_MEMBER_ONLY",
      "chat.error.member_only",
    );
  }

  if (!displayName) {
    const profile = await db.collection("public_profiles").doc(uid).get();
    displayName = safeDisplayName(profile.data()?.displayName);
  }

  return {
    scopeType: "match",
    scopeId: matchId,
    displayName,
    rootRef: matchRef,
  };
}

/**
 * Resolves chat membership for the requested scope type.
 * @param {ChatScopeType} scopeType Lobby or match chat scope.
 * @param {string} uid Authenticated account id.
 * @param {string} scopeId Lobby or match id.
 * @return {Promise<ChatMemberContext>} Resolved member context.
 */
async function readContext(
  scopeType: ChatScopeType,
  uid: string,
  scopeId: string,
): Promise<ChatMemberContext> {
  return scopeType === "lobby" ?
    readLobbyMemberContext(uid, scopeId) :
    readMatchMemberContext(uid, scopeId);
}

/**
 * Projects a stored chat document into the client response shape.
 * @param {FirebaseFirestore.DocumentSnapshot} doc Chat document snapshot.
 * @return {Record<string, unknown>} Stable client message payload.
 */
function projectMessage(
  doc:
    FirebaseFirestore.QueryDocumentSnapshot |
    FirebaseFirestore.DocumentSnapshot,
): Record<string, unknown> {
  const data = doc.data() ?? {};

  return {
    messageId: doc.id,
    accountId:
      typeof data.accountId === "string" ? data.accountId : "",
    displayName: safeDisplayName(data.displayName),
    body:
      typeof data.body === "string" ? data.body : "",
    createdAtEpochMs:
      typeof data.createdAtEpochMs === "number" ? data.createdAtEpochMs : 0,
    senderUtcOffsetMinutes:
      typeof data.senderUtcOffsetMinutes === "number" ?
        data.senderUtcOffsetMinutes : 0,
    schemaVersion:
      typeof data.schemaVersion === "number" ?
        data.schemaVersion : CHAT_SCHEMA_VERSION,
  };
}

/**
 * Validates and persists one chat message with rate limiting.
 * @param {ChatScopeType} scopeType Lobby or match chat scope.
 * @param {string} uid Authenticated account id.
 * @param {ChatSendData} data Callable request data.
 * @return {Promise<Record<string, unknown>>} Created message response.
 */
async function sendMessage(
  scopeType: ChatScopeType,
  uid: string,
  data: ChatSendData,
): Promise<Record<string, unknown>> {
  const scopeId = readScopeId(data.scopeId);
  const body = readMessageBody(data.body);
  const senderUtcOffsetMinutes =
    readUtcOffsetMinutes(data.senderUtcOffsetMinutes);
  const context = await readContext(scopeType, uid, scopeId);

  const db = getFirestore();
  const rateRef =
    context.rootRef.collection("chat_rate_limits").doc(uid);
  const metaRef =
    context.rootRef.collection("chat_meta").doc("state");
  const messageRef =
    context.rootRef.collection("chat_messages").doc();

  let committedEpochMs = 0;

  await db.runTransaction(async (transaction) => {
    const [rateSnap, metaSnap] = await Promise.all([
      transaction.get(rateRef),
      transaction.get(metaRef),
    ]);

    const nowEpochMs = Date.now();
    const lastSentAtEpochMs =
      typeof rateSnap.data()?.lastSentAtEpochMs === "number" ?
        rateSnap.data()?.lastSentAtEpochMs as number : 0;

    const elapsed = nowEpochMs - lastSentAtEpochMs;
    if (lastSentAtEpochMs > 0 && elapsed < SEND_COOLDOWN_MS) {
      error(
        "resource-exhausted",
        "CHAT_RATE_LIMIT",
        "chat.error.rate_limited",
        {
          retryAfterMs: SEND_COOLDOWN_MS - elapsed,
        },
      );
    }

    const lastMessageEpochMs =
      typeof metaSnap.data()?.lastMessageEpochMs === "number" ?
        metaSnap.data()?.lastMessageEpochMs as number : 0;

    committedEpochMs = Math.max(
      nowEpochMs,
      lastMessageEpochMs + 1,
    );

    transaction.create(messageRef, {
      accountId: uid,
      displayName: context.displayName,
      body,
      createdAt: FieldValue.serverTimestamp(),
      createdAtEpochMs: committedEpochMs,
      senderUtcOffsetMinutes,
      schemaVersion: CHAT_SCHEMA_VERSION,
    });

    transaction.set(
      rateRef,
      {
        lastSentAtEpochMs: nowEpochMs,
        updatedAt: FieldValue.serverTimestamp(),
      },
      {merge: true},
    );

    transaction.set(
      metaRef,
      {
        lastMessageEpochMs: committedEpochMs,
        updatedAt: FieldValue.serverTimestamp(),
      },
      {merge: true},
    );
  });

  const created = await messageRef.get();

  return {
    ok: true,
    message: projectMessage(created),
    serverNowEpochMs: Date.now(),
    cooldownMs: SEND_COOLDOWN_MS,
  };
}

/**
 * Lists recent chat messages visible to one scope member.
 * @param {ChatScopeType} scopeType Lobby or match chat scope.
 * @param {string} uid Authenticated account id.
 * @param {ChatListData} data Callable request data.
 * @return {Promise<Record<string, unknown>>} Chat list response.
 */
async function listMessages(
  scopeType: ChatScopeType,
  uid: string,
  data: ChatListData,
): Promise<Record<string, unknown>> {
  const scopeId = readScopeId(data.scopeId);
  const afterEpochMs = readAfterEpochMs(data.afterEpochMs);
  const context = await readContext(scopeType, uid, scopeId);

  let query: FirebaseFirestore.Query =
    context.rootRef.collection("chat_messages");

  let reverse = false;

  if (afterEpochMs > 0) {
    query = query
      .where("createdAtEpochMs", ">", afterEpochMs)
      .orderBy("createdAtEpochMs", "asc")
      .limit(MAX_LIST_MESSAGES);
  } else {
    query = query
      .orderBy("createdAtEpochMs", "desc")
      .limit(MAX_LIST_MESSAGES);
    reverse = true;
  }

  const snapshot = await query.get();
  let docs = snapshot.docs;
  if (reverse) docs = docs.slice().reverse();

  return {
    ok: true,
    scopeType,
    scopeId,
    messages: docs.map(projectMessage),
    serverNowEpochMs: Date.now(),
    maxMessageLength: MAX_MESSAGE_CODEPOINTS,
    cooldownMs: SEND_COOLDOWN_MS,
    schemaVersion: CHAT_SCHEMA_VERSION,
  };
}

export const lobbyChatSend = onCall(
  {
    region: REGION,
    maxInstances: 10,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireUid(request);
    return sendMessage("lobby", uid, request.data ?? {});
  },
);

export const lobbyChatList = onCall(
  {
    region: REGION,
    maxInstances: 10,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireUid(request);
    return listMessages("lobby", uid, request.data ?? {});
  },
);

export const matchChatSend = onCall(
  {
    region: REGION,
    maxInstances: 10,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireUid(request);
    return sendMessage("match", uid, request.data ?? {});
  },
);

export const matchChatList = onCall(
  {
    region: REGION,
    maxInstances: 10,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireUid(request);
    return listMessages("match", uid, request.data ?? {});
  },
);
