# ATLAS BOARD
## Master Architecture & Development Roadmap v5.7

**Canonical checkpoint:** 2026-10-10 (America/Denver)  
**Repository:** `vuartex/AtlasBoardPrototype`  
**Last verified pushed GitHub HEAD:** `d9099673115e91f67873eb717388fd9bdb5e1b43`  
**Last verified pushed HEAD commit:** `Phase 15A-15E: establish security boundary and Steam App Check`  
**Current accepted local checkpoint:** `Phase 15F, 15G and 15H are accepted locally. Phase 15I — Moderation + Data Lifecycle is next.`  
**Phase 12 close commit:** `0eca81450be91b30a5dc1cd66d901c46e2cab07c` — `Phase 12: finalize online resilience hardening and crash recovery`  
**Phase 14 close commit:** `77b4a1c29fe3ee55dbd27f4d35e6ce5e9d02e157` — `Close Phase 14 UI/UX polish and finalize localization/input handling`  
**Unity baseline:** Unity 6000.5.6f1, `Assets/Board_Prototype.unity`, unless a later committed upgrade explicitly changes it.  
**Canonical repository roadmap location:** `Assets/Project/Documentation/MASTER_ROADMAP.md`

This v5.7 checkpoint supersedes the v5.6 sequencing/status checkpoint. The architectural foundations from earlier versions remain valid unless explicitly changed here. The principal update is that Phase 15F, 15G and 15H are now accepted locally with runtime/E2E evidence, and Phase 15I is the next implementation step.

# 1. Source and patch discipline

Before any meaningful source patch:

- Check current GitHub `main` HEAD first.
- GitHub is authoritative only for work already committed and pushed.
- Accepted local work newer than HEAD must not be overwritten by an older GitHub copy.
- Prefer minimal replacement ZIPs.
- Do not add `.cmd`, `.bat` or `.ps1` helper files unless explicitly requested.
- Do not auto-commit or auto-push.
- Do not use `git add .`; stage only intended files.
- Do not stage unrelated historical font changes.
- Firebase Functions changes require TypeScript build before acceptance.
- Run focused lint/tests for the changed backend scope where practical.
- Restart Firebase emulators after backend source changes when emulator runtime behavior is being tested.
- Unity runtime changes require compile/runtime smoke before a meaningful phase close.
- Static review is not equivalent to production runtime acceptance.
- Temporary validators/installers should self-delete after PASS when practical.
- Never place production Steam publisher secrets or App Check debug tokens in Unity source.

Current pushed authority:

- `d9099673115e91f67873eb717388fd9bdb5e1b43` — Phase 15A–15E checkpoint.
- Phase 15F–15H are accepted local work and are not considered pushed until the user commits/pushes them.

# 2. Durable architecture invariants

## 2.1 Canonical identity and provider boundaries

- Canonical Atlas AccountId remains authoritative.
- SeatId remains authoritative inside a match/session.
- SteamID is a verified external provider identity mapped to Atlas AccountId.
- Steam remains an adapter, not the canonical account database.
- Wallet, inventory, progression, lobby, match and history remain Atlas/Firebase-side systems.
- Development Steam AppID `480` must never be treated as the production AtlasBoard AppID.

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

Gameplay state belongs to the Player Seat, not to the current controller.

`Human -> TemporaryBot -> Human` must preserve:

- AccountId / SeatId
- money
- properties
- pawn position
- development
- turn state
- bankruptcy state
- match-result state

## 2.4 Hosting strategy

- Firebase / managed Google Cloud remains the selected backend direction.
- Do not add a self-managed always-on VPS/game server by default.
- Steam does not automatically host AtlasBoard gameplay authority.
- Steam Networking/SDR may be evaluated later as an optional transport optimization.
- The current Firebase + Host-authoritative board-game architecture remains acceptable for the present scope.

## 2.5 App Check invariant

