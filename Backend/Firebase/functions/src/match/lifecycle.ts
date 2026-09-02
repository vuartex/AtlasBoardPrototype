import {
  FieldValue,
  getFirestore,
} from "firebase-admin/firestore";
import {HttpsError} from "firebase-functions/v2/https";
import {getMatchNetworkSnapshot} from "./network";

const RECONNECT_WINDOW_MS = 5 * 60 * 1000;
const MAX_PLAYERS = 4;

interface LifecycleContext {
  matchRef: FirebaseFirestore.DocumentReference;
  matchData: FirebaseFirestore.DocumentData;
  seats: FirebaseFirestore.QueryDocumentSnapshot[];
  isHost: boolean;
  localSeat: FirebaseFirestore.QueryDocumentSnapshot | null;
}

/**
 * Validates a match id supplied to lifecycle callables.
 * @param {unknown} value Candidate match id.
 * @return {string} Validated match id.
 */
function requireMatchId(value: unknown): string {
  if (
    typeof value !== "string" ||
    value.length < 8 ||
    value.length > 128
  ) {
    throw new HttpsError(
      "invalid-argument",
      "INVALID_MATCH_ID",
      {errorKey: "match.error.invalid_request"},
    );
  }

  return value;
}

/**
 * Validates a stable player slot index.
 * @param {unknown} value Candidate slot index.
 * @return {number} Validated slot index.
 */
function requireSlotIndex(value: unknown): number {
  if (
    typeof value !== "number" ||
    !Number.isSafeInteger(value) ||
    value < 0 ||
    value >= MAX_PLAYERS
  ) {
    throw new HttpsError(
      "invalid-argument",
      "INVALID_SLOT_INDEX",
      {errorKey: "match.error.invalid_request"},
    );
  }

  return value;
}

/**
 * Loads the protected match, seats, and caller membership context.
 * @param {string} uid Authenticated account id.
 * @param {string} matchId Match document id.
 * @return {Promise<LifecycleContext>} Protected lifecycle context.
 */
async function readContext(
  uid: string,
  matchId: string,
): Promise<LifecycleContext> {
  const db = getFirestore();
  const matchRef = db.collection("matches").doc(matchId);
  const [matchSnap, seatsSnap] = await Promise.all([
    matchRef.get(),
    matchRef.collection("seats").get(),
  ]);

  if (!matchSnap.exists) {
    throw new HttpsError(
      "not-found",
      "MATCH_NOT_FOUND",
      {errorKey: "match.error.not_found"},
    );
  }

  const matchData = matchSnap.data() ?? {};
  const hostAccountId =
    typeof matchData.hostAccountId === "string" ?
      matchData.hostAccountId :
      "";

  let localSeat: FirebaseFirestore.QueryDocumentSnapshot | null = null;

  for (const seat of seatsSnap.docs) {
    const data = seat.data();
    const accountId =
      typeof data.accountId === "string" ? data.accountId : "";
    const localOwnerAccountId =
      typeof data.localOwnerAccountId === "string" ?
        data.localOwnerAccountId :
        "";

    if (accountId === uid || localOwnerAccountId === uid) {
      localSeat = seat;
      break;
    }
  }

  const isHost = hostAccountId === uid;

  if (!isHost && localSeat === null) {
    throw new HttpsError(
      "permission-denied",
      "MATCH_MEMBER_ONLY",
      {errorKey: "match.error.member_only"},
    );
  }

  return {
    matchRef,
    matchData,
    seats: seatsSnap.docs,
    isHost,
    localSeat,
  };
}

/**
 * Throws unless the lifecycle caller owns Host authority.
 * @param {LifecycleContext} context Protected lifecycle context.
 */
function assertHost(context: LifecycleContext): void {
  if (!context.isHost) {
    throw new HttpsError(
      "permission-denied",
      "HOST_ONLY",
      {errorKey: "match.error.host_only"},
    );
  }
}

/**
 * Converts a voluntary leave into a five-minute TemporaryBot reservation.
 * @param {{uid: string, matchId: unknown}} input Lifecycle request.
 * @return {Promise<Record<string, unknown>>} Updated network snapshot.
 */
