import {createHash} from "crypto";
import {
  FieldValue,
  getFirestore,
} from "firebase-admin/firestore";
import {HttpsError} from "firebase-functions/v2/https";

const NETWORK_SCHEMA_VERSION = 1;
const MAX_STATE_JSON_LENGTH = 64 * 1024;
const MAX_INTENT_JSON_LENGTH = 4 * 1024;
const MAX_PENDING_INTENTS = 50;
const MAX_PENDING_INTENT_AGE_MS = 30 * 1000;

const recoverySafePhases = new Set([
  "awaiting_roll",
  "awaiting_decision",
  "turn_complete",
  "match_complete",
]);

const allowedPhases = new Set([
  "starting",
  "starting_order",
  "awaiting_roll",
  "dice_resolving",
  "movement",
  "awaiting_decision",
  "resolving",
  "turn_complete",
  "match_complete",
]);

const allowedIntentTypes = new Set([
  "client_ready_for_match",
  "request_roll",
  "submit_decision",
  "request_trade_action",
  "request_auction_action",
  "request_development_action",
  "heartbeat",
]);

interface MatchMemberContext {
  matchId: string;
  hostAccountId: string;
  isHost: boolean;
  localSeatId: string;
  seats: FirebaseFirestore.QueryDocumentSnapshot[];
}

export interface GetMatchNetworkInput {
  uid: string;
  matchId: string;
}

export interface SubmitMatchIntentInput {
  uid: string;
  matchId: string;
  clientCommandId: string;
  intentType: string;
  payloadJson: string;
  observedRevision: number;
  observedEventSequence: number;
  observedAuthorityEpoch: number;
  observedPhase: string;
}

export interface PublishMatchNetworkInput {
  uid: string;
  matchId: string;
  expectedRevision: number;
  phase: string;
  turnSeatId: string;
  eventSequence: number;
  snapshotJson: string;
}

export interface AcknowledgeMatchIntentsInput {
  uid: string;
  matchId: string;
  intentIds: string[];
}

/**
 * Throws the stable invalid-request error used by match transport callables.
 * @param {string} fieldName Invalid request field.
 */
function invalidRequest(
  fieldName: string,
): never {
  throw new HttpsError(
    "invalid-argument",
    "INVALID_MATCH_NETWORK_REQUEST",
    {
      errorKey: "match.error.invalid_request",
      fieldName,
    },
  );
}

/**
 * Validates a bounded string request value.
 * @param {unknown} value Candidate value.
 * @param {string} fieldName Request field name.
 * @param {number} minLength Minimum accepted length.
 * @param {number} maxLength Maximum accepted length.
 * @return {string} Validated string.
 */
function requireString(
  value: unknown,
  fieldName: string,
  minLength: number,
  maxLength: number,
): string {
  if (
    typeof value !== "string" ||
    value.length < minLength ||
    value.length > maxLength
  ) {
    invalidRequest(fieldName);
  }

  return value;
}

/**
 * Validates a non-negative safe integer request value.
 * @param {unknown} value Candidate value.
 * @param {string} fieldName Request field name.
 * @param {number} min Minimum accepted value.
 * @return {number} Validated integer.
 */
function requireInteger(
  value: unknown,
  fieldName: string,
  min: number,
): number {
  if (
    typeof value !== "number" ||
    !Number.isSafeInteger(value) ||
    value < min
  ) {
    invalidRequest(fieldName);
  }

  return value;
}

/**
 * Converts a Firestore Timestamp-like value into epoch milliseconds.
 * @param {unknown} value Timestamp-like value.
 * @return {number} Epoch milliseconds, or zero when unavailable.
 */
function staleClientState(
  details: Record<string, unknown>,
): never {
  throw new HttpsError(
    "aborted",
    "STALE_MATCH_CLIENT_STATE",
    {
      errorKey: "match.error.stale_client_state",
      ...details,
    },
  );
}

/**
 * Converts a Firestore Timestamp-like value into epoch milliseconds.
 * @param {unknown} value Timestamp-like value.
 * @return {number} Epoch milliseconds, or zero when unavailable.
 */
function timestampMillis(
  value: unknown,
): number {
  if (
    value &&
    typeof value === "object" &&
    "toMillis" in value &&
    typeof (value as {toMillis?: unknown}).toMillis ===
      "function"
  ) {
    return (
      value as {toMillis: () => number}
    ).toMillis();
  }

  return 0;
}

