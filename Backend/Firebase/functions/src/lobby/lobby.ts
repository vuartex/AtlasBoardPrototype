import {
  createCipheriv,
  createDecipheriv,
  createHash,
  createHmac,
  randomBytes,
  randomInt,
  timingSafeEqual,
} from "crypto";
import {
  FieldValue,
  getFirestore,
} from "firebase-admin/firestore";
import {HttpsError} from "firebase-functions/v2/https";

const LOBBY_SCHEMA_VERSION = 2;
const JOIN_CODE_DIGITS = 6;
const MAX_PLAYERS = 4;
const INVALID_ATTEMPT_LIMIT = 5;
const INVALID_ATTEMPT_WINDOW_MS = 5 * 60 * 1000;
const INVALID_ATTEMPT_BLOCK_MS = 60 * 1000;
const LOCAL_JOIN_CODE_PEPPER =
  "atlasboard-local-emulator-lobby-code-pepper-v1";

export type LobbySeatMode =
  | "host_local"
  | "open_online"
  | "local_human"
  | "remote_human"
  | "bot"
  | "inactive";

export type LobbySeatPolicy =
  | "online"
  | "local_human"
  | "bot"
  | "inactive";

export type LobbyVisibility =
  | "private"
  | "public";

export interface LobbyVersionInfo {
  gameVersion: string;
  protocolVersion: number;
  rulesVersion: number;
  contentVersion: string;
  regionId: string;
}

export interface LobbySettingsInput {
  mapId: string;
  themeId: string;
  roundLimit: number;
  maxPlayers: number;
  balancedDevelopment: boolean;
  doublesEnabled: boolean;
  tripleDoublePenaltyEnabled: boolean;
}

export interface CreateLobbyInput {
  uid: string;
  settings: LobbySettingsInput;
  versions: LobbyVersionInfo;
  visibility?: LobbyVisibility;
}

export interface ListPublicLobbiesInput {
  uid: string;
  versions: LobbyVersionInfo;
  limit: number;
}

export interface JoinLobbyInput {
  uid: string;
  roomCode: string;
  password?: string;
  idempotencyKey: string;
  versions: LobbyVersionInfo;
}

export interface JoinPublicLobbyInput {
  uid: string;
  lobbyId: string;
  password?: string;
  idempotencyKey: string;
  versions: LobbyVersionInfo;
}

export interface UpdateLobbyPasswordInput {
  uid: string;
  lobbyId: string;
  expectedSettingsRevision: number;
  password?: string;
}

export interface CloseLobbyInput {
  uid: string;
  lobbyId: string;
}

export interface UpdateLobbySettingsInput {
  uid: string;
  lobbyId: string;
  expectedSettingsRevision: number;
  settings: Pick<
    LobbySettingsInput,
    | "mapId"
    | "themeId"
    | "roundLimit"
    | "balancedDevelopment"
    | "doublesEnabled"
    | "tripleDoublePenaltyEnabled"
  >;
}

export interface ConfigureLobbySeatsInput {
  uid: string;
  lobbyId: string;
  expectedSettingsRevision: number;
  maxPlayers: number;
  seatPolicies: LobbySeatPolicy[];
}

export interface SetLobbyReadyInput {
  uid: string;
  lobbyId: string;
  expectedSettingsRevision: number;
  ready: boolean;
}

export interface SetLobbyPawnCosmeticInput {
  uid: string;
  lobbyId: string;
  slotIndex: number;
  pawnCosmeticId: string;
}

export interface StartLobbyMatchInput {
  uid: string;
  lobbyId: string;
  expectedSettingsRevision: number;
}

export interface KickLobbyMemberInput {
  uid: string;
  lobbyId: string;
  expectedSettingsRevision: number;
  slotIndex: number;
}

export interface LeaveLobbyInput {
  uid: string;
  lobbyId: string;
}

export interface GetLobbySnapshotInput {
  uid: string;
  lobbyId: string;
}

export interface LobbyMemberSnapshot {
  seatId: string;
  slotIndex: number;
  active: boolean;
  seatMode: LobbySeatMode;
  seatType: "human" | "bot" | "inactive";
  accountId: string;
  localOwnerAccountId: string;
  displayName: string;
  pawnCosmeticId: string;
  isHost: boolean;
  connectionState: string;
  controllerKind: string;
  readyForRevision: number;
  requiresReady: boolean;
}

export interface PublicLobbyCard {
  lobbyId: string;
  hostDisplayName: string;
  mapId: string;
  themeId: string;
  roundLimit: number;
  maxPlayers: number;
  occupiedPlayers: number;
  openOnlineSeatCount: number;
  regionId: string;
  gameVersion: string;
  protocolVersion: number;
  rulesVersion: number;
  contentVersion: string;
  settingsRevision: number;
  hasPassword: boolean;
  createdAtEpochMs: number;
}

export interface LobbySnapshot {
  lobbyId: string;
  hostAccountId: string;
  lifecycleState: string;
  visibility: string;
  settingsRevision: number;
  mapId: string;
  themeId: string;
  roundLimit: number;
  maxPlayers: number;
  requiredHumanPlayers: number;
  localHumanCount: number;
  remoteHumanCount: number;
  remoteReadyRequiredCount: number;
  openOnlineSeatCount: number;
  botCount: number;
  balancedDevelopment: boolean;
  doublesEnabled: boolean;
  tripleDoublePenaltyEnabled: boolean;
  gameVersion: string;
  protocolVersion: number;
  rulesVersion: number;
  contentVersion: string;
  regionId: string;
  matchId: string;
  startEventId: string;
  startCountdownEndsAtEpochMs: number;
  hasPassword: boolean;
  members: LobbyMemberSnapshot[];
}

interface LobbyTransactionState {
  lobbyData: FirebaseFirestore.DocumentData;
  members: Array<{
    ref: FirebaseFirestore.DocumentReference;
    data: FirebaseFirestore.DocumentData;
  }>;
  snapshot: LobbySnapshot;
}

/**
 * Internal retry signal for rare six-digit join-code collisions.
 */
class JoinCodeCollisionError extends Error {}

/**
 * Normalizes a private room code.
 * @param {string} code User-entered code.
 * @return {string} Normalized code.
 */
export function normalizeRoomCode(code: string): string {
  return code.replace(/\s+/g, "").trim();
}

/**
 * Returns the backend-only join-code HMAC secret.
 * @return {string} Join-code pepper.
 */
function getJoinCodePepper(): string {
  if (process.env.FUNCTIONS_EMULATOR === "true") {
    return LOCAL_JOIN_CODE_PEPPER;
  }

  const pepper = process.env.ATLAS_JOIN_CODE_PEPPER;
  if (!pepper || pepper.length < 32) {
    throw new HttpsError(
      "failed-precondition",
      "JOIN_CODE_SECRET_NOT_CONFIGURED",
      {
        errorKey: "lobby.error.service_unavailable",
      },
    );
  }

  return pepper;
}

/**
 * Produces a protected room-code lookup id.
 * @param {string} roomCode Normalized six-digit code.
 * @return {string} HMAC-SHA256 lookup id.
 */
export function makeJoinCodeHash(roomCode: string): string {
  return createHmac("sha256", getJoinCodePepper())
    .update(`atlasboard:lobby-code:${roomCode}`, "utf8")
    .digest("hex");
}


/**
 * Normalizes a user-supplied optional lobby password.
 * Blank means password protection is disabled.
 * @param {unknown} value Password-like value.
 * @return {string} Normalized password.
 */
function normalizeLobbyPassword(value: unknown): string {
  return typeof value === "string" ? value.trim() : "";
}

/**
 * Validates a non-empty lobby password before hashing.
 * @param {string} password Normalized password.
 */
function validateLobbyPassword(password: string): void {
  if (password.length === 0) return;
  if (password.length < 4 || password.length > 32) {
    throw new HttpsError(
      "invalid-argument",
      "INVALID_LOBBY_PASSWORD",
      {errorKey: "lobby.error.password_length"},
    );
  }
}

/**
 * Produces a server-secret HMAC for a lobby password. Raw passwords are never
 * stored in Firestore or discovery documents.
 * @param {string} lobbyId Lobby id provides per-room namespace separation.
 * @param {string} password Normalized password.
 * @return {string} HMAC-SHA256 password hash.
 */
function makeLobbyPasswordHash(lobbyId: string, password: string): string {
  return createHmac("sha256", getJoinCodePepper())
    .update(`atlasboard:lobby-password:${lobbyId}:${password}`, "utf8")
    .digest("hex");
}


/**
 * Derives an AES key from the server-only join-code secret. This lets the
 * backend return the same invite/reconnect code to an authenticated member
 * who joined through the public browser without persisting the raw code.
 * @return {Buffer} AES-256 key.
 */
function roomCodeEncryptionKey(): Buffer {
  return createHash("sha256")
    .update(`atlasboard:room-code-display:v1:${getJoinCodePepper()}`, "utf8")
    .digest();
}

/**
 * Encrypts the raw six-digit code for backend-only recovery.
 * @param {string} roomCode Raw generated room code.
 * @return {string} iv.tag.ciphertext payload.
 */
function encryptRoomCode(roomCode: string): string {
  const iv = randomBytes(12);
  const cipher = createCipheriv("aes-256-gcm", roomCodeEncryptionKey(), iv);
  const encrypted = Buffer.concat([
    cipher.update(roomCode, "utf8"),
    cipher.final(),
  ]);
  const tag = cipher.getAuthTag();
  return [iv, tag, encrypted]
    .map((part) => part.toString("base64url"))
    .join(".");
}

/**
 * Decrypts a backend-only room-code payload.
 * @param {unknown} value Stored encrypted payload.
 * @return {string} Six-digit room code or blank on invalid legacy state.
 */
function decryptRoomCode(value: unknown): string {
  if (typeof value !== "string" || value.length < 1) return "";
  try {
    const parts = value.split(".");
    if (parts.length !== 3) return "";
    const iv = Buffer.from(parts[0], "base64url");
    const tag = Buffer.from(parts[1], "base64url");
    const encrypted = Buffer.from(parts[2], "base64url");
    const decipher = createDecipheriv(
      "aes-256-gcm",
      roomCodeEncryptionKey(),
      iv,
    );
    decipher.setAuthTag(tag);
    return Buffer.concat([
      decipher.update(encrypted),
      decipher.final(),
    ]).toString("utf8");
  } catch {
    return "";
  }
}

/**
 * Enforces an optional lobby password using constant-time hash comparison.
 * @param {FirebaseFirestore.DocumentData} lobby Authoritative lobby data.
 * @param {string} lobbyId Lobby id.
 * @param {unknown} supplied Supplied password.
 */
function assertLobbyPassword(
  lobby: FirebaseFirestore.DocumentData,
  lobbyId: string,
  supplied: unknown,
): void {
  if (lobby.hasPassword !== true) return;

  const password = normalizeLobbyPassword(supplied);
  if (!password) {
    throw new HttpsError(
      "failed-precondition",
      "LOBBY_PASSWORD_REQUIRED",
      {errorKey: "lobby.error.password_required"},
    );
  }

  const stored =
    typeof lobby.passwordHash === "string" ?
      lobby.passwordHash :
      "";
  const candidate = makeLobbyPasswordHash(lobbyId, password);
  const storedBuffer = Buffer.from(stored, "hex");
  const candidateBuffer = Buffer.from(candidate, "hex");

  if (
    storedBuffer.length !== candidateBuffer.length ||
    storedBuffer.length === 0 ||
    !timingSafeEqual(storedBuffer, candidateBuffer)
  ) {
    throw new HttpsError(
      "permission-denied",
      "LOBBY_PASSWORD_INCORRECT",
      {errorKey: "lobby.error.password_incorrect"},
    );
  }
}

