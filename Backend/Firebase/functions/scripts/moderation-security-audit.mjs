import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {fileURLToPath} from "node:url";
import {dirname, resolve} from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(resolve(here, "../src/chat/chat.ts"), "utf8");
const rules = readFileSync(resolve(here, "../../firestore.rules"), "utf8");
let checks = 0;
function check(condition, description) {
  assert(condition, description);
  checks++;
  console.log(`PASS ${description}`);
}

for (const fn of ["lobbyChatSend", "lobbyChatList",
  "matchChatSend", "matchChatList"]) {
  const re = new RegExp(`export const ${fn} = onCall\\([\\s\\S]*?` +
    `enforceAppCheck: ATLAS_ENFORCE_APP_CHECK,[\\s\\S]*?` +
    `const uid = requireUid\\(request\\);`);
  check(re.test(source), `${fn} authenticates and stages App Check`);
}
check(source.includes("claims.atlasChatModerator === true || claims.admin === true"),
  "Moderator authority is explicit and cannot follow generic role labels");
check(source.includes("requireModerator(token)"),
  "Moderator operations require trusted Firebase token claims");
check(source.includes("return item.removed !== true && unexpired"),
  "Chat reads exclude deleted and logically expired records");
check(source.includes("CHAT_MESSAGE_EXPIRED"),
  "Message reports reject expired chat content");
check(source.includes("return {ok: true, messageId, alreadyRemoved}"),
  "Message-removal replay handling is explicit");
check(source.includes("tx.create(auditRef, moderatorAuditRecord("),
  "Removal/review transactions atomically create audit records");
check(source.includes("batch.create(db.collection(\"chat_moderation_audit\").doc()"),
  "Sanctions and configuration use atomic audit batches");
const openReports = Array.from(source.matchAll(
  /collection\("chat_reports"\)\.add\(\{([\s\S]*?)schemaVersion: 2,/g));
check(openReports.length === 2 && openReports.every((m) =>
  m[1].includes('retentionPolicy: "open_case_hold_v1"') &&
  !m[1].includes("expiresAt:")),
"Both report creation routes hold open investigations without TTL");
const auditBlock = source.slice(source.indexOf("function moderatorAuditRecord("),
  source.indexOf("/**\n * Validates and persists one chat message"));
check(auditBlock.includes("review_required_no_automatic_ttl_v1") &&
  !auditBlock.includes("expiresAt:"),
"Immutable moderation audit documents do not auto-expire");
check(source.includes("moderator_review_report") &&
  source.includes('expiresAt: status === "open" ? FieldValue.delete()'),
"Moderator case review supports open-hold, close and reopen");
check(rules.includes("match /{document=**}") &&
  rules.includes("allow read, write: if false;"),
"Firestore default deny blocks direct client mutation");
console.log(`Phase 15I static security audit PASS: ${checks} checks.`);
