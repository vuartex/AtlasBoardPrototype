import {
  FieldValue,
  getFirestore,
} from "firebase-admin/firestore";
import {HttpsError} from "firebase-functions/v2/https";
import {getMatchNetworkSnapshot} from "./network";

const RECONNECT_WINDOW_MS = 5 * 60 * 1000;
const MATCH_PRESENCE_STALE_MS = 15 * 1000;
const MAX_PLAYERS = 4;

// A voluntary Host leave is only committed from a stable authoritative
// checkpoint. Movement, dice animation, and blocking decisions finish first.
const HOST_MIGRATION_SAFE_PHASES = new Set([
  "awaiting_roll",
  "awaiting_decision",
  "turn_complete",
  "match_complete",
]);

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
 * Atomically hands Host authority to the lowest-slot connected remote Human,
 * then converts the departing Host seat into the normal five-minute reclaim
 * reservation. Extra LocalHuman seats owned by the old Host become bots.
 *
 * Gameplay state is deliberately NOT rewritten here. The existing revisioned
 * network/state snapshot remains the handoff checkpoint and the new Host
 * continues from it.
 */
async function migrateHostAndLeave(
  uid: string,
  matchId: string,
  context: LifecycleContext,
): Promise<Record<string, unknown>> {
  const db = getFirestore();
  const matchRef = context.matchRef;
  const stateRef = matchRef.collection("network").doc("state");
  const seatRefs = context.seats.map((seat) => seat.ref);
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

  const lobbyRef = db.collection("lobbies").doc(lobbyId);
  const now = Date.now();
  const reconnectExpiresAt = now + RECONNECT_WINDOW_MS;

  await db.runTransaction(async (transaction) => {
    const matchSnap = await transaction.get(matchRef);
    const stateSnap = await transaction.get(stateRef);
    const lobbySnap = await transaction.get(lobbyRef);

    if (!matchSnap.exists || !stateSnap.exists || !lobbySnap.exists) {
      throw new HttpsError(
        "failed-precondition",
        "HOST_MIGRATION_STATE_MISSING",
        {errorKey: "match.error.network_not_initialized"},
      );
    }

    const match = matchSnap.data() ?? {};
    const state = stateSnap.data() ?? {};

    if (match.hostAccountId !== uid) {
      throw new HttpsError(
        "permission-denied",
        "HOST_ONLY",
        {errorKey: "match.error.host_only"},
      );
    }

    const phase =
      typeof state.phase === "string" ?
        state.phase :
        "";

    if (!HOST_MIGRATION_SAFE_PHASES.has(phase)) {
      throw new HttpsError(
        "failed-precondition",
        "HOST_MIGRATION_NOT_SAFE_YET",
        {
          errorKey: "match.error.host_migration_not_safe",
          phase,
        },
      );
    }

    const seats: FirebaseFirestore.DocumentSnapshot[] = [];
    for (const seatRef of seatRefs) {
      seats.push(await transaction.get(seatRef));
    }

    const candidates = seats
      .filter((seat) => {
        if (!seat.exists) return false;
        const data = seat.data() ?? {};
        const accountId =
          typeof data.accountId === "string" ? data.accountId : "";
        return accountId.length > 0 &&
          accountId !== uid &&
          data.controllerKind === "human" &&
          data.connectionState === "connected" &&
          data.afkLockedOut !== true;
      })
      .sort((left, right) => {
        const a = left.data() ?? {};
        const b = right.data() ?? {};
        const aSlot = typeof a.slotIndex === "number" ? a.slotIndex : 999;
        const bSlot = typeof b.slotIndex === "number" ? b.slotIndex : 999;
        if (aSlot !== bSlot) return aSlot - bSlot;
        return left.id.localeCompare(right.id);
      });

    const candidate = candidates[0];
    if (!candidate) {
      throw new HttpsError(
        "failed-precondition",
        "HOST_MIGRATION_NO_CANDIDATE",
        {errorKey: "match.error.host_migration_no_candidate"},
      );
    }

    const candidateData = candidate.data() ?? {};
    const nextHostAccountId =
      typeof candidateData.accountId === "string" ?
        candidateData.accountId :
        "";

    if (!nextHostAccountId) {
      throw new HttpsError(
        "failed-precondition",
        "HOST_MIGRATION_NO_CANDIDATE",
        {errorKey: "match.error.host_migration_no_candidate"},
      );
    }

    const oldHostSeat = seats.find((seat) => {
      if (!seat.exists) return false;
      const data = seat.data() ?? {};
      const accountId =
        typeof data.accountId === "string" ? data.accountId : "";
      const localOwnerAccountId =
        typeof data.localOwnerAccountId === "string" ?
          data.localOwnerAccountId :
          "";
      return data.isHost === true &&
        (accountId === uid || localOwnerAccountId === uid);
    }) ?? seats.find((seat) => {
      const data = seat.data() ?? {};
      return data.accountId === uid;
    });

    if (!oldHostSeat) {
      throw new HttpsError(
        "failed-precondition",
        "MATCH_SEAT_REQUIRED",
        {errorKey: "match.error.seat_required"},
      );
    }

    const previousEpoch =
      typeof match.authorityEpoch === "number" ?
        match.authorityEpoch :
        0;
    const nextEpoch = previousEpoch + 1;
    const serverTimestamp = FieldValue.serverTimestamp();

    transaction.set(
      matchRef,
      {
        hostAccountId: nextHostAccountId,
        authorityEpoch: nextEpoch,
        hostMigrationCount:
          (typeof match.hostMigrationCount === "number" ?
            match.hostMigrationCount :
            0) + 1,
        lastHostMigrationReason: "voluntary_leave",
        lastHostMigrationAtEpochMs: now,
        updatedAt: serverTimestamp,
      },
      {merge: true},
    );

    transaction.set(
      stateRef,
      {
        authorityHostAccountId: nextHostAccountId,
        authorityEpoch: nextEpoch,
        authorityHandoffReason: "voluntary_leave",
        authorityHandoffAtEpochMs: now,
        updatedAt: serverTimestamp,
      },
      {merge: true},
    );

    transaction.set(
      lobbyRef,
      {
        hostAccountId: nextHostAccountId,
        hostHeartbeatAtEpochMs: now,
        hostMigrationEpoch: nextEpoch,
        updatedAt: serverTimestamp,
      },
      {merge: true},
    );

    for (const seat of seats) {
      if (!seat.exists) continue;

      const data = seat.data() ?? {};
      const slotIndex =
        typeof data.slotIndex === "number" ? data.slotIndex : -1;
      if (slotIndex < 0 || slotIndex >= MAX_PLAYERS) continue;

      const lobbyMemberRef = lobbyRef
        .collection("members")
        .doc(`seat_${slotIndex + 1}`);

      const accountId =
        typeof data.accountId === "string" ? data.accountId : "";
      const localOwnerAccountId =
        typeof data.localOwnerAccountId === "string" ?
          data.localOwnerAccountId :
          "";

      if (seat.id === candidate.id) {
        const hostSeatUpdate = {
          seatMode: "host_local",
          accountId: nextHostAccountId,
          localOwnerAccountId: nextHostAccountId,
          isHost: true,
          controllerKind: "human",
          connectionState: "connected",
          reconnectExpiresAtEpochMs: 0,
          afkLockedOut: false,
          removalReason: "",
          updatedAt: serverTimestamp,
        };

        transaction.set(seat.ref, hostSeatUpdate, {merge: true});
        transaction.set(
          lobbyMemberRef,
          {
            seatMode: "host_local",
            seatType: "human",
            accountId: nextHostAccountId,
            localOwnerAccountId: nextHostAccountId,
            isHost: true,
            controllerKind: "human",
            connectionState: "connected",
            readyForRevision: 0,
            updatedAt: serverTimestamp,
          },
          {merge: true},
        );
        continue;
      }

      if (seat.id === oldHostSeat.id) {
        const oldHostUpdate = {
          seatMode: "remote_human",
          accountId: uid,
          localOwnerAccountId: "",
          isHost: false,
          controllerKind: "temporary_bot",
          connectionState: "reconnecting",
          reconnectExpiresAtEpochMs: reconnectExpiresAt,
          afkLockedOut: false,
          removalReason: "host_migrated_leave",
          updatedAt: serverTimestamp,
        };

        transaction.set(seat.ref, oldHostUpdate, {merge: true});
        transaction.set(
          lobbyMemberRef,
          {
            seatMode: "remote_human",
            seatType: "human",
            accountId: uid,
            localOwnerAccountId: "",
            isHost: false,
            controllerKind: "temporary_bot",
            connectionState: "reconnecting",
            readyForRevision: 0,
            updatedAt: serverTimestamp,
          },
          {merge: true},
        );
        continue;
      }

      // Split-screen/local-human seats belong to the departing Host process.
      // They cannot remain Human after authority leaves that machine.
      if (localOwnerAccountId === uid && accountId !== uid) {
        const botUpdate = {
          seatMode: "bot",
          accountId: "",
          localOwnerAccountId: "",
          isHost: false,
          controllerKind: "permanent_bot",
          connectionState: "host_owner_left",
          reconnectExpiresAtEpochMs: 0,
          afkLockedOut: false,
          removalReason: "host_local_owner_left",
          updatedAt: serverTimestamp,
        };

        transaction.set(seat.ref, botUpdate, {merge: true});
        transaction.set(
          lobbyMemberRef,
          {
            seatMode: "bot",
            seatType: "bot",
            accountId: "",
            localOwnerAccountId: "",
            isHost: false,
            controllerKind: "permanent_bot",
            connectionState: "host_owner_left",
            readyForRevision: 0,
            updatedAt: serverTimestamp,
          },
          {merge: true},
        );
      }
    }
  });

  return getMatchNetworkSnapshot({uid, matchId});
}



