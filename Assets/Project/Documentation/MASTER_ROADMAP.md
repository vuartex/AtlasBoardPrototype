# ATLAS BOARD
## Master Architecture & Development Roadmap v5.17

**Canonical checkpoint:** 2026-10-12 owner local time (America/Denver); owner Windows QA log recorded 2026-10-13 UTC  
**Repository:** `vuartex/AtlasBoardPrototype`  
**Last verified pushed GitHub HEAD (rechecked during v5.17 preparation):** `bed153c2071716e1d7216fe0e3e8ecdb36f3f341`  
**Last verified pushed HEAD commit:** `Accept Phase 16I local QA and Phase 17B Steam invite overlay validation`  
**Current accepted checkpoint:** `SECURITY CORE 01/02/03 LOCAL QA ACCEPTED by owner on Windows: SEC01 normal progression 14/14 plus guarded host-result negative checks; SEC02 economy 19/19; SEC03 rewards 15/15; restored inventory 11/11 and promo 13/13; terminal AtlasBoard QA FULL LOCAL PASS. SEC11 interim reward gate protects against observed fabricated-match XP (XP=0, wins=0, first_win=false), but an independent server-side full gameplay/result verifier is still NOT implemented and unverified permanent match rewards remain PENDING. All new source/test changes and this roadmap are OWNER LOCAL until pushed; no production deployment. Execution order: remaining Security/Core Audit -> optional pre-release gameplay/UX features -> real Steamworks Phase17C -> final feature-frozen audit -> owner GO/NO-GO.`  
**Phase 12 close commit:** `0eca81450be91b30a5dc1cd66d901c46e2cab07c` — `Phase 12: finalize online resilience hardening and crash recovery`  
**Phase 14 close commit:** `77b4a1c29fe3ee55dbd27f4d35e6ce5e9d02e157` — `Close Phase 14 UI/UX polish and finalize localization/input handling`  
**Unity baseline:** Unity 6000.5.6f1, `Assets/Board_Prototype.unity`, unless a later committed upgrade explicitly changes it.  
**Canonical repository roadmap location:** `Assets/Project/Documentation/MASTER_ROADMAP.md`

This v5.17 checkpoint is based on already-pushed GitHub main `bed153c2`, while preserving the owner's newer local v5.12 roadmap content. **The owner has explicitly deferred Phase 17C because no official AtlasBoard Steamworks AppID has been obtained.** First complete Security & Core Audit (fix issues independently of the AppID where possible), then select/implement pre-release features, then return to Phase 17C and run actual cross-device/Steam/deployed tests; finally repeat audit on the frozen Release candidate. This roadmap-only handoff does not change running source, deployed services, GitHub commits or production settings; the owner will stage and push the accepted local source separately.

# 1. Source, replacement and Git discipline

Before meaningful source changes:

- Inspect current GitHub `main` before using repository source as a baseline.
- GitHub is authoritative only for work already committed and pushed.
- Newer accepted local work must never be overwritten by an older GitHub copy.
- Deliver only complete replacement source files at their canonical paths when a functional change is necessary; NEVER add new `Tools` folders, one-off patch scripts, installers, or diagnostic helpers to the project by default. A roadmap-only update must contain only the roadmap.
- Do not use textual patches, `git apply` patches or installer scripts unless explicitly requested. Reuse existing QA entrypoints and diagnostics rather than proliferating one-time tools.
- Do not auto-commit, auto-push, deploy or mutate production configuration.
- Do not use `git add .`; stage only intended files.
- Do not stage unrelated historical/font/generated changes.
- Backend QA belongs in `Backend/Firebase/functions/scripts/qa.mjs`.
- Roadmap status changes belong in this canonical `MASTER_ROADMAP.md`.
- Firebase Functions source changes require TypeScript build and focused validation before acceptance.
- Restart emulators after backend source changes when emulator runtime behavior is under test.
- Unity runtime changes require compile + runtime smoke before acceptance.
- Static review is not equivalent to production runtime acceptance.
- Never place Steam publisher secrets, production credentials, App Check debug tokens or raw provider tickets in Unity source/logs.
- Local-emulator QA shortcuts must be narrowly gated and must never silently apply to Release.

Current source authority (checked 2026-10-12 America/Denver):

- Latest verified pushed `main`: `bed153c2071716e1d7216fe0e3e8ecdb36f3f341`, `Accept Phase 16I local QA and Phase 17B Steam invite overlay validation`. Phase 16I and the Phase 17B.2 QA component are in the PUSHED checkpoint.
- The push also included `Tools/Phase17B/SteamGuestPreflight.ps1` and two modified TextMesh Pro/Noto font assets. They are NOT prerequisites for future roadmap-only delivery; review those font changes for unintended diffs, and consider removing the one-off preflight script later through an explicitly approved housekeeping change, NOT an automatic cleanup.
- Keep development-only `steam_appid.txt` at AppID 480 out of the repository and release package; never stage Steam tickets, publisher API keys, private tokens, log dumps, generated builds or emulator database files.
- Only the owner stages/pushes approved project files; inspect local `git status` and any new local work before replacing this roadmap.

# 2. Durable architecture invariants

## 2.1 Canonical identity and provider boundaries

- Canonical Atlas AccountId remains authoritative.
- SeatId remains authoritative inside a match/session.
- SteamID is a verified external provider identity mapped to Atlas AccountId.
- Steam remains an adapter, not the canonical account database.
- Wallet, inventory, progression, lobby, match, moderation and history remain Atlas/Firebase-side systems.
- Development Steam AppID `480` is development-only and must never be treated as the production AtlasBoard AppID.

## 2.2 Server authority

Backend/server authority remains required for:

- account identity and provider mappings;
- wallet/premium economy;
- inventory entitlements;
- promo/event rewards;
- lobby/session membership;
- match authority and authority transfer;
- presence/reconnect state;
- chat/moderation/report state;
- completed-match progression and achievements;
- future competitive/ranked outcomes.

## 2.3 Host/seat invariant

Gameplay state belongs to the Player Seat, not the current controller.

`Human -> TemporaryBot -> Human` must preserve:

- AccountId / SeatId;
- money;
- properties;
- pawn position;
- development;
- turn state;
- bankruptcy state;
- match-result state.

Reconnect and snapshot optimization must preserve this invariant. Do not reduce or discard required authoritative synchronization merely to improve apparent latency.

## 2.4 Hosting strategy

- Firebase / managed Google Cloud remains the selected backend direction.
- Do not add a self-managed always-on VPS/game server by default.
- Steam does not automatically host AtlasBoard gameplay authority.
- Steam Networking/SDR may be evaluated later as an optional transport optimization.
- Current architecture remains Firebase-backed lobby/match persistence plus Host-authoritative turn simulation.
- Local emulator cold-start behavior must not be mistaken for measured deployed production latency.

## 2.5 App Check invariant

- Editor/development security validation uses the Firebase App Check debug-provider path.
- Desktop standalone production architecture uses the Steam custom App Check provider.
- `platformSteamCreateAppCheckToken` remains the bootstrap endpoint and must not require an existing App Check token.
- Production Steam custom App Check runtime acceptance remains deferred until a real AtlasBoard Steam AppID and publisher Web API secret exist.
- Local Functions Emulator acceptance is not cryptographic proof of production App Check validity.
- Local two-client Guest QA may use an explicit emulator-only placeholder App Check header only when:
  - Development Build;
  - Windows standalone Guest;
  - explicit `-atlasLocalEmulators`;
  - loopback Functions endpoint (`127.0.0.1` / `localhost`).
- That placeholder is never a production credential.

## 2.6 Phase 14 UI invariants

- Full gameplay-tablet geometry uses `TabletUIManager.tabletRoot`.
- Existing gameplay managers remain authoritative; presentation wrappers mirror/call them.
- While a gameplay tablet is open, world City Information clicks do not pass through.
- City Information ScrollRect owns wheel/drag while the pointer is interacting with it.
- Generic labels such as `Bölge N` resolve into the active locale.
- Required localization target remains EN/TR/ES/FR/DE/KO/RU.
- Rejected world-space P1/P2/P3/P4 ownership badges stay removed.

## 2.7 Public error-message invariant

Player-facing online/runtime errors must not expose backend implementation details.

Public UI must never expose:

- Firebase / Firestore / Functions provider names;
- hostnames, ports or backend URLs;
- project IDs;
- raw HTTP/service responses;
- auth headers or tokens;
- database/collection names;
- Steam publisher/security internals;
- stack traces.

Player-facing errors use:

- a generic localized message;
- a stable `ATBxxx` support code.

Technical meaning belongs in protected diagnostics and the internal codebook.

# 3. Accepted foundation summary

Accepted foundations include:

- Core board gameplay, dice, movement and economy.
- Auction, Trade, Development, bankruptcy and Event/Special systems.
- Bots and player-seat architecture.
- Camera/audio/settings.
- Seven-language localization.
- Private/public lobby flow.
- Public lobby browser and Join by Code.
- Host-authoritative online gameplay.
- Lobby + Match Chat and moderation/report foundation.
- Store/economy/seasonal-event foundation.
- Career/progression/history/achievement foundation.
- Provider-neutral platform contracts.
- Steamworks.NET integration.
- Secure Steam account linking.
- Returning Steam sign-in and Steam recovery.
- Shared development backend.
- Same-match reconnect preservation.
- Graceful Host Migration and crash failover.
- Phase 12 resilience hardening.
- Phase 14A–14K UI/UX polish.
- Phase 15 security/operations hardening through 15K at the documented local/emulator acceptance level.
- Phase 16 backend QA and Unity build-readiness validation.
- Local two-client Guest startup stabilization.
- Runtime diagnostics.
- Snapshot single-flight/coalescing.
- Host-bootstrap prioritization.
- Localized loading/bootstrap UX.
- ATB frontend error-code framework.

# 4. Phase status overview

## Phase 3B — Seat / Turn / AFK
**Status: DONE.**

## Phase 3C — Account & Cloud Profile
**Status: FOUNDATION DONE; production provider validation remains a release gate.**

## Phase 3D — Lobby / Session UI
**Status: DONE.**

## Phase 4 — Public Lobby Browser
**Status: DONE foundation; Phase 16I local cursor pagination accepted. Production-scale validation remains outstanding.**

## Phase 5 — Online Gameplay Networking
**Status: MAIN PATH DONE / runtime flows proven. Phase 16H latency/concurrency hardening accepted locally.**

## Phase 6 — Chat + Moderation Foundation
**Status: DONE.**

## Phase 8 — Gameplay / Content Checkpoint
**Status: DONE.**

## Phase 9 — Store / Economy / Seasonal Event
**Status: DONE foundation.**

## Phase 10 — Career / Progression / History / Achievements
**Status: DONE foundation.**

## Phase 11 — Platform / Steam / Multiplayer / Host Migration Foundation
**Status: DONE through 11G.**

Key pushed checkpoints:

- `11fdcdaba2f37e506994708674b0ee01e62ab867` — 11A/11B platform foundation + Steam integration.
- `8e91db76914db59cbf17f28f788adf24a193fd17` — 11C secure Steam account linking.
- `a5e4b28f442a7ad22b5789096633c8ef7bd141f1` — 11D returning Steam sign-in.
- `a8d777b8eb9e798fec8b5f750185ae428b34ca18` — 11E Steam recovery / achievement resync.
- `6ddf01ec66f0116396d18b27b171b9c6080ee671` — 11F shared multiplayer lobby/reconnect.
- `068ca6f0d1736516ace63b79681227202a408cf8` — 11G Host Migration parent checkpoint.

## Phase 12 — Resilience Hardening
**Status: DONE / accepted.**

Close commit:

- `0eca81450be91b30a5dc1cd66d901c46e2cab07c`

## Phase 13
**Status: RETIRED AS A SEPARATE IMPLEMENTATION PHASE.**

Host Migration was absorbed into Phase 11G and further validated in Phase 12.

## Phase 14 — Dedicated UI/UX Polish
**Status: DONE / CLOSED / PUSHED.**

Close commit:

- `77b4a1c29fe3ee55dbd27f4d35e6ce5e9d02e157`
- `Close Phase 14 UI/UX polish and finalize localization/input handling`

# 5. Phase 15 — Security and Production Backend Hardening

