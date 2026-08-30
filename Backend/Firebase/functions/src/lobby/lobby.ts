
import {createHmac, randomInt} from "crypto";
import {
  FieldValue,
  getFirestore,
} from "firebase-admin/firestore";
import {HttpsError} from "firebase-functions/v2/https";

const LOBBY_SCHEMA_VERSION = 1;
const JOIN_CODE_DIGITS = 6;
const MAX_PLAYERS = 4;
const INVALID_ATTEMPT_LIMIT = 5;
const INVALID_ATTEMPT_WINDOW_MS = 5 * 60 * 1000;
const INVALID_ATTEMPT_BLOCK_MS = 60 * 1000;
const LOCAL_JOIN_CODE_PEPPER =
  "atlasboard-local-emulator-lobby-code-pepper-v1";

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
  requiredHumanPlayers: number;
  balancedDevelopment: boolean;
  doublesEnabled: boolean;
  tripleDoublePenaltyEnabled: boolean;
}

export interface CreateLobbyInput {
  uid: string;
  settings: LobbySettingsInput;
  versions: LobbyVersionInfo;
}

export interface JoinLobbyInput {
  uid: string;
  roomCode: string;
  idempotencyKey: string;
  versions: LobbyVersionInfo;
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

export interface SetLobbyReadyInput {
  uid: string;
  lobbyId: string;
  expectedSettingsRevision: number;
  ready: boolean;
}

export interface GetLobbySnapshotInput {
  uid: string;
  lobbyId: string;
}

export interface LobbyMemberSnapshot {
  seatId: string;
  slotIndex: number;
  seatType: "human" | "bot";
  accountId: string;
  displayName: string;
  isHost: boolean;
  connectionState: string;
  controllerKind: string;
  readyForRevision: number;
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