export async function leaveActiveMatch(input: {
  uid: string;
  matchId: unknown;
}): Promise<Record<string, unknown>> {
  const matchId = requireMatchId(input.matchId);
  const context = await readContext(input.uid, matchId);

  if (context.isHost) {
    throw new HttpsError(
      "failed-precondition",
      "HOST_MIGRATION_REQUIRED",
      {errorKey: "match.error.host_leave_requires_migration"},
    );
  }

  if (context.localSeat === null) {
    throw new HttpsError(
      "failed-precondition",
      "MATCH_SEAT_REQUIRED",
      {errorKey: "match.error.seat_required"},
    );
  }

  const now = Date.now();
  const expiresAt = now + RECONNECT_WINDOW_MS;

  await context.localSeat.ref.set(
    {
      controllerKind: "temporary_bot",
      connectionState: "reconnecting",
      reconnectExpiresAtEpochMs: expiresAt,
      afkLockedOut: false,
      removalReason: "voluntary_leave",
      updatedAt: FieldValue.serverTimestamp(),
    },
    {merge: true},
  );

  const lobbyId =
    typeof context.matchData.lobbyId === "string" ?
      context.matchData.lobbyId :
      "";

  if (lobbyId) {
    const slotIndex = context.localSeat.data().slotIndex;

    if (typeof slotIndex === "number") {
      await getFirestore()
        .collection("lobbies")
        .doc(lobbyId)
        .collection("members")
        .doc(`seat_${slotIndex + 1}`)
        .set(
          {
            controllerKind: "temporary_bot",
            connectionState: "reconnecting",
            updatedAt: FieldValue.serverTimestamp(),
          },
          {merge: true},
        );
    }
  }

  return getMatchNetworkSnapshot({uid: input.uid, matchId});
}

/**
 * Permanently bot-converts a human seat removed after the AFK threshold.
 * @param {{uid: string, matchId: unknown, slotIndex: unknown}} input Request.
 * @return {Promise<Record<string, unknown>>} Updated network snapshot.
 */
export async function hostMarkAfkRemoved(input: {
  uid: string;
  matchId: unknown;
  slotIndex: unknown;
}): Promise<Record<string, unknown>> {
  const matchId = requireMatchId(input.matchId);
  const slotIndex = requireSlotIndex(input.slotIndex);
  const context = await readContext(input.uid, matchId);
  assertHost(context);

  const target = context.seats.find((seat) =>
    seat.data().slotIndex === slotIndex
  );

  if (!target) {
    throw new HttpsError(
      "not-found",
      "MATCH_SEAT_NOT_FOUND",
      {errorKey: "match.error.seat_required"},
    );
  }

  await target.ref.set(
    {
      controllerKind: "permanent_bot",
      connectionState: "afk_removed",
      reconnectExpiresAtEpochMs: 0,
      afkLockedOut: true,
      removalReason: "afk",
      updatedAt: FieldValue.serverTimestamp(),
    },
    {merge: true},
  );

  const lobbyId =
    typeof context.matchData.lobbyId === "string" ?
      context.matchData.lobbyId :
      "";

  if (lobbyId) {
    await getFirestore()
      .collection("lobbies")
      .doc(lobbyId)
      .collection("members")
      .doc(`seat_${slotIndex + 1}`)
      .set(
        {
          controllerKind: "permanent_bot",
          connectionState: "afk_removed",
          updatedAt: FieldValue.serverTimestamp(),
        },
        {merge: true},
      );
  }

  return getMatchNetworkSnapshot({uid: input.uid, matchId});
}

/**
 * Expires TemporaryBot reclaim reservations after five minutes.
 * @param {{uid: string, matchId: unknown}} input Host lifecycle request.
 * @return {Promise<Record<string, unknown>>} Updated network snapshot.
 */
export async function hostExpireReconnects(input: {
  uid: string;
  matchId: unknown;
}): Promise<Record<string, unknown>> {
  const matchId = requireMatchId(input.matchId);
  const context = await readContext(input.uid, matchId);
  assertHost(context);

  const now = Date.now();
  const batch = getFirestore().batch();
  let changed = 0;

  for (const seat of context.seats) {
    const data = seat.data();
    const expiresAt =
      typeof data.reconnectExpiresAtEpochMs === "number" ?
        data.reconnectExpiresAtEpochMs :
        0;

    if (
      data.controllerKind !== "temporary_bot" ||
      data.connectionState !== "reconnecting" ||
      expiresAt <= 0 ||
      expiresAt > now
    ) {
      continue;
    }

    batch.set(
      seat.ref,
      {
        controllerKind: "permanent_bot",
        connectionState: "reconnect_expired",
        reconnectExpiresAtEpochMs: 0,
        removalReason: "reconnect_expired",
        updatedAt: FieldValue.serverTimestamp(),
      },
      {merge: true},
    );

    changed++;
  }

  if (changed > 0) {
    await batch.commit();
  }

  return getMatchNetworkSnapshot({uid: input.uid, matchId});
}

