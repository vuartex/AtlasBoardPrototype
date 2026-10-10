# ATLAS BOARD
## Master Architecture & Development Roadmap v5.6

**Canonical checkpoint:** 2026-10-10 (America/Denver)  
**Repository:** `vuartex/AtlasBoardPrototype`  
**Last verified pushed GitHub HEAD:** `77b4a1c29fe3ee55dbd27f4d35e6ce5e9d02e157`  
**Last verified pushed HEAD commit:** `Close Phase 14 UI/UX polish and finalize localization/input handling`  
**Last verified pushed verification date:** 2026-10-10 (America/Denver)  
**Current accepted local checkpoint:** `Phase 15A–15E security/App Check foundation accepted locally. Phase 15E code/build acceptance is complete; real production Steam attestation acceptance is deferred until the real AtlasBoard Steam AppID and publisher secret are available. Phase 15F staged App Check enforcement is next.`  
**Phase 12 close commit:** `0eca81450be91b30a5dc1cd66d901c46e2cab07c` — `Phase 12: finalize online resilience hardening and crash recovery`  
**Phase 14 close commit:** `77b4a1c29fe3ee55dbd27f4d35e6ce5e9d02e157` — `Close Phase 14 UI/UX polish and finalize localization/input handling`  
**Unity baseline:** Unity 6000.5.6f1, `Assets/Board_Prototype.unity`, unless a later committed upgrade explicitly changes it.  
**Canonical repository roadmap location:** `Assets/Project/Documentation/MASTER_ROADMAP.md`

This v5.6 checkpoint supersedes the v5.5 status/sequencing checkpoint. Detailed accepted gameplay, networking, resilience and Phase 14 UI history from v5.5 remains valid unless explicitly changed here. The main change in v5.6 is the accepted Phase 15A–15E security foundation and the revised remaining Phase 15 execution order.

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

Current pushed authority:

- `77b4a1c29fe3ee55dbd27f4d35e6ce5e9d02e157` — Phase 14 close.
- Phase 15A–15E currently remain local until the user creates and pushes the next checkpoint.

# 2. Durable architecture invariants

## 2.1 Canonical identity and provider boundaries

- Canonical Atlas AccountId remains authoritative.
- SeatId remains authoritative inside a match/session.
- SteamID is a verified external provider identity mapped to Atlas AccountId.
- Steam must remain an adapter, not the canonical account database.
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
- Reconnect state.
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

## 2.5 Phase 14 UI invariants

- Full gameplay-tablet geometry uses `TabletUIManager.tabletRoot`.
- Do not use narrow inner decision/result panels as the geometry owner for full-tablet layouts.
- Existing gameplay managers remain authoritative; presentation wrappers mirror/call them.
- While a gameplay tablet is open, world City Information clicks do not pass through.
- While the pointer is interacting with City Information ScrollRect/card, that UI owns wheel/drag input and board-camera mouse controls must not consume the same input.
- Generic presentation labels such as `Bölge N` must resolve from canonical semantics into the active locale.
- Required localization target remains EN/TR/ES/FR/DE/KO/RU.
- Rejected world-space P1/P2/P3/P4 ownership badges stay removed.

# 3. Accepted foundation summary

The following are accepted foundations and must not be casually rewritten:

- Core board gameplay, dice, movement and economy.
- Auction, Trade, Development, bankruptcy and Event/Special systems.
- Bots and player-seat architecture.
- Camera/audio/settings.
- Seven-language localization.
- Private/public lobby flow.
- Public lobby browser and Join by Code.
- Host-authoritative online gameplay.
- Lobby + Match Chat and moderation/report foundation.
- Store/economy/seasonal event foundation.
- Career/progression/history/achievement foundation.
- Provider-neutral platform contracts.
- Steamworks.NET integration.
- Secure Steam account linking.
- Returning Steam sign-in and Steam recovery.
- Shared development backend.
- Same-match reconnect preservation.
- Graceful Host Migration and crash failover.
- Phase 12 resilience hardening.
- Phase 14A–14K dedicated UI/UX polish and final input/localization cleanup.

