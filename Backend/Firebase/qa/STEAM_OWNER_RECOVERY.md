# AtlasBoard | Owner-only Steam legacy UID recovery

**Scope:** Restore the verified Steam account to the **original Firebase Auth UID** with the existing wallet, inventory, Career history and cosmetics. No wallet copying. No user deletion. No source/user lookup by name.

## Evidence and prerequisites

- RTDB export contained two `Vuartex` display-name records with **different Firebase UID values**. Only one holds the historical purchase and wallet data. These are not safe ownership proofs.
- Unity runtime log on 2026-11-02 shows a Steam session followed by `automatic Steam returning sign-in did not restore an existing account`; then Firebase anonymous sign-in created a different UID, whose wallet reads zero. The source of truth for Steam ↔ UID mappings is **Firestore** (`platform_identity_links`, `account_platform_links`) rather than RTDB.
- `platformSteamReturningSignIn` must be deployed, have a valid `STEAM_PUBLISHER_WEB_API_KEY` secret, and verify the Steam Web API ticket for the configured `STEAM_APP_ID`. The development AppID 480 is not proof a future production AppID will work. If the callable is not deployed, this utility alone will not sign the user in.
- Have a current backup of both RTDB and Auth-user exports. Confirm the EXACT original Firebase Auth UID and SteamID64 independently (not via display name). Authenticate as the project owner with Firebase Admin SDK credentials. Do not post those credentials, tokens or private key files in chat or GitHub.

## Utility

`qa/phase19br_owner_steam_uid_recovery.cjs` uses the **existing Firestore linking schema**. It is not part of Unity or player builds. It performs **read-only checks by default** (without `--apply`): original Auth user exists and is not disabled, original RTDB user has its own `metaEconomy` record, and both Firestore mapping directions are unclaimed or already match the intended pair. It prints wallet totals for comparison but no password or token.

After inspecting the output and verifying Steam account ownership, the project owner may explicitly authorize a transactional mapping write using `--apply --owner-approval I_VERIFIED_STEAM_ID_AND_ORIGINAL_UID`. It refuses any SteamID already linked to another UID or UID already linked to a different SteamID. Each applied mapping is recorded in the `platform_link_events` audit log with verification mode `owner_manual_recovery_v1`. Manual owner attestation is distinct from a cryptographically verified Steam ticket; the returning sign-in callable still verifies each Steam ticket.

**This utility does not modify the wallet, inventory, purchases, profile, RTDB Rules or Auth UID.** It does not modify Firebase until explicitly run with Admin credentials and `--apply`. Do not use it if the SteamID has not been independently checked, if a mapping conflict appears, or if the Steam callable is not available.

## Acceptance after explicitly approved recovery

1. Auth user UID for the historical wallet remains unchanged; RTDB wallet and items are unchanged.
2. The same SteamID64 can authenticate through `platformSteamReturningSignIn` and the custom token yields **exactly** that historical UID.
3. Restart Unity/Windows build twice; the same UID, wallet, purchases and Career data appear both times.
4. Completely close AtlasBoard, switch Steam accounts and relaunch. Steam B **must never** display Steam A's wallet or cosmetics; it should show its own properly linked UID or a clear unlinked state.
5. Verify without clearing local identity caches and without deleting either Auth user; do not replay the migration if both mappings already agree.

**Not completed in this package:** Firebase production recovery; Steam ticket / custom token live integration; Unity/Guest build tests. Those require owner execution and actual authentication evidence.
