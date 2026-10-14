# AtlasBoard Phase 17B — Android-first Unity build and device smoke acceptance

**Version:** v5.25.0, 2026-10-13 (UTC)  
**Purpose:** Finish the first of TWO feature-development-entry gates after the confirmed v5.24.0 full Firebase local QA.  
**Scope:** Android Unity Editor source compilation and actual development APK. Windows/Steam integration remains untouched. No production backend, Play Console or Steam action.

## Verified input / honest boundaries

The owner's `Pasted text(20261013-043843).txt` ended `AtlasBoard QA FULL LOCAL PASS (emulators only).` Phase17A validator 19/19 offline, Phase17A callable 12/12 and Phase17A.3 concurrent-finalization 9/9 passed. All previous Security Core 01–07, economy, moderation, promo, Firestore, Steam development and App Check emulator regression suites passed. In the eight-way Public Lobby browser burst, 4 of 8 calls exceeded 5 seconds; actual device/staging latency remains an OPEN gate. These are SHARED backend results, **not Android compilation or runtime acceptance**.

## First feature-entry gate: Unity Android Editor + development APK

1. Open project with **Unity 6000.5.6f1**. Allow all scripts/assets/packages to finish importing. Inspect the Editor Console. If any compilation error exists, mark FAIL and do not claim APK PASS.
2. Install **Android Build Support**, including Android SDK/NDK Tools and OpenJDK, using Unity Hub for this Editor version.
3. Select **File > Build Profiles > Android > Switch Platform**. The existing Windows `AtlasBoardBuildQA.cs` deliberately targets Windows, so **do not replace or invoke that older Windows-only gate** for this step.
4. Select **Atlas Board > QA & Diagnostics > QA > Android > 1 - Validate Android Readiness**. Expected Console marker: `ATLASBOARD_ANDROID_BUILD_GATE PREFLIGHT_PASS`.
5. Select **Atlas Board > QA & Diagnostics > QA > Android > 2 - Build Development APK**. Wait for `ATLASBOARD_ANDROID_BUILD_GATE APK_BUILD_PASS` with an actual file path. APK is written to `Builds/QA/Android/AtlasBoard-Android-Development-YYYYMMDD-HHMMSS.apk` and the script does not remove older builds.
6. The script includes only the canonical `Assets/Board_Prototype.unity` as its Android smoke startup scene, **not SampleScene**. It does not modify Build Settings, target platform, signing, bundle version, package name, Firebase environment, or store settings. It temporarily sets development-only HTTP and APK mode, then restores their original Player Settings even on failure.
7. If the build errors, provide **the first relevant Unity Console compiler error and `Editor.log` build failure**. Do not use `node scripts/qa.mjs --full` to substitute for Unity compilation.

**Optional headless entrypoints** (after Android was selected in the Editor and Unity 6 installed): `-batchmode -quit -projectPath <project> -executeMethod AtlasBoardAndroidSmokeQA.BatchValidate -logFile <path>` or `-executeMethod AtlasBoardAndroidSmokeQA.BatchBuildDevelopmentApk`. Exit code 0 is required; no direct Cloud build/deploy is invoked.

## Second feature-entry gate: real Android device test (must be done before adding extra features)

- Install APK onto at least one real Android device (ideally two distinct Android versions/GPUs). No boot crash, black screen, unexpected Steam dependency, missing text, touch/controller issues or excessive heating; visually confirm the board and main UI.
- Check tap, hold/drag, camera zoom and move, board city information, screen rotation/resize handling, audio and accessibility toggles; capture `adb logcat` if it crashes.
- Login, list/create/join/leave lobby, host/guest ready and start, 2-player complete match, reconnect/AFK/rematch, purchases/trades, settings persistence and matching/cross-login identity. Validate with **two real Android clients** or approved Android + other-platform combination as required.
- Use only an explicitly configured **isolated STAGING backend** with valid mobile auth and Play Integrity staging, or local Firebase emulators via per-device USB `adb reverse` with strict network isolation. **The app's `127.0.0.1` on a phone is the phone itself**, not the developer PC; an APK compiling is NOT proof of Firebase connectivity. Don't point test fixtures at production. Google Play release configuration must not be inferred from this dev APK.
- Record device models, Android versions, Unity Console / logcat issues, backend environment, FPS/frame stalls and network latency. The prior Public Lobby 4/8 over-5s emulator warning remains OPEN until staging-device measurement.

## New feature development after both gates

**Two remaining feature-entry gates counted after v5.24.0:** (1) Unity Android build; (2) real Android gameplay smoke. Completing these does **not** authorize public launch. It means a practical baseline on which to implement optional new features, with backend security reward guard intact and fresh regression after every feature.

## Google Play RELEASE blockers (independent of feature-entry gates)

- Real independent server verification / trusted match event evidence or explicit no-permanent-XP release policy: protected result receipt is still not minted by a trustworthy live replay verifier. Host-self-consistency and concurrency tests do not close this.
- Android Play Integrity/App Check and deployed invalid-token enforcement, Firebase authenticated staging security and user deletion/privacy compliance; local emulators accept some malformed nonempty App Check tokens as `VALID`.
- Production-like 2–4 client load, reconnect, billing/quotas, moderation reporting, retained data/TTL, crash metrics, 16I Public Lobby production latency, game economy and abuse tests.
- Signed ARM64 Android App Bundle in **non-development** configuration, compatible SDK/target API, store listing, permissions/privacy policy, Play Console internal testing and all applicable release policies. Requirements change with time; verify in Play Console immediately before submission.
- Steam later optionally; its real AppID/Steamworks publisher flow is separate and must not delay Google Play work.

## Source ZIP contract

Every future package contains COMPLETE replacement/new source/doc files at canonical project-root-relative paths and full `MASTER_ROADMAP.md`. Never send patch/diff/APPLY script or incomplete roadmap. Owner extracts ZIP directly at project root; project-level file settings are not overwritten by this package.