**Overall engineering status: LOCAL/EMULATOR SECURITY AND OPERATIONS IMPLEMENTATION COMPLETE THROUGH 15K.**  
**Checkpoint through Phase 15 local close is pushed in `74697e472b865f76afe8f23b3ba8036ac5d35032`.**  
**Production Steam cryptographic acceptance: DEFERRED.**

## 15A — Firestore Direct-Client Security Boundary
**Status: DONE / PUSHED.**

## 15B — Unified Emulator Firebase Session
**Status: DONE for Editor/dev architecture; standalone local Guest uses the isolated QA topology documented under Phase 16F.**

## 15C — App Check Transport Plumbing
**Status: DONE / PUSHED.**

## 15D — Firebase App Check SDK + Editor Debug Provider
**Status: DONE / PUSHED.**

## 15E — Windows/Steam Production Custom App Check Provider
**Status: CODE/BUILD ACCEPTED; REAL PRODUCTION RUNTIME PROOF DEFERRED.**

## 15F — Staged App Check Enforcement
**Status: DONE at local/emulator level; deployed cryptographic invalid-token proof deferred.**

## 15G — Callable Authorization + Abuse Hardening
**Status: DONE.**

## 15H — Economy / Commerce Security
**Status: DONE.**

## 15I — Moderation + Data Lifecycle
**Status: DONE / PUSHED CHECKPOINT.**

Accepted direction:

- explicit moderator authority;
- no user self-escalation;
- server-authoritative chat/report/sanction flows;
- deleted/expired chat excluded from normal reads;
- open investigation evidence preserved;
- audit/config evidence protected;
- local moderation security validation passed.

## 15J — Production Operations / Environment Hardening
**Status: DONE / PUSHED CHECKPOINT.**

Accepted direction:

- loopback-only emulator exposure;
- fail-closed environment boundaries;
- publisher secret kept server-side;
- sensitive Steam/App Check logs bounded/redacted;
- credentials excluded from Git;
- production rollout blocked until real production prerequisites exist.

## 15K — Steam / Backend Security Close
**Status: LOCAL CODE/EMULATOR SECURITY CLOSE PASS / PUSHED CHECKPOINT; PRODUCTION CRYPTOGRAPHIC CLOSE DEFERRED.**

Still required before production security close:

- real AtlasBoard Steam AppID;
- real publisher Web API secret;
- real Steam ticket verification;
- real Windows standalone Steam custom App Check runtime acceptance;
- deployed missing-token and cryptographically invalid-token rejection proof;
- bootstrap exemption proof against deployed configuration;
- Release verification that no local emulator shortcut can activate.

# 6. Phase 16 — Build Readiness, Runtime Stability and Performance

**Status: ACTIVE.**  
**Current project workstream: Security & Core Audit active; Phase 17C formally deferred until after pre-release feature work and official Steamworks AppID availability.**

## 16A — Consolidated backend QA
**Status: PASS locally.**

Canonical backend QA:

`Backend/Firebase/functions/scripts/qa.mjs`

## 16B — Unity build readiness
**Status: PASS locally.**

Canonical validator:

`Assets/Project/Editor/AtlasBoardBuildQA.cs`

Canonical menu:

`Atlas Board/QA & Diagnostics/QA/Validate Build Readiness`

## 16C — Canonical two-client local Guest QA build
**Status: IMPLEMENTED / ACCEPTED.**

Canonical builder:

`Assets/Project/Editor/AtlasBoardTwoClientLobbyTestV1.cs`

Canonical menu:

`Atlas Board/Online & Backend/Online/Build Two-Client Guest Test v1`

Output:

`C:\Users\crazy\AtlasBoard_3D3C_GuestBuild\AtlasBoardGuest.exe`

Local emulator testing must use the generated BAT with `-atlasLocalEmulators`.

## 16D — Automatic runtime/crash diagnostics
**Status: IMPLEMENTED / ACCEPTED.**

Canonical Development Guest diagnostics:

`Assets/Project/Scripts/Online/AtlasBoardRuntimeDiagnostics.cs`

Diagnostics include:

- startup/build metadata;
- Unity warnings/errors;
- frame summaries;
- main-thread stalls;
- session clean/unclean markers;
- separate timestamped files per run.

## 16E — October 11 Windows Guest native-crash regression
**Status: MITIGATED / CURRENT LOCAL QA PASS / REGRESSION WATCH.**

Historical signature:

- Windows WER `BEX64`;
- `0xC0000409`;
- subcode `7`;
- `ucrtbase.dll`;
- repeated same fault offset.

Important conclusion:

- exact native root function was not symbolically proven;
- D3D11 did not solve it;
- backend latency was not sufficient to explain the native process death;
- unnecessary overlapping native Firebase/Steam startup paths in the standalone local Guest were strongly correlated with the failure.

Current local Guest mitigation:

1. loopback Development callable requests use an emulator-only App Check placeholder;
2. native App Check provider registration is bypassed;
3. Steam startup/provider path is bypassed;
4. Phase 15B secondary FirebaseAuth/Firestore SDK emulator sign-in is suppressed;
5. Guest build-scene native AccountService/test components are stripped;
6. HTTP Auth Emulator + Functions Emulator multiplayer identity remains authoritative;
7. Editor and Release behavior remain separate.

Repeated later Guest sessions have not reproduced the historical native crash and have reached gameplay/clean exit.

Do not claim the exact historical native root cause is mathematically proven. Reopen immediately if the same signature returns.

## 16F — QA-only isolation and Release retest gate
**Status: ACCEPTED.**

Intentionally bypassed only in the explicit local Windows Development Guest:

- native FirebaseAuth initialization;
- native Firestore initialization;
- Phase 15B secondary SDK emulator sign-in;
- native App Check provider registration;
- SteamAPI/provider startup.

These must not be casually re-enabled in the local Guest.

Before Release:

- verify build-strip logic cannot affect Release;
- verify native Firebase account/profile path;
- verify cloud preferences/profile persistence;
- verify real Steam identity;
- verify returning Steam sign-in;
- verify real Steam custom App Check;
- run repeated Release startup/shutdown tests;
- reopen native crash investigation if the historical signature returns.

There is no forgotten feature to turn back on now. This is an explicit QA topology separation.

## 16G — Lobby/match responsiveness
**Status: ACCEPTED locally.**

Accepted behavior:

- Lobby Back remains usable during pending work.
- Guest match/lobby exit is immediate locally.
- Remote cleanup remains safe/asynchronous.
- Host exit preserves authority handoff semantics.
- stale responses from a departed match cannot reopen old UI/session state.

Invariant:

Do not break reconnect, host migration or authoritative snapshot semantics to improve apparent responsiveness.

## 16H — First-run latency / request concurrency
**Status: PASS locally.**

### 16H.1 — Snapshot single-flight / coalescing
**Status: PASS locally.**

Accepted behavior:

- only one `matchGetSnapshot` network request per client/match may be in flight;
- concurrent callers join the existing task;
- forced refresh during an in-flight call becomes at most one follow-up refresh;
- reconnect/host-migration/explicit refresh semantics remain intact;
- polling cadence was not blindly removed;
- remote pawn-cosmetic intent does not submit before a valid authoritative match snapshot exists.

Observed acceptance evidence:

- runtime diagnostics reported `NetworkRequests=1, CoalescedCallers=1` under overlapping callers;
- previous large snapshot-request bursts were no longer reproduced;
- warm `matchGetSnapshot` execution returned to normal tens-of-milliseconds behavior.

### 16H.2 — Host bootstrap priority
**Status: PASS locally.**

Fresh-match startup now prioritizes:

1. initial authoritative snapshot/readiness;
2. first authoritative host publish;
3. follower/starting-roll visibility;
4. maintenance traffic after the critical frame.

Non-critical startup maintenance such as presence/sweep/reconnect-expiry no longer races ahead of the first authoritative frame.

Observed acceptance evidence:

- first authoritative host frame was published in approximately `221 ms` in the accepted test;
- follower match loading observed authoritative readiness in tens to low hundreds of milliseconds;
- starting-roll flow remained functional.

### 16H.3 — Localized loading/bootstrap UX + frontend diagnostic code system
**Status: PASS locally.**

New runtime files:

- `Assets/Project/Scripts/UI/Loading/AtlasBoardLoadingExperience.cs`
- `Assets/Project/Scripts/UI/Loading/AtlasBoardFrontendErrorCodes.cs`

New documentation:

- `Assets/Project/Documentation/FRONTEND_ERROR_CODEBOOK.md`

Accepted behavior:

- startup loading screen performs real readiness/bootstrap work;
- Development/Editor may prefetch a small public-lobby read to absorb emulator cold start behind loading;
- Release does not add speculative lobby traffic merely for warming;
- match transition shows a short blocking loading state until an authoritative snapshot exists;
- loading screens do not disable reconnect, host migration, snapshots or security;
- localized tips rotate during loading;
- supported loading languages remain EN/TR/ES/FR/DE/KO/RU;
- public error text never exposes infrastructure/provider details;
- player-facing problems use generic localized text plus stable `ATBxxx` codes;
- Development logs include phase name, duration, status and sanitized diagnostic notes.

Delay behavior:

- Startup `10 s` -> `ATB101`
- Startup `20 s` -> `ATB102`
- Startup `30 s` -> `ATB103`
- Match sync `10 s` -> `ATB301`
- Match sync `20 s` -> `ATB302`
- Match sync `30 s` -> `ATB303`

Delay codes are observations, not automatic proof of backend failure.

Accepted startup evidence from the later healthy Guest sample:

- identity readiness approximately `75 ms`;
- lobby prefetch approximately `238 ms`;
- profile bootstrap approximately `994 ms`;
- total startup bootstrap approximately `1203 ms`;
- match authoritative readiness approximately `44–151 ms` depending on transition;
- session completed with clean exit.

Cold emulator runs may still cross a delay threshold such as `ATB101`; if the operation subsequently succeeds, the game continues normally.

### 16H housekeeping — one-shot match-loading completion
**Status: PASS locally.**

The earlier accepted test showed repeated `ATB_MATCH_LOAD event=authoritative_ready` diagnostic lines while the short overlay-close delay was still pending.

Accepted fix:

- `AtlasBoardLoadingExperience.cs` uses a one-shot completion guard per match-loading cycle;
- repeated `SnapshotChanged` notifications no longer duplicate the `authoritative_ready` completion log;
- this does not change polling, networking, reconnect, host migration, backend calls or loading duration.

Acceptance evidence:

- later Guest runtime showed one `ATB_MATCH_LOAD event=authoritative_ready` line for each distinct loading cycle;
- no duplicate completion spam was observed;
- gameplay and networking remained healthy.

### 16H.4 — Critical gameplay-intent transport reliability
**Status: PASS locally.**

A real runtime defect was observed after the initial Phase 16H acceptance:

- a remote purchase decision could surface client-side `HTTP 0: Request timeout`;
- host-crash recovery/maintenance calls could time out in the same window;
- backend execution itself was not consistently slow;
- the missing usable purchase decision could reach the existing 10-second safe-AFK fallback and convert the action to `SKIP`/auction even though the player had acted.

Accepted fix:

- gameplay-critical intents use a short bounded transport attempt;
- one transport retry is allowed only for retryable timeout/connection failure;
- retry preserves the **same `clientCommandId`** so backend idempotency remains authoritative;
- no new gameplay command identity is created for retry;
- while a critical intent is active, non-critical client traffic is deprioritized/suppressed where safe so it does not compete with the decision request;
- snapshot/reconnect/host-migration authority remains intact;
- cosmetic traffic remains non-critical.

Acceptance evidence from the later Editor + Guest test:

- no `HTTP 0: Request timeout`;
- no `Remote purchase decision failed`;
- no host-recovery/sweep timeout regression;
- real Guest and Editor purchase decisions completed successfully;
- backend `matchSubmitIntent` examples completed in tens of milliseconds and were consumed/acknowledged by the host;
- snapshot single-flight remained active;
- host bootstrap remained active;
- session ended with `SESSION_CLEAN_EXIT droppedMessages=0`.

The retry branch itself did not need to trigger in the accepted healthy run; the test proves the normal path did not regress and the previously observed failure did not reproduce under the same local topology.

## 16I — Public lobby scale / discovery / measured capacity
**Status: LOCAL/EMULATOR ACCEPTED — PRODUCTION CAPACITY NOT PROVEN.**

