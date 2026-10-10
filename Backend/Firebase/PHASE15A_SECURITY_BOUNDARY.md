# Atlas Board — Phase 15A Security Boundary

## Objective

Phase 15A establishes a production-style Firestore client-access boundary before
App Check enforcement or deployed-production migration.

The rule is:

> If a collection is authoritative shared state or account value, Unity does not
> read/write it directly through Firestore.

Trusted mutations remain behind Firebase Cloud Functions / Admin SDK.

## Current direct Unity Firestore access

The current Unity source directly accesses only:

- `users/{uid}`
- `public_profiles/{uid}`
- `preferences/{uid}`

These paths are preserved intentionally because the existing account runtime
creates and maintains them directly.

Everything else becomes deny-by-default for Firebase client SDK access.

## Allowed boundary

### `users/{uid}`

Owner only.

Create:
- UID must match Firebase Auth UID.
- schemaVersion must be 1.
- accountStatus must start as `active`.
- membershipTier must start as `normal`.
- countryCode must be a two-character string.
- preferredLanguage must be one of EN/TR/ES/FR/DE/KO/RU.
- no extra fields.

Update:
- only `countryCode` and `preferredLanguage` may change.
- UID/schema/status/tier/createdAt stay immutable to the client.

Delete:
- denied. Account deletion should be an explicit backend workflow.

### `public_profiles/{uid}`

Authenticated reads.

Owner create/update only.

Allowed client-managed fields:
- displayName
- avatarId
- profileFrameId
- updatedAt

Note:
Profile cosmetic entitlement validation is still a Phase 15 follow-up. The
current client already owns these profile fields directly; Phase 15A does not
silently redesign that product contract.

### `preferences/{uid}`

Owner read/write only.

Allowed top-level fields:
- language
- audio
- graphics
- controls
- gameplay
- crossplayPreference
- updatedAt

These are user-owned preferences, not gameplay/economy authority.

## Denied by default

Examples now inaccessible through direct client Firestore:

- wallets / wallet ledger
- inventories
- commerce transactions/events
- promo codes/redemptions
- seasonal authoritative state
- progression authoritative state/history
- lobbies and lobby members
- matches, seats, network state and intents
- chat/moderation/report/sanction state
- Steam/provider identity mappings
- any future collection not explicitly reviewed

Cloud Functions use Firebase Admin SDK and bypass Firestore Security Rules, so
trusted backend authority continues to operate.

## App Check

App Check is intentionally NOT enforced in Phase 15A.

Current callable Functions still use `enforceAppCheck: false`, and the Unity
project does not yet have an Atlas production App Check provider/bootstrap
configured.

Turning enforcement on first would break valid clients.

Planned order:
1. Phase 15A — Firestore direct-access boundary.
2. Phase 15B — Unity App Check provider/bootstrap + emulator/development path.
3. Phase 15C — staged callable App Check enforcement + authorization/rate-limit
   review.
4. Continue economy/audit/retention/secret/monitoring hardening.

## Known legacy diagnostic impact

`AtlasBoardFirebaseAuthFirestoreTest.cs` is an early development diagnostic that
writes an old unrestricted `users/{uid}` shape and then deletes the document.

That diagnostic is no longer compatible with the production-style Phase 15A
rules by design.

The current `AtlasBoardAccountService` registration/profile/preferences flow is
the supported client contract.

Do not weaken production Security Rules to preserve an obsolete unrestricted
diagnostic.

## Validation

Start Auth + Firestore + Functions emulators with the new rules loaded.

From `Backend/Firebase/functions`:

```text
npm run serve:economy
```

In a second terminal:

```text
npm run test:security-rules
```

Expected:

```text
Phase 15A result: 17 passed, 0 failed.
PASS: direct Firestore access is deny-by-default...
```

Then run one Unity account smoke:
- register/sign in
- load account
- update locale
- update public profile
- save/load cloud preferences

No lobby/match/economy code is changed by Phase 15A.
