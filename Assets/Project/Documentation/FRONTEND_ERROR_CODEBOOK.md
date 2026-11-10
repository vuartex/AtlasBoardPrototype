## v5.49.12cr — Duplicate non-store item rewards (2026-11-09)

- **PROMO / World Journey:** If a cosmetic/item is already owned at the time of an otherwise valid **first** promo redemption or event claim, grant a fixed RTDB-rules-verified Gold compensation (Pawn 200, Dice 300, Board 800, Map 1200, Profile Frame 250, Emote 120). The item remains owned; no money is minted by ordinary Store purchases and an already used promo code is **still rejected** with ATB-434.
- **ATB-435 historical note:** `promo.error.item_already_owned` used to reject an item already in inventory. After this update, eligible items instead receive category Gold compensation; preserve ATB-435 mapping only for backwards compatibility, not as the standard duplicate-gift outcome.
- QA claim-marker reset is editor-only and requires `devWalletTesters/{uid}=true` plus a QA-mode World Journey proof. It never refunds or removes previously earned balances/items, and each subsequent actual claim is subject to normal RTDB Event claim verification.
- No new user-facing error codes introduced. `ATB-424` continues to mean reward verification/service failure.

---

## v5.49.12co — Public RTDB promo redemption (2026-11-09)

- `REDEEM CODE` now performs authenticated exact-code RTDB catalog lookup and an ETag-protected canonical wallet mutation. Owner QA / Dev Tester is NOT required to redeem a public promo. `ATB-434` means wallet `rewardHistory` already contains that `|CODE|` marker; it is not based on a mutable UI cache or an optional display receipt.
- `ATB-433` is returned only for a confirmed `null` exact-code lookup. `ATB-432` means inactive/non-public catalog definition. `ATB-424` remains a service/rules failure, **not** an invalid-code or already-used result. `ATB-435` is a valid item code for an already-owned cosmetic. No new user-facing code IDs were introduced.
- An informational `/users/{uid}/promoRedemptions/{CODE}` receipt may lag its atomic `metaEconomy` wallet+ledger commit; retry never pays twice. XP uses canonical `metaEconomy/promoXp`, read by Career.
- World Journey claim/overview Cloud Functions are not changed by this promo-only package; migrate/test separately. Prior `metaPromoRedeem` and `owner_qa` directives below are historical, not active instructions.

---

## v5.49.12cn — RTDB promo catalog; obsolete seeding removed (2026-11-03)

- Owner has supplied 64 definitions under root `/promoCodes`, separately from game source. **No Editor/in-game seed button, one-off seed callable, Node seeder or embedded QA code manifest remains.** Do not recreate these retired mechanisms.
- Existing authenticated `metaPromoRedeem` is the intended reward verification path; it reads `/promoCodes/{NORMALIZED_CODE}` on the server and records each successful account redemption in `/users/{uid}/promoRedemptions`. Event overview and claim callables remain unchanged.
- `ATB-424` still means the promo or event service could not be verified; it does **not** mean a nonexistent code. Backend deployment/runtime status is a separate acceptance item.
- Old notes below are historical and cannot be used to justify restoring the removed seeding route.

---

## v5.49.12cj — Canonical RTDB promo/event states (2026-11-03)

- **No new player-facing code range was created.** Preserve the existing `ATB-424`, `ATB-430`…`ATB-441` meanings recorded in `ci` below. All public UI errors remain localized, short, and free of raw Firebase paths, token details, HTTP responses and full UIDs.
- A new code may be present in an administrator-only `/promoCodes/{NORMALIZED_CODE}` but not in its current account's entitlement history. `ATB-433` means the server *confirmed* no matching active definition; `ATB-434` means the server found the same code's immutable per-UID receipt; `ATB-441` means that UID is not entitled to an owner QA code. Never treat a service error as an invalid or previously used code.
- For `ATB-424`, the client must not imply success, invent history or claim an event reward while the live Functions endpoint is unreachable. The event may show read-only 0/N placeholders with a friendly verification notice; actual 0/N becomes accepted only from server evidence.
- **Retired:** Owner Editor-only promo seed diagnostics and callable. Actual `/promoCodes` management is performed out-of-band by an authorized RTDB administrator.
- Historical `serverPromoCodes` and the legacy Node seeder are retired; the only live lookup is authenticated server-side RTDB `/promoCodes`. Existing Store purchase receipts and Daily Reward are unchanged.

