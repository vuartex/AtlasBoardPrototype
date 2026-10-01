# ATLAS BOARD
## Master Architecture & Development Roadmap v5.3

**Canonical checkpoint:** 2026-09-30 (America/Denver)  
**Repository:** `vuartex/AtlasBoardPrototype`  
**Last verified pushed GitHub HEAD:** `0eca81450be91b30a5dc1cd66d901c46e2cab07c`  
**Last verified pushed HEAD commit:** `Phase 12: finalize online resilience hardening and crash recovery`  
**Last verified pushed verification date:** 2026-09-30 (America/Denver)  
**Current accepted local checkpoint:** `Phase 14A–14D accepted locally; Unity 6.5 warning/menu cleanup accepted; pending commit/push`  
**Phase 12 close commit:** `0eca81450be91b30a5dc1cd66d901c46e2cab07c` — `Phase 12: finalize online resilience hardening and crash recovery`  
**Unity baseline:** Unity 6000.5.6f1, `Assets/Board_Prototype.unity`, unless a later committed upgrade explicitly changes it.  
**Canonical repository roadmap location:** `Assets/Project/Documentation/MASTER_ROADMAP.md`  
**Previous human-readable archive copy:** `AtlasBoard_Master_Roadmap_v5_0_2026-09-27.docx`

This v5.3 document supersedes Master Roadmap v5.2 dated 2026-09-30. It retains the accepted Phase 12 resilience checkpoint and records the locally accepted Phase 14A–14D UI/UX work: clickable City Information, provider-neutral Network Quality diagnostics, canonical profile-header identity, P1/P2/P3/P4 HUD alignment, Unity editor-menu organization, and targeted Unity 6.5 warning cleanup. These Phase 14 changes are accepted locally and are pending the next GitHub commit/push.

The most important planning change from v5.2 is that Phase 14A–14D now have accepted local implementation evidence. The old standalone Phase 13 implementation milestone remains retired because Host Migration was absorbed into Phase 11G. Phase 14 remains active: the next subphase is Phase 14E Final HUD Typography & Responsive Polish.

# 1. How to use this roadmap

## 1.1 Source precedence rule

Before any source patch:

- Check the current GitHub `main` HEAD first.
- If GitHub contains all latest accepted work, GitHub is the authoritative source baseline.
- If meaningful local work exists after HEAD, use only the actually available current local files for touched areas.
- Never reconstruct a risky patch from an older guessed source when a newer accepted local source exists.
- Never overwrite accepted local fixes with an older GitHub file.
- After a meaningful phase close, commit and push before starting the next large subsystem whenever practical.
- This roadmap records architecture, status, acceptance evidence, open scope, and workflow. It does not override newer source code.
- If the HEAD recorded at the top of this document differs from the repository's current HEAD, inspect the newer commits before editing code or updating this roadmap.

Current authoritative source checkpoint:

- `0eca81450be91b30a5dc1cd66d901c46e2cab07c` — Phase 12 final resilience checkpoint (12A through 12E), committed and pushed.
- Local working tree after this pushed HEAD contains accepted Phase 14A–14D UI/UX changes and targeted Unity 6.5 warning/menu cleanup. These changes must be committed/pushed before beginning the next large UI subphase.
- `068ca6f0d1736516ace63b79681227202a408cf8` — Phase 11G final parent checkpoint.
- `6ddf01ec66f0116396d18b27b171b9c6080ee671` — Phase 11F shared multiplayer lobby/reconnect.
- `a8d777b8eb9e798fec8b5f750185ae428b34ca18` — Phase 11E Steam recovery/achievement resync.
- `a5e4b28f442a7ad22b5789096633c8ef7bd141f1` — Phase 11D returning Steam sign-in.
- `8e91db76914db59cbf17f28f788adf24a193fd17` — Phase 11C secure Steam account linking.
- `11fdcdaba2f37e506994708674b0ee01e62ab867` — Phase 11A/11B platform foundation + Steam client integration.
- `ebe72662bf058e18df123d4cad06e8161a04a092` — Phase 10 progression/career checkpoint.
- `8223ad419fb09cffc5d0b7924a751a99088ba8a7` — Phase 9 storefront/seasonal checkpoint.

## 1.2 Patch delivery and testing rule

- Prefer downloadable minimal replacement ZIPs for source patches.
- Do not add `.cmd`, `.bat`, or `.ps1` helper files unless explicitly requested.
- Do not auto-commit or auto-push; Git operations are controlled by the user.
- Do not use `git add .`; stage only intended files.
- Do not stage unrelated historical font changes.
- Unity runtime C# changes require a fresh Guest Development Build before two-client validation.
- Firebase Functions changes require TypeScript build first; lint/E2E should be run when the affected backend scope has tests.
- Restart Firebase emulators after backend source changes so the running Functions process actually serves the new build.
- Static/source review is not equivalent to Unity compile, standalone build, or two-client runtime acceptance.
- Fix the observed concrete error first before broadening scope.

## 1.3 Roadmap sequencing rule

When the user asks for the next phase, use this v5.3 sequence unless an explicit blocker or new product requirement changes priority. Avoid repeating work already accepted in prior phases. A later regression does not automatically reopen a completed phase; repair the regression in the current hardening/QA phase unless architecture actually changed.

# 2. Canonical architecture principles

## 2.1 Seat-owned gameplay state

Gameplay state belongs to a Player Seat, not to whichever controller currently owns that seat.

A seat owns:

- Canonical AccountId linkage.
- SeatId and stable PlayerSlotIndex.
- Money and net worth.
- Owned properties.
- Pawn position.
- Development levels.
- Turn/scheduler state.
- Bankruptcy state.
- Match-result state.
- Other authoritative match state.

Controller is replaceable:

- Human.
- TemporaryBot.
- PermanentBot.

Connection state is separate:

- Connected.
- Reconnecting.
- Left.
- AFK Removed.
- Kicked.

Core invariant: `Human -> TemporaryBot -> Human` reuses the same seat and authoritative state. A reconnect, Host transfer, controller change, or client restart must not recreate money, ownership, development, pawn position, round, or match identity.

## 2.2 Provider-neutral identity and multiplayer

Steam is an adapter, not the canonical Atlas identity.

- Canonical Atlas AccountId remains authoritative.
- SeatId remains authoritative inside a match/session.
- SteamID is a verified provider identity mapped to Atlas AccountId.
- Wallet, inventory, progression, lobby, match, and history remain Atlas/Firebase-side systems.
- Steam, and later Game Center / Play Games / other providers, project into these contracts rather than replacing them.

## 2.3 Data-driven map/content architecture

Gameplay code remains generic. Turkey, Colorado, USA, city, state, and country names are content/data, not gameplay branches.

Required preserved content:

- Turkey includes Manisa.
- Colorado includes Longmont.
- USA includes Colorado.
- Current board-size assumptions remain explicit until board-size abstraction is a dedicated milestone.

## 2.4 Server authority

Use backend/server authority wherever shared truth or account value matters:

- Account identity and provider mappings.
- Wallet/premium economy.
- Inventory entitlements.
- Promo/event rewards.
- Lobby/session membership.
- Match authority and authority transfer.
- Presence/reconnect state.
- Chat timestamp/rate limiting/moderation.
- Reports and sanctions.
- Completed-match progression, XP, achievements, history evidence.
- Future ranked/competitive outcomes.

## 2.5 Safe authority handoff

Host transfer is an authoritative state-machine operation, not a cosmetic label swap.

Required invariants now implemented:

