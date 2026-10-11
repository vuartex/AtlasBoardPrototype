# Atlas Board Frontend Error Codebook

**Canonical purpose:** stable player-facing support/error codes without exposing infrastructure details.  
**UI rule:** the player sees only a localized generic message plus an `ATBxxx` code.  
**Diagnostics rule:** technical detail may be logged in Development diagnostics after redaction.  
**Do not show in public UI:** provider names, hostnames, ports, URLs, project IDs, tokens, auth headers, raw service responses, stack traces, database/collection names or secret/configuration names.

This codebook currently defines **113 stable codes**.

## Public message policy

Normal failure:

```text
We couldn't complete this step. Please try again. Code: ATB201
```

Delay:

```text
This is taking a little longer than expected. Code: ATB301
```

Supported UI languages:

- English (`en`)
- Türkçe (`tr`)
- Español (`es`)
- Français (`fr`)
- Deutsch (`de`)
- 한국어 (`ko`)
- Русский (`ru`)

The player-facing string must never contain the internal meaning from this document.

## Loading / timing diagnostics

`AtlasBoardLoadingExperience` emits timing records through Unity logging. In Development builds they are automatically captured by `AtlasBoardRuntimeDiagnostics`.

Examples:

```text
ATB_BOOTSTRAP event=startup_begin
ATB_BOOTSTRAP phase=identity durationMs=412 success=true
ATB_BOOTSTRAP phase=profile durationMs=87 success=true
ATB_BOOTSTRAP phase=lobby_prefetch durationMs=520 success=true note=rooms=3
ATB_BOOTSTRAP event=startup_ready totalMs=1284 identityReady=true

ATB_LOADING_DELAY mode=Startup bucket10s=1 elapsedMs=10002 code=ATB101
ATB_MATCH_LOAD event=begin
ATB_MATCH_LOAD event=authoritative_ready elapsedMs=714 revision=4

ATB_FRONTEND_CODE code=ATB820 category=Connectivity severity=Recoverable phase=lobby_join detail=transport_failure
```

The logger must never record credentials or raw authorization material.

## Delay thresholds

Delay codes are observations, not proof of a backend failure.

| Code | Threshold |
|---|---|
| ATB101 | Startup exceeded 10 seconds. |
| ATB102 | Startup exceeded 20 seconds. |
| ATB103 | Startup exceeded 30 seconds. |
| ATB301 | Initial match synchronization exceeded 10 seconds. |
| ATB302 | Initial match synchronization exceeded 20 seconds. |
| ATB303 | Initial match synchronization exceeded 30 seconds. |

At 20 seconds the loading UI may expose **Retry**. At 30 seconds it may also expose **Continue** so the player is not trapped behind the loading layer.

## Client

| Code | Internal meaning | Severity |
|---|---|---|
| ATB001 | Unexpected managed client failure. | Recoverable |
| ATB002 | Local state is inconsistent with the requested action. | Recoverable |
| ATB003 | Requested client operation is not available in the current state. | Recoverable |
| ATB004 | Required UI controller or presentation surface was unavailable. | Recoverable |
| ATB005 | Required local resource was unavailable. | Recoverable |
| ATB006 | Local client data could not be read. | Recoverable |
| ATB007 | Local client data could not be written. | Recoverable |
| ATB008 | Client received a result that could not be represented safely. | Recoverable |
| ATB009 | Unknown client failure. | Recoverable |
## Startup

| Code | Internal meaning | Severity |
|---|---|---|
| ATB100 | Startup pipeline could not begin. | Blocking |
| ATB101 | Startup exceeded the first 10-second delay threshold. | Informational |
| ATB102 | Startup exceeded the 20-second delay threshold. | Informational |
| ATB103 | Startup exceeded the 30-second delay threshold. | Informational |
| ATB110 | Online session/identity preparation did not complete. | Blocking |
| ATB111 | Existing client session is no longer valid. | Recoverable |
| ATB120 | Optional startup profile preparation failed. | Recoverable |
| ATB130 | Optional startup online-data preparation failed. | Recoverable |
| ATB140 | Startup localization preparation failed. | Recoverable |
| ATB150 | Runtime configuration is incompatible with the requested environment. | Blocking |
| ATB160 | Startup readiness could not be determined. | Recoverable |
## Lobby