/**
 * Builds a deterministic idempotency document id for a client command.
 * @param {string} uid Canonical account id.
 * @param {string} clientCommandId Client-generated command id.
 * @return {string} Stable SHA-256 document id.
 */
function intentDocumentId(
  uid: string,
  clientCommandId: string,
): string {
  return createHash("sha256")
    .update(
      `${uid}:${clientCommandId}`,
      "utf8",
    )
    .digest("hex");
}

/**
 * Resolves host/member authority and seat identity for a match.
 * @param {string} uid Canonical account id.
 * @param {string} matchId Match id.
 * @return {Promise<MatchMemberContext>} Resolved match membership context.
 */
async function readMemberContext(
  uid: string,
  matchId: string,
): Promise<MatchMemberContext> {
  const db = getFirestore();
  const matchRef =
    db.collection("matches").doc(matchId);

  const [matchSnap, seatsSnap] =
    await Promise.all([
      matchRef.get(),
      matchRef.collection("seats").get(),
    ]);

  if (!matchSnap.exists) {
    throw new HttpsError(
      "not-found",
      "MATCH_NOT_FOUND",
      {
        errorKey: "match.error.not_found",
      },
    );
  }

  const match =
    matchSnap.data() ?? {};

  const hostAccountId =
    typeof match.hostAccountId === "string" ?
      match.hostAccountId :
      "";

  const isHost =
    hostAccountId === uid;

  let localSeatId = "";

  for (const seat of seatsSnap.docs) {
    const data = seat.data();

    const accountId =
      typeof data.accountId === "string" ?
        data.accountId :
        "";

    const localOwnerAccountId =
      typeof data.localOwnerAccountId === "string" ?
        data.localOwnerAccountId :
        "";

    if (
      accountId === uid ||
      localOwnerAccountId === uid
    ) {
      localSeatId =
        typeof data.seatId === "string" ?
          data.seatId :
          seat.id;

      break;
    }
  }

  if (!isHost && !localSeatId) {
    throw new HttpsError(
      "permission-denied",
      "MATCH_MEMBER_ONLY",
      {
        errorKey: "match.error.member_only",
      },
    );
  }

  return {
    matchId,
    hostAccountId,
    isHost,
    localSeatId,
    seats: seatsSnap.docs,
  };
}

/**
 * Projects a protected match-seat document into the client-safe snapshot.
 * @param {FirebaseFirestore.QueryDocumentSnapshot} seat Match seat document.
 * @return {Record<string, unknown>} Sanitized seat projection.
 */
function seatSnapshot(
  seat: FirebaseFirestore.QueryDocumentSnapshot,
): Record<string, unknown> {
  const data = seat.data();

  return {
    seatId:
      typeof data.seatId === "string" ?
        data.seatId :
        seat.id,
    slotIndex:
      typeof data.slotIndex === "number" ?
        data.slotIndex :
        0,
    seatMode:
      typeof data.seatMode === "string" ?
        data.seatMode :
        "",
    displayName:
      typeof data.displayName === "string" ?
        data.displayName :
        "",
    isHost:
      data.isHost === true,
    controllerKind:
      typeof data.controllerKind === "string" ?
        data.controllerKind :
        "",
    connectionState:
      typeof data.connectionState === "string" ?
        data.connectionState :
        "",
    reconnectExpiresAtEpochMs:
      typeof data.reconnectExpiresAtEpochMs === "number" ?
        data.reconnectExpiresAtEpochMs :
        0,
    afkLockedOut:
      data.afkLockedOut === true,
    removalReason:
      typeof data.removalReason === "string" ?
        data.removalReason :
        "",
  };
}

/**
 * Returns the latest authoritative match-network snapshot for a member.
 * @param {GetMatchNetworkInput} input Authenticated snapshot request.
 * @return {Promise<Record<string, unknown>>} Authoritative network snapshot.
 */
