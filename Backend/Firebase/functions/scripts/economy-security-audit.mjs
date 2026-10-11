import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";

const scriptDir =
  path.dirname(
    fileURLToPath(import.meta.url),
  );

const functionsDir =
  path.resolve(
    scriptDir,
    "..",
  );

function read(relativePath) {
  return fs.readFileSync(
    path.join(
      functionsDir,
      relativePath,
    ),
    "utf8",
  );
}

function fail(message) {
  console.error(
    "Phase 15H economy security audit FAIL: " +
    message,
  );
  process.exit(1);
}

const wallet =
  read("src/economy/wallet.ts");

const inventory =
  read("src/economy/inventory.ts");

const commerce =
  read("src/economy/commerce.ts");

const promo =
  read("src/economy/promo.ts");

const seasonal =
  read("src/economy/seasonal.ts");

for (const marker of [
  "makeLedgerEntryId(",
  "assertIdempotentPayloadMatches(",
  "transaction.create(ledgerRef",
  'collection("wallet_ledger")',
]) {
  if (!wallet.includes(marker)) {
    fail(
      "wallet invariant missing: " +
      marker,
    );
  }
}

for (const marker of [
  "transaction.get(catalogRef)",
  "assertIdempotentPayloadMatches(",
  "transaction.create(eventRef",
  "catalogData.active !== true",
]) {
  if (!inventory.includes(marker)) {
    fail(
      "inventory invariant missing: " +
      marker,
    );
  }
}

for (const marker of [
  "transaction.get(catalogRef)",
  "readCatalogWalletPrice(",
  "assertPurchaseReplayMatches(",
  "COMMERCE_OWNER_MISMATCH",
  "transaction.create(chargeLedgerRef",
  "transaction.create(refundLedgerRef",
  "transaction.create(purchaseEventRef",
]) {
  if (!commerce.includes(marker)) {
    fail(
      "commerce invariant missing: " +
      marker,
    );
  }
}

for (const marker of [
  "makePromoId(",
  "makePromoRedemptionEventId(",
  "globalLimit",
  "perAccountLimit",
  "parseRewards(",
]) {
  if (!promo.includes(marker)) {
    fail(
      "promo invariant missing: " +
      marker,
    );
  }
}

if (
  !/transaction\.create\(\s*redemptionEventRef\s*,/.test(
    promo,
  )
) {
  fail(
    "promo invariant missing: transactional immutable redemption event create",
  );
}

if (
  !/transaction\.update\(\s*promoRef\s*,/.test(
    promo,
  )
) {
  fail(
    "promo invariant missing: transactional global redemption counter update",
  );
}

for (const marker of [
  "function assertTransactionalSeasonalRuntime(",
  "transaction.get(eventRuntimeRef)",
  "transaction.get(definitionRef)",
  "transaction.get(tierRef)",
  "transaction.get(catalogRef)",
  "SEASONAL_CHALLENGE_CHANGED",
  "SEASONAL_TRACK_CHANGED",
  "SEASONAL_ITEM_CHANGED",
]) {
  if (!seasonal.includes(marker)) {
    fail(
      "seasonal transactional-authority invariant missing: " +
      marker,
    );
  }
}

console.log(
  "Phase 15H economy security audit PASS. " +
  "Wallet ledger/idempotency, inventory catalog authority, commerce " +
  "server-price/owner/refund evidence, promo limits/replay evidence, and " +
  "transactional seasonal config revalidation are present.",
);
