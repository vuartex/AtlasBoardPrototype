# ATLAS BOARD — PHASE 17F: SPARK / SHARED CAREER / RTDB / MOBILE FPS

**Checkpoint:** v5.30.5 shared navigation/UI correction, 2026-10-15. Prior v5.30.4 architecture and test gates retained.  **Status: CANDIDATE; Unity compile, real device, RTDB Rules Emulator, multiplayer, trusted rewards and Steam verification NOT ACCEPTED.**

This is a new whole-file continuation record, cross-checked against all 1618 lines of `Assets/Project/Documentation/MASTER_ROADMAP.md` on GitHub, including architecture §§2/2A, Phase 3, Phase 10, Security Core 01–07, Phase 16, Phase 17C/17E, the QA acceptance ledger and the final gates. The GitHub roadmap was v5.30.0 (215229 characters), *older than the owner's local v5.30.2/v5.30.3 edits*. Do **not** replace the owner's possibly newer local `MASTER_ROADMAP.md` from old GitHub. The full latest local master must be provided for a safe in-place revision; this supplemental roadmap deliberately leaves that file untouched.

## 1. Immutable product/platform policy

| Feature/behavior | Unity Editor | Windows/Steam | Android/Google Play | iOS/tablets |
|---|---|---|---|---|
| Career palette: purple/indigo; Shop: orange | **COMMON** | **COMMON** | **COMMON** | **COMMON** |
| Career Overview / History / Achievements tabs and map/player identity | **COMMON** | **COMMON** | **COMMON** | **COMMON** |
| Backend data model and Firebase UID canonical identity | **COMMON** | **COMMON** | **COMMON** | **COMMON** |
| LocalOffline zero-backend / zero rewards / no cloud history | **COMMON** | **COMMON** | **COMMON** | **COMMON** |
| Auth options on non-Steam clients | Email Auth when chosen | Email only when non-Steam build/provider | Email Auth | Email Auth |
| Steamworks identity / automatic returning sign-in | Only when Steam test provider is *actually active* | **STEAM ONLY**, do not require email account creation | Never | Never |
| Touch UI, on-screen keyboard, safe-area, large buttons | No | No | **MOBILE ONLY** | **MOBILE ONLY** |
| Mobile runtime frame budget/profile | No | Never mutate desktop settings | **MOBILE ONLY** | **MOBILE ONLY** |
| Steam overlay, native Steam achievements | Provider test only | **STEAM ONLY** | No | No |

A standalone Windows build without the Steam provider is *not* automatically a Steam user. An Editor play session cannot claim a valid Steam identity unless the Steam adapter is actually initialized. A Steam persona name or local guest is **never** a Firebase UID and cannot be accepted as reward authority.

## 2. v5.30.4 staged code (do not mark PASS until testing)

1. `AtlasBoardUniversalCareerTheme.cs` is platform-neutral; recolors Career ribbon/card purple without altering Shop, renders a fixed-layout left-top Career HUD in the correct field order without wallet queries, places ACCOUNT inside navigation to the right of Settings Controls or Career Achievements on ALL platforms, stabilizes the profile name by removing conflicting legacy ` • Lv. N` suffix, moves Account X within the header on all platforms, and separates Forgot Password status from its button. When Steam provider is selected, Career uses Steam identity without forcing e-mail registration, even while provider initialization is pending. The Steam Account dialog is hosted above Settings/Career. Permanent Steam rewards remain backend-gated.
2. `AtlasBoardMobileAccountUX.cs` remains mobile-specific for password eyes, keyboard focus, touch input, Guest preview and Firebase password change. v5.30.5 removes its competing Account-tab positioning code: **all tab locations are now centrally owned by the common presentation helper.** Existing v5.30.2 sources are untouched.
3. `AtlasBoardRtdbCareerReadModel.cs` is cross-platform. A signed-in user's verified Career snapshot is read from RTDB `/careerProfiles/{uid}` when present; reads are infrequent. A missing/failed record displays an explicit **zero-value preview** and no fabricated XP. The v5.30.5 UI removes overlapping footnote text from Career stats; read errors still appear in sanitized logs. No XP or achievement write method exists.
4. `AtlasBoardMobilePerformanceProbe.cs` is mobile runtime-only and adds CPU/GPU timing evidence, texture/pixel-light budgets, and attempts dynamic render-buffer scaling. On a built-in/non-supporting render pipeline, a requested buffer scale **does not prove** a lower backbuffer resolution. FPS success remains UNVERIFIED until device measurements.
5. `Backend/Firebase/RTDB_RULES_PHASE17F_CANDIDATE.json` is a **complete proposal** based on the owner-supplied deployed RTDB rules (not old GitHub rules): the current rooms/users/catalogs branches are retained, and *optional* private `/careerProfiles`, `/guestProfiles`, `/casualStats` and `/users/$uid/settings` branches are added. This is a separate candidate filename: it does NOT overwrite the currently deployed canonical `database.rules.json`. It has **not been deployed**; do not redeploy until client schema and Rules Emulator negative tests are ready.

