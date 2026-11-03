# AtlasBoard v5.49.12bu — Identity/session owner acceptance

**Status:** source contract PASS, Unity compilation/live user tests NOT RUN. No production backend changes in this ZIP. Apply as full project-relative files after v5.49.12bs + bt.

## Steam / Unity Editor — account continuity

1. Close Editor Play mode and any open Guest EXEs. Run as the same Steam account used for the saved purchases.
2. In the Editor, verify `ATB_STEAM_IDENTITY ready=true` and record only the masked UID prefix. Confirm wallet/owned items from RTDB.
3. Activate the Development wallet, purchase and equip a cosmetic, then open/change the profile and visit Career and online lobby.
4. After *each* action, inspect the `ATB_PHASE19E_RTDB_WALLET`, `ATB_RTDB_USER_SYNC`, and `ATB_RTDB_CASUAL_AUTH` logs. The UID MUST remain identical. The `ATB_STEAM_IDENTITY_FENCE` guard should restore the original UID when another subsystem changes FirebaseAuth. A transient new Authentication-only anonymous UID is still possible from legacy `SignInAnonymouslyAsync` until that call is eliminated at source; **do not mark this gate CLOSED unless the user count and wallet UID remain stable**.
5. Close and reopen Editor Play mode. Verify the original purchased items, career, coins and Gold. For a separate Steam account, close the game, change Steam account, then relaunch; ownership must not bleed between accounts.

## Live Spark Guest QA — register existing Guest

Use the established **Build Two-Client Guest Test v1** -> **Launch Live Spark Guest QA (No Steam)**. Do not create another QA build menu.

1. Start the Guest and record the exact Firebase UID and Gold balance.
2. In Career account registration choose a non-guest nickname (for example `Deneme1`), valid email and strong password.
3. Press **HESAP OLUŞTUR** once. Success must close the register dialog automatically and display the new nickname. UID and wallet MUST remain unchanged. No new Firebase Auth UID should be created.
4. In Firebase Authentication verify Email/Password on the same UID. In RTDB verify `/users/{uid}/general/accountType` is `registered`, and wallet/career/ownership is unchanged.
5. Reopen and verify name and Career, then restart and sign in with the same email. Errors should appear as a dialog with an ATB support code.
6. Existing RTDB `displayName` change cooldown (24h) remains in force; Firebase Auth displayName now holds the registered nickname. If the old RTDB displayName persists, treat cross-device name consistency as OPEN, not PASS.

## Release boundaries

- No direct RTDB wallet/XP modifications, account deletion, cross-UID merging, or third-party account claims.
- Windows Editor/Development Steam DPAPI recovery is **not a production Steam ownership proof**. Retail release requires a functioning server-verified SteamID ↔ Firebase UID path.
- Firebase *server-side version enforcement* is **DEFERRED — OWNER DECISION** and untouched.
- Production Firebase deploy and GitHub push were **NOT PERFORMED**.
- Source QA is not proof of Unity compile, emulator pass, or live acceptance.