export async function getMatchNetworkSnapshot(
  input: GetMatchNetworkInput,
): Promise<Record<string, unknown>> {
  const matchId =
    requireString(
      input.matchId,
      "matchId",
      8,
      128,
    );

  const context =
    await readMemberContext(
      input.uid,
      matchId,
    );

  const db = getFirestore();
  const matchRef =
    db.collection("matches").doc(matchId);

  const [matchSnap, stateSnap] =
    await Promise.all([
      matchRef.get(),
      matchRef
        .collection("network")
        .doc("state")
        .get(),
    ]);

  if (!matchSnap.exists || !stateSnap.exists) {
    throw new HttpsError(
      "failed-precondition",
      "MATCH_NETWORK_NOT_INITIALIZED",
      {
        errorKey: "match.error.network_not_initialized",
      },
    );
  }

  const match =
    matchSnap.data() ?? {};

  const state =
    stateSnap.data() ?? {};

  return {
    matchId,
    lobbyId:
      typeof match.lobbyId === "string" ?
        match.lobbyId :
        "",
    status:
      typeof match.status === "string" ?
        match.status :
        "starting",
    localSeatId:
      context.localSeatId,
    localIsHost:
      context.isHost,
    revision:
      typeof state.revision === "number" ?
        state.revision :
        0,
    phase:
      typeof state.phase === "string" ?
        state.phase :
        "starting",
    turnSeatId:
      typeof state.turnSeatId === "string" ?
        state.turnSeatId :
        "",
    eventSequence:
      typeof state.eventSequence === "number" ?
        state.eventSequence :
        0,
    snapshotJson:
      typeof state.snapshotJson === "string" ?
        state.snapshotJson :
        "{}",
    updatedAtEpochMs:
      timestampMillis(state.updatedAt),
    hostHeartbeatAtEpochMs:
      typeof match.hostHeartbeatAtEpochMs === "number" ?
        match.hostHeartbeatAtEpochMs :
        0,
    authorityEpoch:
      typeof match.authorityEpoch === "number" ?
        match.authorityEpoch :
        0,
    authorityHandoffReason:
      typeof state.authorityHandoffReason === "string" ?
        state.authorityHandoffReason :
        "",
    seats:
      context.seats.map(seatSnapshot),
    networkSchemaVersion:
      NETWORK_SCHEMA_VERSION,
  };
}

/**
 * Queues an idempotent client intent for the authoritative host.
 * @param {SubmitMatchIntentInput} input Authenticated client intent.
 * @return {Promise<Record<string, unknown>>} Intent acceptance result.
 */
