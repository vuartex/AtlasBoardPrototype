/**
 * Security Core 03: authoritative reward-track claim.
 * This callable overrides the legacy export from src/index.ts at the entrypoint.
 * Inactive/disabled tiers are rejected both before and INSIDE the transaction.
 * Preserves the existing callable name, response schema, auth and App Check.
 *
 * Follow-up: consolidate this implementation into economy/seasonal.ts once
 * current local complete source is captured. Never call the unguarded claim.
 */
import {createHash} from "crypto";
import {FieldValue, getFirestore} from "firebase-admin/firestore";
import {logger} from "firebase-functions";
import {HttpsError, onCall} from "firebase-functions/v2/https";
import {getActiveSeasonalEvent} from "../economy/seasonal";
import {ATLAS_ENFORCE_APP_CHECK} from "./app-check-policy";

const REGION = "europe-west1";
const EVENT_SCHEMA_VERSION = 1;
const MAX_SAFE_BALANCE = Number.MAX_SAFE_INTEGER;

function digest(value: string): string {
  return createHash("sha256").update(value, "utf8").digest("hex");
}

function readAmount(value: unknown, label: string): number {
  if (value === undefined) return 0;
  if (typeof value !== "number" || !Number.isSafeInteger(value) || value < 0) {
    throw new HttpsError("internal", "SEASONAL_STATE_INVALID", {
      errorKey: "seasonal.error.state_invalid", fieldName: label,
    });
  }
  return value;
}

function requireTierId(value: unknown): string {
  if (typeof value !== "string" || value.length < 2 || value.length > 96) {
    throw new HttpsError("invalid-argument", "INVALID_TIERID", {
      errorKey: "seasonal.error.invalid_request", fieldName: "tierId",
    });
  }
  return value;
}

function inactiveTier(): never {
  throw new HttpsError("failed-precondition", "SEASONAL_TRACK_INACTIVE", {
    errorKey: "seasonal.error.track_inactive",
  });
}

