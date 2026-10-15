# AtlasBoard Phase 17E — Runtime Integrity / Account Access / Android Performance v5.28.0

Date: 2026-10-14
Base: owner-tested v5.27.0 on top of GitHub `f170229ca4e20c255d605a35249939f516a863b3`

## Purpose

This checkpoint closes Android device regressions revealed by repeated local
match entry/exit and establishes an account UX without converting offline play
into an online requirement.

The product contract is:

- Local Offline stays playable without authentication or internet.
- PRIVATE TABLE uses one canonical modern seat model.
- selected open seats become Bots at Start; they never become implicit Humans;
- local Start shows a visible five-second countdown;
- scene reload/re-entry must recreate modern Phase14 presentation components;
- Android gets a runtime-only performance ceiling; desktop/Steam settings stay
  untouched;
- Career is cloud/account-backed and gates unsigned users into account access;
- Settings exposes an account access button;
- Firebase Auth owns credentials; AtlasBoard never stores passwords in RTDB,
  Firestore, PlayerPrefs or logs.

## Android evidence that triggered this checkpoint

On samsung SM-G780G / Android 13 / 2400x1080, v5.27 measured approximately
16–23 FPS for large portions of gameplay despite targetFps=60 and despite the
adaptive 4x -> 2x MSAA reduction. Therefore the old AA-only fallback was not a
credible performance fix.

Repeated `Board_Prototype` reloads also reproduced legacy presentation drift:
the scene reloaded, the persistent mobile helper survived, but some modern
scene-local presentation objects were no longer available when the next match
opened.

## Local/private start invariant

`AtlasBoardOfflineModeBootstrap` owns unsigned LocalOffline Start only.

Before launch:

1. read the modern `hostSeatModes` array;
2. preserve explicit LocalHuman selections;
3. preserve explicit Bot selections;
4. for every selected active seat still `OpenOnline`, convert it to Bot;
5. mirror that finalized canonical state to the legacy MatchSetup controls once;
6. show 5..1 countdown;
7. launch through the existing `StartMatchAfterPrivateBackendAuthorization`
   bridge.

The old Start listener is replaced only on that LocalOffline scene instance.
A new scene reload restores serialized online behavior, so Windows/Steam and
backend-authorized lobby Start are not globally rewritten.

## Scene-reload presentation invariant

On every active-scene handle change, the mobile runtime clears every cached
scene reference and verifies these scene-local Phase14 components exist:

- Purchase presentation polish
- Auction presentation polish
- Trade presentation polish
- Development presentation polish
- Final match result layout
- City information panel
- City-info camera input guard
- Tablet/world interaction guard

A missing component is recreated once and logged as
`ATB_PRESENTATION_SELF_HEAL`. Existing instances are never duplicated.

## Android mobile render ceiling

This profile is runtime-only and is never persisted to desktop/mobile cloud
preferences:

Initial mobile ceiling:
- VSync = 0
- target FPS = requested setting, capped to 30..60 (default 60)
- pixel lights <= 1
- MSAA <= 2x
- hard shadows only, Low shadow resolution, shadow distance <= 18
- URP renderScale target 0.82 when that API is available
- camera HDR off
- URP camera post processing off when discoverable by reflection

If two consecutive 10-second windows remain below 45 FPS:
- renderScale -> 0.70
- MSAA -> off
- real-time shadows -> off

No `Screen.SetResolution` call is introduced. Device resolution/orientation
remains OS-owned.

Development builds have additional diagnostics overhead, so Release performance
must still be measured separately. v5.28 is not accepted as a 60-FPS release
until device logs demonstrate the result.

## Mobile UX changes

- accepted Roll button location is unchanged;
- Trade button moves farther left so it no longer overlaps P2 HUD;
- tablet root mobile scale increases to 1.25;
- Trade text and fixed touch controls receive larger minimums;
- city panel increases to 600x720 reference units with touch scrolling;
- Career uses an indigo accent on mobile while Shop remains orange.

## Guest identity

Unsigned players receive one persistent device-local name:

`Guest` + five digits + one uppercase ASCII letter

Example: `Guest48273K`.

The value is deliberately NOT localized; the same `Guest` prefix is used in all
seven languages so identity storage/presentation does not change with UI
language.

This identifier is probabilistically unique on one device and across ordinary
users, but it is not a server-certified globally unique account identifier.
Global uniqueness belongs to authenticated UID/account identity.

Guest identity grants no protected progression.

## Account access

A runtime account surface now provides:

- email/password sign in;
- create-account form;
- display-name choice during registration;
- password confirmation;
- easy local addition/subtraction math check;
- loading state and one-request-at-a-time lock;
- password reset email;
- persistent Firebase Auth session across launches;
- account sign out;
- display-name change UI;
- local 24-hour rename cooldown feedback;
- canonical signed-in display-name propagation to menu/lobby/local P1
  presentation.

The math question is UX friction only. It is NOT a security-grade CAPTCHA and
must not be documented as bot protection. App Check/rate limiting remain the
proper production abuse controls.

## Credential/storage boundary

Passwords and repeated passwords never leave Firebase Authentication APIs.
They are not written to RTDB, Firestore, PlayerPrefs, analytics or diagnostics.

Persistent account/profile identity remains Auth + Firestore. Moving credentials
or valuable identity authority to RTDB would weaken the architecture and does
not meaningfully reduce match quota.

RTDB remains reserved for the Phase17C transient transport plan:
room membership/presence, intents, acknowledgements, live room state and safe
read-only display catalogs.

## Display-name throttling

`firestore.rules` now requires `public_profiles.updatedAt` to be a near-current server-time value (not older than one minute) and,
when `displayName` changes, requires the prior profile update timestamp to be at
least 24 hours old.

The current v1 profile schema has one `updatedAt` timestamp, so an avatar/frame
update conservatively restarts the rename cooldown. This is stricter than the
minimum product requirement but fail-closed; it prevents a client from bypassing
the daily rename limit without requiring a paid trusted Function.

Rules must be deployed separately by the owner before server-side cooldown is
claimed live.

## Still open / do not claim complete

- v5.28 real Unity Android compile and physical-device acceptance;
- measured post-profile Android FPS in Development and Release builds;
- Firebase App Check production provider warning on device;
- RTDB Unity transport and two-device Private Casual Online;
- removal of legacy rewarded-path polling;
- independent authoritative match replay/result verification;
- Google Play AAB/release pipeline;
- Steam production AppID and Steam release validation.

## Career authority clarification

This checkpoint fixes the unsigned Career dead-end by routing unsigned Career
entry into account access. It does **not** fabricate XP/career data locally.
The currently protected progression collections remain server-owned. If the
Firebase project still has no deployed trusted progression backend, a signed-in
Career can identify the account but authoritative XP/wins/history remain an open
backend gate. Local/Private matches deliberately do not mint those values.