- Deterministic new Host selection.
- Safe gameplay checkpoint before graceful transfer.
- Hidden turn scheduler state is included in handoff.
- Crash failover does not promote from a half-executed dice/movement/decision block.
- Authority epoch/revision continuity prevents ambiguous leadership.
- Old Host seat becomes reconnectable state, not a new player.
- Bots execute on the current Host only.
- Result/Rematch/Leave authority follows the new Host.

## 2.6 Hosting / transport strategy

Current architectural decision:

- Atlas Board does **not** require a self-managed dedicated game server for the current board-game scope.
- Firebase remains the canonical shared backend for account identity, lobby/session state, authoritative match snapshots, intents, reconnect state, chat, progression, Store/Career data and cross-platform persistence.
- Firebase is a managed/serverless backend; prefer Firebase/Google Cloud managed services over operating a custom VPS/game-server fleet unless a future gameplay requirement clearly demands one.
- Steam remains a platform adapter for identity, invites, presence, achievements and future optional Steam-native networking. Steam is **not** assumed to automatically host Atlas Board gameplay servers.
- Steam Datagram Relay / Steam Networking may be evaluated later for a Steam-only transport optimization, but it is not required for the current Firebase-authoritative architecture.
- If Steam P2P/SDR is introduced, it must not replace canonical Atlas AccountId/SeatId/Firebase authority contracts without a dedicated architecture milestone.
- Google Play / Android builds should use the same Firebase backend rather than depending on Steam networking.
- Cross-platform Steam <-> Android multiplayer is optional. The provider-neutral identity/backend architecture keeps it possible, but the project may ship platform-separated matchmaking if cross-platform networking/identity complexity is not justified.
- In the current Host-authoritative simulation, players synchronize through Firebase. The current Host's Internet quality still affects responsiveness because the Host client executes authoritative gameplay and publishes authoritative state through Firebase.
- Removing Host-quality dependency entirely would require moving gameplay simulation authority to managed server-side execution such as Cloud Run or another dedicated authoritative service. This is not currently required for Atlas Board's low-frequency board-game interactions.
- Do not add a self-managed always-on server/VPS by default.

# 3. Non-regression baseline

The following are accepted foundations and should not be casually rewritten:

- Core board gameplay.
- Physical 2d6 dice and synchronized visual/result flow.
- Starting order and tie architecture.
- Doubles and triple-double rules.
- Balanced Development rule.
- Economy, purchase, rent, ownership.
- Auction.
- Trade.
- Development and development visuals.
- Bankruptcy core gameplay and accepted liquidation rules.
- Event Deck v3 with 36 cards.
- Special tiles.
- Bot foundation and personalities.
- Pawn motion/customization/spacing.
- Camera controls/collision.
- Audio/settings foundation.
- Seven-language localization: EN/TR/ES/FR/DE/KO/RU.
- Korean font fallback foundation.
- Private/public lobby architecture.
- Public lobby browser and dedicated Join by Code UX.
- Human/Bot seat mapping.
- Host-authoritative online gameplay.
- Human HUD authority label.
- Lobby + Match Chat.
- Chat unread count.
- Phase 6B moderation/report system.
- Phase 9 Store/Economy/Seasonal Event systems.
- Phase 10 Career/XP/statistics/history/achievements.
- Provider-neutral Phase 11 platform contracts.
- Steamworks.NET client integration.
- Secure Steam account-linking architecture.
- Returning Steam sign-in and live Steam recovery.
- Shared development backend over private remote networking.
- Same-match leave/rejoin state preservation.
- Graceful Host Migration and crash failover.
- Stable local-emulator development identity.
- Authoritative bankruptcy replication and bankrupt pawn removal.
- Fair bankruptcy liquidation: minimum required cheapest property transfer, remainder released.
- Guest Result Leave behavior.
- Host Rematch and authority-sensitive result actions.
- Clean second-match/session reuse.
- Dice reuse after tested rematch/new-lobby path.
- Rest + doubles extra-roll suppression.
- Board walnut edge + warm light center across environment themes.

# 4. Foundation phases - current status

## 4.1 Core gameplay and presentation

**Status: DONE / accepted foundation.**

Includes board gameplay, dice, economy, property, bots, auction, trade, development, bankruptcy, Event Deck, special tiles, camera, themes, audio, settings, localization, pawn customization and motion.

## 4.2 Phase 3B - Runtime Seat + Turn/AFK

**Status: IMPLEMENTED / substantially complete.**

Implemented:

- PlayerGameState/seat integration.
- Human roll timeout.
- Host-authoritative auto-roll for eligible remote Human seats.
- Starting-order auto-roll without false AFK increment.
- Scheduled-turn AFK streak logic.
- Manual first roll resets AFK streak.
- Automatic first roll increments AFK streak.
- Doubles do not double-count the same scheduled-turn AFK streak.
- Trade/blocked-management pauses or resets timeout appropriately.
- AFK removal architecture.
- PermanentBot transition foundation.

Exact expiry and repeated reconnect-cycle torture were accepted in Phase 12.

## 4.3 Phase 3C - Account & Cloud Profile

**Status: FOUNDATION IMPLEMENTED; production hardening remains.**

Implemented foundation:

- Account models/service.
- Firebase Auth/Firestore connectivity.
- Cloud preferences.
- Backend health checks.
- Wallet.
- Inventory.
- Commerce.
- Promo/event modules.
- Steam provider mapping and returning-sign-in development/production boundary.

Still later:

- Real production provider configuration and account recovery UX.
- App Check enforcement.
- Final Firestore Security Rules review.
- Production admin/reward authorization boundaries.
- Monitoring/audit strategy.
- Final production cloud-profile UX.

## 4.4 Phase 3D - Create Room / Ready / Session UI

**Status: DONE.**

Includes:

- Private rooms and secure room codes.
- Host / Local Human / Bot / Open Online / Remote Human seats.
- Ready revision invalidation.
- Host Start authority.
- Open-seat resolution.
- Kick confirmation and kicked-room protection.
- Voluntary lobby leave.
- Synchronized countdown.
- Room password.
- Host room cleanup.
- Room-code show/hide/copy.
- Stale waiting-room cleanup and Host heartbeat from Phase 11F.

## 4.5 Phase 4 - Public Lobby Browser

**Status: DONE.**

Includes:

- Public room creation/discovery.
- Browser filtering.
- Password indicator/join.
- Join and double-click join.
- Version checks.
- Atomic final-seat reservation/race protection.
- Public room cleanup.
- Dedicated `JOIN BY CODE` access directly from Public Rooms.
- Dedicated code + optional password modal, separate from Private Table room creation.
- Correct modal layering over Main Menu/Public Rooms content.

# 5. Phase 5 - Online Gameplay Networking

**Status: MAIN PATH DONE / runtime proven in major flows.**

## 5.1 Turn + dice synchronization

**Status: DONE / accepted.**

- Host authoritative RNG.
- Remote roll intent.
- Starting-order synchronization.
- Dice result synchronization.
- Physical dice presentation on both clients.
- Doubles/triple-double synchronization.
- Host-transfer scheduler state now survives authority changes.

## 5.2 Pawn movement synchronization

**Status: DONE / accepted.**

- Authoritative movement state.
- Follower movement queue.
- Checkpoint/snap recovery.
- Pawn cosmetic synchronization foundation.
- Stale movement presentation protection.
- Crash recovery safe checkpoint avoids inheriting half-executed movement.

## 5.3 Economy / ownership / rent

**Status: DONE / accepted main path.**