/**
 * Renews one real Human match seat presence lease. Host presence also renews
 * the match/lobby Host lease used by crash failover.
 */
export async function touchMatchPresence(input: {
  uid: string;
  matchId: unknown;
}): Promise<Record<string, unknown>> {
  const matchId = requireMatchId(input.matchId);
  const context = await readContext(input.uid, matchId);

  if (context.localSeat === null) {
    throw new HttpsError(
      "failed-precondition",
      "MATCH_SEAT_REQUIRED",
      {errorKey: "match.error.seat_required"},
    );
  }

  const localData = context.localSeat.data();

  if (localData.controllerKind !== "human") {
    return getMatchNetworkSnapshot({
      uid: input.uid,
      matchId,
    });
  }

  const db = getFirestore();
  const now = Date.now();
  const serverTimestamp = FieldValue.serverTimestamp();
  const batch = db.batch();

  batch.set(
    context.localSeat.ref,
    {
      lastHeartbeatAtEpochMs: now,
      connectionState: "connected",
      updatedAt: serverTimestamp,
    },
    {merge: true},
  );

  if (context.isHost) {
    batch.set(
      context.matchRef,
      {
        hostHeartbeatAtEpochMs: now,
        updatedAt: serverTimestamp,
      },
      {merge: true},
    );

    const lobbyId =
      typeof context.matchData.lobbyId === "string" ?
        context.matchData.lobbyId :
        "";

    if (lobbyId) {
      batch.set(
        db.collection("lobbies").doc(lobbyId),
        {
          hostHeartbeatAtEpochMs: now,
          updatedAt: serverTimestamp,
        },
        {merge: true},
      );
    }
  }

  await batch.commit();

  return getMatchNetworkSnapshot({
    uid: input.uid,
    matchId,
  });
}