/**
 * Loads and validates the canonical Atlas account.
 * @param {string} uid Firebase account id.
 * @return {Promise<{displayName:string}>} Public identity.
 */
async function loadActiveAccount(
  uid: string,
): Promise<{displayName: string}> {
  const db = getFirestore();
  const [userSnapshot, profileSnapshot] = await Promise.all([
    db.collection("users").doc(uid).get(),
    db.collection("public_profiles").doc(uid).get(),
  ]);

  if (!userSnapshot.exists) {
    throw new HttpsError(
      "failed-precondition",
      "ACCOUNT_PROFILE_REQUIRED",
      {
        errorKey: "lobby.error.account_required",
      },
    );
  }

  const user = userSnapshot.data() ?? {};
  if (user.accountStatus !== "active") {
    throw new HttpsError(
      "permission-denied",
      "ACCOUNT_NOT_ACTIVE",
      {
        errorKey: "lobby.error.account_not_active",
      },
    );
  }

  const profile = profileSnapshot.data() ?? {};
  const displayName =
    typeof profile.displayName === "string" &&
    profile.displayName.trim().length > 0 ?
      profile.displayName.trim().slice(0, 40) :
      "Player";

  return {displayName};
}

/**
 * Creates a stable invalid request error.
 * @param {string} fieldName Invalid field.
 * @return {HttpsError} Callable error.
 */
function invalidRequest(fieldName: string): HttpsError {
  return new HttpsError(
    "invalid-argument",
    "INVALID_LOBBY_REQUEST",
    {
      errorKey: "lobby.error.invalid_request",
      fieldName,
    },
  );
}

/**
 * Validates lobby rules.
 * @param {Partial<LobbySettingsInput>} settings Lobby settings.
 */
function validateRuleSettings(
  settings: Partial<LobbySettingsInput>,
): void {
  if (
    typeof settings.mapId !== "string" ||
    settings.mapId.trim().length < 1 ||
    settings.mapId.length > 64
  ) {
    throw invalidRequest("mapId");
  }

  if (
    typeof settings.themeId !== "string" ||
    settings.themeId.trim().length < 1 ||
    settings.themeId.length > 64
  ) {
    throw invalidRequest("themeId");
  }

  if (
    !Number.isSafeInteger(settings.roundLimit) ||
    ![10, 15, 20, 30].includes(settings.roundLimit as number)
  ) {
    throw invalidRequest("roundLimit");
  }

  for (const field of [
    "balancedDevelopment",
    "doublesEnabled",
    "tripleDoublePenaltyEnabled",
  ] as const) {
    if (typeof settings[field] !== "boolean") {
      throw invalidRequest(field);
    }
  }

  if (
    settings.tripleDoublePenaltyEnabled === true &&
    settings.doublesEnabled !== true
  ) {
    throw new HttpsError(
      "invalid-argument",
      "TRIPLE_DOUBLE_REQUIRES_DOUBLES",
      {
        errorKey: "lobby.error.invalid_rules",
      },
    );
  }
}

/**
 * Validates full create settings.
 * @param {LobbySettingsInput} settings Lobby settings.
 */
function validateCreateSettings(settings: LobbySettingsInput): void {
  if (
    !Number.isSafeInteger(settings.maxPlayers) ||
    settings.maxPlayers < 2 ||
    settings.maxPlayers > MAX_PLAYERS
  ) {
    throw invalidRequest("maxPlayers");
  }

  validateRuleSettings(settings);
}

/**
 * Validates compatibility metadata.
 * @param {LobbyVersionInfo} versions Version metadata.
 */
function validateVersions(versions: LobbyVersionInfo): void {
  if (
    typeof versions.gameVersion !== "string" ||
    versions.gameVersion.trim().length < 1 ||
    versions.gameVersion.length > 32 ||
    !Number.isSafeInteger(versions.protocolVersion) ||
    versions.protocolVersion < 1 ||
    !Number.isSafeInteger(versions.rulesVersion) ||
    versions.rulesVersion < 1 ||
    typeof versions.contentVersion !== "string" ||
    versions.contentVersion.trim().length < 1 ||
    versions.contentVersion.length > 32 ||
    typeof versions.regionId !== "string" ||
    versions.regionId.trim().length < 1 ||
    versions.regionId.length > 32
  ) {
    throw new HttpsError(
      "invalid-argument",
      "INVALID_VERSION_METADATA",
      {
        errorKey: "lobby.error.invalid_version_metadata",
      },
    );
  }
}

/**
 * Generates a six-digit numeric room code.
 * @return {string} Six-digit room code.
 */
function generateRoomCode(): string {
  return randomInt(0, 10 ** JOIN_CODE_DIGITS)
    .toString()
    .padStart(JOIN_CODE_DIGITS, "0");
}

/**
 * Returns true when the stored concrete seat needs network Ready.
 * @param {LobbySeatMode} seatMode Concrete seat mode.
 * @return {boolean} Whether Ready is required.
 */
function seatRequiresReady(seatMode: LobbySeatMode): boolean {
  return seatMode === "remote_human";
}

/**
 * Converts stored member data into a stable client snapshot.
 * @param {string} seatId Seat document id.
 * @param {FirebaseFirestore.DocumentData} data Stored seat data.
 * @param {number} fallbackIndex Fallback slot index.
 * @return {LobbyMemberSnapshot} Member snapshot.
 */
function memberFromData(
  seatId: string,
  data: FirebaseFirestore.DocumentData,
  fallbackIndex: number,
): LobbyMemberSnapshot {
  const seatMode = readSeatMode(data.seatMode);
  const active =
    typeof data.active === "boolean" ?
      data.active :
      seatMode !== "inactive";

  return {
    seatId,
    slotIndex:
      typeof data.slotIndex === "number" ?
        data.slotIndex :
        fallbackIndex,
    active,
    seatMode,
    seatType:
      seatMode === "bot" ?
        "bot" :
        seatMode === "inactive" ?
          "inactive" :
          "human",
    accountId:
      typeof data.accountId === "string" ?
        data.accountId :
        "",
    localOwnerAccountId:
      typeof data.localOwnerAccountId === "string" ?
        data.localOwnerAccountId :
        "",
    displayName:
      typeof data.displayName === "string" ?
        data.displayName :
        "",
    pawnCosmeticId:
      typeof data.pawnCosmeticId === "string" ?
        data.pawnCosmeticId :
        "",
    isHost: data.isHost === true,
    connectionState:
      typeof data.connectionState === "string" ?
        data.connectionState :
        "empty",
    controllerKind:
      typeof data.controllerKind === "string" ?
        data.controllerKind :
        "none",
    readyForRevision:
      typeof data.readyForRevision === "number" ?
        data.readyForRevision :
        0,
    requiresReady: seatRequiresReady(seatMode),
  };
}

/**
 * Reads a concrete seat mode safely.
 * @param {unknown} value Stored value.
 * @return {LobbySeatMode} Concrete seat mode.
 */
function readSeatMode(value: unknown): LobbySeatMode {
  switch (value) {
  case "host_local":
  case "open_online":
  case "local_human":
  case "remote_human":
  case "bot":
  case "inactive":
    return value;
  default:
    return "inactive";
  }
}

/**
 * Calculates compatibility/summary counts for the lobby snapshot.
 * @param {LobbyMemberSnapshot[]} members Members.
 * @return {Object} Summary counts.
 */
function summarizeMembers(
  members: LobbyMemberSnapshot[],
): {
  requiredHumanPlayers: number;
  localHumanCount: number;
  remoteHumanCount: number;
  remoteReadyRequiredCount: number;
  openOnlineSeatCount: number;
  botCount: number;
} {
  let requiredHumanPlayers = 0;
  let localHumanCount = 0;
  let remoteHumanCount = 0;
  let remoteReadyRequiredCount = 0;
  let openOnlineSeatCount = 0;
  let botCount = 0;

  for (const member of members) {
    if (!member.active) {
      continue;
    }

    switch (member.seatMode) {
    case "host_local":
    case "local_human":
      requiredHumanPlayers++;
      localHumanCount++;
      break;
    case "remote_human":
      requiredHumanPlayers++;
      remoteHumanCount++;
      remoteReadyRequiredCount++;
      break;
    case "open_online":
      requiredHumanPlayers++;
      openOnlineSeatCount++;
      break;
    case "bot":
      botCount++;
      break;
    default:
      break;
    }
  }

  return {
    requiredHumanPlayers,
    localHumanCount,
    remoteHumanCount,
    remoteReadyRequiredCount,
    openOnlineSeatCount,
    botCount,
  };
}

/**
 * Builds a stable lobby snapshot from stored data.
 * @param {string} lobbyId Lobby document id.
 * @param {FirebaseFirestore.DocumentData} lobby Stored lobby data.
 * @param {LobbyMemberSnapshot[]} members Member snapshots.
 * @return {LobbySnapshot} Client-safe snapshot.
 */
function buildLobbySnapshot(
  lobbyId: string,
  lobby: FirebaseFirestore.DocumentData,
  members: LobbyMemberSnapshot[],
): LobbySnapshot {
  const summary = summarizeMembers(members);

  return {
    lobbyId,
    hostAccountId:
      typeof lobby.hostAccountId === "string" ?
        lobby.hostAccountId :
        "",
    lifecycleState:
      typeof lobby.lifecycleState === "string" ?
        lobby.lifecycleState :
        "waiting",
    visibility:
      typeof lobby.visibility === "string" ?
        lobby.visibility :
        "private",
    settingsRevision:
      typeof lobby.settingsRevision === "number" ?
        lobby.settingsRevision :
        1,
    mapId:
      typeof lobby.mapId === "string" ?
        lobby.mapId :
        "",
    themeId:
      typeof lobby.themeId === "string" ?
        lobby.themeId :
        "",
    roundLimit:
      typeof lobby.roundLimit === "number" ?
        lobby.roundLimit :
        20,
    maxPlayers:
      typeof lobby.maxPlayers === "number" ?
        lobby.maxPlayers :
        MAX_PLAYERS,
    ...summary,
    balancedDevelopment: lobby.balancedDevelopment === true,
    doublesEnabled: lobby.doublesEnabled === true,
    tripleDoublePenaltyEnabled:
      lobby.tripleDoublePenaltyEnabled === true,
    gameVersion:
      typeof lobby.gameVersion === "string" ?
        lobby.gameVersion :
        "",
    protocolVersion:
      typeof lobby.protocolVersion === "number" ?
        lobby.protocolVersion :
        0,
    rulesVersion:
      typeof lobby.rulesVersion === "number" ?
        lobby.rulesVersion :
        0,
    contentVersion:
      typeof lobby.contentVersion === "string" ?
        lobby.contentVersion :
        "",
    regionId:
      typeof lobby.regionId === "string" ?
        lobby.regionId :
        "",
    matchId:
      typeof lobby.matchId === "string" ?
        lobby.matchId :
        "",
    startEventId:
      typeof lobby.startEventId === "string" ?
        lobby.startEventId :
        "",
    startCountdownEndsAtEpochMs:
      typeof lobby.startCountdownEndsAtEpochMs === "number" ?
        lobby.startCountdownEndsAtEpochMs :
        0,
    hasPassword: lobby.hasPassword === true,
    members,
  };
}

const PUBLIC_DISCOVERY_SCHEMA_VERSION = 1;
const PUBLIC_DISCOVERY_QUERY_LIMIT = 50;
const PUBLIC_BROWSER_MAX_RESULTS = 25;

