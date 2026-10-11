# ATLAS BOARD
## Master Architecture & Development Roadmap v5.9

**Canonical checkpoint:** 2026-10-11 (America/Denver)  
**Repository:** `vuartex/AtlasBoardPrototype`  
**Last verified pushed GitHub HEAD:** `74697e472b865f76afe8f23b3ba8036ac5d35032`  
**Last verified pushed HEAD commit:** `Complete Phase 15 local close and stabilize Phase 16 Guest QA`  
**Current accepted local checkpoint:** `Phase 16H / 16H.2 / 16H.3 accepted locally. Snapshot request single-flight/coalescing, host bootstrap prioritization, localized loading/bootstrap UX and ATB frontend diagnostic codes are working. Historical Windows Guest native crash remains mitigated/regression-watch. Phase 16I public-lobby scale/pagination/load validation is the next active phase.`  
**Phase 12 close commit:** `0eca81450be91b30a5dc1cd66d901c46e2cab07c` — `Phase 12: finalize online resilience hardening and crash recovery`  
**Phase 14 close commit:** `77b4a1c29fe3ee55dbd27f4d35e6ce5e9d02e157` — `Close Phase 14 UI/UX polish and finalize localization/input handling`  
**Unity baseline:** Unity 6000.5.6f1, `Assets/Board_Prototype.unity`, unless a later committed upgrade explicitly changes it.  
**Canonical repository roadmap location:** `Assets/Project/Documentation/MASTER_ROADMAP.md`

This v5.9 checkpoint supersedes v5.8. It records the accepted Phase 16H networking/performance work, the Phase 16H.2 host-start prioritization, the Phase 16H.3 startup/match loading experience and frontend diagnostic-code system, and moves the active implementation target to Phase 16I.

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

Current pushed authority:

- `74697e472b865f76afe8f23b3ba8036ac5d35032` is the latest verified pushed checkpoint.
- Phase 15 local close and Phase 16 Guest stabilization through that checkpoint are pushed.
- Phase 16H / 16H.2 / 16H.3 are accepted locally but are not considered pushed until the user commits/pushes them.

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
**Status: DONE foundation; pagination/scale hardening remains Phase 16I.**

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
**Current active subphase: 16I.**

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

### 16H housekeeping
**Status: NON-BLOCKING CLEANUP.**

The accepted test showed repeated `ATB_MATCH_LOAD event=authoritative_ready` diagnostic lines while the overlay-close delay was still pending.

This is:

- not a network-request duplication;
- not a gameplay blocker;
- not a reason to reopen Phase 16H.

Required cleanup:

- add a one-shot completion guard so each match-loading cycle logs authoritative readiness once.

This may be completed alongside the next source checkpoint.

## 16I — Public lobby scale / discovery / measured capacity
**Status: OPEN — NEXT ACTIVE PHASE.**

Current room discovery is bounded per query/browser page and is not equivalent to a total system lobby limit.

Required work:

- implement cursor-based public-room pagination;
- preserve server-side lobby eligibility filtering;
- avoid fixed-window discovery that can hide valid rooms under scale;
- define deterministic ordering/cursor behavior;
- prevent duplicate rooms across pages;
- preserve Join/Back responsiveness while page requests are pending;
- add loading/empty/error presentation using ATB public-error policy;
- measure callable/read/write volume per active lobby and match;
- test concurrent public lobby discovery;
- test realistic simultaneous active matches;
- validate Firebase quota/cost implications;
- separate emulator performance from deployed backend performance;
- test multiple physical PCs and deployed backend before making capacity claims.

Acceptance rules:

- no valid public lobby should become undiscoverable merely because it fell outside a fixed first-N scan;
- pagination must not duplicate or skip rooms under stable data;
- stale/closed/full rooms must be handled safely;
- page fetch must not lock the menu;
- reconnect/match networking must remain unaffected;
- no supported concurrent-user/lobby claim may be published until measured load tests exist.

# 7. Phase 17 — External / Live Testing

**Status: FUTURE after Phase 16I.**

Required:

- real Internet;
- multiple physical PCs;
- multiple real accounts;
- multiple regions/time zones where feasible;
- production-like backend;
- real AtlasBoard Steam AppID;
- real Steam provider verification;
- repeated reconnect/host loss;
- 2/3/4-player sessions;
- cold/warm measurements;
- chat UTC behavior;
- Release topology validation;
- production App Check validation;
- sustained lobby/match load testing;
- repeated startup/shutdown stability on Release.

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

These must not displace Phase 16I without an explicit priority decision.

# 9. Revised execution order from v5.9

1. **Phase 14 — COMPLETE / CLOSED / PUSHED.**
2. **Phase 15A–15K — local/emulator implementation accepted; checkpoint pushed through `74697e...`; production Steam cryptographic proof deferred.**
3. **Phase 16A — consolidated backend QA PASS.**
4. **Phase 16B — Unity build readiness PASS.**
5. **Phase 16C — canonical two-client Guest build PASS.**
6. **Phase 16D — automatic runtime diagnostics PASS.**
7. **Phase 16E — historical Guest native crash MITIGATED / regression watch.**
8. **Phase 16F — QA-only native Firebase/Steam isolation ACCEPTED.**
9. **Phase 16G — lobby/match responsive exit ACCEPTED.**
10. **Phase 16H.1 — snapshot single-flight/coalescing PASS locally.**
11. **Phase 16H.2 — host-bootstrap prioritization PASS locally.**
12. **Phase 16H.3 — loading/bootstrap UX + ATB error-code system PASS locally.**
13. **Phase 16H housekeeping — one-shot duplicate match-loading diagnostic cleanup; non-blocking.**
14. **Phase 16I — NEXT: public-lobby pagination + scale/load/cost/capacity validation.**
15. **Phase 17 — external/live/release-topology validation.**
16. **Release candidate / Steam launch preparation.**

