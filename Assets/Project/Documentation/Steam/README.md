# Atlas Board — Steam Integration Reference

This folder is the repository-side canonical reference for Steam platform
configuration that should remain readable from GitHub across future phases.

## Achievement API mapping

`STEAM_ACHIEVEMENT_API_NAMES.csv` maps the canonical internal AtlasBoard
achievement IDs from Phase 10 to their planned Steamworks achievement API
names.

Rules:
- AtlasBoard internal achievement IDs remain canonical.
- Steam API names are provider projections only.
- Do not rename the internal IDs to match Steam.
- The Steamworks dashboard must use the Steam API names exactly as listed in
  the CSV when the real AtlasBoard Steam AppID is available.

## Phase 11B checkpoint

Accepted development checkpoint:
- Steamworks.NET compiled.
- Phase 11B static validator PASS 8/8.
- Real Steam provider initialized in Unity Editor.
- Steam AppID 480 (Spacewar) used for development-only client testing.
- Real SteamID and Steam persona are visible in diagnostics.
- Canonical Firebase AccountId remains separate from Steam Platform User Id.

## AppID 480 limitation

Spacewar AppID 480 is useful for Steam client initialization, Steam identity,
overlay and Rich Presence development testing.

AtlasBoard's custom achievement API names are not configured in Spacewar.
Actual Steam achievement projection requires the real AtlasBoard Steam AppID
and the 14 API names from `STEAM_ACHIEVEMENT_API_NAMES.csv` to be created and
published in Steamworks.

## Security boundary

Do not persist or trust a client-supplied SteamID as an Atlas account link.
Verified account linking belongs to Phase 11C:
Steam authentication ticket -> server-side verification -> verified Steam
identity -> canonical Atlas AccountId mapping/conflict handling.
