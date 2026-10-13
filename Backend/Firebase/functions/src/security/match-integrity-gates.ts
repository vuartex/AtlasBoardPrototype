/**
 * AtlasBoard SECURITY-CORE-01: fail-closed persistence of unverified host results.
 *
 * Host-authoritative gameplay is NOT server-authenticated gameplay. A Firebase
 * Auth token proves who sent the host snapshot, not that the claimed victory,
 * balance or development actually happened. Only a privileged, independently
 * verifying SERVER process may create the match_result_verifications receipt.
 * This project does not yet implement that verifier: online progression rewards
 * therefore remain PENDING until it is implemented and accepted.
 *
 * Never create such receipts from the Unity client, a host callback, a
 * client-acknowledgement counter, or a purely elapsed-time/turn-count check.
 * This file intentionally does not implement any emulator/prod bypass.
 */
import {createHash} from "crypto";
import {getFirestore} from "firebase-admin/firestore";
import {HttpsError, onCall} from "firebase-functions/v2/https";
import {publishMatchNetworkState} from "../match/network";
import {
  recordCompletedMatch,
  ProgressionSlotTelemetryInput,
} from "../progression/progression";
import {ATLAS_ENFORCE_APP_CHECK} from "./app-check-policy";

const REGION = "europe-west1";
const VERIFICATION_SCHEMA_VERSION = 1;
const VERIFICATION_METHOD = "server_authoritative_replay_v1";
const VERIFIER_ID = "atlasboard-backend-verifier-v1";
const COMPLETED_PHASE = "match_complete";

function invalidMatchId(value: unknown): string {
  if (typeof value !== "string" || value.length < 8 || value.length > 128 ||
    !/^[a-zA-Z0-9_-]+$/.test(value)) {
    throw new HttpsError("invalid-argument", "INVALID_MATCH_ID", {
      errorKey: "progression.error.invalid_request",
    });
  }
  return value;
}

function requireAuthenticatedUid(
  auth: {uid: string} | undefined,
): string {
  if (!auth?.uid) {
    throw new HttpsError("unauthenticated", "AUTHENTICATION_REQUIRED", {
      errorKey: "account.error.unauthenticated",
    });
  }
  return auth.uid;
}

/**
 * Result verification is a privileged write-only receipt, NOT host testimony.
 * Even if the host controls the snapshot, it cannot mint its own receipt since
 * direct Firestore writes to these collections are denied by security rules.
 * The future verifier must also freeze the approved revision atomically.
 */
async function hasTrustedMatchResult(
  matchId: string,
  uid: string,
): Promise<boolean> {
  const db = getFirestore();
  const matchRef = db.collection("matches").doc(matchId);
  const [matchSnap, networkSnap, verificationSnap] = await Promise.all([
    matchRef.get(),
    matchRef.collection("network").doc("state").get(),
    db.collection("match_result_verifications").doc(matchId).get(),
  ]);

  if (!matchSnap.exists || matchSnap.data()?.hostAccountId !== uid) {
    throw new HttpsError("permission-denied", "MATCH_HOST_REQUIRED", {
      errorKey: "progression.error.host_required",
    });
  }
  if (!networkSnap.exists || !verificationSnap.exists) {
    return false;
  }
  const match = matchSnap.data() ?? {};
  const network = networkSnap.data() ?? {};
  const proof = verificationSnap.data() ?? {};
  const snapshotJson = network.snapshotJson;
  if (typeof snapshotJson !== "string" || snapshotJson.length < 2 ||
    snapshotJson.length > 64 * 1024) {
    return false;
  }
  const sha256 = createHash("sha256")
    .update(snapshotJson, "utf8")
    .digest("hex");
  return (
    proof.schemaVersion === VERIFICATION_SCHEMA_VERSION &&
    proof.verificationStatus === "verified" &&
    proof.verificationMethod === VERIFICATION_METHOD &&
    proof.verifiedBy === VERIFIER_ID &&
    proof.matchId === matchId &&
    proof.hostAccountId === uid &&
    proof.networkPhase === COMPLETED_PHASE &&
    network.phase === COMPLETED_PHASE &&
    (match.status === "complete" || match.status === "completed") &&
    Number.isSafeInteger(network.revision) &&
    proof.networkRevision === network.revision &&
    Number.isSafeInteger(network.eventSequence) &&
    proof.networkEventSequence === network.eventSequence &&
    proof.snapshotSha256 === sha256 &&
    typeof proof.verifiedAt?.toMillis === "function" &&
    proof.verifiedAt.toMillis() <= Date.now()
  );
}