---

## v5.49.12ci — Player-facing promo responses / World Journey read states (2026-11-03)

These strings are shown only after the existing authenticated Firebase callable provides a status. `ATB-424` means the response could not be verified; **it does not prove that a code is invalid or previously used**. All other codes below correspond to a concrete server-side outcome. These are UI error IDs and must not be confused with HTTP response codes.

| Player code | Server-key / condition | Turkish player-facing response | Notes |
|---|---|---|---|
| `ATB-424` | `meta.error.service_unavailable` / missing callable, unreachable backend, or unverified response | Promosyon servisine ulaşılamadı. Kod doğrulanamadı ve ödül onaylanmadı. Daha sonra yeniden dene. | No successful claim inferred |
| `ATB-430` | `promo.error.invalid_code` | Kod biçimi geçersiz. Karakterleri kontrol edip yeniden dene. | Client + backend validation |
| `ATB-431` | `account.error.authentication_required` | Kod kullanmak için hesabına giriş yap. | Verified UID needed |
| `ATB-432` | `promo.error.disabled` | Bu kod şu anda kullanım dışı. | Server definition present |
| `ATB-433` | `promo.error.not_found` | Kod bulunamadı. Henüz etkinleştirilmemiş olabilir. | Lookup against the existing catalog |
| `ATB-434` | `promo.error.already_redeemed` | Bu promosyon kodu bu hesapta daha önce kullanılmış. | Confirmed by `users/{uid}/promoRedemptions/{promoId}` then rechecked in transaction |
| `ATB-435` | `promo.error.item_already_owned` | Bu kodun verdiği ürün zaten envanterinde bulunuyor. | No inventory overwrite |
| `ATB-436` | `promo.error.invalid_reward` | Bu kodun ödülü doğrulanamadı. Herhangi bir ödül verilmedi. | Server-side reward/entitlement validation |
| `ATB-437` | `promo.error.not_started` | Bu kod henüz kullanıma açılmadı. | Server-clock gate |
| `ATB-438` | `promo.error.expired` | Bu kodun kullanım süresi sona ermiş. | Server-clock gate |
| `ATB-439` | `promo.error.limit_reached` | Bu kodun kullanım sınırına ulaşıldı. | Reserved for quotas, not claimed implemented |
| `ATB-440` | `promo.error.account_not_ready` | Hesap verileri hazır değil. Kod kullanmadan önce yeniden giriş yap. | No granted reward |
| `ATB-441` | `promo.error.qa_denied` | Bu test kodu bu hesap için yetkilendirilmemiş. | Admin-only dev tester lookup; no client override |

**Success** uses seven-language `AtlasBoardShopContentDraft.WorldText` strings, displays confirmed reward and server transaction ID, and refreshes the existing Store history. **No History** is a separate verified empty state; if the event backend was not reachable, the UI instead shows an `ATB-424` unverifiable-records state. Unsupported star/globe glyphs were removed from the World Journey event UI. All unverified event claim buttons stay disabled; do not report `OWNER PASS` until Unity + Firebase emulator tests pass.

This section does not repurpose legacy `ATB400` (lobby chat), `ATB401` (match chat), or `ATB410` (report submission).

---

## v5.49.12bz — Source diagnostics / outcome tracking (2026-11-03)


**ATB954** is now represented in Career inventory source: the registered REST account must verify the exact wallet UID, and a paid `Owned` item is not rejected solely because the catalog's `PreviewOnly` presentation flag is true. Source fix is **not** Unity runtime acceptance.


