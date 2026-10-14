# AtlasBoard Phase 17C — RTDB Rules / Free-Tier Transport v5.26.1

Date: 2026-10-14
Firebase project: `atlasboard-usa`
RTDB location selected by owner: Belgium / Europe

## Purpose

This checkpoint prepares Firebase Realtime Database for low-cost, event-driven
AtlasBoard traffic without pretending that RTDB live multiplayer is already
implemented.

Create the database in **Locked Mode**. Do not use Test Mode.

Canonical rules file:

`Backend/Firebase/database.rules.json`

Firebase CLI configuration:

`Backend/Firebase/firebase.json`

After the Realtime Database instance exists, deploy only its rules with:

```powershell
cd C:\Users\crazy\AtlasBoardPrototype\Backend\Firebase
firebase.cmd deploy --only database --project atlasboard-usa
```

This does not deploy Cloud Functions and does not modify Firestore rules.

## RTDB ownership model

### Private Casual Online

The authenticated host owns the casual match simulation. Remote humans send
small intents. Bots and local humans execute on the host device. No permanent
XP, wallet currency, ranked wins or protected achievements are granted.

Paths:
- `joinCodes/{code}` — short-lived mapping to high-entropy room id.
- `roomMeta/{roomId}` — host-owned casual-room metadata.
- `joinRequests/{roomId}/{uid}` — guest asks to join.
- `roomMembers/{roomId}/{uid}` — host accepts/removes remote members.
- `presence/{roomId}/{uid}` — member-owned online state.
- `intents/{roomId}/{uid}/{intentId}` — remote human sends one bounded intent.
- `intentAcks/{roomId}/{uid}/{intentId}` — host acknowledges processing.
- `roomState/{roomId}` — host publishes monotonic state revisions.

The database is deny-by-default. Client-created room metadata is restricted to
`privateCasualOnline`; clients cannot mark their own session rewarded/ranked.

### Public / Online Rewarded

`publicRoomDirectory` is readable to authenticated clients but client writes are
denied. A future trusted backend/Admin path may publish it. Rewarded permanent
state must not be created by an untrusted host.

### Catalogs

Read-only display metadata may live under `catalogs`:
- store item display metadata;
- event display metadata;
- public promotion/banner metadata.

Secret redeem codes, promo redemption state, purchase validation, wallet,
entitlements, XP, wins and achievements remain trusted operations. Do not expose
secret promo-code records in a client-readable RTDB catalog.

## Indexes

RTDB uses direct-key access for room state, members, intents and presence, so no
query index is required for those paths.

The rules define indexes for the paths expected to use queries:
- `publicRoomDirectory`: `joinable`, `updatedAt`;
- `catalogs/storeItems`: `active`, `sortOrder`;
- `catalogs/events`: `active`, `startsAt`, `endsAt`;
- `catalogs/publicPromotions`: `active`, `startsAt`, `endsAt`.

Firestore composite indexes remain empty until a real Firestore query requires
one. Indexes do not solve the legacy polling cost problem.

## Capacity targets

Current legacy four-player / 20-round / ~30-minute polling estimate:
- about 32,280 callable requests per match;
- about 158,400 Firestore document reads per match.

Target architecture:
- Local Offline: 100% gameplay backend traffic removed.
- Private Casual Online: normal gameplay Functions = 0, live Firestore polling =
  0, RTDB event traffic only.
- Online Rewarded: target 200–500 trusted calls per match, approximately
  98.5–99.4% fewer than legacy polling.

Spark RTDB official limits relevant to AtlasBoard:
- 100 simultaneous connections;
- 1 GB stored data;
- 10 GB/month downloaded data.

If the final four-player 20-round RTDB transport measures ~1–5 MB downloaded per
match, the download allowance is roughly 2,000–10,000 matches/month before other
RTDB traffic. The 100-connection cap is about 25 simultaneous four-player games
or 50 simultaneous two-player games. These are planning estimates, not accepted
runtime measurements.

## Current implementation status

Implemented in v5.26.1:
- v5.26.0 compile blocker fixed (`HandlePublicCodeRaised` callback binding);
- RTDB deny-by-default rules source added;
- RTDB query indexes added to rules where planned;
- `firebase.json` wired to `database.rules.json`;
- architecture/capacity split recorded in the canonical roadmap.

Still pending:
- Unity Firebase Database SDK/module verification;
- RTDB Unity transport implementation;
- two-device Private Casual Online runtime test;
- measured RTDB bytes per match;
- migration of legacy polling off the rewarded path;
- trusted Online Rewarded verification backend.