  if (
    !Number.isSafeInteger(settings.requiredHumanPlayers) ||
    settings.requiredHumanPlayers < 1 ||
    settings.requiredHumanPlayers > settings.maxPlayers
  ) {
    throw invalidRequest("requiredHumanPlayers");
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
  return {
    seatId,
    slotIndex:
      typeof data.slotIndex === "number" ?
        data.slotIndex :
        fallbackIndex,
    seatType: data.seatType === "bot" ? "bot" : "human",
    accountId:
      typeof data.accountId === "string" ?
        data.accountId :
        "",
    displayName:
      typeof data.displayName === "string" ?
        data.displayName :
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
  };
}

/**
 * Builds a stable lobby snapshot from data already read in the transaction.
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
    mapId: typeof lobby.mapId === "string" ? lobby.mapId : "",
    themeId: typeof lobby.themeId === "string" ? lobby.themeId : "",
    roundLimit:
      typeof lobby.roundLimit === "number" ?
        lobby.roundLimit :
        20,
    maxPlayers:
      typeof lobby.maxPlayers === "number" ?
        lobby.maxPlayers :
        MAX_PLAYERS,
    requiredHumanPlayers:
      typeof lobby.requiredHumanPlayers === "number" ?
        lobby.requiredHumanPlayers :
        1,
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
    members,
  };
}

/**
 * Reads a lobby and all of its fixed seat documents before any writes.
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
  const maxPlayers =
    typeof lobbyData.maxPlayers === "number" ?
      lobbyData.maxPlayers :
      MAX_PLAYERS;

  const seatRefs: FirebaseFirestore.DocumentReference[] = [];
  for (let index = 0; index < maxPlayers; index++) {
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
 * Creates a private lobby and protected code lookup.
 * @param {CreateLobbyInput} input Create request.
 * @return {Promise<{roomCode:string,snapshot:LobbySnapshot}>} Result.
 */
export async function createPrivateLobby(
  input: CreateLobbyInput,
): Promise<{roomCode: string; snapshot: LobbySnapshot}> {
  validateCreateSettings(input.settings);
  validateVersions(input.versions);
  const account = await loadActiveAccount(input.uid);

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
        const lobbyData = {
          lobbyId: lobbyRef.id,
          hostAccountId: input.uid,
          visibility: "private",
          lifecycleState: "waiting",
          settingsRevision: 1,
          mapId: input.settings.mapId.trim(),
          themeId: input.settings.themeId.trim(),
          roundLimit: input.settings.roundLimit,
          maxPlayers: input.settings.maxPlayers,
          requiredHumanPlayers: input.settings.requiredHumanPlayers,
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
          matchId: "",
          startEventId: "",
          schemaVersion: LOBBY_SCHEMA_VERSION,
          createdAt: serverTimestamp,
          updatedAt: serverTimestamp,
        };

        const memberSnapshots: LobbyMemberSnapshot[] = [];

        transaction.create(lobbyRef, lobbyData);

        for (
          let slotIndex = 0;
          slotIndex < input.settings.maxPlayers;
          slotIndex++
        ) {
          const seatRef = lobbyRef
            .collection("members")
            .doc(`seat_${slotIndex + 1}`);

          const isHumanSeat =
            slotIndex < input.settings.requiredHumanPlayers;
          const isHost = slotIndex === 0;

          const seatData = {
            seatId: seatRef.id,
            slotIndex,
            seatType: isHumanSeat ? "human" : "bot",
            accountId: isHost ? input.uid : "",
            displayName: isHost ? account.displayName : "",
            isHost,
            controllerKind:
              isHost ?
                "human" :
                isHumanSeat ?
                  "none" :
                  "permanent_bot",
            connectionState: isHost ? "connected" : "empty",
            readyForRevision: 0,
            joinIdempotencyKey: "",
            joinedAt: isHost ? serverTimestamp : null,
            updatedAt: serverTimestamp,
            schemaVersion: LOBBY_SCHEMA_VERSION,
          };

          transaction.create(seatRef, seatData);
          memberSnapshots.push(
            memberFromData(seatRef.id, seatData, slotIndex),
          );
        }

        transaction.create(codeRef, {
          lobbyId: lobbyRef.id,
          active: true,
          schemaVersion: LOBBY_SCHEMA_VERSION,
          createdAt: serverTimestamp,
          updatedAt: serverTimestamp,
        });

        return buildLobbySnapshot(
          lobbyRef.id,
          lobbyData,
          memberSnapshots,
        );
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
 * Clears invalid-code attempt state after a valid code lookup.
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
 * Atomically resolves a private room code and reserves a human seat.
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

  if (!codeSnapshot.exists || codeSnapshot.data()?.active !== true) {
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

    for (const member of state.members) {
      if (member.data.accountId !== input.uid) {
        continue;
      }

      if (member.data.joinIdempotencyKey === input.idempotencyKey) {
        return {
          snapshot: state.snapshot,
          idempotentReplay: true,
        };
      }

      throw new HttpsError(
        "already-exists",
        "ACCOUNT_ALREADY_IN_LOBBY",
        {
          errorKey: "lobby.error.already_joined",
        },
      );
    }

    const openSeat = state.members.find(
      (member) =>
        member.data.seatType === "human" &&
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
      accountId: input.uid,
      displayName: account.displayName,
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
      member.accountId = input.uid;
      member.displayName = account.displayName;
      member.controllerKind = "human";
      member.connectionState = "connected";
      member.readyForRevision = 0;
    }

    return {
      snapshot: state.snapshot,
      idempotentReplay: false,
    };
  });

  await clearJoinAttemptState(input.uid);
  return result;
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