- Editor/development uses Firebase App Check debug provider.
- Desktop standalone production architecture uses the Steam custom App Check provider.
- `platformSteamCreateAppCheckToken` is the App Check bootstrap endpoint and must not require a pre-existing App Check token.
- Production Steam custom App Check runtime acceptance remains deferred until the real AtlasBoard Steam AppID and publisher secret exist.
- App Check tokens have a one-hour TTL; Firebase SDK/provider flow obtains fresh tokens as needed.
- The development debug credential is separate from the one-hour runtime token.
- Any debug credential exposed in chat/screenshots must be revoked/replaced before production close.

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
- Phase 14A–14K UI/UX polish and final input/localization cleanup.
- Phase 15A–15H security hardening accepted to the levels documented below.

# 4. Phase status overview

## Phase 3B — Seat / Turn / AFK
**Status: DONE.**

## Phase 3C — Account & Cloud Profile
**Status: FOUNDATION DONE; production security hardening continues in Phase 15.**

## Phase 3D — Lobby / Session UI
**Status: DONE.**

## Phase 4 — Public Lobby Browser
**Status: DONE.**

## Phase 5 — Online Gameplay Networking
**Status: MAIN PATH DONE / major runtime flows proven.**

## Phase 6 — Chat + Moderation Foundation
**Status: DONE foundation; production moderation/data-lifecycle close remains in Phase 15I.**

## Phase 8 — Gameplay / Content Checkpoint
**Status: DONE.**

## Phase 9 — Store / Economy / Seasonal Event
**Status: DONE foundation; production economy hardening accepted through Phase 15H.**

## Phase 10 — Career / Progression / History / Achievements
**Status: DONE foundation.**

## Phase 11 — Platform / Steam / Multiplayer / Host Migration Foundation
**Status: DONE through 11G.**

Key pushed checkpoints include:

- `11fdcdaba2f37e506994708674b0ee01e62ab867` — 11A/11B platform foundation + Steam integration.
- `8e91db76914db59cbf17f28f788adf24a193fd17` — 11C secure Steam account linking.
- `a5e4b28f442a7ad22b5789096633c8ef7bd141f1` — 11D returning Steam sign-in.
- `a8d777b8eb9e798fec8b5f750185ae428b34ca18` — 11E Steam recovery / achievement resync.
- `6ddf01ec66f0116396d18b27b171b9c6080ee671` — 11F shared multiplayer lobby/reconnect.
- `068ca6f0d1736516ace63b79681227202a408cf8` — 11G Host Migration final parent checkpoint.

## Phase 12 — Resilience Hardening
**Status: DONE / accepted.**

Close commit:

- `0eca81450be91b30a5dc1cd66d901c46e2cab07c`

## Phase 13
**Status: RETIRED AS A SEPARATE IMPLEMENTATION PHASE.**

Host Migration was absorbed into Phase 11G and validated further in Phase 12.

## Phase 14 — Dedicated UI/UX Polish
**Status: DONE / CLOSED / PUSHED.**

Close commit:

- `77b4a1c29fe3ee55dbd27f4d35e6ce5e9d02e157`
- `Close Phase 14 UI/UX polish and finalize localization/input handling`

# 5. Phase 15 — Security and Production Backend Hardening

**Overall status: IN PROGRESS.**  
**Accepted locally through Phase 15H.**

## 15A — Firestore Direct-Client Security Boundary
**Status: DONE / PUSHED in the 15A–15E checkpoint.**

Implemented:

- Deny-by-default Firestore policy for unreviewed collections.
- Restricted owner access for `users/{uid}`, `public_profiles/{uid}`, and `preferences/{uid}`.
- Direct client access denied for server-authoritative wallet, ledger, inventory, commerce, promo, progression, lobby, match and Steam mapping data.
- Admin SDK / Cloud Functions remain trusted authority.

Acceptance:

- Firestore rules E2E: **17 passed, 0 failed**.
- Public lobby + authoritative match smoke passed.

## 15B — Unified Emulator Firebase Session
**Status: DONE / PUSHED in the 15A–15E checkpoint.**

Implemented:

- FirebaseAuth SDK uses Auth Emulator.
- Firestore SDK uses Firestore Emulator.
- SDK identity is unified with the deterministic lobby/manual REST development identity.
- SDK UID and Lobby UID are verified to match.
- Canonical account/profile snapshot drives visible profile presentation.

## 15C — App Check Transport Plumbing
**Status: DONE / PUSHED in the 15A–15E checkpoint.**

Central App Check request-security helper covers:

