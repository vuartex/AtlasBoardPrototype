# Phase 11C — Verified Steam Account Linking

## Security model

AtlasBoard never accepts a client-supplied SteamID as a verified production
account link.

Production flow:

1. Steam client calls `ISteamUser::GetAuthTicketForWebApi` with identity:
   `atlasboard-account-link-v1`
2. Client waits for `GetTicketForWebApiResponse_t`.
3. Only the hexadecimal ticket is sent to the authenticated Atlas callable.
4. Cloud Functions calls Valve's
   `ISteamUserAuth/AuthenticateUserTicket/v1` endpoint from the server.
5. Valve returns the verified Steam64 identity.
6. Firestore atomically creates both lookup directions:
   - SteamID -> canonical Atlas AccountId
   - Atlas AccountId -> SteamID
7. Conflicting links are rejected.
8. Raw Steam tickets are never persisted or logged.
9. Audit evidence stores only a SHA-256 proof digest.

Official Valve requirement:
`AuthenticateUserTicket` requires a publisher authentication key and must be
called from a secure server, never from the client.

## Production configuration

Real verified linking remains fail-closed until both are configured:

- `STEAM_APP_ID`
  - actual AtlasBoard Steam AppID
- `STEAM_PUBLISHER_WEB_API_KEY`
  - Firebase/Google Cloud Secret Manager secret
  - never store this value in GitHub or Unity

AppID 480 / Spacewar is not the production AtlasBoard publisher identity.

## Development test

The emulator-only callable:
`platformSteamDevLinkCurrentAccount`

exists only to test:
- one SteamID -> one Atlas AccountId
- one Atlas AccountId -> one SteamID
- replay idempotency
- mapping persistence
- audit evidence

It marks the record:
- `verified = false`
- `developmentOnly = true`
- `verificationMode = emulator_dev_proof`

Production code must never treat that record as a verified Steam sign-in.

## Returning Steam sign-in

Phase 11C v1.0.0 establishes verified linking to an already authenticated
canonical Atlas account.

Automatic returning-user Steam -> Firebase sign-in/custom-token issuance is
the next account-auth slice after the real AtlasBoard AppID/publisher
credential is available. It must only consume `verified = true` mappings.
