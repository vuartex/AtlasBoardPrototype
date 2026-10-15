# AtlasBoard next-chat handoff — v5.30.1

Use the user's actual local project as source of truth. GitHub may lag because the owner intentionally batches pushes after testing. Do not overwrite newer local files with older GitHub versions.

## Delivery contract
- Turkish, concise.
- ZIPs contain complete replacement files in canonical project paths only.
- NO patch/yama/diff/APPLY installer scripts.
- Do not auto-push GitHub or auto-deploy Firebase.
- A failing log => diagnose and proactively return corrected full-file ZIP; do not ask approval.
- A 30-minute APK build is expensive. First require Unity Editor compile with ZERO red errors. Request APK only when Android/device-specific runtime behavior actually needs physical validation.

## Current immediate state
v5.30.0 integrated package was applied but could not be accepted because Unity 6000.5.6f1 reported:
`AtlasBoardMobileGameplayUX.cs(168,15) CS0619 SceneHandle implicit operator int is obsolete`.
v5.30.1 changes that to `active.handle.GetRawData()` and configures Android smoke builds to use SymbolTable debug symbols temporarily/restorably.
After applying v5.30.1: first wait for Unity compile. Do NOT immediately make another APK if Console still has a red source error.

Still-open nonblocking source warnings to fix next using full canonical files, not suppression:
1. `AtlasBoardLoadingExperience.cs(1242)` obsolete `enableWordWrapping` -> `textWrappingMode`.
2. `AtlasBoardLobbyRuntimeBridge.cs(35)` Android `CS0414 useLocalEmulatorsInEditor`; keep the value Editor-only while preserving `-atlasLocalEmulators` development-build semantics.

## One-build Android acceptance list after compile is clean
Test ALL of these in a single APK cycle:
1. Main Menu: Shop and Career are visually distinct and not eye-fatiguing.
2. Private Table uses modern `+ -> Local Player / Bot`; no legacy Human/Bot dropdown screen.
3. Player Count=4 with unresolved slots: unresolved slots auto-fill as Bots, not Local Humans. Explicit Local Players/Bots remain exactly as selected.
4. Start shows visible 5,4,3,2,1 countdown; no instant match start.
5. Roll Dice button remains beside lower-left HUD; Trade button no longer overlaps lower-right HUD.
6. Trade/tablet text/dropdowns/touch targets are comfortably readable on phone.
7. Touch camera: one-finger rotate, two-finger pinch zoom, two-finger pan; UI interaction must not move board camera.
8. City Info supports touch vertical scrolling.
9. Play a match, leave to menu, open Shop/Career/Settings, start a second and third match: modern tablet/panels/City Info must remain identical. No legacy UI route may reappear.
10. FPS: record menu and gameplay FPS; previous S20 FE evidence was ~16–23 FPS even after AA fell 4x->2x. Target materially better/stable performance without changing Windows/Steam settings.
11. Guest flow: no-login local play works; persistent name uses `Guest#####X`; restart keeps guest identity.
12. Career unsigned: must not remain indefinitely on `Loading career data...`; show account/sign-in path.
13. Settings unsigned: account/sign-in entry available.
14. Account popup: login, register, password repeat, easy +/− challenge, one-submit loading lock, duplicate-email error, forgot-password reset.
15. Successful Firebase Auth sign-in persists across app restart; user should not re-enter password every launch.
16. Signed-in canonical display name is the same everywhere (menu/lobby/match/career/store). Rename is max once/24h and must ultimately be server-enforced.
17. Share Logs and send log after the consolidated run.

## Firebase / data architecture
- project `atlasboard-usa`; Android package `com.atlasboardgame.atlasboard`.
- Spark/free-first. No Blaze/payment without explicit owner permission.
- Email/Password Auth enabled. Play Integrity App Check registered; enforcement proof still pending.
- Firestore and RTDB rules were already deployed by the owner; do not redeploy just because this is a new chat.
- RTDB region: Belgium / europe-west1.
- RTDB target user tree: `/users/{uid}` with stable userId/playerId/email/displayName/general data and `/platforms/{platform}/devices/{deviceId}` for multiple devices/platforms.
- Never store password/password confirmation in RTDB/Firestore/PlayerPrefs/logs.
- RTDB: general/cached/live/ephemeral data, presence, future private-casual state/intents, public low-value catalog/promo metadata.
- Trusted/authoritative path: XP, wallet/currency, purchases/entitlements, permanent progression, valuable promo redemption, verification/reward finalization.
- arbitrary Firestore composite indexes are not needed unless a real query demands them; RTDB `.indexOn` rules already deployed.

## Session policy
- `LocalOffline`: local humans/bots, zero gameplay backend traffic, internet not required, no permanent XP/win/achievement/wallet/rewarded history.
- `PrivateCasualOnline`: target host-authoritative RTDB live transport, no permanent ranked/rewarded progression.
- `OnlineRewarded`: RTDB live state/presence/intents + trusted checks for secure economy/progression/finalization.
- Bot simulation and microsteps stay on host; send only meaningful state revisions.

## Explicit unfinished work
- RTDB Private Casual TWO REAL DEVICE multiplayer transport is NOT implemented.
- Event-driven RTDB replacement for legacy 0.25s/0.5s polling is NOT implemented.
- Production Online Rewarded server-authoritative verification is NOT complete.
- No Functions deployed to cloud; owner does not want paid Blaze now.
- Play App Check enforcement proof, Google Play release AAB/device matrix, real Steam production trust and final release audit remain pending.
