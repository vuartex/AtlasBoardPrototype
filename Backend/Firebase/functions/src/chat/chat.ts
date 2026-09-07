import {
  FieldValue,
  Timestamp,
  getFirestore,
} from "firebase-admin/firestore";
import {HttpsError, onCall} from "firebase-functions/v2/https";
import {
  containsBlockedProfanity,
  profanityStaticTermCount,
} from "./profanity";

const REGION = "europe-west1";
const CHAT_SCHEMA_VERSION = 2;
const MAX_MESSAGE_CODEPOINTS = 120;
const SEND_COOLDOWN_MS = 10_000;
const MAX_LIST_MESSAGES = 50;
const DEFAULT_MESSAGE_RETENTION_DAYS = 30;
const DEFAULT_REPORT_RETENTION_DAYS = 180;
const DEFAULT_MAX_BURST_PER_MINUTE = 5;
const DEFAULT_MAX_DUPLICATE_REPEATS = 2;
const DEFAULT_DUPLICATE_WINDOW_MS = 120_000;
const REPORT_COOLDOWN_MS = 15_000;
const REPORT_WINDOW_MS = 60 * 60 * 1000;
const MAX_REPORTS_PER_WINDOW = 20;
const DAY_MS = 24 * 60 * 60 * 1000;

type ChatScopeType = "lobby" | "match";

type ErrorCode =
  "invalid-argument" |
  "not-found" |
  "permission-denied" |
  "failed-precondition" |
  "resource-exhausted" |
  "unauthenticated";

interface ChatMemberContext {
  scopeType: ChatScopeType;
  scopeId: string;
  displayName: string;
  rootRef: FirebaseFirestore.DocumentReference;
}

interface ChatModerationConfig {
  extraBlockedTerms: string[];
  blockExternalLinks: boolean;
  messageRetentionDays: number;
  reportRetentionDays: number;
  maxBurstPerMinute: number;
  maxDuplicateRepeats: number;
  duplicateWindowMs: number;
}

interface ChatAccessState {
  chatBanned: boolean;
  mutedUntilEpochMs: number;
  chatDisabled: boolean;
  minorRestricted: boolean;
}

interface ChatSendData {
  operation?: unknown;
  scopeId?: unknown;
  body?: unknown;
  senderUtcOffsetMinutes?: unknown;
  targetAccountId?: unknown;
  messageId?: unknown;
  reasonCode?: unknown;
  comment?: unknown;
  muted?: unknown;
  action?: unknown;
  durationMinutes?: unknown;
  extraBlockedTerms?: unknown;
  blockExternalLinks?: unknown;
  messageRetentionDays?: unknown;
  reportRetentionDays?: unknown;
  maxBurstPerMinute?: unknown;
  maxDuplicateRepeats?: unknown;
  duplicateWindowSeconds?: unknown;
}

interface ChatListData {
  operation?: unknown;
  scopeId?: unknown;
  afterEpochMs?: unknown;
  limit?: unknown;
}

/**
 * Throws a stable callable chat error.
 * @param {ErrorCode} code Firebase callable error code.
 * @param {string} technical Stable technical error identifier.
 * @param {string} errorKey Localized client error key.
 * @param {Record<string, unknown>} extra Optional structured error data.
 */
