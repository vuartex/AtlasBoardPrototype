import {
  createHash,
  createHmac,
  timingSafeEqual,
} from "crypto";
import {
  FieldPath,
  FieldValue,
  getFirestore,
} from "firebase-admin/firestore";
import {HttpsError} from "firebase-functions/v2/https";

const PUBLIC_DISCOVERY_CURSOR_VERSION = 1;
const PUBLIC_DISCOVERY_CHUNK_SIZE = 50;
const PUBLIC_DISCOVERY_MAX_SCAN_DOCS = 250;
const PUBLIC_BROWSER_MAX_RESULTS = 25;
const HOST_HEARTBEAT_STALE_MS = 30 * 1000;
const MAX_CURSOR_LENGTH = 1024;
const LOCAL_CURSOR_PEPPER =
  "atlasboard-local-emulator-lobby-code-pepper-v1";

export interface PublicLobbyVersionInfo {
  gameVersion: string;
  protocolVersion: number;
  rulesVersion: number;
  contentVersion: string;
  regionId: string;
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

export interface PublicLobbyPageInput {
  uid: string;
  versions: PublicLobbyVersionInfo;
  limit: number;
  cursor?: string;
}

export interface PublicLobbyPageDiagnostics {
  scannedDocuments: number;
  queryCount: number;
  compatibleRooms: number;
  staleDocuments: number;
  scanLimitReached: boolean;
}

export interface PublicLobbyPageResult {
  rooms: PublicLobbyCard[];
  nextCursor: string;
  hasMore: boolean;
  diagnostics: PublicLobbyPageDiagnostics;
}

interface PublicLobbyCursorPayload {
  v: number;
  createdAtEpochMs: number;
  documentId: string;
  filterHash: string;
}

interface RawCursorPosition {
  createdAtEpochMs: number;
  documentId: string;
}

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

function validateVersions(versions: PublicLobbyVersionInfo): void {
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

async function assertActiveAccount(uid: string): Promise<void> {
  const db = getFirestore();
  const [userSnapshot] = await Promise.all([
    db.collection("users").doc(uid).get(),
    db.collection("public_profiles").doc(uid).get(),
  ]);

  if (!userSnapshot.exists) {
    throw new HttpsError(
      "failed-precondition",
      "ACCOUNT_PROFILE_REQUIRED",
      {errorKey: "lobby.error.account_required"},
    );
  }

  const user = userSnapshot.data() ?? {};
  if (user.accountStatus !== "active") {
    throw new HttpsError(
      "permission-denied",
      "ACCOUNT_NOT_ACTIVE",
      {errorKey: "lobby.error.account_not_active"},
    );
  }

  // Preserve the existing list-public-rooms account/profile read contract.
  // A missing public profile is tolerated by the legacy implementation.
}

function getCursorSecret(): string {
  if (process.env.FUNCTIONS_EMULATOR === "true") {
    return LOCAL_CURSOR_PEPPER;
  }

  const pepper = process.env.ATLAS_JOIN_CODE_PEPPER;
  if (!pepper || pepper.length < 32) {
    throw new HttpsError(
      "failed-precondition",
      "PUBLIC_LOBBY_CURSOR_SECRET_NOT_CONFIGURED",
      {errorKey: "lobby.error.service_unavailable"},
    );
  }

  return pepper;
}

function normalizedFilterHash(
  versions: PublicLobbyVersionInfo,
): string {
  return createHash("sha256")
    .update(
      [
        versions.gameVersion.trim(),
        String(versions.protocolVersion),
        String(versions.rulesVersion),
        versions.contentVersion.trim(),
        versions.regionId.trim(),
      ].join("\u001f"),
      "utf8",
    )
    .digest("hex");
}

function cursorSignature(encodedPayload: string): Buffer {
  return createHmac("sha256", getCursorSecret())
    .update(`atlasboard:public-lobby-cursor:v1:${encodedPayload}`, "utf8")
    .digest();
}

function encodeCursor(
  position: RawCursorPosition,
  filterHash: string,
): string {
  const payload: PublicLobbyCursorPayload = {
    v: PUBLIC_DISCOVERY_CURSOR_VERSION,
    createdAtEpochMs: position.createdAtEpochMs,
    documentId: position.documentId,
    filterHash,
  };

  const encodedPayload = Buffer
    .from(JSON.stringify(payload), "utf8")
    .toString("base64url");
  const signature = cursorSignature(encodedPayload)
    .toString("base64url");

  return `${encodedPayload}.${signature}`;
}

function decodeCursor(
  cursor: string | undefined,
  expectedFilterHash: string,
): RawCursorPosition | null {
  if (!cursor) {
    return null;
  }

  if (
    typeof cursor !== "string" ||
    cursor.length < 8 ||
    cursor.length > MAX_CURSOR_LENGTH
  ) {
    throw invalidRequest("cursor");
  }

  const parts = cursor.split(".");
  if (parts.length !== 2 || !parts[0] || !parts[1]) {
    throw invalidRequest("cursor");
  }

  try {
    const expectedSignature = cursorSignature(parts[0]);
    const suppliedSignature = Buffer.from(parts[1], "base64url");

    if (
      suppliedSignature.length !== expectedSignature.length ||
      !timingSafeEqual(suppliedSignature, expectedSignature)
    ) {
      throw invalidRequest("cursor");
    }

    const parsed = JSON.parse(
      Buffer.from(parts[0], "base64url").toString("utf8"),
    ) as Partial<PublicLobbyCursorPayload>;

    if (
      parsed.v !== PUBLIC_DISCOVERY_CURSOR_VERSION ||
      !Number.isSafeInteger(parsed.createdAtEpochMs) ||
      (parsed.createdAtEpochMs ?? 0) < 0 ||
      typeof parsed.documentId !== "string" ||
      parsed.documentId.length < 1 ||
      parsed.documentId.length > 256 ||
      typeof parsed.filterHash !== "string" ||
      parsed.filterHash !== expectedFilterHash
    ) {
      throw invalidRequest("cursor");
    }

    return {
      createdAtEpochMs: parsed.createdAtEpochMs as number,
      documentId: parsed.documentId,
    };
  } catch (error) {
    if (error instanceof HttpsError) {
      throw error;
    }

    throw invalidRequest("cursor");
  }
}

function hostHeartbeatEpochMs(
  data: FirebaseFirestore.DocumentData,
): number {
  if (typeof data.hostHeartbeatAtEpochMs === "number") {
    return data.hostHeartbeatAtEpochMs;
  }
  if (typeof data.updatedAtEpochMs === "number") {
    return data.updatedAtEpochMs;
  }
  if (typeof data.createdAtEpochMs === "number") {
    return data.createdAtEpochMs;
  }
  return 0;
}

function hostHeartbeatIsStale(
  data: FirebaseFirestore.DocumentData,
  now: number,
): boolean {
  const heartbeat = hostHeartbeatEpochMs(data);
  return heartbeat <= 0 || now - heartbeat > HOST_HEARTBEAT_STALE_MS;
}

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

function isCompatible(
  card: PublicLobbyCard,
  versions: PublicLobbyVersionInfo,
): boolean {
  if (
    card.gameVersion !== versions.gameVersion ||
    card.protocolVersion !== versions.protocolVersion ||
    card.rulesVersion !== versions.rulesVersion ||
    card.contentVersion !== versions.contentVersion
  ) {
    return false;
  }

  const requestedRegion = versions.regionId.trim();
  return requestedRegion === "auto" ||
    card.regionId === "auto" ||
    card.regionId === requestedRegion;
}

async function closeStaleWaitingLobbyIfNeeded(
  lobbyId: string,
  now: number,
): Promise<boolean> {
  const db = getFirestore();
  const lobbyRef = db.collection("lobbies").doc(lobbyId);

  return db.runTransaction(async (transaction) => {
    const snapshot = await transaction.get(lobbyRef);

    if (!snapshot.exists) {
      transaction.delete(
        db.collection("lobby_discovery").doc(lobbyId),
      );
      return true;
    }

    const lobby = snapshot.data() ?? {};
    if (
      lobby.lifecycleState !== "waiting" ||
      !hostHeartbeatIsStale(lobby, now)
    ) {
      return false;
    }

    const timestamp = FieldValue.serverTimestamp();
    transaction.update(
      lobbyRef,
      {
        lifecycleState: "closed",
        updatedAt: timestamp,
      },
    );
    transaction.delete(
      db.collection("lobby_discovery").doc(lobbyId),
    );

    const codeHash =
      typeof lobby.joinCodeHash === "string" ? lobby.joinCodeHash : "";
    if (codeHash) {
      transaction.set(
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

    return true;
  });
}

/**
 * Cursor-based public-lobby discovery. The cursor advances by the last raw
 * discovery document scanned, not the last compatible card returned. This is
 * what prevents incompatible/stale documents from creating stable-data skips.
 */
export async function listPublicLobbyPage(
  input: PublicLobbyPageInput,
): Promise<PublicLobbyPageResult> {
  validateVersions(input.versions);
  await assertActiveAccount(input.uid);

  const requestedLimit =
    Number.isSafeInteger(input.limit) ? input.limit : 20;
  const resultLimit = Math.min(
    PUBLIC_BROWSER_MAX_RESULTS,
    Math.max(1, requestedLimit),
  );

  const filterHash = normalizedFilterHash(input.versions);
  let position = decodeCursor(input.cursor, filterHash);
  const db = getFirestore();
  const rooms: PublicLobbyCard[] = [];
  const seenLobbyIds = new Set<string>();
  const staleLobbyIds = new Set<string>();
  const now = Date.now();

  let scannedDocuments = 0;
  let queryCount = 0;
  let sourceHasMore = false;
  let stoppedInsideChunk = false;
  let lastScanned: RawCursorPosition | null = position;

  while (
    rooms.length < resultLimit &&
    scannedDocuments < PUBLIC_DISCOVERY_MAX_SCAN_DOCS
  ) {
    const remainingScanBudget =
      PUBLIC_DISCOVERY_MAX_SCAN_DOCS - scannedDocuments;
    const chunkSize = Math.min(
      PUBLIC_DISCOVERY_CHUNK_SIZE,
      remainingScanBudget,
    );

    let query: FirebaseFirestore.Query = db
      .collection("lobby_discovery")
      .orderBy("createdAtEpochMs", "desc")
      .orderBy(FieldPath.documentId(), "desc");

    if (position) {
      query = query.startAfter(
        position.createdAtEpochMs,
        db.collection("lobby_discovery").doc(position.documentId),
      );
    }

    const snapshot = await query.limit(chunkSize + 1).get();
    queryCount++;

    const docsToScan = snapshot.docs.slice(0, chunkSize);
    sourceHasMore = snapshot.docs.length > chunkSize;
    stoppedInsideChunk = false;

    if (docsToScan.length === 0) {
      sourceHasMore = false;
      break;
    }

    for (let index = 0; index < docsToScan.length; index++) {
      const document = docsToScan[index];
      const discovery = document.data();
      const createdAtEpochMs = discovery.createdAtEpochMs;

      // The ordered query should only surface documents carrying this field.
      // Fail closed if malformed state somehow reaches this point.
      if (!Number.isSafeInteger(createdAtEpochMs)) {
        scannedDocuments++;
        continue;
      }

      lastScanned = {
        createdAtEpochMs,
        documentId: document.id,
      };
      position = lastScanned;
      scannedDocuments++;

      if (hostHeartbeatIsStale(discovery, now)) {
        staleLobbyIds.add(document.id);
        continue;
      }

      const card = discoveryCardFromData(discovery);
      if (!card || !isCompatible(card, input.versions)) {
        continue;
      }

      if (seenLobbyIds.has(card.lobbyId)) {
        continue;
      }

      seenLobbyIds.add(card.lobbyId);
      rooms.push(card);

      if (rooms.length >= resultLimit) {
        stoppedInsideChunk = index < docsToScan.length - 1;
        break;
      }
    }

    if (rooms.length >= resultLimit) {
      sourceHasMore = stoppedInsideChunk || sourceHasMore;
      break;
    }

    if (docsToScan.length < chunkSize || !sourceHasMore) {
      sourceHasMore = false;
      break;
    }
  }

  const scanLimitReached =
    scannedDocuments >= PUBLIC_DISCOVERY_MAX_SCAN_DOCS &&
    sourceHasMore;
  const hasMore = Boolean(lastScanned) &&
    (sourceHasMore || stoppedInsideChunk || scanLimitReached);

  // Keep legacy stale-room cleanup semantics without letting cleanup failure
  // fail a read-only browser page. Cap cleanup work per request so pagination
  // stays bounded even if old emulator/test data is unusually dirty.
  await Promise.all(
    Array.from(staleLobbyIds)
      .slice(0, PUBLIC_DISCOVERY_CHUNK_SIZE)
      .map((lobbyId) =>
        closeStaleWaitingLobbyIfNeeded(lobbyId, now)
          .catch(() => false),
      ),
  );

  return {
    rooms,
    nextCursor:
      hasMore && lastScanned ?
        encodeCursor(lastScanned, filterHash) :
        "",
    hasMore,
    diagnostics: {
      scannedDocuments,
      queryCount,
      compatibleRooms: rooms.length,
      staleDocuments: staleLobbyIds.size,
      scanLimitReached,
    },
  };
}