/**
 * Counts concrete occupied participants for the public browser.
 * OpenOnline is capacity, not an occupied player.
 * @param {LobbySnapshot} snapshot Lobby snapshot.
 * @return {number} Occupied seat count.
 */
function occupiedPlayers(snapshot: LobbySnapshot): number {
  return snapshot.localHumanCount +
    snapshot.remoteHumanCount +
    snapshot.botCount;
}

/**
 * Returns the host display name from the canonical host seat snapshot.
 * @param {LobbySnapshot} snapshot Lobby snapshot.
 * @return {string} Public host display name.
 */
function hostDisplayName(snapshot: LobbySnapshot): string {
  const host = snapshot.members.find((member) => member.isHost);
  return host && host.displayName ? host.displayName : "Player";
}

/**
 * Builds the intentionally-sanitized discovery document.
 * No room code, join-code hash, account id, Ready state, or internal member
 * documents are exposed through the browser index.
 * @param {LobbySnapshot} snapshot Authoritative lobby snapshot.
 * @param {number} createdAtEpochMs Original lobby creation time.
 * @return {FirebaseFirestore.DocumentData} Discovery document.
 */
function publicDiscoveryData(
  snapshot: LobbySnapshot,
  createdAtEpochMs: number,
): FirebaseFirestore.DocumentData {
  return {
    lobbyId: snapshot.lobbyId,
    visibility: "public",
    lifecycleState: snapshot.lifecycleState,
    joinable:
      snapshot.lifecycleState === "waiting" &&
      snapshot.openOnlineSeatCount > 0,
    hostDisplayName: hostDisplayName(snapshot),
    mapId: snapshot.mapId,
    themeId: snapshot.themeId,
    roundLimit: snapshot.roundLimit,
    maxPlayers: snapshot.maxPlayers,
    occupiedPlayers: occupiedPlayers(snapshot),
    openOnlineSeatCount: snapshot.openOnlineSeatCount,
    regionId: snapshot.regionId,
    gameVersion: snapshot.gameVersion,
    protocolVersion: snapshot.protocolVersion,
    rulesVersion: snapshot.rulesVersion,
    contentVersion: snapshot.contentVersion,
    settingsRevision: snapshot.settingsRevision,
    hasPassword: snapshot.hasPassword,
    createdAtEpochMs,
    updatedAtEpochMs: Date.now(),
    discoverySchemaVersion: PUBLIC_DISCOVERY_SCHEMA_VERSION,
    updatedAt: FieldValue.serverTimestamp(),
  };
}

/**
 * Keeps the public discovery projection synchronized inside the SAME Firestore
 * transaction as the authoritative lobby mutation.
 * Private lobbies never receive a browser document.
 * @param {FirebaseFirestore.Transaction} transaction Current transaction.
 * @param {FirebaseFirestore.Firestore} db Firestore instance.
 * @param {LobbySnapshot} snapshot New authoritative snapshot.
 * @param {FirebaseFirestore.DocumentData} lobby Stored/new lobby data.
 */
function syncPublicDiscovery(
  transaction: FirebaseFirestore.Transaction,
  db: FirebaseFirestore.Firestore,
  snapshot: LobbySnapshot,
  lobby: FirebaseFirestore.DocumentData,
): void {
  const discoveryRef =
    db.collection("lobby_discovery").doc(snapshot.lobbyId);

  if (
    snapshot.visibility !== "public" ||
    snapshot.lifecycleState !== "waiting"
  ) {
    transaction.delete(discoveryRef);
    return;
  }

  const createdAtEpochMs =
    typeof lobby.createdAtEpochMs === "number" ?
      lobby.createdAtEpochMs :
      Date.now();

  transaction.set(
    discoveryRef,
    publicDiscoveryData(snapshot, createdAtEpochMs),
  );
}

/**
 * Converts a stored discovery document to the client-safe browser card.
 * @param {FirebaseFirestore.DocumentData} data Stored discovery doc.
 * @return {PublicLobbyCard|null} Browser card or null when invalid.
 */
function discoveryCardFromData(
  data: FirebaseFirestore.DocumentData,
): PublicLobbyCard | null {
  if (
    data.visibility !== "public" ||
    data.lifecycleState !== "waiting" ||
    data.joinable !== true ||
    typeof data.lobbyId !== "string" ||
    typeof data.hostDisplayName !== "string" ||
    typeof data.mapId !== "string" ||
    typeof data.themeId !== "string" ||
    !Number.isSafeInteger(data.roundLimit) ||
    !Number.isSafeInteger(data.maxPlayers) ||
    !Number.isSafeInteger(data.occupiedPlayers) ||
    !Number.isSafeInteger(data.openOnlineSeatCount) ||
    typeof data.regionId !== "string" ||
    typeof data.gameVersion !== "string" ||
    !Number.isSafeInteger(data.protocolVersion) ||
    !Number.isSafeInteger(data.rulesVersion) ||
    typeof data.contentVersion !== "string" ||
    !Number.isSafeInteger(data.settingsRevision) ||
    typeof data.hasPassword !== "boolean" ||
    !Number.isSafeInteger(data.createdAtEpochMs)
  ) {
    return null;
  }

  return {
    lobbyId: data.lobbyId,
    hostDisplayName: data.hostDisplayName,
    mapId: data.mapId,
    themeId: data.themeId,
    roundLimit: data.roundLimit,
    maxPlayers: data.maxPlayers,
    occupiedPlayers: data.occupiedPlayers,
    openOnlineSeatCount: data.openOnlineSeatCount,
    regionId: data.regionId,
    gameVersion: data.gameVersion,
    protocolVersion: data.protocolVersion,
    rulesVersion: data.rulesVersion,
    contentVersion: data.contentVersion,
    settingsRevision: data.settingsRevision,
    hasPassword: data.hasPassword,
    createdAtEpochMs: data.createdAtEpochMs,
  };
}

/**
 * Reads a lobby and all four fixed seat documents before writes.
 * @param {FirebaseFirestore.Transaction} transaction Firestore transaction.
 * @param {FirebaseFirestore.DocumentReference} lobbyRef Lobby reference.
 * @return {Promise<LobbyTransactionState>} Read transaction state.
 */
async function readLobbyState(
  transaction: FirebaseFirestore.Transaction,
  lobbyRef: FirebaseFirestore.DocumentReference,
): Promise<LobbyTransactionState> {
  const lobbySnapshot = await transaction.get(lobbyRef);
  if (!lobbySnapshot.exists) {
    throw new HttpsError(
      "not-found",
      "LOBBY_NOT_FOUND",
      {
        errorKey: "lobby.error.not_found",
      },
    );
  }

  const lobbyData = lobbySnapshot.data() ?? {};
  const seatRefs: FirebaseFirestore.DocumentReference[] = [];

  for (let index = 0; index < MAX_PLAYERS; index++) {
    seatRefs.push(
      lobbyRef.collection("members").doc(`seat_${index + 1}`),
    );
  }

  const seatSnapshots = await transaction.getAll(...seatRefs);
  const members = seatSnapshots.map((snapshot) => ({
    ref: snapshot.ref,
    data: snapshot.data() ?? {},
  }));

  return {
    lobbyData,
    members,
    snapshot: buildLobbySnapshot(
      lobbySnapshot.id,
      lobbyData,
      members.map((member, index) =>
        memberFromData(member.ref.id, member.data, index),
      ),
    ),
  };
}

/**
 * Creates default concrete data for one lobby seat.
 * @param {number} slotIndex Zero-based slot index.
 * @param {number} maxPlayers Active seat count.
 * @param {string} hostUid Host account id.
 * @param {string} hostDisplayName Host public display name.
 * @return {FirebaseFirestore.DocumentData} Seat data.
 */
function createDefaultSeatData(
  slotIndex: number,
  maxPlayers: number,
  hostUid: string,
  hostDisplayName: string,
): FirebaseFirestore.DocumentData {
  const isHost = slotIndex === 0;
  const active = slotIndex < maxPlayers;
  const seatMode: LobbySeatMode =
    isHost ?
      "host_local" :
      active ?
        "open_online" :
        "inactive";

  return {
    seatId: `seat_${slotIndex + 1}`,
    slotIndex,
    active,
    seatMode,
    seatType:
      seatMode === "inactive" ?
        "inactive" :
        "human",
    accountId: isHost ? hostUid : "",
    localOwnerAccountId: isHost ? hostUid : "",
    displayName: isHost ? hostDisplayName : "",
    pawnCosmeticId: "",
    isHost,
    controllerKind:
      isHost ?
        "local_human" :
        "none",
    connectionState:
      isHost ?
        "connected" :
        active ?
          "empty" :
          "inactive",
    readyForRevision: 0,
    joinIdempotencyKey: "",
    joinedAt: null,
    updatedAt: FieldValue.serverTimestamp(),
    schemaVersion: LOBBY_SCHEMA_VERSION,
  };
}

/**
 * Creates one lobby and protected room-code lookup.
 * Public creation additionally writes the sanitized browser projection in the
 * same transaction. Private creation never writes discovery state.
 * @param {CreateLobbyInput} input Create request.
 * @return {Promise<{roomCode:string,snapshot:LobbySnapshot}>} Result.
 */
async function createLobby(
  input: CreateLobbyInput,
): Promise<{roomCode: string; snapshot: LobbySnapshot}> {
  validateCreateSettings(input.settings);
  validateVersions(input.versions);
  const account = await loadActiveAccount(input.uid);
  const visibility: LobbyVisibility =
    input.visibility === "public" ? "public" : "private";

  const db = getFirestore();
  const lobbyRef = db.collection("lobbies").doc();

  for (let attempt = 0; attempt < 12; attempt++) {
    const roomCode = generateRoomCode();
    const codeHash = makeJoinCodeHash(roomCode);
    const codeRef = db.collection("join_codes").doc(codeHash);

    try {
      const result = await db.runTransaction(async (transaction) => {
        const codeSnapshot = await transaction.get(codeRef);
        if (codeSnapshot.exists) {
          throw new JoinCodeCollisionError();
        }

        const serverTimestamp = FieldValue.serverTimestamp();
        const createdAtEpochMs = Date.now();
        const lobbyData = {
          lobbyId: lobbyRef.id,
          hostAccountId: input.uid,
          visibility,
          lifecycleState: "waiting",
          settingsRevision: 1,
          mapId: input.settings.mapId.trim(),
          themeId: input.settings.themeId.trim(),
          roundLimit: input.settings.roundLimit,
          maxPlayers: input.settings.maxPlayers,
          balancedDevelopment: input.settings.balancedDevelopment,
          doublesEnabled: input.settings.doublesEnabled,
          tripleDoublePenaltyEnabled:
            input.settings.tripleDoublePenaltyEnabled,
          gameVersion: input.versions.gameVersion.trim(),
          protocolVersion: input.versions.protocolVersion,
          rulesVersion: input.versions.rulesVersion,
          contentVersion: input.versions.contentVersion.trim(),
          regionId: input.versions.regionId.trim(),
          authorityMode: "host_authoritative",
          crossplayMode: "cross_platform",
          joinCodeHash: codeHash,
          roomCodeCiphertext: encryptRoomCode(roomCode),
          matchId: "",
          startEventId: "",
          startCountdownEndsAtEpochMs: 0,
          hasPassword: false,
          passwordHash: "",
          schemaVersion: LOBBY_SCHEMA_VERSION,
          createdAtEpochMs,
          createdAt: serverTimestamp,
          updatedAt: serverTimestamp,
        };

        const memberSnapshots: LobbyMemberSnapshot[] = [];
        transaction.create(lobbyRef, lobbyData);

        for (let slotIndex = 0; slotIndex < MAX_PLAYERS; slotIndex++) {
          const seatRef = lobbyRef
            .collection("members")
            .doc(`seat_${slotIndex + 1}`);
          const seatData = createDefaultSeatData(
            slotIndex,
            input.settings.maxPlayers,
            input.uid,
            account.displayName,
          );

          transaction.create(seatRef, seatData);
          memberSnapshots.push(
            memberFromData(seatRef.id, seatData, slotIndex),
          );
        }

        transaction.create(codeRef, {
          lobbyId: lobbyRef.id,
          active: true,
          lookupActive: true,
          joinOpen: true,
          matchId: "",
          lifecycleState: "waiting",
          schemaVersion: LOBBY_SCHEMA_VERSION,
          createdAt: serverTimestamp,
          updatedAt: serverTimestamp,
        });

        const snapshot = buildLobbySnapshot(
          lobbyRef.id,
          lobbyData,
          memberSnapshots,
        );

        syncPublicDiscovery(
          transaction,
          db,
          snapshot,
          lobbyData,
        );

        return snapshot;
      });

      return {roomCode, snapshot: result};
    } catch (error) {
      if (error instanceof JoinCodeCollisionError) {
        continue;
      }

      throw error;
    }
  }

  throw new HttpsError(
    "resource-exhausted",
    "ROOM_CODE_ALLOCATION_FAILED",
    {
      errorKey: "lobby.error.code_allocation_failed",
    },
  );
}