Accepted local Phase 16I work:

- Server-side cursor-based public lobby discovery, callable contract, Unity paging client/browser and focused QA were delivered/tested locally. The implementation replaced the earlier fixed-first-N discovery behavior in this accepted local branch.
- Public-lobby backend E2E, Auth/App Check and host-close test regressions passed in the accepted Phase 16I test sequence; consolidated local QA was reported PASS.
- Initial capacity/read probes: 80 synthetic discovery projections with 2 real two-user test matches, 168/168 workflows; 120 projections with 4 matches, 960/960 workflows. Combined 1,128/1,128 successful measured workflows and 1,410 callable attempts, zero reported failures. These are local emulator observations, **not production user-capacity numbers**.
- Active match transport smoke: 8 independent two-account matches, 56/56 full guest-intent/host-list/ACK/publish/guest-read cycles successful; idempotent replay and stale-revision guards passed.
- Timed active-match soak: 8 concurrent matches 208/208 cycles PASS; 16 concurrent matches 480/480 cycles PASS; total 688/688, 0 errors and no idle matches in that local run.
- Mixed load v1 revealed a **test harness bug**: synthetic discovery heartbeats expired during the run; 4 discovery workflow failures and no 32-match phase. This was not accepted as backend failure or PASS.
- Mixed load v2 refreshed 100 synthetic room heartbeat projections every 8 seconds. With four concurrent browser workers: 16-match stage 184/184 cycles plus 91/91 discovery workflows; 32-match stage 677/677 cycles plus 98/98 discovery workflows. Combined 861/861 match cycles and 189/189 discovery workflows PASS. Zero cleanup warnings.
- A/B latency diagnostic: all 32 match pairs prewarmed. Three 45-second stages: 16 synchronous 288/288 cycles, 32 synchronous 507/507 cycles, 32 staggered by 75 ms 576/576 cycles; 1,371/1,371 cycles and 216/216 discovery workflows successful, with 32 replay plus 64 stale-revision guard probes accepted. Local cleanup: 0 warnings.
- A/B observed complete-cycle P95: 16 synchronous 1,354 ms; 32 synchronous 1,438 ms; 32 staggered 253 ms. Some synchronous-stage tail latency exceeded 19 seconds; staggered-stage P95 was approximately 253 ms. This indicates burst sensitivity in the **local emulator** and may also reflect warmup/order effects. It is not proof of a production pacing fix or production capacity.

Accepted tooling/evidence (local scripts and generated reports are QA artifacts):

- `Backend/Firebase/functions/scripts/qa.mjs` and the focused public-lobby E2E suite.
- `phase16i-active-match-load.mjs`, `phase16i-active-match-soak.mjs`, `phase16i-mixed-stress.mjs`, `phase16i-latency-diagnostic.mjs` local harnesses.
- Representative output JSON: `AtlasBoard_Phase16I_ActiveMatchSoak_mv4i7m57d0bdef27.json`, `AtlasBoard_Phase16I_MixedStress_mv4j5jg3298f3f11.json`, `AtlasBoard_Phase16I_LatencyDiagnostic_mv4jve2k501ed80b.json`.

**Open Phase 16I external gates:** Firebase billing/quota/read/write cost measurements, production-like deployed capacity, physical Windows clients, multi-region latency, release build behavior. No assertion is made that 32 concurrent emulator matches equal a certified player limit. Full production acceptance is deferred to Phase 17.

# 7. Phase 17 — External / Live Testing

**Status: PAUSED BY OWNER AFTER 17A/17B LOCAL STEAM SMOKE PASS. Phase 17C and later external/production tests resume AFTER Security & Core Audit and pre-release features (and after official Steamworks AppID is available).**

## 17A — Steam development client readiness and Unity runtime

**Status: DEVELOPMENT STEAM RUNTIME SMOKE PASS; PUBLISHER VERIFICATION PENDING.**

- Read-only Windows preflight found Steam desktop running, Steamworks.NET present, Unity platform/auth source present, and development AppID 480 configured; standalone build was not yet discovered at the initial preflight.
- In Unity Editor Play Mode: Steam provider active (`steam`), Steam initialized, Steam AppID 480, Steam identity/persona nonempty, and Phase 11B static Steam validation PASS 8/8.
- The Steamworks client/overlay smoke on AppID 480 is explicitly **not** real AtlasBoard Steam publisher authentication and does not prove a deployed App Check token or publisher-ticket verification.

## 17B — Windows Development Guest / Steam invite overlay

**Status: 17B.2 LOCAL OVERLAY OPEN PASS; REAL FRIEND INVITE/JOIN PENDING.**

- Standard local Guest QA is still intentionally isolated from unnecessary native Firebase Auth/Firestore and automatic Steam initialization. In `AtlasBoardPlatformRuntime.ShouldUseSteamProvider()`, explicit loopback Development Guests use the development identity provider; the ordinary Steam-gated invite button therefore cannot be expected to show in this mode.
- Phase 17B added a separate **opt-in Development Windows Guest QA component** `Assets/Project/Scripts/Platform/AtlasBoardSteamGuestOverlayQa.cs`. Only explicit `-atlasLocalEmulators` on loopback **and** `-atlasSteamOverlayQa` activate it. This does not change the production identity or App Check implementation.
- First QA run proved the button was created and clicked, but `SteamAPI.Init()` failed. The later preflight proved `steam_appid.txt` was **missing beside the Guest EXE**. A development-only local file containing `480` was then created; no Steam secret was involved.
- After rebuilding, preflight and manual correction, the accepted 2026-10-11 20:07 America/Denver Guest log showed:
  - `ATB_BOOTSTRAP event=startup_ready` and a real local-emulator private Waiting lobby;
  - `ATB_PHASE17_STEAM_OVERLAY_QA_V2 event=BUTTON_VISIBILITY visible=True`;
  - `CLICK_RECEIVED` -> `STEAM_INIT_BEGIN` -> `STEAM_INIT_OK appid=480 overlay_enabled=True`;
  - `OVERLAY_CALLBACK_REGISTERED` -> `INVITE_API_BEGIN` -> `INVITE_API_RETURNED` -> `OVERLAY_ACTIVATED`;
  - overlay closing callback and `SESSION_CLEAN_EXIT droppedMessages=0`.
- The user screenshot confirms the Steam Overlay opens directly to an **`INVITE ANYONE TO PLAY`** panel with **Spacewar** branding (AppID 480). The normal AtlasBoard production invitation/Steam friends join flow was **not** proved. The absent full direct-invite menu for Spacewar is a development AppID limitation/Steam client context; a real app AppID and publisher setup are necessary but not by themselves sufficient for acceptance.
- V2 QA component diagnostics log button creation/visibility/clicks, environment/AppID paths, Steam process, Steam API init, invite API and Overlay activated/closed callbacks, with the **QA component's** raw room connect payload redacted; repeated failed native init is blocked for that run. **Privacy watch:** a separate, older private-room creation logger still prints the six-digit room code into the overall Guest session log; redact that inherited logger before any public telemetry/log sharing.
- The QA-only Steam native init still carries historical Windows native-crash regression risk. Future production implementation must use the real platform adapter, not promote this QA component or AppID 480 as a release entitlement workaround.
- **Expected Development Guest Steam launch options:**
  `-atlasLocalEmulators -atlasSteamOverlayQa -atlasDevName="Colorado Player" -screen-width 1280 -screen-height 720 -screen-fullscreen 0`
- **Dev-only AppID file:** `steam_appid.txt` alongside the Guest EXE with value `480` (local only, do not commit).
- **Historical one-off preflight:** `Tools/Phase17B/SteamGuestPreflight.ps1` is already committed in bed153c2. Keep only if useful for reproducible developer onboarding; new work must NOT introduce additional `Tools` files, and a future authorized cleanup may remove this helper without touching the accepted Steam QA runtime.

## Remaining Phase 17 acceptance gates

1. Obtain the actual AtlasBoard Steam AppID; configure Steamworks title/app permissions/builds and real publisher credentials. Recheck native Steam initialization and actual friend invitations through Steamworks, including friend acceptance, start/join payload handling and canceled/invalid invites.
2. Validate real Steam ticket server-side with Valve; verified account mapping and returning sign-in must keep canonical Atlas AccountId stable. Never treat a SteamID sent from the client as verified proof.
3. Prove custom Steam App Check minting and deployed enforcement against an explicitly approved staging/production project; valid token positive control plus cryptographically invalid/missing token negative controls. No production deployment or enforcement changes without explicit authorization.
4. Real two-, three- and four-player Unity matches using separate physical PCs and real Internet; examine invite, reconnect, host loss/migration, game-state consistency and match outcome. Local `127.0.0.1` emulators cannot connect separate geographic PCs.
5. Multiple regions/providers where feasible, cold/warm launch, latency and jitter/loss observations, chat UTC timestamps, retry/timeout handling and real deployed quota/cost studies.
6. Windows Release topology gate: prove dev-only flags do not work in Release, no native Firebase/Steam regression, clean build/startup/shutdown, crash diagnostics, signed-off release/security/operations controls.

**Do not mark Phase 17 or production security CLOSED until these gates pass.**

# 8. Optional backlog — not committed scope

Possible later work:

- Friend/party system.
- Quick Play / matchmaking.
- Casual/ranked split.
- Skill rating.
- Spectators.
- Replay/history expansion.
- Quick-chat/emotes/pings.
- Voice only if moderation burden is justified.
- Seasonal profile cosmetics.
- Additional missions.
- Alternate rules / tournament modes.
- Steam Workshop/custom maps.
- Colorblind palettes and additional accessibility.
- Privacy-safe crash telemetry/analytics.
- Feature flags / staged rollout.
- Backend health dashboard.
- Moderation dashboard.

These remain optional. Owner has explicitly put pre-release feature planning/implementation **after the current Security & Core Audit** and **before returning to Phase 17C**. Release-blocking security checks remain mandatory; never treat an optional feature as a security waiver.

# 9. OWNER-APPROVED EXECUTION ORDER — UPDATED v5.17

1. **Phase 14 and prior accepted phases:** preserve as implemented/accepted with documented regressions watched.
2. **Phase 15A–15K:** local/emulator hardening accepted; deployed App Check and real Steam publisher proof still blocked.
3. **Phase 16A–16I:** local/backend/Unity/development test checkpoints accepted; no production capacity claims.
4. **Phase 17A and 17B.2:** Unity Steam AppID 480 and Development Guest invite-overlay smoke accepted; no actual Steam friend invite/remote join proof.
5. **NOW — Security & Core Audit, PASS 1:** Security Core 01/02/03 owner Windows emulator QA accepted; continue untested callable authorization, privacy, rate limits, pagination abuse, Unity gameplay and server-verifiable match event design. The current interim gate must not be mistaken for a permanent trusted result engine. No production or Steamworks changes until explicitly approved.
6. **NEXT — Pre-release Features + UX/Gameplay Polish:** choose scope from Section 16, implement accepted additions with regression tests. Decide Android vs Windows-only and crossplay separately; do not silently add every wishlist item.
7. **THEN — Phase 17C and Phase 17D+:** obtain official Steamworks AppID and approved shared staging, validate real Steam tickets/App Check, 2/3/4 real PCs, invites, reconnect/host loss, cold/warm cross-region, Release topology and real-world quota/cost.
8. **THEN — Security & Core Audit, FINAL PASS:** repeat critical suites against feature-frozen EXACT release candidate and staging; review all P0/P1 findings and dependencies; require human evidence/approval.
9. **LAST — Release candidate, Steam launch preparation and release owner GO/NO-GO.**

Dependencies: Step 5 can start immediately without a real Steamworks AppID. Production cryptographic close in Steps 7/8 **cannot** be labeled PASS until official AppID, publisher verification and approved staging exist. Never weaken production security to bypass this dependency.

# 10. Immediate next action

**ACTIVE: Security & Core Audit — Batch B / Security Core 04 (after accepted Core 01–03 local emulator QA).** The pushed-baseline source inventory covered 21 backend TypeScript files and 55 callable endpoints; newer local guarded exports are now covered by the accepted local suite, but production inventory and Unity proof are still outstanding. Maintain comprehensive security review, not an all-project PASS certificate. Prioritize the remaining tracks:

1. **Authentication / permissions / abuse:** Firestore Rules, callable authorization by account and object, emulator-only helper guards, lobby join throttles, pagination cursor tamper/flood cases, denied access cross-accounts.
2. **Host authority / match results / progression:** trace host `matchHostPublishState` -> completion frame -> `progressionRecordCompletedMatch` -> XP/achievements; design controlled negative cases for forged match completion/outcome/turn sequence; avoid assuming host-hosted casual play is cheat-proof.
3. **Economy:** wallet/ledger and inventory idempotency; duplicate purchase/refund/promo/daily claims; concurrency and overspend; host-progression abuse must not grant tradable or money-backed benefits.
4. **Core Unity gameplay:** deterministic dice/turn order, trade/auction/rent/bankruptcy/development, bot actions, temporary bot and reconnection/host changes, event-sequence handling, snapshot convergence, unclean shutdown recovery. Check complete game scenarios with 2/3/4 human seats later.
5. **Operational code QA:** TypeScript lint/build, `node scripts/qa.mjs --check`, then controlled `node scripts/qa.mjs --full` when **no other emulators are running**; Unity compile, metadata, actual Editor/Guest play tests and crash logs. Capture logs/errors. The owner DID execute fresh local `node scripts\qa.mjs --full` with passing SEC01/02/03 results on Windows, per the 2026-10-13 transcript; Unity Editor/Release and deployed stages remain unverified.

**NEXT AFTER AUDIT:** feature selection and implementation under Section 16. **DEFERRED:** Phase 17C Steamworks publisher/real multi-PC work under Sections 7 and 14, until owner resumes. No need to obtain Steamworks AppID to start current source/core audit.

Do not deploy, update GitHub, force cloud App Check enforcement or provision services without explicit owner permission. Deliver only complete actual replacement source files when a code fix is approved; no new Tools/patch helpers.

# 11. Current checkpoint summary

## Green / accepted locally

- Phase 16A–H.4 local QA and runtime reliability baselines, subject to crash regression watch.
- Phase 16I paginated public discovery and local emulator capacity/read/mixed load, including 32 concurrent match pairs and a 75 ms staggering A/B diagnostic.
- Phase 17A Unity Steam provider initialization and Phase 11B static check 8/8 using AppID 480.
- Phase 17B.2: Windows Development Guest local emulator identity, private lobby, QA-only SteamAPI Init AppID 480 and Steam invite Overlay activation.
- Phase 17B.2: event-level Steam QA component diagnostics with its own invite payload redacted, and clean process exit; inherited lobby creation logger still exposes room code in the session log and must be hardened before public telemetry.

## Yellow / current watch and active work

- **ACTIVE NOW: Security & Core Audit**; Security Core 01/02/03 local emulator fixes and negative tests accepted, but broader auth/abuse, Unity core gameplay and server-verified progression remain OPEN.
- Actual Steam platform join/invite and multi-PC network acceptance are deferred to Phase 17C.
- Historical `BEX64 / 0xC0000409` native Windows Guest crash remains a regression watch; native Steam was re-enabled only on explicit QA user click in opt-in Development Guest.
- Private-lobby room codes appear in local DEVELOPMENT diagnostics. Owner explicitly accepts temporary local logging. Risk: LOW in private short-lived logs, conditional on access, expiry and server-side join controls; reclassify if logs are publicly uploaded or persistent telemetry captures live join codes. No urgent code change requested; verify no production/public log forwarding.
- Emulator burst-tail latency and measured local workload cannot be extrapolated into a production capacity claim.
- Phase 17C real AppID / physical-PC / shared staging requirements are **deferred by owner** until Security & Core Audit and feature work are finished; preserve these release gates. The current owner local Full QA outcome is PASS only for emulators, not production.

## Deferred production gates

- Real AtlasBoard Steam AppID and Steamworks partner setup; published identity/invites/achievements.
- Publisher Web API secret, real Valve Steam-ticket attestation and returning Steam sign-in.
- Correct Firebase App ID and real custom Steam App Check token mint/enforcement/cryptographic rejection.
- Production/staging separation, IAM, logging/alerts, TTL and billing/quota governance.
- Real physical 2/3/4-player gameplay, reconnect, host loss and region/performance measurements.
- Release compilation, startup/shutdown security regressions and confirmation QA-only native bypasses are excluded.
- Release owner approval.

# 12. Next-chat handoff block

Future AtlasBoard chats should start from these facts:

- Canonical roadmap checkpoint: **v5.17, 2026-10-12 America/Denver / 2026-10-13 UTC Windows QA log**, owner-local document to be committed by owner; treat v5.16.1 and earlier status paragraphs as historical when superseded by Section 23.
- Repository: `vuartex/AtlasBoardPrototype`, Unity baseline 6000.5.6f1, scene `Assets/Board_Prototype.unity`.
- Last **already pushed** `main` at latest read: `bed153c2071716e1d7216fe0e3e8ecdb36f3f341`, `Accept Phase 16I local QA and Phase 17B Steam invite overlay validation`. Do not fabricate a future commit SHA.
- Phase 16I and Phase 17B.2 accepted source ARE pushed in bed153c2; all newer Security Core 01/02/03 fixes and tests and this roadmap remain owner-local until the user commits/pushes. Inspect `git status` and exact source before replacing anything; never assume a push already happened.
- Phase 16I cursor pagination and local emulator mixed load PASS; 32 concurrent matches observed with no workflow failures in the accepted v2/A-B tests. Local result is not certified production capacity.
- Phase 17A Editor: Steam initialized with AppID 480 and Phase 11B 8/8. Not real publisher verification.
- Phase 17B.2 Guest on local emulators: opt-in `-atlasSteamOverlayQa` enables a separate QA invite control; `SteamAPI.Init`, `ActivateGameOverlayInviteDialogConnectString` and Overlay activation callback were observed PASS after adding local `steam_appid.txt=480` beside the EXE.
- User-provided 2026-10-11 20:07 America/Denver Guest log demonstrated `STEAM_INIT_OK`, `INVITE_API_RETURNED`, `OVERLAY_ACTIVATED`, `SESSION_CLEAN_EXIT droppedMessages=0` with no credential/ticket exposure.
- Screenshot: Steam opened invite UI with Spacewar branding. The real Steam friend invite/accept/join is NOT tested or accepted. Do not claim the actual real AppID alone automatically resolves all invitation behavior.
- Keep `.local` tokens, app publisher secret and raw tickets out of source/logs; QA component itself redacts invite codes, but an existing lobby creation diagnostic still logs the raw room code. The owner explicitly accepts these temporary codes in local development logs; never publish live-room logs or ship them to remote telemetry without sanitization.
- Production Steam custom App Check and returning sign-in gates remain blocked until real AppID/Valve/deployed proof.
- Historical native Guest crash must remain on regression watch; explicit opt-in Steam Native Init is confined to the development-only test path.
- Owner-approved next work: **Security & Core Audit (Core 01/02/03 locally accepted, Core 04 next) -> pre-release features -> Phase 17C real physical Steam clients / official AppID / approved staging -> final release audit and GO/NO-GO**. Do not connect remote clients to localhost emulator.
- Existing Phase 16I/17B files are accepted and pushed at bed153c2; never overwrite with earlier source or unapproved QA helpers.
- Git discipline: stage only desired paths, no `git add .`, user performs commit/push. Never auto-push/deploy.
- Deliver only complete affected source files when genuinely necessary. For roadmap maintenance deliver only the full `MASTER_ROADMAP.md`; no new `Tools` scripts, patches or installers. Review unrelated font/generated binary changes before future staging.

# 13. Roadmap maintenance rule

At every meaningful checkpoint:

1. Record the latest already-existing pushed SHA/title; never invent a SHA for a commit that does not yet exist.
2. Preserve accepted local work until it is safely pushed.
3. Update the current blocker and acceptance evidence.
4. Separate local/emulator acceptance from production acceptance.
5. Record QA-only bypasses and the release gate proving they cannot leak into Release.
6. Remove obsolete future-scope wording when work is completed.
7. Keep `Assets/Project/Documentation/MASTER_ROADMAP.md` as the canonical roadmap.
8. Keep player-facing ATB error codes stable once published/documented.

# 14. Phase 17C — Real Steam / cross-device test design (OPEN, no deployment)

**Status: FORMALLY DEFERRED BY OWNER until completion of Security & Core Audit, pre-release feature development, and availability of an official Steamworks AppID.** Phase 17B.2 proves that Steamworks API initialized and the Spacewar AppID 480 overlay opened on one Windows Development Guest. It does NOT prove delivery to a friend, approval of an official Steam AppID, cross-PC network reachability or Firebase security in a deployed build.

## 14.1 Prerequisites and architecture decisions

1. Verify whether a real AtlasBoard Steamworks AppID, partner permissions, build branches, Steam publisher Web API secret and Steam-account licensing are available. Keep actual values out of public roadmap and logs. AppID 480 is never production proof.
2. Inventory test devices and accounts: two, then three and four separate authenticated humans using physically distinct clients (and at least two independent networks when feasible). Same-PC Guest Editor/emulator combinations remain regression tests, not external proof.
3. Design dedicated Firebase staging (separate project from `atlasboard-usa`) with explicitly approved owner/IAM/billing quotas/alerts, client config, secrets, App Check registration, deployed Functions/Firestore indexes and rollback procedures. Do not deploy or modify account settings during documentation.
4. Preserve canonical Atlas AccountId and server-side lobby/match persistence. Steam is identity/invite/presence/achievement adapter, not automatically AtlasBoard's dedicated matchmaking/game server. Do not introduce a self-managed VPS/dedicated server merely to finish Phase 17.
5. Confirm the peer/host-authoritative topology and non-loopback Firebase cloud endpoints for every test machine; `127.0.0.1` always resolves to the local machine. Investigate NAT/relay needs separately only if selected transport actually requires them.
6. Decide Steam-only PC launch versus eventual Android/Google Play. Android may use Firebase with a platform-appropriate App Check provider. Cross-platform multiplayer is **OPTIONAL / UNDECIDED**; separate platform queues are acceptable. No premature commitment to cross-play or paid hosting.

## 14.2 Real-user acceptance scenarios

- Steam start / quit / restart, account creation, returning sign-in, identity stability, invite overlay, friend selected, invite accepted/rejected/expired, private/public lobby joining, wrong code/password, revoked access, full/closed/stale lobby.
- Full 2/3/4-human match from lobby ready through authoritative turns, movement, development/trade/auction, settlement, chat, endgame, progression, rematch; verify deterministic seat ownership and same visible state after each move.
- Host closes app, network loss, disconnect/reconnect, temporary-bot handoff, host election/migration, duplicate/stale intent replay, conflicting revisions and idempotent finalization; no duplicate wallet or progression rewards.
- Multiple US/EU networks, cold vs warm runs, last-mile Wi-Fi/mobile hotspot variation, latency and errors, successful/failed requests, bandwidth and costs; report environment and test sample size, not invented global capacity.
- Collect redacted results with software version, test setup, successful/failed assertions and repeatability; define explicit pass/fail criteria before promoting the release target.

**Gate:** any real cloud test requires explicit approved staging target. No production App Check bypass, fake Valve verification, development SteamID trust, or insecure port forwarding to private emulators.

# 15. Mandatory final whole-project security, code and QA audit — PRE-RELEASE GATE

**Status: SECURITY & CORE PASS 1 ACTIVE; SECURITY CORE 01/02/03 LOCAL QA ACCEPTED (owner transcript, 2026-10-13 UTC), FULL PROJECT / RELEASE AUDIT STILL OPEN.** This phase began before feature work and before official Steam AppID issuance. Pushed baseline GitHub `bed153c2` had an inventory of **21 backend TS files, 55 exported callable endpoints** and deny-by-default Firestore rules. The owner subsequently ran the newer local combined suite: 14 progression controls, SEC01 host-result negative test, SEC02 19 economy controls, 11 inventory, 13 promo and SEC03 15 reward controls, with full emulator QA PASS. Selected Unity match/lobby/platform files were inspected, not all Unity assets or gameplay scripts. **A standalone independent gameplay-result verifier, deployed App Check negative proof, full project penetration/dependency audit, Unity Release build and real Steam/physical multiplayer tests have not been performed.** Repeat release-critical checks **after feature freeze** and again on the exact release candidate.

## 15.1 Read-only source-review observations (2026-10-12)