# 10. Immediate next action

The next main implementation step is:

**Phase 16I — Public lobby scale, pagination and measured capacity.**

Start with architecture before synthetic load:

1. inspect current public-lobby query and client browser limits;
2. define a cursor contract that is deterministic and stable;
3. implement backend pagination without weakening server-side eligibility filtering;
4. implement client next-page fetching without blocking Back/Join;
5. deduplicate rooms by LobbyId;
6. handle rooms that close/fill/change between pages;
7. attach generic localized ATB error presentation;
8. add focused QA for first page, next page, end-of-results and changing data;
9. measure callable/read/write volume;
10. perform emulator concurrency tests;
11. later repeat against deployed backend and multiple physical clients;
12. only then document measured capacity.

Before or alongside the Phase 16I source checkpoint, perform the small 16H housekeeping item that suppresses repeated `ATB_MATCH_LOAD event=authoritative_ready` logging.

# 11. Current checkpoint summary

## Green / accepted

- Phase 15 local/emulator security and operations close at documented level.
- Consolidated backend QA.
- Unity Build Readiness.
- Canonical two-client Guest build.
- Automatic Development Guest logs.
- Immediate lobby/match exit behavior.
- Local Guest native Firebase/Steam startup isolation.
- Historical native Guest crash not reproduced in repeated accepted samples.
- Snapshot single-flight/coalescing.
- Pre-snapshot cosmetic-intent protection.
- Host bootstrap priority.
- Fast later authoritative match readiness.
- Localized startup/match loading presentation.
- ATB frontend error-code framework.
- Seven-language loading-tip support.
- Sanitized bootstrap timing diagnostics.
- Clean-exit evidence in accepted Guest sessions.

## Yellow / active or watch

- Public lobby pagination.
- Measured concurrent lobby capacity.
- Measured active-match capacity.
- Firebase quota/cost/load characterization.
- Multi-PC/deployed latency testing.
- Historical `0xC0000409` crash remains on regression watch.
- Duplicate `ATB_MATCH_LOAD authoritative_ready` diagnostic lines need one-shot cleanup.

## Deferred production gates

- Real AtlasBoard Steam AppID.
- Publisher Web API secret.
- Real Steam returning sign-in.
- Real custom Steam App Check attestation.
- Deployed cryptographic invalid-token rejection.
- Release verification of native FirebaseAuth/Firestore account/profile path.
- Proof that local Guest QA isolation cannot activate in Release.
- Release startup/shutdown regression.
- External load/latency/quota/cost acceptance.

# 12. Next-chat handoff block

A future AtlasBoard chat should start from these facts:

- Canonical roadmap checkpoint: **v5.9, 2026-10-11 America/Denver**.
- Repository: `vuartex/AtlasBoardPrototype`.
- Last verified pushed GitHub HEAD:
  `74697e472b865f76afe8f23b3ba8036ac5d35032`
- Pushed HEAD title:
  `Complete Phase 15 local close and stabilize Phase 16 Guest QA`
- Phase 16H / 16H.2 / 16H.3 are accepted locally and must not be assumed pushed until the next user commit.
- Historical Guest crash signature:
  `BEX64 / 0xC0000409 / ucrtbase.dll / subcode 7`.
- Exact native root was not symbolically proven.
- Current local Guest mitigation isolates unnecessary native Firebase/Steam startup while preserving HTTP emulator multiplayer.
- Do not casually re-enable native FirebaseAuth/Firestore, native App Check or Steam startup inside the canonical local Guest.
- Release behavior remains separate and must be revalidated before launch.
- Guest lobby/match local exit should remain immediate.
- Snapshot polling/reconnect/host-migration semantics must remain intact.
- Phase 16H single-flight is accepted: overlapping snapshot callers share one in-flight network task.
- Forced refresh can schedule at most one follow-up refresh.
- Remote cosmetic intent waits for an authoritative snapshot.
- Phase 16H.2 host bootstrap publishes the first authoritative frame before maintenance traffic.
- Phase 16H.3 adds localized startup/match loading and stable ATB frontend error codes.
- Public UI must not expose provider/backend implementation details.
- `FRONTEND_ERROR_CODEBOOK.md` is the canonical ATB code reference.
- `ATB101/102/103` are startup delay observations; `ATB301/302/303` are match-sync delay observations.
- One non-blocking cleanup remains: suppress duplicate `ATB_MATCH_LOAD event=authoritative_ready` log entries per loading cycle.
- Current active phase is **16I public-lobby pagination + measured scale/load**.
- Do not claim a supported concurrent-player/lobby count until measured testing exists.
- Do not auto-commit, auto-push or deploy.
- Prefer complete replacement files over patches unless explicitly asked.

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