function error(
  code: ErrorCode,
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
 * Reads a string operation with a stable fallback.
 * @param {unknown} value Candidate operation.
 * @param {string} fallback Default operation.
 * @return {string} Normalized operation.
 */
function readOperation(value: unknown, fallback: string): string {
  if (value === undefined || value === null || value === "") {
    return fallback;
  }

  if (typeof value !== "string") {
    error(
      "invalid-argument",
      "INVALID_CHAT_OPERATION",
      "chat.error.invalid_request",
    );
  }

  return value.trim().toLowerCase();
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
 * Validates an account id used by moderation operations.
 * @param {unknown} value Candidate account id.
 * @return {string} Validated account id.
 */
function readAccountId(value: unknown): string {
  if (
    typeof value !== "string" ||
    value.length < 6 ||
    value.length > 128
  ) {
    error(
      "invalid-argument",
      "INVALID_CHAT_ACCOUNT",
      "chat.error.invalid_request",
    );
  }

  return value;
}

/**
 * Validates a message id used by moderation operations.
 * @param {unknown} value Candidate message id.
 * @return {string} Validated message id.
 */
function readMessageId(value: unknown): string {
  if (
    typeof value !== "string" ||
    value.length < 4 ||
    value.length > 160
  ) {
    error(
      "invalid-argument",
      "INVALID_CHAT_MESSAGE_ID",
      "chat.error.invalid_request",
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

  return body;
}

/**
 * Sanitizes an optional report or moderation comment.
 * @param {unknown} value Candidate comment.
 * @param {number} maxLength Maximum allowed code points.
 * @return {string} Sanitized comment.
 */
function readComment(value: unknown, maxLength: number): string {
  if (value === undefined || value === null) return "";
  if (typeof value !== "string") {
    error(
      "invalid-argument",
      "INVALID_CHAT_COMMENT",
      "chat.error.invalid_request",
    );
  }

  const comment = value
    .replace(/[\r\n\t]+/g, " ")
    .trim();

  if (Array.from(comment).length > maxLength) {
    error(
      "invalid-argument",
      "CHAT_COMMENT_TOO_LONG",
      "chat.error.invalid_request",
    );
  }

  return comment;
}

/**
 * Validates a report reason code.
 * @param {unknown} value Candidate reason.
 * @return {string} Validated reason code.
 */
function readReasonCode(value: unknown): string {
  const allowed = new Set([
    "abuse",
    "harassment",
    "spam",
    "profanity",
    "cheating",
    "other",
  ]);

  if (typeof value !== "string" || !allowed.has(value)) {
    error(
      "invalid-argument",
      "INVALID_CHAT_REPORT_REASON",
      "chat.error.invalid_request",
    );
  }

  return value;
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
 * Clamps a numeric config field from Firestore.
 * @param {unknown} value Candidate number.
 * @param {number} fallback Default value.
 * @param {number} min Minimum value.
 * @param {number} max Maximum value.
 * @return {number} Safe integer value.
 */
function configInteger(
  value: unknown,
  fallback: number,
  min: number,
  max: number,
): number {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    return fallback;
  }

  return Math.max(min, Math.min(max, Math.round(value)));
}

/**
 * Reads the current backend-managed chat moderation configuration.
 * @return {Promise<ChatModerationConfig>} Effective moderation config.
 */
async function readModerationConfig(): Promise<ChatModerationConfig> {
  const db = getFirestore();
  const snap = await db
    .collection("chat_moderation_config")
    .doc("current")
    .get();
  const data = snap.data() ?? {};

  const extraBlockedTerms = Array.isArray(data.extraBlockedTerms) ?
    data.extraBlockedTerms
      .filter((item: unknown) => typeof item === "string")
      .map((item: unknown) => (item as string).trim())
      .filter((item: string) => item.length >= 2 && item.length <= 40)
      .slice(0, 300) : [];

  return {
    extraBlockedTerms,
    blockExternalLinks:
      typeof data.blockExternalLinks === "boolean" ?
        data.blockExternalLinks : true,
    messageRetentionDays: configInteger(
      data.messageRetentionDays,
      DEFAULT_MESSAGE_RETENTION_DAYS,
      1,
      90,
    ),
    reportRetentionDays: configInteger(
      data.reportRetentionDays,
      DEFAULT_REPORT_RETENTION_DAYS,
      7,
      365,
    ),
    maxBurstPerMinute: configInteger(
      data.maxBurstPerMinute,
      DEFAULT_MAX_BURST_PER_MINUTE,
      2,
      10,
    ),
    maxDuplicateRepeats: configInteger(
      data.maxDuplicateRepeats,
      DEFAULT_MAX_DUPLICATE_REPEATS,
      1,
      5,
    ),
    duplicateWindowMs: configInteger(
      data.duplicateWindowMs,
      DEFAULT_DUPLICATE_WINDOW_MS,
      30_000,
      600_000,
    ),
  };
}

/**
 * Reads account-level chat sanctions and safety restrictions.
 * @param {string} uid Account id.
 * @return {Promise<ChatAccessState>} Effective account chat state.
 */
async function readAccessState(uid: string): Promise<ChatAccessState> {
  const db = getFirestore();
  const snap = await db.collection("chat_sanctions").doc(uid).get();
  const data = snap.data() ?? {};

  return {
    chatBanned: data.chatBanned === true,
    mutedUntilEpochMs:
      typeof data.mutedUntilEpochMs === "number" ?
        data.mutedUntilEpochMs : 0,
    chatDisabled: data.chatDisabled === true,
    minorRestricted: data.minorRestricted === true,
  };
}

/**
 * Blocks accounts that are not allowed to read chat.
 * @param {ChatAccessState} state Account chat access state.
 */
function assertCanReadChat(state: ChatAccessState): void {
  if (state.chatBanned) {
    error(
      "permission-denied",
      "CHAT_ACCOUNT_BANNED",
      "chat.error.banned",
    );
  }

  if (state.chatDisabled) {
    error(
      "failed-precondition",
      "CHAT_ACCOUNT_DISABLED",
      "chat.error.disabled",
    );
  }
}

/**
 * Blocks accounts that are not allowed to send chat.
 * @param {ChatAccessState} state Account chat access state.
 */
function assertCanSendChat(state: ChatAccessState): void {
  assertCanReadChat(state);

  const now = Date.now();
  if (state.mutedUntilEpochMs > now) {
    error(
      "failed-precondition",
      "CHAT_ACCOUNT_MUTED",
      "chat.error.muted",
      {
        retryAfterMs: state.mutedUntilEpochMs - now,
      },
    );
  }
}

/**
 * Returns true when a message contains an external link or invite.
 * @param {string} body Message body.
 * @return {boolean} True when a link-like pattern is present.
 */
function containsExternalLink(body: string): boolean {
  const source =
    "(https?:\\/\\/|www\\.|discord\\.gg|" +
    "discord\\.com\\/invite|t\\.me\\/|wa\\.me\\/|" +
    "steamcommunity\\.com\\/|(?:^|\\s)[\\w-]+\\." +
    "(?:com|net|org|gg|io|co|xyz|ru|tr|de|fr|es)" +
    "(?:\\/|\\s|$))";
  return new RegExp(source, "iu").test(body);
}

/**
 * Produces a stable anti-spam fingerprint for repeated-message checks.
 * @param {string} body Message body.
 * @return {string} Normalized message fingerprint.
 */
function spamFingerprint(body: string): string {
  return body
    .toLowerCase()
    .normalize("NFKD")
    .replace(/\p{M}+/gu, "")
    .replace(/[^\p{L}\p{N}]+/gu, " ")
    .replace(/\s+/g, " ")
    .trim()
    .slice(0, 120);
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

    if (accountId === uid || localOwnerAccountId === uid) {
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
 * Returns true when a token carries trusted moderator authority.
 * @param {unknown} token Firebase auth token claims.
 * @return {boolean} True when moderator authority is present.
 */
function hasModeratorClaim(token: unknown): boolean {
  if (typeof token !== "object" || token === null) return false;
  const claims = token as Record<string, unknown>;
  const role = typeof claims.role === "string" ? claims.role : "";
  return claims.admin === true ||
    claims.moderator === true ||
    claims.atlasChatModerator === true ||
    role === "admin" ||
    role === "moderator";
}

/**
 * Requires trusted moderator authority.
 * @param {unknown} token Firebase auth token claims.
 */
function requireModerator(token: unknown): void {
  if (!hasModeratorClaim(token)) {
    error(
      "permission-denied",
      "CHAT_MODERATOR_REQUIRED",
      "chat.error.moderator_required",
    );
  }
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
 * Reads account ids muted by one user.
 * @param {string} uid Account id.
 * @return {Promise<Set<string>>} Muted account ids.
 */
async function readMutedAccountIds(uid: string): Promise<Set<string>> {
  const db = getFirestore();
  const snapshot = await db
    .collection("chat_user_safety")
    .doc(uid)
    .collection("muted_accounts")
    .limit(100)
    .get();

  return new Set(snapshot.docs.map(
    (doc: FirebaseFirestore.QueryDocumentSnapshot) => doc.id,
  ));
}

/**
 * Reads account ids blocked by one user.
 * @param {string} uid Account id.
 * @return {Promise<Set<string>>} Blocked account ids.
 */
async function readBlockedAccountIds(uid: string): Promise<Set<string>> {
  const db = getFirestore();
  const snapshot = await db
    .collection("chat_user_safety")
    .doc(uid)
    .collection("blocked_accounts")
    .limit(100)
    .get();

  return new Set(snapshot.docs.map(
    (doc: FirebaseFirestore.QueryDocumentSnapshot) => doc.id,
  ));
}

/**
 * Validates that a target account belongs to the current chat scope.
 * @param {ChatMemberContext} context Current member context.
 * @param {string} targetAccountId Target account id.
 * @return {Promise<string>} Safe target display name.
 */
async function requireTargetInScope(
  context: ChatMemberContext,
  targetAccountId: string,
): Promise<string> {
  const rootSnap = await context.rootRef.get();
  const root = rootSnap.data() ?? {};
  const hostAccountId =
    typeof root.hostAccountId === "string" ? root.hostAccountId : "";

  const collectionName =
    context.scopeType === "lobby" ? "members" : "seats";
  const members = await context.rootRef.collection(collectionName).get();

  for (const member of members.docs) {
    const data = member.data();
    const accountId =
      typeof data.accountId === "string" ? data.accountId : "";
    const localOwnerAccountId =
      typeof data.localOwnerAccountId === "string" ?
        data.localOwnerAccountId : "";

    if (
      accountId === targetAccountId ||
      localOwnerAccountId === targetAccountId
    ) {
      return safeDisplayName(data.displayName);
    }
  }

  if (hostAccountId === targetAccountId) {
    const profile = await getFirestore()
      .collection("public_profiles")
      .doc(targetAccountId)
      .get();
    return safeDisplayName(profile.data()?.displayName);
  }

  error(
    "not-found",
    "CHAT_TARGET_NOT_IN_SCOPE",
    "chat.error.target_not_found",
  );
}

/**
 * Writes a moderator audit event.
 * @param {string} moderatorUid Moderator account id.
 * @param {string} action Audit action.
 * @param {Record<string, unknown>} details Audit details.
 * @return {Promise<void>} Completion task.
 */
async function writeModeratorAudit(
  moderatorUid: string,
  action: string,
  details: Record<string, unknown>,
): Promise<void> {
  const db = getFirestore();
  const now = Date.now();
  await db.collection("chat_moderation_audit").add({
    moderatorAccountId: moderatorUid,
    action,
    details,
    createdAt: FieldValue.serverTimestamp(),
    createdAtEpochMs: now,
    expiresAt: Timestamp.fromMillis(now + 365 * DAY_MS),
    schemaVersion: 1,
  });
}

/**
 * Validates and persists one chat message with abuse controls.
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
  const [context, config, access] = await Promise.all([
    readContext(scopeType, uid, scopeId),
    readModerationConfig(),
    readAccessState(uid),
  ]);

  assertCanSendChat(access);

  if (containsBlockedProfanity(body, config.extraBlockedTerms)) {
    error(
      "failed-precondition",
      "CHAT_PROFANITY_BLOCKED",
      "chat.error.blocked_language",
    );
  }

  if (
    (config.blockExternalLinks || access.minorRestricted) &&
    containsExternalLink(body)
  ) {
    error(
      "failed-precondition",
      "CHAT_EXTERNAL_LINK_BLOCKED",
      "chat.error.external_link_blocked",
    );
  }

  const db = getFirestore();
  const rateRef = context.rootRef
    .collection("chat_rate_limits")
    .doc(uid);
  const metaRef = context.rootRef.collection("chat_meta").doc("state");
  const messageRef = context.rootRef.collection("chat_messages").doc();
  const fingerprint = spamFingerprint(body);

  let committedEpochMs = 0;

  await db.runTransaction(async (
    transaction: FirebaseFirestore.Transaction,
  ) => {
    const [rateSnap, metaSnap] = await Promise.all([
      transaction.get(rateRef),
      transaction.get(metaRef),
    ]);

    const nowEpochMs = Date.now();
    const rateData = rateSnap.data() ?? {};
    const lastSentAtEpochMs =
      typeof rateData.lastSentAtEpochMs === "number" ?
        rateData.lastSentAtEpochMs : 0;
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

    let windowStartEpochMs =
      typeof rateData.windowStartEpochMs === "number" ?
        rateData.windowStartEpochMs : nowEpochMs;
    let windowMessageCount =
      typeof rateData.windowMessageCount === "number" ?
        rateData.windowMessageCount : 0;

    if (nowEpochMs - windowStartEpochMs >= 60_000) {
      windowStartEpochMs = nowEpochMs;
      windowMessageCount = 0;
    }

    if (windowMessageCount >= config.maxBurstPerMinute) {
      error(
        "resource-exhausted",
        "CHAT_BURST_LIMIT",
        "chat.error.rate_limited",
        {
          retryAfterMs:
            Math.max(1000, 60_000 - (nowEpochMs - windowStartEpochMs)),
        },
      );
    }

    const lastFingerprint =
      typeof rateData.lastBodyFingerprint === "string" ?
        rateData.lastBodyFingerprint : "";
    let duplicateWindowStartEpochMs =
      typeof rateData.duplicateWindowStartEpochMs === "number" ?
        rateData.duplicateWindowStartEpochMs : nowEpochMs;
    let duplicateCount =
      typeof rateData.duplicateCount === "number" ?
        rateData.duplicateCount : 0;

    if (
      nowEpochMs - duplicateWindowStartEpochMs >
      config.duplicateWindowMs
    ) {
      duplicateWindowStartEpochMs = nowEpochMs;
      duplicateCount = 0;
    }

    if (lastFingerprint === fingerprint) {
      duplicateCount++;
    } else {
      duplicateCount = 1;
      duplicateWindowStartEpochMs = nowEpochMs;
    }

    if (duplicateCount > config.maxDuplicateRepeats) {
      error(
        "resource-exhausted",
        "CHAT_DUPLICATE_SPAM",
        "chat.error.duplicate_spam",
      );
    }

    const metaData = metaSnap.data() ?? {};
    const lastMessageEpochMs =
      typeof metaData.lastMessageEpochMs === "number" ?
        metaData.lastMessageEpochMs : 0;

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
      removed: false,
      expiresAt: Timestamp.fromMillis(
        committedEpochMs + config.messageRetentionDays * DAY_MS,
      ),
      schemaVersion: CHAT_SCHEMA_VERSION,
    });

    transaction.set(
      rateRef,
      {
        lastSentAtEpochMs: nowEpochMs,
        windowStartEpochMs,
        windowMessageCount: windowMessageCount + 1,
        lastBodyFingerprint: fingerprint,
        duplicateWindowStartEpochMs,
        duplicateCount,
        updatedAt: FieldValue.serverTimestamp(),
        expiresAt: Timestamp.fromMillis(nowEpochMs + DAY_MS),
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
  const [
    context,
    access,
    mutedAccountIds,
    blockedAccountIds,
  ] = await Promise.all([
    readContext(scopeType, uid, scopeId),
    readAccessState(uid),
    readMutedAccountIds(uid),
    readBlockedAccountIds(uid),
  ]);

  assertCanReadChat(access);

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
  let docs = snapshot.docs.filter((
    doc: FirebaseFirestore.QueryDocumentSnapshot,
  ) => {
    const item = doc.data();
    const accountId =
      typeof item.accountId === "string" ? item.accountId : "";
    return item.removed !== true &&
      !mutedAccountIds.has(accountId) &&
      !blockedAccountIds.has(accountId);
  });

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

/**
 * Applies or removes a user-level mute for another player.
 * @param {ChatScopeType} scopeType Lobby or match chat scope.
 * @param {string} uid Authenticated account id.
 * @param {ChatSendData} data Callable request data.
 * @return {Promise<Record<string, unknown>>} Mute operation result.
 */
async function setPlayerMuted(
  scopeType: ChatScopeType,
  uid: string,
  data: ChatSendData,
): Promise<Record<string, unknown>> {
  const scopeId = readScopeId(data.scopeId);
  const targetAccountId = readAccountId(data.targetAccountId);
  if (uid === targetAccountId) {
    error(
      "invalid-argument",
      "CHAT_CANNOT_MUTE_SELF",
      "chat.error.invalid_request",
    );
  }

  if (typeof data.muted !== "boolean") {
    error(
      "invalid-argument",
      "INVALID_CHAT_MUTE_VALUE",
      "chat.error.invalid_request",
    );
  }

  const context = await readContext(scopeType, uid, scopeId);
  const targetDisplayName = await requireTargetInScope(
    context,
    targetAccountId,
  );
  const ref = getFirestore()
    .collection("chat_user_safety")
    .doc(uid)
    .collection("muted_accounts")
    .doc(targetAccountId);

  if (data.muted) {
    await ref.set({
      targetAccountId,
      targetDisplayName,
      mutedAt: FieldValue.serverTimestamp(),
      mutedAtEpochMs: Date.now(),
      schemaVersion: 1,
    });
  } else {
    await ref.delete();
  }

  return {
    ok: true,
    targetAccountId,
    targetDisplayName,
    muted: data.muted,
  };
}

/**
 * Applies or removes a user-level block for another player.
 * @param {ChatScopeType} scopeType Lobby or match chat scope.
 * @param {string} uid Authenticated account id.
 * @param {ChatSendData} data Callable request data.
 * @return {Promise<Record<string, unknown>>} Block operation result.
 */
async function setPlayerBlocked(
  scopeType: ChatScopeType,
  uid: string,
  data: ChatSendData,
): Promise<Record<string, unknown>> {
  const scopeId = readScopeId(data.scopeId);
  const targetAccountId = readAccountId(data.targetAccountId);
  if (uid === targetAccountId) {
    error(
      "invalid-argument",
      "CHAT_CANNOT_BLOCK_SELF",
      "chat.error.invalid_request",
    );
  }

  if (typeof data.muted !== "boolean") {
    error(
      "invalid-argument",
      "INVALID_CHAT_BLOCK_VALUE",
      "chat.error.invalid_request",
    );
  }

  const context = await readContext(scopeType, uid, scopeId);
  const targetDisplayName = await requireTargetInScope(
    context,
    targetAccountId,
  );
  const ref = getFirestore()
    .collection("chat_user_safety")
    .doc(uid)
    .collection("blocked_accounts")
    .doc(targetAccountId);

  if (data.muted) {
    await ref.set({
      targetAccountId,
      targetDisplayName,
      blockedAt: FieldValue.serverTimestamp(),
      blockedAtEpochMs: Date.now(),
      schemaVersion: 1,
    });
  } else {
    await ref.delete();
  }

  return {
    ok: true,
    targetAccountId,
    targetDisplayName,
    blocked: data.muted,
  };
}

/**
 * Enforces report spam controls inside a transaction.
 * @param {string} uid Reporting account id.
 * @return {Promise<void>} Completion task.
 */
async function reserveReportSlot(uid: string): Promise<void> {
  const db = getFirestore();
  const ref = db.collection("chat_report_rate_limits").doc(uid);

  await db.runTransaction(async (
    transaction: FirebaseFirestore.Transaction,
  ) => {
    const snap = await transaction.get(ref);
    const data = snap.data() ?? {};
    const now = Date.now();
    const lastReportAtEpochMs =
      typeof data.lastReportAtEpochMs === "number" ?
        data.lastReportAtEpochMs : 0;

    if (
      lastReportAtEpochMs > 0 &&
      now - lastReportAtEpochMs < REPORT_COOLDOWN_MS
    ) {
      error(
        "resource-exhausted",
        "CHAT_REPORT_RATE_LIMIT",
        "chat.error.report_rate_limited",
      );
    }

    let windowStartEpochMs =
      typeof data.windowStartEpochMs === "number" ?
        data.windowStartEpochMs : now;
    let windowCount =
      typeof data.windowCount === "number" ? data.windowCount : 0;

    if (now - windowStartEpochMs >= REPORT_WINDOW_MS) {
      windowStartEpochMs = now;
      windowCount = 0;
    }

    if (windowCount >= MAX_REPORTS_PER_WINDOW) {
      error(
        "resource-exhausted",
        "CHAT_REPORT_WINDOW_LIMIT",
        "chat.error.report_rate_limited",
      );
    }

    transaction.set(
      ref,
      {
        lastReportAtEpochMs: now,
        windowStartEpochMs,
        windowCount: windowCount + 1,
        updatedAt: FieldValue.serverTimestamp(),
        expiresAt: Timestamp.fromMillis(now + DAY_MS),
      },
      {merge: true},
    );
  });
}

/**
 * Captures a bounded authoritative chat-context snapshot for moderation.
 * The report keeps recent server-stored messages so future ticket review does
 * not depend on the reporting client keeping local logs.
 * @param {ChatMemberContext} context Resolved chat scope context.
 * @param {number} limit Maximum number of recent messages to snapshot.
 * @return {Promise<Array<Record<string, unknown>>>} Context message records.
 */
async function captureReportContext(
  context: ChatMemberContext,
  limit = 30,
): Promise<Array<Record<string, unknown>>> {
  const snapshot = await context.rootRef
    .collection("chat_messages")
    .orderBy("createdAtEpochMs", "desc")
    .limit(Math.max(1, Math.min(limit, 30)))
    .get();

  return snapshot.docs
    .slice()
    .reverse()
    .map((doc) => {
      const data = doc.data() ?? {};
      return {
        messageId: doc.id,
        accountId:
          typeof data.accountId === "string" ? data.accountId : "",
        displayName: safeDisplayName(data.displayName),
        body:
          typeof data.body === "string" ? data.body.slice(0, 120) : "",
        createdAtEpochMs:
          typeof data.createdAtEpochMs === "number" ?
            data.createdAtEpochMs : 0,
        senderUtcOffsetMinutes:
          typeof data.senderUtcOffsetMinutes === "number" ?
            data.senderUtcOffsetMinutes : 0,
        removed: data.removed === true,
      };
    });
}

/**
 * Creates a player-level abuse report.
 * @param {ChatScopeType} scopeType Lobby or match chat scope.
 * @param {string} uid Reporting account id.
 * @param {ChatSendData} data Callable request data.
 * @return {Promise<Record<string, unknown>>} Report result.
 */
async function reportPlayer(
  scopeType: ChatScopeType,
  uid: string,
  data: ChatSendData,
): Promise<Record<string, unknown>> {
  const scopeId = readScopeId(data.scopeId);
  const targetAccountId = readAccountId(data.targetAccountId);
  if (uid === targetAccountId) {
    error(
      "invalid-argument",
      "CHAT_CANNOT_REPORT_SELF",
      "chat.error.invalid_request",
    );
  }

  const reasonCode = readReasonCode(data.reasonCode);
  const comment = readComment(data.comment, 160);
  const [context, config] = await Promise.all([
    readContext(scopeType, uid, scopeId),
    readModerationConfig(),
  ]);
  const targetDisplayName = await requireTargetInScope(
    context,
    targetAccountId,
  );
  await reserveReportSlot(uid);

  const contextMessages = await captureReportContext(context);
  const now = Date.now();
  const report = await getFirestore().collection("chat_reports").add({
    reportType: "player",
    status: "open",
    reporterAccountId: uid,
    targetAccountId,
    targetDisplayName,
    scopeType,
    scopeId,
    reasonCode,
    comment,
    contextSource: "server_authoritative_chat_history",
    contextCapturedAtEpochMs: now,
    contextMessageCount: contextMessages.length,
    contextMessages,
    createdAt: FieldValue.serverTimestamp(),
    createdAtEpochMs: now,
    expiresAt: Timestamp.fromMillis(
      now + config.reportRetentionDays * DAY_MS,
    ),
    schemaVersion: 1,
  });

  return {
    ok: true,
    reportId: report.id,
  };
}

/**
 * Creates a message-level abuse report from authoritative stored content.
 * @param {ChatScopeType} scopeType Lobby or match chat scope.
 * @param {string} uid Reporting account id.
 * @param {ChatSendData} data Callable request data.
 * @return {Promise<Record<string, unknown>>} Report result.
 */
async function reportMessage(
  scopeType: ChatScopeType,
  uid: string,
  data: ChatSendData,
): Promise<Record<string, unknown>> {
  const scopeId = readScopeId(data.scopeId);
  const messageId = readMessageId(data.messageId);
  const reasonCode = readReasonCode(data.reasonCode);
  const comment = readComment(data.comment, 160);
  const [context, config] = await Promise.all([
    readContext(scopeType, uid, scopeId),
    readModerationConfig(),
  ]);
  const messageRef = context.rootRef
    .collection("chat_messages")
    .doc(messageId);
  const messageSnap = await messageRef.get();

  if (!messageSnap.exists) {
    error(
      "not-found",
      "CHAT_MESSAGE_NOT_FOUND",
      "chat.error.message_not_found",
    );
  }

  const message = messageSnap.data() ?? {};
  const targetAccountId =
    typeof message.accountId === "string" ? message.accountId : "";
  if (!targetAccountId || targetAccountId === uid) {
    error(
      "invalid-argument",
      "CHAT_CANNOT_REPORT_MESSAGE",
      "chat.error.invalid_request",
    );
  }

  await reserveReportSlot(uid);
  const contextMessages = await captureReportContext(context);
  const now = Date.now();
  const report = await getFirestore().collection("chat_reports").add({
    reportType: "message",
    status: "open",
    reporterAccountId: uid,
    targetAccountId,
    targetDisplayName: safeDisplayName(message.displayName),
    scopeType,
    scopeId,
    messageId,
    messageBodySnapshot:
      typeof message.body === "string" ? message.body.slice(0, 120) : "",
    messageCreatedAtEpochMs:
      typeof message.createdAtEpochMs === "number" ?
        message.createdAtEpochMs : 0,
    reasonCode,
    comment,
    contextSource: "server_authoritative_chat_history",
    contextCapturedAtEpochMs: now,
    contextMessageCount: contextMessages.length,
    contextMessages,
    createdAt: FieldValue.serverTimestamp(),
    createdAtEpochMs: now,
    expiresAt: Timestamp.fromMillis(
      now + config.reportRetentionDays * DAY_MS,
    ),
    schemaVersion: 1,
  });

  return {
    ok: true,
    reportId: report.id,
  };
}

/**
 * Returns player safety state and moderator capability.
 * @param {string} uid Authenticated account id.
 * @param {unknown} token Firebase auth token claims.
 * @return {Promise<Record<string, unknown>>} Safety state response.
 */
async function getSafetyState(
  uid: string,
  token: unknown,
): Promise<Record<string, unknown>> {
  const [
    access,
    mutedIds,
    blockedIds,
    config,
  ] = await Promise.all([
    readAccessState(uid),
    readMutedAccountIds(uid),
    readBlockedAccountIds(uid),
    readModerationConfig(),
  ]);

  return {
    ok: true,
    mutedAccountIds: Array.from(mutedIds),
    blockedAccountIds: Array.from(blockedIds),
    canModerate: hasModeratorClaim(token),
    chatBanned: access.chatBanned,
    mutedUntilEpochMs: access.mutedUntilEpochMs,
    chatDisabled: access.chatDisabled,
    minorRestricted: access.minorRestricted,
    blockExternalLinks: config.blockExternalLinks,
    staticBlockedTermCount: profanityStaticTermCount(),
    extraBlockedTermCount: config.extraBlockedTerms.length,
    messageRetentionDays: config.messageRetentionDays,
    reportRetentionDays: config.reportRetentionDays,
  };
}

/**
 * Removes one stored message with moderator authority.
 * @param {ChatScopeType} scopeType Lobby or match chat scope.
 * @param {string} moderatorUid Moderator account id.
 * @param {unknown} token Firebase auth token claims.
 * @param {ChatSendData} data Callable request data.
 * @return {Promise<Record<string, unknown>>} Removal result.
 */
async function moderatorRemoveMessage(
  scopeType: ChatScopeType,
  moderatorUid: string,
  token: unknown,
  data: ChatSendData,
): Promise<Record<string, unknown>> {
  requireModerator(token);
  const scopeId = readScopeId(data.scopeId);
  const messageId = readMessageId(data.messageId);
  const reason = readComment(data.comment, 160);
  const rootRef = getFirestore()
    .collection(scopeType === "lobby" ? "lobbies" : "matches")
    .doc(scopeId);
  const rootSnap = await rootRef.get();
  if (!rootSnap.exists) {
    error(
      "not-found",
      "CHAT_SCOPE_NOT_FOUND",
      "chat.error.scope_not_found",
    );
  }

  const messageRef = rootRef.collection("chat_messages").doc(messageId);
  const messageSnap = await messageRef.get();
  if (!messageSnap.exists) {
    error(
      "not-found",
      "CHAT_MESSAGE_NOT_FOUND",
      "chat.error.message_not_found",
    );
  }

  await messageRef.set(
    {
      body: "",
      removed: true,
      removedAt: FieldValue.serverTimestamp(),
      removedAtEpochMs: Date.now(),
      removedBy: moderatorUid,
      removalReason: reason,
    },
    {merge: true},
  );

  await writeModeratorAudit(
    moderatorUid,
    "remove_message",
    {
      scopeType,
      scopeId,
      messageId,
      reason,
    },
  );

  return {
    ok: true,
    messageId,
  };
}

/**
 * Applies a moderator-controlled account chat sanction.
 * @param {string} moderatorUid Moderator account id.
 * @param {unknown} token Firebase auth token claims.
 * @param {ChatSendData} data Callable request data.
 * @return {Promise<Record<string, unknown>>} Sanction result.
 */
async function moderatorSetSanction(
  moderatorUid: string,
  token: unknown,
  data: ChatSendData,
): Promise<Record<string, unknown>> {
  requireModerator(token);
  const targetAccountId = readAccountId(data.targetAccountId);
  if (targetAccountId === moderatorUid) {
    error(
      "invalid-argument",
      "CHAT_MODERATOR_SELF_SANCTION",
      "chat.error.invalid_request",
    );
  }

  const action = readOperation(data.action, "");
  const reason = readComment(data.comment, 160);
  const ref = getFirestore().collection("chat_sanctions").doc(targetAccountId);
  const update: Record<string, unknown> = {
    updatedAt: FieldValue.serverTimestamp(),
    updatedAtEpochMs: Date.now(),
    updatedBy: moderatorUid,
    reason,
    schemaVersion: 1,
  };

  if (action === "mute") {
    const duration = configInteger(data.durationMinutes, 10, 1, 43_200);
    update.mutedUntilEpochMs = Date.now() + duration * 60_000;
  } else if (action === "unmute") {
    update.mutedUntilEpochMs = 0;
  } else if (action === "ban") {
    update.chatBanned = true;
  } else if (action === "unban") {
    update.chatBanned = false;
  } else if (action === "disable") {
    update.chatDisabled = true;
  } else if (action === "enable") {
    update.chatDisabled = false;
  } else if (action === "restricted_on") {
    update.minorRestricted = true;
  } else if (action === "restricted_off") {
    update.minorRestricted = false;
  } else {
    error(
      "invalid-argument",
      "INVALID_CHAT_SANCTION_ACTION",
      "chat.error.invalid_request",
    );
  }

  await ref.set(update, {merge: true});
  await writeModeratorAudit(
    moderatorUid,
    `sanction_${action}`,
    {
      targetAccountId,
      reason,
      durationMinutes: data.durationMinutes ?? null,
    },
  );

  return {
    ok: true,
    targetAccountId,
    action,
  };
}

/**
 * Replaces backend-managed moderation configuration values.
 * @param {string} moderatorUid Moderator account id.
 * @param {unknown} token Firebase auth token claims.
 * @param {ChatSendData} data Callable request data.
 * @return {Promise<Record<string, unknown>>} Config update result.
 */
async function moderatorUpdateConfig(
  moderatorUid: string,
  token: unknown,
  data: ChatSendData,
): Promise<Record<string, unknown>> {
  requireModerator(token);
  const current = await readModerationConfig();
  let extraBlockedTerms = current.extraBlockedTerms;

  if (data.extraBlockedTerms !== undefined) {
    if (!Array.isArray(data.extraBlockedTerms)) {
      error(
        "invalid-argument",
        "INVALID_CHAT_BLOCKED_TERMS",
        "chat.error.invalid_request",
      );
    }

    extraBlockedTerms = data.extraBlockedTerms
      .filter((item: unknown) => typeof item === "string")
      .map((item: unknown) => (item as string).trim())
      .filter((item: string) => item.length >= 2 && item.length <= 40)
      .slice(0, 300);
  }

  const next: ChatModerationConfig = {
    extraBlockedTerms,
    blockExternalLinks:
      typeof data.blockExternalLinks === "boolean" ?
        data.blockExternalLinks : current.blockExternalLinks,
    messageRetentionDays: configInteger(
      data.messageRetentionDays,
      current.messageRetentionDays,
      1,
      90,
    ),
    reportRetentionDays: configInteger(
      data.reportRetentionDays,
      current.reportRetentionDays,
      7,
      365,
    ),
    maxBurstPerMinute: configInteger(
      data.maxBurstPerMinute,
      current.maxBurstPerMinute,
      2,
      10,
    ),
    maxDuplicateRepeats: configInteger(
      data.maxDuplicateRepeats,
      current.maxDuplicateRepeats,
      1,
      5,
    ),
    duplicateWindowMs: configInteger(
      typeof data.duplicateWindowSeconds === "number" ?
        data.duplicateWindowSeconds * 1000 : undefined,
      current.duplicateWindowMs,
      30_000,
      600_000,
    ),
  };

  await getFirestore()
    .collection("chat_moderation_config")
    .doc("current")
    .set(
      {
        ...next,
        updatedAt: FieldValue.serverTimestamp(),
        updatedAtEpochMs: Date.now(),
        updatedBy: moderatorUid,
        schemaVersion: 1,
      },
      {merge: true},
    );

  await writeModeratorAudit(
    moderatorUid,
    "update_config",
    {
      extraBlockedTermCount: next.extraBlockedTerms.length,
      blockExternalLinks: next.blockExternalLinks,
      messageRetentionDays: next.messageRetentionDays,
      reportRetentionDays: next.reportRetentionDays,
      maxBurstPerMinute: next.maxBurstPerMinute,
      maxDuplicateRepeats: next.maxDuplicateRepeats,
      duplicateWindowMs: next.duplicateWindowMs,
    },
  );

  return {
    ok: true,
    extraBlockedTermCount: next.extraBlockedTerms.length,
  };
}

/**
 * Lists recent reports for a trusted moderator.
 * @param {unknown} token Firebase auth token claims.
 * @param {ChatListData} data Callable request data.
 * @return {Promise<Record<string, unknown>>} Report list result.
 */
async function moderatorListReports(
  token: unknown,
  data: ChatListData,
): Promise<Record<string, unknown>> {
  requireModerator(token);
  const limit = configInteger(data.limit, 25, 1, 50);
  const snapshot = await getFirestore()
    .collection("chat_reports")
    .orderBy("createdAtEpochMs", "desc")
    .limit(limit)
    .get();

  return {
    ok: true,
    reports: snapshot.docs.map((
      doc: FirebaseFirestore.QueryDocumentSnapshot,
    ) => {
      const item = doc.data();
      return {
        reportId: doc.id,
        reportType: item.reportType ?? "",
        status: item.status ?? "open",
        reporterAccountId: item.reporterAccountId ?? "",
        targetAccountId: item.targetAccountId ?? "",
        targetDisplayName: item.targetDisplayName ?? "",
        scopeType: item.scopeType ?? "",
        scopeId: item.scopeId ?? "",
        messageId: item.messageId ?? "",
        messageBodySnapshot: item.messageBodySnapshot ?? "",
        reasonCode: item.reasonCode ?? "",
        comment: item.comment ?? "",
        createdAtEpochMs: item.createdAtEpochMs ?? 0,
      };
    }),
  };
}

/**
 * Routes write-style chat operations through an existing callable export.
 * @param {ChatScopeType} scopeType Lobby or match scope.
 * @param {string} uid Authenticated account id.
 * @param {unknown} token Firebase auth token claims.
 * @param {ChatSendData} data Callable request data.
 * @return {Promise<Record<string, unknown>>} Operation result.
 */
async function routeSendOperation(
  scopeType: ChatScopeType,
  uid: string,
  token: unknown,
  data: ChatSendData,
): Promise<Record<string, unknown>> {
  const operation = readOperation(data.operation, "send");

  if (operation === "send") {
    return sendMessage(scopeType, uid, data);
  }
  if (operation === "set_muted") {
    return setPlayerMuted(scopeType, uid, data);
  }
  if (operation === "set_blocked") {
    return setPlayerBlocked(scopeType, uid, data);
  }
  if (operation === "report_player") {
    return reportPlayer(scopeType, uid, data);
  }
  if (operation === "report_message") {
    return reportMessage(scopeType, uid, data);
  }
  if (operation === "moderator_remove_message") {
    return moderatorRemoveMessage(scopeType, uid, token, data);
  }
  if (operation === "moderator_set_sanction") {
    return moderatorSetSanction(uid, token, data);
  }
  if (operation === "moderator_update_config") {
    return moderatorUpdateConfig(uid, token, data);
  }

  error(
    "invalid-argument",
    "UNKNOWN_CHAT_WRITE_OPERATION",
    "chat.error.invalid_request",
  );
}

/**
 * Routes read-style chat operations through an existing callable export.
 * @param {ChatScopeType} scopeType Lobby or match scope.
 * @param {string} uid Authenticated account id.
 * @param {unknown} token Firebase auth token claims.
 * @param {ChatListData} data Callable request data.
 * @return {Promise<Record<string, unknown>>} Operation result.
 */
async function routeListOperation(
  scopeType: ChatScopeType,
  uid: string,
  token: unknown,
  data: ChatListData,
): Promise<Record<string, unknown>> {
  const operation = readOperation(data.operation, "list");

  if (operation === "list") {
    return listMessages(scopeType, uid, data);
  }
  if (operation === "safety_state") {
    return getSafetyState(uid, token);
  }
  if (operation === "moderator_reports") {
    return moderatorListReports(token, data);
  }

  error(
    "invalid-argument",
    "UNKNOWN_CHAT_READ_OPERATION",
    "chat.error.invalid_request",
  );
}

export const lobbyChatSend = onCall(
  {
    region: REGION,
    maxInstances: 10,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireUid(request);
    return routeSendOperation(
      "lobby",
      uid,
      request.auth?.token,
      request.data ?? {},
    );
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
    return routeListOperation(
      "lobby",
      uid,
      request.auth?.token,
      request.data ?? {},
    );
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
    return routeSendOperation(
      "match",
      uid,
      request.auth?.token,
      request.data ?? {},
    );
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
    return routeListOperation(
      "match",
      uid,
      request.auth?.token,
      request.data ?? {},
    );
  },
);