export const metaSeasonalClaimTrackTier = onCall(
  {
    region: REGION,
    maxInstances: 4,
    enforceAppCheck: ATLAS_ENFORCE_APP_CHECK,
  },
  async (request) => {
    const uid = request.auth?.uid;
    if (!uid) {
      throw new HttpsError("unauthenticated", "AUTH_REQUIRED", {
        errorKey: "account.error.authentication_required",
      });
    }
    const tierId = requireTierId(request.data?.tierId);
    const db = getFirestore();
    const event = await getActiveSeasonalEvent();
    if (!event.active || !event.eventId) {
      throw new HttpsError("failed-precondition", "SEASONAL_EVENT_INACTIVE", {
        errorKey: "seasonal.error.event_inactive",
      });
    }

    const tierRef = db.collection("seasonal_tracks")
      .doc(event.eventId).collection("tiers").doc(tierId);
    const tierSnapshot = await tierRef.get();
    if (!tierSnapshot.exists) {
      throw new HttpsError("not-found", "SEASONAL_TRACK_TIER_NOT_FOUND", {
        errorKey: "seasonal.error.track_not_found",
      });
    }
    const tier = tierSnapshot.data() ?? {};
    if (tier.active !== true) inactiveTier();
    const requiredXp = readAmount(tier.requiredXp, "requiredXp");
    const rewardType = tier.rewardType;
    const rewardAmount = readAmount(tier.rewardAmount, "rewardAmount");
    if (rewardType !== "gold" && rewardType !== "atlas_coin" &&
        rewardType !== "event_ticket") {
      throw new HttpsError("internal", "SEASONAL_TRACK_REWARD_INVALID", {
        errorKey: "seasonal.error.state_invalid",
      });
    }

    const progressRef = db.collection("seasonal_progress").doc(uid)
      .collection("events").doc(event.eventId);
    const progressSnapshot = await progressRef.get();
    const xp = readAmount(progressSnapshot.data()?.xp, "seasonal_progress.xp");
    if (xp < requiredXp) {
      throw new HttpsError("failed-precondition", "SEASONAL_TRACK_LOCKED", {
        errorKey: "seasonal.error.track_locked", requiredXp, currentXp: xp,
      });
    }

    const currencyId = rewardType === "event_ticket" ?
      event.ticketCurrencyId : rewardType;
    const claimRef = db.collection("seasonal_track_claims").doc(uid)
      .collection("events").doc(event.eventId)
      .collection("tiers").doc(tierId);
    const balanceRef = db.collection("wallets").doc(uid)
      .collection("balances").doc(currencyId);
    const transactionId = digest(`${uid}:${event.eventId}:track:${tierId}`);
    const ledgerEntryId = digest(`${uid}:${transactionId}`);
    const ledgerRef = db.collection("wallet_ledger").doc(ledgerEntryId);
    const runtimeRef = db.collection("seasonal_runtime").doc("active");

    const result = await db.runTransaction(async (transaction) => {
      const [claimSnap, balanceSnap, ledgerSnap, runtimeSnap, tierTxnSnap,
        progressTxnSnap] = await Promise.all([
        transaction.get(claimRef),
        transaction.get(balanceRef),
        transaction.get(ledgerRef),
        transaction.get(runtimeRef),
        transaction.get(tierRef),
        transaction.get(progressRef),
      ]);

      if (claimSnap.exists) {
        const claim = claimSnap.data() ?? {};
        // Preserve accepted same-tier replay behavior. The claim has already
        // been paid, and must never mint another reward.
        if (claim.uid !== uid || claim.eventId !== event.eventId ||
            claim.tierId !== tierId || claim.transactionId !== transactionId ||
            claim.rewardType !== rewardType ||
            claim.rewardAmount !== rewardAmount ||
            claim.currencyId !== currencyId) {
          throw new HttpsError("internal", "SEASONAL_CLAIM_STATE_INVALID", {
            errorKey: "seasonal.error.state_invalid",
          });
        }
        return {
          applied: false,
          idempotentReplay: true,
          eventId: event.eventId,
          referenceId: tierId,
          transactionId,
          ticketBalance: rewardType === "event_ticket" ?
            readAmount(claim.balanceAfter, "balanceAfter") : -1,
          eventXp: readAmount(progressTxnSnap.data()?.xp, "seasonal_progress.xp"),
          rewardType,
          rewardAmount,
        };
      }

      const runtime = runtimeSnap.data() ?? {};
      const now = Date.now();
      if (!runtimeSnap.exists || runtime.schemaVersion !== EVENT_SCHEMA_VERSION ||
          runtime.active !== true || runtime.eventId !== event.eventId ||
          runtime.ticketCurrencyId !== event.ticketCurrencyId ||
          readAmount(runtime.startsAtEpochMs, "startsAtEpochMs") > now ||
          now > readAmount(runtime.endsAtEpochMs, "endsAtEpochMs")) {
        throw new HttpsError("failed-precondition", "SEASONAL_EVENT_CHANGED", {
          errorKey: "seasonal.error.event_inactive",
        });
      }

      const currentTier = tierTxnSnap.data() ?? {};
      if (!tierTxnSnap.exists || currentTier.active !== true) inactiveTier();
      if (readAmount(currentTier.requiredXp, "requiredXp") !== requiredXp ||
          currentTier.rewardType !== rewardType ||
          readAmount(currentTier.rewardAmount, "rewardAmount") !== rewardAmount) {
        throw new HttpsError("failed-precondition", "SEASONAL_TRACK_CHANGED", {
          errorKey: "seasonal.error.state_invalid",
        });
      }

      const currentXp = readAmount(progressTxnSnap.data()?.xp, "seasonal_progress.xp");
      if (currentXp < requiredXp) {
        throw new HttpsError("failed-precondition", "SEASONAL_TRACK_LOCKED", {
          errorKey: "seasonal.error.track_locked", requiredXp, currentXp,
        });
      }
      if (ledgerSnap.exists) {
        throw new HttpsError("internal", "SEASONAL_PARTIAL_STATE", {
          errorKey: "seasonal.error.partial_state",
        });
      }
      const balanceBefore = readAmount(balanceSnap.data()?.amount, "wallet.amount");
      const balanceAfter = balanceBefore + rewardAmount;
      if (!Number.isSafeInteger(balanceAfter) || balanceAfter > MAX_SAFE_BALANCE) {
        throw new HttpsError("out-of-range", "SEASONAL_BALANCE_OUT_OF_RANGE", {
          errorKey: "seasonal.error.balance_out_of_range",
        });
      }
      const serverTimestamp = FieldValue.serverTimestamp();
      transaction.set(balanceRef, {
        uid,
        currencyId,
        amount: balanceAfter,
        schemaVersion: EVENT_SCHEMA_VERSION,
        updatedAt: serverTimestamp,
      }, {merge: false});
      transaction.create(ledgerRef, {
        uid,
        currencyId,
        delta: rewardAmount,
        balanceBefore,
        balanceAfter,
        reason: "seasonal_track",
        transactionId,
        idempotencyKeyHash: ledgerEntryId,
        actorType: "seasonal_backend",
        actorUid: uid,
        source: "store_seasonal_track",
        schemaVersion: EVENT_SCHEMA_VERSION,
        createdAt: serverTimestamp,
      });
      transaction.create(claimRef, {
        uid,
        eventId: event.eventId,
        tierId,
        requiredXp,
        rewardType,
        currencyId,
        rewardAmount,
        balanceBefore,
        balanceAfter,
        transactionId,
        source: "store_seasonal_track",
        schemaVersion: EVENT_SCHEMA_VERSION,
        createdAt: serverTimestamp,
      });
      return {
        applied: true,
        idempotentReplay: false,
        eventId: event.eventId,
        referenceId: tierId,
        transactionId,
        ticketBalance: rewardType === "event_ticket" ? balanceAfter : -1,
        eventXp: currentXp,
        rewardType,
        rewardAmount,
      };
    });

    logger.info("AtlasBoard seasonal track tier claimed.", {
      accountId: uid,
      tierId,
      eventId: result.eventId,
      applied: result.applied,
      rewardType: result.rewardType,
      rewardAmount: result.rewardAmount,
    });
    return {ok: true, ...result};
  },
);