- Authoritative money mirror.
- Ownership state.
- Purchase/rent outcomes.
- Property presentation.
- Bankruptcy ownership release/transfer rules synchronized.

## 5.4 Decision paths

**Status: substantially completed.**

Includes:

- Purchase.
- Event.
- Bonus.
- Tax.
- Auction.
- Vacation/special paths.
- Trade.
- Development.
- Rent-bankruptcy result context.

Host authority and seat-owned decisions remain mandatory invariants.

## 5.5 Result / rematch / reusable session

**Status: DONE for tested path.**

Accepted:

- Guest Result shows Host-sensitive leave/wait behavior.
- Current Host receives Rematch authority.
- Host transfer changes Result action ownership correctly.
- Synchronized Rematch returns clients to the same lobby.
- New/second match starts without executable restart.
- Tested stale HUD/pawn/money/modal/result state is cleared.
- Dice remain usable after rematch/new-lobby flow.
- Rest + doubles no longer creates a duplicate extra roll.

## 5.6 Formerly deferred Phase 5 edge cases - current disposition

The old roadmap deferred the following:

- Voluntary Leave Match -> TemporaryBot -> same-account rejoin.
- Unexpected disconnect/reconnect timing.
- Reconnect during trade/auction/decision.
- Repeated reconnect cycles.
- TemporaryBot -> PermanentBot expiry.
- Bankruptcy visual cleanup.
- Duplicate/late/stale intent abuse.
- Host Migration.

Current v5.3 status:

- Voluntary leave/rejoin: **implemented in 11F/11G and repeated-cycle validated in Phase 12**.
- Unexpected process loss: **implemented in 11G and crash-torture validated in Phase 12**.
- Host Migration: **implemented and accepted in 11G; split-brain/election behavior validated in Phase 12**.
- Bankruptcy pawn + ownership cleanup: **implemented in 11G and developed-property Host/Guest cleanup accepted in Phase 12**.
- Exact TemporaryBot expiry, stale/duplicate command rejection, repeated reconnect, crash-election recovery, and purchase-decision failover are **accepted Phase 12 behavior**.

# 6. Current visual/UI checkpoint

## 6.1 Game board

Accepted invariant across Classic/Garden/Beach/Pavilion/Street:

- Physical board edge remains warm walnut/dark.
- Physical board center remains warm light/parchment.
- Surrounding table/environment may change by theme.
- Future theme work must not recolor the physical board back to blue/green.

## 6.2 Player HUD / profile presentation

Critical Human/Bot authority representation is solved for match seats.

Accepted in Phase 14:

- Top-left generic `PLAYER` profile/header now binds to the canonical signed-in Atlas profile identity.
- P1/P2/P3/P4 HUD icon/badge geometry is aligned consistently.

Remaining polish:

- Final HUD typography/spacing.
- Long-text/responsive polish across supported languages and aspect ratios.

# 7. Phase 6 - Lobby + Match Chat

## 7.1 Phase 6A - Core chat

**Status: DONE / runtime accepted.**

- Separate Lobby Chat and Match Chat scopes.
- Authenticated Firebase callable backend.
- Server-authoritative canonical message time.
- Sender local display as HH:mm with UTC offset based on server instant.
- Maximum 120 Unicode characters/code points.
- Empty/whitespace-only rejected.
- Server-side per-account/per-scope cooldown.
- Membership-required chat access.
- Collapsible right-edge launcher.
- Unread badge.
- Character counter and cooldown UI.
- Launcher/panel avoids Player HUD.

## 7.2 Phase 6B - Moderation & reliability

**Status: DONE / backend and runtime UX accepted.**

Implemented:

- EN/TR/ES/FR/DE/KO/RU profanity baseline.
- Case/diacritic/basic leetspeak/punctuation normalization.
- Dynamic/backend-managed moderation terms.
- URL/invite blocking.
- Repeated-message/flood heuristics.
- Local mute/unmute and block/unblock.
- Report Player and Report Message.
- Bounded report context.
- Report IDs.
- Moderator/admin message removal.
- Timed mute/ban/chat-disable foundation.
- Moderator audit foundation.
- TTL/retention-ready data.
- Chat text-size accessibility.
- Runtime language refresh.
- Report confirmation popup.
- Phase 11E async lifecycle fix prevents stale destroyed-object callbacks after menu/game transitions.

Phase 6 remains closed for normal roadmap sequencing.

# 8. Content and gameplay completion checkpoint

## 8.1 Full gameplay regression

**Status: DONE for the accepted content/regression checkpoint.**

Accepted high-value runtime coverage includes:

- Starting-order tie reroll.
- Triple-double penalty.
- Human roll timeout.
- Human/Human and Human/Bot auction.
- Developed rent.
- Travel/Vacation.
- Event-card runtime.
- Host -> Guest and Guest -> Host trade.
- Trade reject/cancel and timeout interactions.
- Balanced Development.
- Bankruptcy result path.
- 2-player Human/Human.
- 2-player Human/Bot.
- 3/4-player mixed sessions.
- Seven-language critical UI smoke.

Later resilience regressions belong to Phase 12/16 rather than reopening Phase 8.

## 8.2 Map/content baseline

**Status: DONE for current Turkey / Colorado / USA checkpoint.**

- Turkey preserves Manisa.
- Colorado preserves Longmont.
- USA preserves Colorado.
- All maps use common data-driven architecture.
- No map-specific gameplay branches.
- Economy/content expansion is a later content backlog.

# 9. Phase 9 - Meta progression / Store / Economy / Events

**Status: DONE / committed / runtime accepted.**

Canonical Phase 9 commit:

`8223ad419fb09cffc5d0b7924a751a99088ba8a7` - Phase 9 storefront and seasonal event systems.

## 9.1 Currency separation

- In-match cash is temporary match state.
- Gold is persistent soft currency.
- Atlas Coin is persistent premium currency working model.
- Event tickets/tokens are server-authoritative event balances.

## 9.2 Store / inventory

Accepted:

- Single canonical Main Menu Store entry.
- Home / Items / Event / Redeem Code / History tabs.
- Shared authenticated identity/wallet.
- Firestore entitlement restore.
- Server catalog price authority.
- Idempotent purchase identities.
- Promo/redeem reuse.
- Unified user-facing transaction history.
- EN/TR/ES/FR/DE/KO/RU response/localization.
- Korean glyph fix.
- Emulator-only development wallet/reset tools.

## 9.3 Seasonal event

Accepted Atlas Harvest foundation:

- Server-authoritative Daily Reward.
- Event tickets.
- Event XP.
- Daily/weekly challenges.
- Reward track.
- Limited-time item purchases.
- Persistent account seasonal state.
- Event-expiry server rejection.

## 9.4 Production boundary

Not yet production-enabled:

- Real-money/provider receipt verification.
- Production App Check/security rules.
- Production catalog/live-ops authorization.
- Production monitoring/rate-limit hardening.

# 10. Phase 10 - Career / Achievements / Progression

**Status: DONE / committed / backend E2E and Unity runtime accepted.**

Canonical commit:

`ebe72662bf058e18df123d4cad06e8161a04a092` - Phase 10 Career progression, achievements and match history.

Accepted:

- CAREER Main Menu entry.
- Overview / Match History / Achievements.
- Completed-match progression authority.
- Human-only account history.
- Idempotent match finalization.
- Cumulative statistics.
- Map-scoped statistics.
- Account XP / Player Level.
- 14 canonical internal Atlas achievements.
- Persistent achievement progress/unlocks.
- Newest-10 / 72-hour user-facing recent history.
- Structured match details.
- Turkey/Colorado/USA selected-map consistency.
- Canonical evidence retained independently from short recent history.