/**
 * Hardening for obvious zero-turn completions and post-completion rewrites.
 * This check is a defense-in-depth measure, NOT cryptographic game authority.
 * Full transition/rules validation is still needed for official progression.
 */
export const matchHostPublishState = onCall(
  {
    region: REGION,
    maxInstances: 30,
    enforceAppCheck: ATLAS_ENFORCE_APP_CHECK,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request.auth);
    const data = request.data ?? {};
    const matchId = invalidMatchId(data.matchId);
    const db = getFirestore();
    const [match, proof] = await Promise.all([
      db.collection("matches").doc(matchId).get(),
      db.collection("match_result_verifications").doc(matchId).get(),
    ]);

    // The original publishMatchNetworkState performs the authoritative host,
    // seat and transaction revision checks; do not weaken these.
    if (match.exists && match.data()?.hostAccountId === uid) {
      if (match.data()?.status === "complete" ||
        match.data()?.status === "completed" || proof.exists) {
        throw new HttpsError("failed-precondition", "MATCH_ALREADY_COMPLETE", {
          errorKey: "match.error.already_complete",
        });
      }
      if (data.phase === COMPLETED_PHASE) {
        if (!Number.isSafeInteger(data.expectedRevision) ||
          data.expectedRevision < 2 ||
          !Number.isSafeInteger(data.eventSequence) ||
          data.eventSequence < 2 ||
          typeof data.snapshotJson !== "string" ||
          data.snapshotJson.length > 64 * 1024) {
          throw new HttpsError("failed-precondition", "PREMATURE_MATCH_COMPLETE", {
            errorKey: "match.error.premature_completion",
          });
        }
        let frame: Record<string, unknown>;
        try {
          frame = JSON.parse(data.snapshotJson) as Record<string, unknown>;
        } catch {
          throw new HttpsError("invalid-argument", "INVALID_MATCH_RESULT", {
            errorKey: "match.error.invalid_request",
          });
        }
        const result = frame.matchResult as Record<string, unknown> | null;
        if (!frame || frame.phase !== COMPLETED_PHASE ||
          !result || result.valid !== true) {
          throw new HttpsError("failed-precondition", "MATCH_RESULT_NOT_READY", {
            errorKey: "match.error.premature_completion",
          });
        }
      }
    }

    const state = await publishMatchNetworkState({
      uid,
      matchId,
      expectedRevision: data.expectedRevision as number,
      phase: data.phase as string,
      turnSeatId: data.turnSeatId as string,
      eventSequence: data.eventSequence as number,
      snapshotJson: data.snapshotJson as string,
    });
    return {ok: true, state};
  },
);

/**
 * Fail closed for permanent XP, career wins, match-history rewards and
 * achievements until an independent server verifier signs the exact network
 * state. A pending response is successful at the transport level so Unity's
 * current completion recorder does not repeatedly retry or block the game.
 * It deliberately has applied=false and affectedAccountCount=0.
 */
export const progressionRecordCompletedMatch = onCall(
  {
    region: REGION,
    maxInstances: 30,
    enforceAppCheck: ATLAS_ENFORCE_APP_CHECK,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request.auth);
    const data = request.data ?? {};
    const matchId = invalidMatchId(data.matchId);

    if (!await hasTrustedMatchResult(matchId, uid)) {
      return {
        ok: true,
        matchId,
        applied: false,
        idempotentReplay: false,
        affectedAccountCount: 0,
        affectedAccountIds: [],
        verificationStatus: "pending_authoritative_verification",
      };
    }
    const telemetry = Array.isArray(data.telemetry) ?
      data.telemetry.slice(0, 4).map((item: unknown) => {
        const value = item && typeof item === "object" ?
          item as Record<string, unknown> : {};
        return {
          slotIndex: Number(value.slotIndex ?? -1),
          diceRolls: Number(value.diceRolls ?? 0),
          doublesRolled: Number(value.doublesRolled ?? 0),
          totalDiceValue: Number(value.totalDiceValue ?? 0),
          highestRoll: Number(value.highestRoll ?? 0),
        } as ProgressionSlotTelemetryInput;
      }) : [];
    const result = await recordCompletedMatch({
      uid,
      matchId,
      completedTurns: Number(data.completedTurns ?? 0),
      telemetry,
    });
    return {
      ...result,
      verificationStatus: "verified",
    };
  },
);
