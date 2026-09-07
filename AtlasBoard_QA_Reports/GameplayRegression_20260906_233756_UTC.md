# Atlas Board Gameplay Regression Report

Generated UTC: 2026-09-06T23:37:56.4529168Z
Unity: 6000.5.6f1
Scene: Assets/Board_Prototype.unity

## Static preflight

- PASS — Active scene is saved and valid.
- PASS — Scene component present: TurnManager
- CHECK — Scene component present: PrototypeDiceController
- PASS — Scene component present: DiceVisualController
- PASS — Scene component present: AuctionManager
- PASS — Scene component present: TradeManager
- PASS — Scene component present: PropertyDevelopmentManager
- PASS — Scene component present: BankruptcyManager
- PASS — Scene component present: MatchResultManager
- PASS — Scene component present: EventCardManager
- PASS — Scene component present: SpecialTileManager
- PASS — Scene component present: MatchSetupManager
- PASS — Scene component present: AtlasBoardTurnDiceNetworkCoordinator
- PASS — Scene component present: AtlasBoardLobbyRuntimeBridge
- PASS — PlayerGameState scene capacity >= 4 (found 4).
- PASS — EventDeckDefinition asset exists.
- PASS — Event Deck contains at least 36 cards (largest=36).
- PASS — Map/content asset search contains 'Turkey'.
- PASS — Map/content asset search contains 'Colorado'.
- PASS — Map/content asset search contains 'USA'.
- PASS — Phase 6 chat UI source/component is present.

## Regression matrix

### Turn / Dice

- **TURN-01 — Starting order** — `NotTested`
  - Setup: 2–4 players; fresh match.
  - Expected: Each active seat rolls once; highest starts; no duplicate/missing seat.
- **TURN-02 — Starting-order tie reroll** — `NotTested`
  - Setup: Force/observe a tie in starting order.
  - Expected: Only tied players reroll until resolved.
- **TURN-03 — 2d6 visual/result sync** — `NotTested`
  - Setup: Roll repeatedly.
  - Expected: Visual die faces equal authoritative gameplay result every roll.
- **TURN-04 — Doubles extra roll** — `NotTested`
  - Setup: Doubles enabled; roll doubles.
  - Expected: Same player receives exactly one extra roll.
- **TURN-05 — Triple-double penalty** — `NotTested`
  - Setup: Triple-double rule enabled; obtain three consecutive doubles.
  - Expected: Penalty triggers once and turn flow remains valid.
- **TURN-06 — Rest + doubles** — `NotTested`
  - Setup: Land on Rest with a double, Continue.
  - Expected: No extra roll after Rest acknowledgement; turn passes to next player.
- **TURN-07 — Human roll timeout** — `NotTested`
  - Setup: Human does not roll for configured timeout.
  - Expected: Authoritative automatic roll occurs without duplicate roll.

### Economy / Property

- **ECO-01 — Unowned property purchase** — `NotTested`
  - Setup: Human lands on purchasable unowned property and buys.
  - Expected: Money decreases once; ownership updates on both clients.
- **ECO-02 — Decline purchase -> auction** — `NotTested`
  - Setup: Human declines an unowned property.
  - Expected: Auction opens once with correct property and eligible bidders.
- **ECO-03 — Rent payment** — `NotTested`
  - Setup: Player lands on another player's property.
  - Expected: Correct rent transfers exactly once on Host and Guest.
- **ECO-04 — Ownership persistence** — `NotTested`
  - Setup: Acquire several properties then continue multiple turns.
  - Expected: Ownership remains stable across snapshots/UI refreshes.
- **ECO-05 — Money synchronization** — `NotTested`
  - Setup: Trigger purchase, rent, tax and bonus changes.
  - Expected: Host/Guest balances converge to the same authoritative values.

### Special Tiles / Cards

- **SPEC-01 — Event card** — `NotTested`
  - Setup: Land on Event.
  - Expected: One card resolves once; localized title/body/result appear; state syncs.
