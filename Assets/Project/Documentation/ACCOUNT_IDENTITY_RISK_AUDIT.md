# AtlasBoard v5.49.12bk — Account Identity Risk Assessment

Source: GitHub main `3e869ce...` (2026-11-02), owner-provided RTDB export (read-only), canonical roadmap `v5.49.12bj`.

## Confirmed
- Two distinct Steam accounts reportedly display one account's wallet/appearance (owner screenshots + report).
- Source fallback may retain Firebase account after failed automatic Steam returning sign-in.
- Email/password registration currently creates new Auth UID rather than linking an existing anonymous Auth UID.
- Current registration/accountType rules already distinguish guest from registered.
- Current AccountService.SignOut only signs out Firebase Auth. A full UI/cache invalidation isn't guaranteed by that method alone.
- Export aggregate: 10 user entries (8 registered, 2 guest); 2 wallet documents; no visible root SteamLink or guestOrigin entries in that particular RTDB export.

## Critical caveat
This is an audit, not an implemented fix. No actual Steam A/B logs or production identity-provider proof were supplied. Do not delete Firebase Auth users or reset a persisted anonymous Auth session. Do not modify production RTDB using this export.

## Proposed implementation
1. Server-verified Steam ticket/account link resolving to exact Firebase UID, fail closed on mismatch/unlinked identity.
2. Email registration via Firebase Auth `LinkWithCredentialAsync` for currently anonymous principal, preserve same UID and RTDB wallet/history in place, handle credential-already-in-use without auto-merge.
3. Restore anonymous guest on logout without leaking registered state; make UID-bound view model/reload atomic for new Auth identity.
4. Add immutable provenance `guestOrigin` under same canonical user, with explicit original UID/name and read-only historical snapshots; enforce anti-forgery and no double Gold/XP in Firebase Rules.
5. Auth+RTDB emulator matrix, two real Steam accounts, Android + Windows account switch and cross-device login.

## Next gate
Create safe backend/migration design and complete source implementation, then emulator/Unity QA. DO NOT consider this gate CLOSED from a source audit alone.
