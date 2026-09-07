# Atlas Board Phase 8.1 Focused Regression Report v2

Generated UTC: 2026-09-07T04:11:02.0524071Z
Unity: 6000.5.6f1
Scene: Assets/Board_Prototype.unity
Gate: NOT READY

## Accepted historical baseline

- Previously accepted Phase 5/6 paths are intentionally not duplicated in this focused matrix.
- Late resilience, bankruptcy development visual cleanup E2E, and Host Migration remain deliberately deferred.

## Static preflight v2

- PASS - Active scene is saved and valid.
- PASS - Scene component present: TurnManager
- PASS - Scene component present: DiceVisualController
- PASS - Scene component present: AuctionManager
- PASS - Scene component present: TradeManager
- PASS - Scene component present: PropertyDevelopmentManager
- PASS - Scene component present: BankruptcyManager
- PASS - Scene component present: MatchResultManager
- PASS - Scene component present: EventCardManager
- PASS - Scene component present: SpecialTileManager
- PASS - Scene component present: MatchSetupManager
- PASS - Scene component present: AtlasBoardTurnDiceNetworkCoordinator
- PASS - Scene component present: AtlasBoardLobbyRuntimeBridge
- PASS - PlayerGameState scene capacity >= 4 (found 4).
- PASS - EventDeckDefinition asset exists.
- PASS - Event Deck contains at least 36 cards (largest=36).
- PASS - Turkey map contains required Manisa content.
- PASS - Colorado map contains required Longmont content.
- PASS - USA map contains required Colorado content.
- PASS - Phase 6B Chat Safety UI source exists.
- PASS - Chat Safety does not use deprecated GetInstanceID().
- PASS - Chat Safety uses GetEntityId() for TMP text-scale identity.
- INFO - PrototypeDiceController is legacy/optional and is NOT a required scene component. At the reviewed GitHub checkpoint, code search found only its class source and the old v1 QA check/report; the active dice path is validated through DiceVisualController + network coordinator.

## Focused runtime matrix

### Turn rules

- **F08-01 - Starting-order tie reroll** - `NotTested`
  - Setup: 2-4 players; force or observe a tie during starting order.
  - Expected: Only tied seats reroll until a unique winner is selected; no missing/duplicate seat.
- **F08-02 - Triple-double penalty** - `NotTested`
  - Setup: Triple-double rule enabled; obtain three consecutive doubles.
  - Expected: Penalty triggers exactly once and the next turn remains valid on Host/Guest.
- **F08-03 - Human roll timeout** - `NotTested`
  - Setup: Human does not roll during the configured timeout.
  - Expected: One authoritative automatic roll occurs; no duplicate roll or incorrect Bot label/control.

### Auction / Economy

- **F08-04 - Decline purchase -> Human auction** - `NotTested`
  - Setup: Two Human players; decline an unowned property.
  - Expected: Auction opens once; Human bid/pass authority, winner, price, money and ownership synchronize.
- **F08-05 - Human vs Bot auction** - `NotTested`
  - Setup: At least one Human and one Bot bidder.
  - Expected: Bot acts only for its seat; Human remains interactive; final winner/price/state synchronize.
- **F08-06 - Developed rent** - `NotTested`
  - Setup: Develop a property, then another player lands on it.
  - Expected: Rent uses the current development level and transfers exactly once on both clients.

### Special tiles

- **F08-07 - Travel path** - `NotTested`
  - Setup: Land on Travel and complete its selection/flow.
  - Expected: Authoritative destination/pawn/state agree on Host and Guest; turn flow remains responsive.
- **F08-08 - Vacation path** - `NotTested`
  - Setup: Land on Vacation and complete its effect.
  - Expected: Vacation state/effect resolves once and later turn flow remains correct.
- **F08-09 - Event-card runtime sample** - `NotTested`
  - Setup: Resolve several Event cards including movement and money outcomes.
  - Expected: Each card resolves once with localized UI and synchronized authoritative outcome.

### Trade

- **F08-10 - Host -> Guest trade** - `NotTested`
  - Setup: Host Human proposes a valid trade to Guest Human.
  - Expected: Guest owns the remote decision; accept synchronizes assets/money exactly once.
- **F08-11 - Guest -> Host trade** - `NotTested`
  - Setup: Guest Human proposes a valid trade to Host Human.
  - Expected: Host receives the decision; accept synchronizes assets/money exactly once.
- **F08-12 - Trade reject/cancel + timeout pause** - `NotTested`
  - Setup: Open a valid trade during a Human roll window, then reject/cancel.
  - Expected: No asset change; no surprise auto-roll while trade is open; normal turn flow resumes.

### Development / Bankruptcy

- **F08-13 - Balanced development rule** - `NotTested`
  - Setup: Balanced-development rule enabled; attempt an invalid uneven build.
  - Expected: Invalid build is blocked consistently; valid build still works afterward.
- **F08-14 - Core bankruptcy + result values** - `NotTested`
  - Setup: Drive a player into bankruptcy and finish a match with known standings.
  - Expected: Match does not deadlock; bankrupt seat resolves correctly; winner/net-worth values are correct.

### Player matrix

- **F08-15 - 2-player Human/Human smoke** - `NotTested`
  - Setup: Two Human clients; complete representative turns including at least one decision.
  - Expected: Turns rotate, decisions remain seat-owned, money/pawns converge, no blocked UI.
- **F08-16 - 2-player Human/Bot smoke** - `NotTested`
  - Setup: One Human plus one Bot; complete representative turns.
  - Expected: Bot acts only for its seat; Human remains interactive; turn loop remains stable.
- **F08-17 - 3-player mixed smoke** - `NotTested`
  - Setup: Three active seats, preferably Human/Human/Bot.
  - Expected: All seats remain distinct; HUD/pawns/turn order and decisions stay correct.
- **F08-18 - 4-player mixed smoke** - `NotTested`
  - Setup: Four active seats with a Human/Bot mix.
  - Expected: All four seats rotate without duplication, blocked decisions, HUD/pawn crossover or authority leaks.

### Localization

- **F08-19 - Seven-language critical UI smoke** - `NotTested`
  - Setup: Cycle EN/TR/ES/FR/DE/KO/RU through menu, lobby and active match.
  - Expected: Critical action labels/glyphs are readable; no major overlap or missing glyph that blocks play.
