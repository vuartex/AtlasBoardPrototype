# ATLAS BOARD
## Master Architecture & Development Roadmap v5.8

**Canonical checkpoint:** 2026-10-11 (America/Denver)  
**Repository:** `vuartex/AtlasBoardPrototype`  
**Last verified pushed GitHub HEAD:** `dcfc6550ea20a0f4719714abb5ecc2d125765021`  
**Last verified pushed HEAD commit:** `Complete Phase 15F-15H security hardening`  
**Current accepted local checkpoint:** `Phase 15I/15J/15K local security and operations validation completed. Phase 16 backend/build readiness is PASS. Two-client local Guest startup crash is currently mitigated and under regression watch. First-run multiplayer latency/request burst is the next active blocker.`  
**Phase 12 close commit:** `0eca81450be91b30a5dc1cd66d901c46e2cab07c` — `Phase 12: finalize online resilience hardening and crash recovery`  
**Phase 14 close commit:** `77b4a1c29fe3ee55dbd27f4d35e6ce5e9d02e157` — `Close Phase 14 UI/UX polish and finalize localization/input handling`  
**Unity baseline:** Unity 6000.5.6f1, `Assets/Board_Prototype.unity`, unless a later committed upgrade explicitly changes it.  
**Canonical repository roadmap location:** `Assets/Project/Documentation/MASTER_ROADMAP.md`

This v5.8 checkpoint supersedes the v5.7 sequencing/status checkpoint. It records the completed local Phase 15I/15J/15K work, the Phase 16 backend/build QA checkpoint, the October 11 local Windows Guest native-crash investigation, the QA-only Firebase isolation introduced to stabilize two-client local testing, and the remaining first-run latency/request-concurrency work.

# 1. Source, replacement and Git discipline

Before meaningful source changes:

- Inspect current GitHub `main` before using repository source as a baseline.
- GitHub is authoritative only for work already committed and pushed.
- Newer accepted local work must never be overwritten by an older GitHub copy.
- For user-delivered Unity source changes, prefer complete replacement `.cs` files in the canonical project path.
- Do not send/apply textual patches, `git apply` patches or installer scripts unless explicitly requested.
- Do not auto-commit, auto-push, deploy or mutate production configuration.
- Do not use `git add .`; stage only intended files.
- Do not stage unrelated historical/font/generated changes.
- Backend QA belongs in the existing `Backend/Firebase/functions/scripts/qa.mjs`; do not proliferate phase-specific permanent QA scripts.
- Roadmap status changes belong in this canonical `MASTER_ROADMAP.md`; temporary phase notes are not the canonical source of truth.
- Firebase Functions source changes require TypeScript build and focused validation before acceptance.
- Restart emulators after backend source changes when emulator runtime behavior is under test.
- Unity runtime changes require compile + runtime smoke before acceptance.
- Static review is not equivalent to production runtime acceptance.
- Never place Steam publisher secrets, production credentials, App Check debug tokens or raw provider tickets in Unity source/logs.
- Local-emulator QA shortcuts must be narrowly gated and must never silently apply to a release build.

Current pushed authority:

- `dcfc6550ea20a0f4719714abb5ecc2d125765021` — Phase 15F–15H pushed checkpoint.
- Phase 15I–15K, Phase 16 QA/build work and the October 11 Guest runtime stabilization are local until the user explicitly commits/pushes them.

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

- Account identity and provider mappings.
- Wallet/premium economy.
- Inventory entitlements.
- Promo/event rewards.
- Lobby/session membership.
- Match authority and authority transfer.
- Presence/reconnect state.
- Chat/moderation/report state.
- Completed-match progression and achievements.
- Future competitive/ranked outcomes.

## 2.3 Host/seat invariant

Gameplay state belongs to the Player Seat, not the current controller.

`Human -> TemporaryBot -> Human` must preserve:

- AccountId / SeatId
- money
- properties
- pawn position
- development
- turn state
- bankruptcy state
- match-result state

Reconnect and snapshot optimization must preserve this invariant. Do not reduce polling or discard authoritative snapshots merely to improve apparent latency without proving reconnect/host-migration behavior still works.

## 2.4 Hosting strategy

- Firebase / managed Google Cloud remains the selected backend direction.
- Do not add a self-managed always-on VPS/game server by default.
- Steam does not automatically host AtlasBoard gameplay authority.
- Steam Networking/SDR may be evaluated later as an optional transport optimization.
- Current architecture remains Firebase-backed lobby/match persistence plus Host-authoritative turn simulation.
- Local emulator limitations and cold-start behavior are QA-environment concerns; they must not be mistaken for measured deployed production latency.

