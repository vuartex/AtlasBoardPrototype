# AtlasBoard v5.49.12cc — Owner acceptance (no production changes made)

## Root cause and separation

This fixes an actual shared RTDB wallet rule bug. Existing account `Deneme Hesabı` has a missing `metaEconomy/dailyReward`, despite a valid profile, purchases, and existing DAILY receipt. Previously the parent wallet `.validate` required `dailyReward` on every PUT, so **PURCHASE**, **DAILY**, and **DEV_GRANT** all failed HTTP 401. Ordinary shopping never requires owner codes. Dev-grant remains restricted to approved testers.

New schema makes dailyReward OPTIONAL for ordinary wallet writes and only creates it on the first DAILY claim (or on wallet INIT with `lastClaimAt=0`). Clients preserve an old missing daily field across purchases/equips. For **DAILY ONLY**, immutable receipt history is used to prevent paying a previous recent claim again.

## Safest owner check — run emulator first

From `C:\Users\crazy\AtlasBoardPrototype\Backend\Firebase`:

```powershell
node qa/phase19cc_store_daily_dice_source_gate.test.cjs
firebase.cmd emulators:exec --only auth,database --project demo-atlasboard "node qa/phase19cc_store_daily_dice_emulator.test.cjs"
```

The runtime emulator test MUST return `ATB_PHASE19CC_STORE_DAILY_DICE PASS`. If it does not, **stop; do not deploy rules**. All emulator tests target the demo project, never production. Existing closed phases need not be reopened.

## Release/test gate

- Firebase Auth+RTDB emulator: **NOT RUN** by the assistant (tooling unavailable here).
- Unity 6000.5.6f1 build compilation: **NOT RUN** by the assistant.
- Production database rules deployment: **NOT DONE**; requires explicit owner authorization after emulator acceptance.
- GitHub push/commit: **NOT DONE**.

When accepted by the owner, only the following production rule file is changed: `Backend/Firebase/database.rules.json`. Use the already-existing Firebase project configuration. Do not manually import a JSON database export or touch `/users/...` data. The game cannot pass new store writes until the updated rules are deployed.

## Minimal Windows runtime acceptance

1. Run the existing **Build Two-Client Guest Test v1 → Launch Live Spark Guest QA (No Steam)** (do not add another Editor menu).
2. Sign in to the same registered `Deneme Hesabı` (or another normal test account). Verify a normal `PURCHASE` debits EXACT catalog price and grants only one item **without admin code**. Verify 401 no longer appears in `ATB_STORE_MUTATION`.
3. Test a truly never-claimed account. `DAILY` should create `dailyReward.lastClaimAt` and add exactly 50 Gold; repeating within 24h cannot add another 50. An account with a previous recent DAILY receipt but missing timestamp must **not** get early Gold.
4. On authorized dev tester only, verify **DEV BAKİYE** still requires the `devWalletTesters/<uid>` allowlist. A normal non-tester purchase and DAILY must work regardless.
5. Complete an eligible match and open Career. Check `careerDiceObservations/<same-auth-uid>/<matchId>` appears and remains after restarting/logging in from the same account on another device. This is account telemetry; it must not change Gold, XP, purchases, or Career match truth.
6. Preserve old user-approved PASS: emote inventory/unequip, server-status copy, Steam UID continuity, Final Round, local observed dice UI.

## Legacy limitation

For wallets that lost the daily timestamp, the immutable receipt read prevents the **official client** from granting a recent duplicate. RTDB security rules cannot dynamically aggregate randomly named older receipt entries to derive the precise prior DAILY date. Once a DAILY claim succeeds, the new `lastClaimAt` is persisted and normal server-side 24-hour enforcement resumes. This historic-data edge is not falsely marked as fully server-authoritative. Cloud dice totals/highest rolls are append-only, UID-owned, and bounded but client-observed, so they are not valid proof for awards.
