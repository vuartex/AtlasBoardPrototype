# v5.49.12ca — Owner acceptance: registered store and QA console

## Scope

Only `AtlasBoardRtdbMetaEconomy.cs` and `AtlasBoardOwnerConsole.cs` are modified runtime files. Prior `bz` Career inventory, emote equip and Cloud Data status screen are intentionally preserved. **No Firebase Rules edits or production deploy.**

## A. Store daily / purchase

1. Rebuild with the existing **Build Two-Client Guest Test v1 → Launch Live Spark Guest QA (No Steam)** flow, using the same registered Firebase user.
2. Capture current UID (redacted in logs), Gold, Atlas Coin and daily-claim history; make sure the account isn't switched during testing.
3. Open Store. Run a daily reward claim and a purchase once only; a date's already-claimed reward must remain idempotent.
4. If denied, extract safe log lines with prefixes `ATB_STORE_AUTH_REFRESH`, `ATB_STORE_SESSION`, `ATB_STORE_WALLET_READ`, `ATB_STORE_MUTATION`, and `ATB_PHASE19C_RTDB_WALLET`. They include HTTP codes, not password/token.
5. No new UID, no duplicate Gold, no accidental item entitlement and no partial charge are acceptable. HTTP 403 *after* refresh is a server authorization rejection, **not** evidence that the client should bypass the rule.

## B. Console / fast-forward

1. The existing session code hash matches the owner's original local QA-code document; if the console says `server_allowlisted=false`, the code is **not** the problem.
2. Firebase Console → Authentication → Users: find the **exact UID** for the active test account. Firebase Console → Realtime Database → Data: `/devWalletTesters/<UID>` must exist as JSON `true` for that approved user. Do not enable this for all players. The flag is a powerful test-only privilege; revoke before production.
3. In Development/Editor, enter session access once, then final round or finish-turn command as the actual online host. Check log for `ATB_DEV_MATCH_FAST_FORWARD rejected=...` if the command reports a request but game state did not change.
4. Guest QA / other login UID cannot use a previously authorized Steam UID's QA entitlement.

## C. Status

Source gate PASS is not a Unity compile, server emulator PASS or actual owner run. Client gate + server allowlist must both succeed for the admin console. Production Firebase edits remain under owner control.