# 4. Phase status overview

## Phase 3B — Seat / Turn / AFK
**Status: DONE / accepted foundation.**

## Phase 3C — Account & Cloud Profile
**Status: FOUNDATION DONE; production security hardening is Phase 15.**

## Phase 3D — Lobby / Session UI
**Status: DONE.**

## Phase 4 — Public Lobby Browser
**Status: DONE.**

## Phase 5 — Online Gameplay Networking
**Status: MAIN PATH DONE / major runtime flows proven.**

## Phase 6 — Chat + Moderation Foundation
**Status: DONE foundation; production authorization/retention review remains in Phase 15.**

## Phase 8 — Gameplay / Content Checkpoint
**Status: DONE.**

## Phase 9 — Store / Economy / Seasonal Event
**Status: DONE foundation; production commerce hardening remains in Phase 15.**

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

Accepted through 14K:

- City Information.
- Network Quality diagnostics + Pause.
- Canonical profile header.
- HUD alignment/typography.
- Match Result and exit flow.
- Auction polish.
- Trade polish.
- Development polish.
- Responsive/seven-language regression.
- Controller/accessibility pass.
- Purchase/Special/localization polish.
- Final generic-label localization semantics.
- City Information vs board-camera input ownership.
- Rejected ownership badges removed.

# 5. Phase 15 — Security and Production Backend Hardening

**Overall status: IN PROGRESS.**

## 15A — Firestore Direct-Client Security Boundary
**Status: DONE / accepted locally.**

Implemented:

- Deny-by-default Firestore policy for unreviewed collections.
- Direct Unity client access retained only for deliberately reviewed user-facing documents.
- `users/{uid}` owner access with restricted self-editable fields.
- `public_profiles/{uid}` authenticated reads / owner writes.
- `preferences/{uid}` owner access with restricted fields.
- Direct client access denied for wallet, ledger, inventory, commerce, promo, progression authority, lobby/match authority, Steam mappings and other server-owned state.
- Admin SDK / Cloud Functions remain trusted authority.

Acceptance:

- Firestore rules E2E: **17 passed, 0 failed**.
- Public lobby + authoritative match smoke worked without permission errors.

## 15B — Unified Emulator Firebase Session
**Status: DONE / accepted locally.**

Implemented:

- FirebaseAuth SDK uses Auth Emulator.
- Firestore SDK uses Firestore Emulator.
- SDK identity is unified with the deterministic lobby/manual REST development identity.
- SDK UID and Lobby UID are verified to match.
- Missing preferences are created only when required.
- Canonical account/profile snapshot drives visible profile presentation.
- Main Menu, Settings/Profile, Lobby and Store presentation races were repaired.
- Local lobby seat presentation uses the canonical display name.

Accepted presentation:

- Main Menu: canonical display name with level.
- Settings/Profile modal: canonical display name.
- Lobby/Store top-left profile presentation remains synchronized.

## 15C — App Check Transport Plumbing
**Status: DONE / accepted locally.**

Implemented a centralized App Check request-security helper used by the manual HTTP transport bridges.

Coverage includes:

1. Lobby
2. Match
3. Chat
4. Chat Moderation
5. Meta Economy
6. Progression
7. Steam Account Link

Behavior:

- SDK absent -> request continues without App Check.
- SDK present but token unavailable -> request continues while enforcement is still OFF.
- Token available -> `X-Firebase-AppCheck` is attached.
- No callable enforcement was enabled at this stage.

Initial transport validation passed at 7/7 coverage.

## 15D — Firebase App Check SDK + Editor Debug Provider
**Status: DONE / accepted locally.**

Implemented and verified:

- Firebase App Check Unity SDK 13.15.0 imported.
- Early provider configuration occurs before Firebase service initialization.
- Editor debug token can be read from process environment or Windows User environment.
- Debug token value is never logged by AtlasBoard.
- Firebase App ID verified:
  `1:962084427661:android:3e8f20b1ddfe0da4d68fc4`
