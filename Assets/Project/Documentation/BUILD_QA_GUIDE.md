# AtlasBoard — Build and QA Guide

Status: Phase 16 implementation candidate; local Phase 15I/15J/15K evidence is accepted. **Phase 15K production close remains BLOCKED** until real Steam AppID, publisher secret, deployed App Check cryptographic negative proof, and staged rollout are verified. This guide does not declare any production release accepted.

## Stable interface and script organization

Do not create a new `phaseXX-fix-test*.mjs` for every correction. `Backend/Firebase/functions/scripts/qa.mjs` is the public QA entrypoint. Keep older `phase15*` scripts intact as historical evidence and internal regression dependencies; their filenames must not be renamed until their references are migrated and a full suite passes. New test features should be incorporated in `qa.mjs` or grouped by domain, not by chat turn.

From `C:\Users\crazy\AtlasBoardPrototype\Backend\Firebase\functions`:

```powershell
node .\scripts\qa.mjs --check
node .\scripts\qa.mjs --full
```

`--check` runs Unity *structure-only* preflight and the accepted Phase 15K static regression, including TypeScript build, focused ESLint and its security audits. `--full` adds new Auth/Firestore/Functions emulator sessions: integration tests with App Check off, then local App Check enforcement tests with App Check on. **No existing emulator may occupy ports 9099, 8080, 5001, 4400.** The script starts/stops emulator instances and restores `.env.local` after normal exit. To recover if forcibly killed while configuring the environment:

```powershell
node .\scripts\qa.mjs --restore-env
```

The QA tool temporarily places `STEAM_APP_ID=480` in `.env.local` for emulator use only. It does not edit real Steam secrets; if `.secret.local` does not exist, it creates and later removes a harmless test placeholder so the emulator does not query cloud Secret Manager. Keep all local secret files out of Git. **Do not share logs containing private credentials.**

CI (GitHub Actions) runs `qa.mjs --ci`, no deployment, Unity license, or Steam upload. The workflow becomes executable only after the accepted Phase 15I/J/K local work is intentionally committed and pushed; GitHub main may currently lag accepted local source. CI is backend plus Unity metadata, **not a Unity compilation/build**.

## Unity Windows build interface

Open Unity **6000.5.6f1** and switch active target to **Windows x86_64** in Build Profiles, allowing the editor to finish recompiling. The new persistent editor menu is:

- `Atlas Board/QA & Diagnostics/Build/Validate Readiness`
- `Atlas Board/QA & Diagnostics/Build/Windows Development Build`
- `Atlas Board/QA & Diagnostics/Build/Windows Release Candidate (NOT FOR DISTRIBUTION)`

Development uses Unity Development/Debugging build options; Release Candidate uses non-development build options but **is NOT approved for public distribution**. Both output to the ignored `Builds/Windows/<channel>/...` tree, with a `build-receipt.json` and `NOT_FOR_PUBLIC_RELEASE.txt`. No Steam upload is performed.

The current `EditorBuildSettings` puts `SampleScene` before `Board_Prototype`; the QA build sets `Board_Prototype` first **in its BuildPlayerOptions only**, and does not rewrite `.asset` settings. If compiling/building produces warnings or errors, capture and triage them rather than declaring Phase 16 complete.

Optional Unity batchmode structural check (use your actual Unity Hub Editor path):

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'C:\Users\crazy\AtlasBoardPrototype' -executeMethod AtlasBoardBuildQA.BatchValidate -logFile 'C:\Users\crazy\Desktop\AtlasBoard-Unity-QA.log'
```

An editor installation and the correct Unity license/target modules must already be available. This command does not build the player; the build commands are separate.

## Runtime QA evidence — required for Phase 16 acceptance

Track **PASS / FAIL / BLOCKED**, test machine, date, build receipt and observed results. Do not equate static scripts with physical validation.

| Category | Required exercise | Current gate |
|---|---|---|
| Unity compilation | Reopen Unity, zero unexplained errors/warnings, build Windows Development and candidate | Pending actual Editor results |
| Performance | Profiler frame CPU/GPU time, GC spikes, memory and loading across typical gameplay | Pending measurements |
| Video | Aspect ratios 16:9, 16:10, 21:9; VSync on/off, FPS; low-end/high-end hardware | Pending devices |
| Controls | Mouse, keyboard, scroll, tablet input focus, overlays, Steam Overlay | Pending editor/build run |
| Localization | EN/TR/ES/FR/DE/KO/RU glyph, wrap, TMP font fallback, UI clipping | Pending screenshots/test |
| Multiplayer | Two physical PCs, then 2/3/4 seats, host/guest reversal and hot join | Pending physical testers |
| Network resilience | Reconnect, host crash/migration, AFK, repeated rematch/lobby reuse | Pending physical testers |
| Network quality HUD | Injected latency/jitter/loss and visible ping/jitter/packet loss indicators | Pending measured run |
| Deployed parity | Emulator vs authorized staging backend behaviors | Blocked until safe staging exists |
| Production Steam | Real AppID/publisher key, Windows custom App Check, deployed invalid-token rejection | Phase 15K production gate BLOCKED |

## Versioning, artifact and Steam branch discipline

Artifacts follow `AtlasBoard-v<bundleVersion>-<channel>-win64-<UTC>.exe` in a unique build directory, with a receipt. Record the exact source Git commit **after** the user explicitly commits accepted work; do not claim an uncommitted build has a durable remote revision. Proposed future Steam branch names: `internal-testing`, `beta`, `public-default`, with `public-default` blocked until production security gates, live testing and explicit release approval. None are created automatically.

## Current acceptance boundary

Backend security regression from Phase 15K: accepted on local emulators (App Check disabled/enabled in separate sessions; emulator cannot prove invalid-token cryptographic rejection). Phase 16 QA: **IN PROGRESS** until Unity compile/build and multi-PC/runtime evidence. Production backend and Steam shipping: **NOT APPROVED**.