## 3. RTDB target data: authority classes, not blind Firestore removal

- **Firebase Authentication:** sole custodian for passwords, provider credentials, sessions, duplicate-email, reset and reauthentication. No plaintext, password-confirmation or refresh token in RTDB/Firestore/PlayerPrefs/logs.
- **RTDB `/users/$uid`:** private general user identity; immutable UID/Atlas playerId/createdAt, canonical display name and timestamps, language/country, device/platform inventory. Add owner-readable `settings` with bounded global and per-platform properties. Account status and membership tier must not become self-upgradable client authority. Existing RTDB user mirror tested on Android is retained until the replacement read path is accepted.
- **RTDB `/careerProfiles/$uid`:** private **server-owned projection** of total verified XP, level, overall stats, map stats, match-history, achievements. Clients are read-only. This is the owner's desired final storage location, NOT permission to mint rewards on a phone. A trusted verifier/proof writer must validate each rewarded match first; the prior SECURITY-CORE-01/03/04/17A finalization replay protections remain release gates.
- **RTDB `/casualStats/$uid`:** optional private client-owned unranked counts, clearly **unverified**, separate from the verified Career projection. It must not grant XP, gold, currency, leaderboard, permanent win, event rewards or achievements.
- **RTDB `/guestProfiles/$uid`:** optional short-lived private anonymous-auth cache with `Guest#####X`, createdAt and expiresAt; device offline play stays possible without network. Anonymous Auth is not implemented in the current account runtime. Do not write public/open Guest data and do not claim globally unique Guest names.
- **Firestore:** keep existing `users`, `public_profiles`, `preferences` while migrating. Do not delete live records until same-UID cross-device read parity, dual-session regressions, Rules security negative tests and safe migration/rollback are complete. Legacy valuable commerce/wallets, abuse/audit, progression backend and verified match proofs are NOT blindly migrated to a client-writable branch.
- **Preferences:** move general/per-platform low-value settings under `/users/$uid/settings` only after Unity settings sync is migrated and verified. Platform graphics presets MUST NOT cross-contaminate Steam/Windows and Android.

### Membership and account state

`membershipTier` and `accountStatus` are entitlement/authorization-related data even when displayed in the UI. Do not grant an owner-writable `premium` field with unchecked values. Candidate rules only permit client values `normal` and `active`; elevated future values are written by trusted Admin. If later entitlement decisions rely on them, use a server-owned authority projection instead of a client-owned mirror.

### Guest cleanup under the requested 3-second UX

The owner requests issuing guest deletion within **3 seconds of successful sign-in**. This can be a best-effort **command deadline**, NOT guaranteed database deletion if offline/network fails. Proper design: anonymous Firebase Auth session for a networked Guest; a stable anonymous UID; when *linking* anonymous to newly registered credentials (same UID), send delete immediately and retry safely; when switching to a different existing account, delete under anonymous credentials *before* switching auth or use a trusted server/expiry cleanup. Once a different account signs in, its token must NOT be allowed to erase another UID's guest path. Current v5.30.4 has the **rules proposal only**, not end-to-end Guest upload/deletion; no false completion claim. Offline Guests remain local and need no RTDB write.