- **SPEC-02 — Bonus** — `NotTested`
  - Setup: Land on Bonus.
  - Expected: Reward applies once and money syncs.
- **SPEC-03 — Tax** — `NotTested`
  - Setup: Land on Tax.
  - Expected: Tax applies once and does not double-charge follower.
- **SPEC-04 — Travel** — `NotTested`
  - Setup: Land on Travel and complete selection/flow.
  - Expected: Pawn/state resolve consistently on both clients.
- **SPEC-05 — Vacation** — `NotTested`
  - Setup: Land on Vacation.
  - Expected: Vacation effect/state resolves and turn continues correctly.
- **SPEC-06 — Rest** — `NotTested`
  - Setup: Land on Rest without doubles.
  - Expected: Continue/acknowledgement resolves once and passes turn.
- **SPEC-07 — 36-card Event Deck** — `NotTested`
  - Setup: Run static preflight and sample cards.
  - Expected: Deck asset contains >=36 cards; no missing runtime card reference.

### Auction

- **AUC-01 — Human vs Human auction** — `NotTested`
  - Setup: Two Human seats bid/pass.
  - Expected: Bid authority, winner, price, money and ownership sync.
- **AUC-02 — Human vs Bot auction** — `NotTested`
  - Setup: Include at least one active Bot bidder.
  - Expected: Bot decision does not block Human and final state syncs.
- **AUC-03 — All but one pass** — `NotTested`
  - Setup: All bidders except one pass.
  - Expected: Auction closes deterministically with the correct winner/price.

### Trade

- **TRD-01 — Host -> Guest trade** — `NotTested`
  - Setup: Host Human proposes a trade to Guest Human.
  - Expected: Guest alone owns the remote decision; accept/reject syncs once.
- **TRD-02 — Guest -> Host trade** — `NotTested`
  - Setup: Guest Human proposes a trade to Host Human.
  - Expected: Host receives correct decision and final assets/money sync.
- **TRD-03 — Trade cancel/reject** — `NotTested`
  - Setup: Open a valid trade then cancel/reject.
  - Expected: No assets/money change; turn/control flow remains responsive.
- **TRD-04 — Trade during roll timeout** — `NotTested`
  - Setup: Open Trade during Human roll window.
  - Expected: Roll timeout pauses/resets; no surprise auto-roll during trade.

### Development

- **DEV-01 — Build development** — `NotTested`
  - Setup: Own eligible set/property and develop.
  - Expected: Cost/development level/visual update once on both clients.
- **DEV-02 — Balanced development rule** — `NotTested`
  - Setup: Balanced rule enabled; attempt invalid uneven build.
  - Expected: Invalid development is blocked consistently.
- **DEV-03 — Developed rent** — `NotTested`
  - Setup: Land on developed opponent property.
  - Expected: Rent uses current development level and syncs.
- **DEV-04 — Development visual sync** — `NotTested`
  - Setup: Build multiple levels while Host/Guest observe.
  - Expected: Both clients show the same development visuals.
- **DEV-05 — Bankruptcy development cleanup** — `Deferred`
  - Setup: Bankrupt owner of developed property.
  - Expected: All development levels and visuals clear on both clients.

### Bankruptcy / Result

- **BANK-01 — Core bankruptcy** — `NotTested`
  - Setup: Drive one player below bankruptcy threshold.
  - Expected: Player is removed/converted according to rules without match deadlock.
- **BANK-02 — Result winner/net worth** — `NotTested`
  - Setup: Finish match with known standings.
  - Expected: Winner and displayed net worth/estate values are correct.
- **BANK-03 — Guest result controls** — `NotTested`
  - Setup: Finish as Guest.
  - Expected: Guest sees host-wait message + Leave; no Restart.
- **BANK-04 — Host rematch** — `NotTested`
  - Setup: Finish as Host and click rematch once.
  - Expected: Button disables/loads; both clients return to same lobby.