## 2.5 App Check invariant

- Editor/development security validation uses the Firebase App Check debug-provider path.
- Desktop standalone production architecture uses the Steam custom App Check provider.
- `platformSteamCreateAppCheckToken` remains the bootstrap endpoint and must not require an existing App Check token.
- Production Steam custom App Check runtime acceptance remains deferred until a real AtlasBoard Steam AppID and publisher Web API secret exist.
- Local Functions Emulator acceptance is not cryptographic proof of production App Check validity.
- Local two-client Guest QA may use an explicit emulator-only placeholder App Check header only when all of the following are true:
  - Development Build.
  - Windows standalone Guest.
  - explicit `-atlasLocalEmulators`.
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
- Phase 15A–15H pushed security hardening.
- Phase 15I–15K local security/operations validation.
- Phase 16 backend QA and Unity build-readiness validation.

# 4. Phase status overview

## Phase 3B — Seat / Turn / AFK
**Status: DONE.**

## Phase 3C — Account & Cloud Profile
**Status: FOUNDATION DONE; production provider validation remains a release gate.**

## Phase 3D — Lobby / Session UI
**Status: DONE.**

## Phase 4 — Public Lobby Browser
**Status: DONE foundation; scale/pagination hardening remains Phase 16 work.**

## Phase 5 — Online Gameplay Networking
**Status: MAIN PATH DONE / runtime flows proven; first-run latency/concurrency hardening remains Phase 16.**

## Phase 6 — Chat + Moderation Foundation
**Status: DONE; Phase 15I local security/lifecycle close passed.**

## Phase 8 — Gameplay / Content Checkpoint
**Status: DONE.**

## Phase 9 — Store / Economy / Seasonal Event
**Status: DONE foundation; security accepted through Phase 15H.**

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

**Overall engineering status: LOCAL SECURITY/OPERATIONS IMPLEMENTATION COMPLETE THROUGH 15K.**  
**Production Steam cryptographic acceptance: DEFERRED.**

## 15A — Firestore Direct-Client Security Boundary
**Status: DONE / PUSHED.**

- Deny-by-default boundary for unreviewed collections.
- Owner-scoped access for reviewed profile/preference documents.
- Server-authoritative wallet, inventory, commerce, progression, lobby, match and Steam-mapping data.
- Rules/E2E acceptance completed.

## 15B — Unified Emulator Firebase Session
**Status: DONE / PUSHED for the original Editor/dev architecture.**

- FirebaseAuth SDK + Firestore Emulator identity was unified with the development lobby identity.
- This remains valid for Editor QA.
- Important Phase 16 qualification: the canonical standalone local Guest no longer starts this second native SDK session; see Phase 16 local Guest topology.

## 15C — App Check Transport Plumbing
**Status: DONE / PUSHED.**

Central request-security coverage exists for lobby, match, chat, moderation, economy, progression and Steam link traffic.

## 15D — Firebase App Check SDK + Editor Debug Provider
**Status: DONE / PUSHED.**

Editor/debug App Check path remains available and is not removed by the Phase 16 Guest isolation.

## 15E — Windows/Steam Production Custom App Check Provider
**Status: CODE/BUILD ACCEPTED / PUSHED; REAL PRODUCTION RUNTIME PROOF DEFERRED.**

- Desktop Steam provider architecture remains required for Release.
- Local Guest QA isolation must never be used as evidence that the production Steam provider passed.

## 15F — Staged App Check Enforcement
**Status: DONE / PUSHED.**

- 44 protected runtime callables.
- 9 emulator/dev exemptions.
- 1 Steam App Check bootstrap exemption.
- Missing-token rejection proven locally.
- Production cryptographic invalid-token proof remains deferred.

## 15G — Callable Authorization + Abuse Hardening
**Status: DONE / PUSHED.**

- 54-callable authorization inventory.
- 51 require Firebase Auth.
- 3 intentional auth exceptions.
- Server-owned low-frequency abuse controls retained.
- High-frequency polling/snapshot endpoints are not treated like low-frequency abuse-sensitive endpoints.

## 15H — Economy / Commerce Security
**Status: DONE / PUSHED.**

- Wallet ledger/idempotency.
- Inventory catalog authority.
- Commerce server-price/refund ownership.
- Promo replay/limit protections.
- Seasonal config/catalog revalidation inside value-granting transactions.