/**
 * Host-side abrupt-disconnect sweep. A Remote Human that stops heartbeating
 * becomes a five-minute TemporaryBot reservation.
 */
export async function hostSweepDisconnectedMatchSeats(input: {
  uid: string;
  matchId: unknown;
}): Promise<Record<string, unknown>> {
  const matchId = requireMatchId(input.matchId);
  const context = await readContext(input.uid, matchId);
  assertHost(context);

  const now = Date.now();
  const db = getFirestore();
  const batch = db.batch();
  const lobbyId =
    typeof context.matchData.lobbyId === "string" ?
      context.matchData.lobbyId :
      "";

  let changed = 0;

  for (const seat of context.seats) {
    const data = seat.data();

    if (
      data.isHost === true ||
      data.controllerKind !== "human" ||
      data.connectionState !== "connected"
    ) {
      continue;
    }

    const accountId =
      typeof data.accountId === "string" ?
        data.accountId :
        "";

    if (!accountId) {
      continue;
    }

    const heartbeat =
      typeof data.lastHeartbeatAtEpochMs === "number" ?
        data.lastHeartbeatAtEpochMs :
        0;

    if (
      heartbeat <= 0 ||
      now - heartbeat <= MATCH_PRESENCE_STALE_MS
    ) {
      continue;
    }

    const expiresAt =
      now + RECONNECT_WINDOW_MS;

    batch.set(
      seat.ref,
      {
        controllerKind: "temporary_bot",
        connectionState: "reconnecting",
        reconnectExpiresAtEpochMs: expiresAt,
        afkLockedOut: false,
        removalReason: "connection_lost",
        updatedAt: FieldValue.serverTimestamp(),
      },
      {merge: true},
    );

    const slotIndex =
      typeof data.slotIndex === "number" ?
        data.slotIndex :
        -1;

    if (
      lobbyId &&
      slotIndex >= 0 &&
      slotIndex < MAX_PLAYERS
    ) {
      batch.set(
        db.collection("lobbies")
          .doc(lobbyId)
          .collection("members")
          .doc(`seat_${slotIndex + 1}`),
        {
          controllerKind: "temporary_bot",
          connectionState: "reconnecting",
          updatedAt: FieldValue.serverTimestamp(),
        },
        {merge: true},
      );
    }

    changed++;
  }

  if (changed > 0) {
    await batch.commit();
  }

  return getMatchNetworkSnapshot({
    uid: input.uid,
    matchId,
  });
}

