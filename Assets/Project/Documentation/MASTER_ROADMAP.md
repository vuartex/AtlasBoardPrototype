# ATLAS BOARD
## Master Architecture & Development Roadmap v5.11

**Canonical checkpoint:** 2026-10-11 (America/Denver)  
**Repository:** `vuartex/AtlasBoardPrototype`  
**Last verified pushed GitHub HEAD (checked 2026-10-11 America/Denver):** `ca3a9174146165e4238d2aeaa47eedd8a11156df`  
**Last verified pushed HEAD commit:** `Finalize Phase 16H reliability and prepare Phase 16I`  
**Current accepted local checkpoint:** `Phase 16I cursor pagination plus local emulator read/match/soak/mixed load and latency A/B checks accepted. Phase 17A Steam Editor runtime smoke PASS (AppID 480; Phase 11B 8/8). Phase 17B.2 opt-in Development Guest Steam invite overlay smoke PASS (SteamAPI.Init, invite dialog API, overlay activation); real Steam publisher, live ticket/App Check, cross-PC invite/join, and release validation are still blocked or pending. Local accepted source and this roadmap must be staged/pushed explicitly by the owner.`  
**Phase 12 close commit:** `0eca81450be91b30a5dc1cd66d901c46e2cab07c` — `Phase 12: finalize online resilience hardening and crash recovery`  
**Phase 14 close commit:** `77b4a1c29fe3ee55dbd27f4d35e6ce5e9d02e157` — `Close Phase 14 UI/UX polish and finalize localization/input handling`  
**Unity baseline:** Unity 6000.5.6f1, `Assets/Board_Prototype.unity`, unless a later committed upgrade explicitly changes it.  
**Canonical repository roadmap location:** `Assets/Project/Documentation/MASTER_ROADMAP.md`

This v5.11 checkpoint updates the prior v5.10 local roadmap baseline with accepted Phase 16I and Phase 17A/17B.2 evidence. The original pre-implementation notes are preserved only as historical baseline context; they are no longer the current implementation status. Production/release approval remains explicitly blocked. GitHub `main` is still behind accepted local Phase 16I/17B source until the owner stages, commits and pushes the verified files.

# 1. Source, replacement and Git discipline

Before meaningful source changes:

- Inspect current GitHub `main` before using repository source as a baseline.
- GitHub is authoritative only for work already committed and pushed.
- Newer accepted local work must never be overwritten by an older GitHub copy.
- Prefer complete replacement `.cs` files in canonical project paths for user-delivered Unity source changes.
- Do not use textual patches, `git apply` patches or installer scripts unless explicitly requested.
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

Current source authority (checked 2026-10-11 America/Denver):

- Latest verified pushed `main`: `ca3a9174146165e4238d2aeaa47eedd8a11156df`, `Finalize Phase 16H reliability and prepare Phase 16I`.
- That pushed checkpoint is **not** evidence that the subsequently accepted Phase 16I and Phase 17B.2 local files have been committed or pushed.
- Latest user-accepted local source includes Phase 16I pagination and local load harnesses plus Phase 17B.2 opt-in Steam Guest invite QA. Preserve those local files.
- The local owner must stage and push only intended source/doc files. Keep `steam_appid.txt` for AppID 480 as a local development file only; never stage Steam tickets, private tokens, Secret Manager values, logs, generated builds or emulator databases.
- Before replacing this roadmap on a different workstation, compare it with that workstation's current canonical roadmap. Merge any newer local sections rather than overwriting them.

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
**Current active subphase: Phase 17B.2 local Steam invite overlay accepted; Phase 17C external prerequisites next.**

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

**Status: ACTIVE — 17A/17B LOCAL STEAM SMOKE ACCEPTED; EXTERNAL/PRODUCTION PENDING.**

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
- **Preflight tool:** `Tools/Phase17B/SteamGuestPreflight.ps1`; creates the missing development AppID file only with explicit `-FixDevAppId`.

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

These remain optional and must not displace open Phase 17 release/security requirements without an explicit priority decision.

# 9. Revised execution order from v5.10

1. **Phase 14 — COMPLETE / CLOSED / PUSHED.**
2. **Phase 15A–15K — local/emulator hardening accepted; production publisher/App Check gates open.**
3. **Phase 16A–16H.4 — locally accepted with documented native-crash regression watch.**
4. **Phase 16I — LOCAL CURSOR PAGINATION + EMULATOR LOAD/DIAGNOSTIC ACCEPTED.**
5. **Phase 17A — Unity Steam Editor/AppID 480 runtime smoke PASS (not publisher proof).**
6. **Phase 17B.1 — Dev Guest Invite QA button surfaced; SteamAPI.Init initially failed (missing AppID file).**
7. **Phase 17B.2 — after local development AppID 480 fix, Steam Overlay invite UI activation PASS.**
8. **Phase 17C — NEXT: physical multi-client Steam invite/join planning + staged backend prerequisites; no production operations without explicit approval.**
9. **Phase 17D+ — reconnect/host loss, region/latency, deployed App Check/Steam proof, Release gate.**
10. **Release candidate / Steam launch preparation — NOT READY.**

