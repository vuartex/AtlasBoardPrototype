# AtlasBoard Phase 17A — Server-side result consistency (not a replay verifier)

## Intent

Owner release order: finish security/Unity tests, implement optional additions, **Android via Google Play first**, Steam later if desired. Keep PC and Steam code dormant but intact. No deployment, push, Steam AppID purchase, live credential change or production App Check activation in this package.

## Source review evidence

Unity `MatchResultManager.BuildOnlineResultSnapshot()` builds `netWorth = CurrentMoney + propertyValue + developmentValue`. It selects all tied non-bankrupt participating players with the maximal net worth. The completion transport currently accepted any syntactically parseable host `matchResult.valid = true` when revision and eventSequence exceeded low thresholds. It did not reject contradictions among the alleged result arrays. Therefore this package adds a *pure deterministic consistency check* in the production host completion route. It does **not** establish that game turns, dice or economic actions truly happened.

## Changed files

- `Backend/Firebase/functions/src/security/match-result-consistency.ts`: strict final-frame shape, 4 slots, safe integers, net-worth arithmetic, winner/tie/bankruptcy logic, inactive-slot zeroes, non-negative asset values. Pure function returns a reason code only; it cannot write proof.
- `Backend/Firebase/functions/src/security/match-integrity-gates.ts`: canonical full replacement preserving existing App Check, host-only, revision, premature-completion and verified-receipt gates; before a host calls the state publisher with `phase=match_complete`, assert the new result consistency contract and reject invalid data with `match.error.invalid_result_consistency`.
- `Backend/Firebase/functions/scripts/match-result-consistency-e2e.mjs`: 19 unit cases using the *compiled* TS module plus 12 real local emulator cases. Negative attempts must leave state revision unchanged. A self-consistent final result can be displayed by the players but still cannot issue permanent XP with no independent proof.
- `Backend/Firebase/functions/scripts/qa.mjs`: Phase 17A first in full E2E sequence, then Core07, Core06 and every prior baseline. The offline 19-case compiled-module self-test is part of the same full run.
- `MASTER_ROADMAP.md`: owner Core07 acceptance and this stage/remaining gates.

## Acceptance / reproduction

Unzip at repo root. From `Backend\Firebase\functions` run **only** `node scripts\qa.mjs --full`, using the owner's existing installed dependencies. New required markers:

- `Phase 17A compiled validator UNIT PASS: 19/19 cases. No emulator calls.`
- `ATLASBOARD_PHASE_17A_CONSISTENCY_PASS: 12/12 (emulator only).`
- Existing Core07 `23/23`, Core06 `28/28`, original Firestore rules `17/17`, Core05 `20/20`, all accepted suites and **terminal `AtlasBoard QA FULL LOCAL PASS (emulators only).`**

Do NOT accept source-only checks as proof of emulator runtime success. Current packaging container has no Firebase CLI or full installed Functions dependency tree. The Firebase tests are for LOCAL test user fixtures only and perform cleanup. They must not touch production.

## Remaining integrity architecture

The app currently lets a Unity host authoritatively compute and publish the game state. Even a host whose final snapshot is self-consistent can fabricate a complete game. Existing `match_result_verifications` documents are created only by privileged test fixture infrastructure; there is **no real independent server replay writer**. The permanent XP grant remains fail-closed. Before releasing *ranked rewards*, implement a server-validated per-turn event log + deterministic Unity rules port/replay (or fully server-owned game simulation), including server-side dice/randomness, turn/rule enforcement, outcome calculation, concurrency, host migration and idempotent receipt creation. A hash chain of host messages alone is insufficient.

The current `progressionRecordCompletedMatch` accepts bounded host-provided completed-turn and dice telemetry once a trusted receipt exists. Before real verifier activation, move every reward-relevant field to the verified server-owned receipt or replay output; do not treat host telemetry as independently validated. Existing 14/14 progression test uses synthetic proof fixtures strictly as local development, not a demonstration of independent live match verification.

Android Unity editor compilation, account login, platform preferences sync/migration, Android input, physical multiplayer and release App Check integration are separate as-yet-unverified gates. Steam production remains blocked pending real AppID and deployed integration; preserve the code for later.