**ATB955** is now emitted in Development diagnostics by the single Server Status class when foreground connectivity checks cannot complete. Public UI shows localized Account, Online, Shop, Cloud Data and Career states, never infrastructure names or raw HTTP statuses. At most one check per 60s while the tab is open; Auth/room/career reads do not alter Gold.


**ATB956** remains an open *cross-device / signed-proof* issue. The UI reads only genuine device-local dice observations with the same UID and match ID, and marks partial results with `*`. It does not falsify an old missing roll sum/max. Current `careerFinalProofs` and `careerClaims` rules reject new fields via `$other: false`; formal server schema upgrade requires emulator rules validation and owner approval.


**ATB952** remains the existing code for failed authenticated emote load/save, UID/ETag/ownership validation. Development logs only include redacted UID suffix and response category; no raw email, password, token or URL.


---


## v5.49.12by — Emote / session consistency diagnostics


All text below is **developer documentation**, not material to display verbatim to players.
Customer-facing UI uses a translated, short message and the ATB code only.


| Code | Internal meaning | Status / severity |
|---|---|---|
| ATB951 | Emote selection rejected: zero entries, more than six, duplicates or unowned ID. | Wired in emote loadout service / recoverable |
| ATB952 | Emote load/save failed remote verification, transport or ETag authorization. | Wired in emote loadout service / recoverable |
| ATB953 | Missing/empty emote loadout could not yet be re-seeded with free defaults; no paid cosmetics granted. | Wired in emote loadout service / recoverable |
| ATB954 | Registered REST player was erroneously blocked by native-only Career inventory login check. | Tracked P0; NOT yet wired to UI |
| ATB955 | Customer-facing online status is stale, unverified or inconsistent with verified account state. | Tracked P1; NOT yet wired to UI |
| ATB956 | Roll count exists but sum/highest proof is unavailable; must not display invented averages. | Tracked P1; NOT yet wired to UI |
| ATB957 | Promo redemption requires an unavailable trusted service or non-issued code; do not pretend it succeeded. | Tracked P1; NOT yet wired to UI |


**Server Status UI policy:** Public labels must refer to **Hesap bağlantısı**, **Çevrimiçi oyun**, **Mağaza ve ödüller**, **Kariyer eşitleme**, and **Ağ bağlantısı**, not databases, hosts, ports or providers. For each probe, render one of: `Bağlı`, `Kontrol ediliyor`, `Giriş gerekli`, `Geçici olarak kullanılamıyor`, `Yavaş / yeniden deneniyor`. A green badge is allowed only after a real successful authenticated check. The current Settings implementation does not yet satisfy this requirement.


---


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


## Account creation and sign-in (private diagnostic mapping)


These internal meanings must NEVER be displayed alongside the player message.
The retail UI displays only localized short text plus one support code.
The development log may include a redacted HTTP status and provider category;
it must never include e-mail addresses, passwords, tokens or raw responses.


| Code | Internal meaning | Severity |
|---|---|---|
| ATB731 | Account request failed due to network, response parsing, or unexpected provider error. | Recoverable |
| ATB732 | Account creation email already linked to an existing identity (EMAIL_EXISTS). | Recoverable |
| ATB733 | Invalid credentials for an existing account. | Recoverable |
| ATB734 | Credential or existing authenticated UID was invalid or expired. | Blocking |
| ATB735 | Provider rejected account operation (OPERATION_NOT_ALLOWED); check account-link endpoint and email-enumeration protection. | Blocking |
| ATB736 | Authentication abuse/rate limit. | Recoverable |
| ATB737 | Backend password policy rejected credential. | Recoverable |
| ATB738 | Authentication succeeded but canonical user-profile RTDB sync remains pending. | Recoverable |
| ATB739 | Sign-in blocked locally because the password input is blank; no authentication request was sent. | Recoverable |
| ATB741 | Password reset dialog rejected an invalid email address before contacting Firebase. | Recoverable |
| ATB742 | REST password-reset request failed due to provider/transport/rate limiting; external account existence is not disclosed. | Recoverable |
| ATB743 | Registered REST account nickname update rejected by 24h server cooldown, validation or transport. | Recoverable |
| ATB744 | Registered REST sign-out could not securely establish and persist a new anonymous Firebase UID. Existing registered account remains. | Recoverable |