# 11. Phase 11 - Platform, Steam, shared multiplayer and authority resilience

**Status: DONE for the current development checkpoint.**

Phase 11 expanded beyond the old roadmap. It now contains 11A through 11G.

## 11A - Provider-neutral platform foundation

**Status: DONE.**

- Provider-neutral identity/invite/presence/achievement contracts.
- Persistent platform runtime.
- Development provider.
- Safe join payload parsing.
- Incoming invites reuse existing Atlas room-code flow.
- Safe presence payload excludes passwords/secrets.
- Replay-safe achievement projection.
- Static validation accepted.

## 11B - Real Steam client integration

**Status: DONE for AppID 480 development checkpoint.**

Canonical checkpoint commit:

`11fdcdaba2f37e506994708674b0ee01e62ab867`

Implemented:

- Steamworks.NET 2025.164.1 via Unity Package Manager.
- SteamAPI initialization/callback/shutdown.
- Real SteamID/persona detection.
- Canonical Firebase AccountId remains separate.
- Rich Presence for Main Menu/Lobby/Match.
- Safe connect payload.
- `GameRichPresenceJoinRequested_t` routes into existing Atlas join flow.
- Real Steam invite dialog.
- Lobby `INVITE FRIENDS` action beside `START MATCH`.
- Invite button isolated from Start Match persistent UnityEvent.
- 14 canonical achievement -> Steam API-name mappings.

Accepted:

- Phase 11A validation PASS.
- Phase 11B validator PASS 8/8.
- Real Steam initialization PASS.
- Real SteamID/persona PASS.
- Lobby Rich Presence PASS.
- Standalone overlay/invite dialog PASS.
- Lobby Invite Friends PASS.

Production boundary remains: AppID 480 is development-only; real AtlasBoard AppID and Steamworks configuration are still required.

## 11C - Secure Steam account linking

**Status: DONE for foundation/development acceptance.**

Canonical commit:

`8e91db76914db59cbf17f28f788adf24a193fd17`

Implemented:

- Server-side Steam identity-link architecture.
- Bidirectional SteamID <-> Atlas AccountId mapping.
- One-to-one conflict protection.
- Idempotent replay.
- Immutable platform-link audit evidence.
- Raw Steam auth tickets never persisted/logged.
- SHA-256 proof digest.
- Production Valve `AuthenticateUserTicket` verification path.
- Secret Manager boundary for publisher key.
- Emulator-only development proof path.
- Link-status callable/runtime diagnostics.
- Single canonical Firebase Functions `src/index.ts` entry.

Production verified linking remains fail-closed until the real AtlasBoard AppID and publisher Web API secret are configured.

## 11D - Returning Steam sign-in

**Status: DONE for development architecture/runtime.**

Canonical commit:

`a5e4b28f442a7ad22b5789096633c8ef7bd141f1`

Returning flow:

1. Steam auth ticket.
2. Server verifies Steam in production.
3. Verified SteamID resolves existing provider mapping.
4. Backend issues Firebase custom token for canonical Atlas AccountId.
5. Firebase Auth signs into the same canonical account.
6. Wallet/inventory/progression/achievements/profile remain attached to the same UID.

Emulator development returning-sign-in path exists without pretending AppID 480 is production verification.

## 11E - Steam runtime resilience and achievement resync

**Status: DONE / runtime accepted.**

Canonical commit:

`a8d777b8eb9e798fec8b5f750185ae428b34ca18`

Implemented/accepted:

- Bounded Steam initialization retry: 5s -> 10s -> 20s -> 30s.
- No Unity freeze while Steam is unavailable.
- Firebase/Atlas identity remains usable while Steam is offline.
- Steam can recover while Unity remains in Play Mode.
- Previously linked canonical Atlas AccountId restores.
- Lobby identity does not race ahead of Steam recovery.
- Lobby identity rebinds after Firebase Auth restoration.
- Achievement resync runs immediately after Steam recovery.
- Periodic achievement sync remains protected against overlapping runs.
- Async chat lifecycle MissingReferenceException fixed.

## 11F - Shared development multiplayer / lobby and reconnect hardening

**Status: DONE / real two-machine acceptance.**

Canonical commit:

`6ddf01ec66f0116396d18b27b171b9c6080ee671`

Architecture:

- Development builds can target a configurable Firebase emulator host instead of only localhost.
- Development HTTP is enabled only for the test build path and project settings are restored after build.
- Colorado and Turkey physical machines were tested against one shared emulator backend over a private Tailscale path.
- Required development ports were restricted to the intended peer.

Accepted two-machine runtime coverage:

- Shared lobby visibility.
- Player count synchronization.
- Round limit, map, theme synchronization.
- Lobby chat bidirectional.
- Simultaneous match start.
- Pawn position synchronization.
- Auction.
- AFK auto-roll.
- Match chat.
- Property ownership.
- Trade.
- Lobby kick.
- 2-player result flow.
- Rematch / second match.
- XP / Career / achievements / history.
- Public lobby discovery/join.

Hardening added:

- Active-match reconnect no longer re-runs lobby start bootstrap and resets gameplay.
- Host `local_human` is normalized as Human instead of Bot.
- Rule-toggle synchronization reads real UI values.
- Disabling Doubles automatically disables Triple Double Penalty.
- Host waiting-lobby heartbeat.
- Stale waiting-room cleanup.
- Dedicated Public Rooms Join by Code modal.
- Room-entry modal layering fix.

## 11G - Host Migration / crash recovery / final multiplayer cleanup

**Status: DONE / accepted.**

Canonical current HEAD:

`068ca6f0d1736516ace63b79681227202a408cf8`  
`Phase 11G: finalize host migration and multiplayer resilience`

### 11G.1 Graceful Host Migration

Implemented:

- Host may leave an active match.
- Leave request waits for a safe gameplay checkpoint.
- Repeat-click protection / pending leave UX.
- Deterministic replacement Host.
- Existing match ID and authoritative state are preserved.
- Old Host becomes reconnectable TemporaryBot.
- New Host receives Bot authority.
- Former Host can rejoin the same active match.

Initial migration exposed stale follower TurnManager scheduler state; this was fixed by synchronizing:

- Turn order.
- Current turn-order position.
- Completed turn count.
- Completed active slots in the current round.
- Consecutive doubles state.

Accepted:

- Host leave -> Guest takeover.
- No money/property/pawn/round reset.
- Bot turns continue.
- Former Host rejoin.
- Result authority follows new Host.

### 11G.2 Abrupt crash/disconnect failover

Implemented:

- Per-Human match presence heartbeat.
- Abrupt Guest loss -> TemporaryBot.
- Host heartbeat timeout.
- Surviving Human clients probe server-authoritative recovery.
- Deterministic lowest-slot eligible Human election.
- Authority epoch/revision continuity.
- Old Host -> TemporaryBot with five-minute reclaim.
- Host-owned extra local bot seats normalize correctly.
- Safe recovery frame is stored separately from newest transient frame.
- Crash during movement/dice/decision can recover from the last safe checkpoint instead of inheriting half-executed state.
- Stable local-emulator fallback identity derived deterministically from the development client identity when Steam returning identity is unavailable.
- Restarting the same development client can restore the same Firebase UID and reclaim the same SeatId while emulator state remains alive.

### 11G.3 Tablet localization and bankruptcy finalization

Implemented/accepted:

- Tablet panel localization audit.
- Remaining hard-coded Trade labels and cash placeholders localized.
- Trade validation/error paths localized.
- Rent/bankruptcy follower presentation is rebuilt in each client's own selected language rather than replaying Host-localized prose.
- Authoritative bankruptcy flag replicates to followers.
- Bankrupt pawn disappears on all clients.
- Bankruptcy liquidation no longer awards every property for a tiny unpaid debt.
- Cash pays first.
- Remaining properties are ordered by PurchasePrice ascending with TileIndex deterministic tie-break.
- Only the minimum property/property set necessary to cover unpaid debt transfers to a valid creditor.
- Remaining properties return to unowned state.
- Tax/bank/event bankruptcy with no creditor releases properties.
- Development is liquidated/reset.
- Result distinguishes transferred and released properties.

Runtime acceptance reported:

- Tax bankruptcy released properties without transferring them to another player.
- Creditor bankruptcy transferred only the needed property in the observed test.
- Host migration and rejoin remained functional.
- Phase 11G accepted and closed.

## 11 overall close decision

Phase 11 is **DONE for the current development checkpoint**.

Remaining Steam production work is not a Phase 11 architecture blocker:

- Real AtlasBoard Steam AppID.
- Publisher Web API credentials.
- Published custom achievement definitions.
- Production provider verification configuration.

Those now belong to production integration/hardening and release preparation.

# 12. Phase 12 - Focused Late Online Resilience

**Status: DONE / accepted 2026-09-29.**

Phase 12 was re-baselined after 11F/11G so it would harden the existing architecture instead of rebuilding reconnect or Host Migration. The accepted work is organized as 12A through 12E.

## 12A - Duplicate / late / stale command hardening

**Status: DONE / backend E2E 12/12 PASS + Unity runtime smoke PASS.**

Implemented:

- Every remote gameplay intent carries the authoritative state the user actually acted on: network revision, event sequence, authority epoch, and network phase.
- Backend atomically verifies that the submitting seat still belongs to the authenticated account, is Human-controlled, connected, and not AFK-locked.
- Commands produced from an old revision/event/phase/authority epoch are rejected with `match.error.stale_client_state`.
- Exact same `clientCommandId` replay remains idempotent.
- Pending Host intent queue marks legacy/expired/stale commands `stale` instead of presenting them for execution.
- Pending commands are invalidated when authority epoch, network revision, event sequence, phase, or Human controller ownership no longer matches.
- Host processes at most one current remote gameplay command before allowing an authoritative publish/revision boundary.

Accepted evidence:

- `phase12-intent-resilience-e2e.mjs` PASS 12/12.
- Purchase/Auction/Trade normal runtime behavior remained functional.
- Fast repeated clicks did not double-apply tested money, ownership, bid, trade, roll, or result effects.
- Guest leave -> rejoin remained functional.
- Host Migration remained functional.

## 12B - TemporaryBot / PermanentBot expiry hardening

**Status: DONE / backend E2E 10/10 PASS.**

Implemented:

- Active-match reclaim is valid only for `temporary_bot + reconnecting` while the authoritative reconnect lease is still active.
- Exact expiry boundary uses `expiresAt <= server now`.
- Expired conversion commits before the callable returns `match.error.reconnect_expired`; the permanent conversion is not lost through transaction rollback.
- `permanent_bot + reconnect_expired` is explicitly non-reclaimable.
- Already-connected Human duplicate Join-by-Code remains idempotent.
- Host expiry sweep re-reads Host authority and seat state transactionally.
- Reconnect-vs-expiry-sweep races resolve by transaction retry rather than stale overwrite.
- Expiry state is mirrored to both match seat and lobby member.

Accepted evidence:

- `phase12-temporarybot-expiry-e2e.mjs` PASS 10/10.
- Valid pre-expiry reclaim restores the same Human seat.
- Expired reclaim is rejected and PermanentBot conversion persists.
- A PermanentBot cannot later be silently reclaimed.
- Future/non-expired reservations are not prematurely expired.

## 12C - Repeated reconnect and decision continuity validation

**Status: DONE / automated repeated-cycle E2E 9/9 PASS + representative runtime decision reconnect accepted.**

Accepted:

- Five consecutive `Human -> TemporaryBot -> same Human` cycles reclaimed the exact same canonical `SeatId`.
- No duplicate match seat was created.
- Lobby intentionally retains four canonical member documents (`seat_1` through `seat_4`); inactive slots remain inactive rather than being duplicate players.
- No duplicate active member was created.
- Host authority did not drift during repeated Guest reconnect.
- Room code continued to resolve the same active match.
- Already-connected duplicate join remained idempotent.
- Auction reconnect was manually observed to reconstruct the active auction and allow the returning Human to participate.
- Existing authoritative decision snapshots and Phase 12A stale-command guards remain the canonical mechanism for Purchase/Trade/Auction/Event/Special continuity.

Accepted evidence:

- Corrected `phase12-repeated-reconnect-e2e.mjs` PASS 9/9.
- Initial 12C failure was a QA assertion error: a 2-player lobby still intentionally persists four canonical slot documents.

## 12D - Host crash election and transient-state torture

**Status: DONE / backend E2E 11/11 PASS + Unity crash torture PASS.**

Backend crash-election acceptance:

- Concurrent recovery probes cannot create split-brain.
- Deterministic lowest-slot eligible live Human wins.
- Authority epoch advances exactly once.
- Crashed Host becomes `TemporaryBot / Reconnecting`.
- Old Host-owned Local Bot normalizes to PermanentBot authority.
- Losing recovery probes cannot increment authority again.
- A stale lowest-slot candidate is skipped in favor of the next live Human.
- Fresh Host heartbeat prevents premature failover.
- Lobby Host projection follows the recovered match Host.
- Match/network authority metadata remains internally consistent.
- Unsafe transient movement frame rolls back to the last safe authoritative checkpoint.

Accepted evidence:

- `phase12-host-crash-election-e2e.mjs` PASS 11/11.

Manual Unity crash torture accepted:

- Host crash during normal `awaiting_roll`: PASS.
- Host crash during visible pawn movement: PASS.
- Host crash during dice animation/result: PASS.
- Host crash during decision handling: PASS.
- Game continued on surviving client without freeze, duplicate movement, duplicate money effect, or split-brain.

### 12D.1 - Purchase-decision crash recovery

One real defect was found during 12D:

- When another Human had an unresolved property `BUY / SKIP` decision and the Host crashed, generic safe-checkpoint recovery could rewind before the property decision and the player lost the opportunity to buy.

Fix:

- `awaiting_decision` is a recovery-safe authoritative phase.
- Follower -> Host promotion restores the unresolved Purchase decision: decision player, decision tile, Host-side pending Purchase authority, and normal turn-completion callback.
- If the newly promoted Host owns the decision, local Purchase UI remains usable.
- If another Human owns the decision, the new Host keeps headless authority while that Human submits BUY/SKIP.
- Phase 12A stale authority/revision guards still reject commands targeting the old Host epoch.

Acceptance:

- Purchase decision survives Host crash.
- BUY completes once after failover.
- Money changes once.
- Ownership transfers once.
- Turn advances normally.
- 12D.1 runtime retest PASS.

## 12E - Developed-property bankruptcy cleanup E2E

**Status: DONE / deterministic Host QA PASS + Guest visual/runtime PASS.**

Deterministic fixture:

- Fresh 3-player match.
- seat_2 owns three developed properties with development levels 1 / 2 / 3.
- seat_2 cash is forced to 1.
- Mandatory debt is 2, leaving exactly 1 unpaid.
- Bankruptcy liquidation therefore transfers only the cheapest required property to the creditor and releases the remaining properties.

