# AtlasBoard — Phase 15J Production Operations / Environment Hardening

**Source baseline:** `dcfc6550ea20a0f4719714abb5ecc2d125765021` (GitHub `main`, checked 2026-10-10 America/Denver).  
**Prerequisite:** Phase 15I local/emulator accepted: TypeScript build, focused ESLint, 15 static security checks, 49 moderation E2E, 17 Firestore rules E2E.  
**Status:** 15J candidate package, NOT accepted until local Phase 15J validation PASS. No claims about deployed production operations.

## Changes (one unified Phase 15J)

- Firebase Auth, Firestore, Functions, and Emulator UI bind only to `127.0.0.1`. No accidental LAN exposure from `0.0.0.0`. Unity on the same Windows PC continues to use loopback ports `9099`, `8080`, `5001`. Remote/mobile device testing will need a separately secured transport, NOT unbinding local emulators.
- Harden Steam publisher ticket and App Check diagnostic logs: remove raw exception strings and Steam response description strings; replace with stable failure classes and HTTP status. Exception text can include a URL with the publisher key/ticket. Never log raw keys/tickets, Firebase ID/custom/App Check tokens, or unredacted credential-bearing request URLs.
- Remove `accountId` and `steamId` from four Steam link/sign-in **operational logs only**. Canonical identity, persisted mappings and callable **response payloads** remain unchanged.
- Ignore common env variants and key files in `Backend/Firebase/.gitignore`; `functions/*.local` remains ignored. `functions/.env.local` is NEVER packaged, uploaded to chat, committed or printed.
- Add an explicit **read-only** release preflight. Shared development project `atlasboard-usa` + development Steam AppID `480` are for local emulators only. Staging is not provisioned as part of this patch. Production remains intentionally BLOCKED until 15K.
- One combined validator builds, lints modified TS files, performs static operations audit and policy unit tests, then starts fresh emulators and runs runtime smoke + existing Firestore rules E2E. No live Steam request, deploy or Git operation.

## Operational separation and secret handling

| Environment | Project | Steam | App Check | State |
|---|---|---|---|---|
| Local development | `atlasboard-usa` emulator only | AppID 480 permitted | staged `.env.local` debug/provider policy | tested locally (15I) |
| Staging | Must be a separately provisioned Firebase/GCP project | explicitly configured | deployed validation before promotion | **not provisioned by 15J** |
| Production | independently approved Firebase/GCP project (do not assume `atlasboard-usa` is production) | real AtlasBoard AppID | deployed enforcement and negative cryptographic tests required | **BLOCKED pending 15K** |

Publisher Web API key must be injected with managed Firebase/GCP Secret Manager via `defineSecret` and explicitly bound only to callables requiring it. Never store in Unity source, repo, `.firebaserc`, `firebase.json`, logs, screenshots, or tickets. Verify IAM least privilege and secret rotation procedures before deployment. The prior exposed App Check debug credential must be revoked and replaced privately before production close; this patch does not perform credential rotation.

Release procedures must specify the target project *explicitly*. Do not use `firebase deploy` alone when `.firebaserc` has a development default project. **Nothing in this ZIP deploys or changes GCP resources.** Preflight intentionally refuses staging and production, rather than accepting unverified strings.

## Required telemetry and alerting for staging/production acceptance

The following is an operations runbook and alert specification, NOT proof that Cloud Monitoring resources exist:

1. **Cloud Functions**: request/error counts per callable, 5xx proportion, latency (p50/p95/p99), invocations/instance quota, cold-start or timeout spikes. Suggested initial warning: error ratio above 2% over 15 minutes with >=100 requests; critical above 5% for 5 minutes with >=50 requests. Exclude expected 401/403/429 from server-error denominator, but track anomalous surges separately.
2. **Firebase Auth / App Check**: count valid/missing/invalid/expired attestations and rejected authentications by endpoint; alert on sustained unusual rejection volume. Do not log token values or client-identifying raw tickets. Production App Check enforcement is still OFF by design until 15K.
3. **Firestore**: quota/read/write errors, concurrent contention, indexes, active TTL policies, expired-document backlog. Verify TTL control-plane status in GCP; emulator cannot prove TTL deletion. Preserve open reports and moderation audit evidence as specified in `PHASE_15I_MODERATION_DATA_LIFECYCLE.md`.
4. **Security**: rate-limit 429 counts, moderation action failures, Steam publisher verification failure classes, secret access anomalies, abuse rule spikes. Route critical alert to an on-call destination verified by the team. Do not invent or hard-code email/phone destinations.
5. **Privacy**: bounded retention for operational logs, restricted log-reader IAM, deny logging of PII/secrets, incident evidence access process and human review. Query production logs before launch for accidental tokens/keys and rotate compromised material.

## Incident/recovery runbook (manual, non-destructive)

- Alert triage: identify impacted function and scope, compare release revision, verify Auth/App Check rejection metrics and Firestore health. Do not paste tokens or confidential logs into ChatGPT.
- Containment: use GCP IAM and existing rate limits, revoke compromised secrets/debug tokens, isolate affected staging/production project. Keep audit and open report evidence on legal hold where required.
- Recovery: roll back to previously verified deployment revision using cloud-native tooling after verifying backward schema compatibility; never destroy active matches or Firestore records merely to remove a bad release. Re-run build, rules, Steam/App Check and moderation E2E.
- Reporting: write incident timeline, affected scope, reversible config change, test evidence and follow-up owner. Define who receives security alerts and who may operate production deployments prior to 15K.

## Applying the guarded patch

Extract the Phase 15J ZIP into the **root** `C:\Users\crazy\AtlasBoardPrototype`. No existing source file is replaced during ZIP extraction; only new `.mjs` scripts and this document are installed. Then run from PowerShell:

```powershell
cd 'C:\Users\crazy\AtlasBoardPrototype\Backend\Firebase\functions'
node .\scripts\phase15j-apply.mjs --dry-run
node .\scripts\phase15j-apply.mjs
node .\scripts\phase15j-validation.mjs
```

The installer verifies the original Git blob SHA against the committed 15F–H source for each of four existing files before changing anything. It does NOT touch local Phase 15I chat/moderation source or its scripts. If local source differs, patch refuses to overwrite it. On a failed validation, stop and inspect the output; do not promote/deploy.

**Important:** Stop manually running Firebase emulators before validation. Validation starts and stops Auth/Firestore/Functions itself. Unlike the Phase 15I negative-authorization suite, this Phase 15J smoke does **not** require disabling App Check. The original `.env.local` and `STEAM_APP_ID=480` are untouched, and the test does not contact Steam.

## Acceptance / deferred gates

Local acceptance requires: guarded apply success; `npm.cmd run build`; focused ESLint; 15J static audit; 15J preflight tests; fresh-emulator smoke; Firestore rules E2E. The package does not modify Unity or require Unity recompilation except normal project refresh.

**Still deferred:** provisioning actual staging/production projects and IAM, Cloud Monitoring alert resources, key/debug-token rotation, verifying published Firestore TTL policies, production App Check enforcement and real Steam AppID/publisher-secret runtime acceptance. Those are operational acceptance requirements, not emulator PASS claims, and will be carried to 15K and pre-launch checklists. Phase 15J can be labeled DONE (local hardening) after combined validation, but never DONE (production operations).

Once Phase 15J local acceptance passes, automatically proceed to Phase 15K (Production Steam / Backend Security Close); do not commit or push without user's instruction.