/**
 * Creates a private lobby.
 * @param {CreateLobbyInput} input Create request.
 * @return {Promise<{roomCode:string,snapshot:LobbySnapshot}>} Result.
 */
export async function createPrivateLobby(
  input: CreateLobbyInput,
): Promise<{roomCode: string; snapshot: LobbySnapshot}> {
  return createLobby({...input, visibility: "private"});
}

/**
 * Closes older waiting public rooms owned by the same host. This prevents a
 * host from accumulating abandoned browser cards when they create a new room.
 * @param {string} uid Host account id.
 */
async function closePreviousPublicLobbiesForHost(uid: string): Promise<void> {
  const db = getFirestore();
  const query = await db.collection("lobbies")
    .where("hostAccountId", "==", uid)
    .limit(25)
    .get();

  const candidates = query.docs.filter((doc) => {
    const data = doc.data();
    return data.visibility === "public" && data.lifecycleState === "waiting";
  });

  if (candidates.length === 0) return;

  const batch = db.batch();
  const timestamp = FieldValue.serverTimestamp();

  for (const doc of candidates) {
    const data = doc.data();
    batch.update(doc.ref, {
      lifecycleState: "closed",
      updatedAt: timestamp,
    });
    batch.delete(db.collection("lobby_discovery").doc(doc.id));

    const codeHash =
      typeof data.joinCodeHash === "string" ?
        data.joinCodeHash :
        "";
    if (codeHash) {
      batch.set(
        db.collection("join_codes").doc(codeHash),
        {
          active: false,
          lookupActive: false,
          joinOpen: false,
          lifecycleState: "closed",
          updatedAt: timestamp,
        },
        {merge: true},
      );
    }
  }

  await batch.commit();
}

/**
 * Creates a public lobby and sanitized discovery projection.
 * @param {CreateLobbyInput} input Create request.
 * @return {Promise<{roomCode:string,snapshot:LobbySnapshot}>} Result.
 */
export async function createPublicLobby(
  input: CreateLobbyInput,
): Promise<{roomCode: string; snapshot: LobbySnapshot}> {
  await closePreviousPublicLobbiesForHost(input.uid);
  return createLobby({...input, visibility: "public"});
}

/**
 * Records an invalid room-code attempt.
 * @param {string} uid Authenticated account id.
 */
async function recordInvalidJoinAttempt(uid: string): Promise<void> {
  const db = getFirestore();
  const ref = db.collection("join_code_attempts").doc(uid);
  const now = Date.now();

  await db.runTransaction(async (transaction) => {
    const snapshot = await transaction.get(ref);
    const data = snapshot.data() ?? {};
    const existingWindow =
      typeof data.windowStartedAtEpochMs === "number" ?
        data.windowStartedAtEpochMs :
        now;
    const sameWindow =
      now - existingWindow < INVALID_ATTEMPT_WINDOW_MS;
    const previousCount =
      typeof data.invalidCount === "number" ?
        data.invalidCount :
        0;
    const invalidCount = sameWindow ? previousCount + 1 : 1;
    const blockedUntil =
      invalidCount >= INVALID_ATTEMPT_LIMIT ?
        now + INVALID_ATTEMPT_BLOCK_MS :
        0;

    transaction.set(ref, {
      uid,
      invalidCount,
      windowStartedAtEpochMs: sameWindow ? existingWindow : now,
      blockedUntilEpochMs: blockedUntil,
      updatedAt: FieldValue.serverTimestamp(),
      schemaVersion: LOBBY_SCHEMA_VERSION,
    });
  });
}

/**
 * Rejects currently blocked join-code attempts.
 * @param {string} uid Authenticated account id.
 */
async function assertJoinAttemptAllowed(uid: string): Promise<void> {
  const snapshot = await getFirestore()
    .collection("join_code_attempts")
    .doc(uid)
    .get();

  const data = snapshot.data() ?? {};
  const blockedUntil =
    typeof data.blockedUntilEpochMs === "number" ?
      data.blockedUntilEpochMs :
      0;

  if (blockedUntil > Date.now()) {
    throw new HttpsError(
      "resource-exhausted",
      "JOIN_RATE_LIMITED",
      {
        errorKey: "lobby.error.too_many_attempts",
        retryAfterEpochMs: blockedUntil,
      },
    );
  }
}

/**
 * Clears invalid-code attempt state after valid code lookup.
 * @param {string} uid Authenticated account id.
 */
async function clearJoinAttemptState(uid: string): Promise<void> {
  await getFirestore()
    .collection("join_code_attempts")
    .doc(uid)
    .delete();
}

/**
 * Validates exact room/client compatibility.
 * @param {FirebaseFirestore.DocumentData} lobby Lobby data.
 * @param {LobbyVersionInfo} versions Client versions.
 */
function assertCompatible(
  lobby: FirebaseFirestore.DocumentData,
  versions: LobbyVersionInfo,
): void {
  if (
    lobby.gameVersion !== versions.gameVersion ||
    lobby.protocolVersion !== versions.protocolVersion ||
    lobby.rulesVersion !== versions.rulesVersion ||
    lobby.contentVersion !== versions.contentVersion
  ) {
    throw new HttpsError(
      "failed-precondition",
      "LOBBY_VERSION_MISMATCH",
      {
        errorKey: "lobby.error.version_mismatch",
      },
    );
  }
}

/**
 * Lists sanitized, compatible, currently joinable public lobbies.
 * This is discovery only. A browser card is NEVER authority for a future Join;
 * Phase 4D will re-read the canonical lobby in a transaction before reserving a
 * seat.
 * @param {ListPublicLobbiesInput} input List request.
 * @return {Promise<PublicLobbyCard[]>} Public cards.
 */
export async function listPublicLobbies(
  input: ListPublicLobbiesInput,
): Promise<PublicLobbyCard[]> {
  validateVersions(input.versions);
  await loadActiveAccount(input.uid);

  const requestedLimit =
    Number.isSafeInteger(input.limit) ? input.limit : 20;
  const resultLimit = Math.min(
    PUBLIC_BROWSER_MAX_RESULTS,
    Math.max(1, requestedLimit),
  );

  const snapshot = await getFirestore()
    .collection("lobby_discovery")
    .where("joinable", "==", true)
    .limit(PUBLIC_DISCOVERY_QUERY_LIMIT)
    .get();

  const cards: PublicLobbyCard[] = [];

  for (const document of snapshot.docs) {
    const card = discoveryCardFromData(document.data());
    if (!card) {
      continue;
    }

    if (
      card.gameVersion !== input.versions.gameVersion ||
      card.protocolVersion !== input.versions.protocolVersion ||
      card.rulesVersion !== input.versions.rulesVersion ||
      card.contentVersion !== input.versions.contentVersion
    ) {
      continue;
    }

    const requestedRegion = input.versions.regionId.trim();
    if (
      requestedRegion !== "auto" &&
      card.regionId !== "auto" &&
      card.regionId !== requestedRegion
    ) {
      continue;
    }

    cards.push(card);
  }

  cards.sort(
    (left, right) => right.createdAtEpochMs - left.createdAtEpochMs,
  );

  return cards.slice(0, resultLimit);
}

/**
 * Atomically resolves a private room code and reserves only OpenOnline.
 * @param {JoinLobbyInput} input Join request.
 * @return {Promise<{snapshot:LobbySnapshot,idempotentReplay:boolean}>} Result.
 */