export async function submitMatchIntent(
  input: SubmitMatchIntentInput,
): Promise<Record<string, unknown>> {
  const matchId =
    requireString(
      input.matchId,
      "matchId",
      8,
      128,
    );

  const clientCommandId =
    requireString(
      input.clientCommandId,
      "clientCommandId",
      8,
      128,
    );

  const intentType =
    requireString(
      input.intentType,
      "intentType",
      3,
      64,
    );

  if (!allowedIntentTypes.has(intentType)) {
    invalidRequest("intentType");
  }

  const payloadJson =
    requireString(
      input.payloadJson ?? "{}",
      "payloadJson",
      2,
      MAX_INTENT_JSON_LENGTH,
    );

  const observedRevision =
    requireInteger(
      input.observedRevision,
      "observedRevision",
      0,
    );

  const observedEventSequence =
    requireInteger(
      input.observedEventSequence,
      "observedEventSequence",
      0,
    );

  const observedAuthorityEpoch =
    requireInteger(
      input.observedAuthorityEpoch,
      "observedAuthorityEpoch",
      0,
    );

  const observedPhase =
    requireString(
      input.observedPhase,
      "observedPhase",
      3,
      64,
    );

  if (!allowedPhases.has(observedPhase)) {
    invalidRequest("observedPhase");
  }

  const context =
    await readMemberContext(
      input.uid,
      matchId,
    );

  if (!context.localSeatId) {
    throw new HttpsError(
      "failed-precondition",
      "MATCH_SEAT_REQUIRED",
      {
        errorKey: "match.error.seat_required",
      },
    );
  }

  const db = getFirestore();
  const matchRef =
    db.collection("matches").doc(matchId);

  const intentId =
    intentDocumentId(
      input.uid,
      clientCommandId,
    );

  const intentRef =
    matchRef
      .collection("intents")
      .doc(intentId);

  const stateRef =
    matchRef
      .collection("network")
      .doc("state");

  const localSeat =
    context.seats.find((seat) => {
      const data = seat.data();

      return seat.id === context.localSeatId ||
        data.seatId === context.localSeatId;
    });

  if (!localSeat) {
    throw new HttpsError(
      "failed-precondition",
      "MATCH_SEAT_REQUIRED",
      {
        errorKey: "match.error.seat_required",
      },
    );
  }

  return db.runTransaction(
    async (transaction) => {
      const [
        matchSnap,
        stateSnap,
        seatSnap,
        existing,
      ] =
        await Promise.all([
          transaction.get(matchRef),
          transaction.get(stateRef),
          transaction.get(localSeat.ref),
          transaction.get(intentRef),
        ]);

      if (!matchSnap.exists) {
        throw new HttpsError(
          "not-found",
          "MATCH_NOT_FOUND",
          {
            errorKey: "match.error.not_found",
          },
        );
      }

      const match =
        matchSnap.data() ?? {};

      const status =
        typeof match.status === "string" ?
          match.status :
          "";

      if (
        status !== "starting" &&
        status !== "active"
      ) {
        throw new HttpsError(
          "failed-precondition",
          "MATCH_NOT_ACTIVE",
          {
            errorKey: "match.error.not_active",
          },
        );
      }

      // A retry of the exact same command remains idempotent even if the
      // authoritative state advanced after the original write.
      if (existing.exists) {
        return {
          intentId,
          accepted: true,
          idempotentReplay: true,
        };
      }

      if (!stateSnap.exists || !seatSnap.exists) {
        throw new HttpsError(
          "failed-precondition",
          "MATCH_NETWORK_NOT_INITIALIZED",
          {
            errorKey:
              "match.error.network_not_initialized",
          },
        );
      }

      const state =
        stateSnap.data() ?? {};

      const seat =
        seatSnap.data() ?? {};

      const seatAccountId =
        typeof seat.accountId === "string" ?
          seat.accountId :
          "";

      const localOwnerAccountId =
        typeof seat.localOwnerAccountId === "string" ?
          seat.localOwnerAccountId :
          "";

      const controllerKind =
        typeof seat.controllerKind === "string" ?
          seat.controllerKind :
          "";

      const connectionState =
        typeof seat.connectionState === "string" ?
          seat.connectionState :
          "";

      const seatOwnedByCaller =
        seatAccountId === input.uid ||
        localOwnerAccountId === input.uid;

      const humanControlled =
        controllerKind === "human" ||
        controllerKind === "local_human";

      if (
        !seatOwnedByCaller ||
        !humanControlled ||
        connectionState !== "connected" ||
        seat.afkLockedOut === true
      ) {
        throw new HttpsError(
          "failed-precondition",
          "MATCH_SEAT_NOT_ACTIVE_HUMAN",
          {
            errorKey:
              "match.error.seat_not_active_human",
          },
        );
      }

      const currentRevision =
        typeof state.revision === "number" ?
          state.revision :
          0;

      const currentEventSequence =
        typeof state.eventSequence === "number" ?
          state.eventSequence :
          0;

      const currentPhase =
        typeof state.phase === "string" ?
          state.phase :
          "starting";

      const currentAuthorityEpoch =
        typeof match.authorityEpoch === "number" ?
          match.authorityEpoch :
          0;

      if (
        observedRevision !== currentRevision ||
        observedEventSequence !== currentEventSequence ||
        observedAuthorityEpoch !== currentAuthorityEpoch ||
        observedPhase !== currentPhase
      ) {
        staleClientState({
          observedRevision,
          currentRevision,
          observedEventSequence,
          currentEventSequence,
          observedAuthorityEpoch,
          currentAuthorityEpoch,
          observedPhase,
          currentPhase,
        });
      }

      transaction.create(
        intentRef,
        {
          intentId,
          clientCommandId,
          accountId: input.uid,
          seatId: context.localSeatId,
          intentType,
          payloadJson,
          submittedRevision:
            currentRevision,
          submittedEventSequence:
            currentEventSequence,
          submittedAuthorityEpoch:
            currentAuthorityEpoch,
          submittedPhase:
            currentPhase,
          status: "pending",
          createdAt:
            FieldValue.serverTimestamp(),
          updatedAt:
            FieldValue.serverTimestamp(),
          schemaVersion:
            NETWORK_SCHEMA_VERSION,
        },
      );

      return {
        intentId,
        accepted: true,
        idempotentReplay: false,
      };
    },
  );
}