/**
 * Server-authoritative crashed-Host election. Only the lowest-slot live Human
 * can commit. Unsafe transient state rolls back to the last safe checkpoint.
 */
export async function tryRecoverCrashedHost(input: {
  uid: string;
  matchId: unknown;
}): Promise<Record<string, unknown>> {
  const matchId = requireMatchId(input.matchId);
  const context = await readContext(input.uid, matchId);

  if (context.localSeat === null) {
    throw new HttpsError(
      "failed-precondition",
      "MATCH_SEAT_REQUIRED",
      {errorKey: "match.error.seat_required"},
    );
  }

  const db = getFirestore();
  const matchRef = context.matchRef;
  const stateRef =
    matchRef.collection("network").doc("state");
  const seatRefs =
    context.seats.map((seat) => seat.ref);
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

  const lobbyRef =
    db.collection("lobbies").doc(lobbyId);
  const now = Date.now();

  await db.runTransaction(async (transaction) => {
    const matchSnap =
      await transaction.get(matchRef);
    const stateSnap =
      await transaction.get(stateRef);
    const lobbySnap =
      await transaction.get(lobbyRef);

    if (
      !matchSnap.exists ||
      !stateSnap.exists ||
      !lobbySnap.exists
    ) {
      throw new HttpsError(
        "failed-precondition",
        "HOST_RECOVERY_STATE_MISSING",
        {errorKey: "match.error.network_not_initialized"},
      );
    }

    const match = matchSnap.data() ?? {};
    const state = stateSnap.data() ?? {};

    const currentHost =
      typeof match.hostAccountId === "string" ?
        match.hostAccountId :
        "";

    if (currentHost === input.uid) {
      return;
    }

    const hostHeartbeat =
      typeof match.hostHeartbeatAtEpochMs === "number" ?
        match.hostHeartbeatAtEpochMs :
        0;

    if (
      hostHeartbeat <= 0 ||
      now - hostHeartbeat <= MATCH_PRESENCE_STALE_MS
    ) {
      return;
    }

    const seats:
      FirebaseFirestore.DocumentSnapshot[] = [];

    for (const seatRef of seatRefs) {
      seats.push(
        await transaction.get(seatRef),
      );
    }

    const candidates = seats
      .filter((seat) => {
        if (!seat.exists) return false;

        const data = seat.data() ?? {};
        const accountId =
          typeof data.accountId === "string" ?
            data.accountId :
            "";
        const heartbeat =
          typeof data.lastHeartbeatAtEpochMs === "number" ?
            data.lastHeartbeatAtEpochMs :
            0;

        return accountId.length > 0 &&
          accountId !== currentHost &&
          data.controllerKind === "human" &&
          data.connectionState === "connected" &&
          data.afkLockedOut !== true &&
          heartbeat > 0 &&
          now - heartbeat <= MATCH_PRESENCE_STALE_MS;
      })
      .sort((left, right) => {
        const a = left.data() ?? {};
        const b = right.data() ?? {};
        const aSlot =
          typeof a.slotIndex === "number" ?
            a.slotIndex :
            999;
        const bSlot =
          typeof b.slotIndex === "number" ?
            b.slotIndex :
            999;

        if (aSlot !== bSlot) {
          return aSlot - bSlot;
        }

        return left.id.localeCompare(right.id);
      });

    const candidate = candidates[0];

    if (!candidate) {
      return;
    }

    const candidateData =
      candidate.data() ?? {};
    const nextHostAccountId =
      typeof candidateData.accountId === "string" ?
        candidateData.accountId :
        "";

    if (
      !nextHostAccountId ||
      nextHostAccountId !== input.uid
    ) {
      return;
    }

    const currentPhase =
      typeof state.phase === "string" ?
        state.phase :
        "";

    let recoveryPhase = currentPhase;
    let recoveryTurnSeatId =
      typeof state.turnSeatId === "string" ?
        state.turnSeatId :
        "";
    let recoveryEventSequence =
      typeof state.eventSequence === "number" ?
        state.eventSequence :
        0;
    let recoverySnapshotJson =
      typeof state.snapshotJson === "string" ?
        state.snapshotJson :
        "{}";

    if (!HOST_MIGRATION_SAFE_PHASES.has(currentPhase)) {
      recoveryPhase =
        typeof state.recoveryPhase === "string" ?
          state.recoveryPhase :
          "";
      recoveryTurnSeatId =
        typeof state.recoveryTurnSeatId === "string" ?
          state.recoveryTurnSeatId :
          "";
      recoveryEventSequence =
        typeof state.recoveryEventSequence === "number" ?
          state.recoveryEventSequence :
          0;
      recoverySnapshotJson =
        typeof state.recoverySnapshotJson === "string" ?
          state.recoverySnapshotJson :
          "";
    }

    if (
      !HOST_MIGRATION_SAFE_PHASES.has(recoveryPhase) ||
      !recoverySnapshotJson ||
      recoverySnapshotJson === "{}"
    ) {
      throw new HttpsError(
        "failed-precondition",
        "HOST_RECOVERY_CHECKPOINT_MISSING",
        {errorKey: "match.error.host_migration_not_safe"},
      );
    }

    const oldHostSeat = seats.find((seat) => {
      if (!seat.exists) return false;

      const data = seat.data() ?? {};
      const accountId =
        typeof data.accountId === "string" ?
          data.accountId :
          "";
      const localOwnerAccountId =
        typeof data.localOwnerAccountId === "string" ?
          data.localOwnerAccountId :
          "";

      return data.isHost === true ||
        accountId === currentHost ||
        localOwnerAccountId === currentHost;
    });

    if (!oldHostSeat) {
      throw new HttpsError(
        "failed-precondition",
        "MATCH_SEAT_REQUIRED",
        {errorKey: "match.error.seat_required"},
      );
    }

    const previousEpoch =
      typeof match.authorityEpoch === "number" ?
        match.authorityEpoch :
        0;
    const nextEpoch =
      previousEpoch + 1;
    const currentRevision =
      typeof state.revision === "number" ?
        state.revision :
        0;
    const nextRevision =
      currentRevision + 1;
    const reconnectExpiresAt =
      now + RECONNECT_WINDOW_MS;
    const serverTimestamp =
      FieldValue.serverTimestamp();

    transaction.set(
      matchRef,
      {
        hostAccountId: nextHostAccountId,
        authorityEpoch: nextEpoch,
        hostHeartbeatAtEpochMs: now,
        hostMigrationCount:
          (typeof match.hostMigrationCount === "number" ?
            match.hostMigrationCount :
            0) + 1,
        lastHostMigrationReason: "host_timeout",
        lastHostMigrationAtEpochMs: now,
        networkRevision: nextRevision,
        status:
          recoveryPhase === "match_complete" ?
            "complete" :
            "active",
        updatedAt: serverTimestamp,
      },
      {merge: true},
    );

    transaction.set(
      stateRef,
      {
        revision: nextRevision,
        phase: recoveryPhase,
        turnSeatId: recoveryTurnSeatId,
        eventSequence: recoveryEventSequence,
        snapshotJson: recoverySnapshotJson,
        authorityHostAccountId:
          nextHostAccountId,
        authorityEpoch: nextEpoch,
        authorityHandoffReason: "host_timeout",
        authorityHandoffAtEpochMs: now,
        updatedAt: serverTimestamp,
      },
      {merge: true},
    );

    transaction.set(
      lobbyRef,
      {
        hostAccountId: nextHostAccountId,
        hostHeartbeatAtEpochMs: now,
        hostMigrationEpoch: nextEpoch,
        updatedAt: serverTimestamp,
      },
      {merge: true},
    );

    for (const seat of seats) {
      if (!seat.exists) continue;

      const data = seat.data() ?? {};
      const slotIndex =
        typeof data.slotIndex === "number" ?
          data.slotIndex :
          -1;

      if (
        slotIndex < 0 ||
        slotIndex >= MAX_PLAYERS
      ) {
        continue;
      }

      const lobbyMemberRef =
        lobbyRef
          .collection("members")
          .doc(`seat_${slotIndex + 1}`);

      const accountId =
        typeof data.accountId === "string" ?
          data.accountId :
          "";
      const localOwnerAccountId =
        typeof data.localOwnerAccountId === "string" ?
          data.localOwnerAccountId :
          "";

      if (seat.id === candidate.id) {
        transaction.set(
          seat.ref,
          {
            seatMode: "host_local",
            accountId: nextHostAccountId,
            localOwnerAccountId:
              nextHostAccountId,
            isHost: true,
            controllerKind: "human",
            connectionState: "connected",
            reconnectExpiresAtEpochMs: 0,
            afkLockedOut: false,
            removalReason: "",
            lastHeartbeatAtEpochMs: now,
            updatedAt: serverTimestamp,
          },
          {merge: true},
        );

        transaction.set(
          lobbyMemberRef,
          {
            seatMode: "host_local",
            seatType: "human",
            accountId: nextHostAccountId,
            localOwnerAccountId:
              nextHostAccountId,
            isHost: true,
            controllerKind: "human",
            connectionState: "connected",
            readyForRevision: 0,
            updatedAt: serverTimestamp,
          },
          {merge: true},
        );

        continue;
      }

      if (seat.id === oldHostSeat.id) {
        transaction.set(
          seat.ref,
          {
            seatMode: "remote_human",
            accountId: currentHost,
            localOwnerAccountId: "",
            isHost: false,
            controllerKind: "temporary_bot",
            connectionState: "reconnecting",
            reconnectExpiresAtEpochMs:
              reconnectExpiresAt,
            afkLockedOut: false,
            removalReason: "host_connection_lost",
            updatedAt: serverTimestamp,
          },
          {merge: true},
        );

        transaction.set(
          lobbyMemberRef,
          {
            seatMode: "remote_human",
            seatType: "human",
            accountId: currentHost,
            localOwnerAccountId: "",
            isHost: false,
            controllerKind: "temporary_bot",
            connectionState: "reconnecting",
            readyForRevision: 0,
            updatedAt: serverTimestamp,
          },
          {merge: true},
        );

        continue;
      }

      if (
        localOwnerAccountId === currentHost &&
        accountId !== nextHostAccountId
      ) {
        transaction.set(
          seat.ref,
          {
            seatMode: "bot",
            accountId: "",
            localOwnerAccountId: "",
            isHost: false,
            controllerKind: "permanent_bot",
            connectionState: "host_owner_lost",
            reconnectExpiresAtEpochMs: 0,
            afkLockedOut: false,
            removalReason: "host_owner_lost",
            updatedAt: serverTimestamp,
          },
          {merge: true},
        );

        transaction.set(
          lobbyMemberRef,
          {
            seatMode: "bot",
            seatType: "bot",
            accountId: "",
            localOwnerAccountId: "",
            isHost: false,
            controllerKind: "permanent_bot",
            connectionState: "host_owner_lost",
            readyForRevision: 0,
            updatedAt: serverTimestamp,
          },
          {merge: true},
        );

        continue;
      }

      if (data.isHost === true) {
        transaction.set(
          seat.ref,
          {
            isHost: false,
            updatedAt: serverTimestamp,
          },
          {merge: true},
        );

        transaction.set(
          lobbyMemberRef,
          {
            isHost: false,
            updatedAt: serverTimestamp,
          },
          {merge: true},
        );
      }
    }
  });

  return getMatchNetworkSnapshot({
    uid: input.uid,
    matchId,
  });
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
    return migrateHostAndLeave(
      input.uid,
      matchId,
      context,
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

  const db = getFirestore();
  const matchRef = context.matchRef;
  const seatRefs = context.seats.map((seat) => seat.ref);
  const lobbyId =
    typeof context.matchData.lobbyId === "string" ?
      context.matchData.lobbyId :
      "";
  const lobbyRef =
    lobbyId ?
      db.collection("lobbies").doc(lobbyId) :
      null;
  const now = Date.now();

  // Re-read authority + seats in one transaction. A reconnect and an expiry
  // sweep may happen at almost the same instant; whichever commits first makes
  // the other transaction retry against the new controller/expiry state.
  // This prevents a late Host sweep from converting a successfully reclaimed
  // Human back into a PermanentBot.
  await db.runTransaction(async (transaction) => {
    const matchSnap =
      await transaction.get(matchRef);

    if (!matchSnap.exists) {
      throw new HttpsError(
        "not-found",
        "MATCH_NOT_FOUND",
        {errorKey: "match.error.not_found"},
      );
    }

    const match =
      matchSnap.data() ?? {};

    if (match.hostAccountId !== input.uid) {
      throw new HttpsError(
        "permission-denied",
        "HOST_ONLY",
        {errorKey: "match.error.host_only"},
      );
    }

    const seatSnaps:
      FirebaseFirestore.DocumentSnapshot[] = [];

    for (const seatRef of seatRefs) {
      seatSnaps.push(
        await transaction.get(seatRef),
      );
    }

    const serverTimestamp =
      FieldValue.serverTimestamp();

    for (const seatSnap of seatSnaps) {
      if (!seatSnap.exists) {
        continue;
      }

      const data =
        seatSnap.data() ?? {};

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

      transaction.set(
        seatSnap.ref,
        {
          controllerKind: "permanent_bot",
          connectionState: "reconnect_expired",
          reconnectExpiresAtEpochMs: 0,
          removalReason: "reconnect_expired",
          updatedAt: serverTimestamp,
        },
        {merge: true},
      );

      if (lobbyRef) {
        const slotIndex =
          typeof data.slotIndex === "number" ?
            data.slotIndex :
            -1;

        if (
          slotIndex >= 0 &&
          slotIndex < MAX_PLAYERS
        ) {
          transaction.set(
            lobbyRef
              .collection("members")
              .doc(`seat_${slotIndex + 1}`),
            {
              controllerKind: "permanent_bot",
              connectionState: "reconnect_expired",
              updatedAt: serverTimestamp,
            },
            {merge: true},
          );
        }
      }
    }
  });

  return getMatchNetworkSnapshot({
    uid: input.uid,
    matchId,
  });
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
