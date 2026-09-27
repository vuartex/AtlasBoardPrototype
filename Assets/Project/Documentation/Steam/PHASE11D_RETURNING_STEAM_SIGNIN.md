# Phase 11D — Returning Steam User Sign-In

## Goal

A previously linked Steam user must recover the same canonical Atlas
`AccountId` instead of creating a new Atlas account.

Production flow:

1. Steam starts and provides `GetAuthTicketForWebApi`.
2. AtlasBoard sends the ticket to `platformSteamReturningSignIn`.
3. Cloud Functions verifies the Steam ticket with Valve.
4. The verified SteamID resolves through the existing Phase 11C mapping.
5. Both mapping directions must agree and the link must be `verified=true`.
6. Backend issues a short-lived Firebase custom token for the canonical Atlas
   account.
7. Unity calls `FirebaseAuth.SignInWithCustomTokenAsync`.
8. Firebase Auth `CurrentUser.UserId` must equal the linked Atlas AccountId.
9. Existing wallet, inventory, progression, achievements and profile remain
   attached to that same UID.

## Development proof

AppID 480 cannot prove AtlasBoard publisher verification. For emulator testing:

`platformSteamDevReturningSignIn`

is available only when Functions, Firestore and Auth emulators are active.

It:
- accepts only a Steam64 id,
- resolves an already-existing Phase 11C emulator link,
- never creates or changes a mapping,
- returns an emulator-only custom token,
- cannot run in production.

Firebase Authentication Emulator intentionally does not validate custom-token
signatures, so the development token is handcrafted only for emulator use.

## Test surface

Phase 11D is tested in **Unity Editor Play Mode**, not the standalone Guest
Client. Steam overlay behavior was already accepted in Phase 11B.

Editor test:
1. Steam provider ON, AppID 480.
2. Firebase emulators running.
3. Open `Atlas Board > Platform > Steam Account Link Diagnostics`.
4. If needed, create the Phase 11C DEV LINK first.
5. Press `SIGN OUT FIREBASE AUTH (TEST ONLY)`.
6. Press `DEV RETURNING STEAM SIGN-IN (EMULATOR)`.
7. `Firebase Auth UID` and `Recovered Atlas AccountId` must match.
8. Sign out again and repeat. The same AccountId must be recovered.

Real production verification remains deferred until the real AtlasBoard Steam
AppID and Steam publisher secret are available.
