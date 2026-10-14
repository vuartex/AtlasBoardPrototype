# AtlasBoard — Phase 17B.1 / v5.25.1 Android Device QA

**Status:** Source package prepared; Unity Editor compilation, rebuilt APK and physical-device regression **NOT YET VERIFIED**. The previous v5.25.0 Editor preflight and APK build passed. No remote Firebase deployment has been performed.

## Why this package exists

Owner's Galaxy S20 FE test: APK launches quickly and menus/career taps work, but the app appears portrait/cropped; Android system Back does not return as expected; ATB110/ATB103 appear because online identity/session preparation is unavailable; physical-device diagnostics are needed. The 38-minute Gradle/IL2CPP first build is not evidence of a failure. Unity's `report.summary.totalSize` may differ from the size of the actual APK on disk; the updated build log prints **both**.

## Changed full files

1. `Assets/Project/Editor/AtlasBoardAndroidSmokeQA.cs`: Android QA build temporarily sets `PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft`, then **restores** the previous value. Prints both `reportTotalBytes` and `apkFileBytes` and preserves SampleScene exclusion and Android Development-only HTTP setting.
2. `Assets/Project/Scripts/Platform/AtlasBoardMobilePlatformQA.cs`: on Android/iOS only, locks runtime orientation to **LandscapeLeft** (not autorotation). On Android API 33+, tries native `OnBackInvokedCallback` registration on Android UI thread. Routes callbacks to EXISTING public/private lobby, modal, leave confirmation and pause/resume functions; never force-quits, directly deletes rooms or reloads a live match. On Android Development builds, a small `< Back` touch button permits QA when OS Back callback is unavailable.
3. `Assets/Project/Scripts/Platform/AtlasBoardAndroidDeviceDiagnostics.cs`: Android Development-only additional independent local log file with bounds, basic sensitive-data redaction and retention in `Application.persistentDataPath/Diagnostics`. Includes Unity warnings/exceptions and ATB code messages. Native fatal crashes still require Android logcat; never add production credentials or raw tokens to bug reports.
4. `Assets/Project/Documentation/MASTER_ROADMAP.md`: full existing roadmap retained with this acceptance checkpoint and security/release blockers.

## How to test

- Extract the ZIP **into the repository root** and overwrite its named files. No install/patch scripts.
- In Unity 6000.5.6f1 switch to Android target; wait for compilation, confirm 0 Console errors. Re-run **Atlas Board → QA & Diagnostics → QA → Android → 1 - Validate Android Readiness**.
- Run **2 - Build Development APK**. It may take substantial time on the first IL2CPP/Gradle build. Confirm both `ATLASBOARD_ANDROID_BUILD_GATE RESTORE_PLAYER_SETTINGS` and `APK_BUILD_PASS`.
- Install the newest APK on the Galaxy S20 FE (not the old APK). Verify it opens **LandscapeLeft only**, never portrait or flipped to the other horizontal direction. Check safe areas and **all** visible buttons, overlay pages, tablet UI, career and scrolling.
- Test OS Back from modal, room entry, public browser, lobby, gameplay pause and leave confirmation. Record `ATB_ANDROID_BACK_NATIVE_REGISTERED` or `ATB_ANDROID_BACK_NATIVE_UNAVAILABLE` and `ATB_ANDROID_BACK_ROUTE handler=...` in logs. Test the visible `< Back` fallback separately; native support is **not accepted** based solely on the fallback.
- After a session, use `adb logcat` and retrieve the device file below. Make note of exact reproduction steps and ATB error codes.

### ADB commands (PowerShell; Android SDK platform-tools must be installed)

```powershell
adb devices
adb logcat -d -v time -s Unity AndroidRuntime > .\AtlasBoard-Android-logcat.txt
adb pull /sdcard/Android/data/com.atlasboardgame.atlasboard/files/Diagnostics .\AtlasBoard-Android-Diagnostics
```

If `adb` is not on PATH, use the `adb.exe` installed by Unity Hub's Android SDK platform-tools. If Android scoped storage blocks `adb pull`, retrieve the Unity Logcat output; do **not** disable Android security controls to access files. `adb logcat` also captures native errors that occur before managed C# logging starts.

## About connectivity — do not confuse emulator with Cloud

- `127.0.0.1` is loopback. On a **physical phone** it does not refer to the developer's Windows PC. Starting Firebase Emulator on Windows alone is not enough; `adb reverse` cannot help unless the APK is also configured to use development emulator endpoints. Current Development APK selects cloud Functions by default unless explicit local-emulator runtime mode is enabled, and current Firebase project ID is `atlasboard-usa`.
- ATB103 is the **30-second startup delay warning**, not a standalone server diagnosis. ATB110 is **online session/identity preparation failed**. Capture actual underlying Auth/HTTP/Functions failure and backend deployment status before choosing a fix.
- For persistent online service without a manually running PC, use managed Firebase Authentication, Firestore and Cloud Functions in a **distinct staging project** first. Check Auth providers, app signing SHA-1/SHA-256, Android Firebase configuration (`google-services.json`), environment-specific Functions project ID/region, Firestore rules/indexes, App Check/Play Integrity, quotas, billing, privacy and secure rollout. **Do not deploy or disable App Check as a shortcut.** Create/configure a dedicated staging environment with explicitly approved Firebase project ID and credentials before switching this APK's backend.
- Google Play live publication additionally needs server-owned gameplay/result verification (currently no independent authoritative replay verifier), genuine deployed App Check negative tests, device QA, performance and release AAB/signing checks. Untrusted host-consistent results must not mint permanent rewards.

## Pass conditions

- Android Unity compilation and APK build succeed.
- Galaxy S20 FE stays locked to one landscape orientation with no clipped primary UI.
- Native Android Back works for tested screens (button fallback measured separately).
- Session file or `adb logcat` provides useful, bounded diagnostics without sensitive tokens.
- Backend connection is documented either as **STAGING_NOT_CONFIGURED** or independently tested against approved staging; ATB110 cannot be considered resolved until Auth + Functions + multiplayer actually work.
- No desktop/Steam scene, player settings, networking security, or production backend regressions.

**This package does not falsely close Android multiplayer, live Firebase, backend independent gameplay verification or Google Play release gates.**
