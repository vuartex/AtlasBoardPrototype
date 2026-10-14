# AtlasBoard Phase 17B.2 — Android physical-device QA v5.25.3 compile hotfix

## Source evidence, not assumptions

The owner tested Android Development APK on Galaxy S20 FE, 2048x921 landscape screenshots. UI initially cropped and a later Settings > Mute All Audio > Apply action changed the resolution and made the Settings button inaccessible. Review of `AtlasBoardSettingsV2Controller.ApplyAndClose` and `ApplyGraphics` shows that saving any Settings tab also applies saved desktop graphics and calls `Screen.SetResolution` outside Unity Editor. `ATB110` denotes failed online session preparation, `ATB202` public discovery failure, `ATB103` 30-second startup delay. Real internet/Firebase cause must be established from sanitized device logs. A production Functions deployment has NOT been verified.

## Full sources in this ZIP

- `Assets/Project/Scripts/Platform/AtlasBoardMobilePlatformQA.cs`: **removes** QA-only onscreen Back button; retains Android 13+ native Back callback and mobile landscape lock; replaces removed `FindObjectsSortMode` overloads.
- `Assets/Project/Scripts/Platform/AtlasBoardMobilePresentationGuard.cs`: new mobile-only runtime; makes screen-space CanvasScaler reference fit height and mobile safe-area inset with breathing room; guards against portrait/wrong screen dimensions from desktop settings, disables mobile resolution/display-mode dropdown controls; keeps mobile pixel-light budget to one. The underlying desktop UI layout and Player Settings are NOT modified.
- `Assets/Project/Scripts/Platform/AtlasBoardMobileErrorPresentation.cs`: new mobile-only UI policy. While a blocking startup overlay already displays the code, hide the identical redundant toast; throttle the same code's duplicate toast for 75 seconds. Preserve underlying error logs, status, and Retry. Never mask a distinct failure or claim backend success.
- `Assets/Project/Scripts/Platform/AtlasBoardAndroidDeviceDiagnostics.cs`: update existing bounded, sanitized Android Development session logs and add a small **Share Logs** button. This opens Android's built-in sharing sheet; no USB or file manager access required. Android/data storage is sandboxed, so Android 11+ file managers may not expose it directly; use the built-in log sharing UI. Shares newest ~48K chars, redacts credentials and identifiers. Native crash before Unity managed code may still require logcat.
- `Assets/Project/Editor/AtlasBoardAndroidSmokeQA.cs`: successful new Development APK automatically removes older known `AtlasBoard-Android-Development-*.apk` from `Builds/QA/Android` and associated symbol zips. Cleanup happens ONLY AFTER a good new APK, never deletes unknown APK/AAB or previously working build on failure.
- `Assets/Project/Documentation/MASTER_ROADMAP.md`: latest full roadmap, new test gates and one-project Firebase handoff.

## Owner Windows / Unity checks

1. Extract ZIP into repository root; overwrite only the named complete files. Wait for Unity compile. No installer or patch.
2. Unity Android target, `Atlas Board > QA & Diagnostics > QA > Android > 1 - Validate Android Readiness`, then `2 - Build Development APK`.
3. Look for `ATLASBOARD_ANDROID_BUILD_GATE APK_BUILD_PASS ... apkFileBytes=...`, `APK_OLD_BUILD_CLEANUP`, and zero Console red compile/build errors.
4. Install fresh APK over old version on Galaxy S20 FE. Test startup landscape, main menu safe top/left/right margins, Store, Career, private/public lobby, settings tabs, mute + Apply, return to Settings, pressing Apply again, Android hardware/system Back. **The old onscreen `< Back` button must be gone.**
5. At bottom-left find small QA **Share Logs** button; tap it, share through email or messaging to yourself, and send the text to the developer. Include `ATB_MOBILE_`, `ATB_ANDROID_`, `ATB_BOOTSTRAP`, HTTP/Auth/Functions failures. Logs should not contain tokens/passwords; check redaction before forwarding.
6. On-device test at 16:9, 18:9, 20:9 and tablet form factors remains pending. Runtime Canvas adaptation is a candidate, not certified until screenshots/interactive QA verify no cut-offs.

## Warning triage

- Obsolete `FindObjectsSortMode` references in the v5.25.1 mobile script are removed in the full source.
- Obsolete `TMP_Text.enableWordWrapping` warning comes from `Assets/Project/Scripts/UI/Loading/AtlasBoardLoadingExperience.cs`. **Not modified in this ZIP**; remains an explicit known compiler-warning task, not mislabeled fixed.
- `useLocalEmulatorsInEditor` CS0414 is a field assigned in Android conditional compilation; remains as a pre-existing nonfatal warning.
- Dynamic Batching deprecated: project Player/Quality setting needs separately verified migration; do not claim its deprecation is resolved by a mobile runtime pixel-light cap.
- Diagnostics Data/Debug Symbols warning: symbol setting must be validated in Unity Android Build Profile; not artificially suppressed.
- TextMeshPro C++ large-translation-unit informational output does not indicate C# compiler failure.
- APK ~230MB is still a performance and download-size optimization gate, not addressed by deleting old local APKs.

