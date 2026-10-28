# Germany and Mexico Map Authoring Draft — not playable

Owner-approved Gold purchase price: Germany 30,000 Gold, Mexico 25,000 Gold. These are **account cosmetic entitlement prices**, never within-match city prices. The 24-city data files contain draft game-balance figures and multilingual fallback descriptive text (7 languages). They are **not historical facts or official economic statistics**. They are **not** Unity BoardMapDefinition/BoardTileDefinition assets and they are **not** wired into the Store, Lobby, RTDB, GameBoard or Career Economy.

**DO NOT deploy or sell these maps on the basis of this package alone.** The goal here is safe, concrete map data prep and ownership contract without touching functional existing maps.

Future full implementation: author 32 tile definitions with 24 city and 8 special cells, complete city pricing/rent/upgrades, regional/country-specific Civic Projects and Atlas Pulse, localized city Info (not just fallback), real mesh/background assets and previews, seven-language UI, secure RTDB Gold purchase and owner-only lobby choice, snapshot-verified room map sync, host-migration freeze, new-room free-default reset, Android/performance and two-client tests. Preserve existing Turkey/USA/Colorado maps. No duplicate map manager.

Review existing `BoardMapDefinition`, `BoardEconomyProfile`, `BoardTileDefinition`, Civic and Pulse data before runtime implementation. This draft intentionally changes **no** gameplay/RTDB files.

The two `*_32_Tiles_LAYOUT_DRAFT.csv` files expand the 24-city list into 32 ordered cells using 8 placeholder special slots. **These special tile types/positions are placeholders and are NOT connected to live BoardTileDefinition or validated game rules.**
