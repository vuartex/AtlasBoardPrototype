import {FieldValue, getFirestore} from "firebase-admin/firestore";

export interface MetaDevResetResult {
  resetCount: number;
  auditId: string;
}

/**
 * Emulator-only helper that removes only CURRENT commerce-backed ownership.
 * Commerce transactions, wallet ledgers, promo history, and audit evidence
 * remain untouched so testing can be repeated without erasing history.
 * @param {string} uid Authenticated account id.
 * @param {string} operationId Client operation id for audit correlation.
 * @return {Promise<MetaDevResetResult>} Reset summary.
 */
export async function resetDevPurchasedEntitlements(
  uid: string,
  operationId: string,
): Promise<MetaDevResetResult> {
  const db = getFirestore();

  const commerceSnapshot = await db
    .collection("commerce_transactions")
    .where("uid", "==", uid)
    .get();

  const purchaseTransactionIdsByItem =
    new Map<string, Set<string>>();

  for (const document of commerceSnapshot.docs) {
    const data = document.data();

    if (
      data.status !== "succeeded" ||
      data.entitlementStatus !== "granted" ||
      typeof data.itemId !== "string" ||
      data.itemId.length === 0 ||
      typeof data.transactionId !== "string" ||
      data.transactionId.length === 0
    ) {
      continue;
    }

    let transactionIds =
      purchaseTransactionIdsByItem.get(data.itemId);

    if (transactionIds === undefined) {
      transactionIds = new Set<string>();
      purchaseTransactionIdsByItem.set(
        data.itemId,
        transactionIds,
      );
    }

    transactionIds.add(data.transactionId);
  }

  const candidateItems =
    [...purchaseTransactionIdsByItem.keys()];

  if (candidateItems.length === 0) {
    const auditRef = db
      .collection("meta_dev_audit")
      .doc(uid)
      .collection("events")
      .doc();

    await auditRef.set({
      uid,
      operationId,
      action: "reset_purchased_entitlements",
      resetCount: 0,
      itemIds: [],
      preservedCommerceHistory: true,
      preservedWalletLedger: true,
      createdAt: FieldValue.serverTimestamp(),
    });

    return {
      resetCount: 0,
      auditId: auditRef.id,
    };
  }

  const itemRefs = candidateItems.map((itemId) =>
    db
      .collection("inventories")
      .doc(uid)
      .collection("items")
      .doc(itemId),
  );

  const itemSnapshots = await db.getAll(...itemRefs);
  const resetItemIds: string[] = [];

  for (const snapshot of itemSnapshots) {
    if (!snapshot.exists) {
      continue;
    }

    const data = snapshot.data() ?? {};
    const itemId = snapshot.id;
    const matchingTransactions =
      purchaseTransactionIdsByItem.get(itemId);

    if (
      data.owned !== true ||
      typeof data.lastTransactionId !== "string" ||
      matchingTransactions === undefined ||
      !matchingTransactions.has(data.lastTransactionId)
    ) {
      continue;
    }

    resetItemIds.push(itemId);
  }

  const batch = db.batch();
  const timestamp = FieldValue.serverTimestamp();

  for (const itemId of resetItemIds) {
    const itemRef = db
      .collection("inventories")
      .doc(uid)
      .collection("items")
      .doc(itemId);

    batch.set(
      itemRef,
      {
        owned: false,
        quantity: 0,
        revokedAt: timestamp,
        lastSource: "phase9_dev_purchase_reset",
        updatedAt: timestamp,
      },
      {merge: true},
    );
  }

  const auditRef = db
    .collection("meta_dev_audit")
    .doc(uid)
    .collection("events")
    .doc();

  batch.create(auditRef, {
    uid,
    operationId,
    action: "reset_purchased_entitlements",
    resetCount: resetItemIds.length,
    itemIds: resetItemIds,
    preservedCommerceHistory: true,
    preservedWalletLedger: true,
    createdAt: timestamp,
  });

  await batch.commit();

  return {
    resetCount: resetItemIds.length,
    auditId: auditRef.id,
  };
}