## 15I — Moderation + Data Lifecycle
**Status: DONE / accepted locally; not yet pushed at this checkpoint.**

Accepted local direction:

- Moderator authority is explicit and not inferred from generic labels.
- Normal users cannot self-escalate moderator operations.
- Chat/report/sanction operations remain server-authoritative.
- Deleted/expired chat records are excluded from normal reads.
- Report/investigation evidence that must remain open is not prematurely expired.
- Moderation audit/config evidence remains protected.
- Local moderation security audit/validation passed.

Permanent validation belongs in `qa.mjs`; do not keep one-off phase installer/validation scripts as the canonical QA entry point.

## 15J — Production Operations / Environment Hardening
**Status: DONE / accepted locally; not yet pushed at this checkpoint.**

Accepted local direction:

- Emulator services bind to loopback.
- Development/staging/production environment boundaries fail closed.
- Steam publisher secret remains Functions-secret/config territory, never client source.
- Steam/App Check operational logs omit raw provider secrets/tickets and reduce unnecessary identifiers.
- Transport/provider failures use bounded failure classes rather than raw sensitive payloads.
- Local credentials and publisher-key file patterns are excluded from Git.
- Production rollout remains blocked when real Steam production requirements are absent.
- Operational/security validation passed locally.

## 15K — Steam / Backend Security Close
**Status: LOCAL CODE/EMULATOR SECURITY CLOSE PASS; PRODUCTION CRYPTOGRAPHIC CLOSE DEFERRED.**

Local acceptance includes:

- Steam environment fail-closed guards.
- App Check OFF/ON emulator regression.
- Steam development link and returning-sign-in regression.
- Abuse/economy/Firestore/security regression.
- Historical Steam E2E compatibility repaired without weakening deny-by-default Firestore rules.
- Phase 15I/15J/15K suites incorporated into the consolidated backend QA path.

Still required before production security close:

- Real AtlasBoard Steam AppID.
- Real publisher Web API secret.
- Real Steam ticket verification.
- Real Windows standalone Steam custom App Check runtime acceptance.
- Deployed missing-token and cryptographically invalid-token rejection proof.
- Bootstrap exemption proof against deployed configuration.
- Release verification that no local emulator shortcut can activate.

# 6. Phase 16 — Build Readiness, Runtime Stability and Performance

**Status: ACTIVE.**

## 16A — Consolidated backend QA
**Status: PASS locally.**

Canonical backend QA entry point:

`Backend/Firebase/functions/scripts/qa.mjs`

Phase-specific permanent QA script proliferation is not desired. The consolidated QA path covers the accepted backend security/regression suites.

## 16B — Unity build readiness
**Status: PASS locally.**

Canonical validator:

`Assets/Project/Editor/AtlasBoardBuildQA.cs`

Canonical menu:

`Atlas Board/QA & Diagnostics/QA/Validate Build Readiness`

Build readiness checks passed at the current local checkpoint.

## 16C — Canonical two-client local Guest QA build

Canonical builder:

`Assets/Project/Editor/AtlasBoardTwoClientLobbyTestV1.cs`

Canonical menu:

`Atlas Board/Online & Backend/Online/Build Two-Client Guest Test v1`

Output:

`C:\Users\crazy\AtlasBoard_3D3C_GuestBuild\AtlasBoardGuest.exe`

Local launch must use the generated BAT for emulator testing. The executable launched without the explicit local-emulator arguments may target a different runtime path.

Canonical local arguments include:

- `-atlasLocalEmulators`
- `-atlasDevName="Guest Player"`
- windowed 1280x720 development settings

## 16D — Automatic runtime/crash diagnostics
**Status: IMPLEMENTED locally.**

Development Guest diagnostics:

`Assets/Project/Scripts/Online/AtlasBoardRuntimeDiagnostics.cs`

Expected behavior:

- starts automatically when the Guest starts;
- creates timestamped session logs under the Guest build directory `Logs`;
- records startup identity/build information;
- records Unity warnings/errors;
- records frame summaries and main-thread frame stalls;
- records clean exit markers;
- preserves separate files per run.

Windows WER LocalDumps may additionally be configured to write native `.dmp` files under the build `Logs/CrashDumps` directory. Raw dumps may contain sensitive process memory and should not be casually shared.

## 16E — October 11 Windows Guest native-crash regression

