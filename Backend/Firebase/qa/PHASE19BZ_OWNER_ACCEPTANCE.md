# AtlasBoard v5.49.12bz — Owner Acceptance / No Production Writes

## Scope and baseline

- Patch on top of accepted `v5.49.12by`. It contains complete changed sources, not patch/PowerShell installers.
- Previously owner-accepted: Steam UID continuity, Guest registration/login, Gold and shop purchase transaction, password reset, sign-out confirmation layout.
- This archive does NOT modify production Firebase rules or user data. Do not deploy rules for this UI correction.
- Local user-device roll observations are NOT a substitute for server-attested historical dice proof.

## Test A: paid emote inventory and buttons

1. Windows QA: existing **Build Two-Client Guest Test v1** then **Launch Live Spark Guest QA (No Steam)**. Reuse the already registered account; do not create another UID.
2. Shop > Emotes: confirm previously purchased emote says Owned. Write down category/name, Gold, and latest history entry; no new purchase is necessary.
3. Career > Inventory > Emotes: verify that paid emote now appears alongside the 3 built-in free emotes, **without login dialog**. Watch for `ATB_INVENTORY_IDENTITY status=verified ... wallet_verified=True paid_emotes=...` (UID suffix only).
4. Select the purchased emote and press **Add**. Confirm that the button responds, active count changes, and reopening Career retains it.
5. Remove one of several active free emotes. **Remove** must be clickable if at least two remain. The last active emote must not be removable. Default board/dice remain selected if no paid selection is made.
6. Sign out. New Guest must **not inherit** that purchase. Sign in to the original account and confirm the paid emote reappears with the original wallet and inventory. Never combine Guest Gold with the registered wallet.
7. Inspect diagnostics if failed: `ATB_INVENTORY_IDENTITY`, `ATB_EMOTE_LOADOUT`, `ATB_EMOTE_INVENTORY`, `ATB_PHASE19C_RTDB_WALLET`, `ATB_SERVER_PROBE` (no credentials or raw UID required).

## Test B: Settings > Server Status

1. With registered REST account, open **Settings > Server Status**. No visible technical database name, URL, port or provider should appear.
2. Confirm Account and Cloud Data indicators turn green only after their own successful authenticated account-data read. Offline bots may show ready offline.
3. Online Rooms: reachable rooms only = amber/limited; after two live match networking samples during an active match, green/verified. No real match = never green solely from lobby access.
4. Shop: amber means **account ready, payment operation not tested**. Never green merely because catalog renders.
5. Test with network disabled: offline state without backend leakage. Test with signed-out Guest: account checks ask for sign-in rather than incorrectly claiming verified registered account.
6. Reopen after a minute to verify bounded checking. Diagnostics `ATB_SERVER_PROBE` log only response statuses and UID suffix, not tokens.
7. Seven locales: EN/TR/ES/FR/DE/KO/RU should show localized technical-free descriptions.

## Test C: Dice statistics

1. Existing signed Career proof contains dice **counts** but not sum/max; an old account with no local roll observation must show **—**, never an invented zero-derived average.
2. If this device has local saved dice rolls for same current UID and overlapping match IDs, average/highest show real values with **\*** and the text **\*Bu cihazda kaydedilen maçlar**. These are device-local samples, not claimed global lifetime totals.
3. If no local observation exists, run a FULL real match until match-recording and Career reload; verify the observed local roll path. This does not close the server-side proof extension.
4. Cross-device persistence needs a separate `careerFinalProofs` / `careerClaims` validation update with emulator negative tests; existing `$other: false` currently rejects new dice fields. Do not add these fields to production without owner-approved rules security regression.

## Sign-off

- Source contract: `node qa/phase19bz_inventory_status_dice_source_gate.test.cjs`.
- Unity compiler: **NOT RUN** here. Guest runtime: **NOT RUN** here. Live Firebase: **NOT MODIFIED / NOT QUERIED LIVE**.
- Full QA screenshots and logs remain acceptance evidence; do not mark the user test PASS without actual result.
- Server-forced version gate remains **owner-deferred**. Steam production release identity gate remains separately open.