| Code | Internal meaning | Severity |
|---|---|---|
| ATB200 | Lobby creation failed. | Recoverable |
| ATB201 | Lobby join failed. | Recoverable |
| ATB202 | Public lobby discovery failed. | Recoverable |
| ATB203 | Join-by-code failed. | Recoverable |
| ATB204 | Lobby access/password update failed. | Recoverable |
| ATB205 | Lobby settings update failed. | Recoverable |
| ATB206 | Lobby seat configuration failed. | Recoverable |
| ATB207 | Lobby ready-state update failed. | Recoverable |
| ATB208 | Lobby leave/close operation failed. | Recoverable |
| ATB209 | Authoritative lobby start failed. | Recoverable |
| ATB210 | Lobby snapshot synchronization failed. | Recoverable |
| ATB211 | Lobby host heartbeat failed. | Recoverable |
| ATB212 | Requested lobby no longer exists or is unavailable. | Recoverable |
| ATB213 | Requested lobby has no suitable capacity. | Recoverable |
| ATB214 | Lobby client/content/protocol version is incompatible. | Blocking |
| ATB215 | Lobby action was rejected by authoritative access rules. | Recoverable |
## Match

| Code | Internal meaning | Severity |
|---|---|---|
| ATB300 | Match runtime initialization failed. | Recoverable |
| ATB301 | Initial authoritative match state exceeded the first 10-second threshold. | Informational |
| ATB302 | Initial authoritative match state exceeded the 20-second threshold. | Informational |
| ATB303 | Initial authoritative match state exceeded the 30-second threshold. | Informational |
| ATB304 | Reconnect/catch-up operation failed. | Recoverable |
| ATB305 | Client gameplay intent submission failed. | Recoverable |
| ATB306 | Host authoritative state publication failed. | Recoverable |
| ATB307 | Authoritative match snapshot request failed. | Recoverable |
| ATB308 | Match presence update failed. | Recoverable |
| ATB309 | Host authority recovery/migration failed. | Recoverable |
| ATB310 | Host pending-intent list operation failed. | Recoverable |
| ATB311 | Host intent acknowledgement failed. | Recoverable |
| ATB312 | Disconnected-seat sweep failed. | Recoverable |
| ATB313 | Reconnect expiry processing failed. | Recoverable |
| ATB314 | Rematch preparation failed. | Recoverable |
| ATB315 | Active-match leave failed. | Recoverable |
| ATB316 | Authoritative match state failed client validation. | Recoverable |
| ATB317 | Client state was stale relative to authority. | Recoverable |
| ATB318 | Turn/dice presentation could not synchronize. | Recoverable |
| ATB319 | Match-result synchronization failed. | Recoverable |
## Communication

| Code | Internal meaning | Severity |
|---|---|---|
| ATB400 | Lobby chat operation failed. | Recoverable |
| ATB401 | Match chat operation failed. | Recoverable |
| ATB410 | Report submission failed. | Recoverable |
| ATB411 | Authorized moderation action failed. | Recoverable |
| ATB420 | Content filtering service was unavailable. | Recoverable |
## Profile

| Code | Internal meaning | Severity |
|---|---|---|
| ATB500 | Progression service unavailable. | Recoverable |
| ATB501 | Player profile load failed. | Recoverable |
| ATB502 | Match history load failed. | Recoverable |
| ATB503 | Achievement synchronization failed. | Recoverable |
| ATB510 | Settings synchronization failed. | Recoverable |
| ATB511 | Cloud preference synchronization failed. | Recoverable |
| ATB520 | Cosmetic selection synchronization failed. | Recoverable |
| ATB530 | Profile presentation could not be refreshed. | Recoverable |
## Economy