    if (lobby.hostAccountId !== input.uid) {
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

    if (lobby.settingsRevision !== input.expectedSettingsRevision) {
      throw new HttpsError(
        "aborted",
        "SETTINGS_REVISION_MISMATCH",
        {
          errorKey: "lobby.error.settings_changed",
          currentSettingsRevision: lobby.settingsRevision,
        },
      );
    }

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

    return {
      snapshot: state.snapshot,
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
        member.seatType === "human",
    );

    if (!callerIsMember) {
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
 * Sets readyForRevision and performs the single authoritative start transition.
 * @param {SetLobbyReadyInput} input Ready request.
 * @return {Promise<{snapshot:LobbySnapshot,started:boolean}>} Result.
 */
export async function setLobbyReady(
  input: SetLobbyReadyInput,
): Promise<{snapshot: LobbySnapshot; started: boolean}> {
  const db = getFirestore();
  const lobbyRef = db.collection("lobbies").doc(input.lobbyId);

  return db.runTransaction(async (transaction) => {
    const state = await readLobbyState(transaction, lobbyRef);
    const lobby = state.lobbyData;
    const currentRevision =
      typeof lobby.settingsRevision === "number" ?
        lobby.settingsRevision :
        1;

    const callerMember = state.members.find(
      (member) => member.data.accountId === input.uid,
    );

    if (!callerMember) {
      throw new HttpsError(
        "permission-denied",
        "LOBBY_MEMBER_REQUIRED",
        {
          errorKey: "lobby.error.member_required",
        },
      );
    }

    if (lobby.lifecycleState === "starting") {
      return {
        snapshot: state.snapshot,
        started: true,
      };
    }

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

    const nextReadyRevision = input.ready ? currentRevision : 0;

    const callerSnapshot = state.snapshot.members.find(
      (member) => member.seatId === callerMember.ref.id,
    );
    if (callerSnapshot) {
      callerSnapshot.readyForRevision = nextReadyRevision;
    }

    let occupiedRequiredHumans = 0;
    let readyRequiredHumans = 0;

    for (const member of state.snapshot.members) {
      if (member.seatType !== "human" || !member.accountId) {
        continue;
      }

      occupiedRequiredHumans++;

      if (member.readyForRevision === currentRevision) {
        readyRequiredHumans++;
      }
    }

    const shouldStart =
      occupiedRequiredHumans === state.snapshot.requiredHumanPlayers &&
      readyRequiredHumans === state.snapshot.requiredHumanPlayers;

    transaction.update(callerMember.ref, {
      readyForRevision: nextReadyRevision,
      updatedAt: FieldValue.serverTimestamp(),
    });

    if (!shouldStart) {
      transaction.update(lobbyRef, {
        updatedAt: FieldValue.serverTimestamp(),
      });

      return {
        snapshot: state.snapshot,
        started: false,
      };
    }

    const matchRef = db.collection("matches").doc();
    const startEventId = db.collection("_ids").doc().id;
    const serverTimestamp = FieldValue.serverTimestamp();

    transaction.update(lobbyRef, {
      lifecycleState: "starting",
      matchId: matchRef.id,
      startEventId,
      startedAt: serverTimestamp,
      updatedAt: serverTimestamp,
    });

    transaction.create(matchRef, {
      matchId: matchRef.id,
      lobbyId: input.lobbyId,
      hostAccountId: lobby.hostAccountId,
      mode: "online",
      status: "starting",
      startEventId,
      settingsRevision: currentRevision,
      mapId: lobby.mapId,
      themeId: lobby.themeId,
      roundLimit: lobby.roundLimit,
      maxPlayers: lobby.maxPlayers,
      requiredHumanPlayers: lobby.requiredHumanPlayers,
      balancedDevelopment: lobby.balancedDevelopment,
      doublesEnabled: lobby.doublesEnabled,
      tripleDoublePenaltyEnabled:
        lobby.tripleDoublePenaltyEnabled,
      gameVersion: lobby.gameVersion,
      protocolVersion: lobby.protocolVersion,
      rulesVersion: lobby.rulesVersion,
      contentVersion: lobby.contentVersion,
      regionId: lobby.regionId,
      schemaVersion: LOBBY_SCHEMA_VERSION,
      createdAt: serverTimestamp,
      updatedAt: serverTimestamp,
    });

    state.snapshot.lifecycleState = "starting";
    state.snapshot.matchId = matchRef.id;
    state.snapshot.startEventId = startEventId;

    return {
      snapshot: state.snapshot,
      started: true,
    };
  });
}
