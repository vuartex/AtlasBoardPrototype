import {createHash} from "crypto";
import {FieldValue, getFirestore} from "firebase-admin/firestore";
import {HttpsError} from "firebase-functions/v2/https";

export type WalletCurrencyId = "gold" | "atlas_coin";

export interface WalletMutationInput {
  uid: string;
  currencyId: WalletCurrencyId;
  delta: number;
  reason: string;
  transactionId: string;
  idempotencyKey: string;
  source: string;
}

export interface WalletMutationResult {
  applied: boolean;
  idempotentReplay: boolean;
  ledgerEntryId: string;
  currencyId: WalletCurrencyId;
  delta: number;
  balanceBefore: number;
  balanceAfter: number;
  transactionId: string;
}

const LEDGER_SCHEMA_VERSION = 1;
const MAX_ABSOLUTE_BALANCE = Number.MAX_SAFE_INTEGER;

/**
 * Checks whether a value is a supported persistent wallet currency.
 * @param {unknown} value Value to validate.
 * @return {boolean} True when the value is a supported wallet currency id.
 */
export function isWalletCurrencyId(
  value: unknown,
): value is WalletCurrencyId {
  return value === "gold" || value === "atlas_coin";
}

/**
 * Builds the deterministic ledger document id used for idempotency.
 * @param {string} uid Authenticated account id.
 * @param {string} idempotencyKey Stable caller idempotency key.
 * @return {string} SHA-256 ledger document id.
 */
export function makeLedgerEntryId(
  uid: string,
  idempotencyKey: string,
): string {
  return createHash("sha256")
    .update(`${uid}:${idempotencyKey}`, "utf8")
    .digest("hex");
}

/**
 * Reads and validates an existing non-negative wallet integer field.
 * @param {unknown} value Stored field value.
 * @param {string} fieldName Field name used in error details.
 * @return {number} Validated non-negative safe integer.
 */
function readExistingInteger(
  value: unknown,
  fieldName: string,
): number {
  if (value === undefined) {
    return 0;
  }

  if (
    typeof value !== "number" ||
    !Number.isSafeInteger(value) ||
    value < 0
  ) {
    throw new HttpsError(
      "internal",
      "WALLET_STATE_INVALID",
      {
        errorKey: "economy.error.wallet_state_invalid",
        fieldName,
      },
    );
  }

  return value;
}

/**
 * Ensures a replayed idempotency key matches the original mutation.
 * @param {FirebaseFirestore.DocumentData} existing Existing ledger data.
 * @param {WalletMutationInput} input Requested wallet mutation.
 */
function assertIdempotentPayloadMatches(
  existing: FirebaseFirestore.DocumentData,
  input: WalletMutationInput,
): void {
  if (
    existing.uid !== input.uid ||
    existing.currencyId !== input.currencyId ||
    existing.delta !== input.delta ||
    existing.reason !== input.reason ||
    existing.transactionId !== input.transactionId
  ) {
    throw new HttpsError(
      "already-exists",
      "IDEMPOTENCY_KEY_CONFLICT",
      {
        errorKey: "economy.error.idempotency_conflict",
      },
    );
  }
}

/**
 * Applies one atomic wallet balance mutation and immutable ledger record.
 * @param {WalletMutationInput} input Validated wallet mutation input.
 * @return {Promise<WalletMutationResult>} Mutation result.
 */
export async function applyWalletMutation(
  input: WalletMutationInput,
): Promise<WalletMutationResult> {
  const db = getFirestore();
  const ledgerEntryId = makeLedgerEntryId(
    input.uid,
    input.idempotencyKey,
  );

  const balanceRef = db
    .collection("wallets")
    .doc(input.uid)
    .collection("balances")
    .doc(input.currencyId);

  const ledgerRef = db
    .collection("wallet_ledger")
    .doc(ledgerEntryId);

  return db.runTransaction(async (transaction) => {
    const ledgerSnapshot = await transaction.get(ledgerRef);

    if (ledgerSnapshot.exists) {
      const existing = ledgerSnapshot.data() ?? {};
      assertIdempotentPayloadMatches(existing, input);

      return {
        applied: false,
        idempotentReplay: true,
        ledgerEntryId,
        currencyId: input.currencyId,
        delta: input.delta,
        balanceBefore: readExistingInteger(
          existing.balanceBefore,
          "balanceBefore",
        ),
        balanceAfter: readExistingInteger(
          existing.balanceAfter,
          "balanceAfter",
        ),
        transactionId: input.transactionId,
      };
    }

    const balanceSnapshot = await transaction.get(balanceRef);
    const balanceData = balanceSnapshot.data() ?? {};
    const balanceBefore = readExistingInteger(
      balanceData.amount,
      "amount",
    );
    const balanceAfter = balanceBefore + input.delta;

    if (!Number.isSafeInteger(balanceAfter)) {
      throw new HttpsError(
        "out-of-range",
        "WALLET_BALANCE_OUT_OF_RANGE",
        {
          errorKey: "economy.error.balance_out_of_range",
        },
      );
    }

    if (balanceAfter < 0) {
      throw new HttpsError(
        "failed-precondition",
        "INSUFFICIENT_FUNDS",
        {
          errorKey: "economy.error.insufficient_funds",
          currencyId: input.currencyId,
          required: Math.abs(input.delta),
          available: balanceBefore,
        },
      );
    }

    if (balanceAfter > MAX_ABSOLUTE_BALANCE) {
      throw new HttpsError(
        "out-of-range",
        "WALLET_BALANCE_OUT_OF_RANGE",
        {
          errorKey: "economy.error.balance_out_of_range",
        },
      );
    }

    const serverTimestamp = FieldValue.serverTimestamp();

    transaction.set(
      balanceRef,
      {
        uid: input.uid,
        currencyId: input.currencyId,
        amount: balanceAfter,
        schemaVersion: LEDGER_SCHEMA_VERSION,
        updatedAt: serverTimestamp,
      },
      {merge: false},
    );

    transaction.create(ledgerRef, {
      uid: input.uid,
      currencyId: input.currencyId,
      delta: input.delta,
      balanceBefore,
      balanceAfter,
      reason: input.reason,
      transactionId: input.transactionId,
      idempotencyKeyHash: ledgerEntryId,
      actorType: "authenticated_account",
      actorUid: input.uid,
      source: input.source,
      schemaVersion: LEDGER_SCHEMA_VERSION,
      createdAt: serverTimestamp,
    });

    return {
      applied: true,
      idempotentReplay: false,
      ledgerEntryId,
      currencyId: input.currencyId,
      delta: input.delta,
      balanceBefore,
      balanceAfter,
      transactionId: input.transactionId,
    };
  });
}
