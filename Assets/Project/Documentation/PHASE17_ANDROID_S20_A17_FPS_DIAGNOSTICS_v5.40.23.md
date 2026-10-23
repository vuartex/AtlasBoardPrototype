# AtlasBoard Phase 17 — S20 FE + A17 FPS root-cause diagnosis (v5.40.23)

**Decision:** DIAGNOSTIC SOURCE CANDIDATE / LIVE ANDROID TEST OPEN. Gameplay, Android graphics profile, Spark RTDB Rules, Steam/Windows settings and the planned Phase 18A–18H remain unchanged. Do not call FPS fixed until the two physical devices report supported counters and a post-fix build is measured.

## Code audit / source-based findings (hypotheses; not physical-device proof)

Reviewed GitHub `main` source at `4cb4d99c22bd7f0a144c26cfec444ce057f56268` and canonical roadmap owner acceptance. `AtlasBoardMobileGameplayUX` presently sets Android-only target<=60, VSync 0, shadows off, AA <=2, URP scale 0.75 or emergency 0.62. It already logs 10-second `ATB_MOBILE_PERF` averages and falls back to emergency after two windows under 45 FPS. These settings may fail to help when the root bottleneck is Main Thread/GC rather than pixels.

**P0 measured on earlier build only:** S20 FE sustained approx. 16–23 FPS at 2400×1080. No equivalent A17 measurement or root-cause counters have been provided. Treat model/version/graphics API/quality as unknown until device logs arrive.

**Specific suspected hotspot requiring correlation with p95/GC spikes:** `AtlasBoardMobileGameplayUX.Update` invokes `ApplyCareerMobileAccent` once per second even in gameplay. That method calls `FindSceneObject("ProgressionCard")`, which enumerates `Resources.FindObjectsOfTypeAll<GameObject>()` and then scans the result array. Search/array allocation repeats even after presentation has been initialized. This is a credible CPU/GC hypothesis, **not** a demonstrated explanation for 16–23 FPS and is not altered by this diagnostic package. Other candidates: per-frame tablet reflection, 0.35s mobile layout adjustment, UI rebuild/reflow, RTDB snapshot deserialization and network polling, draw calls/GPU fill, resolution/thermal/power limit. Do not guess which dominates.

**Existing network telemetry:** `AtlasBoardNetworkTelemetry.SampleReported` tracks some match requests and reports duration/failures, but does not guarantee counting every raw RTDB/HTTP request. New log labels `rt*` indicate this bounded sample only, **not total network traffic**.

## New Android development-only profiler

`Assets/Project/Scripts/Platform/AtlasBoardAndroidFpsDiagnostics.cs` is one unique diagnostic-only class using `Unity.Profiling.ProfilerRecorder`. It is excluded from Unity Editor and non-Development Android release builds. It does **not** modify target FPS, render quality, resolution, UI, Firebase state, or business logic. It does not add a gameplay manager or second graphics controller.

Every ~10 seconds it writes:
- `ATB_ANDROID_FPS_WINDOW`: average FPS, p50/p95/p99 and max frame time, stalls >33/50/100 ms; lobby/online-match/offline-match/menu phase inference; target FPS, actual resolution, render scale, AA, shadows.
- Main Thread, Render Thread, GPU Frame Time, WaitForPresent **when the Unity device exposes counters**, otherwise `NA`. Values are samples, not GPU utilization percentages.
- GC alloc per frame max/average, managed/total allocated memory; draw calls/batches/setpass/triangles, canvas build marker **if supported**.
- Partial existing match network telemetry call count, latency and failures, annotated as such; battery level/status **not temperature** (`thermalSensor=NA`).
- `ATB_ANDROID_FPS_HINT` on low FPS (unless the user has set a 30-FPS cap), with hypothesis labeled *candidate*, never automatic proof. 
- `ATB_ANDROID_FPS_FOCUS` skips invalid background/paused measurements; `ATB_ANDROID_FPS_SCENE` marks scene changes; `ATB_ANDROID_FPS_DIAG_START` records device, GPU, graphics API and RAM.

## Build and test protocol — the *same* Development APK on both phones