/**
 * Returns a completed match roster to its lobby for a synchronized rematch.
 * @param {{uid: string, matchId: unknown}} input Host lifecycle request.
 * @return {Promise<Record<string, unknown>>} Updated network snapshot.
 */
export async function hostPrepareRematch(input: {
  uid: string;
  matchId: unknown;
}): Promise<Record<string, unknown>> {
  const matchId = requireMatchId(input.matchId);
  const context = await readContext(input.uid, matchId);
  assertHost(context);

  const lobbyId =
    typeof context.matchData.lobbyId === "string" ?
      context.matchData.lobbyId :
      "";

  if (!lobbyId) {
    throw new HttpsError(
      "failed-precondition",
      "MATCH_HAS_NO_LOBBY",
      {errorKey: "match.error.invalid_request"},
    );
  }

  const db = getFirestore();
  const stateSnap = await context.matchRef
    .collection("network")
    .doc("state")
    .get();
  const phase = stateSnap.data()?.phase;

  if (phase !== "match_complete") {
    throw new HttpsError(
      "failed-precondition",
      "MATCH_NOT_COMPLETE",
      {errorKey: "match.error.not_complete"},
    );
  }

  const lobbyRef = db.collection("lobbies").doc(lobbyId);
  const lobbySnap = await lobbyRef.get();

  if (!lobbySnap.exists) {
    throw new HttpsError(
      "not-found",
      "LOBBY_NOT_FOUND",
      {errorKey: "lobby.error.not_found"},
    );
  }

  const lobby = lobbySnap.data() ?? {};
  const batch = db.batch();

  batch.set(
    lobbyRef,
    {
      lifecycleState: "waiting",
      matchId: "",
      startEventId: "",
      startCountdownEndsAtEpochMs: 0,
      updatedAt: FieldValue.serverTimestamp(),
    },
    {merge: true},
  );

  for (let slotIndex = 0; slotIndex < MAX_PLAYERS; slotIndex++) {
    const memberRef = lobbyRef
      .collection("members")
      .doc(`seat_${slotIndex + 1}`);
    const matchSeat = context.seats.find((seat) =>
      seat.data().slotIndex === slotIndex
    );

    if (!matchSeat) {
      continue;
    }

    const seatData = matchSeat.data();
    const controller =
      typeof seatData.controllerKind === "string" ?
        seatData.controllerKind :
        "";

    if (
      controller === "permanent_bot" ||
      seatData.afkLockedOut === true
    ) {
      batch.set(
        memberRef,
        {
          seatMode: "bot",
          seatType: "bot",
          accountId: "",
          localOwnerAccountId: "",
          controllerKind: "bot",
          connectionState: "connected",
          readyForRevision: 0,
          updatedAt: FieldValue.serverTimestamp(),
        },
        {merge: true},
      );
    } else {
      batch.set(
        memberRef,
        {
          controllerKind:
            controller === "temporary_bot" ?
              "temporary_bot" :
              "human",
          connectionState:
            controller === "temporary_bot" ?
              "reconnecting" :
              "connected",
          readyForRevision: 0,
          updatedAt: FieldValue.serverTimestamp(),
        },
        {merge: true},
      );
    }
  }

  const codeHash =
    typeof lobby.joinCodeHash === "string" ? lobby.joinCodeHash : "";

  if (codeHash) {
    batch.set(
      db.collection("join_codes").doc(codeHash),
      {
        active: true,
        lookupActive: true,
        joinOpen: true,
        matchId: "",
        lifecycleState: "waiting",
        updatedAt: FieldValue.serverTimestamp(),
      },
      {merge: true},
    );
  }

  batch.set(
    context.matchRef,
    {
      status: "rematch_ready",
      updatedAt: FieldValue.serverTimestamp(),
    },
    {merge: true},
  );

  await batch.commit();

  return getMatchNetworkSnapshot({uid: input.uid, matchId});
}
