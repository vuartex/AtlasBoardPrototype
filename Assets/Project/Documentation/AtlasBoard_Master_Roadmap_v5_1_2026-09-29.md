# ATLAS BOARD
## Master Architecture & Development Roadmap v5.1

**Canonical checkpoint:** 2026-09-29 (America/Denver)  
**Repository:** `vuartex/AtlasBoardPrototype`  
**Last verified pushed GitHub HEAD:** `068ca6f0d1736516ace63b79681227202a408cf8`  
**Last verified pushed HEAD commit:** `Phase 11G: finalize host migration and multiplayer resilience`  
**Last verified pushed timestamp:** 2026-09-27 23:21:42 America/Denver / 2026-09-28 05:21:42 UTC  
**Current accepted local checkpoint:** `Phase 12 complete — pending Git commit/push`  
**Planned Phase 12 close commit title:** `Phase 12: finalize online resilience hardening and crash recovery`  
**Unity baseline:** Unity 6000.5.6f1, `Assets/Board_Prototype.unity`, unless a later committed upgrade explicitly changes it.  
**Canonical repository roadmap location:** `Assets/Project/Documentation/MASTER_ROADMAP.md`  
**Previous human-readable archive copy:** `AtlasBoard_Master_Roadmap_v5_0_2026-09-27.docx`

This v5.1 document supersedes Master Roadmap v5.0 dated 2026-09-27. It records the accepted completion of Phase 12 on top of the Phase 11G checkpoint, including stale-command hardening, TemporaryBot expiry hardening, repeated reconnect validation, crash-election torture, purchase-decision crash recovery, and developed-property bankruptcy cleanup.

The most important planning change from v5.0 is that the focused Phase 12 resilience work is now accepted and closed. The old standalone Phase 13 implementation milestone remains retired because Host Migration was absorbed into Phase 11G. The next active milestone is Phase 14 Dedicated UI/UX Polish.

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

- `LOCAL ACCEPTED / PENDING COMMIT` — Phase 12 final resilience checkpoint (12A through 12E accepted on 2026-09-29).
- `068ca6f0d1736516ace63b79681227202a408cf8` — last verified pushed GitHub HEAD; Phase 11G final.
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

When the user asks for the next phase, use this v5.1 sequence unless an explicit blocker or new product requirement changes priority. Avoid repeating work already accepted in prior phases. A later regression does not automatically reopen a completed phase; repair the regression in the current hardening/QA phase unless architecture actually changed.

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

Current v5.1 status:

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

Remaining non-blocking polish:

- Top-left generic `PLAYER` profile/header binding should use the canonical signed-in profile identity instead of remaining generic.
- P1/P2/P3/P4 icon/badge alignment.
- Final typography/spacing.
- Long-text/responsive polish.

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

**Status: LATER / focused polish backlog.**

Already improved during 11F/11G:

- Public Rooms dedicated Join by Code.
- Modal layering.
- Host leave pending UX.
- Trade localization.
- Bankruptcy result localization.

Remaining:

- Canonical profile name in the top-left header instead of generic `PLAYER`.
- P1/P2/P3/P4 HUD badge alignment.
- Final HUD typography and spacing.
- Result screen polish.
- Auction/Trade/Development visual polish.
- Responsive resolution/aspect-ratio review.
- Korean/Russian/German long-text regression.
- Controller/gamepad navigation.
- Colorblind-safe ownership indicators.
- Reduced-motion consistency.
- Mobile-safe layout if mobile becomes active scope.

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
- Warning cleanup.
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

Commit and push the accepted Phase 12 checkpoint together with this roadmap update.

Because a Git commit cannot reliably contain its own final SHA before the commit exists, this v5.1 file records:

- the last verified pushed parent HEAD: `068ca6f0d1736516ace63b79681227202a408cf8`;
- the accepted local Phase 12 close state;
- the planned Phase 12 close commit title.

After the Phase 12 commit is pushed, verify the new GitHub HEAD before the first Phase 14 source patch and update this roadmap's checkpoint metadata at the next documentation checkpoint.

Then begin **Phase 14 - Dedicated UI/UX Polish**.

Recommended first Phase 14 work package:

1. Replace the top-left generic `PLAYER` header with canonical signed-in/profile identity.
2. Audit P1/P2/P3/P4 HUD icon/badge alignment.
3. Run a modal-layering pass across Main Menu, Public Rooms, Private Table, Career, Store, Result, Auction, Trade, Development and decision panels.
4. Fix responsive layout / long-text issues for EN/TR/ES/FR/DE/KO/RU.
5. Polish Result, Auction, Trade and Development presentation without changing accepted gameplay/network authority.

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
- Last verified pushed GitHub HEAD before Phase 12 close commit: `068ca6f0d1736516ace63b79681227202a408cf8`.
- Current accepted local state: **Phase 12 complete / pending commit + push.**

## Yellow / next

- Phase 14 UI/UX polish.
- Canonical profile-header identity instead of generic `PLAYER`.
- HUD icon/badge alignment.
- Modal layering and responsive/long-text polish.
- Result/Auction/Trade/Development presentation polish.

## Production/later

- Real AtlasBoard Steam AppID/publisher credentials.
- App Check / Security Rules / least privilege / monitoring.
- Release backend.
- Build/performance QA.
- External live testing and release candidate.

# 22. Next-chat handoff block

A future AtlasBoard chat should start from these facts:

- Canonical roadmap: `Assets/Project/Documentation/MASTER_ROADMAP.md`, version 5.1.
- Roadmap checkpoint date: 2026-09-29 America/Denver.
- Last verified pushed GitHub HEAD: `068ca6f0d1736516ace63b79681227202a408cf8`.
- Last verified pushed HEAD title: `Phase 11G: finalize host migration and multiplayer resilience`.
- Current accepted local checkpoint: **Phase 12 complete; pending Phase 12 close commit/push.**
- Planned Phase 12 close commit title: `Phase 12: finalize online resilience hardening and crash recovery`.
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
- **Phase 14 Dedicated UI/UX Polish is NEXT.**
- First Phase 14 target should include the generic top-left `PLAYER` identity/header binding.
- Preserve canonical AccountId/SeatId; never replace them with SteamID.
- AppID 480 is development-only.
- Do not add `.cmd/.bat/.ps1` helper files unless explicitly requested.
- Do not auto-commit/push.
- Runtime Unity changes require a fresh Guest build before two-client acceptance.
- Backend Functions changes require a successful TypeScript build and emulator restart before runtime acceptance.
- Before any Phase 14 patch, verify the post-Phase-12 GitHub HEAD and inspect newer source rather than using the old `068ca6f0...` source blindly.

# 23. Roadmap maintenance rule

At every meaningful phase close:

1. Commit/push the accepted source checkpoint.
2. At the next roadmap update, record the latest already-existing pushed GitHub SHA, commit title and checkpoint date at the top of this file. Do not pretend a roadmap file can know the SHA of a commit that has not yet been created.
3. Update phase status and acceptance evidence.
4. Move completed future-scope items out of later phases rather than duplicating them.
5. Keep the Markdown roadmap in the repository as the canonical planning/handoff source.
6. Regenerate the Word archive when major roadmap structure changes.