1. Replace/add the full listed file(s) and inspect Unity C# Console for red errors. Use the existing `Atlas Board → QA & Diagnostics → QA → Android → 1 - Validate Android Readiness`, then `2 - Build Development APK` in Unity 6000.5.6f1. The existing `AtlasBoardAndroidDeviceDiagnostics` is responsible for Android session logs and `Share Logs`; no new log button is needed.
2. Install the *same* Development APK on **Samsung S20 FE** and **Samsung A17**. Battery >50%, no power-saving mode, similar room temperature; keep both foreground and, for comparative runs, use the same FPS cap and graphics settings. Record the actual model/GPU/OS from `ATB_ANDROID_FPS_DIAG_START`, not assumptions. Retest Release for final acceptance (Development build profiling overhead affects FPS).
3. **Each device / offline baseline**: start and idle at Main Menu for 60 sec; create Local/Offline 1 Human + Bots and play 60–90 sec. Include one camera move, tablet open, trade screen and City Information open. Look for *menu vs offline_match* FPS and spikes. Keep app in foreground; don't include loading/transitions in a 60 sec stable sample.
4. **Two phones / Live Spark RTDB**: one Host, the other Guest; enter lobby for ≥60 sec, then start a 2 Human + 2 Bot Casual match. Play ≥120 sec and include rolls, pawn moves, purchase, rent, tax, Event, chat/trade if convenient; look for *online_lobby vs online_match*; compare each device in Host and Guest role by running a second game with the roles swapped. No need for Unity Editor Inspect on either phone.
5. **Session reuse:** finish/leave and enter the next match **without app restart**, observe ≥60 sec; this checks accumulated UI, retained callbacks, allocations and three-match reset. The earlier N4 three-game owner PASS is preserved and does not replace the physical Android perf run.
6. At the end of **each** phase or after a representative 60-sec section, press the existing Android **Share Logs** control and share the session log text. The built-in sharing implementation limits the payload to a recent tail; if missing a phase, share soon after that phase. If native crashes occur, collect adb logcat because Unity log sharing may not capture them.
7. Provide log snippets for both models containing `ATB_ANDROID_FPS_DIAG_START`, `ATB_ANDROID_FPS_WINDOW`, `ATB_ANDROID_FPS_HINT`, `ATB_MOBILE_PERF`, `ATB_MOBILE_PERF_ADAPT`, `ATB_MOBILE_RENDER_PROFILE`, plus any `Exception`, `Error`, `OutOfMemory`, `GC`, `ATB_RTDB` warnings. Never include Firebase auth tokens or passwords.

## Interpreting results without inventing root causes

| Evidence | Working hypothesis / next proof |
| --- | --- |
| Both devices slow *offline* and online | Game/render/UI bottleneck; compare main-thread/GPU metrics and GC. |
| Offline smooth; online slow with RTDB latency/GC spikes | Serialization/polling or online authority/UI churn candidate. Verify Profiler timeline, not only ping. |
| Only A17 slow; high supported GPU frame time, large batches | Device-specific GPU/fill/batching candidate. Check resolution/renderScale before changing quality. |
| p95 30–60ms with high Main Thread/GcAlloc; GPU counter low | Scripts/UI/resource scanning candidate; inspect `Resources.FindObjectsOfTypeAll`, Canvas rebuild and hierarchy. |
| `renderScale=1.00` when profile expects `0.75` / `0.62` | Existing reflection setter may not affect current pipeline. Inspect active RenderPipelineAsset; don't assume downscale applied. |
| `targetFps=30` and stable ~30 FPS | Cap is configured; do not label GPU/CPU bound from 30-FPS measurement. |
| Main/GPU counter `NA` | This build/device did not expose that counter; use Unity Profiler connected device and Timeline/GPU profiling if available. |
| FPS declines during long session, GC/allocated memory grows | Accumulation/leak/thermal candidate; battery info alone cannot prove thermal throttling. |

## Acceptance / explicit stop

This package is **diagnostic only**, no premature FPS PASS. After comparing S20 FE and A17 logs: implement one measured bottleneck fix in its canonical existing source file; compare **before vs after on both devices**, with consistent graphics settings; record avg, p95, FPS cap, GC and safe UI/network regressions. Android-only modifications must not change Windows/Steam. **Stop here and await owner permission before moving to another phase.** Firebase rules remain unchanged, no deploy needed; no GitHub push/commit was executed.