**Current status: MITIGATED / PASS FOR CURRENT LOCAL QA SAMPLE / REGRESSION WATCH.**

Observed failure before mitigation:

- intermittent standalone Guest termination, including when no UI input was performed;
- Windows WER `BEX64`;
- exception `0xC0000409`;
- subcode/P9 `7`;
- faulting module reported as `ucrtbase.dll`;
- repeated same fault offset;
- D3D11 did not eliminate the crash;
- native dump evidence placed Firebase C++ SDK frames in the failing process, but the exact native root function was not symbolically proven.

Important conclusion:

The problem was not treated as a graphics-only issue and was not proven to be caused by the Firebase backend server load. The local standalone Guest was entering several native Firebase/Steam startup paths while it already had an authoritative HTTP emulator identity path. This created unnecessary local QA startup complexity and was strongly correlated with the intermittent native failure.

Mitigations currently active for the explicit local Windows Development Guest:

1. Local callable App Check uses an emulator-only HTTP placeholder header on the loopback Functions Emulator path.
2. Native Firebase App Check provider registration is bypassed for this explicit local Guest.
3. Steam provider initialization/retry is bypassed for this explicit local Guest.
4. The Phase 15B secondary FirebaseAuth/Firestore SDK sign-in is suppressed for this explicit local Guest.
5. The canonical Guest build removes native `AtlasBoardAccountService` and old Firebase runtime test components from the temporary build-scene copy.
6. Lobby/match identity for the Guest continues through the existing Auth Emulator + Functions Emulator HTTP transport.
7. Editor and Release/production behavior are intentionally not disabled by these QA gates.

Current runtime evidence:

- repeated Guest opens after the final isolation no longer reproduced the previous native crash in the current test sample;
- long sessions reached match gameplay and exited cleanly;
- Guest lobby/match exit responsiveness is improved and local exit no longer depends on waiting indefinitely for a server response.

Do not claim the historical native root cause is mathematically proven. Treat the issue as a mitigated regression and reopen it immediately if the same `0xC0000409` signature returns.

## 16F — QA-only isolation: what is disabled and what must be re-tested

The following are intentionally disabled/bypassed only in the canonical explicit local Guest QA topology:

- native FirebaseAuth initialization in the stripped Guest scene;
- native Firestore initialization in the stripped Guest scene;
- Phase 15B secondary SDK emulator sign-in;
- native App Check provider registration;
- SteamAPI/provider startup for the local emulator Guest.

These must **not** be manually re-enabled in the local Guest merely to make the QA executable resemble production. The purpose of this executable is reliable two-client local multiplayer testing.

They are still required in the Release/production architecture. Before release:

- verify the build stripper does not affect normal Release output;
- verify Release still initializes the intended Firebase account/profile path;
- verify real Steam identity and returning sign-in;
- verify real Steam custom App Check;
- verify cloud profile/preferences behavior;
- run repeated release startup/shutdown tests with Steam active;
- re-open native crash investigation if production native Firebase + Steam startup reproduces the old failure.

Therefore there is no forgotten feature that must be “turned back on” now. The required action is a future **Release-path acceptance test**, not removal of the local QA isolation.

## 16G — Lobby/match responsiveness changes
**Status: PARTIALLY ACCEPTED.**

Accepted behavior:

- Lobby Back remains usable instead of trapping the player behind a pending list request.
- Guest can leave a match and return to the menu locally without waiting indefinitely for the backend.
- Host exit retains safe authority-migration semantics rather than blindly abandoning a live human session.
- After a local leave, stale responses from the old match must not reopen/overwrite the new local UI state.

Invariant:

Do not disable reconnect snapshots, host-authoritative snapshots or required polling simply to reduce request count. Optimize duplicated/overlapping work while preserving recovery semantics.

## 16H — First-run latency / request burst
**Status: OPEN — NEXT ACTIVE BLOCKER.**

Observed local behavior is inconsistent between cold and warm emulator/runtime states:

- one Guest run observed a public-lobby list call near 4 seconds;
- later runs observed the same lobby list path around tens of milliseconds;
- the first invocation of some Functions emulator callables may take roughly 1–2 seconds while later invocations are much faster;
- one early match showed the follower waiting far too long before receiving starting-roll state;
- later matches synchronized starting dice quickly;
- request logs show overlapping/repeated `matchGetSnapshot` traffic and periods where multiple requests are in flight;
- timeout and host-recovery probes appeared during the bad first-match run.