| Ref | Severity / state | Observed evidence | Required proof or disposition |
| --- | --- | --- | --- |
| SEC-01 | **P0 RELEASE BLOCKER** | `Backend/Firebase/functions/src/security/app-check-policy.ts` currently defines `ATLAS_ENFORCE_APP_CHECK` as `isFunctionsEmulator && localEnforcementRequested`. Thus outside emulator protected callables are configured **without enforced App Check**. This is an accepted development staging choice, not production readiness. | Implement explicit owner-approved staging/production policy that fails closed without blocking the legitimate Steam token bootstrap; test valid, missing, forged, revoked and expired tokens against deployed staging. Include emergency rollback. Do not flip an emulator-only flag and assume deploy safety. |
| SEC-02 | **P0 RELEASE BLOCKER** | Steam AppID 480/Spacewar QA does not prove Valve publisher-ticket authenticity, real Steamworks invite delivery, canonical returning sign-in or correct Firebase App Check App ID. Staging / real AppID and publisher-secret setup are pending. | Real-AppID server-side Valve validation, identity mismatch/replay tests, approved secret management, correct App Check app registration and end-to-end Steam account mapping; never trust client-supplied SteamID alone. |
| SEC-03 | **P1 / anti-cheat threat model** | `src/match/network.ts:publishMatchNetworkState` restricts publication to the authenticated current host and uses revision/sequence/seat constraints, but accepts a bounded `snapshotJson` supplied by the host rather than independently simulating all rules server-side. `src/progression/progression.ts` explicitly excludes host telemetry from future ranked authority until backend event-sourcing. | Keep game sessions casual/friend-authoritative at launch unless business/design accepts host-cheat risk. Before ranked, leaderboards with valuable stakes, tradable rewards or competitive payouts, add server-verifiable actions/outcomes and abuse tests. Review normal progression/reward integrity as well. |
| SEC-04 | **P1 / availability & billing review** | `platformSteamCreateAppCheckToken` is intentionally unauthenticated and App Check-exempt to bootstrap Steam proof; current code binds a publisher secret and verifies with Valve, and uses `maxInstances:20`. In this focused read, no dedicated edge pre-auth IP/risk limiter was confirmed. | Threat-model flooding, rate/quota exhaustion and Valve API cost; configure gateway/platform anti-abuse, monitor budgets and enforce safe timeouts without breaking legitimate initial sign-in. Do not call it a proven exploit without testing. |
| SEC-05 | **P1 / rules & privacy review** | `firestore.rules` is deny-by-default for private authority collections; authenticated clients may query `public_profiles`, while `preferences` validates top-level map types but not nested shapes/size limits. | Check profile enumeration policy, data minimization, nested preference schema/limits and security-rules abuse/fuzz tests on staging. Treat this as an audit item, not established privilege escalation. |
| SEC-06 | **P2 / local log privacy accepted** | Local Development Guest lobby creation diagnostic includes a six-digit temporary join code. Host-close/expiry, authenticated joins and invalid-attempt controls limit risk. Owner explicitly permits this in local logs. | No urgent change. Prevent public/player support telemetry from forwarding usable live lobby codes, tokens, private IDs or detailed backend responses; revisit severity if logs become accessible while a room remains open. |
| SEC-07 | **P1 / Release isolation gate** | QA-only native Steam init is guarded by `DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN && !UNITY_EDITOR`, emulator loopback and opt-in `-atlasSteamOverlayQa`. Native Firebase emulator Guest isolation has separate compile/argument gates. Historical `0xC0000409` startup crash remains on regression watch. | Compile a genuine Release; confirm test flags do not activate QA paths, local fake App Check never authenticates remotely, native Firebase/Steam initialization and clean exit work repeatedly. |
| SEC-08 | **P2 / source hygiene** | Commit bed153c2 includes one-off `Tools/Phase17B/SteamGuestPreflight.ps1`, five specialized Phase 16I stress harnesses and modified font assets. This is developer-source footprint, not automatically added to Unity player binary. | Do not add more Tools/patch helpers; reuse existing canonical QA diagnostics. Review font diffs for accidental changes. Archive/consolidate one-off scripts only after preserving accepted QA coverage and only with owner approval. |
| SEC-09 | **P1 / pagination abuse** | `src/lobby/public-lobby-pagination.ts` bounds query chunks and scan budget, validates heartbeat and signs pagination cursors. Runtime public calls are authenticated. | Add invalid/tampered/expired cursor tests, high-churn pagination, abusive authenticated fetch rates, query-index and database-read billing review in staging. |
| SEC-10 | **P1 / Dev-only callable exposure audit** | `index.ts` and Steam platform expose development mutation/identity helpers that use emulator-only guards; Firestore rules deny authority writes from ordinary clients. | Enumerate every deployed callable, verify dev helpers fail closed outside emulator, inspect `onCall` auth/rate limit/roles and object-level authorization, and run negative authorization/abuse suites against safe staging. |
| SEC-11 | **P1 CONFIRMED — interim mitigation LOCAL QA PASS, Release BLOCKED pending real verifier** | Local hosted negative E2E demonstrated that a legitimate host can publish fabricated `match_complete` at revision 0 with zero turns. `progressionRecordCompletedMatch` then accepted the frame and minted 260 XP, a win, `first_win` and a progression marker. The evidence is from isolated Firebase emulators only (2026-10-12). | v5.14 fail-closed gate candidate: zero-turn completion and post-completion host rewrites rejected; `progressionRecordCompletedMatch` writes no permanent XP/wins/achievements without a digest-matched, privileged `match_result_verifications/<matchId>` receipt. All unverified results remain pending; no real independent verifier exists. Integrated 14/14 progression + host negative tests and full local QA PASSED (owner 2026-10-13 receipt). Implement/review a trustworthy server-side event/result verifier before enabling normal permanent rewards. No claim of full anti-cheat closure until replay validation, race/TOCTOU analysis and actual real-world tests. |
| SEC-12 | **P2 / automated quality gates** | Backend `scripts/qa.mjs` provides offline/full suites, and owner newer-local fresh `--full` transcript PASSED on 2026-10-13 UTC; branch CI enforcement and a pushed-checkpoint CI run were NOT verified. | Run and capture current lint/build, `qa.mjs --check/--full`, Unity Editor compilation / build, and verify CI is required for protected branches before release. No claim of CI absence until scope is explicitly verified. |

**Review snapshot:** initial triage plus accepted owner-local Security Core 01/02/03 negative emulator suites is complete enough to prioritize remaining audit work but **not** to close the whole audit. Host-published results affecting progression (SEC-03/SEC-11) are the first code/gameplay integrity investigation. SEC-01 and SEC-02 remain unavoidable production gates requiring future Steam/App Check infrastructure. No completed whole-project audit certificate exists.

## 15.2 Mandatory code and application audit matrix

Every section below needs either an executed automated test, a documented manual device test, or a reviewed written risk acceptance with evidence. **Current Audit Pass 1 is underway BEFORE new features**; after features are complete, repeat the critical security/core suites on the exact build to be published.

- **Identity/session:** Auth account registration and recovery, provider mapping, Steam ticket authenticity/replay, spoofed UID/SteamID, expired credentials, session invalidation, deleted/banned users, account switching, linked-account conflicts.
- **Firebase backend:** enumerate all exported callables and indexes, Firestore Rules least privilege, server checks independent from client-side UI, transaction concurrency, App Check positive/negative, raw HTTP and unauthorized endpoints, rate limits, input validation, pagination cursor integrity and SSRF/secret boundaries.
- **Money/store/entitlements:** duplicate purchases/refunds/claims, concurrency/retries, signed ledger, negative balances, promo redemption abuse, reward replay, season rollover, unfinished match rewards, economic inflation/exploit loops and real-money/IAP compliance if introduced.
- **Online gameplay:** client/host snapshot manipulation, command idempotency, turn order, dice and trade/auction logic, bankruptcy, property prices and payouts, disconnect/reconnect/host loss, state-hash comparison and recovery after app termination, 2/3/4 participants.
- **Chat/moderation/privacy:** flood and malicious payloads, profanity/report/block flow, permissions, input rendering, history retention/TTL, public profiles, data deletion/export, privacy policy, user reporting and escalation.
- **Steam/PC:** official AppID, overlay/invite accept+reject, achievements 14 mappings, Steam restart/offline behavior, startup flags, Windows builds/signing, Steamworks callbacks and SDK versions, no issuer secrets or ticket dumps in logs.
- **Android/Google Play if approved:** Android identity & App Check/Play Integrity provider, SDK/API level, permissions, CPU/GPU/device memory, phone/tablet aspect ratios, touchscreen, store policies, data privacy and purchase receipt validation, platform-isolated/cross-play tests per decision. NOT an automatic Steam release prerequisite if Android scope is deferred.
- **Unity runtime, UI and content:** scene references, null/lifecycle races, asset/meta GUID integrity, memory/GC leaks, performance/frame stalls, device resolutions, input system and controller, pause/back navigation, UI safe areas, camera, city information overlay input, localization for 7 languages, accessibility, audio, tutorial and game rule clarity.
- **Quality/observability:** TypeScript lint/build, current `qa.mjs --check/--full` with test receipts, Unity edit/playmode tests and actual Windows Release build, fuzz/negative tests, crash reproduction, CI workflow and dependency/license/SBOM review; never substitute source regex audits for runtime tests.
- **Operations/release:** separate staging/production projects, least-privilege IAM, managed secrets and rotation, billing alarms, Firestore TTL indexes, retention/backups, alerting, incident and rollback runbook, CDN/assets patching, Steam beta branch, marketing/age/privacy/store legal checks and explicit owner GO/NO-GO decision.

## 15.3 Audit completion evidence template

For each SEC issue: record owner, category, reproduction steps, code path, severity, mitigation, regression test, device/backend configuration, evidence link, remaining risk, and close date. Block production on outstanding P0s, unapproved P1s, an unverified Release build or any failed critical multi-player/identity/economy test. No self-issued "secure" certificate.

# 16. Pre-release features and refinements — OWNER-APPROVED NEXT PHASE AFTER AUDIT

Owner explicitly requested these potential additions be remembered **before** release. Feature planning and development follow the current Security & Core Audit and precede resuming Phase 17C. Presence of a related file does NOT mean UX/reliability acceptance; discuss MVP/optional scope first, and retain the ability to freeze features for final security regression.

## 16.1 Existing features needing final polish / verification

| Work item | Current grounded state | Proposed pre-release acceptance | Priority |
| --- | --- | --- | --- |
| City information on tile/city click | EXISTING `AtlasBoardCityInformationPanel.cs` and tablet/world-input guard; not a missing feature. | Verify city/property ownership, rent/property prices, improvements and rule data match selected map; correct tap/click, scrolling, safe area and seven-language labels without leaking clicks through tablet/modal. | P1 UX |
| Network quality HUD | EXISTING `AtlasBoardNetworkTelemetry.cs` + `AtlasBoardNetworkQualityHud.cs`; current widget is inside PauseWindow and computes smoothed callable RTT, RTT variation and an application-request failure/late proxy. | Consider small nonintrusive corner indicators for ping/jitter/failure quality during matches, localized tooltip and show/hide option. **Do not claim measured UDP packet loss** from HTTPS callable failures; label explicitly as request failure/quality unless real packet transport metrics exist. | P1 UX if owner accepts |
| Startup/error experience | Existing loading tips (7 languages), ATB codes and diagnostics accepted in Phase 16H. | Revalidate cold start, return login, retry/offline, language switching and stable nontechnical player messages on production-like paths. | P0 regression |
| Lobby/public browser & private invitation | Phase 16I local paging and Phase 17B Spacewar UI smoke accepted. | Private/public join/rejoin, invite deep link, expected failures/host-close, pagination under churn, clear player feedback, Steam overlay with official AppID. | P0 regression |
| 2–4-player turn-based gameplay | Core gameplay/host-authoritative transport accepted locally. | Real human sessions and full-game finalization with deterministic state/reward checks; bots/temporary bots, turn timeout, trading/auction/bankruptcy and rematch. | P0 regression |
| Achievements/career/store | Existing 14 Steam achievement mappings and Firebase profile, inventory, seasonal/economy systems. | Real Steam AppID API names, unlock parity, cosmetic entitlement sync, wallet abuse/duplicate award protection, offline/read-only fallback, game completion receipts. | P0/P1 |