- Project ID verified:
  `atlasboard-usa`
- Firebase Console app and Unity runtime App ID match.
- Direct `exchangeDebugToken` probe eventually returned HTTP 200 after replacing the original invalid/mismatched debug token.
- Normal Firebase App Check SDK token acquisition then worked.

Final runtime acceptance evidence:

- `SDK=True`
- `DebugProviderConfigured=True`
- `RequestsSeen=7`
- `HeadersAttached=7`
- `SdkUnavailableSkips=0`
- `TokenUnavailableSkips=0`

Therefore the Editor development path is proven end-to-end:

`registered debug secret -> Firebase App Check token -> X-Firebase-AppCheck -> AtlasBoard backend request`

Security note:

- Debug tokens are development credentials.
- Any debug token exposed in chat/screenshots should be revoked and replaced before long-term/shared use.
- Debug tokens must never be embedded in source or production builds.

## 15E — Windows/Steam Production Custom App Check Provider
**Status: DONE for code/build acceptance; real production runtime acceptance DEFERRED until real Steam credentials exist.**

Implemented architecture:

### Unity / desktop standalone

- Editor keeps the Phase 15D Debug App Check provider.
- Desktop standalone builds use `AtlasBoardSteamAppCheckProviderFactory`.
- Provider implements Firebase Unity custom `IAppCheckProvider`.
- Firebase Unity 13.15 `GetLimitedUseTokenAsync()` contract is implemented with the standard-token fallback because AtlasBoard does not currently require replay-protected limited-use tokens.
- App Check provider requests a fresh Steam Web API ticket with dedicated identity:
  `atlasboard-appcheck-v1`
- Steam proof ticket is cancelled after use.
- Raw Steam ticket is not logged or persisted.
- Returned App Check token is not logged.
- Custom provider validates Firebase App ID, Steam AppID and SteamID returned by the server.
- App Check token expiry is passed back to Firebase SDK so SDK caching/refresh can operate normally.

### Backend

New callable:

- `platformSteamCreateAppCheckToken`

Security flow:

1. Desktop client obtains a fresh Steam Web API ticket.
2. Backend calls Valve `ISteamUserAuth/AuthenticateUserTicket/v1`.
3. Valve proof must match configured Steam AppID.
4. Invalid or publisher-banned proof is rejected.
5. Firebase Admin App Check mints a custom App Check token.
6. Token TTL is **1 hour**.
7. Client returns the token to Firebase App Check SDK.
8. Existing Phase 15C transport attaches `X-Firebase-AppCheck` to protected backend requests.

The token-acquisition endpoint intentionally does **not** require App Check because it is the bootstrap path that creates the App Check token.

The endpoint also does not require Firebase Auth as proof-of-app authenticity comes from verified Steam evidence. Atlas canonical account authentication remains a separate concern for account-authorized endpoints.

### 15E acceptance completed now

- Unity compile issue caused by Firebase 13.15 `GetLimitedUseTokenAsync()` requirement was fixed.
- One-time Phase 15E installer validation was corrected and passed.
- Installer removed itself after success; no permanent temporary Editor installer remains.
- `index.ts` exports `platformSteamCreateAppCheckToken`.
- `app-check-steam.ts` exists.
- `AtlasBoardSteamAppCheckProvider.cs` exists.
- TypeScript `npm.cmd run build` — **PASS**.
- Focused `npx.cmd eslint "src/platform/app-check-steam.ts"` — **PASS**.
- Full backend lint still reports 15 pre-existing errors in:
  - `src/lobby/lobby.ts`
  - `src/match/lifecycle.ts`
  - `src/match/network.ts`
- Those full-lint errors are not introduced by Phase 15E.

### 15E deferred acceptance

A real production Steam attestation cannot yet be considered proven because AtlasBoard still lacks the final live Steam configuration in this checkpoint:

- Real AtlasBoard Steam AppID.
- Production publisher Web API secret.