1. Lobby
2. Match
3. Chat
4. Chat Moderation
5. Meta Economy
6. Progression
7. Steam Account Link

Token available -> `X-Firebase-AppCheck` is attached.

## 15D — Firebase App Check SDK + Editor Debug Provider
**Status: DONE / PUSHED in the 15A–15E checkpoint.**

Accepted evidence:

- Firebase App Check Unity SDK 13.15.0.
- Debug provider configured before Firebase initialization.
- Firebase project/app identity verified.
- Direct debug-token exchange returned HTTP 200 after replacing the original invalid credential.
- Runtime diagnostics:
  - `RequestsSeen=7`
  - `HeadersAttached=7`
  - `SdkUnavailableSkips=0`
  - `TokenUnavailableSkips=0`

## 15E — Windows/Steam Production Custom App Check Provider
**Status: DONE for code/build acceptance / PUSHED; real production runtime acceptance DEFERRED.**

Implemented:

- Desktop standalone custom `IAppCheckProvider`.
- Dedicated Steam Web API ticket identity:
  `atlasboard-appcheck-v1`
- Valve ticket verification before Firebase Admin token mint.
- One-hour custom App Check token TTL.
- Raw Steam tickets and App Check tokens are not logged.
- Bootstrap callable:
  `platformSteamCreateAppCheckToken`
- Bootstrap callable intentionally remains outside App Check enforcement.

Acceptance:

- TypeScript build PASS.
- Focused lint for `app-check-steam.ts` PASS.
- Real production runtime proof remains deferred until:
  - real AtlasBoard Steam AppID;
  - production publisher Web API secret.

## 15F — Staged App Check Enforcement Rollout
**Status: DONE / accepted locally.**

### Callable classification

Current callable inventory:

- **44 protected runtime callables**
- **9 emulator/dev-only exemptions**
- **1 App Check bootstrap exemption**
- **54 total**

Protected runtime endpoints use a central enforcement switch.

### Local staging model

The initial Firebase BooleanParam strategy generated repeated function-discovery warnings. It was replaced with an emulator-safe plain-boolean policy:

- enforcement can be enabled locally with:
  `ATLAS_ENFORCE_APP_CHECK=true`
- it is only honored when `FUNCTIONS_EMULATOR=true`
- production remains OFF at this checkpoint.

This prevents accidental production enforcement before the production Steam path is accepted.

### Negative/exemption acceptance

Local E2E proved:

- Missing App Check on a protected callable -> rejected.
- Emulator/dev exception remains reachable.
- Steam App Check bootstrap remains reachable without a pre-existing App Check token.
- Functions Emulator treats an arbitrary non-empty App Check header as valid, so cryptographic invalid-token rejection cannot be proven locally.

Accepted local result:

`Phase 15F local enforcement E2E PASS. StrictChecks=3/3.`

Deferred production gate:

- Real invalid-token cryptographic rejection must be proven against a deployed App Check-enforced callable before Phase 15K closes.

### Unity positive runtime acceptance

A transport gap was discovered in `AtlasBoardLobbyRuntimeBridge`:

- Public Rooms list path.
- Shared lobby callable path.

These paths were patched to apply App Check before sending requests.

Accepted runtime result:

- protected lobby flow succeeded with enforcement enabled;
- backend verification showed both App Check and Firebase Auth valid;
- public lobby creation succeeded.

Therefore Phase 15F positive Unity enforcement is accepted locally.

## 15G — Callable Authorization + Abuse Hardening
**Status: DONE / accepted locally.**

### Authorization audit

Current callable surface:

- **54 total callables**
- **51 require Firebase Auth**
- **3 intentional auth exceptions**

Intentional exceptions:

1. `platformSteamReturningSignIn`
2. `platformSteamDevReturningSignIn`
3. `platformSteamCreateAppCheckToken`

### New abuse controls

Server-owned rate limiting added to abuse-sensitive, low-frequency paths:

- Lobby creation.
- Lobby join attempt volume.
- Authenticated Steam account linking.

High-frequency polling/heartbeat/snapshot paths are deliberately not put behind these low-frequency limits.

Existing protections retained:

- Invalid room-code attempt blocking.
- Chat cooldown/burst/duplicate limits.
- Chat report rate limits.
- Existing idempotency and ownership checks in lobby/match flows.