## 4. Private/Public Room HTTP 404 root cause and free-first rollout

Physical Android logs 2026-10-15: `lobbyListPublicRooms` and `lobbyCreatePrivateRoom` call `https://europe-west1-atlasboard-usa.cloudfunctions.net/...` and return **HTTP 404** because the relevant Functions are not deployed on Spark. More retries, App Check flags, longer timeouts or Firestore Rules redeployment **do not create those endpoints**. There is no legitimate client-side one-line fix.

**Phase 17F.1: Private Casual RTDB transport (OPEN, P0):** Retrofit `AtlasBoardLobbyRuntimeBridge`/private-room UI and `AtlasBoardMatchRuntimeBridge` to use native/event-driven RTDB `joinCodes`, `roomMeta`, `joinRequests`, `roomMembers`, `presence`, `intents`, `intentAcks`, `roomState`. Implement secure host/seat assignment, match sequence/revision monotonicity, idempotent client commands, reconnect/leave/AFK and no multiplayer rewards. Existing RTDB rules require focused reviews before direct host/member listing because `joinRequests/$roomId` currently has no host-parent `.read`; host cannot enumerate pending joins. Send explicit test evidence from **two actual Android devices** before PASS.

**Phase 17F.2: Public Casual discovery (OPEN):** A bounded safe RTDB public room index needs host publishing, expiry, auth, query limits, abuse budget and secure join validation; current `publicRoomDirectory` is readable but intentionally client-write denied. Never flip it to public unrestricted write or announce public browser as working because UI can render.

**Phase 17F.3: Cloud/Rewarded (OPEN):** Verified online progression, rewarded achievements, wallet/gold, economy and anti-cheat are separate protected workflows. On Spark there is no deployed trusted Cloud Functions verifier. A separate user-approved trusted server / eventual paid service and secure replay verifier are required. Do not grant competitive XP from client-supplied RTDB snapshots.

**Integration prerequisite:** GitHub `main` is older than the owner-local v5.30.2/v5.30.3. Before replacing lobby/match/main menu/settings/account services, ingest the latest **owner-local complete source files**. Overwriting current Unity files with old GitHub bodies would regress prior private-seat/UI/security changes. Do not ship an old baseline to claim 404 fixed.

## 5. Android performance investigation (OPEN, P0)

- Samsung SM-G780G @2400x1080: stable gameplay ~18–22 FPS; 16–23 FPS previously. 4x->2x->0x MSAA made no meaningful difference; shadows already disabled, VSync=0, targetFrameRate=60. `renderScale=1.00` persisted despite a requested 0.75/0.62. This suggests render pipeline/DRS path may not actually downscale; GPU vs main-thread bottleneck is **not proven**.
- New probe reports frame timing (`cpuMs`, `gpuMs`, if supported), reported dynamic-buffer factors and frame memory; camera HDR/MSAA off, limited mobile anisotropy, pixel lights, mipmaps and LOD, no persistent graphics saves. Never use `Screen.SetResolution` to sidestep the source bug or change desktop quality.
- Acceptance: capture warm menu and active match for >=60s in Development **and** Release, check device thermal throttling and CPU/GPU profiler timeline, then optimize actual largest render/input/UI hot path; target stable >=30 FPS minimum before wider QA, preferably stable 45–60 if device allows. If CPU/GPU times unavailable, Unity Profiler on device required. Do not change dice/turn animation duration to fake a performance improvement.

## 6. UI and regression matrix

