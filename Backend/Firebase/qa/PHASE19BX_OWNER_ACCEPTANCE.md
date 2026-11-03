# AtlasBoard v5.49.12bx — Account/Store Acceptance

Status: CODE CHANGES DELIVERED, UNITY & LIVE FIREBASE TEST **NOT RUN** by assistant.

## Apply
Extract the ZIP to `C:\Users\crazy\AtlasBoardPrototype` and preserve its `Assets/` and `Backend/` subdirectories. No PS1, patch runner, new scene, or new Unity menu. No Firebase deploy (rules unchanged), no GitHub push.

In Unity 6000.5.6f1, check Console for **zero C# compile errors**. Build using the existing **Atlas Board → Online & Backend → Online → Build Two-Client Guest Test v1**, then **Launch Live Spark Guest QA (No Steam)**.

## Acceptance in order

1. In the registered QA guest (same existing email account), cold start and wait for `ATB_SESSION_GATE auth=registered_rest_verified`. The `HESAP` action under Career/Settings should display a modern account panel with name, email, **ADI DEĞİŞTİR** (24-hour RTDB rule), **ŞİFREYİ DEĞİŞTİR** (sends Firebase reset email) and **HESAPTAN ÇIKIŞ YAP**. No direct raw UID/token/password in player UI.
2. Open **MAĞAZA**. Expected: not `GİRİŞ / KAYIT` for a server-recognized registered user. In logs, `ATB_STORE_SESSION account_type_registered=true native_auth_required=false` on a mutation. Anonymous Guest must remain restricted.
3. Claim daily reward once: existing Gold should increase by **50** on same UID; second claim before 24 hours must not award Gold again. Confirm `users/<registeredUid>/metaEconomy` and RTDB immutable receipt, not only on-screen text.
4. Buy/equip an item the registered account can afford. Confirm Gold decrease and inventory entitlement are on the same UID. Guest should never inherit another account's purchases. Test missing Gold, duplicate purchase, restart and inventory recovery.
5. Rename within 24 hours: expect **deny** (`ATB743`) and no nickname change. If cooldown has expired, expect server-validated rename without UID/wallet changes. Password reset should send e-mail; use the new password without UID change.
6. **Sign out** using confirmation. Expected: new Firebase anonymous UID, new Guest display name, own wallet (zero by default), the old registered account untouched. Verify the new Guest may accumulate its own rewards only according to existing rules; *do not* attach those Guest balances to the old email account on later sign-in.
7. Sign in with the original registered email/password. Expected: old UID, old Gold/XP/history/purchases return, **no merging** with the newer Guest. Confirm Career, Settings, Store and online lobby all show the same owner. Repeat close/reopen. Steam Editor sign-in path is intentionally unchanged.

## Diagnostic tags
`ATB_SESSION_GATE`, `ATB_ACCOUNT_PROFILE_SYNC`, `ATB_STORE_SESSION`, `ATB_ACCOUNT_RENAME`, `ATB_ACCOUNT_SIGNOUT`, `ATB_PHASE19C_RTDB_OP`, `ATB_PHASE19E_RTDB_WALLET`.

Never send password or ID/refresh tokens. Provide log files if any gate fails. A `PASS` in source tests is NOT a Unity build, emulator, or Firebase production PASS.

## Known open follow-ups
`guestOrigin` archival RTDB node cannot be added without reviewing the existing security rule `$other` rejects. The original account's immutable UID remains the wallet authority. Server-forced app version remains **DEFERRED — OWNER DECISION**.
