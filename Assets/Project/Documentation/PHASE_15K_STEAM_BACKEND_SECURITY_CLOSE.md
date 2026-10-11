# AtlasBoard — Phase 15K: Steam / Backend Security Close

**Prepared:** 2026-10-10, America/Denver.  
**Latest inspected pushed GitHub main:** `dcfc6550ea20a0f4719714abb5ecc2d125765021`.  
**Prerequisites:** accepted local Phase 15I and 15J changes already present on disk. No local file should be reset to older GitHub `main` content.  
**Status of this delivery:** Phase 15K local candidate. Production security close is **NOT** granted.

## Intent and patch scope

1. Add `src/security/steam-environment.ts` to fail closed when any **deployed** Steam verifier is configured with development AppID **480**. Local Functions Emulator still permits 480, preserving development behavior. This is a minimum safety check, **not** production Steam ticket proof and **not** an App Check rollout switch.
2. Add a guarded `phase15k-apply.mjs` that installs this guard into both `verifySteamWebApiTicket` (Steam link and returning sign-in) and `verifySteamAppCheckProof` (custom App Check bootstrap). The installer accepts only recognizable 15J-hardened verifier anchors; it aborts on unknown or partial source and supports `--dry-run`. It does not touch Unity, Chat, Firebase config, Secret Manager, or local environment files.
3. Add read-only 15K static regression and focused compiled runtime tests, plus 15I/15J/15F/G/H regression suites. Two fresh emulator runs are required: one with App Check disabled to test functional integration, one with enforcement enabled to confirm no-token rejection and dev/bootstrap exceptions. The emulator may accept bogus non-empty App Check headers: **this does not validate cryptographic rejection**.
4. Include an optional read-only **deployed** `economyHealthCheck` App Check probe. It requires an independently provisioned nondevelopment project, HTTPS endpoint, valid Firebase ID token **and** valid App Check token, plus explicit opt-in. The three checks are: valid+valid returns 200; valid+missing returns 401; valid+invalid returns 401. A missing/invalid-token 401 without the valid positive control is insufficient evidence because the Auth token might be bad. The probe makes three read-only HTTP requests; it does not deploy or use live Steam tickets.
5. Keep release preflight fail-closed. Phase 15J `phase15j-release-preflight.mjs` remains blocked for production; the new 15K production-gate report exits nonzero and lists the still-required human/deployed proof. Nothing silently turns it green.
6. After a **full** 15K local suite passes, append an idempotent checkpoint to the existing local `MASTER_ROADMAP.md` without overwriting user work or changing prior roadmap history.

## One-command local test

Extract the ZIP to `C:\Users\crazy\AtlasBoardPrototype` without clearing any existing files. **Close all manually started Auth/Firestore/Functions emulators first** (`Ctrl+C`). In PowerShell:

```powershell
cd "C:\Users\crazy\AtlasBoardPrototype\Backend\Firebase\functions"
node .\scripts\phase15k-apply.mjs --dry-run
if ($LASTEXITCODE -ne 0) { throw "15K dry-run failed" }
node .\scripts\phase15k-apply.mjs
if ($LASTEXITCODE -ne 0) { throw "15K apply failed" }
node .\scripts\phase15k-validation.mjs
if ($LASTEXITCODE -ne 0) { throw "15K local validation failed" }
```

The validator builds and lints, validates the Stage 15I/15J/15F security boundaries, runs Steam/Chat/Economy/Firestore E2E, and checks App Check enforcement with a second fresh emulator run. It backs up `.env.local` to an ignored private local backup, temporarily switches `ATLAS_ENFORCE_APP_CHECK=false` and then `true`, and restores **byte-for-byte original `.env.local`** in a `finally` block (including on ordinary test failure). If the whole PowerShell/Node process is forcibly killed mid-test and the original cannot be restored, do **not** restart emulators until running:

```powershell
node .\scripts\phase15k-validation.mjs --restore-env
```

No production resources, secrets, or GCP control plane are touched by the combined local validator; `.env.local` remains the user's private file. A `firebase-functions` outdated warning may still appear; evaluate the installed vs pinned dependency in a separate compatibility update, do not blindly upgrade as part of security close. Firestore emulator does not enforce production TTL policies.