export async function joinLobbyByCode(
  input: JoinLobbyInput,
): Promise<{snapshot: LobbySnapshot; idempotentReplay: boolean}> {
  validateVersions(input.versions);

  const normalizedCode = normalizeRoomCode(input.roomCode);
  if (!/^\d{6}$/.test(normalizedCode)) {
    await recordInvalidJoinAttempt(input.uid);
    throw new HttpsError(
      "not-found",
      "INVALID_ROOM_CODE",
      {
        errorKey: "lobby.error.invalid_code",
      },
    );
  }

  await assertJoinAttemptAllowed(input.uid);
  const account = await loadActiveAccount(input.uid);

  const db = getFirestore();
  const codeHash = makeJoinCodeHash(normalizedCode);
  const codeSnapshot = await db
    .collection("join_codes")
    .doc(codeHash)
    .get();

  if (
    !codeSnapshot.exists ||
    codeSnapshot.data()?.active !== true ||
    codeSnapshot.data()?.lookupActive === false
  ) {
    await recordInvalidJoinAttempt(input.uid);
    throw new HttpsError(
      "not-found",
      "INVALID_ROOM_CODE",
      {
        errorKey: "lobby.error.invalid_code",
      },
    );
  }

  const lobbyId = codeSnapshot.data()?.lobbyId;
  if (typeof lobbyId !== "string" || lobbyId.length < 1) {
    throw new HttpsError(
      "internal",
      "JOIN_CODE_STATE_INVALID",
      {
        errorKey: "lobby.error.service_unavailable",
      },
    );
  }

  const lobbyRef = db.collection("lobbies").doc(lobbyId);

  const result = await db.runTransaction(async (transaction) => {
    const state = await readLobbyState(transaction, lobbyRef);
    const lobby = state.lobbyData;

    const kickSnapshot = await transaction.get(
      lobbyRef.collection("kicks").doc(input.uid),
    );

    if (kickSnapshot.exists &&
        kickSnapshot.data()?.active === true) {
      throw new HttpsError(
        "permission-denied",
        "LOBBY_KICKED",
        {
          errorKey: "lobby.error.kicked",
        },
      );
    }

    const existingMember = state.members.find(
      (member) =>
        member.data.active === true &&
        member.data.accountId === input.uid,
    );

    // Active-match reconnect is intentionally NOT a normal join. The protected
    // room code only resolves the session; authorization still requires the
    // authenticated account to already own this exact seat. Other accounts
    // continue to receive the normal not-joinable response.
    if (
      lobby.lifecycleState === "starting" &&
      existingMember &&
      typeof lobby.matchId === "string" &&
      lobby.matchId.length > 0
    ) {
      const matchSeatRef = db
        .collection("matches")
        .doc(lobby.matchId)
        .collection("seats")
        .doc(existingMember.data.seatId);
      const matchSeatSnap = await transaction.get(matchSeatRef);

      if (!matchSeatSnap.exists) {
        throw new HttpsError(
          "failed-precondition",
          "MATCH_SEAT_NOT_FOUND",
          {errorKey: "match.error.seat_required"},
        );
      }

      const matchSeat = matchSeatSnap.data() ?? {};
      const expiresAt =
        typeof matchSeat.reconnectExpiresAtEpochMs === "number" ?
          matchSeat.reconnectExpiresAtEpochMs :
          0;

      if (matchSeat.afkLockedOut === true) {
        throw new HttpsError(
          "permission-denied",
          "AFK_REMOVED_FROM_MATCH",
          {errorKey: "match.error.afk_removed"},
        );
      }

      if (
        matchSeat.controllerKind === "temporary_bot" &&
        expiresAt > 0 &&
        expiresAt < Date.now()
      ) {
        transaction.set(
          matchSeatRef,
          {
            controllerKind: "permanent_bot",
            connectionState: "reconnect_expired",
            reconnectExpiresAtEpochMs: 0,
            removalReason: "reconnect_expired",
            updatedAt: FieldValue.serverTimestamp(),
          },
          {merge: true},
        );

        throw new HttpsError(
          "failed-precondition",
          "RECONNECT_WINDOW_EXPIRED",
          {errorKey: "match.error.reconnect_expired"},
        );
      }

      transaction.set(
        matchSeatRef,
        {
          controllerKind: "human",
          connectionState: "connected",
          reconnectExpiresAtEpochMs: 0,
          removalReason: "",
          updatedAt: FieldValue.serverTimestamp(),
        },
        {merge: true},
      );

      transaction.set(
        existingMember.ref,
        {
          controllerKind: "human",
          connectionState: "connected",
          updatedAt: FieldValue.serverTimestamp(),
        },
        {merge: true},
      );

      return {
        snapshot: state.snapshot,
        idempotentReplay: true,
      };
    }

    if (lobby.lifecycleState !== "waiting") {
      throw new HttpsError(
        "failed-precondition",
        "LOBBY_NOT_JOINABLE",
        {
          errorKey: "lobby.error.not_joinable",
        },
      );
    }

    assertCompatible(lobby, input.versions);
    assertLobbyPassword(lobby, lobbyId, input.password);

    if (existingMember) {
      // Same authenticated account returning to a waiting rematch lobby. Do not
      // reserve another seat and do not expose a technical duplicate error.
      transaction.set(
        existingMember.ref,
        {
          controllerKind: "human",
          connectionState: "connected",
          readyForRevision: 0,
          joinIdempotencyKey: input.idempotencyKey,
          updatedAt: FieldValue.serverTimestamp(),
        },
        {merge: true},
      );

      return {
        snapshot: state.snapshot,
        idempotentReplay: true,
      };
    }

    const openSeat = state.members.find(
      (member) =>
        member.data.active === true &&
        member.data.seatMode === "open_online" &&
        (!member.data.accountId || member.data.accountId === ""),
    );

    if (!openSeat) {
      throw new HttpsError(
        "resource-exhausted",
        "LOBBY_FULL",
        {
          errorKey: "lobby.error.full",
        },
      );
    }

    transaction.update(openSeat.ref, {
      seatMode: "remote_human",
      seatType: "human",
      accountId: input.uid,
      localOwnerAccountId: "",
      displayName: account.displayName,
      pawnCosmeticId: "",
      controllerKind: "human",
      connectionState: "connected",
      readyForRevision: 0,
      joinIdempotencyKey: input.idempotencyKey,
      joinedAt: FieldValue.serverTimestamp(),
      updatedAt: FieldValue.serverTimestamp(),
    });

    transaction.update(lobbyRef, {
      updatedAt: FieldValue.serverTimestamp(),
    });

    const member = state.snapshot.members.find(
      (item) => item.seatId === openSeat.ref.id,
    );

    if (member) {
      member.seatMode = "remote_human";
      member.seatType = "human";
      member.accountId = input.uid;
      member.localOwnerAccountId = "";
      member.displayName = account.displayName;
      member.pawnCosmeticId = "";
      member.controllerKind = "human";
      member.connectionState = "connected";
      member.readyForRevision = 0;
      member.requiresReady = true;
    }

    state.snapshot = buildLobbySnapshot(
      state.snapshot.lobbyId,
      lobby,
      state.snapshot.members,
    );

    syncPublicDiscovery(
      transaction,
      db,
      state.snapshot,
      lobby,
    );

    return {
      snapshot: state.snapshot,
      idempotentReplay: false,
    };
  });

  await clearJoinAttemptState(input.uid);
  return result;
}

/**
 * Atomically joins a public lobby directly by authoritative lobbyId. Browser
 * cards are discovery only; this function re-reads the canonical lobby and
 * reserves only a real OpenOnline seat.
 * @param {JoinPublicLobbyInput} input Public join request.
 * @return {Promise<{snapshot:LobbySnapshot,idempotentReplay:boolean}>} Result.
 */
export async function joinPublicLobby(
  input: JoinPublicLobbyInput,
): Promise<{
  snapshot: LobbySnapshot;
  idempotentReplay: boolean;
  roomCode: string;
}> {
  validateVersions(input.versions);
  const account = await loadActiveAccount(input.uid);
  const db = getFirestore();
  const lobbyRef = db.collection("lobbies").doc(input.lobbyId);

  const result = await db.runTransaction(async (transaction) => {
    const state = await readLobbyState(transaction, lobbyRef);
    const lobby = state.lobbyData;

    if (lobby.visibility !== "public" || lobby.lifecycleState !== "waiting") {
      throw new HttpsError(
        "failed-precondition",
        "PUBLIC_LOBBY_NOT_JOINABLE",
        {errorKey: "lobby.error.not_joinable"},
      );
    }

    assertCompatible(lobby, input.versions);
    assertLobbyPassword(lobby, input.lobbyId, input.password);

    const kickSnapshot = await transaction.get(
      lobbyRef.collection("kicks").doc(input.uid),
    );
    if (kickSnapshot.exists && kickSnapshot.data()?.active === true) {
      throw new HttpsError(
        "permission-denied",
        "LOBBY_KICKED",
        {errorKey: "lobby.error.kicked"},
      );
    }

    for (const member of state.members) {
      if (member.data.accountId !== input.uid) continue;
      if (member.data.joinIdempotencyKey === input.idempotencyKey) {
        return {
          snapshot: state.snapshot,
          idempotentReplay: true,
          encryptedRoomCode: lobby.roomCodeCiphertext ?? "",
        };
      }
      throw new HttpsError(
        "already-exists",
        "ACCOUNT_ALREADY_IN_LOBBY",
        {errorKey: "lobby.error.already_joined"},
      );
    }

    const openSeat = state.members.find(
      (member) =>
        member.data.active === true &&
        member.data.seatMode === "open_online" &&
        (!member.data.accountId || member.data.accountId === ""),
    );

    if (!openSeat) {
      throw new HttpsError(
        "resource-exhausted",
        "LOBBY_FULL",
        {errorKey: "lobby.error.full"},
      );
    }

    const serverTimestamp = FieldValue.serverTimestamp();
    transaction.update(openSeat.ref, {
      seatMode: "remote_human",
      seatType: "human",
      accountId: input.uid,
      localOwnerAccountId: "",
      displayName: account.displayName,
      controllerKind: "human",
      connectionState: "connected",
      readyForRevision: 0,
      joinIdempotencyKey: input.idempotencyKey,
      joinedAt: serverTimestamp,
      updatedAt: serverTimestamp,
    });
    transaction.update(lobbyRef, {updatedAt: serverTimestamp});

    const member = state.snapshot.members.find(
      (item) => item.seatId === openSeat.ref.id,
    );
    if (member) {
      member.seatMode = "remote_human";
      member.seatType = "human";
      member.accountId = input.uid;
      member.localOwnerAccountId = "";
      member.displayName = account.displayName;
      member.controllerKind = "human";
      member.connectionState = "connected";
      member.readyForRevision = 0;
      member.requiresReady = true;
    }

    state.snapshot = buildLobbySnapshot(
      state.snapshot.lobbyId,
      lobby,
      state.snapshot.members,
    );

    syncPublicDiscovery(transaction, db, state.snapshot, lobby);

    return {
      snapshot: state.snapshot,
      idempotentReplay: false,
      encryptedRoomCode: lobby.roomCodeCiphertext ?? "",
    };
  });

  return {
    snapshot: result.snapshot,
    idempotentReplay: result.idempotentReplay,
    roomCode: decryptRoomCode(result.encryptedRoomCode),
  };
}
/**
 * Host-only password/access update shared by private and public rooms.
 * Blank password disables the lock. Any actual change advances the settings
 * revision so existing Remote Ready state becomes stale.
 * @param {UpdateLobbyPasswordInput} input Password update.
 * @return {Promise<{snapshot:LobbySnapshot,applied:boolean}>} Result.
 */
export async function updateLobbyPassword(
  input: UpdateLobbyPasswordInput,
): Promise<{snapshot: LobbySnapshot; applied: boolean}> {
  const password = normalizeLobbyPassword(input.password);
  validateLobbyPassword(password);
  const db = getFirestore();
  const lobbyRef = db.collection("lobbies").doc(input.lobbyId);

  return db.runTransaction(async (transaction) => {
    const state = await readLobbyState(transaction, lobbyRef);
    const lobby = state.lobbyData;
    assertHostWaiting(lobby, input.uid, input.expectedSettingsRevision);

    const nextHasPassword = password.length > 0;
    const nextHash = nextHasPassword ?
      makeLobbyPasswordHash(input.lobbyId, password) : "";
    const changed =
      lobby.hasPassword !== nextHasPassword ||
      (nextHasPassword && lobby.passwordHash !== nextHash);

    if (!changed) {
      return {snapshot: state.snapshot, applied: false};
    }

    const nextRevision = input.expectedSettingsRevision + 1;
    const serverTimestamp = FieldValue.serverTimestamp();
    transaction.update(lobbyRef, {
      hasPassword: nextHasPassword,
      passwordHash: nextHash,
      settingsRevision: nextRevision,
      updatedAt: serverTimestamp,
    });

    state.snapshot.hasPassword = nextHasPassword;
    state.snapshot.settingsRevision = nextRevision;
    syncPublicDiscovery(
      transaction,
      db,
      state.snapshot,
      {
        ...lobby,
        hasPassword: nextHasPassword,
        passwordHash: nextHash,
        settingsRevision: nextRevision,
      },
    );

    return {snapshot: state.snapshot, applied: true};
  });
}

/**
 * Host closes a Waiting room when leaving the lobby. Public discovery is
 * removed and the protected room-code lookup is deactivated.
 * @param {CloseLobbyInput} input Close request.
 * @return {Promise<{snapshot:LobbySnapshot,applied:boolean}>} Result.
 */
