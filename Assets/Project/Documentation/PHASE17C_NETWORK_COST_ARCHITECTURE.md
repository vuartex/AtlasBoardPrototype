# AtlasBoard — Phase 17C Network Cost Architecture

Version: v5.26.1 candidate
Date: 2026-10-14

## 1. Goal

AtlasBoard must not spend managed-backend quota on work that can safely remain
on the host device. The networking model is therefore split into three explicit
session classes. This classification is a product/security contract, not merely
an optimization hint.

## 2. Session classes

### 2.1 Local Offline

Examples:
- one human + bots;
- multiple local humans on the same device + bots;
- PRIVATE TABLE opened without an authenticated online identity.

Rules:
- internet is not required;
- live transport is local memory only;
- no Cloud Functions gameplay calls;
- no Firebase Realtime Database gameplay traffic;
- no Firestore gameplay traffic;
- bots and all local-human turns execute on the local host;
- no anti-cheat verification is required because no competitive/persistent
  reward is created;
- no permanent XP, wins, achievements, wallet currency, competitive rating or
  server match history is minted;
- local-only statistics may be displayed/stored locally but must never be
  mistaken for authenticated career progression.

### 2.2 Private Casual Online

Examples:
- invite/code room containing at least one remote human;
- room may also contain local humans and bots.

Target transport:
- Firebase Realtime Database persistent connection for transient room state,
  presence, small player intents and host state revisions;
- host-authoritative simulation for bots and normal board resolution;
- no periodic Cloud Functions match polling.

Rules:
- no permanent XP, wins, achievements, premium rewards or competitive rating;
- expensive server-side anti-cheat replay is not required for the casual room;
- RTDB security rules still must enforce authenticated room membership and
  prevent one guest from writing another guest's intent path;
- host owns authoritative casual match state;
- disconnect/presence should use RTDB connection/onDisconnect semantics rather
  than repeated 0.25 s / 0.5 s / 3 s HTTP polling;
- match history is local by default. Optional minimal cloud history may be
  added later as one small end-of-match write, not a continuous stream.

### 2.3 Online Rewarded

Examples:
- public/normal PLAY room with at least two distinct authenticated human
  accounts and persistent progression enabled.

Target transport:
- Firebase Realtime Database for live, transient state/presence/intents;
- host simulates bots locally and publishes only meaningful state revisions;
- Cloud Functions are reserved for trusted boundaries instead of live polling.

Cloud Functions remain appropriate for:
- secure room/match admission or protected ticket creation where required;
- authoritative economy mutations;
- promo redemption and reward claims;
- purchase/refund validation;
- moderation/report actions that require trusted server authority;
- server-owned/randomness commitments and future authoritative event proof;
- bounded checkpoint/final match verification;
- permanent progression finalization after independent verification.

Persistent XP/wins/achievements are never eligible unless:
- session mode is Online Rewarded;
- at least two distinct authenticated human accounts participated; and
- the independent server-authoritative verification gate accepts the result.

A host-supplied internally consistent result is still not independent proof.

## 3. Storage split

### Keep in Firestore / trusted Functions

Persisted/valuable security-sensitive records:
- accounts and provider mappings;
- platform-specific cloud preferences when cloud sync is desired;
- wallet and wallet ledger;
- inventories and entitlements;
- purchase/refund transaction records;
- promo redemption records;
- authenticated progression, achievements and competitive history;
- protected match-verification receipts;
- moderation/report audit records.

### Move live/transient traffic toward Realtime Database

- room presence;
- join/leave connection state;
- ready state where casual semantics allow it;
- live match state revision;
- remote-human intents;
- host state broadcasts;
- reconnect lease/presence signals;
- ephemeral room/chat data where moderation requirements permit.

### Static catalogs

Do not repeatedly fetch static store/item/map/promo display data on every menu
open. Prefer:
1. client-bundled versioned catalog where possible;
2. local cache with a small version check;
3. a compact read-only remote catalog only when live content is required.

Purchase price, ownership, promo eligibility and reward issuance remain trusted
server decisions even if their display metadata is cached locally or delivered
from RTDB.

## 4. Traffic reduction requirements

The legacy match path currently contains aggressive polling such as:
- match snapshot request every 0.5 s per client;
- host pending-intent request every 0.25 s;
- match presence heartbeat every 3 s;
- host disconnect/reconnect maintenance around 5 s;
- lobby snapshot request every 1 s;
- lobby host heartbeat every 5 s.

Phase 17C must remove these as the normal live-transport mechanism.

Target behavior:
- publish only when state revision changes;
- send a remote-human intent once and acknowledge it once;
- batch one turn's compatible state changes into one revision/event bundle;
- combine rapid lobby settings edits before network write;
- do not transmit bot think/movement micro-steps;
- host resolves bots locally and sends only the resulting authoritative event;
- use connection-state events instead of heartbeat polling where RTDB supports
  the requirement;
- cache immutable or slow-changing documents instead of rereading them every
  snapshot request.

## 5. Latency rule

Cost reduction must not be achieved by making remote-human input feel slow.
RTDB persistent transport is intended to replace HTTP polling, not merely lower
poll frequency. Remote-human intents should be small, event-driven messages.
Bots remain host-local. Critical server verification belongs at bounded trust
boundaries, not on every animation frame.

## 6. Current implementation status

Implemented in v5.26.0 candidate:
- canonical session-mode policy types;
- local/offline startup fallback is non-blocking when Firebase identity is not
  ready;
- PRIVATE TABLE without an online identity routes to the existing local lobby;
- offline/local matches are explicitly classified as non-rewarded;
- Android display guard no longer calls Screen.SetResolution as a repair;
- Android Settings resolution model is forced to the active landscape viewport;
- mobile canvas reference height reduced from 1248 to 1160 for less unused
  margin on the validated 2400x1080 Samsung device.