**Security contract:** When upgrading an anonymous Guest, send that Guest's
current `idToken` to `accounts:signUp` along with email and password. Verify
the response `localId` equals the original Firebase UID before updating the
profile, wallet or any local session. With email enumeration protection
enabled, `accounts:update` cannot be used for this provider-link operation.
See Google Cloud Identity Platform email enumeration protection documentation.


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




---


### v5.49.12ca — QA and economy diagnostics (2026-11-03)


| Code | Diagnostic meaning | Player-facing guidance / action |
|---|---|---|
| ATB958 | Existing, registered REST user's bearer token cannot be refreshed for the same UID. | Session or network verification failed. Retry sign-in without creating a new account. |
| ATB959 | Wallet GET or wallet transaction PUT returned HTTP 401/403 after a safe same-UID refresh attempt. | Cannot verify purchase/claim. Do not charge locally; use log stage `ATB_STORE_WALLET_READ` / `ATB_STORE_MUTATION`, never show Firebase details. |
| ATB961 | Owner QA session code was valid but no canonical Firebase UID could be resolved. | Sign in to intended owner test account, then retry. No privileged operations allowed. |
| ATB962 | Owner QA UID not on server-side `/devWalletTesters` allowlist. | An authorized Firebase project administrator must explicitly approve that exact UID. Client code must not self-approve. |
| ATB963 | QA wallet could not be verified while checking owner authorization. | Check network and authenticated wallet; never fall back to local admin privilege. |


No raw authentication credentials, full UID, token, API URL or provider implementation details should be rendered in player-facing alerts. Account and client errors must stay separated from server denials. All source-level diagnostics remain internal; these codes do not imply live test PASS.


---


### v5.49.12cc — Store daily-claim independence and cloud dice observations


- `ATB_STORE_MUTATION stage=wallet_put_failed kind=PURCHASE/DAILY/DEV_GRANT http=401`: Firebase RTDB security rule denied the canonical economy PUT. For regular purchases and DAILY this **does not imply missing developer rights**. Verify the deployed `database.rules.json` accepts existing wallets without `dailyReward`.
- `ATB_DAILY_LEGACY stage=read missing=true historical_receipt_found=true/false`: Old wallet has no daily timestamp; the read-only UI recovered the last claim from immutable history. Must not overwrite cash/inventory to repair this field.
- `ATB_DAILY_LEGACY stage=claim missing=true receipt_found=true/false`: A first claim or legacy claim used the latest historical DAILY receipt to enforce 24 hours on the client. Never grant twice when the receipt is recent.
- `ATB_DAILY_LEGACY history_unavailable=true`: Cannot verify whether an older daily award exists; the **DAILY action fails closed** rather than paying unknown Gold. Normal purchases MUST NOT be blocked by this condition.
- `ATB_CAREER_DICE_CLOUD stage=loaded stored_matches=... rolls=... reward_authority=false`: Account-scoped telemetry loaded. These records are not Gold/XP or achievement proof.
- `ATB_CAREER_DICE_CLOUD stage=observation_pending http=401/403`: Account/claim/archive/rules prevented insertion; display existing local observation with `*` until eligibility is available. This message is diagnostic-only.
- `ATB_CAREER_DICE_CLOUD stage=unavailable`: Network or Auth token error; account ownership cannot be replaced by device-local values in the backend, and gameplay remains available.


**Player-facing UI:** No terms such as RTDB, token, raw Firebase method, or developer identity for a normal purchase/day claim. Keep operator-facing cause in safe diagnostic logs only; never log Firebase bearer token/password.