Observed accepted result:

- Amount due = 2.
- Paid = 1.
- Unpaid = 1.
- Transferred properties = 1.
- Released properties = 2.
- Transferred property value = 100.
- Cheapest transferred property in the acceptance run: Tile 1 `Edirne`.
- Host check: PASS.
- Bankrupt pawn hidden on Host.
- All authoritative development levels returned to 0.
- All house/hotel/development markers disappeared.
- Only the cheapest property transferred to seat_1.
- Remaining two properties became unowned.
- Guest mirrored the same pawn removal, development cleanup, ownership transfer, and released-property visuals.
- A released property was later landed on and purchased normally.
- Continued play did not resurrect the bankrupt pawn or stale development markers.

## 12.6 Phase 12 close gate

**Phase 12 close gate: PASS.**

Accepted close evidence:

- No unresolved freeze/reset/split-brain remains in the tested focused scenarios.
- Repeated reconnect cycles preserve canonical AccountId/SeatId state.
- Exact TemporaryBot expiry behavior is accepted.
- PermanentBot post-expiry reclaim is rejected.
- Duplicate/late/stale commands cannot double-apply the tested authoritative gameplay effects.
- Crash failover works through normal turn, movement, dice, and decision states.
- Purchase decision continuity survives Host crash.
- Developed-property bankruptcy cleanup is accepted on Host and Guest.
- Bankruptcy pawn/development visuals remain cleared during continued play.
- Existing lobby/chat/progression/Store/Career/Steam identity architecture was not rewritten by Phase 12.

## 12.7 Phase 12 close artifacts

- `Backend/Firebase/functions/scripts/phase12-intent-resilience-e2e.mjs`
- `Backend/Firebase/functions/scripts/phase12-temporarybot-expiry-e2e.mjs`
- `Backend/Firebase/functions/scripts/phase12-repeated-reconnect-e2e.mjs`
- `Backend/Firebase/functions/scripts/phase12-host-crash-election-e2e.mjs`
- `Assets/Project/Editor/AtlasBoardPhase12EDevelopedBankruptcyE2ETool.cs`

Phase 12 production changes are intentionally narrow and preserve the accepted Phase 11G Host Migration/reconnect architecture.

# 13. Former Host Migration milestone

**Status: RETIRED AS A SEPARATE IMPLEMENTATION PHASE / ABSORBED INTO PHASE 11G.**

The v4.5 roadmap defined Phase 13 as a future standalone Host Migration milestone covering deterministic new Host selection, authoritative state transfer, pending intents, Bot authority, AFK ownership, revision continuity, original Host rejoin, and migration during gameplay transitions.

Those architectural requirements are now implemented in Phase 11G.

Therefore:

- Do not build a second Host Migration system in Phase 13.
- Host Migration regressions are fixed against the existing 11G architecture.
- Phase 12 timing torture is complete; future regressions are handled in the phase where they are discovered.
- Performance/build-level migration QA belongs to Phase 16.
- Real external-network Host-loss validation belongs to Phase 17.

Phase number 13 is intentionally retained in documentation history so older roadmap references remain understandable.

# 14. Dedicated UI/UX polish

**Status: ACTIVE — Phase 14A through 14D accepted locally; Phase 14E NEXT.**

Already improved during 11F/11G:

- Public Rooms dedicated Join by Code.
- Modal layering.
- Host leave pending UX.
- Trade localization.
- Bankruptcy result localization.

Accepted during the current Phase 14 local checkpoint:

- Clickable/tappable City Information Panel.
- Provider-neutral Network Quality diagnostics using existing Firebase application traffic.
- Network Quality presentation moved out of the permanent gameplay HUD and into the Pause menu.
- Canonical signed-in profile identity in the top-left Main Menu profile card instead of generic `PLAYER`.
- P1/P2/P3/P4 HUD icon, content-lane and turn-badge alignment.
- Unity `Atlas Board` editor tools reorganized under grouped categories.
- Known Phase 14 Unity 6.5 deprecated API warnings cleaned.
- Known LiberationSans/TextMeshPro ellipsis fallback warning spam cleaned by using the effective `Truncate` behavior explicitly.

Still remaining in Phase 14:

- Final HUD typography and spacing.
- Result screen polish.
- Auction / Trade / Development visual polish.
- Responsive resolution/aspect-ratio review.
- EN/TR/ES/FR/DE/KO/RU long-text regression.
- Controller/gamepad navigation.
- Colorblind-safe ownership indicators.
- Reduced-motion consistency.
- Mobile-safe layout if mobile becomes active scope.

## 14.1 Clickable City Information Panel

**Status: DONE / locally accepted.**

Accepted behavior:

- Clicking/tapping a City property opens a compact informational panel without changing gameplay authority.
- Panel reads existing data-driven `BoardTileDefinition` / runtime tile data instead of city-specific gameplay branches.
- Shows city/property name, active map/group context, owner/unowned state, purchase price, base rent, current effective rent, development cost and development level.
- Uses `Description` when content provides one.
- If description content is empty, uses a localized neutral fallback rather than inventing city facts.
- Supports EN/TR/ES/FR/DE/KO/RU labels and runtime language changes.
- Mouse and touch input supported.
- Safe-area aware.
- Clicking UI does not select a City behind the UI.
- `X`, `ESC`, empty-board or non-City click closes the panel.
- Opening the panel never performs Purchase/Auction/Trade/Development/network authority actions.

Architecture invariant:

- City information remains content/data driven.
- Existing Purchase/Auction/Trade/Development decision panels remain authoritative and separate from the informational panel.

## 14.2 Network Quality Diagnostics

**Status: DONE / locally accepted presentation; controlled-network validation remains part of Phase 16.**

Measurement behavior:

- Reuses existing `matchGetSnapshot` Firebase application traffic.
- Does **not** add a dedicated ping endpoint or extra Firebase polling.
- `PING` = smoothed application/backend round-trip time.
- `JIT` = recent successful RTT variation.
- `LOSS` = application-level failed/late snapshot percentage.
- `LOSS` is explicitly not raw UDP packet loss.
- Diagnostic telemetry is presentation-only and never gameplay authority.

Final UX decision:

- Network quality is **not** permanently shown over the gameplay board.
- During an online match, the compact network row appears inside the Pause menu, under `QUIT GAME` and above the `ESC` hint.
- Values continue collecting from existing snapshot traffic while gameplay runs, so opening Pause can show recent/current measurements immediately.
- Local/offline matches do not need to show the network row.
- States remain provider-neutral: Good / Fair / Poor / Reconnecting / Offline.
- Future SteamNetworkingSockets/SDR transport may replace the metric source while preserving the same provider-neutral UI contract.

Phase 16 still owns controlled latency/jitter/disconnect validation and release-quality threshold tuning.

## 14.3 Canonical Profile Header

**Status: DONE / locally accepted.**

Accepted:

- Main Menu top-left profile card uses canonical Atlas profile `DisplayName` from the existing account/profile service.
- Generic localized `PLAYER` remains only as signed-out/unavailable fallback.
- Actual user display name is not translated when UI language changes.
- Avatar initial follows the canonical display name.
- Existing Profile modal receives the same identity.
- Store/wallet, lobby, Steam provider identity and match authority are not rewritten by this binding.
- Canonical AccountId remains authoritative; Steam persona/SteamID does not replace Atlas identity.

## 14.4 P1/P2/P3/P4 HUD Alignment + Editor Cleanup

**Status: DONE / locally accepted.**