- All platforms: purple Career / orange Shop; Account X completely within the blue header; Forgot Password description visually separated; Career Overview shows a mirrored existing player HUD (name, wallet labels), level/XP or explicit 0/unavailable, stats and map panel instead of unbounded Loading. HUD mirroring is presentation-only and does not fetch or grant wallet currency. History/Achievements remain visible without pretending rewards exist.
- Steam-ready desktop: no forced email signup; Steam persona remains provider identity. Editor without Steam provider: normal email login remains available. No spoofing SteamID by persona name.
- Android/iOS: real password show/hide buttons, empty initial password inputs, status separated from math captcha, keyboard focus and Next controls, change password through Firebase reauth and keyboard focus. **Account tab is now common to all platforms** beside Settings Controls / Career Achievements; it is not a mobile layout exception. Guest preview is available where the provider allows it.
- Private Offline: 4-seat example Local/Local/Bot/unset -> final seat Bot; visible 5/4/3/2/1; no remote cloud writes or XP. Validate first, second and third matches, back to lobby, Shop/Career/Settings round trips and scene rebind.
- Online Private Casual: two actual Android clients join code, host start, UI match commands, leave/rejoin, one-sided network disruption, no credentials in RTDB logs. *Not ready to build/test in this package.*
- Firebase: Email account registration, logout/login session persistence, password change and duplicate account checks; `users` private mirror; do not insert passwords into logs or database exports.

## 7. Owner test/acceptance order and packaging discipline

1. Extract complete source files in this ZIP into the Unity project root; keep latest v5.30.2/v5.30.3. **No GitHub push or Firebase deploy during this step.**
2. Unity 6000.5.6f1 Editor: first ensure 0 red C# compile errors; if errors, request direct replacement **complete file ZIP** based on exact owner Console line. No APK yet.
3. Retest Editor purple Career and Account X/description without building APK. Editor is not allowed to act as a genuine Steam user unless Steamworks adapter is initialized.
4. Only after RTDB offline/casual bridge is integrated and Editor compiled, take a **single consolidated 30-minute Android APK** to cover touch/UI, auth, Career, offline seats/3 matches, FPS logs and two-phone private casual transport.
5. Run Rules Emulator abuse tests: cross-UID denied; client XP/achievement write denied; membership escalation denied; anonymous guest cannot delete another guest; no public unauthorized room mutations; room revision replays denied. Run actual Firebase writes only after rule acceptance and owner-approved deploy.
6. Do not perform Cloud Functions deployment, enable Blaze, change Firebase production App Check enforcement, delete Firestore collections, clear player stats or mutate the owner's Steam/Windows settings without explicit approval.

### Carry-forward acceptance status

| Workstream | Current state |
|---|---|
| Email registration and session restore on Android | **Owner device evidence 2026-10-15: observed working**; duplicate/UI edge cases open |
| RTDB private `/users/$uid` mirror | **Owner device evidence: write success**; settings migration open |
| Common Career purple + Steam routing | **v5.30.4 candidate; Unity compile + Steam tests pending** |
| Android Account UX v5.30.3 + spacing v5.30.4 | **Candidate; real device retest pending** |
| RTDB Career read + zero fallback | **v5.30.4 candidate; server-owned writer absent** |
| RTDB Career verified XP/history/achievements | **NOT IMPLEMENTED; requires trusted verifier** |
| Guest RTDB anonymous write + 3-second cleanup | **NOT IMPLEMENTED; auth migration and cleanup needed** |
| Private RTDB multiplayer | **NOT IMPLEMENTED; Cloud Functions 404 remains** |
| Public RTDB room directory | **NOT IMPLEMENTED; directory currently readonly** |
| S20 FE FPS fix | **NOT ACCEPTED; new diagnostic/budget code is candidate only** |
| v5.30.2 CS0029 + source warning fixes | **Source fix staged; user Android runtime screenshots imply APK built; exact compiler warnings require a fresh Console log** |
| Google Play production AAB / Steam release | **BLOCKED** |

---

## 8. PHASE 17F.4 — Canonical feature ownership / UI bug acceptance (v5.30.5; OPEN)

This section **supersedes conflicting UI/platform wording in Sections 1, 2 and 6 above** without erasing the history of v5.30.4. User-provided Unity Editor captures on 2026-10-15 showed: profile `Guest81477Z` alternating with `Guest81477Z • Lv. 1`; Career HUD rendered tiny at upper-right with text order reversed; both Steam/RTDB explanatory captions drawn over the bottom Career statistics; ACCOUNT/SIGN IN floating at bottom-left of Settings and Career, with SIGN IN appearing briefly even though Steam provider was active; Settings ACCOUNT opened a Steam account notice on a different underlying UI layer; first Career click could bypass the normal sign-in modal on Steam. This was a real source-scope error, **not** a supposed mobile-only bug.