Development AppID `480` is not accepted as production evidence.

Once the real credentials are available, the production runtime proof must validate:

`Steam standalone client -> fresh Web API ticket -> Valve verification -> Firebase Admin custom App Check token -> Firebase SDK -> X-Firebase-AppCheck -> protected backend request`

### Enforcement status after 15E

**App Check callable enforcement remains OFF intentionally.**

Do not enable broad `enforceAppCheck:true` until 15F classifies endpoints and provides safe staged enforcement tests.

# 6. Remaining Phase 15 execution plan

## 15F — Staged App Check Enforcement Rollout
**Status: NEXT.**

Goals:

- Inventory callable endpoints.
- Classify endpoints as:
  - normal authenticated/protected runtime endpoints;
  - bootstrap/token-acquisition endpoints;
  - emulator/dev-only endpoints.
- Keep `platformSteamCreateAppCheckToken` outside App Check enforcement because it creates the initial token.
- Add positive/negative App Check tests:
  - valid token accepted;
  - missing token rejected where enforcement applies;
  - invalid token rejected;
  - bootstrap endpoint remains usable without pre-existing App Check token.
- Introduce enforcement in a staged/reversible way rather than blindly flipping every callable at once.
- Preserve local emulator development workflow.

## 15G — Callable Authorization + Abuse Hardening
**Status: FUTURE.**

- Least-privilege callable authorization review.
- Account/seat/lobby/match ownership checks.
- Host-only action review.
- Rate-limit / spam-abuse review.
- Idempotency review.
- Replay-sensitive action review.

## 15H — Economy / Commerce Security
**Status: FUTURE.**

- Wallet authority.
- Immutable ledger/audit trail review.
- Purchase replay/duplicate prevention.
- Inventory entitlement authority.
- Catalog authorization.
- Promo/event reward authorization.
- Seasonal ticket/reward integrity.

## 15I — Moderation + Data Lifecycle
**Status: FUTURE.**

- Chat authorization/rate-limit review.
- Report/sanction authority.
- Moderation operations requirements.
- TTL/retention deployment.
- Privacy/data-retention documentation.

## 15J — Production Operations / Environment Hardening
**Status: FUTURE.**

- Secret/config review.
- Development vs staging vs production separation.
- Logging.
- Monitoring.
- Alerting.
- Privacy-safe operational diagnostics.
- Release-safe HTTP/security configuration.

## 15K — Production Steam / Backend Security Close
**Status: FUTURE / final Phase 15 close gate.**

Requires:

- Real AtlasBoard Steam AppID.
- Production Steam publisher Web API secret.
- Production Steam verification.
- Real Windows/Steam custom App Check runtime acceptance.
- Final App Check enforcement acceptance.
- Invalid/missing token rejection proof.
- Production Steam achievement/provider configuration review.
- Removal of development emulator assumptions from deployed paths.
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

The previous Colorado/Turkey two-machine tests proved the shared development architecture. Phase 17 validates the release topology rather than merely repeating emulator experiments.

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

# 10. Revised execution order from v5.6

1. **Phase 14 — COMPLETE / CLOSED / PUSHED at `77b4a1c...`.**
2. **Phase 15A–15E — DONE locally at the defined acceptance level.**
3. **Phase 15F — NEXT: staged App Check enforcement.**
4. **Phase 15G — callable authorization + abuse/idempotency.**
5. **Phase 15H — economy/commerce security.**
6. **Phase 15I — moderation + retention/privacy.**
7. **Phase 15J — production operations/environment hardening.**
8. **Phase 15K — real Steam/backend security close.**
9. **Phase 16 — build readiness / QA / pipeline.**
10. **Phase 17 — external live validation.**
11. **Release candidate / Steam launch preparation.**

# 11. Immediate next action

The next implementation step is:

**Phase 15F — Staged App Check Enforcement Rollout.**

Do not begin by changing every callable to `enforceAppCheck:true`.

First:

1. Inventory the current callable surface.
2. Identify bootstrap/dev exceptions.
3. Add positive and negative App Check tests.
4. Stage enforcement safely.
5. Preserve emulator development workflow.
6. Keep production Steam acceptance deferred until real credentials are available.

# 12. Current checkpoint summary

## Green / accepted

- Phase 14 complete and pushed.
- Phase 15A Firestore boundary.
- Phase 15B unified emulator identity.
- Phase 15C App Check transport plumbing.
- Phase 15D Editor debug App Check end-to-end.
- Phase 15E Steam production custom-provider architecture.
- Phase 15E TypeScript build.
- Phase 15E focused lint.
- Temporary diagnostic/install tools cleaned after use.
- Debug provider and Steam provider are separated by build context.
- App Check transport is capable of attaching a valid token.
- Production enforcement remains deliberately disabled until staged rollout.

## Yellow / deferred

- Real production Steam App Check runtime proof.
- Real AtlasBoard Steam AppID.
- Production Steam publisher secret.
- Existing full-backend lint debt in lobby/match files.
- Phase 15F–15K.

## Production/later

- Full App Check enforcement.
- Final least-privilege callable review.
- Economy/commerce security close.
- Moderation/retention close.
- Monitoring/alerting/environment separation.
- Production Steam verification.
- Phase 16 QA.
- Phase 17 live testing.

# 13. Next-chat handoff block

A future AtlasBoard chat should start from these facts:

- Canonical roadmap checkpoint: **v5.6, 2026-10-10 America/Denver**.
- Last verified pushed GitHub HEAD:
  `77b4a1c29fe3ee55dbd27f4d35e6ce5e9d02e157`
- Pushed HEAD title:
  `Close Phase 14 UI/UX polish and finalize localization/input handling`
- Phase 14 is closed.
- Phase 15A–15E are accepted locally at the documented acceptance level.
- Phase 15E production runtime acceptance is deferred until real Steam AppID/publisher credentials exist.
- **Phase 15F staged App Check enforcement is next.**
- Do not broadly enable `enforceAppCheck:true` before endpoint classification and negative tests.
- `platformSteamCreateAppCheckToken` is an App Check bootstrap endpoint and must not require a pre-existing App Check token.
- Editor App Check uses the registered debug provider.
- Desktop standalone production architecture uses the Steam custom App Check provider.
- Steam custom provider uses ticket identity:
  `atlasboard-appcheck-v1`
- Backend verifies Steam evidence with Valve before Firebase Admin mints a custom App Check token.
- Custom App Check token TTL is 1 hour.
- The Firebase SDK/provider flow is responsible for obtaining fresh App Check tokens as required; the user does not manually recreate a token every hour.
- Development debug token is a separate long-lived debug credential until revoked; it is not the one-hour App Check token.
- AppID 480 remains development-only.
- Current hosting decision remains Firebase/managed Google Cloud.
- Do not add a self-managed dedicated server by default.
- Canonical full-tablet UI geometry remains `TabletUIManager.tabletRoot`.
- Gameplay managers remain authoritative.
- City Information owns wheel/drag while pointer interaction is over its ScrollRect/card.
- EN/TR/ES/FR/DE/KO/RU remains the localization target.
- Rejected world-space P1/P2/P3/P4 ownership badges stay removed.
- Do not add `.cmd/.bat/.ps1` helpers unless explicitly requested.
- Do not auto-commit/push.
- Before every meaningful patch, inspect current GitHub `main` and preserve newer accepted local work.

# 14. Roadmap maintenance rule

At every meaningful phase checkpoint:

1. Commit/push accepted source when the user chooses.
2. On the next roadmap update, record the latest already-existing pushed SHA/title.
3. Never invent the SHA of a commit that does not yet exist.
4. Update phase acceptance evidence and deferred gates.
5. Remove obsolete future-scope items rather than duplicating completed work.
6. Keep `Assets/Project/Documentation/MASTER_ROADMAP.md` as the canonical repository roadmap.