/**
 * Lists pending remote intents for the authoritative host only.
 * @param {GetMatchNetworkInput} input Authenticated host request.
 * @return {Promise<Record<string, unknown>[]>} Pending intent projections.
 */
export async function listPendingMatchIntents(
  input: GetMatchNetworkInput,
): Promise<Record<string, unknown>[]> {
  const matchId =
    requireString(
      input.matchId,
      "matchId",
      8,
      128,
    );

  const context =
    await readMemberContext(
      input.uid,
      matchId,
    );

  if (!context.isHost) {
    throw new HttpsError(
      "permission-denied",
      "HOST_ONLY",
      {
        errorKey: "match.error.host_only",
      },
    );
  }

  const db = getFirestore();

  const matchRef =
    db.collection("matches").doc(matchId);

  const [matchSnap, stateSnap, query] =
    await Promise.all([
      matchRef.get(),
      matchRef
        .collection("network")
        .doc("state")
        .get(),
      matchRef
        .collection("intents")
        .where("status", "==", "pending")
        .limit(MAX_PENDING_INTENTS)
        .get(),
    ]);

  if (!matchSnap.exists || !stateSnap.exists) {
    throw new HttpsError(
      "failed-precondition",
      "MATCH_NETWORK_NOT_INITIALIZED",
      {
        errorKey:
          "match.error.network_not_initialized",
      },
    );
  }

  const match =
    matchSnap.data() ?? {};

  if (match.hostAccountId !== input.uid) {
    throw new HttpsError(
      "permission-denied",
      "HOST_ONLY",
      {
        errorKey: "match.error.host_only",
      },
    );
  }

  const state =
    stateSnap.data() ?? {};

  const currentRevision =
    typeof state.revision === "number" ?
      state.revision :
      0;

  const currentEventSequence =
    typeof state.eventSequence === "number" ?
      state.eventSequence :
      0;

  const currentPhase =
    typeof state.phase === "string" ?
      state.phase :
      "starting";

  const currentAuthorityEpoch =
    typeof match.authorityEpoch === "number" ?
      match.authorityEpoch :
      0;

  const now = Date.now();
  const valid:
    Record<string, unknown>[] = [];
  const stale:
    Array<{
      ref: FirebaseFirestore.DocumentReference;
      reason: string;
    }> = [];

  for (const doc of query.docs) {
    const data = doc.data();

    const createdAtEpochMs =
      timestampMillis(data.createdAt);

    const submittedRevision =
      typeof data.submittedRevision === "number" ?
        data.submittedRevision :
        -1;

    const submittedEventSequence =
      typeof data.submittedEventSequence === "number" ?
        data.submittedEventSequence :
        -1;

    const submittedAuthorityEpoch =
      typeof data.submittedAuthorityEpoch === "number" ?
        data.submittedAuthorityEpoch :
        -1;

    const submittedPhase =
      typeof data.submittedPhase === "string" ?
        data.submittedPhase :
        "";

    const accountId =
      typeof data.accountId === "string" ?
        data.accountId :
        "";

    const seatId =
      typeof data.seatId === "string" ?
        data.seatId :
        "";

    const seat =
      context.seats.find((candidate) => {
        const seatData = candidate.data();

        return candidate.id === seatId ||
          seatData.seatId === seatId;
      });

    let staleReason = "";

    if (
      submittedRevision < 0 ||
      submittedEventSequence < 0 ||
      submittedAuthorityEpoch < 0 ||
      !submittedPhase
    ) {
      staleReason =
        "legacy_or_missing_envelope";
    } else if (
      submittedAuthorityEpoch !==
        currentAuthorityEpoch
    ) {
      staleReason =
        "authority_epoch_changed";
    } else if (
      submittedRevision !== currentRevision
    ) {
      staleReason =
        "network_revision_changed";
    } else if (
      submittedEventSequence !==
        currentEventSequence
    ) {
      staleReason =
        "event_sequence_changed";
    } else if (
      submittedPhase !== currentPhase
    ) {
      staleReason =
        "phase_changed";
    } else if (
      createdAtEpochMs <= 0 ||
      now - createdAtEpochMs >
        MAX_PENDING_INTENT_AGE_MS
    ) {
      staleReason =
        "intent_expired";
    } else if (!seat) {
      staleReason =
        "seat_missing";
    } else {
      const seatData =
        seat.data();

      const seatAccountId =
        typeof seatData.accountId === "string" ?
          seatData.accountId :
          "";

      const localOwnerAccountId =
        typeof seatData.localOwnerAccountId === "string" ?
          seatData.localOwnerAccountId :
          "";

      const controllerKind =
        typeof seatData.controllerKind === "string" ?
          seatData.controllerKind :
          "";

      const connectionState =
        typeof seatData.connectionState === "string" ?
          seatData.connectionState :
          "";

      const ownedByIntentAccount =
        seatAccountId === accountId ||
        localOwnerAccountId === accountId;

      const humanControlled =
        controllerKind === "human" ||
        controllerKind === "local_human";

      if (
        !ownedByIntentAccount ||
        !humanControlled ||
        connectionState !== "connected" ||
        seatData.afkLockedOut === true
      ) {
        staleReason =
          "seat_no_longer_active_human";
      }
    }

    if (staleReason) {
      stale.push({
        ref: doc.ref,
        reason: staleReason,
      });
      continue;
    }

    valid.push({
      intentId:
        typeof data.intentId === "string" ?
          data.intentId :
          doc.id,
      clientCommandId:
        typeof data.clientCommandId === "string" ?
          data.clientCommandId :
          "",
      accountId,
      seatId,
      intentType:
        typeof data.intentType === "string" ?
          data.intentType :
          "",
      payloadJson:
        typeof data.payloadJson === "string" ?
          data.payloadJson :
          "{}",
      createdAtEpochMs,
    });
  }

  if (stale.length > 0) {
    const batch =
      db.batch();

    for (const item of stale) {
      batch.set(
        item.ref,
        {
          status: "stale",
          staleReason: item.reason,
          staleAt:
            FieldValue.serverTimestamp(),
          updatedAt:
            FieldValue.serverTimestamp(),
        },
        {merge: true},
      );
    }

    await batch.commit();
  }

  return valid;
}