### Online Authority / Session

- **NET-01 — Human authority label** — `NotTested`
  - Setup: First online match start from Unity Editor Human seat.
  - Expected: HUD says HUMAN/İNSAN immediately and seat never auto-acts as Bot.
- **NET-02 — Host/Guest decision ownership** — `NotTested`
  - Setup: Exercise purchase/trade/auction/development decisions.
  - Expected: Only the owning Human client can submit its decision; Host remains authoritative.
- **NET-03 — Pawn synchronization** — `NotTested`
  - Setup: Both clients observe several moves/special moves.
  - Expected: Pawn positions converge without replay/duplicate movement.
- **NET-04 — Rematch clean second match** — `NotTested`
  - Setup: Complete match -> rematch -> start second match.
  - Expected: No old money/property/pawn/modal/result state leaks into match two.
- **NET-05 — New lobby without app restart** — `NotTested`
  - Setup: Leave result/lobby flow then create/join another room.
  - Expected: Fresh game starts cleanly without restarting executable.
- **NET-06 — Dice reuse after rematch/new lobby** — `NotTested`
  - Setup: Start second match/session without app restart.
  - Expected: Dice remain present and functional.
- **NET-07 — Voluntary Leave -> TemporaryBot -> Rejoin** — `Deferred`
  - Setup: Late-resilience milestone.
  - Expected: Same seat reclaims within reservation window with state catch-up.
- **NET-08 — Repeated reconnect cycles** — `Deferred`
  - Setup: Late-resilience milestone.
  - Expected: Multiple disconnect/reconnect cycles do not duplicate/replay state.
- **NET-09 — Host migration** — `Deferred`
  - Setup: Dedicated future milestone.
  - Expected: New Host receives authority safely after Host departure.

### Player Matrix

- **PLY-01 — 2-player Human/Human** — `NotTested`
  - Setup: Run representative full-turn smoke with 2 Human players.
  - Expected: No blocked turn/decision; state remains synchronized.
- **PLY-02 — 2-player Human/Bot** — `NotTested`
  - Setup: Run representative full-turn smoke with Human + Bot.
  - Expected: Bot acts only for its seat; Human remains interactive.
- **PLY-03 — 3-player mixed** — `NotTested`
  - Setup: Run 3-player Human/Human/Bot or equivalent.
  - Expected: All three seats rotate and resolve decisions correctly.
- **PLY-04 — 4-player mixed** — `NotTested`
  - Setup: Run 4-player mixed Human/Bot session.
  - Expected: All four seats remain distinct with stable HUD/pawns/turn order.

### UI / Themes / Localization

- **UI-01 — Board invariant across themes** — `NotTested`
  - Setup: Switch Classic/Garden/Beach/Pavilion/Street.
  - Expected: Only environment changes; game-board walnut edge + warm center stay invariant.
- **UI-02 — HUD no-overlap** — `NotTested`
  - Setup: Observe all 4 HUD cards and long/localized names.
  - Expected: Name/money/control/turn badge remain readable; P1 icon alignment is polish-only.
- **UI-03 — Seven-language smoke** — `NotTested`
  - Setup: Cycle EN/TR/ES/FR/DE/KO/RU through menu/lobby/match.
  - Expected: No missing glyphs, major overlap or untranslated critical action labels.
- **UI-04 — Lobby + Match Chat smoke** — `NotTested`
  - Setup: Host/Guest send messages in lobby and active match.
  - Expected: Messages sync, unread badge works, chat does not block gameplay HUD.
- **UI-05 — Chat server constraints** — `NotTested`
  - Setup: Try empty, 120, 121 chars, <10s resend, profanity.
  - Expected: Server enforces empty/length/rate/profanity rules consistently.
- **UI-06 — Chat server time/UTC offset** — `NotTested`
  - Setup: Send from clients with different UTC offsets.
  - Expected: Canonical server instant is used; sender-local HH:mm (UTC±X) displays correctly.