### A. Feature placement is a product decision, platform presentation is an adaptation

| Scope | Feature / requirement | Owner and acceptance |
|---|---|---|
| **GLOBAL — all platforms** | Career purple; Shop orange; stable player name without oscillating `Lv.` suffix | `AtlasBoardUniversalCareerTheme`; validate main menu, lobby, Career and second/third match |
| **GLOBAL — all platforms** | Career HUD at upper-LEFT aligned with other pages (same name/avatar/Gold/Atlas Coin order and size) | Common Career HUD; no second wallet query or fabricated coins |
| **GLOBAL — all platforms** | Account as a fifth Settings navigation tab **right of Controls**, and fourth Career tab **right of Achievements**. Never a floating lower-left launcher. The navigation item must always be called **ACCOUNT**, not transient SIGN IN | One common positioning owner; no Android-only duplicate |
| **GLOBAL — all platforms** | Account modal close X inside blue header; Forgot Password help below action with margin | Shared account window layout; mobile keyboard may additionally shift active input |
| **GLOBAL — all platforms** | Career stats, history, achievements and map selection visible even if account backend has no profile; zero state is honest | RTDB read model; no explanatory footnote overlaying stat cards |
| **GLOBAL — all platforms** | Room code shown in an explicit **ROOMS** section/tab of lobby, with Show/Copy/Join, room lifecycle, invite expiry and display metadata | **PENDING** latest local lobby source and RTDB bridge integration; must not be implemented solely on Android |
| **GLOBAL — all platforms** | Invite/room codes stored in RTDB (`/joinCodes/$code` -> `/roomMeta/$roomId`) rather than new Firestore documents | RTDB rules/protocol with Auth/host authorization, 6-digit code collision/expiry controls. Existing rules already define the RTDB branches; real gameplay bridge not implemented |
| **PROVIDER-SPECIFIC** | Steamworks automatic sign-in/recovery/link with actual validated Steam identity; never force email registration when Steam provider selected | `AtlasBoardPlatformRuntime` + Steam link bridge, trusted service remains required. Standalone Windows without Steam provider may use Email Auth |
| **PROVIDER-SPECIFIC** | Non-Steam Firebase Email/Password registration, password change, forgot password, session restore | Firebase Auth; do not store plaintext password |
| **MOBILE ONLY** | Touch/safe-area, virtual keyboard and focus indicator, show/hide password, larger tap targets | `AtlasBoardMobileAccountUX` and existing mobile platform guards |
| **MOBILE ONLY** | FPS budget/texture/AA/render profile | `AtlasBoardMobilePerformanceProbe` and `AtlasBoardMobileGameplayUX`; never modify desktop/Steam graphics settings |
| **SERVER-AUTHORITY (ALL PLATFORMS)** | Verified persistent XP, win statistics, achievements, wallet, entitlements | Firestore/RTDB physical storage can vary, **authorization cannot**: never client-write these. Spark lacks trusted production verifier |
| **LOCALOFFLINE (ALL PLATFORMS)** | Modern Local/Bot seats and real 5->1 countdown; no gameplay backend, XP or rewards | No Cloud Functions, Firestore gameplay or RTDB gameplay in this mode |

### B. Why previous code regressed and the v5.30.5 repair