/**
 * Marks host-consumed intents as consumed.
 * @param {AcknowledgeMatchIntentsInput} input Authenticated host ACK request.
 * @return {Promise<{acknowledged: number}>} Number of acknowledged intents.
 */
export async function acknowledgeMatchIntents(
  input: AcknowledgeMatchIntentsInput,
): Promise<{acknowledged: number}> {
  const matchId =
    requireString(
      input.matchId,
      "matchId",
      8,
      128,
    );

  const context =
    await readMemberContext(
      input.uid,
      matchId,
    );

  if (!context.isHost) {
    throw new HttpsError(
      "permission-denied",
      "HOST_ONLY",
      {
        errorKey: "match.error.host_only",
      },
    );
  }

  if (
    !Array.isArray(input.intentIds) ||
    input.intentIds.length > MAX_PENDING_INTENTS
  ) {
    invalidRequest("intentIds");
  }

  const validIds =
    input.intentIds.map((value) =>
      requireString(
        value,
        "intentId",
        8,
        128,
      )
    );

  const db = getFirestore();
  const batch = db.batch();

  for (const intentId of validIds) {
    const ref =
      db.collection("matches")
        .doc(matchId)
        .collection("intents")
        .doc(intentId);

    batch.set(
      ref,
      {
        status: "consumed",
        consumedAt:
          FieldValue.serverTimestamp(),
        updatedAt:
          FieldValue.serverTimestamp(),
      },
      {
        merge: true,
      },
    );
  }

  await batch.commit();

  return {
    acknowledged:
      validIds.length,
  };
}

/**
 * Publishes one revisioned authoritative match state from the host.
 * @param {PublishMatchNetworkInput} input Authenticated host publication.
 * @return {Promise<Record<string, unknown>>} Published authoritative state.
 */