## 16.2 Proposed launch-quality features requiring priority decision

- **P1 candidate:** concise in-game new-player tutorial / first-turn hints, controls/settings discoverability, clear error-recovery actions and friend/lobby join guidance.
- **P1 candidate:** game balance and bot difficulty/decision quality; playtest edge cases on available maps, human-vs-bot experience and minimum viable content completeness.
- **P1 candidate:** accessible UI (colorblind patterns beyond color only, keyboard/controller options, text legibility, low-end GPU settings) across supported Windows aspect ratios; verify existing partial accessibility before rebuilding anything.
- **P1 candidate:** clearer network indicators, reconnect countdown/status, host-change notifications and consistent localized fallback when service is unreachable.
- **P1 candidate:** privacy-safe opt-in diagnostic upload / support workflow with removal of credentials, room codes and user identifiers before sharing externally. Local Development logs may retain temporary lobby codes per owner's explicit decision.
- **P2 optional:** friend/party list; Quick Play/automatic matchmaking; party invites; casual/ranked split **only after security design**; skill rating and leaderboards; spectator/replay; quick-chat/emotes; tournament / alternate rule sets; additional content, cosmetics and missions.
- **P2 optional:** Android/Google Play build and device UX, with Android-specific provider and store verification. Cross-platform Windows/Android gameplay is **undecided**, and separate populations are valid. Firebase is preferred managed backend; no self-managed always-on server by default.
- **P3 future:** voice chat only with moderation capacity, Steam Workshop/custom maps only after content moderation/IP review, seasonal live-ops dashboards and richer analytics.

## 16.3 Feature intake and release scope lock

New ideas should enter this Section 16 with user intent, planned UX, dependencies, MVP vs optional label, priority, cost/hosting impact, security/privacy concerns, platform matrix and acceptance criteria. Do not implement without owner approval. Establish a **feature freeze** before the full audit of Section 15; retest modified areas and rerun final release gates afterward.

# 17. Final Release readiness / GO-NO-GO — AFTER AUDIT, FEATURES, PHASE 17C

A Release candidate requires all of the following: confirmed official Steam AppID/publisher ownership; separated approved staging; deployed Steam Auth/App Check proofs; P0 security closure and approved P1 risks; physical 2/3/4-player sessions; complete progression/economy, negative input and reconnect/host-loss testing; production-like latency/billing/alerts; real Windows Release packaging and crash-free repeated launches; seven-language/accessibility and legal/store acceptance; final source+dependency audit receipts; signed-off rollback plan; owner authorization for build and deployment.

**Current state: NO-GO / NOT READY for Steam public launch.** Phase 16I local backend PASS and Phase 17B.2 AppID 480 overlay PASS are retained without exaggeration. Nothing in this document deploys, commits, exposes secrets, runs tests, or authorizes a publishing operation.

# 18. Security & Core Audit — Batch A execution register (2026-10-12)

**Current audit type:** source inspection of pushed GitHub `bed153c2071716e1d7216fe0e3e8ecdb36f3f341` combined with newer owner-local full Firebase Emulator test receipts on Windows through Security Core 03 (2026-10-13 UTC). The earlier host-forged-XP exploit was confirmed locally and the interim deny gate was regression tested. **Not** a full real-game verifier, deployed security proof or independent certification. The owner has authorized audit/planning and local roadmap delivery, NOT automatic push or deploy.

| Batch | Priority | Audit unit / files | Current evidence and action | Status |
| --- | --- | --- | --- | --- |
| A-01 | P0 release | `src/security/app-check-policy.ts`, Firebase entry exports / callable policy | Production-enforcement currently false outside emulator; explicit future staging policy and positive+negative credential tests required; real Steam AppID dependency documented. | **OPEN / blocker before release** |
| A-02 | P1 CONFIRMED | Match publication / progression / Unity | Initial exploit: zero-turn fabricated host match minted 260 XP, win and `first_win`. Updated owner-local test: negative attack rejected, XP/wins/achievement remain zero; ordinary progression 14/14 PASS with local privileged receipt fixture. | **INTERIM MITIGATION LOCAL QA PASS; independent real-match verifier still BLOCKER** |
| A-03 | P1 | `src/economy/wallet.ts`, `inventory.ts`, `commerce.ts`, `promo.ts`, `seasonal.ts` | SEC02 19/19 concurrent purchase/refund tests PASS; inventory 11/11, promo 13/13, SEC03 15/15 rewards abuse tests PASS. Disabled seasonal track tier now rejected through guarded callable. | **LOCAL EMULATOR ACCEPTED; live provider/season rollover not verified** |
| A-04 | P1 NEXT | Firestore Rules, callable entrypoints, Steam platform and `src/security/callable-abuse.ts` | Firestore Rules baseline 17/17 PASS; core auth abuse 6/6 PASS, but exhaustive object-level authorization, rate/DoS and privacy coverage of all callables remains incomplete. | **SECURITY CORE 04 NEXT** |
| A-05 | P1 NEXT | `src/lobby/lobby.ts`, pagination, chat and moderation | Public discovery E2E incl. tampered cursor and concurrent reads PASS; chat/moderation 49 controls PASS. Broader lobby abuse/flood, repeated cursor fetch cost and authenticated enumeration limits remain open. | **PARTIALLY LOCAL ACCEPTED; ABUSE/QUOTA REVIEW OPEN** |
| A-06 | P1 NOW | Unity gameplay/turn, bots, trades, auctions, development, bankruptcy, reconnect | Audit state-machine and Unity lifecycle; validate move/dice/turn/game-end invariants and full-game 2–4-player scenario outcomes (physical part deferred to 17C). | **OPEN / Unity QA PENDING** |
| A-07 | P1 NOW | Windows Development vs Release, Steam QA isolation, logging/crashes | Development Steam QA branch is compile/flag-gated; owner permits local six-digit lobby codes in logs. Genuine Release compile/start-stop/hardening remains open. | **SOURCE CHECKED / RELEASE PENDING** |
| A-08 | P1 NOW | Dependencies/Unity package licenses/assets/GUIDs/configuration | Require dependency advisories, Unity package version compatibility, build outputs, asset GUID validity, crash and memory checks. | **OPEN** |
| A-09 | P1 after features | Final backend+Unity+Steam+economy/security re-audit | Re-run all impacted suites and validate feature-frozen exact release binary and staging. | **DEFERRED UNTIL FEATURE FREEZE** |
| A-10 | P0 after AppID | Real Valve ticket, deployed App Check token and negative enforcement, publisher permissions | No real Steamworks AppID currently; do not fake proof or enable production enforcement prematurely. | **BLOCKED BY OFFICIAL APPID/STAGING** |

**Accepted code-pattern observations, not exploit claims:** 21 TS backend files and 55 `onCall` endpoints enumerated; direct Firestore authority writes are deny-by-default; wallet/inventory use hashed idempotency keys and transactions; join-code lookup uses server-side HMAC; pagination uses signed cursors and bounded scans; match host publication uses monotonic revision/sequence checks; progression reads a host-authored completion frame. These are starting points for negative QA, not full audit results.

**Local QA receipt collection order (manual Windows terminal, not an added project tool):**

```powershell
cd "$env:USERPROFILE\AtlasBoardPrototype\Backend\Firebase\functions"
node scripts\qa.mjs --check
```

When this completes, ensure no emulator suite is already running, then run `node scripts\qa.mjs --full` from the same directory. Capture the full output; this suite manages its own local emulator configuration/cleanup and must not be run alongside an already-running emulator. If an interrupted QA left `.qa-env-backup.local`, use the existing `node scripts\qa.mjs --restore-env` recovery command **only after inspecting whether the run was interrupted**. No new test scripts or `Tools` directory are needed. Later run controlled source-focused tests for SEC-11 without inventing production credentials.

**Owner decisions captured:**

- No official Steamworks AppID today. Do not ask for it during Batch A or pause unrelated security/core work.
- Do not discard previously accepted local test evidence; distinguish emulator proof from deployed security.
- Local temporary room codes may remain in local development logs; do not publicly upload active-room logs or real credentials.
- No new one-off Tools/patch/helper files. Deliver only the exact complete source files changed by an approved fix, or a single complete roadmap document for roadmap changes.
- Core audit first; features second; Phase 17C third; final audit and publication last. **Preserve this order across future chats.**
- No automatic GitHub commit/push, no production Firebase deployments, and no local files overwritten in the owner's computer by this document.

# 19. SECURITY-CORE-01 — Host result integrity (v5.14 / HISTORICAL DELIVERY; acceptance in Section 23)

**Evidence:** 2026-10-12 local Firebase emulator run `progression-phase10-e2e.mjs --host-integrity-audit` returned `SECURITY_FINDING_CONFIRMED`: authenticated host created a false completion without turns; backend persisted `XP=260`, `wins=1`, `first_win=true`; test-only records cleaned. This was an actual demonstrated local exploit, not a source-only prediction. Prior standard progression `14/14 PASS`, backend `qa.mjs --full PASS`; these tests do not prove resistance to a malicious host.

**Historical package delivery snapshot (this was superseded by the owner LOCAL QA acceptance in Section 23):**

- `Backend/Firebase/functions/src/entry.ts`: exports two explicitly guarded callables **before** the wildcard export; unchanged callable names and Phase 16I pagination remain exported. This is the Firebase deployed entrypoint.
- `Backend/Firebase/functions/src/security/match-integrity-gates.ts`: rejects obvious zero-turn `match_complete`, blocks host publications after complete/proof; crucially gates permanent progression and achievements behind an **independent server-verification receipt** with exact network revision, sequence and SHA-256 digest. Unverified match result returns `ok=true`, `applied=false`, `affectedAccountCount=0`, `verificationStatus=pending_authoritative_verification` so current Unity completion flow can finish without retry storms; zero XP/wins/achievements are persisted. This is a **temporary fail-closed feature limitation**, not complete anti-cheat.
- `Backend/Firebase/functions/scripts/progression-phase10-e2e.mjs`: full replacement with a single `--security-suite` mode combining local self-test, the 14 positive tests (fixture proof created with local Admin SDK **only**) and negative tests for zero-turn completion, no-receipt fail-closed, spoofed receipt digest, direct-client forbidden receipt write and post-completion rewrite. Test-only Admin SDK fixture must never be promoted into normal deployed code.

**Critical limitations and future engineering (Release blocking):**

1. There is no real AtlasBoard match replay verifier or server-trusted gameplay event stream yet. NO legitimate live result can receive the proof automatically; permanent match-based XP/wins/achievements therefore remain pending on this candidate. This is an explicitly disclosed temporary safety lock, not a feature acceptance. Game results are still shown by Unity's local host simulation.
2. The basic minimum revision/sequence check only stops obvious early completions. A modified host can fabricate intermediate stages; do not call this proof of fair gameplay. The mandatory trust boundary is the **privileged receipt required before permanent reward writes**. Game-state validation must be independent from host snapshots.
3. The privileged verifier must generate its receipt only after actual server-side event/rule verification; atomically bind/freeze the final revision to prevent read-to-write races, receipt reuse, replay, collusion or post-proof overwrite. `matchHostPublishState` still delegates an earlier source transaction with host publication; the new wrapper's read-before-publish guard alone is not an atomic finalization lock. Include concurrent finalize/publish adversarial tests before full security sign-off.
4. The Unity UI currently displays an ordinary completion, while pending reward response has zero affected accounts. Before public beta, explicitly display 'Rewards pending verification' and design how a later trusted award is delivered. Never silently show awarded XP if not persisted.
5. Revisit any existing registered Unity progress/career statistics, matchmaking, rematch, leaderboards, economy or Steam achievements that consume these fields. No monetized/ranked progression until the independent verifier, complete negative tests, app-check production gate, real Steam AppID and staging work all pass.
6. Re-run `node functions/scripts/qa.mjs --full` after installing the candidate; the new `--security-suite` is additive and does not replace accepted QA. Static compile, lint, local emulator end-to-end tests and latest Unity Development/Release smoke are mandatory before marking PASS.
7. Observed Firebase CLI message about installed `firebase-functions` version: do a controlled npm dependency/update+lockfile audit separately; **do not auto-run `npm install --save firebase-functions@latest` in this security fix**.