### Acceptance

Build PASS.  
Focused lint PASS.  
Callable security audit PASS:

`Callables=54, AuthRequired=51, AuthExempt=3, NewAbuseGuards=5, ExistingJoinAndChatLimits=present.`

Authorization/abuse E2E PASS:

`Checks=6/6`

Verified:

- Auth-less protected callable rejected.
- Non-member lobby snapshot rejected.
- Guest Host-only settings mutation rejected.
- Guest match-start attempt rejected.
- Lobby-create flood limiter triggered.
- Steam-link flood limiter triggered.

A test-data issue using unsupported `roundLimit=25` was corrected to valid `roundLimit=30` so the authorization probe reaches the intended Host-only gate.

## 15H — Economy / Commerce Security
**Status: DONE / accepted locally.**

### Existing invariants audited

Wallet:

- deterministic idempotency IDs;
- atomic balance mutation;
- immutable wallet ledger evidence.

Inventory:

- catalog-authoritative item validation;
- immutable inventory entitlement events;
- replay/payload consistency.

Commerce:

- catalog price is server-read;
- client never supplies authoritative purchase price;
- deterministic purchase transaction IDs;
- duplicate/replay protection;
- owner-only refund;
- charge/refund ledger evidence;
- commerce event evidence.

Promo:

- hashed promo identity;
- exact idempotency event evidence;
- active/window checks;
- global redemption limit;
- per-account limit;
- server-configured reward parsing;
- atomic reward mutation.

### New seasonal transactional hardening

Seasonal challenge, reward-track and limited-item purchase paths previously selected some event/config/catalog state before entering the value-granting Firestore transaction.

Phase 15H adds transactional re-validation of:

- active seasonal runtime;
- event identity;
- ticket currency identity;
- event time window;
- challenge definition/config;
- reward-track tier config;
- seasonal limited-item catalog state;
- limited availability window;
- ticket price;
- item type.

If authoritative config changes between the initial read and the mutation transaction, the value grant is rejected rather than using stale configuration.

### Acceptance

TypeScript build PASS.  
Focused ESLint for `src/economy/seasonal.ts` PASS.  
Economy security audit PASS:

`Wallet ledger/idempotency, inventory catalog authority, commerce server-price/owner/refund evidence, promo limits/replay evidence, and transactional seasonal config revalidation are present.`

Economy/commerce E2E PASS:

`Checks=6/6`

Verified:

- server-authoritative catalog price;
- purchase idempotent replay;
- conflicting idempotency payload rejection;
- non-owner refund rejection;
- refund idempotent replay without double credit;
- inactive catalog item rejection.

A promo audit false-negative caused by a multiline `transaction.create(...)` call was corrected by using a multiline-aware audit expression. No promo source bug was present.

# 6. Remaining Phase 15 execution plan

## 15I — Moderation + Data Lifecycle
**Status: NEXT.**

Goals:

- Review chat read/send authorization.
- Review report target/scope authorization.
- Review moderator-only actions and claim enforcement.
- Validate sanctions cannot be self-issued or escalated by normal users.
- Reconfirm chat/report rate-limit behavior under the hardened callable boundary.
- Define and deploy data lifecycle policy where appropriate.
- Add/verify TTL policy for ephemeral moderation/chat records where supported.
- Establish retention expectations for:
  - chat messages;
  - reports;
  - sanctions;
  - rate-limit state;
  - moderation audit/config data.
- Preserve evidence that must not be deleted prematurely.
- Document privacy/data-retention boundaries.
- Add focused audit/E2E coverage for moderator vs normal-user operations.

## 15J — Production Operations / Environment Hardening
**Status: FUTURE.**

- Secret/config review.
- Development vs staging vs production separation.
- Production-safe Firebase parameter/secret handling.
- Logging review.
- Monitoring.
- Alerting.
- Privacy-safe diagnostics.
- Release-safe HTTP/security configuration.
- Operational failure/runbook expectations.

## 15K — Production Steam / Backend Security Close
**Status: FUTURE / final Phase 15 close gate.**

Requires:

- Real AtlasBoard Steam AppID.
- Production Steam publisher Web API secret.
- Production Steam verification.
- Real Windows/Steam custom App Check runtime acceptance.
- Production App Check enforcement rollout.
- Missing-token rejection proof.
- Cryptographic invalid-token rejection proof.
- Bootstrap exception proof.
- Production Steam achievement/provider configuration review.
- Removal/review of development emulator assumptions from deployed paths.
- Debug-token credential rotation/revocation where needed.
- Final security regression suite.
- Phase 15 roadmap close checkpoint.

# 7. Phase 16 — Build Readiness and QA

**Status: FUTURE after Phase 15.**

## Unity/build quality

- Zero unexplained compile errors.
- Warning cleanup.
- Profiler pass.
- GC allocation review.
- Rendering performance.
- Resolution/aspect testing.
- VSync/FPS QA.
- Input QA.
- Localization/font/glyph QA.
- Development vs release security-setting verification.

## Multiplayer QA

- Multiple physical PCs.
- Host/Guest reversal.
- 2/3/4 players.
- Latency/jitter/loss testing.
- Emulator vs deployed-backend parity.
- Reconnect/host-loss regression.
- Repeated rematch/new-lobby reuse.
- Network Quality HUD verification.

## Pipeline

- Repeatable Development Build profile.
- Release build profile.
- CI compile/test.
- Backend lint/build/E2E.
- Version stamping.
- Artifact naming.
- Steam branch/channel strategy.

# 8. Phase 17 — External / Live Testing

**Status: FUTURE.**

Final external validation should include:

- Real Internet.
- Multiple real accounts.
- Multiple time zones.
- Multiple physical PCs.
- Real AtlasBoard Steam AppID.
- Production-like backend.
- Real Steam provider verification.
- Repeated reconnect/Host loss.
- 3/4-player sessions.
- Chat UTC behavior.
- Release-topology validation.

# 9. Optional backlog — not committed scope

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
- Crash reporting / privacy-safe analytics.
- Feature flags / staged rollout.
- Backend health dashboard.
- Moderation dashboard.

These items must not displace Phase 15 security close, Phase 16 QA or Phase 17 release validation without an explicit priority decision.

# 10. Revised execution order from v5.7

1. **Phase 14 — COMPLETE / CLOSED / PUSHED.**
2. **Phase 15A–15E — COMPLETE at documented acceptance level / PUSHED at `d909967...`.**
3. **Phase 15F — COMPLETE locally: staged App Check enforcement.**
4. **Phase 15G — COMPLETE locally: callable authorization + abuse hardening.**
5. **Phase 15H — COMPLETE locally: economy/commerce security.**
6. **Phase 15I — NEXT: moderation + data lifecycle.**
7. **Phase 15J — production operations/environment hardening.**
8. **Phase 15K — real Steam/backend security close.**
9. **Phase 16 — build readiness / QA / pipeline.**
10. **Phase 17 — external live validation.**
11. **Release candidate / Steam launch preparation.**

# 11. Immediate next action

The next implementation step is:

**Phase 15I — Moderation + Data Lifecycle.**

Start with source inspection rather than assumptions:

1. Inspect the current local chat/moderation backend and existing moderation E2E.
2. Preserve the accepted Phase 15F–15H local changes.
3. Inventory chat/report/sanction/config collections and moderator operations.
4. Verify normal-user vs moderator authorization.
5. Review existing cooldown/burst/report limits.
6. Define retention/TTL policy without deleting required evidence.
7. Add focused audit + E2E.
8. Keep production Steam/App Check final gates deferred to 15K.

# 12. Current checkpoint summary

## Green / accepted

- Phase 14 complete and pushed.
- Phase 15A Firestore direct-client boundary.
- Phase 15B unified emulator identity.
- Phase 15C App Check transport plumbing.
- Phase 15D Editor debug App Check.
- Phase 15E Steam production custom-provider architecture/code/build.
- Phase 15F staged local enforcement and positive Unity runtime.
- Phase 15G callable authorization + abuse hardening.
- Phase 15H economy/commerce security.
- Missing App Check rejection proven locally.
- Protected Unity lobby operation proven with App Check + Auth valid.
- 54-callable auth inventory completed.
- Authorization/abuse E2E 6/6.
- Economy/commerce E2E 6/6.
- Existing server-price, ledger, idempotency and promo controls audited.
- Seasonal stale-config mutation risk hardened transactionally.