The next optimization must target **request concurrency and cold-start behavior**, not delete authoritative synchronization.

Required work:

- add/verify one in-flight snapshot request per client/match;
- coalesce duplicate snapshot requests rather than queueing them;
- prevent per-frame/overlapping callers from multiplying the same network operation;
- preserve a refresh request made while one call is in flight by scheduling one follow-up refresh, not dozens;
- keep reconnect/host-migration forced refresh semantics;
- review match chat/cosmetic/presence calls so they do not create startup contention;
- do not submit remote pawn-cosmetic intents before the first authoritative match snapshot exists;
- measure cold vs warm first-lobby and first-match timing separately.

Acceptance targets for local emulator QA:

- no 10–30 second UI wait for starting-roll synchronization;
- normal warm-state authoritative turn/dice propagation should feel effectively immediate;
- cold-start overhead may be visible, but must remain bounded and must not freeze or trap the user;
- no unbounded overlap of `matchGetSnapshot`;
- no functional regression in reconnect, host migration, rematch or match exit.

## 16I — Public lobby scale / discovery
**Status: OPEN before external scale testing.**

Current room-list page limits are not the total number of rooms the backend can store, but discovery is currently bounded per request/query. Before assuming larger player populations:

- implement cursor-based public-room pagination;
- preserve server-side eligibility filtering;
- avoid scanning a fixed small window that can hide valid rooms under load;
- load-test realistic concurrent lobby + match polling;
- measure backend read/write/callable volume per active match;
- validate quota/cost behavior;
- validate latency with multiple physical PCs and a deployed backend.

Do not state a maximum supported concurrent player/lobby count until measured load tests exist.

# 7. Phase 17 — External / Live Testing

**Status: FUTURE after Phase 16 runtime/performance gates.**

Required:

- real Internet;
- multiple physical PCs;
- multiple real accounts;
- multiple time zones/regions;
- real AtlasBoard Steam AppID;
- production-like backend;
- real Steam provider verification;
- repeated reconnect/host loss;
- 2/3/4-player sessions;
- cold/warm start measurements;
- chat UTC behavior;
- release topology validation;
- production App Check validation;
- sustained lobby/match load testing.

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
- Colorblind palettes and additional accessibility options.
- Privacy-safe crash telemetry/analytics.
- Feature flags / staged rollout.
- Backend health dashboard.
- Moderation dashboard.

These must not displace the active Phase 16 networking/performance gates without an explicit priority decision.

# 9. Revised execution order from v5.8

1. **Phase 14 — COMPLETE / CLOSED / PUSHED.**
2. **Phase 15A–15H — COMPLETE at documented level / PUSHED through `dcfc6550...`.**
3. **Phase 15I — COMPLETE locally: moderation + data lifecycle.**
4. **Phase 15J — COMPLETE locally: production operations/environment hardening.**
5. **Phase 15K — LOCAL CODE/EMULATOR SECURITY CLOSE PASS; real production Steam/App Check proof deferred.**
6. **Phase 16A — consolidated backend QA PASS locally.**
7. **Phase 16B — Unity build readiness PASS locally.**
8. **Phase 16C–G — canonical two-client Guest QA, diagnostics and exit responsiveness implemented.**
9. **Phase 16D — historical native Guest crash currently mitigated / regression watch.**
10. **Phase 16H — NEXT: first-run match latency + duplicate/overlapping snapshot request control.**
11. **Phase 16I — public-lobby pagination + measured scalability/load testing.**
12. **Phase 17 — external/live/release-topology validation.**
13. **Release candidate / Steam launch preparation.**

# 10. Immediate next action

The next implementation step is:

**Phase 16H — First-run multiplayer latency and request-concurrency hardening.**

Start from evidence rather than reducing synchronization blindly:

1. Inspect all call sites that can trigger `matchGetSnapshot`.
2. Identify whether more than one caller can have the same match snapshot request in flight.
3. Implement request coalescing/single-flight behavior.
4. Keep one pending refresh flag if a forced refresh arrives during an in-flight request.
5. Preserve reconnect, host migration and explicit force-refresh semantics.
6. Prevent cosmetic intent submission before a valid authoritative snapshot exists.
7. Repeat two-client local test from a freshly started emulator and from a warmed emulator.
8. Measure time from `lobbyStartMatch` to Guest starting-roll presentation.
9. Confirm no native crash regression.
10. Only after this is stable, proceed to pagination/load-capacity work.

# 11. Current checkpoint summary