| Code | Internal meaning | Severity |
|---|---|---|
| ATB600 | Store/catalog data unavailable. | Recoverable |
| ATB601 | Seasonal content unavailable. | Recoverable |
| ATB610 | Wallet data unavailable. | Recoverable |
| ATB620 | Inventory data unavailable. | Recoverable |
| ATB630 | Purchase operation failed. | Recoverable |
| ATB631 | Refund operation failed. | Recoverable |
| ATB640 | Promo redemption failed. | Recoverable |
| ATB650 | Reward claim failed. | Recoverable |
| ATB660 | Authoritative transaction conflicted or replayed unexpectedly. | Recoverable |
## Platform

| Code | Internal meaning | Severity |
|---|---|---|
| ATB700 | Platform runtime unavailable. | Recoverable |
| ATB701 | External platform provider unavailable. | Recoverable |
| ATB710 | Platform session preparation failed. | Recoverable |
| ATB720 | Platform account-link operation failed. | Recoverable |
| ATB721 | Returning platform sign-in failed. | Recoverable |
| ATB730 | Client security/attestation handshake failed. | Recoverable |
| ATB740 | Platform invite/join request failed. | Recoverable |
| ATB750 | Platform entitlement check failed. | Recoverable |
| ATB760 | Platform achievement synchronization failed. | Recoverable |
## Connectivity

| Code | Internal meaning | Severity |
|---|---|---|
| ATB800 | Operating system reports no network route. | Recoverable |
| ATB810 | Request exceeded its allowed time. | Recoverable |
| ATB811 | Observed response latency exceeded the UX threshold. | Informational |
| ATB820 | Transport failed before a valid application response. | Recoverable |
| ATB821 | Service returned an unexpected status. | Recoverable |
| ATB830 | Response could not be parsed. | Recoverable |
| ATB831 | Response did not match the expected authenticated session. | Recoverable |
| ATB840 | Client protocol version mismatch. | Blocking |
| ATB841 | Client content version mismatch. | Blocking |
| ATB850 | Required online service is temporarily unavailable. | Recoverable |
| ATB860 | Authoritative service rate-limited the request. | Recoverable |
| ATB870 | Request was cancelled because the client state changed. | Informational |
| ATB880 | Request failed authoritative security validation. | Recoverable |
## Presentation

| Code | Internal meaning | Severity |
|---|---|---|
| ATB900 | UI presentation failed. | Recoverable |
| ATB901 | Input action could not be processed. | Recoverable |
| ATB910 | Localization entry/language resolution failed. | Recoverable |
| ATB911 | Required localized font could not be resolved. | Recoverable |
| ATB920 | Required local asset could not be loaded. | Recoverable |
| ATB921 | Scene transition/load failed. | Recoverable |
| ATB930 | Graphics/presentation subsystem failure. | Recoverable |
| ATB940 | Audio subsystem failure. | Recoverable |
| ATB950 | Settings storage operation failed. | Recoverable |
| ATB960 | Client received a low-memory signal. | Recoverable |
| ATB970 | Platform-specific client operation failed. | Recoverable |
| ATB980 | Diagnostic output could not be written. | Recoverable |
## Unknown

| Code | Internal meaning | Severity |
|---|---|---|
| ATB990 | Failure did not map to a more specific code. | Recoverable |

## Operational rules

1. Error codes are stable support identifiers. Never reuse an existing code for a different meaning.
2. New client surfaces should call `AtlasBoardFrontendErrorCodes.Report(...)` instead of showing raw technical strings.
3. Provider/backend implementation details are diagnostic-only.
4. Development logs should include the ATB code, phase, duration and a short redacted note.
5. A timeout/delay code records the client observation; it does not by itself prove server failure.
6. Local emulator cold starts and deployed production latency must be measured separately.
7. The loading experience must not disable reconnect, authoritative snapshots, host migration, polling or security validation.
8. Speculative public-lobby warm-up is Development/Editor only. Release does not add that extra call.
9. Startup profile warm-up is non-blocking after identity; failure is logged with an ATB code but does not expose infrastructure names to the player.
10. When integrating legacy UI errors later, map the user-visible surface to an ATB code and keep the existing technical message only in logs.
