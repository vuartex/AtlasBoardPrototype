# AtlasBoard — Phase 15I Moderation + Data Lifecycle

**Baseline:** `dcfc6550ea20a0f4719714abb5ecc2d125765021` (`main`, verified 2026-10-10 America/Denver).  
**Status:** Candidate implementation, NOT accepted until combined test and operational review PASS.  
**Scope:** Existing four chat callables; no new Cloud Function exports, Steam/Unity scripts, self-hosted servers, or production credentials.

## Enforcement contract

- All four lobby/match chat endpoints require Firebase Auth and retain staged App Check policy.
- Trusted moderator claims: **`atlasChatModerator: true`** or **`admin: true`**. Generic `role: moderator` and `moderator: true` no longer grant cross-feature privilege. Only privileged Admin SDK processes should mint these custom claims. After a claim is changed/revoked, existing ID tokens may remain valid until refresh/expiry; urgent revocation requires additional token-revocation enforcement.
- Ordinary players may manage only their own local mute/block lists and submit reports. They cannot remove messages, issue sanctions, change moderation config, review reports or list all reports.
- Lobby and match membership is required to read/send/report. Reporter-target validation excludes inactive lobby members and kicked/AFK-removed match seats. Message reports load the authoritative document under the **same** chat scope; client-supplied `targetAccountId` is ignored.
- Chat read excludes `removed` messages and logically expired documents even if background Firestore TTL has not deleted them. Reporting an expired message is denied.
- Message removal writes its original text/author metadata into backend-only `chat_moderation_audit` **atomically** with redacting the public message; duplicate removals do not create duplicate evidence. Sanction and config mutations are batched atomically with their audit events.
- Moderator operations remain accessed through `lobbyChatSend` / `matchChatSend` / `lobbyChatList` / `matchChatList`; client public contract remains intact. New write operation `moderator_review_report` has `reportId`, `action`: `resolve`, `dismiss`, `reopen`, optional `comment` and a trusted moderator claim.

## Collection inventory and retention

| Collection / collection group | Data | Intended lifetime | TTL on `expiresAt`? |
|---|---|---|---|
| `lobbies/{id}/chat_messages`, `matches/{id}/chat_messages` (`chat_messages`) | Public chat | Configured 1–90 days; default **30 days** | **Yes** |
| `lobbies/{id}/chat_rate_limits`, `matches/{id}/chat_rate_limits` (`chat_rate_limits`) | Anti-spam state | **1 day** after update | **Yes** |
| `chat_report_rate_limits` | Report throttles | **1 day** after update | **Yes** |
| `chat_reports` | Reporter, accused, bounded content snapshot, review state | **Open**: no automatic expiry. **Resolved/dismissed**: configured 7–365 days after review; default **180 days** | **Yes**, only on closed cases with `expiresAt` |
| `chat_moderation_audit` | Evidence, moderator actions, original removed text | Explicit controlled archival/deletion review; no automatic deletion without case-hold verification | **No** |
| `chat_sanctions` | Active chat mute/ban/disable/restricted state | Until explicit privileged update; expired numeric mutes become inactive | **No** |
| `chat_moderation_config/current` | Policy configuration | Persistent | **No** |
| `chat_user_safety/{uid}/muted_accounts` / `blocked_accounts` | Owner-scoped player mute/block preferences | Until owner unblocks/unmutes or account lifecycle cleanup | **No** |
| `lobbies/{id}/chat_meta`, `matches/{id}/chat_meta` | Last message ordering metadata | As long as parent scope data lifecycle requires | **No** |

Note: retention defaults in existing `chat.ts` are unchanged. The prior 365-day automatic TTL on **new** audit records is intentionally removed to avoid evidence deletion; a separate privacy-compliant archival/erasure policy, restricted staff access and review cadence must be established before production close. Open-case holds are **not** a license for indefinite unmanaged personal-data retention.