**Acceptance required:** `ATLASBOARD_SECURITY_CORE_01_INTEGRATED_PASS (emulator only)` plus `AtlasBoard QA FULL LOCAL PASS`; real independent verifier remains OPEN, so SEC-11 **cannot be marked fully CLOSED** or approved for Release based on this interim lock.

**Local acceptance update (2026-10-12, v5.15):** Owner-provided full `node scripts/qa.mjs --full` transcript PASSED. It proved 14 positive progression checks, negative forged-zero-turn rejection, no XP/win/first_win, denied direct result-receipt writes, denied mismatched receipt and post-completion rewrite; `ATLASBOARD_SECURITY_CORE_01_INTEGRATED_PASS` and `AtlasBoard QA FULL LOCAL PASS` recorded. The v2 `qa.mjs` compiled gate prevents testing stale Functions `lib` artifacts. **State is temporary mitigation ACCEPTED LOCALLY, not final game-integrity verifier acceptance.** Steamworks Phase17C remains deferred by owner's instruction.

**Git discipline:** GitHub `main` still `bed153c2` at delivery inspection. v5.14 and the guarded source/test are **local only**; no commit, push, production deploy, or existing local user files were changed remotely. Owner must inspect `git status` before replacing a newer local roadmap or staging any files. The diagnostic/tools cleanup remains separate.

# 20. SECURITY-CORE-02 — Concurrent economy and transaction integrity (v5.15; acceptance in Section 23)

**Status (updated 2026-10-13 UTC):** **LOCAL ACCEPTED — 19/19 SEC02 checks plus combined Full QA PASS.** Production attestation still OPEN; Section 23 records exact evidence.

**Baseline and trust boundary:** Existing wallet and entitlement mutations use Firestore transactions with deterministic SHA-256 ledger/event IDs. Commerce reads prices from the server-owned `item_catalog`, validates currency and ownership, charges or refunds in a single transaction, and refuses production execution of the emulator-only mutation callables. The prior 15H test checks canonical catalog price, basic replay, conflict, owner-only refund, inactive item and one refund. These guarantees still require adversarial negative testing, particularly simultaneous requests. This source review alone is NOT proof that the new concurrency tests pass.

**Delivered without introducing new Tools, standalone test entrypoints or game-code changes:** One full replacement file at `Backend/Firebase/functions/scripts/economy-commerce-security-e2e.mjs` reuses the existing `qa.mjs --full` integration registration and runs an expanded sequence that checks: emulator-only preflight, authenticated identities and local Admin SDK fixtures, Firestore direct-client denial for catalog/wallet/inventory/commerce, caller-supplied price and target-UID spoofing, atomic entitlement grant and ledger, purchase idempotency and changed-payload conflict, owner-only refunds, refund ledger and entitlement revocation, distinct-key double refunds, invalid/inactive/zero-price catalog, missing Auth, insufficient funds, parallel same-key purchases, parallel different-key purchases of the same item, parallel same-key refunds and multiple alternate refund keys. Test cleanup deletes only fresh local fixture accounts and collections.

**Execution (from `Backend/Firebase/functions`, after extracting ZIP at the repository root and with emulators closed):**

`node scripts/qa.mjs --full`

The existing QA compiles TypeScript, confirms guarded callable exports, runs all prior backend suites, then invokes `economy-commerce-security-e2e.mjs` in the same fresh emulator session. Do NOT run standalone emulators or disable App Check / security rules to make failures pass. **Owner log now proves `ATLASBOARD_SECURITY_CORE_02_ECONOMY_PASS: 19/19 checks` and terminal Full Local QA PASS; SEC02 is locally accepted.** Future concrete failed invariants still require a canonical full-source correction and retest.

**Post-PASS audit continuation:** Verify the public/deployed API surface has no unintended wallet mint/refund operations; review seasonal/promo reward issuance, battle-pass inventory entitlement authority and provider receipts; separately test wallet overflow and seasonal contention. Internal `walletTestMutation`, `inventoryTestMutation`, `commerceTestPurchase`, `commerceTestRefund` and `promoTestRedeem` remain LOCAL-ONLY by design and do not establish live payment validation. Production app-check, real Steam AppID, physical multiplayer and Unity release-build QA remain UNVERIFIED and are release blockers.

**Safety and repository discipline:** Existing guarded Core01 files stay in the owner's local project. This batch changes one existing E2E source file and this full roadmap; no owner filesystem, GitHub commit, deployment, production credentials or emulators have been modified or invoked by the package author. The temporary Core01 reward gate is still not a complete server-authoritative match verifier.


# 21. SECURITY-CORE-03 — Promo, daily reward, seasonal tier and inventory abuse (v5.16; acceptance in Section 23)

**SEC02 closure evidence (2026-10-13):** Owner supplied full `qa.mjs --full` transcript. It contains `ATLASBOARD_SECURITY_CORE_02_ECONOMY_PASS: 19/19 checks` and `AtlasBoard QA FULL LOCAL PASS`; single-charge under 3 same-key concurrent purchases, single entitlement under 3 distinct keys, exact-once parallel refund, Firestore deny and atomic ledgers passed. The SEC01 negative fixture also passed. This is an emulator-only acceptance, not deployed provider-payment attestation.

**SEC03 source-review finding (P1, guarded-callable negative test now PASSED locally):** `economy/seasonal.ts` function `claimSeasonalTrackTier` checked existence, XP requirements, reward type/amount and idempotency but did NOT check `tier.active` either preflight or inside the granting Firestore transaction. An administrator-disabled track tier could therefore be invoked directly by a modified authenticated client if its XP threshold was reached. The earlier unguarded legacy branch was a source-reviewed potential exploit, not independently proven as a pre-fix emulator attack. The newly exported guarded callable was directly tested: disabled tier denial **PASS** on the owner local emulator, with the concurrent enabled tier claim also PASS.

**Interim fix delivered:** Add `Backend/Firebase/functions/src/security/seasonal-track-integrity.ts` as a complete new, deployable source file that implements the existing `metaSeasonalClaimTrackTier` callable API with fail-closed `tier.active===true` check both before the mutation and **inside** its atomic Firestore transaction, checking event active window, reward definition, XP threshold, ledger idempotency and wallet overflow. Replace `Backend/Firebase/functions/src/entry.ts` to explicitly export this guarded callable alongside accepted SEC01 host/progression gates, preserving the legacy wildcard exported names and existing Unity API contract. The original economy seasonal claim function remains in `src/economy/seasonal.ts` as a legacy implementation; a follow-up consolidation should remove duplicate logic **after capturing and reconciling the current newer local source**, not by overwriting it from older GitHub main. Do not call the legacy function directly from new exports.

**Added integrated regression:** `Backend/Firebase/functions/scripts/security-core03-rewards-e2e.mjs` exercises 15 local-only checks: emulator preflight, test users, forbidden Firestore direct writes, parallel daily claims, per-account daily isolation, two-account global promo race, exact promo replay, alternative-key promo limits, duplicate currency targets, inactive-tier seed, disabled-tier denial with sufficient XP, locked-tier denial, 3 concurrent active-tier claims with exact-once credit, immutable ledger and cross-account balance isolation. The test cleans its temporary user/doc/ledger fixtures and restores the preexisting local seasonal runtime, even on failure. A failed case stops QA and is not mislabeled as a pass.

**QA orchestration:** `Backend/Firebase/functions/scripts/qa.mjs` full replacement includes the existing `inventory-entitlements-e2e.mjs` and `promo-event-tickets-e2e.mjs` regression scripts plus SEC03 test in the same single compiled-code `--full` run, and asserts the new compiled guarded callable is actually present before starting emulators. `qa.mjs --self-test` must succeed. All scripts run against `atlasboard-usa` LOCAL emulators only. No Firebase deploy/Steamworks/Unity build or actual provider receipt verification.

**Owner verification procedure (now PASSED as recorded in Section 23):** After extraction at repo root, with emulators stopped, run only `cd "$env:USERPROFILE\AtlasBoardPrototype\Backend\Firebase\functions"` followed by `node scripts\qa.mjs --full`. The expected SEC03 marker is `ATLASBOARD_SECURITY_CORE_03_REWARDS_PASS: 15/15 checks`, then `AtlasBoard QA FULL LOCAL PASS`. Do not mark SEC03 accepted until the full Windows/Emulator log proves the result. If test fails, fix the precise source/test cause and rerun from this one full QA entrypoint, never weaken Firestore Rules or bypass production checks.

**Release blockers unchanged:** Independent server-verified gameplay results and permanent XP grant, production App Check with real Steam AppID, real Steam join/invite staging, Firebase deployment controls, Unity Editor/Release build and physical multiplayer E2E remain incomplete. GitHub commit/push and production deployments require explicit authorization.


# 22. SECURITY-CORE-03 QA Fixture Compatibility — v5.16.1 (historical issue; fixed and accepted in Section 23)

**Owner test evidence:** The `node scripts/qa.mjs --full` log (2026-10-13) passed the offline preflight, Firebase Functions TypeScript compile/ESLint, moderation, Auth/Steam local checks, and SEC02 `ATLASBOARD_SECURITY_CORE_02_ECONOMY_PASS: 19/19 checks`. QA then stopped at `scripts/inventory-entitlements-e2e.mjs` with Firestore HTTP 403 while attempting to create `item_catalog/{id}` using unsigned client-side REST. This was a **legacy test fixture error**, not an entitlement or rules vulnerability. Firestore security rules MUST remain deny-by-default. Because the test runner uses `&&`, neither the legacy promo test nor the new SEC03 negative suite executed in that attempt. At that historical failure point SEC03 was still pending; this exact QA fixture issue has now been **resolved and owner-retested successfully** in the later transcript recorded in Section 23.

**One-pass remediation delivered (complete canonical source replacements):**

- `Backend/Firebase/functions/scripts/inventory-entitlements-e2e.mjs`: replace the old REST fixture seeding and cleanup with **Admin SDK strictly bound to verified loopback Auth/Firestore/Functions emulators**, preserving all 11 grant/replay/duplicate/revoke/regrant/unknown-item/audit/final-ownership assertions, adding direct-client catalog-write denial. Fixture identities are temporary and cleanup uses the locally authenticated Admin SDK; rejects unapproved project/environment before importing Admin SDK.
- `Backend/Firebase/functions/scripts/promo-event-tickets-e2e.mjs`: replace the same legacy REST seeding/cleanup pattern for promo and item catalog with the identical emulator-only Admin safety preflight. Preserve 13 stages for multi-currency+ticket+inventory grants, immutable ledgers, per-account/global limits, exact replay, expired/disabled/future rejection, no partial grants, cleanup; add direct-client promo-write denial. Do not weaken production/client Security Rules.
- `Backend/Firebase/functions/scripts/qa.mjs`: keep existing compiled SEC01/SEC03 export gates and **same full regression order**, now invoke both existing fixture offline `--self-test` modes plus SEC03 `--self-test` before launching emulators, so trivial fixture regressions are caught earlier. Full QA continues to start/stops emulators itself and restores private env.

**Offline checks originally performed before owner runtime retest; now superseded by accepted Section 23:** JavaScript `node --check`, the 3 offline `--self-test` checks, QA ordering checks, and refusing execution when no approved emulator is present were validated by the package author. The actual owner Windows Firebase Emulator/Firestore transaction run has NOT yet been rerun. No new server application logic, Firestore Rules, Steamworks AppID, deployment or GitHub state changes are included. On owner machine install extracted full files at repository root, stop standalone emulators, then run exactly `cd "$env:USERPROFILE\AtlasBoardPrototype\Backend\Firebase\functions"` followed by `node scripts\qa.mjs --full`. Do not run standalone inventory/promo test scripts or parallel emulators. Require inventory 11/11, promo 13/13, SEC03 15/15, SEC01 and SEC02 regressions, then terminal `AtlasBoard QA FULL LOCAL PASS` before accepting Security Core 03. On next success continue the audit without requesting a separate approval.

# 23. SECURITY CORE 01–03 — Owner Windows full emulator acceptance / v5.17 (2026-10-13 UTC)