## Green / accepted locally

- Phase 15I moderation/data-lifecycle validation.
- Phase 15J operational/environment validation.
- Phase 15K local Steam/backend security regression.
- Consolidated backend QA.
- Unity Build Readiness validator.
- Canonical two-client Guest builder.
- Automatic per-session Development Guest logging.
- Guest log storage under build `Logs`.
- Immediate Guest match/lobby exit behavior.
- Local Guest Steam startup bypass.
- Local Guest native App Check provider bypass.
- Local Guest Phase 15B secondary SDK-session suppression.
- Canonical Guest build-scene native Firebase account/test-component stripping.
- Current repeated Guest sample did not reproduce the old `0xC0000409` crash.
- Long tested Guest sessions can reach match gameplay and exit cleanly.

## Yellow / active

- First-run/cold-start lobby latency.
- First-match starting-roll synchronization latency.
- Duplicate/overlapping `matchGetSnapshot` traffic.
- Startup contention between snapshot/chat/cosmetic/presence/recovery calls.
- Native crash remains on regression watch.
- Public lobby pagination and measured concurrent capacity.
- Multi-PC/deployed latency testing.

## Deferred production gates

- Real AtlasBoard Steam AppID.
- Publisher Web API secret.
- Real Steam returning sign-in.
- Real custom App Check attestation.
- Deployed cryptographic invalid-token rejection.
- Release verification of native FirebaseAuth/Firestore account/profile path.
- Proof that local Guest QA isolation cannot activate in Release.
- External load/latency/quota/cost acceptance.

# 12. Next-chat handoff block

A future AtlasBoard chat should start from these facts:

- Canonical roadmap checkpoint: **v5.8, 2026-10-11 America/Denver**.
- Repository: `vuartex/AtlasBoardPrototype`.
- Last verified pushed GitHub HEAD:
  `dcfc6550ea20a0f4719714abb5ecc2d125765021`
- Pushed HEAD title:
  `Complete Phase 15F-15H security hardening`
- Phase 15I/15J/15K are accepted locally but are not considered pushed until the user commits/pushes them.
- Phase 16 backend QA is locally PASS.
- Build Readiness validator is locally PASS.
- Canonical Guest builder:
  `Atlas Board/Online & Backend/Online/Build Two-Client Guest Test v1`
- Guest executable:
  `C:\Users\crazy\AtlasBoard_3D3C_GuestBuild\AtlasBoardGuest.exe`
- Use the generated local-emulator BAT for local Guest QA.
- Historical Guest crash:
  `BEX64 / 0xC0000409 / ucrtbase.dll / subcode 7`.
- D3D11 did not fix that crash.
- Exact native root was not symbolically proven.
- Current mitigation isolates the explicit local Guest from unnecessary native Firebase/Steam startup while preserving the HTTP Auth/Functions emulator multiplayer path.
- Do not re-enable native FirebaseAuth/Firestore, Steam startup or native App Check inside the canonical local Guest as a casual cleanup.
- Editor/Release paths are intentionally not disabled.
- Before release, production/native Firebase + real Steam + real App Check must be re-validated separately.
- Current repeated local Guest tests no longer reproduce the historical native crash; status is **mitigated / regression watch**.
- Guest lobby/match local exit is immediate and should remain so.
- Do not break reconnect/snapshot/host-migration semantics while optimizing.
- Current active blocker is first-run/cold match responsiveness.
- Logs show duplicate/overlapping `matchGetSnapshot` traffic; optimize with single-flight/coalescing rather than blindly reducing required refreshes.
- First authoritative snapshot must exist before remote cosmetic intents are submitted.
- Public lobby discovery still needs pagination and measured scale testing.
- Do not claim a supported concurrent player count until load testing exists.
- Do not auto-commit, auto-push or deploy.
- Prefer complete replacement files over patches unless the user explicitly asks for a patch.

# 13. Roadmap maintenance rule

At every meaningful checkpoint:

1. Record the latest already-existing pushed SHA/title; never invent the SHA of a commit that does not yet exist.
2. Preserve accepted local work until it is safely pushed.
3. Update the current blocker and acceptance evidence.
4. Explicitly separate local-emulator acceptance from production acceptance.
5. Record QA-only bypasses and the release gate that proves they do not leak into Release.
6. Remove obsolete future-scope wording when work is actually completed.
7. Keep `Assets/Project/Documentation/MASTER_ROADMAP.md` as the canonical repository roadmap.
