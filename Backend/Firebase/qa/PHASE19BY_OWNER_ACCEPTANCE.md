# AtlasBoard v5.49.12by — Owner QA / scope and gates

## Real owner evidence for v5.49.12bx — ACCEPTED
- Same registered REST/Firebase UID (`mMFv2c…` in redacted logs) in Career, Settings, Store and wallet.
- Daily reward transaction PASS: 180 -> 230 Gold (+50), exactly one RTDB operation.
- Paid emote transaction PASS: 230 -> 80 Gold (-150), matching wallet and store history.
- Account display-name update PASS; later immediate rename received HTTP 401 and needs user-friendly cooldown/validation reporting.
- Sign out to another Guest and return to the original account: owner reports existing account remains distinct and old balance reloads.

## This ZIP fixes (source candidate; NOT Unity-validated)
- Sign-out confirmation two-button layout: independently positioned Cancel / Sign out, no overlapping target areas.
- Emote loadout identifies live Spark Windows Guest QA using the active REST UID/token even without a multiplayer lobby, not the stripped native FirebaseAuth singleton.
- Restore missing/empty emote loadout to available free default(s); at least one emote required for SaveAsync (client-side). Paid emote ownership still verified from the SAME UID's canonical wallet, and ETag prevents overwriting another device.
- Explicit UID-scoped emote cache invalidation on registered-account logout; new Guest never inherits previous active emotes.
- Log `ATB_EMOTE_LOADOUT` and `ATB_EMOTE_INVENTORY` without exposing tokens, emails, redemption codes or full UIDs.

## STILL OPEN: do not mark PASS
1. **P0 Career Inventory**: `AtlasBoardProgressionUI.RefreshInventoryAsync()` currently requires native `AtlasBoardAccountService.IsSignedIn`; standalone REST-registered QA has no native AccountService. The player can correctly purchase a paid emote but Inventory shows only free defaults. The canonical Career source file must be modified in its entirety and tested before this is closed. A runtime duplicate manager/reflection identity bypass is NOT acceptable.
2. **P0 remove emote button**: Career Inventory button can remain inactive because native `inventoryAccount` is null. This package fixes loadout identity and minimum selection at service level but DOES NOT fix that UI gate.
3. **P1 dice stats telemetry**: journal merge counts dice rolls and doubles but does not populate `totalDiceValue` or `highestRoll` from peer-attested claims. Historical raw die values do not exist in those claims; do not invent historical averages or maximums. Future claims/projection/schema and honest no-data UI need separate, fully sourced code change.
4. **P1 server status UI**: native AccountService health rows still report signed out for verified REST QA. UI strings also expose `RTDB`. Need real per-probe status checks with `ok`, `connecting`, `auth_needed`, `offline`, `degraded`, `stale` cases, and customer-safe labels. This package does not alter SettingsV2Controller.
5. **P1 Redeem/promo**: current Store code calls `promoTestRedeem` Cloud Function, not RTDB-only wallet endpoint. No known production promo code tested. Never publish the private QA console unlock codes as promo codes; no invented working sample.
6. **P1 Guest origin**: immutable original guest audit snapshot is pending rules/schema design; never create a second spendable balance.
7. **OWNER DEFERRED**: Firebase-enforced client version gate; do not deploy it without owner request.

## Regression (same existing Unity menu; no new menu)
- Unity Console: no CS errors and no Console exceptions.
- Windows QA: `Atlas Board > Online & Backend > Online > Build Two-Client Guest Test v1`; launch `Launch Live Spark Guest QA (No Steam)`.
- Registered A UID owns paid `emote_sleep` (or purchased item): `ATB_EMOTE_INVENTORY paid_owned=...`, `ATB_EMOTE_LOADOUT status=loaded` and no `identity_mismatch`; NO new UID. Old account purchases/daily/XP unchanged.
- Sign-out confirm: Cancel and Sign out buttons must be physically separate. Cancel no UID change. Confirm changes to new anonymous UID once, old Gold/XP/history not copied. Re-login A returns A's balance only.
- Steam Editor/Development: separate SteamIDs maintain separate UIDs; no change to steam source in this patch.
- Emote selection: saving `0` emotes must fail; 1..6 distinct owned emotes succeed server rules permitting. Normal inventory button remains an OPEN UI item until original ProgressionUI is patched.
- Do NOT run live production mutation tests on somebody else's UID or alter RTDB rules for convenience.

No Firebase production deploy, GitHub push, user deletion, live Unity compile or live emulator run was performed by this ZIP author.
