// Inspect/migrate pre-Phase-15I open reports and moderation audit records.
// Dry-run by default; never deletes documents. Explicit opt-in is mandatory.
import {initializeApp} from "firebase-admin/app";
import {getFirestore, FieldValue} from "firebase-admin/firestore";

const args = process.argv.slice(2);
const apply = args.includes("--apply");
const productionAcknowledged = args.includes("--allow-production");
const projectFlag = args.find((a) => a.startsWith("--project="));
const projectId = projectFlag?.slice("--project=".length) || "atlasboard-usa";
const emulator = Boolean(process.env.FIRESTORE_EMULATOR_HOST);
if (!emulator && (!productionAcknowledged || !projectFlag)) {
  console.error("Refusing production connection. Set --project=PROJECT " +
    "and --allow-production explicitly. Defaults to dry-run.");
  process.exit(2);
}
if (apply && !emulator && !args.includes("--confirm-production-writes")) {
  console.error("Production writes require --confirm-production-writes.");
  process.exit(2);
}
const db = getFirestore(initializeApp({projectId}));
let inspected = 0;
let candidates = 0;
let patched = 0;

async function scan(collectionName, shouldRemoveExpiry) {
  let cursor;
  while (true) {
    let query = db.collection(collectionName)
      .orderBy("__name__").limit(250);
    if (cursor) query = query.startAfter(cursor);
    const snap = await query.get();
    if (snap.empty) break;
    const batch = db.batch();
    let writes = 0;
    for (const doc of snap.docs) {
      inspected++;
      if (!shouldRemoveExpiry(doc.data())) continue;
      candidates++;
      if (apply) {
        batch.update(doc.ref, {expiresAt: FieldValue.delete()});
        writes++;
      }
    }
    if (writes > 0) {
      await batch.commit();
      patched += writes;
    }
    cursor = snap.docs[snap.docs.length - 1];
  }
}

await scan("chat_reports", (data) =>
  (data.status === "open" || !data.status) &&
  Object.hasOwn(data, "expiresAt"));
await scan("chat_moderation_audit", (data) =>
  Object.hasOwn(data, "expiresAt"));
console.log(JSON.stringify({projectId, emulator, apply,
  inspected, candidates, patched}, null, 2));
console.log(apply ? "Backfill completed." :
  "DRY RUN only. Add --apply to patch the listed candidate records.");
