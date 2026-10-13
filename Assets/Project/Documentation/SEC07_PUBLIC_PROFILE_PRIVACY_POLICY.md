# AtlasBoard Security Core 07 — direct profile access and enumeration privacy

## Product decision (Android-first; all platforms)

**Authenticated targeted public-profile GET is allowed; direct client collection LIST and runQuery are denied.** The profile document exists to display the name, avatar and frame for another known player, e.g. an actual lobby member, friend invite or match participant. It is **not** a global account directory or a searchable people index. This rule is platform-neutral and applies equally to Android, Steam/Windows, iOS, macOS and Linux.

Firestore path: `public_profiles/{uid}`.

| Operation | Client policy | Why |
|---|---|---|
| `get` single known UID | Auth required | Unity account service uses `Document(uid).GetSnapshotAsync()` |
| `list` entire collection, pageSize=1/50/500 | Denied even to owner | No unbounded account-ID collection scan |
| `runQuery` limit/filter over collection | Denied | Small or selective queries must not bypass the directory rule |
| `create`/`update` | Owner only, exact current 4-field schema | Prevent private fields or other users' changes |
| `delete` | Denied | Avoid client-side canonical profile deletion |
| Server Admin SDK | Existing server authority unchanged | Trusted lobby, moderation and progression read known profiles server-side |

The canonical public projection fields remain `displayName` (2–32), `avatarId` (<=128), `profileFrameId` (<=128), `updatedAt` (timestamp). The `users/{uid}` record is private. Authentication alone does NOT grant permission to search all profiles.

## Compatibility audit

- Source: `Assets/Project/Scripts/Online/Firebase/AtlasBoardAccountService.cs`: `LoadCurrentAccountAsync` reads current UID from `public_profiles` with `Document(uid).GetSnapshotAsync()`. Creating/updating a profile also uses the exact UID. No Unity user-facing feature in reviewed source requires client collection listing.
- Cloud Functions use privileged Admin SDK and are not subject to client Firestore Rules; callable authorization, privacy and output minimization must continue to be audited independently.
- If a future friend search, ranking, player directory, social follower system or global leaderboard needs discovery, **do not re-enable blanket Firestore LIST**. Build a dedicated indexed server-owned search projection with user visibility opt-in, bounded page limits, authenticated callable quota/cost controls, blocked-user filtering, and independent abuse tests. The exact public data policy must be reviewed with the feature.
- Explicitly changing SEC05's old `[10/20]` assertion from “authenticated LIST allowed” to “authenticated LIST denied” is an approved contract migration under SEC07. All other SEC05 checks remain present.
- Existing `public_profiles` documents are not changed or deleted by the Rules update. There is no Firestore data migration or new Unity dependency.

## Safety and validation

- `security-core07-public-profile-enumeration-e2e.mjs` runs FIRST in `node scripts/qa.mjs --full`, before previously accepted SEC06 and other suites, and fails closed if unexpected permission, data mutation or Firestore expression exhaustion occurs.
- Local Auth/Firestore/Functions target and emulator hub are checked before authenticated fixture creation; server Admin SDK is loaded only for localhost cleanup.
- SEC07 E2E: 23 counted checks across target-GET, LIST sizes, `runQuery` bypass attempts, owner/peer authority, schema allowlisting and post-denial integrity.
- SEC05 E2E: original 20 checks, with only the LIST expectation changed to reflect policy.
- SEC06 E2E: original 28 Android-first checks remain unchanged and continue as second integration script.
- Full local QA still includes the old Firestore security rules, Steam emulator, App Check missing-header and all earlier E2E regressions.
- No production deployment or GitHub push is included. Passing emulator tests does NOT establish that Unity Editor compiles, Android works end to end or a production App Check verification is secure.

## Known limitations and release gates

- UID-targeted GET may still be abused by an authenticated client that already knows UIDs. Firestore Rules cannot reliably rate-limit such GET requests per minute without trusted server-side state; add server-side per-account quotas when developing player search and assess scraping risk before release.
- This policy does not cover a future intentionally public leaderboard, profile search, friend system, or moderation-only directory until their own data contracts exist.
- Security Core 07 is a **candidate** until Windows owner's fresh Firebase Emulator `ATLASBOARD_SECURITY_CORE_07_PUBLIC_PROFILE_PASS: 23/23` AND terminal `AtlasBoard QA FULL LOCAL PASS (emulators only)`. Core 01–06 remain locally accepted based on `Pasted text(20261013-032433).txt`, not production approved.
- Production gate remains blocked pending independent real-match verifier, valid Steam AppID and publisher proof, live App Check malformed-token denial, TTL/quota/cost monitoring, Unity Editor C# compile and Android/physical-multiplayer acceptance.