## Production acceptance gates (not satisfied by local PASS)

| Required production proof | Status at delivery |
|---|---|
| Real Steam AppID obtained and confirmed as the actual AtlasBoard app | BLOCKED — AppID 480 only for emulator |
| Steam publisher Web API key configured via Firebase/GCP Secret Manager and IAM least privilege verified | BLOCKED — local emulator 403 Secret Manager warning is not deployment proof |
| Previously exposed App Check debug token rotated/revoked privately | BLOCKED — do not paste it into ChatGPT |
| Correct Firebase App ID registered and custom App Check token mint demonstrated with real Steam ticket | BLOCKED — requires live evidence |
| App Check enforced on intended deployed production callables | BLOCKED — current policy intentionally disables production enforcement |
| Valid App Check+Firebase Auth positive and missing/invalid cryptographic negative deployed probe | BLOCKED — not provable in emulator |
| Verified Steam link + returning sign-in, account mapping, one-to-one integrity, AppID mismatch denial | BLOCKED — live Steam publisher acceptance required |
| Production Firestore TTL policies for `chat_messages`, `chat_rate_limits`, `chat_report_rate_limits`, and closed reports (as applicable) | BLOCKED — inspect policy state; open cases/audit must not expire inadvertently |
| Cloud Monitoring alerts and on-call destination, privacy-safe logs, quotas/incident/rollback | BLOCKED — resources not provisioned by this ZIP |
| Dev/staging/production separation and release-owner sign-off | BLOCKED — `atlasboard-usa` is treated as shared development backend |

**Important:** the static runtime Steam AppID guard does not make the Firebase App Check production gate safe to enable. The current policy only honors `ATLAS_ENFORCE_APP_CHECK` when `FUNCTIONS_EMULATOR=true`. Turning production enforcement on requires a separately approved code/deployment sequence and real provider proof; doing so prematurely can lock out real users. **No automatic deployment, secret update, TTL activation, or IAM change is performed.**

## Deployed App Check cryptographic test: separate, deliberate step

After real production resources have been provisioned, save *short-lived* valid tokens into two **locally ignored files ending in `.local`**, e.g. `phase15k-firebase-id-token.local` and `phase15k-appcheck-token.local`. The contents must be exactly one token per file, not JSON and not copied into terminal commands, tickets, docs or screenshots. Never use this test against `atlasboard-usa` or a local emulator. Replace placeholders only after a real target has been approved:

```powershell
node .\scripts\phase15k-deployed-appcheck-probe.mjs --project=YOUR_APPROVED_PRODUCTION_PROJECT --steam-app-id=YOUR_REAL_STEAM_APP_ID --target=https://europe-west1-YOUR_APPROVED_PRODUCTION_PROJECT.cloudfunctions.net/economyHealthCheck --id-token-file=phase15k-firebase-id-token.local --appcheck-token-file=phase15k-appcheck-token.local --ack-live-probes --dry-run
```

Remove `--dry-run` to issue **three live, read-only requests** and optionally add `--receipt-file=phase15k-deployed-evidence.local` to write a redacted receipt. The probe refuses a local URL, development project, AppID 480, malformed credentials, or non-Cloud-Functions host. It will NOT print token values. The probe's PASS is evidence for **one deployed callable only**, not proof that all 44 runtime callables enforce App Check.

Production gate report (intentionally returns nonzero and must not be used to permit release):

```powershell
node .\scripts\phase15k-production-gate.mjs --project=YOUR_APPROVED_PRODUCTION_PROJECT --steam-app-id=YOUR_REAL_STEAM_APP_ID
```

## Expected next work after local acceptance

Phase 15K may be labeled **LOCAL SECURITY HARDENING ACCEPTED**, not **PRODUCTION SECURITY CLOSED**, if the combined local suite passes. The production evidence gates must remain on the release checklist. Phase 16 build-readiness and QA work may be prepared next without automatically authorizing a release. Keep all ZIPs minimal and never commit/push until the user explicitly authorizes it.
