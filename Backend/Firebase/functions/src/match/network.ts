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

  return db.runTransaction(
    async (transaction) => {
      const [matchSnap, existing] =
        await Promise.all([
          transaction.get(matchRef),
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

      if (existing.exists) {
        return {
          intentId,
          accepted: true,
          idempotentReplay: true,
        };
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

  const query =
    await db
      .collection("matches")
      .doc(matchId)
      .collection("intents")
      .where("status", "==", "pending")
      .limit(MAX_PENDING_INTENTS)
      .get();

  return query.docs.map((doc) => {
    const data = doc.data();

    return {
      intentId:
        typeof data.intentId === "string" ?
          data.intentId :
          doc.id,
      clientCommandId:
        typeof data.clientCommandId === "string" ?
          data.clientCommandId :
          "",
      accountId:
        typeof data.accountId === "string" ?
          data.accountId :
          "",
      seatId:
        typeof data.seatId === "string" ?
          data.seatId :
          "",
      intentType:
        typeof data.intentType === "string" ?
          data.intentType :
          "",
      payloadJson:
        typeof data.payloadJson === "string" ?
          data.payloadJson :
          "{}",
      createdAtEpochMs:
        timestampMillis(data.createdAt),
    };
  });
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

      transaction.set(
        stateRef,
        {
          revision: nextRevision,
          phase,
          turnSeatId,
          eventSequence,
          snapshotJson,
          authorityHostAccountId:
            input.uid,
          updatedAt:
            FieldValue.serverTimestamp(),
          schemaVersion:
            NETWORK_SCHEMA_VERSION,
        },
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
          updatedAt:
            FieldValue.serverTimestamp(),
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