export async function closeLobby(
  input: CloseLobbyInput,
): Promise<{snapshot: LobbySnapshot; applied: boolean}> {
  const db = getFirestore();
  const lobbyRef = db.collection("lobbies").doc(input.lobbyId);

  return db.runTransaction(async (transaction) => {
    const state = await readLobbyState(transaction, lobbyRef);
    const lobby = state.lobbyData;

    if (lobby.hostAccountId !== input.uid) {
      throw new HttpsError(
        "permission-denied",
        "HOST_REQUIRED",
        {errorKey: "lobby.error.host_required"},
      );
    }

    if (lobby.lifecycleState !== "waiting") {
      return {snapshot: state.snapshot, applied: false};
    }

    const serverTimestamp = FieldValue.serverTimestamp();
    transaction.update(lobbyRef, {
      lifecycleState: "closed",
      updatedAt: serverTimestamp,
    });
    transaction.delete(db.collection("lobby_discovery").doc(input.lobbyId));

    const codeHash =
      typeof lobby.joinCodeHash === "string" ?
        lobby.joinCodeHash :
        "";
    if (codeHash) {
      transaction.set(
        db.collection("join_codes").doc(codeHash),
        {
          active: false,
          lookupActive: false,
          joinOpen: false,
          lifecycleState: "closed",
          updatedAt: serverTimestamp,
        },
        {merge: true},
      );
    }

    state.snapshot.lifecycleState = "closed";
    return {snapshot: state.snapshot, applied: true};
  });
}

/**
 * Host-only rule update. A real change increments settingsRevision.
 * @param {UpdateLobbySettingsInput} input Settings update.
 * @return {Promise<{snapshot:LobbySnapshot,applied:boolean}>} Result.
 */
export async function updateLobbySettings(
  input: UpdateLobbySettingsInput,
): Promise<{snapshot: LobbySnapshot; applied: boolean}> {
  validateRuleSettings(input.settings);
  const db = getFirestore();
  const lobbyRef = db.collection("lobbies").doc(input.lobbyId);

  return db.runTransaction(async (transaction) => {
    const state = await readLobbyState(transaction, lobbyRef);
    const lobby = state.lobbyData;

    assertHostWaiting(
      lobby,
      input.uid,
      input.expectedSettingsRevision,
    );

    const normalized = {
      mapId: input.settings.mapId.trim(),
      themeId: input.settings.themeId.trim(),
      roundLimit: input.settings.roundLimit,
      balancedDevelopment: input.settings.balancedDevelopment,
      doublesEnabled: input.settings.doublesEnabled,
      tripleDoublePenaltyEnabled:
        input.settings.tripleDoublePenaltyEnabled,
    };

    const changed =
      lobby.mapId !== normalized.mapId ||
      lobby.themeId !== normalized.themeId ||
      lobby.roundLimit !== normalized.roundLimit ||
      lobby.balancedDevelopment !==
        normalized.balancedDevelopment ||
      lobby.doublesEnabled !== normalized.doublesEnabled ||
      lobby.tripleDoublePenaltyEnabled !==
        normalized.tripleDoublePenaltyEnabled;

    if (!changed) {
      return {
        snapshot: state.snapshot,
        applied: false,
      };
    }

    const nextRevision = input.expectedSettingsRevision + 1;

    transaction.update(lobbyRef, {
      ...normalized,
      settingsRevision: nextRevision,
      updatedAt: FieldValue.serverTimestamp(),
    });

    Object.assign(state.snapshot, normalized);
    state.snapshot.settingsRevision = nextRevision;

    syncPublicDiscovery(
      transaction,
      db,
      state.snapshot,
      {
        ...lobby,
        ...normalized,
        settingsRevision: nextRevision,
      },
    );

    return {
      snapshot: state.snapshot,
      applied: true,
    };
  });
}

/**
 * Validates one host mutation against waiting lifecycle and revision.
 * @param {FirebaseFirestore.DocumentData} lobby Lobby data.
 * @param {string} uid Caller uid.
 * @param {number} expectedSettingsRevision Expected revision.
 */
function assertHostWaiting(
  lobby: FirebaseFirestore.DocumentData,
  uid: string,
  expectedSettingsRevision: number,
): void {
  if (lobby.hostAccountId !== uid) {
    throw new HttpsError(
      "permission-denied",
      "HOST_ONLY",
      {
        errorKey: "lobby.error.host_only",
      },
    );
  }

  if (lobby.lifecycleState !== "waiting") {
    throw new HttpsError(
      "failed-precondition",
      "LOBBY_ALREADY_STARTING",
      {
        errorKey: "lobby.error.not_joinable",
      },
    );
  }

  if (lobby.settingsRevision !== expectedSettingsRevision) {
    throw new HttpsError(
      "aborted",
      "SETTINGS_REVISION_MISMATCH",
      {
        errorKey: "lobby.error.settings_changed",
        currentSettingsRevision: lobby.settingsRevision,
      },
    );
  }
}

/**
 * Validates a host seat policy request.
 * @param {ConfigureLobbySeatsInput} input Seat configuration.
 */
function validateSeatConfiguration(
  input: ConfigureLobbySeatsInput,
): void {
  if (
    !Number.isSafeInteger(input.maxPlayers) ||
    input.maxPlayers < 2 ||
    input.maxPlayers > MAX_PLAYERS
  ) {
    throw invalidRequest("maxPlayers");
  }

  if (
    !Array.isArray(input.seatPolicies) ||
    input.seatPolicies.length !== MAX_PLAYERS
  ) {
    throw invalidRequest("seatPolicies");
  }

  if (input.seatPolicies[0] !== "local_human") {
    throw invalidRequest("seatPolicies[0]");
  }

  for (let index = 1; index < MAX_PLAYERS; index++) {
    const policy = input.seatPolicies[index];
    const active = index < input.maxPlayers;

    if (active) {
      if (
        policy !== "online" &&
        policy !== "local_human" &&
        policy !== "bot"
      ) {
        throw invalidRequest(`seatPolicies[${index}]`);
      }
    } else if (policy !== "inactive") {
      throw invalidRequest(`seatPolicies[${index}]`);
    }
  }
}

/**
 * Converts a requested host policy into concrete stored seat fields.
 * RemoteHuman is preserved when policy remains Online.
 * @param {number} slotIndex Zero-based slot index.
 * @param {LobbySeatPolicy} policy Requested policy.
 * @param {FirebaseFirestore.DocumentData} current Current stored seat.
 * @param {string} hostUid Host account id.
 * @return {FirebaseFirestore.DocumentData} Updated concrete seat fields.
 */
function concreteSeatFields(
  slotIndex: number,
  policy: LobbySeatPolicy,
  current: FirebaseFirestore.DocumentData,
  hostUid: string,
): FirebaseFirestore.DocumentData {
  if (slotIndex === 0) {
    return {
      active: true,
      seatMode: "host_local",
      seatType: "human",
      localOwnerAccountId: hostUid,
      controllerKind: "local_human",
      connectionState: "connected",
      readyForRevision: 0,
    };
  }

  const currentMode = readSeatMode(current.seatMode);
  const hasRemote =
    currentMode === "remote_human" &&
    typeof current.accountId === "string" &&
    current.accountId.length > 0;

  if (hasRemote && policy !== "online") {
    throw new HttpsError(
      "failed-precondition",
      "REMOTE_SEAT_OCCUPIED",
      {
        errorKey: "lobby.error.remote_seat_occupied",
        seatId: current.seatId,
      },
    );
  }

  if (policy === "online") {
    if (hasRemote) {
      return {
        active: true,
        seatMode: "remote_human",
        seatType: "human",
        localOwnerAccountId: "",
        controllerKind: "human",
        connectionState: "connected",
      };
    }

    return {
      active: true,
      seatMode: "open_online",
      seatType: "human",
      accountId: "",
      localOwnerAccountId: "",
      displayName: "",
      pawnCosmeticId: "",
      controllerKind: "none",
      connectionState: "empty",
      readyForRevision: 0,
      joinIdempotencyKey: "",
      joinedAt: null,
    };
  }

  if (policy === "local_human") {
    return {
      active: true,
      seatMode: "local_human",
      seatType: "human",
      accountId: "",
      localOwnerAccountId: hostUid,
      displayName: "",
      controllerKind: "local_human",
      connectionState: "local",
      readyForRevision: 0,
      joinIdempotencyKey: "",
      joinedAt: null,
    };
  }

  if (policy === "bot") {
    return {
      active: true,
      seatMode: "bot",
      seatType: "bot",
      accountId: "",
      localOwnerAccountId: "",
      displayName: "",
      controllerKind: "permanent_bot",
      connectionState: "bot",
      readyForRevision: 0,
      joinIdempotencyKey: "",
      joinedAt: null,
    };
  }

  if (hasRemote) {
    throw new HttpsError(
      "failed-precondition",
      "REMOTE_SEAT_OCCUPIED",
      {
        errorKey: "lobby.error.remote_seat_occupied",
        seatId: current.seatId,
      },
    );
  }

  return {
    active: false,
    seatMode: "inactive",
    seatType: "inactive",
    accountId: "",
    localOwnerAccountId: "",
    displayName: "",
    pawnCosmeticId: "",
    controllerKind: "none",
    connectionState: "inactive",
    readyForRevision: 0,
    joinIdempotencyKey: "",
    joinedAt: null,
  };
}

/**
 * Host-only fixed-slot policy update.
 * @param {ConfigureLobbySeatsInput} input Seat configuration.
 * @return {Promise<{snapshot:LobbySnapshot,applied:boolean}>} Result.
 */
export async function configureLobbySeats(
  input: ConfigureLobbySeatsInput,
): Promise<{snapshot: LobbySnapshot; applied: boolean}> {
  validateSeatConfiguration(input);

  const db = getFirestore();
  const lobbyRef = db.collection("lobbies").doc(input.lobbyId);

  return db.runTransaction(async (transaction) => {
    const state = await readLobbyState(transaction, lobbyRef);
    const lobby = state.lobbyData;

    assertHostWaiting(
      lobby,
      input.uid,
      input.expectedSettingsRevision,
    );

    let changed =
      lobby.maxPlayers !== input.maxPlayers;

    const nextMemberSnapshots = [...state.snapshot.members];

    for (let index = 0; index < MAX_PLAYERS; index++) {
      const member = state.members[index];
      const policy = input.seatPolicies[index];
      const fields = concreteSeatFields(
        index,
        policy,
        member.data,
        input.uid,
      );

      const currentSnapshot = state.snapshot.members[index];
      const nextMode = readSeatMode(fields.seatMode);
      const nextActive = fields.active === true;

      if (
        currentSnapshot.seatMode !== nextMode ||
        currentSnapshot.active !== nextActive
      ) {
        changed = true;
      }

      if (
        currentSnapshot.seatMode === "remote_human" &&
        nextMode === "remote_human"
      ) {
        nextMemberSnapshots[index] = currentSnapshot;
        continue;
      }

      nextMemberSnapshots[index] = memberFromData(
        member.ref.id,
        {
          ...member.data,
          ...fields,
        },
        index,
      );
    }

    if (!changed) {
      return {
        snapshot: state.snapshot,
        applied: false,
      };
    }

    const nextRevision = input.expectedSettingsRevision + 1;
    const serverTimestamp = FieldValue.serverTimestamp();

    transaction.update(lobbyRef, {
      maxPlayers: input.maxPlayers,
      settingsRevision: nextRevision,
      updatedAt: serverTimestamp,
    });

    for (let index = 0; index < MAX_PLAYERS; index++) {
      const member = state.members[index];
      const policy = input.seatPolicies[index];
      const fields = concreteSeatFields(
        index,
        policy,
        member.data,
        input.uid,
      );

      transaction.update(member.ref, {
        ...fields,
        updatedAt: serverTimestamp,
        schemaVersion: LOBBY_SCHEMA_VERSION,
      });
    }

    const lobbyNext = {
      ...lobby,
      maxPlayers: input.maxPlayers,
      settingsRevision: nextRevision,
    };

    const nextSnapshot = buildLobbySnapshot(
      input.lobbyId,
      lobbyNext,
      nextMemberSnapshots,
    );

    syncPublicDiscovery(
      transaction,
      db,
      nextSnapshot,
      lobbyNext,
    );

    return {
      snapshot: nextSnapshot,
      applied: true,
    };
  });
}