# 10. Immediate next action

**Do not repeat Phase 17B.2 solely to prove the Spacewar Overlay opens:** the accepted log and screenshot already establish that limited result.

Before starting the next external test, inventory the real Steam AppID/partner access, available physical test PCs/accounts and **approved** development/staging backend topology. Prepare Phase 17C real invite/accept/join test with a non-loopback backend; `127.0.0.1` must not be presented as a multi-PC shared service. If real Steam AppID or approved backend is missing, explicitly record the blocker and continue only safe preparation/QA; do not bypass publisher-ticket verification or production App Check for a release test.

Until explicitly authorized, **do not deploy, change GitHub, change secrets or force production App Check enforcement.**

# 11. Current checkpoint summary

## Green / accepted locally

- Phase 16A–H.4 local QA and runtime reliability baselines, subject to crash regression watch.
- Phase 16I paginated public discovery and local emulator capacity/read/mixed load, including 32 concurrent match pairs and a 75 ms staggering A/B diagnostic.
- Phase 17A Unity Steam provider initialization and Phase 11B static check 8/8 using AppID 480.
- Phase 17B.2: Windows Development Guest local emulator identity, private lobby, QA-only SteamAPI Init AppID 480 and Steam invite Overlay activation.
- Phase 17B.2: event-level Steam QA component diagnostics with its own invite payload redacted, and clean process exit; inherited lobby creation logger still exposes room code in the session log and must be hardened before public telemetry.

## Yellow / current watch and active work

- Actual Steam platform join/invite and multi-PC network acceptance is still pending.
- Historical `BEX64 / 0xC0000409` native Windows Guest crash remains a regression watch; native Steam was re-enabled only on explicit QA user click in opt-in Development Guest.
- Existing private-lobby creation logging still includes its six-digit room access code in the Guest session log; do not expose such raw logs to players or public issue trackers. Redaction is pending.
- Emulator burst-tail latency and measured local workload cannot be extrapolated into a production capacity claim.
- Phase 17C physical-client/back-end strategy requires verified real Steam AppID and safe shared environment design.

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

- Canonical roadmap candidate checkpoint: **v5.11, 2026-10-11 America/Denver**; verify against newer local roadmap before overwrite.
- Repository: `vuartex/AtlasBoardPrototype`, Unity baseline 6000.5.6f1, scene `Assets/Board_Prototype.unity`.
- Last **already pushed** `main` at latest read: `ca3a9174146165e4238d2aeaa47eedd8a11156df`, `Finalize Phase 16H reliability and prepare Phase 16I`. Do not fabricate a future commit SHA.
- Subsequent local accepted Phase 16I and Phase 17B.2 work **may not yet be pushed**; inspect `git status`, staged files and local roadmap before replacing anything.
- Phase 16I cursor pagination and local emulator mixed load PASS; 32 concurrent matches observed with no workflow failures in the accepted v2/A-B tests. Local result is not certified production capacity.
- Phase 17A Editor: Steam initialized with AppID 480 and Phase 11B 8/8. Not real publisher verification.
- Phase 17B.2 Guest on local emulators: opt-in `-atlasSteamOverlayQa` enables a separate QA invite control; `SteamAPI.Init`, `ActivateGameOverlayInviteDialogConnectString` and Overlay activation callback were observed PASS after adding local `steam_appid.txt=480` beside the EXE.
- User-provided 2026-10-11 20:07 America/Denver Guest log demonstrated `STEAM_INIT_OK`, `INVITE_API_RETURNED`, `OVERLAY_ACTIVATED`, `SESSION_CLEAN_EXIT droppedMessages=0` with no credential/ticket exposure.
- Screenshot: Steam opened invite UI with Spacewar branding. The real Steam friend invite/accept/join is NOT tested or accepted. Do not claim the actual real AppID alone automatically resolves all invitation behavior.
- Keep `.local` tokens, app publisher secret and raw tickets out of source/logs; QA component itself redacts invite codes, but an existing lobby creation diagnostic still logs the raw room code. Treat logs as sensitive until this is fixed.
- Production Steam custom App Check and returning sign-in gates remain blocked until real AppID/Valve/deployed proof.
- Historical native Guest crash must remain on regression watch; explicit opt-in Steam Native Init is confined to the development-only test path.
- Next work: plan 17C real physical Steam client(s), approved shared backend, verified identity, explicit release gates; do not connect remote clients to localhost emulator.
- Existing Phase 16I/17B files should be treated as locally accepted, not overwritten from older `main` without local diff review.
- Git discipline: stage only desired paths, no `git add .`, user performs commit/push. Never auto-push/deploy.
- Deliver complete replacement files in small ZIPs, not textual patches; ensure no unrelated assets/Fonts/generated binaries are staged.

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
