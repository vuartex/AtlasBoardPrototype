# Phase 19BS — Account identity checkpoint

This package changes three runtime C# files, adds one meta file, the public error-code book, and two QA scripts. It DOES NOT alter RTDB security rules, production Firebase accounts, existing Unity menus, gameplay, or purchases.

## Live Guest account test (existing build and launcher)

1. Build with **Atlas Board → Online & Backend → Online → Build Two-Client Guest Test v1**.
2. Launch with **Launch Live Spark Guest QA (No Steam)**. Do not use the local-emulator launcher for a live registration test.
3. Record the exact Firebase Authentication UID and `/users/{uid}/metaEconomy/wallet` values *before* registration (do not share passwords or tokens).
4. Register with a previously unused email and a password meeting the project's policy.
5. Confirm exactly the **same** Auth UID remains; `/users/{uid}/general/accountType` is `registered`, and the existing wallet/career/inventory are unchanged. No new user profile should be created.
6. Close the EXE and reopen it with the same Live Spark launcher; the same registered identity, inventory and Gold should reappear.
7. Public errors show **only** a short text and an `ATB731–ATB738` code. Development logs can include redacted provider error categories.

**Do not delete the current guest account** if this fails. For `ATB735` verify the exact build contains the `accounts:signUp` link flow and confirm Firebase's Email/Password provider is enabled. Email-enumeration protection should remain enabled.

## Windows Unity Editor / Development Steam identity test

Development-only fallback applies when the trusted production Steam returning-sign-in callable cannot restore the SteamID. It binds a strong random QA credential to a Steam-specific UID, encrypted by the **current Windows user's DPAPI**. It has no cross-computer portability and is **NOT** secure retail Steam account verification.

1. On Steam account A (Vuartex), run Editor, note the current Firebase UID, Gold and purchases. Watch for `ATB_STEAM_LOCAL_SESSION bound=true` or `ATB_STEAM_IDENTITY ready=true` in Unity Console.
2. Quit Unity Play Mode and reopen the same Steam account. Verify **same UID** and unchanged wallet; no new `/users/{uid}` record.
3. Fully close game + Steam and switch to Steam account B. Open game: a separate UID and wallet should load.
4. Fully close game + Steam and switch back to A. Its original UID and purchases should reappear without transfer.
5. Repeat B → A. Verify no identity collision.

**Fail-closed rule:** Never remove DPAPI session files or Firebase users to "reset" a broken mapping; doing so can orphan the only valid access credential. Report `ATB_STEAM_LOCAL_SESSION` diagnostic category instead. A verified server Steam ticket → Firebase UID mapping remains the ONLY accepted retail account-recovery authority.

## Emulator / source QA

From `Backend/Firebase`:

```
node qa/phase19bs_identity_registration_source_gate.test.cjs
firebase.cmd emulators:exec --only auth,database --project demo-atlasboard "node qa/phase19bs_guest_upgrade_emulator.test.cjs"
```

The second command requires local Firebase CLI and currently deployed **local** rules. It is never intended for production or live accounts. Static QA does not prove Unity compilation or production network success.

## Release gate status

- Guest account linking: **SOURCE FIXED; LIVE QA PENDING**.
- Steam same-PC Editor/Development persistence: **SOURCE FIXED; TWO-STEAM-ACCOUNT QA PENDING**.
- Verified Steam returning sign-in for retail and cross-device: **OPEN**, requires trusted deployed backend.
- Firebase server-side version enforcement: **DEFERRED BY OWNER**, no change.