export async function publishMatchNetworkState(
  input: PublishMatchNetworkInput,
): Promise<Record<string, unknown>> {
  const matchId =
    requireString(
      input.matchId,
      "matchId",
      8,
      128,
    );

  const expectedRevision =
    requireInteger(
      input.expectedRevision,
      "expectedRevision",
      0,
    );

  const phase =
    requireString(
      input.phase,
      "phase",
      3,
      64,
    );

  if (!allowedPhases.has(phase)) {
    invalidRequest("phase");
  }

  const turnSeatId =
    typeof input.turnSeatId === "string" ?
      input.turnSeatId :
      "";

  if (turnSeatId.length > 128) {
    invalidRequest("turnSeatId");
  }

  const eventSequence =
    requireInteger(
      input.eventSequence,
      "eventSequence",
      0,
    );

  const snapshotJson =
    requireString(
      input.snapshotJson ?? "{}",
      "snapshotJson",
      2,
      MAX_STATE_JSON_LENGTH,
    );

  const context =
    await readMemberContext(
      input.uid,
      matchId,
    );

  if (!context.isHost) {
    throw new HttpsError(
      "permission-denied",
      "HOST_ONLY",
      {
        errorKey: "match.error.host_only",
      },
    );
  }

  if (
    turnSeatId &&
    !context.seats.some((seat) =>
      seat.id === turnSeatId ||
      seat.data().seatId === turnSeatId
    )
  ) {
    invalidRequest("turnSeatId");
  }

  const db = getFirestore();
  const matchRef =
    db.collection("matches").doc(matchId);

  const stateRef =
    matchRef
      .collection("network")
      .doc("state");

  return db.runTransaction(
    async (transaction) => {
      const [matchSnap, stateSnap] =
        await Promise.all([
          transaction.get(matchRef),
          transaction.get(stateRef),
        ]);

      if (!matchSnap.exists || !stateSnap.exists) {
        throw new HttpsError(
          "failed-precondition",
          "MATCH_NETWORK_NOT_INITIALIZED",
          {
            errorKey:
              "match.error.network_not_initialized",
          },
        );
      }

      const match =
        matchSnap.data() ?? {};

      if (match.hostAccountId !== input.uid) {
        throw new HttpsError(
          "permission-denied",
          "HOST_ONLY",
          {
            errorKey: "match.error.host_only",
          },
        );
      }

      const state =
        stateSnap.data() ?? {};

      const currentRevision =
        typeof state.revision === "number" ?
          state.revision :
          0;

      if (currentRevision !== expectedRevision) {
        throw new HttpsError(
          "aborted",
          "MATCH_REVISION_MISMATCH",
          {
            errorKey:
              "match.error.revision_mismatch",
            currentRevision,
          },
        );
      }

      const currentSequence =
        typeof state.eventSequence === "number" ?
          state.eventSequence :
          0;

      if (eventSequence < currentSequence) {
        throw new HttpsError(
          "aborted",
          "MATCH_EVENT_SEQUENCE_REWIND",
          {
            errorKey:
              "match.error.event_sequence",
            currentSequence,
          },
        );
      }

      const nextRevision =
        currentRevision + 1;

      const matchStatus =
        phase === "match_complete" ?
          "complete" :
          "active";

      const serverTimestamp =
        FieldValue.serverTimestamp();

      const stateUpdate:
        FirebaseFirestore.DocumentData = {
          revision: nextRevision,
          phase,
          turnSeatId,
          eventSequence,
          snapshotJson,
          authorityHostAccountId:
            input.uid,
          updatedAt:
            serverTimestamp,
          schemaVersion:
            NETWORK_SCHEMA_VERSION,
        };

      if (recoverySafePhases.has(phase)) {
        stateUpdate.recoveryRevision =
          nextRevision;
        stateUpdate.recoveryPhase =
          phase;
        stateUpdate.recoveryTurnSeatId =
          turnSeatId;
        stateUpdate.recoveryEventSequence =
          eventSequence;
        stateUpdate.recoverySnapshotJson =
          snapshotJson;
        stateUpdate.recoveryUpdatedAt =
          serverTimestamp;
      }

      transaction.set(
        stateRef,
        stateUpdate,
        {
          merge: true,
        },
      );

      transaction.update(
        matchRef,
        {
          status: matchStatus,
          networkRevision:
            nextRevision,
          hostHeartbeatAtEpochMs:
            Date.now(),
          updatedAt:
            serverTimestamp,
        },
      );

      return {
        matchId,
        revision: nextRevision,
        phase,
        turnSeatId,
        eventSequence,
        snapshotJson,
        status: matchStatus,
      };
    },
  );
}