Not yet implemented / do not claim complete:
- Firebase Realtime Database instance/rules deployment;
- RTDB Unity transport;
- migration of current lobby/match polling to RTDB;
- rewarded-online batched event journal;
- independent server-authoritative gameplay replay/result verifier;
- measured post-migration quota/latency results.

## 7. Acceptance plan

1. Build and run Android v5.26.0.
2. Verify Settings -> Mute All -> Apply causes no portrait-resolution flash.
3. Verify PRIVATE TABLE works with no online identity and can start local/bot
   gameplay without Firebase emulator or Cloud Functions.
4. Confirm no permanent progression/reward write is attempted for that session.
5. Provision RTDB in atlasboard-usa on the free Spark plan.
6. Add fail-closed RTDB rules and authenticated private-room transport.
7. Run 2-device Private Casual Online test.
8. Replace legacy polling incrementally while retaining fallback until parity.
9. Measure requests, bandwidth, Firestore reads/writes and latency.
10. Only then design Online Rewarded checkpoint/final-verification traffic.

## 7. v5.26.1 RTDB schema and security contract

The owner created the AtlasBoard Realtime Database in the Belgium/Europe region.
It must begin in Locked Mode. `Backend/Firebase/database.rules.json` is the
canonical rules source. `firebase.json` now points to that file.

Planned RTDB paths:
- `/joinCodes/{code}`: short-lived mapping to a high-entropy room id. Authenticated
  reads only while unexpired; only the owning host may create/update/delete it.
- `/roomMeta/{roomId}`: Private Casual Online room metadata. Client-created RTDB
  rooms are explicitly restricted to `privateCasualOnline`; they cannot declare
  themselves rewarded/ranked.
- `/joinRequests/{roomId}/{uid}`: authenticated guest requests admission.
- `/roomMembers/{roomId}/{uid}`: host-controlled membership; a non-host can only
  be added after a join request exists.
- `/presence/{roomId}/{uid}`: each accepted member writes only its own presence.
- `/intents/{roomId}/{uid}/{intentId}`: each accepted remote human creates only
  its own bounded intent; the host reads/processes and may delete it.
- `/intentAcks/{roomId}/{uid}/{intentId}`: host acknowledgement for that remote
  human.
- `/roomState/{roomId}`: host-only monotonic revision/event state, read by active
  room members.
- `/publicRoomDirectory`: authenticated read-only client directory reserved for
  trusted/admin publishing; indexed on `joinable` and `updatedAt`.
- `/catalogs`: authenticated read-only store/event/public-promotion DISPLAY
  metadata. Client writes are denied. Secret redeem codes and reward issuance do
  not belong here.

The rules are deny-by-default at the database root. Rewarded/ranked room creation
and secure economy/progression mutation are intentionally NOT granted to RTDB
clients. Firebase Admin/trusted services bypass client rules when those services
are implemented.

## 8. Index policy

RTDB direct-key paths (`roomMeta`, `roomMembers`, `presence`, `intents`,
`roomState`, `joinCodes`) do not need query indexes for the planned access pattern.
Query-oriented paths declare `.indexOn` now:
- `publicRoomDirectory`: `joinable`, `updatedAt`;
- `catalogs/storeItems`: `active`, `sortOrder`;
- `catalogs/events`: `active`, `startsAt`, `endsAt`;
- `catalogs/publicPromotions`: `active`, `startsAt`, `endsAt`.

Firestore composite indexes remain empty until an actual Firestore query requires
one. Adding indexes does not reduce callable invocation counts; the large savings
come from eliminating polling and rereads.

## 9. Capacity model (targets, not measured acceptance)

Legacy four-player, 20-round, ~30-minute polling estimate:
- ~32,280 Cloud Function/callable requests per match;
- ~158,400 Firestore reads per match from snapshot/pending-intent polling and
  associated document reads.

Phase 17C targets:
- Local Offline: 0 gameplay Functions, 0 gameplay Firestore, 0 gameplay RTDB.
  Backend gameplay traffic reduction: effectively 100%.
- Private Casual Online: 0 normal gameplay Functions and 0 live Firestore polling.
  RTDB carries only event-driven membership/presence/intents/state. Design target
  is ~1–5 MB downloaded per four-player 20-round match after batching. At the
  Spark 10 GB/month download ceiling this is approximately 2,000–10,000 matches
  per month before other app RTDB traffic. Spark allows 100 simultaneous RTDB
  connections, so approximately 25 four-player or 50 two-player matches can be
  connected at once.
- Online Rewarded: RTDB remains the live transport, while trusted validation is
  bounded to meaningful security boundaries. A 200–500 trusted-call target is
  98.5–99.4% fewer calls than ~32,280. A 100–300 secure-Firestore-read target is
  over 99.8% fewer reads than ~158,400. These are engineering targets and must
  be replaced by measured counters after implementation.

Important: Spark RTDB can support Casual Online without Cloud Functions, but
secure permanent XP/wallet/purchase issuance cannot be delegated to the host or
RTDB rules. Until a trusted backend is available, Online Rewarded must remain
non-rewarded/disabled rather than minting valuable state from an untrusted host.

## 10. v5.26.1 compile repair

The first v5.26.0 Android build failed because `AtlasBoardOfflineModeBootstrap`
subscribed to `PublicCodeRaised` with `HandlePublicCode`, but the implemented
callback is `HandlePublicCodeRaised`. All subscribe/unsubscribe references now use
`HandlePublicCodeRaised`. No gameplay/security policy was relaxed by this repair.