**Canonical acceptance evidence:** Owner-provided PowerShell transcript `Pasted text(20261013-002449).txt`, executed from `C:\Users\crazy\AtlasBoardPrototype\Backend\Firebase\functions` using **only** `node scripts\qa.mjs --full`. The owner log records a fresh local Functions/Firestore/Auth emulator run with rebuilt TypeScript and a second, independent local App Check emulator run. QA restored `.env.local` from its temporary backup and removed temporary emulator-only secret material. Final marker: **`AtlasBoard QA FULL LOCAL PASS (emulators only). Deployed Steam/App Check, live TTL, Unity build and physical multiplayer remain unverified.`** No production deployment or live-network validation was performed.

## 23.1 Detailed accepted result ledger

| Workstream | Exact owner log evidence | Local result | Not established by this test |
| --- | --- | --- | --- |
| TypeScript + QA build | `> build` / `> tsc`, focused ESLint and full offline preflight | **PASS** | Unity C# compile, Release build, deployed Firebase compile |
| Security Core 01: match results + progression | `Phase 10 Progression + Achievements E2E PASS: 14 checks`; `HOST_INTEGRITY_SECURITY_AUDIT_PASS`; `ATLASBOARD_SECURITY_CORE_01_INTEGRATED_PASS (emulator only)` | **PASS, interim mitigation verified** | Real independently verified full-match completion and permanent legitimate XP awards without test-only trusted receipt fixture |
| Security Core 02: commerce/wallet/refunds | `ATLASBOARD_SECURITY_CORE_02_ECONOMY_PASS: 19/19 checks` | **PASS 19/19** | Deployed economy security, payment provider receipts/real-money compliance |
| Restored entitlement tests | `AtlasBoard Inventory + Entitlements Local E2E v2 PASSED: 11/11` | **PASS 11/11** | Live cloud Firestore, large-scale load |
| Restored promo test | `AtlasBoard Promo Codes + Event Tickets Local E2E v2 PASSED: 13/13` | **PASS 13/13** | Production coupon campaign fraud/billing controls |
| Security Core 03: rewards | `ATLASBOARD_SECURITY_CORE_03_REWARDS_PASS: 15/15 checks` | **PASS 15/15** | All conceivable time-of-check/time-of-use and event-rollover races; deployed billing |
| Firestore Rules | `Phase 15A result: 17 passed, 0 failed` | **PASS 17/17** | Exhaustive field/fuzz tests and staging Rules parity |
| Public lobby / pagination | `AtlasBoard Phase 4A + Phase 16I Public Lobby Discovery E2E PASSED` | **PASS local** | Internet-scale pagination costs, sustained remote traffic |
| Chat & moderation | `Phase 15I combined chat/moderation E2E PASS: 49 checks` | **PASS 49 checks** | Live service traffic and end-user operational moderation |
| Steam local account link and return | `Phase 11C ... PASS: 10 checks`, `Phase 11D ... PASS: 10 checks` | **PASS local** | Real Steam publisher authentication / actual friend invite acceptance |
| App Check emulator negative | `Phase 15F local enforcement E2E PASS. StrictChecks=3/3` | **PASS only for emulator-observable boundaries** | Cryptographic invalid-token rejection: Functions Emulator considered a non-empty intentionally invalid header `VALID`. Deployed staging remains mandatory |

**Correctness detail — Core 01:** An earlier emulator attack demonstrated a host could invent a completed match and obtain 260 XP, one win and `first_win`. The newer guarded callable correctly rejected zero-turn completion and retained `XP=0, wins=0, first_win=false` in its negative run. Ordinary 14-case progression passed **only with a synthetic server verification receipt seeded through the local trusted test fixture**. No independent verifier currently generates such receipts from actual turns in a live game. Therefore **do not re-enable permanent XP/wins/achievements for unverified real match results**, and do not mark SEC-03/SEC-11 anti-cheat fully closed.

**Correctness detail — Core 02:** Three simultaneous same-key purchases debit once; three different-key purchases of the same entitlement grant once; three concurrent refunds do not mint duplicate credits; changed item under an existing idempotency key and cross-account refund are rejected. Direct authenticated client writes to server-owned wallet/catalog/commerce/inventory are denied. Owner log contains local cleanup `PASS`.

**Correctness detail — Core 03:** Three concurrent daily claims grant only 250 Gold to the claimant for the same UTC day; another user claims independently. Promo global/account caps and same/alternate-key replay blocks passed. **`active=false` seasonal track tier is now denied even with sufficient XP**; an enabled but locked tier is denied; after enabling the test fixture, three concurrent tier claims award the Gold prize only once, and evidence ledger remains singular. No wallet credit leaked cross-account. Local cleanup passed and the `seasonal_runtime/active` state was restored.

**Fixture compatibility resolution:** The earlier `403 PERMISSION_DENIED` arose when legacy inventory/promo test scripts tried to seed authoritative Firestore records as ordinary clients. Those scripts were fixed to use Admin SDK strictly inside verified local emulators while preserving client-write denial tests; Firestore Rules were not weakened. Their 11/11 and 13/13 owner-local passes verify the earlier test-only blocker is resolved.

**Source package inventory accepted in owner's local working tree (not a claim of push):**

- `Backend/Firebase/functions/src/entry.ts`: accepted SEC01+SEC03 guarded callable exports, preserving existing endpoint names.
- `Backend/Firebase/functions/src/security/match-integrity-gates.ts`: zero-turn result rejection and trusted verification-receipt gate on persistent progression.
- `Backend/Firebase/functions/src/security/seasonal-track-integrity.ts`: disabled/XP-locked track denial, in-transaction checks, idempotency and overflow protection.
- `Backend/Firebase/functions/scripts/progression-phase10-e2e.mjs`: integrated 14 progression cases + host attack negative controls.
- `Backend/Firebase/functions/scripts/economy-commerce-security-e2e.mjs`: SEC02 owner-race and refund tests.
- `Backend/Firebase/functions/scripts/inventory-entitlements-e2e.mjs`: emulator-safe Admin fixture and client denial, 11/11.
- `Backend/Firebase/functions/scripts/promo-event-tickets-e2e.mjs`: emulator-safe Admin fixture and client denial, 13/13.
- `Backend/Firebase/functions/scripts/security-core03-rewards-e2e.mjs`: 15 local reward/concurrency/negative checks.
- `Backend/Firebase/functions/scripts/qa.mjs`: one-pass, compile-before-emulators orchestrator, self-tests and all three security-core suites.
- `Assets/Project/Documentation/MASTER_ROADMAP.md`: this full v5.17 document.

**Important local-versus-pushed split:** Rechecked GitHub `main` during this roadmap preparation: **`bed153c2071716e1d7216fe0e3e8ecdb36f3f341`**. Newer owner-local security changes and this roadmap must be **staged, reviewed and explicitly pushed by the owner**. The assistant has not pushed any code, deployed anything, or modified the user's Windows files. Preserve accepted newer local source over older GitHub main. Do not stage generated `lib`, logs, `.env.local`, credentials, ZIPs, unexpected fonts or unrelated files. Do not use `git add .`.

## 23.2 Remaining severity and release gates after local acceptance

| Gate | Current state | Mandatory action |
| --- | --- | --- |
| **P0: production App Check** | **BLOCKED** | Official Steam AppID, publisher ticket provider, proper Firebase app registration and deployed invalid/missing/replay token proofs; no emulator-only token substitution |
| **P0: real Steam trust / Phase 17C** | **DEFERRED BY OWNER** | Real app ownership, Valve ticket signature+identity, invite/accept/join, returning sign-in, staging and physical PC validation |
| **P1: server-verified progression** | **OPEN — interim denial PASS** | Design server-verifiable turn/event authority, robust game-result proof and end-to-end valid-match reward unlock. Until then unverified permanent XP remains suspended |
| **P1: callable object authorization / abuse costs** | **SECURITY CORE 04 NEXT** | Enumerate current actual exports; test cross-account and anonymous invocation, owner vs guest/host, query/cursor flood, bootstrap abuse, rate limits, privacy, token replay, Firestore Rules |
| **P1: Unity gameplay integrity and native QA** | **OPEN** | Unity Editor C# compile, dice/rules/trade/auction/bankruptcy/bot/full-match outcomes, crash/reconnect/host restore, actual Windows Development and Release binaries |
| **P1: operational/production governance** | **OPEN** | Staging/prod project separation, managed secrets, billing/rate quotas, TTL/retention, IAM, dependency/license checks, rollback and final candidate audit |

# 24. Next-chat handoff — Security Core 04, feature sequencing and owner GitHub push

**First source of truth:** The owner will attach this exact canonical `Assets/Project/Documentation/MASTER_ROADMAP.md` v5.17 in the next chat and perform his own GitHub `main` push. Treat actual local files and newly verified pushed SHA as source authority; the roadmap captures intent/evidence but is not proof that a push happened. The user has specifically instructed: **do not ask for approval between audit phases; when tests PASS proceed to the next audit; when a reproducible security defect is found, prepare the corrected complete canonical source files and integrated regression together in one package.** The owner runs the required Windows emulator tests and sends their logs. No assistant-side auto-push or Firebase deploy.

**What is actually done:** Core 01 host result interim integrity negative PASS + 14/14 standard progression; Core 02 economy race 19/19; Core 03 rewards 15/15; inventory 11/11; promo 13/13; baseline Rules 17/17 and full backend QA local PASS. An offline preflight warning notes EditorBuildSettings starts with another scene: open `Assets/Board_Prototype.unity` in Editor before building the Development Guest; no Build Settings changes were made by backend QA. No Unity Editor compile / physical multiplayer / Release test was established by the backend command. Local Functions emulator also accepts invalid non-empty App Check tokens, so deployed cryptographic proof remains **UNVERIFIED**.

**Exact current source inventory for comparison:** Firebase root `Backend/Firebase`, Functions entrypoint `src/entry.ts`, original `src/index.ts`, guarded sources `src/security/match-integrity-gates.ts` and `src/security/seasonal-track-integrity.ts`, tests under `Backend/Firebase/functions/scripts/`, canonical `qa.mjs` (`--check`, `--full`, `--self-test`). Unity 6000.5.6f1, scene `Assets/Board_Prototype.unity`. Preserve existing city-information UI and network-quality HUD: they already exist; don't duplicate them. Preferred architecture: Firebase managed backend, no user-run always-on server, Steam PC first; Android/Google Play and crossplay optional and not decided.

**Next audit task — SECURITY CORE 04:** Verify source and actual callable exports; review all account/UID/object checks and Firestore rules for lobby/private room, pagination, auth/rate limits, mutable profiles/preferences, chat/moderation claims and Steam bootstrap exception. Write a risk matrix and prioritize a reproducible test case. Reuse/extend existing `scripts/qa.mjs --full` and existing canonical E2E tests; new permanent E2E test is acceptable only when justified as integrated coverage (no `Tools`/patch scripts). Use local emulator-only Auth/Firestore/Functions verification. Examine simultaneous abusive calls, invalid token, non-member, cross-account reads, stale signed pagination cursors, cursor tampering, quota and pre-auth rate-limiter exposure. Distinguish evidence from hypothetical risks. If no defect, record PASS and progress without redundant code.

**Critical independent track — trusted real-game reward verifier:** Do not try to bypass SEC01 by accepting a client-supplied hash, timer, minimum turns or untrusted host signature. Evaluate event log binding, authoritative turn validation, server-generated random dice / deterministic replay, participant identity and state transitions, anti-replay, crash recovery, resource/cost tradeoffs. Without a real trust source, do not award permanent XP to unverified finished games. Design first, implement complete source+tests in logical production-safe units, and never mislabel a mock receipt as a production verifier.

**Owner workflow:** user handles extracting full-file ZIPs into `C:\Users\crazy\AtlasBoardPrototype`, reviews source, stages only intended paths, commits and pushes himself. Assistant must give descriptive English GitHub commit summary and multiline description when requested, then update whole canonical roadmap, no overwriting newer local code, no automatic push, no deployment. Validate backend with `cd "$env:USERPROFILE\AtlasBoardPrototype\Backend\Firebase\functions"` followed by `node scripts\qa.mjs --full` while standalone emulators are stopped. Only claim a test PASS after owner log confirms the exact marker.

**Next workflow order:** SECURITY CORE 04 and remaining core audit; then mutually prioritized pre-release UI/gameplay features, including city-information and network-quality improvements only where not already implemented; then Phase 17C official Steamworks and approved staging; then final frozen-Release audit and owner GO/NO-GO. Continue the security work immediately after identifying this checkpoint; do not stall for repeated permission/approval questions when work is already within the owner's approved scope.