HUD alignment:

- Four Player HUD cards share consistent internal geometry.
- Player icon/badge is consistently sized and centered.
- Player-name, money and controller-type lanes share consistent left alignment.
- Turn badge uses a consistent top-right inset and size.
- Long player names reserve turn-badge space.
- Existing 2/3/4-player external corner placement remains owned by the established HUD layout system.
- Existing current-turn, Human/Bot/TemporaryBot, bankruptcy and gameplay state behavior remains unchanged.

Editor/tooling cleanup:

- Unity `Atlas Board` menu is grouped under:
  - `Design`
  - `Online & Backend`
  - `Game & Content`
  - `Meta & Progression`
  - `QA & Diagnostics`
  - `Phases`
  - `Project Tools`
- Phase tools are grouped under `Phases -> Phase 14 -> 14A / 14B / 14C / 14D`.
- Final safe organizer uses exact literal menu-prefix replacements rather than reconstructing `MenuItem` attributes.
- The earlier temporary menu-organizer repair scripts were recovery tooling only and are not required repository artifacts.
- Known Unity 6.5 Phase 14 `FindObjectsSortMode.None` deprecation warnings were migrated to current overloads.
- Deprecated `TMP_Text.enableWordWrapping` assignment in the Steam lobby invite button was migrated to `textWrappingMode`.
- TMP ellipsis fallback warning spam was removed by explicitly using `Truncate`, matching the runtime fallback behavior already observed with the current LiberationSans font chain.

## 14.5 Phase 14E — Final HUD Typography & Responsive Polish

**Status: NEXT after the Phase 14A–14D checkpoint is committed/pushed.**

Scope:

- Final typography hierarchy for Player HUD and closely related gameplay HUD surfaces.
- Normalize font sizes, weights, baseline alignment, line heights and spacing.
- Review compact labels across P1/P2/P3/P4 at 2/3/4-player layouts.
- Verify long canonical player names do not collide with turn badges or values.
- Review EN/TR/ES/FR/DE/KO/RU long-text behavior.
- Review 16:9 and representative narrower/wider aspect ratios.
- Preserve current authority/gameplay behavior; this is presentation-only.
- Do not rebuild accepted HUD placement logic unless a concrete responsive defect requires it.

Acceptance should include:

- Unity compile with no new unexplained errors/warnings from touched code.
- 2/3/4-player HUD presentation.
- Long player-name smoke.
- Runtime language smoke across representative long-text languages.
- No regression to Phase 14A City panel, Phase 14B Pause-network row, Phase 14C profile identity, Chat, decision panels or Player HUD authority states.

After 14E, continue the remaining Phase 14 presentation backlog in focused packages rather than mixing Result/Auction/Trade/Development/controller/accessibility changes into one large patch.

# 15. Security and production backend hardening

**Status: FUTURE / required before live release.**

- App Check production enforcement.
- Final Firestore Security Rules.
- Least-privilege callable authorization.
- Abuse/rate-limit review.
- Idempotency review.
- Wallet/commerce immutable audit trail review.
- Catalog/promo authorization.
- Chat/report/sanction production review.
- TTL/retention deployment.
- Secret/config review.
- Environment separation.
- Logging/monitoring/alerting.
- Privacy/data-retention documentation.
- Moderation operations/dashboard if reports are used live.
- Real AtlasBoard Steam AppID and publisher-secret configuration.
- Production Steam verification and achievement configuration.
- Deployed backend migration away from development emulator assumptions.

# 16. Build readiness and QA

## 16.1 Unity/build quality

- Zero unexplained compile errors.
- Warning cleanup. Known Phase 14 Unity 6.5/TMP warning set was cleaned locally; Phase 16 still owns the full-project warning audit.
- Profiler pass.
- GC allocation review.
- Rendering performance.
- Resolution/aspect testing.
- VSync/FPS QA.
- Input QA.
- Localization/font/glyph QA.
- Development vs release HTTP/security-setting verification.

## 16.2 Multiplayer QA

- Multiple physical PCs.
- Host/Guest role reversal.
- 2/3/4 players.
- Latency/jitter/packet-loss simulation where practical.
- Emulator vs deployed-backend parity.
- Version compatibility rejection.
- Reconnect/host-loss regression suite.
- Authority epoch/revision diagnostics.
- Repeated rematch/new-lobby reuse.
- Validate Network Quality HUD against controlled latency/jitter/disconnect conditions.
- Verify Firebase/application-level latency, jitter and timeout/loss estimates are stable and clearly labeled.
- If SteamNetworkingSockets/SDR is later adopted, compare native Steam connection statistics against the provider-neutral HUD contract.

## 16.3 Pipeline

Potential:

- Repeatable Development Build profile.
- Release build profile.
- CI Unity compile/test.
- Backend lint/build/E2E pipeline.
- Version stamping.
- Artifact naming.
- Steam branch/channel strategy.
- Automated focused multiplayer regression harness where feasible.

# 17. External / live testing

Final external testing should include:

- Real Internet rather than only local/private development routing.
- Multiple real accounts.
- Multiple time zones.
- Multiple physical PCs.
- Real AtlasBoard Steam AppID invite/join when available.
- Chat UTC-offset behavior.
- Repeated disconnect/reconnect.
- Host loss/migration.
- 3/4-player sessions.
- Deployed production-like backend.
- Later mobile/crossplay if platform adapters exist.

The Colorado/Turkey two-machine test already proved the shared-development architecture and major gameplay synchronization over two physical machines. Phase 17 should validate the release topology, not repeat the exact emulator experiment.

# 18. Optional feature backlog - not committed scope

These ideas must not displace the core roadmap automatically.

## Social / multiplayer

- Friend list integration.
- Party system.
- Quick Play.
- Matchmaking queues.
- Casual/ranked split.
- Skill rating/ELO.
- Spectator slots.
- Reconnectable spectators.
- Extended/replayable history.
- Replay/event-log files.
- Rematch vote alternative.

## Communication

- Quick-chat phrases.
- Emotes.
- Ping system.
- Party/whisper/spectator chat.
- Voice only if moderation/platform burden is justified.
- Translation assistance only if privacy/cost/quality are acceptable.

## Progression

- Seasonal badges/titles.
- Profile frames/titles as visible Career rewards.
- Map mastery beyond current per-map cumulative stats.
- Additional daily/weekly mission families.

## Gameplay variants

- Alternate rule presets.
- Short/long modes.
- Custom lobby rule presets.
- Tournament mode.
- Team mode only with clean rule redesign.

## Content creation

- Steam Workshop/custom maps.
- Custom Event decks.
- Cosmetic board themes.
- Community map review pipeline.

## Accessibility

- Colorblind palettes.
- Larger UI/text scale.
- Dyslexia-friendly font if validated.
- Remappable controls.
- Keyboard-only/controller-only navigation.
- Reduced motion.
- High contrast.
- Better status log/subtitles.

## Operations

- Crash reporting.
- Privacy-safe analytics.
- Feature flags.
- Remote Config tuning.
- Staged rollout.
- Backend health dashboard.
- Moderation dashboard.

# 19. Revised execution order from v5.1 checkpoint

1. **Phase 14 - Dedicated UI/UX polish**.
2. **Phase 15 - Security and production backend hardening**.
3. **Phase 16 - Build readiness, performance, localization QA, pipeline**.
4. **Phase 17 - External real-PC/live backend testing**.
5. **Release candidate / Steam launch preparation**.

Phase 12 is complete. Phase 13 is not a future coding milestone; Host Migration was absorbed into Phase 11G and validated further during Phase 12.