/**
 * Host-only remote member removal.
 *
 * The concrete remote seat is reset to OpenOnline and the removed account is
 * recorded under the lobby so the same account cannot immediately rejoin by
 * room code. The roster mutation increments settingsRevision, naturally
 * invalidating all previous Ready acknowledgements.
 * @param {KickLobbyMemberInput} input Kick request.
 * @return {Promise<{snapshot:LobbySnapshot,applied:boolean}>} Result.
 */
export async function kickLobbyMember(
  input: KickLobbyMemberInput,
): Promise<{snapshot: LobbySnapshot; applied: boolean}> {
  if (
    !Number.isSafeInteger(input.slotIndex) ||
    input.slotIndex < 1 ||
    input.slotIndex >= MAX_PLAYERS
  ) {
    throw invalidRequest("slotIndex");
  }

  const db = getFirestore();
  const lobbyRef = db.collection("lobbies").doc(input.lobbyId);

  return db.runTransaction(async (transaction) => {
    const state = await readLobbyState(transaction, lobbyRef);
    const lobby = state.lobbyData;

    assertHostWaiting(
      lobby,
      input.uid,
      input.expectedSettingsRevision,
    );

    const target =
      state.members[input.slotIndex];

    const targetSnapshot =
      state.snapshot.members[input.slotIndex];

    if (
      !target ||
      !targetSnapshot ||
      targetSnapshot.seatMode !== "remote_human" ||
      !targetSnapshot.accountId
    ) {
      throw new HttpsError(
        "failed-precondition",
        "REMOTE_PLAYER_NOT_PRESENT",
        {
          errorKey: "lobby.error.remote_player_not_present",
          slotIndex: input.slotIndex,
        },
      );
    }

    const kickedUid =
      targetSnapshot.accountId;

    const nextRevision =
      input.expectedSettingsRevision + 1;

    const serverTimestamp =
      FieldValue.serverTimestamp();

    transaction.update(target.ref, {
      active: true,
      seatMode: "open_online",
      seatType: "human",
      accountId: "",
      localOwnerAccountId: "",
      displayName: "",
      pawnCosmeticId: "",
      controllerKind: "none",
      connectionState: "empty",
      readyForRevision: 0,
      joinIdempotencyKey: "",
      joinedAt: null,
      updatedAt: serverTimestamp,
      schemaVersion: LOBBY_SCHEMA_VERSION,
    });

    transaction.set(
      lobbyRef.collection("kicks").doc(kickedUid),
      {
        accountId: kickedUid,
        lobbyId: input.lobbyId,
        active: true,
        kickedByAccountId: input.uid,
        kickedAt: serverTimestamp,
        schemaVersion: LOBBY_SCHEMA_VERSION,
      },
      {merge: true},
    );

    transaction.update(lobbyRef, {
      settingsRevision: nextRevision,
      updatedAt: serverTimestamp,
    });

    const nextMembers =
      [...state.snapshot.members];

    nextMembers[input.slotIndex] = {
      ...targetSnapshot,
      seatMode: "open_online",
      seatType: "human",
      accountId: "",
      localOwnerAccountId: "",
      displayName: "",
      pawnCosmeticId: "",
      controllerKind: "none",
      connectionState: "empty",
      readyForRevision: 0,
      requiresReady: false,
    };

    const lobbyNext = {
      ...lobby,
      settingsRevision: nextRevision,
    };

    const nextSnapshot = buildLobbySnapshot(
      input.lobbyId,
      lobbyNext,
      nextMembers,
    );

    syncPublicDiscovery(
      transaction,
      db,
      nextSnapshot,
      lobbyNext,
    );

    return {
      snapshot: nextSnapshot,
      applied: true,
    };
  });
}

/**
 * Voluntary leave for one non-host RemoteHuman in a Waiting lobby.
 * No kick record is created; the same account may rejoin normally later.
 * @param {LeaveLobbyInput} input Leave request.
 * @return {Promise<{snapshot:LobbySnapshot,applied:boolean}>} Result.
 */
export async function leaveLobby(
  input: LeaveLobbyInput,
): Promise<{snapshot: LobbySnapshot; applied: boolean}> {
  const db = getFirestore();
  const lobbyRef = db.collection("lobbies").doc(input.lobbyId);

  return db.runTransaction(async (transaction) => {
    const state = await readLobbyState(transaction, lobbyRef);
    const lobby = state.lobbyData;

    if (lobby.lifecycleState !== "waiting") {
      throw new HttpsError(
        "failed-precondition",
        "LOBBY_NOT_LEAVABLE",
        {errorKey: "lobby.error.not_joinable"},
      );
    }

    if (lobby.hostAccountId === input.uid) {
      throw new HttpsError(
        "failed-precondition",
        "HOST_LEAVE_REQUIRES_MIGRATION",
        {errorKey: "lobby.error.host_leave_not_supported"},
      );
    }

    const targetIndex = state.snapshot.members.findIndex(
      (member) =>
        member.seatMode === "remote_human" &&
        member.accountId === input.uid,
    );

    if (targetIndex < 0) {
      return {snapshot: state.snapshot, applied: false};
    }

    const target = state.members[targetIndex];
    const targetSnapshot = state.snapshot.members[targetIndex];
    const currentRevision =
      typeof lobby.settingsRevision === "number" ?
        lobby.settingsRevision :
        1;
    const nextRevision = currentRevision + 1;
    const serverTimestamp = FieldValue.serverTimestamp();

    transaction.update(target.ref, {
      active: true,
      seatMode: "open_online",
      seatType: "human",
      accountId: "",
      localOwnerAccountId: "",
      displayName: "",
      pawnCosmeticId: "",
      controllerKind: "none",
      connectionState: "empty",
      readyForRevision: 0,
      joinIdempotencyKey: "",
      joinedAt: null,
      updatedAt: serverTimestamp,
      schemaVersion: LOBBY_SCHEMA_VERSION,
    });

    transaction.update(lobbyRef, {
      settingsRevision: nextRevision,
      updatedAt: serverTimestamp,
    });

    const nextMembers = [...state.snapshot.members];
    nextMembers[targetIndex] = {
      ...targetSnapshot,
      seatMode: "open_online",
      seatType: "human",
      accountId: "",
      localOwnerAccountId: "",
      displayName: "",
      pawnCosmeticId: "",
      controllerKind: "none",
      connectionState: "empty",
      readyForRevision: 0,
      requiresReady: false,
    };

    const lobbyNext = {
      ...lobby,
      settingsRevision: nextRevision,
    };

    const nextSnapshot = buildLobbySnapshot(
      input.lobbyId,
      lobbyNext,
      nextMembers,
    );

    syncPublicDiscovery(
      transaction,
      db,
      nextSnapshot,
      lobbyNext,
    );

    return {
      snapshot: nextSnapshot,
      applied: true,
    };
  });
}

/**
 * Gets a lobby snapshot for one authenticated lobby member.
 * @param {GetLobbySnapshotInput} input Snapshot request.
 * @return {Promise<LobbySnapshot>} Snapshot.
 */
export async function getLobbySnapshot(
  input: GetLobbySnapshotInput,
): Promise<LobbySnapshot> {
  const db = getFirestore();
  const lobbyRef = db.collection("lobbies").doc(input.lobbyId);

  return db.runTransaction(async (transaction) => {
    const state = await readLobbyState(transaction, lobbyRef);
    const callerIsMember = state.snapshot.members.some(
      (member) =>
        member.accountId === input.uid &&
        (
          member.seatMode === "host_local" ||
          member.seatMode === "remote_human"
        ),
    );

    if (!callerIsMember) {
      const kickSnapshot = await transaction.get(
        lobbyRef.collection("kicks").doc(input.uid),
      );

      if (kickSnapshot.exists &&
          kickSnapshot.data()?.active === true) {
        throw new HttpsError(
          "permission-denied",
          "LOBBY_KICKED",
          {
            errorKey: "lobby.error.kicked",
          },
        );
      }

      throw new HttpsError(
        "permission-denied",
        "LOBBY_MEMBER_REQUIRED",
        {
          errorKey: "lobby.error.member_required",
        },
      );
    }

    return state.snapshot;
  });
}

/**
 * Updates one lobby seat's cosmetic selection without changing game settings.
 * Remote Humans may update only their own occupied seat. The Host may update
 * HostLocal, LocalHuman, and Bot seats. Open/Inactive seats are not writable.
 * @param {SetLobbyPawnCosmeticInput} input Cosmetic update request.
 * @return {Promise<{snapshot:LobbySnapshot,applied:boolean}>} Result.
 */
export async function setLobbyPawnCosmetic(
  input: SetLobbyPawnCosmeticInput,
): Promise<{snapshot: LobbySnapshot; applied: boolean}> {
  const db = getFirestore();
  const lobbyRef = db.collection("lobbies").doc(input.lobbyId);

  const cosmeticId =
    typeof input.pawnCosmeticId === "string" ?
      input.pawnCosmeticId.trim() :
      "";

  if (
    !Number.isInteger(input.slotIndex) ||
    input.slotIndex < 0 ||
    input.slotIndex >= MAX_PLAYERS ||
    cosmeticId.length < 1 ||
    cosmeticId.length > 80 ||
    !/^[A-Za-z0-9._:-]+$/.test(cosmeticId)
  ) {
    throw new HttpsError(
      "invalid-argument",
      "INVALID_PAWN_COSMETIC",
      {errorKey: "lobby.error.invalid_pawn_cosmetic"},
    );
  }

  return db.runTransaction(async (transaction) => {
    const state = await readLobbyState(transaction, lobbyRef);
    const lobby = state.lobbyData;

    if (lobby.lifecycleState !== "waiting") {
      throw new HttpsError(
        "failed-precondition",
        "LOBBY_COSMETIC_LOCKED",
        {errorKey: "lobby.error.not_joinable"},
      );
    }

    const target = state.members[input.slotIndex];
    const targetSnapshot = state.snapshot.members[input.slotIndex];

    if (!target || !targetSnapshot || !targetSnapshot.active) {
      throw new HttpsError(
        "failed-precondition",
        "PAWN_SEAT_NOT_ACTIVE",
        {errorKey: "lobby.error.invalid_seat"},
      );
    }

    const callerIsHost = lobby.hostAccountId === input.uid;
    const callerOwnsRemote =
      targetSnapshot.seatMode === "remote_human" &&
      targetSnapshot.accountId === input.uid;
    const hostOwnsSeat =
      callerIsHost &&
      (targetSnapshot.seatMode === "host_local" ||
       targetSnapshot.seatMode === "local_human" ||
       targetSnapshot.seatMode === "bot");

    if (!callerOwnsRemote && !hostOwnsSeat) {
      throw new HttpsError(
        "permission-denied",
        "PAWN_COSMETIC_NOT_OWNED",
        {errorKey: "lobby.error.permission_denied"},
      );
    }

    if (targetSnapshot.pawnCosmeticId === cosmeticId) {
      return {snapshot: state.snapshot, applied: false};
    }

    transaction.update(target.ref, {
      pawnCosmeticId: cosmeticId,
      updatedAt: FieldValue.serverTimestamp(),
    });

    targetSnapshot.pawnCosmeticId = cosmeticId;

    return {snapshot: state.snapshot, applied: true};
  });
}

/**
 * Sets Ready for a remote non-host human only. Never starts the match.
 * @param {SetLobbyReadyInput} input Ready request.
 * @return {Promise<{snapshot:LobbySnapshot,applied:boolean}>} Result.
 */