## Yellow / deferred

- Real production Steam App Check runtime proof.
- Real AtlasBoard Steam AppID.
- Production Steam publisher secret.
- Cryptographic invalid App Check token rejection against deployed backend.
- Production App Check enforcement rollout.
- Existing full-backend lint debt in older lobby/match files.
- Phase 15I–15K.

## Production/later

- Moderation/data-lifecycle close.
- Monitoring/alerting/environment separation.
- Production Steam verification.
- Final security regression.
- Phase 16 QA.
- Phase 17 live testing.

# 13. Next-chat handoff block

A future AtlasBoard chat should start from these facts:

- Canonical roadmap checkpoint: **v5.7, 2026-10-10 America/Denver**.
- Repository: `vuartex/AtlasBoardPrototype`.
- Last verified pushed GitHub HEAD:
  `d9099673115e91f67873eb717388fd9bdb5e1b43`
- Pushed HEAD title:
  `Phase 15A-15E: establish security boundary and Steam App Check`
- Phase 15A–15E are pushed and accepted at the documented level.
- Phase 15F–15H are accepted locally and must be preserved if GitHub still lacks them.
- **Phase 15I — Moderation + Data Lifecycle is next.**
- Do not auto-commit or auto-push.
- Before meaningful source changes, inspect GitHub `main`, then preserve newer local Phase 15F–15H work.
- App Check callable inventory:
  - 44 protected runtime callables;
  - 9 emulator/dev exemptions;
  - 1 Steam App Check bootstrap exemption;
  - 54 total.
- Local staged App Check enforcement is controlled by `.env.local`:
  `ATLAS_ENFORCE_APP_CHECK=true`
  and is honored only in the Functions Emulator.
- Production enforcement is deliberately still OFF.
- `platformSteamCreateAppCheckToken` must remain App Check-exempt because it bootstraps the token.
- Functions Emulator is not authoritative for cryptographic invalid-token verification; deployed invalid-token rejection remains a 15K gate.
- A Phase 15F transport gap in `AtlasBoardLobbyRuntimeBridge` was fixed so Public Rooms and shared lobby callable paths apply App Check before send.
- Positive Unity enforcement passed: protected lobby creation succeeded with backend App Check + Firebase Auth valid.
- Phase 15G accepted:
  - 54 callable audit;
  - 51 auth-required;
  - 3 intentional auth exemptions;
  - new server-owned rate limits for lobby creation, lobby join volume, and authenticated Steam linking;
  - authorization/abuse E2E PASS 6/6.
- Phase 15H accepted:
  - wallet ledger/idempotency audited;
  - inventory catalog authority audited;
  - commerce server-price/owner/refund evidence audited;
  - promo limit/replay evidence audited;
  - seasonal event/config/catalog revalidated inside value-granting Firestore transactions;
  - economy/commerce E2E PASS 6/6.
- Real Steam production App Check still cannot close until the real AtlasBoard Steam AppID and publisher Web API secret are available.
- Development AppID `480` remains development-only.
- Exposed App Check debug credentials should be revoked/replaced privately before production close.
- Firebase/managed Google Cloud remains the hosting direction.
- Do not add a self-managed dedicated server by default.
- Canonical full-tablet UI geometry remains `TabletUIManager.tabletRoot`.
- Gameplay managers remain authoritative.
- EN/TR/ES/FR/DE/KO/RU remains the localization target.
- Rejected world-space P1/P2/P3/P4 ownership badges stay removed.
- Do not add `.cmd/.bat/.ps1` helpers unless explicitly requested.

# 14. Roadmap maintenance rule

At every meaningful phase checkpoint:

1. Commit/push accepted source when the user chooses.
2. On the next roadmap update, record the latest already-existing pushed SHA/title.
3. Never invent the SHA of a commit that does not yet exist.
4. Update phase acceptance evidence and deferred gates.
5. Remove obsolete future-scope items rather than duplicating completed work.
6. Keep `Assets/Project/Documentation/MASTER_ROADMAP.md` as the canonical repository roadmap.