# 20. Immediate next action

Create and push a meaningful **Phase 14A–14D UI/UX checkpoint** from the currently accepted local working tree.

The last verified pushed GitHub HEAD remains `0eca81450be91b30a5dc1cd66d901c46e2cab07c` (`Phase 12: finalize online resilience hardening and crash recovery`). This roadmap intentionally does not invent the SHA of the not-yet-created Phase 14 checkpoint.

The Phase 14A–14D checkpoint should include the accepted production source and roadmap changes, while excluding temporary recovery tooling such as local `Tools` repair scripts and `.bat` files if they are not intended repository artifacts.

After the push:

1. Verify the new `main` HEAD.
2. On the next roadmap update, record that already-existing pushed SHA/title at the top of this document.
3. Begin **Phase 14E — Final HUD Typography & Responsive Polish** from the newly pushed Phase 14A–14D checkpoint.
4. Then continue focused Result / Auction / Trade / Development presentation polish and the remaining responsive/accessibility Phase 14 backlog.

# 21. Current checkpoint summary

## Green / accepted

- Core gameplay/presentation foundation.
- Phase 3B seat/turn/AFK foundation.
- Phase 3D lobby/session UI.
- Phase 4 public browser.
- Phase 5 main online gameplay path.
- Phase 6 chat + moderation.
- Phase 8 gameplay/content checkpoint.
- Phase 9 Store / Economy / Seasonal Event.
- Phase 10 Career / progression / achievements / history.
- Phase 11A provider-neutral platform foundation.
- Phase 11B real Steam client integration with AppID 480 development testing.
- Phase 11C secure Steam account-linking architecture.
- Phase 11D returning Steam sign-in.
- Phase 11E live Steam recovery and achievement resync.
- Phase 11F shared Colorado/Turkey development multiplayer and reconnect hardening.
- Phase 11G graceful Host Migration, crash failover, scheduler handoff, stable dev identity, tablet localization cleanup, bankruptcy finalization.
- **Phase 12A stale/duplicate command hardening: PASS 12/12 + runtime smoke.**
- **Phase 12B TemporaryBot expiry hardening: PASS 10/10.**
- **Phase 12C repeated reconnect lifecycle: PASS 9/9.**
- **Phase 12D crash election: PASS 11/11 + Unity crash torture.**
- **Phase 12D.1 Purchase decision crash recovery: runtime PASS.**
- **Phase 12E developed-property bankruptcy Host/Guest cleanup: PASS.**
- Last verified pushed GitHub HEAD: `0eca81450be91b30a5dc1cd66d901c46e2cab07c`.
- **Phase 14A City Information Panel: locally accepted.**
- **Phase 14B Network Quality diagnostics + Pause-menu presentation: locally accepted.**
- **Phase 14C canonical profile-header identity: locally accepted.**
- **Phase 14D P1/P2/P3/P4 HUD alignment: locally accepted.**
- **Unity editor-menu grouping + targeted Unity 6.5/TMP warning cleanup: locally accepted.**
- Current accepted local state: **Phase 14A–14D ready for checkpoint commit/push; Phase 14E next.**

## Yellow / next

- Commit/push the accepted Phase 14A–14D checkpoint.
- Phase 14E final HUD typography/spacing and responsive long-text polish.
- Remaining modal-layering/resolution regression where still needed.
- Result/Auction/Trade/Development presentation polish.
- Controller/gamepad and accessibility-focused Phase 14 backlog.

## Production/later

- Real AtlasBoard Steam AppID/publisher credentials.
- App Check / Security Rules / least privilege / monitoring.
- Release backend.
- Build/performance QA.
- External live testing and release candidate.

# 22. Next-chat handoff block

A future AtlasBoard chat should start from these facts:

- Canonical roadmap: `Assets/Project/Documentation/MASTER_ROADMAP.md`, version 5.2.
- Roadmap checkpoint date: 2026-09-30 America/Denver.
- Last verified pushed GitHub HEAD: `0eca81450be91b30a5dc1cd66d901c46e2cab07c`.
- Last verified pushed HEAD title: `Phase 12: finalize online resilience hardening and crash recovery`.
- Current pushed checkpoint: **Phase 12 complete at `0eca814...`; Phase 14A–14D are accepted locally and pending checkpoint commit/push.**
- Phase 12A stale/duplicate/late intent protection is DONE; E2E PASS 12/12.
- Phase 12B TemporaryBot exact-expiry/race hardening is DONE; E2E PASS 10/10.
- Phase 12C five-cycle same-SeatId reconnect validation is DONE; E2E PASS 9/9.
- Phase 12D crash-election/split-brain backend torture is DONE; E2E PASS 11/11.
- Unity Host-crash torture passed during normal turn, pawn movement, dice animation and decision handling.
- Phase 12D.1 fixed the one observed Host-crash defect: unresolved remote Purchase decisions now survive authority failover and can complete once.
- Phase 12E developed-property bankruptcy cleanup is DONE on Host and Guest.
- Phase 12E acceptance observed: due=2, paid=1, unpaid=1, transferred=1, released=2, cheapest transferred property value=100.
- Bankrupt pawn remains gone; development markers remain cleared; released properties remain reusable/purchasable.
- Do not reimplement old Phase 13 Host Migration; it was absorbed into Phase 11G.
- **Phase 14 is ACTIVE. Phase 14A–14D are accepted locally; Phase 14E is NEXT after checkpoint push.**
- Phase 14A clickable/tappable City Information Panel is locally accepted.
- Phase 14B provider-neutral Network Quality diagnostics are locally accepted; final presentation is a compact Pause-menu row using existing Firebase snapshot traffic.
- Current hosting decision: keep Firebase/managed Google Cloud as the shared backend; do not add a self-managed dedicated server by default.
- Steam remains an adapter and optional future networking transport; Steam does not automatically replace Firebase or provide an Atlas dedicated server.
- Phase 14C canonical profile-header identity is locally accepted; generic `PLAYER` is now fallback-only.
- Phase 14D P1/P2/P3/P4 HUD alignment is locally accepted.
- Atlas Board Unity editor menu grouping and targeted Unity 6.5/TMP warning cleanup are locally accepted.
- Phase 14E Final HUD Typography & Responsive Polish is the next implementation target.
- Preserve canonical AccountId/SeatId; never replace them with SteamID.
- AppID 480 is development-only.
- Do not add `.cmd/.bat/.ps1` helper files unless explicitly requested. Temporary Phase 14 recovery scripts are local tooling and may be deleted rather than committed.
- Do not auto-commit/push.
- Runtime Unity changes require a fresh Guest build before two-client acceptance.
- Backend Functions changes require a successful TypeScript build and emulator restart before runtime acceptance.
- Phase 14A–14D currently sit locally on top of verified pushed HEAD `0eca81450be91b30a5dc1cd66d901c46e2cab07c`. Commit/push this checkpoint before Phase 14E, then verify the new `main` HEAD before further meaningful patches.

# 23. Roadmap maintenance rule

At every meaningful phase close:

1. Commit/push the accepted source checkpoint.
2. At the next roadmap update, record the latest already-existing pushed GitHub SHA, commit title and checkpoint date at the top of this file. Do not pretend a roadmap file can know the SHA of a commit that has not yet been created.
3. Update phase status and acceptance evidence.
4. Move completed future-scope items out of later phases rather than duplicating them.
5. Keep the Markdown roadmap in the repository as the canonical planning/handoff source.
6. Regenerate the Word archive when major roadmap structure changes.