export async function setLobbyReady(
  input: SetLobbyReadyInput,
): Promise<{snapshot: LobbySnapshot; applied: boolean}> {
  const db = getFirestore();
  const lobbyRef = db.collection("lobbies").doc(input.lobbyId);

  return db.runTransaction(async (transaction) => {
    const state = await readLobbyState(transaction, lobbyRef);
    const lobby = state.lobbyData;
    const currentRevision =
      typeof lobby.settingsRevision === "number" ?
        lobby.settingsRevision :
        1;

    if (lobby.lifecycleState !== "waiting") {
      throw new HttpsError(
        "failed-precondition",
        "LOBBY_NOT_READYABLE",
        {
          errorKey: "lobby.error.not_joinable",
        },
      );
    }

    if (currentRevision !== input.expectedSettingsRevision) {
      throw new HttpsError(
        "aborted",
        "SETTINGS_REVISION_MISMATCH",
        {
          errorKey: "lobby.error.settings_changed",
          currentSettingsRevision: currentRevision,
        },
      );
    }

    const callerMember = state.members.find(
      (member) =>
        member.data.accountId === input.uid &&
        member.data.seatMode === "remote_human",
    );

    if (!callerMember) {
      const callerIsHost =
        lobby.hostAccountId === input.uid;

      throw new HttpsError(
        "failed-precondition",
        "READY_NOT_REQUIRED",
        {
          errorKey:
            callerIsHost ?
              "lobby.error.host_does_not_ready" :
              "lobby.error.ready_not_required",
        },
      );
    }

    const nextReadyRevision =
      input.ready ?
        currentRevision :
        0;
    const previousReadyRevision =
      typeof callerMember.data.readyForRevision === "number" ?
        callerMember.data.readyForRevision :
        0;

    if (previousReadyRevision === nextReadyRevision) {
      return {
        snapshot: state.snapshot,
        applied: false,
      };
    }

    transaction.update(callerMember.ref, {
      readyForRevision: nextReadyRevision,
      updatedAt: FieldValue.serverTimestamp(),
    });

    const memberSnapshot = state.snapshot.members.find(
      (member) => member.seatId === callerMember.ref.id,
    );

    if (memberSnapshot) {
      memberSnapshot.readyForRevision = nextReadyRevision;
    }

    return {
      snapshot: state.snapshot,
      applied: true,
    };
  });
}

/**
 * Validates the final host-start gate.
 * @param {LobbyTransactionState} state Lobby transaction state.
 * @param {number} currentRevision Current settings revision.
 */
function assertHostCanStart(
  state: LobbyTransactionState,
  currentRevision: number,
): void {
  for (const member of state.snapshot.members) {
    if (!member.active) {
      continue;
    }

    if (
      member.seatMode === "remote_human" &&
      member.readyForRevision !== currentRevision
    ) {
      throw new HttpsError(
        "failed-precondition",
        "REMOTE_PLAYER_NOT_READY",
        {
          errorKey: "lobby.error.remote_player_not_ready",
          seatId: member.seatId,
        },
      );
    }

    if (member.seatMode === "inactive") {
      throw new HttpsError(
        "internal",
        "ACTIVE_SEAT_STATE_INVALID",
        {
          errorKey: "lobby.error.service_unavailable",
          seatId: member.seatId,
        },
      );
    }
  }
}

/**
 * Converts any unresolved active OpenOnline seats into Bots at the exact
 * authoritative Start transaction. This lets a host start immediately without
 * waiting for every optional online slot while still preserving maxPlayers.
 * @param {LobbyTransactionState} state Current transaction state.
 * @param {FirebaseFirestore.Transaction} transaction Firestore transaction.
 * @param {FirebaseFirestore.FieldValue} serverTimestamp Server timestamp.
 * @return {LobbyMemberSnapshot[]} Concrete members used for match bootstrap.
 */
function fillOpenOnlineSeatsWithBots(
  state: LobbyTransactionState,
  transaction: FirebaseFirestore.Transaction,
  serverTimestamp: FirebaseFirestore.FieldValue,
): LobbyMemberSnapshot[] {
  const startMembers =
    state.snapshot.members.map((member) => ({...member}));

  for (let index = 0; index < startMembers.length; index++) {
    const member = startMembers[index];

    if (
      !member.active ||
      member.seatMode !== "open_online"
    ) {
      continue;
    }

    const storedMember =
      state.members[index];

    transaction.update(storedMember.ref, {
      active: true,
      seatMode: "bot",
      seatType: "bot",
      accountId: "",
      localOwnerAccountId: "",
      displayName: "",
      controllerKind: "permanent_bot",
      connectionState: "bot",
      readyForRevision: 0,
      joinIdempotencyKey: "",
      joinedAt: null,
      updatedAt: serverTimestamp,
      schemaVersion: LOBBY_SCHEMA_VERSION,
    });

    Object.assign(member, {
      seatMode: "bot",
      seatType: "bot",
      accountId: "",
      localOwnerAccountId: "",
      displayName: "",
      controllerKind: "permanent_bot",
      connectionState: "bot",
      readyForRevision: 0,
      requiresReady: false,
    });
  }

  return startMembers;
}

/**
 * Creates match-seat bootstrap data from a lobby seat.
 * @param {LobbyMemberSnapshot} member Lobby member.
 * @param {string} hostUid Host account id.
 * @param {string} lobbyId Source lobby.
 * @return {FirebaseFirestore.DocumentData} Match seat data.
 */
function createMatchSeatData(
  member: LobbyMemberSnapshot,
  hostUid: string,
  lobbyId: string,
): FirebaseFirestore.DocumentData {
  return {
    seatId: member.seatId,
    slotIndex: member.slotIndex,
    seatMode: member.seatMode,
    accountId: member.accountId,
    localOwnerAccountId:
      member.seatMode === "local_human" ||
      member.seatMode === "host_local" ?
        hostUid :
        "",
    displayName: member.displayName,
    isHost: member.isHost,
    controllerKind: member.controllerKind,
    connectionState: member.connectionState,
    sourceLobbyId: lobbyId,
    schemaVersion: LOBBY_SCHEMA_VERSION,
    createdAt: FieldValue.serverTimestamp(),
    updatedAt: FieldValue.serverTimestamp(),
  };
}

/**
 * Host-only authoritative Waiting -> Starting transition.
 * @param {StartLobbyMatchInput} input Start request.
 * @return {Promise<{snapshot:LobbySnapshot,started:boolean,
 * idempotentReplay:boolean}>} Result.
 */
export async function startLobbyMatch(
  input: StartLobbyMatchInput,
): Promise<{
  snapshot: LobbySnapshot;
  started: boolean;
  idempotentReplay: boolean;
}> {
  const db = getFirestore();
  const lobbyRef = db.collection("lobbies").doc(input.lobbyId);

  return db.runTransaction(async (transaction) => {
    const state = await readLobbyState(transaction, lobbyRef);
    const lobby = state.lobbyData;

    if (lobby.hostAccountId !== input.uid) {
      throw new HttpsError(
        "permission-denied",
        "HOST_ONLY",
        {
          errorKey: "lobby.error.host_only",
        },
      );
    }

    if (lobby.lifecycleState === "starting") {
      return {
        snapshot: state.snapshot,
        started: true,
        idempotentReplay: true,
      };
    }

    if (lobby.lifecycleState !== "waiting") {
      throw new HttpsError(
        "failed-precondition",
        "LOBBY_NOT_STARTABLE",
        {
          errorKey: "lobby.error.not_joinable",
        },
      );
    }

    const currentRevision =
      typeof lobby.settingsRevision === "number" ?
        lobby.settingsRevision :
        1;

    if (currentRevision !== input.expectedSettingsRevision) {
      throw new HttpsError(
        "aborted",
        "SETTINGS_REVISION_MISMATCH",
        {
          errorKey: "lobby.error.settings_changed",
          currentSettingsRevision: currentRevision,
        },
      );
    }

    assertHostCanStart(
      state,
      currentRevision,
    );

    const matchRef = db.collection("matches").doc();
    const startEventId = db.collection("_ids").doc().id;
    const serverTimestamp = FieldValue.serverTimestamp();
    const startCountdownEndsAtEpochMs = Date.now() + 5000;

    const startMembers =
      fillOpenOnlineSeatsWithBots(
        state,
        transaction,
        serverTimestamp,
      );

    transaction.update(lobbyRef, {
      lifecycleState: "starting",
      matchId: matchRef.id,
      startEventId,
      startCountdownEndsAtEpochMs,
      startedAt: serverTimestamp,
      updatedAt: serverTimestamp,
    });

    const codeHash =
      typeof lobby.joinCodeHash === "string" ?
        lobby.joinCodeHash :
        "";

    if (codeHash) {
      transaction.update(
        db.collection("join_codes").doc(codeHash),
        {
          // Keep the protected hash lookup alive for the lifetime of the
          // session. Normal lobby joins are closed by lifecycle/joinOpen,
          // while a future reconnect flow can still resolve this match
          // without ever storing the raw six-digit code.
          active: true,
          lookupActive: true,
          joinOpen: false,
          matchId: matchRef.id,
          lifecycleState: "starting",
          updatedAt: serverTimestamp,
        },
      );
    }

    transaction.create(matchRef, {
      matchId: matchRef.id,
      lobbyId: input.lobbyId,
      hostAccountId: lobby.hostAccountId,
      mode: "online",
      status: "starting",
      startEventId,
      startCountdownEndsAtEpochMs,
      settingsRevision: currentRevision,
      mapId: lobby.mapId,
      themeId: lobby.themeId,
      roundLimit: lobby.roundLimit,
      maxPlayers: lobby.maxPlayers,
      balancedDevelopment: lobby.balancedDevelopment,
      doublesEnabled: lobby.doublesEnabled,
      tripleDoublePenaltyEnabled:
        lobby.tripleDoublePenaltyEnabled,
      gameVersion: lobby.gameVersion,
      protocolVersion: lobby.protocolVersion,
      rulesVersion: lobby.rulesVersion,
      contentVersion: lobby.contentVersion,
      regionId: lobby.regionId,
      seatSchemaVersion: LOBBY_SCHEMA_VERSION,
      schemaVersion: LOBBY_SCHEMA_VERSION,
      createdAt: serverTimestamp,
      updatedAt: serverTimestamp,
    });

    for (const member of startMembers) {
      if (!member.active) {
        continue;
      }

      transaction.create(
        matchRef.collection("seats").doc(member.seatId),
        createMatchSeatData(
          member,
          lobby.hostAccountId,
          input.lobbyId,
        ),
      );
    }

    transaction.create(
      matchRef.collection("network").doc("state"),
      {
        revision: 0,
        phase: "starting",
        turnSeatId: "",
        eventSequence: 0,
        snapshotJson: "{}",
        authorityHostAccountId:
          lobby.hostAccountId,
        schemaVersion: 1,
        createdAt: serverTimestamp,
        updatedAt: serverTimestamp,
      },
    );

    const lobbyNext = {
      ...lobby,
      lifecycleState: "starting",
      matchId: matchRef.id,
      startEventId,
      startCountdownEndsAtEpochMs,
    };

    const startSnapshot =
      buildLobbySnapshot(
        input.lobbyId,
        lobbyNext,
        startMembers,
      );

    syncPublicDiscovery(
      transaction,
      db,
      startSnapshot,
      lobbyNext,
    );

    return {
      snapshot: startSnapshot,
      started: true,
      idempotentReplay: false,
    };
  });
}