1. Previous mobile helper owned tab positioning exclusively, so Windows Editor had no positioning despite the shared Account button being shown. The shared helper now positions four/five tabs on every platform and the mobile helper **does not reposition** it. The click destination is still selected by provider, not inferred from visible label.
2. Previous Career HUD was a 0.75x clone of the menu prefab at positive right anchor, and inherited child RectTransform behavior. The shared source now constructs a left-top HUD with explicit field order, no gameplay code mutation; texts copy existing canonical player header.
3. The existing progression/UI presentation owners disagree about appending ` • Lv. N` to `ProfileName`. Common LateUpdate normalizes visible header; actual XP/level stays in Career statistics. Deeper canonical fix in original UI controller remains optional once local full sources are ingested.
4. Steam/RTDB explanatory notes were placed near the bottom of `ContentPanel` over stat rows. These renderers are removed; backend state remains observable via sanitized log entries and the honest zero-value Career view.
5. Steam Account dialog formerly parented to the progression canvas regardless of active Settings. It is now placed on the shared account canvas above the active context. Selecting a Steam provider that has not initialized still must never trigger unrelated email signup.
6. No changes in this package to Remote/Local seats, lobby APIs, RTDB membership, on-device authentication persistence, PlayerPrefs graphics or Firebase rules.

### C. Verification gates (do not mark PASS before owner confirms)

- Editor with Steam adapter selected: purple Career, main menu and lobby profile name stable for 30 seconds; Career HUD fully legible at left; zero overlapping status/footer text; both ACCOUNT tabs appear on same row as native tabs, open the Steam provider notice **above** the active page, and require no email registration.
- Editor without Steam adapter: ACCOUNT tab remains in navigation, opens Email Auth when pressed; Career prompts for sign-in or offers explicitly unrewarded Guest preview if supported. The UI must not falsely claim a Steam account.
- Android later (single consolidated APK): same navigation placement plus mobile password eye/focus behavior, safe-area; selected seat Local/Bot policy; first through third match; FPS GPU/CPU warm measurement.
- RTDB Room tab, RTDB Private/Public room game transport, Guest RTDB upload/cleanup, and permanent XP/achievement server write stay **OPEN**, not inherited PASS from this UI package. They require latest local source files, secure protocol design and a real two-device run.
- The GitHub 1618-line `MASTER_ROADMAP.md` was read, but **the owner's newer local master may not match**. The deliverable updates this whole Phase 17F continuation file, **does not overwrite** unknown newer local `MASTER_ROADMAP.md`. Once provided, merge this scope table into its canonical platform-feature register without discarding any unrelated roadmap milestone.

---

## 9. PHASE 17F.5 — v5.30.6 Editor regression recovery (2026-10-15, SOURCE CANDIDATE; NOT ACCEPTED)

**Owner's repeat Editor evidence:** Career/Settings tab positions improved in v5.30.5 but the old `Button_AccountContext` briefly rendered SIGN IN in the lower left while the common helper moved it every ~0.4 seconds; Account had a different accent/image from the other tabs; ProfileName continued oscillating between `Guest#####X` and `Guest#####X • Lv. 1`; Career HUD touched the Navigation banner; LocalOffline PAUSED showed neither Room Code nor Ping/Jitter/Loss. Existing Private Table **LocalOffline** starts and plays on Editor; this is not a verified cloud Private/Public room acceptance. The user explicitly requested no changes to unrelated systems.

### Common product acceptance — every target platform (Editor, Windows/Steam, Android/iOS)