## One Firebase project, no manually started emulators for remote play

Owner prefers just existing `atlasboard-usa`, without a second staging project. This is possible by using **deployed, managed Firebase Authentication + Firestore + Cloud Functions** under one project, with a controlled non-production data namespace/tests. Live endpoint preflight, user authentication, App Check/Play Integrity, rules, backend release safety, cost budget, deployed negative security tests and Android Firebase SDK configuration must be closed BEFORE considering server connectivity accepted. The Android client already selects Cloud Functions URL when not in explicit emulator mode; deploying or validating public endpoints is NOT a substitute for configuring Android Firebase Auth/App Check. **This ZIP does not deploy Firebase, change rules, mint users, or enable permanent XP.**

CRITICAL: Firebase Functions export surface includes emulator-only test callables (e.g. economy test mutations). The source `requireEmulatedFirestore` rejects these outside emulator, but production exposure and App Check need review before deploy. Local Functions emulator accepts some invalid-present App Check tokens; cryptographic enforcement needs real deployed tests. Server-owned canonical match-replay verifier remains missing and permanent progression MUST remain fail-closed.

No real production services, security policy, app identifiers or GitHub branches are mutated by this ZIP. Secure Firebase access requires owner-managed plugin authorization or owner Firebase CLI login and an explicit production-safe deployment plan.

## Pass/fail

Offline source review and ZIP integrity are author checks only. Unity compile, APK build, physical device fit, audio Apply, log sharing and hosted authenticated multiplayer all **PENDING**. Keep QA errors visible in logs even if redundant notifications are throttled.


## v5.25.3 — Unity 6000.5.6f1 Android compiler hotfix (2026-10-13)

**Evidence:** The owner attempted to build v5.25.2 and got `Assets/Project/Scripts/Platform/AtlasBoardMobilePresentationGuard.cs(121,40): error CS0619: Object.GetInstanceID() is obsolete: GetInstanceID is deprecated. Use GetEntityId instead`. Unity then emitted `Error building Player because scripts had compiler errors`, `ATLASBOARD_ANDROID_BUILD_GATE APK_FAIL result=Failed errors=2`, and correctly restored Player Settings. No new v5.25.2 APK was produced in that run.

**Fix:** The COMPLETE `AtlasBoardMobilePresentationGuard.cs` source replaces `HashSet<int>` IDs with `HashSet<CanvasScaler>` and `configuredCanvases.Add(scaler.GetInstanceID())` with `configuredCanvases.Add(scaler)`. This preserves one-time CanvasScaler logging per object and the existing mobile-safe layout and native-viewport controls, without calling deprecated Unity instance-ID APIs. Other source files are carried over byte-for-byte from v5.25.2; none of the user-tested v5.25.2 features was deliberately removed. The fix has no effect on standalone Windows/Steam logic.

**Build validation:** Offline full-file ZIP integrity and source invariant checks PASS. A real Unity Editor Android compile/build cannot be executed in the packaging environment. **UNITY_ANDROID_BUILD_GATE v5.25.3 PENDING** until the owner runs Unity. If another compiler error appears, report exact file, line, and C# error; do not mask it by removing Android code.

**Instructions:** Extract ZIP over `C:\Users\crazy\AtlasBoardPrototype` (overwrite files). In Unity 6000.5.6f1, Android active build target, select `Atlas Board > QA & Diagnostics > QA > Android > 2 - Build Development APK`. PASS must show `ATLASBOARD_ANDROID_BUILD_GATE APK_BUILD_PASS` and `APK_OLD_BUILD_CLEANUP`. Do not install/test previous failed v5.25.2 as though it were fresh; the old APK on disk is not newly built.

**Still open, nonfatal compiler warnings:** `TMP_Text.enableWordWrapping` (CS0618) in LoadingExperience and `useLocalEmulatorsInEditor` (CS0414) in LobbyRuntimeBridge. The Performance Test framework's JSON asset generation and Debug Symbols guidance are independent of CS0619. Do not confuse generated performance-test JSON with a Player build failure. Do not treat Windows emulators as real Android connectivity or call Firebase production deployed without validation.

**Firebase:** Re-requested Firebase connector authorization by owner; until confirmed there is no verified deploy, no live Auth/Functions access, no Android cloud integration PASS. Owner wants a single `atlasboard-usa` project: respect this, but continue production security gates and do not blindly deploy.