Deleting a parent lobby or match document does **not** remove its subcollections; a separate lifecycle cleanup strategy is required for parent scopes. All listed protected collections remain server-owned under the default-deny Firestore Security Rules.

## Legacy-data migration — before enabling/changing live TTL policies

Earlier versions issued `expiresAt` on **open** `chat_reports` and on all `chat_moderation_audit` entries. If TTL is enabled before those timestamps are removed, investigations/evidence may be irreversibly deleted.

The bundled `moderation-retention-backfill.mjs` inspects only the relevant collections, defaults to **dry-run**, and removes `expiresAt` for legacy open reports and audit records only with `--apply` (no document deletion). Disable or pause existing TTL on `chat_reports` and `chat_moderation_audit` before using it; verify backup/snapshot and legal hold policy before a production run.

Emulator dry-run (from `Backend/Firebase/functions`):

```powershell
$env:FIRESTORE_EMULATOR_HOST = '127.0.0.1:8080'
node scripts/moderation-retention-backfill.mjs
node scripts/moderation-retention-backfill.mjs --apply
```

Production **manual review only**; do not run without approval, service-account access review, backups, previewed counts and explicit flags:

```powershell
Remove-Item Env:FIRESTORE_EMULATOR_HOST -ErrorAction SilentlyContinue
node scripts/moderation-retention-backfill.mjs --project=atlasboard-usa --allow-production
# Only after explicit approval; do not run automatically:
# node scripts/moderation-retention-backfill.mjs --project=atlasboard-usa --allow-production --confirm-production-writes --apply
```

## TTL deployment checklist (production operations; NOT run by this package)

Configure only after confirming migration and operational sign-off. Google Cloud CLI commands (PowerShell compatible):

```powershell
gcloud firestore fields ttls update expiresAt --collection-group=chat_messages --enable-ttl --project=atlasboard-usa
gcloud firestore fields ttls update expiresAt --collection-group=chat_rate_limits --enable-ttl --project=atlasboard-usa
gcloud firestore fields ttls update expiresAt --collection-group=chat_report_rate_limits --enable-ttl --project=atlasboard-usa
gcloud firestore fields ttls update expiresAt --collection-group=chat_reports --enable-ttl --project=atlasboard-usa
```

Never enable automatic TTL on `chat_moderation_audit`, `chat_sanctions`, `chat_moderation_config`, `chat_user_safety` or `chat_meta`. Verify policies in Google Cloud Console > Firestore > Time-to-live, or using `gcloud firestore fields ttls list`. Firestore TTL deletion is asynchronous (often within 24 hours); it cannot be proven by Firebase emulator E2E. TTL processing incurs billed delete operations.

Reference: <https://firebase.google.com/docs/firestore/ttl> and <https://docs.cloud.google.com/sdk/gcloud/reference/firestore/fields/ttls/update>.

## Combined Phase 15I validation

Apply only the files in the ZIP to your **current local repository** and preserve unrelated local changes. Do not copy any `.meta` files (none included). Shut down any already running Auth/Firestore/Functions emulator instance to prevent port conflicts. From **`Backend/Firebase/functions`**:

```powershell
node scripts/phase15i-validation.mjs
```

The single command runs **TypeScript build → focused ESLint → static moderation security audit → fresh emulator boot → original + expanded moderation E2E → existing Firestore rules E2E** and shuts down its emulators. It does not auto commit/push or deploy.

If using `ATLAS_ENFORCE_APP_CHECK=true` in the emulator, configure `ATLAS_TEST_APP_CHECK_TOKEN` privately for the runtime tests, or leave staged enforcement disabled for this focused authorization suite. A non-empty App Check header accepted by the Functions emulator is not cryptographic production-token validation; the deferred Phase 15K deployed test remains mandatory.

Acceptance gate: confirm build/lint/static audit and every runtime test PASS, inspect any dev/production migration counts, confirm official TTL field policies in actual Google Cloud and verify live access protections separately. **Until that evidence exists, Phase 15I remains IN PROGRESS, not DONE.**
