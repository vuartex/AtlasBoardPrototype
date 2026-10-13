# AtlasBoard Security Core 06 — platform-isolated preferences v2

**Package:** v5.21.0 · **Status:** candidate; emulator + Unity compilation not yet accepted.

## Why this architecture

Release order is Android before Steam. An account can later run on Android, iOS, Windows, macOS and Linux, so camera/display settings must be independent per platform. Firestore Security Rules have a **1,000 expression evaluation limit per request**. The former monolithic `preferences/{uid}` Settings map exhausted that limit even for rejected multi-platform updates. Retrying the same nested `Map.diff()` validator is NOT an appropriate permanent architecture.

## Document contract

| Path | Ownership | Stored data | Allowed writes |
| --- | --- | --- | --- |
| `preferences/{uid}` | only `uid` | Original registration fields; `language`, `audio`, `gameplay.global`, `crossplayPreference`, `updatedAt` | Canonical empty registration create. Later shared-field updates only. Legacy graphics/gameplay platform maps (if present) are immutable. |
| `preferences/{uid}/platforms/android` | only `uid` | `schemaVersion:1`, `graphics`, `gameplay` camera controls, `updatedAt` | Current Android client only by convention (Firestore Auth is UID-level, cannot prove OS), whitelist/ranges/boolean types. |
| `preferences/{uid}/platforms/ios` | only `uid` | same platform schema | same |
| `preferences/{uid}/platforms/windows` | only `uid` | same platform schema | same |
| `preferences/{uid}/platforms/macos` | only `uid` | same platform schema | same |
| `preferences/{uid}/platforms/linux` | only `uid` | same platform schema | same |

`platforms` document LIST and cross-user GET/write are denied. The platform identifier is an explicit allowlist; unsupported names fail closed. Do not confuse the platform path with device attestation: a signed-in owner may update their own other allowed platform documents, but cannot read or write another account's documents. App Check and Steam publisher attestation are separately staged.

## Unity changes

`AtlasBoardCloudPreferencesSync.cs` is a full canonical replacement that keeps the original component class/name, input event hooks, localization, audio, Settings v2 runtime application and platform detection. It reads only the current platform's subdocument. On sign-in:

1. Read the canonical account snapshot through unchanged `AtlasBoardAccountService`.
2. Read `preferences/{uid}/platforms/{currentPlatform}` directly.
3. Apply shared `language`, `audio`, `gameplay.global` from root.
4. Prefer v2 `graphics` and `gameplay` in the platform document. If absent, read the *current platform's* legacy nested maps from the root. If neither exists, use this device's local PlayerPrefs.
5. If shared/platform data is absent, seed v2 via one Firestore WriteBatch: root `SetOptions.MergeAll` for shared-only values, plus `Set` (`SetOptions.Overwrite`) of the current platform doc. This avoids all-platform root rewrites and preserves legacy maps. Normal subsequent uploads use the same two-document atomic batch.
6. Never change or delete another platform document. Fail closed for unknown platforms; retry failed uploads without losing local PlayerPrefs.

`AtlasBoardAccountService.cs` remains unchanged to preserve account registration and public APIs. Its old `SaveCloudPreferencesAsync` method is NOT used by the new sync component; other callers must not reintroduce it to upload nested platform settings. Existing registrations still create canonical empty root documents.

## Next customizations / migration

New platform settings can be isolated in dedicated schema-versioned module documents (e.g. `platforms/android/features/touchControls`) **after** matching Rules, Unity code and migration tests are added. Current broad/nested feature writes are denied by default. A new OS or Steam Deck-specific variant requires explicit platform key, Rules allowlist update and schema acceptance rather than implicitly treating arbitrary IDs as trusted.

Legacy root graphics/platform gameplay fields are intentionally **not deleted** during migration. They are fallback input only. After separate migration-coverage evidence and real-device acceptance, an explicit server-side migration may remove historical fields; clients are not authorized to delete them. No live migration or production deployment has occurred.

## Acceptance and limitations

- `node scripts/qa.mjs --full` must show `ATLASBOARD_SECURITY_CORE_06_SETTINGS_SCHEMA_PASS: 28/28`, accepted Core01–05 regressions, original Rules 17/17, App Check local checks, and terminal `AtlasBoard QA FULL LOCAL PASS (emulators only)`.
- Unity Editor must actually compile the replacement C# component, and an Android test client should sign in, save/restart, and read its `platforms/android` doc. Windows/iOS updates must not alter Android data. Static Unity metadata checks are **not** Unity compilation.
- The emulator does not validate real Steamworks attestation or App Check cryptographic proofs. Independent gameplay-result verification remains open.
- This package is a local project candidate only: **no GitHub commit/push, Firebase deployment, or production account migration**.