1. **One native-looking Account tab per settings/career navigation row.** Source `AtlasBoardAccountAccessUI` still owns login/modals, but its old bottom-left `Button_AccountContext` is now visually hidden behind `CanvasGroup(alpha=0, no raycast)`; it must never flicker as Sign In anywhere. Settings Account uses a true clone of native `Tab_Controls` under the same `SettingsWindow` parent and shares exact sprite/size/inactive color. Career Account uses a clone of its native page button positioned inside the common Navigation banner at the same vertical baseline and blue inactive color. Unlike previous v5.30.5 placement, the persistent Career Account control survives the legacy `RebuildNavigation()` cycle. The button label remains `ACCOUNT` (localized) regardless of authentication state; clicking selects appropriate Steam or Firebase provider behavior.
2. **No profile-name level suffix in visible HUD.** The root source conflict is known: legacy `AtlasBoardProgressionUI.SyncSharedProfileLevel()` mutates the canonical `ProfileName` approximately once per second while the identity owner restores it. v5.30.6 adds `AtlasBoardStableProfileNameView` on original ProfileName TMP UI nodes. Its child UI graphic renders the normalized original name continuously and hides only the legacy source's drawing (not its data). Career level stays in Career progression. This is a presentation mitigation, **not** a rewrite of the original `SyncSharedProfileLevel()` method. A future clean canonical source change requires the owner's latest local ProgressionUI file, not a stale GitHub replacement.
3. **Career HUD no overlay.** Common upper-left HUD reference width reduced from 455 to 365 and height from 155 to 122 with matching avatar/text sizing. Other pages' HUD appearance/data sources are not modified, and there is no new wallet query. Its right edge must not touch the left edge of the 1120-wide four-tab Career banner at 1920 reference resolution. Stress across 16:9 and 20:9 safe-area layouts remains open.
4. **Restore meaningful pause information without inventing network numbers.** Original `AtlasBoardLeaveFlowController` intentionally hides RoomCodeSection absent a genuine six-digit online invite code. Original `AtlasBoardNetworkQualityHud` intentionally hides its metrics unless the network match bridge has a CurrentMatchId. v5.30.6 adds common `AtlasBoardSharedPauseDiagnostics` to expose `LOCAL / NO ROOM CODE` with code em dash on offline pause and `NO NETWORK SESSION` with Ping/Jitter/Loss dashes; existing online Show/Copy and telemetry HUD remain the owners when a valid room and real network match exist. This deliberately does not create a fake invite code, fake ping, gameplay RTDB writes, Cloud Functions or rewards. Verify no overlap with Resume/ESC at each supported aspect ratio.
5. **Steam is provider-specific, not a different UI layout.** `ProgressionCard` gains a transparent Steam-provider click guard so competing legacy Firebase button rebinding cannot momentarily open Email Sign In. It activates only with the actual Steam provider selected. Steam account remains a provider notice; verified automatic online sign-in still needs the trusted service. Standalone non-Steam client routes to Firebase Auth.
6. **Scene and settings integrity.** These runtime helpers re-resolve destroyed scene references and do not touch mobile render profiles, keyboard flow, gameplay rules, lobby transport, PlayerPrefs or live Firebase Rules; `AtlasBoardMobileAccountUX` v5.30.3 remains intact. Never let two runtime owners recolor or reposition the same tab independently.

### Verification and status ledger

- [ ] Editor compile 0 errors and note any source warnings; **no APK required** for this iteration.
- [ ] 30 seconds at Main Menu, Career, Lobby, return to Menu: no visible `Lv.` suffix, name flicker, white placeholder or double ProfileName text.
- [ ] Settings open/close and Career page switch 10 times rapidly: only native Account tab, no lower-left Sign In flash; native inactive/selected color conventions; correct provider opens.
- [ ] Career HUD at upper left does not collide with Navigation on 1920x1080, 16:9, 20:9, scene reload/second match.
- [ ] Start Local Offline match (including explicit Local/Bot seat selection) and pause: explain unavailable Room Code and Ping/Jitter/Loss without numbers. For genuine online match, preserve real six-digit masked Show/Copy and actual telemetry; do not report network PASS without deployed RTDB transport and real device evidence.
- [ ] Samsung S20 FE Android later: no FPS regression attributable to per-frame global object scanning, test same UI layout in single consolidated APK.
- [ ] Private/Public RTDB transport, public discovery, trustworthy XP and achievements, Guest RTDB deletion, and full 2-device tests remain **OPEN**. v5.30.6 is a UI recovery, not backend completion.

**Packaging:** ZIP contains only complete `Assets/Project/Scripts/Platform/AtlasBoardUniversalCareerTheme.cs`, new `AtlasBoardStableProfileNameView.cs`, new `AtlasBoardSharedPauseDiagnostics.cs`, and this entire updated Phase17F roadmap. GitHub's older canonical `MASTER_ROADMAP.md`, `AtlasBoardProgressionUI.cs`, `AtlasBoardLeaveFlowController.cs`, network telemetry and the owner's existing project sources are NOT overwritten. No Rules deploy, Blaze activation or push.
