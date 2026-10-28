# ATLASBOARD Phase 19W — EMOTES / MAPS Design Contract (2026-10-27)

**Status: SPECIFICATION / LICENSE UPDATE. Not a completed gameplay implementation.**

## Change approved by owner
- Replace the planned Store/Inventory **Motion** cosmetic category with **Maps**, retaining underlying `PawnMotionAnimator`, `PawnMotionSetDefinition`, and the working movement code unchanged. Merely changing a text label is not a complete Maps product.
- Keep Pawns, Boards, Dice, Profiles and Career Economy intact. Do not create a duplicate wallet, inventory, lobby, map or gesture manager. Single canonical roadmap remains authoritative.

## Emotes — user acceptance contract

**Assets:** Kenney Emotes Pack, CC0 1.0, 480 PNG outputs comprising visual variants of a smaller set of unique reactions. Use a stable semantic ID per distinct reaction; do not count alternate bubble backgrounds or pixel/vector exports as distinct purchasable emotions. Follow actual imported filenames, not guessed paths. Owner previews show happy/sad/angry and many other emotions.

**Free default entitlements:** simple happy/smile, sad, and angry expressions are always owned by every player (including new accounts); never appear as purchasable Store products. Default active loadout initially includes these 3; current client may choose up to 6 total. In absence of RTDB loadout, use safe defaults without creating unauthorized paid ownership.

**Paid catalog:** remaining distinct Kenney emotions purchasable with in-game Gold, via existing RTDB ETag wallet transactions. No separate cash/currency or distinct purchase per style. UI needs clear name, sprite preview, price, Buy/Owned state. Shop label Events unchanged; Store Motion category becomes Maps after Maps feature readiness. Ownership in Career Inventory persists per authenticated UID. All names and prompts supported in existing 7 languages (EN, TR, ES, FR, DE, KO, RU).

**Selection:** Career -> Inventory -> Emotes shows Owned, Active, inactive actions. A player may equip MAX 6 active. Attempting a seventh must NOT auto-unequip an unexpected item; explicit message 'Remove one of your six active emotes first'. No duplicates. Unowned cannot equip. All matching clients show active loadout, only account owner may change it.

**Match-only emote UX:** clicking one's OWN Player HUD opens scrollable/paginated popup of up to six equipped reactions. Clicking another human's HUD or bot HUD must not open sender controls. Popup must not appear on Main Menu, Lobby or outside a live match. Lower-left/lower-right HUDs show the selected reaction **above**, aligned to left edge; upper-left/upper-right HUDs show it **below**. The bubble visually extends toward HUD's left side without clipping or covering dice/buttons. Display duration e.g. 2.0–2.5 seconds, configurable, no permanent world-space object; 2D uGUI preferred. Use existing HUD controls and maintain existing 7-language fallback.

**Anti-spam (design decision, requires secure RTDB enforcement):** >=3 seconds between sends; count at most 20 accepted emotes in rolling 3-minute window, then 3-minute mute with localized warning (TR 'Çok fazla emote kullandın. 3 dakika ara ver.'). Exact server-authoritative window state must be restricted to owner UID with room membership and timestamp progression; reject forged other-user emotes, counterfeit entitlements and incorrect event counters. UI local debounce alone is insufficient, especially online.

**Multiplayer:** sender event must include room ID, sender UID, monotonic sequence and server-validated time; authorized room participants receive the same emote without duplication after reconnect. Late packets should not flash old bubbles after a match ends or when another room starts. Host migration does not grant host control over others' emotes. Spark RTDB ONLY; no Functions/Firestore/Blaze. For bots, do not auto-generate purchasable reactions.

**Tests OPEN:** exact Kenney PNG import, sprite identity/uniqueness, Unity compile, full shop Buy/Owned/equip, RTDB permission/emulator, multi-player concurrent/sender spoof, HUD positions under 16:9/tablet/small-screen, second/third match, Android, seven languages. Do not label as PASS until actually tested.

## Maps — Germany and Mexico (deferred complete implementation)

**Model:** existing `BoardMapDefinition`/`BoardEconomyProfile`/`BoardTileDefinition`; 32 ordered tile positions, 24 city properties plus 8 special tiles; existing civic, Pulse, tile info, investment/traffic/prosperity/stability systems, political/regulatory cards where supported. Separate map ID and data per country; no replacing Türkiye/USA/Colorado definitions, no shadow data source. Cities named and localized accurately; write original seven-language Info texts, civic project catalogs, country-neutral terminology, and financial data. Game balance numbers are authored fictional game values, NOT verified economic indicators.

**Premium proposed Gold prices, awaiting owner signoff:** Mexico 25,000 Gold; Germany 30,000 Gold. Mark 'PROPOSED' until implemented and approved. Store preview can show city/landmark overview and product entitlement. Do not confuse premium Gold price with within-match in-world money (MXN/EUR symbols) or Atlas Coin. Starting cash, city purchase cost, rent, upgrades, investments, and project budgets must be defined in each `BoardEconomyProfile` and validated for plausible relative difficulty.

**Candidate 24 cities — Germany:** Berlin, Munich, Hamburg, Frankfurt am Main, Cologne, Stuttgart, Düsseldorf, Leipzig, Dresden, Hanover, Bremen, Dortmund, Essen, Nuremberg, Bonn, Karlsruhe, Freiburg, Heidelberg, Augsburg, Regensburg, Münster, Aachen, Kiel, Rostock.

**Candidate 24 cities — Mexico:** Mexico City, Guadalajara, Monterrey, Puebla, Tijuana, Mérida, Cancún, Querétaro, León, Toluca, Veracruz, Oaxaca, Aguascalientes, San Luis Potosí, Chihuahua, Ciudad Juárez, Hermosillo, Culiacán, Mazatlán, Morelia, Campeche, Villahermosa, Saltillo, Acapulco.

Each needs stable `property_01..24`, grouping, balanced buy/rent/building rates, accurate location associations and authored infobox text for all seven supported languages. These are *proposed city lists*, not approved economic statistics.

**Host control:** an online room's Map dropdown filters to maps OWNED by the current host account; existing freely included maps stay free. During lobby selection every peer sees the selected map through room metadata and an appropriate pre-match background/board preview. The map must remain unchanged mid-match during host migration. **A new room defaults to a safe free map for its new host**; no stale prior owner premium selection from remembered local state. If a selected map cannot be legitimately owned/loaded, fall back to a free map visibly (not silently cheating ownership). Private/Public, second/third matches, local/offline, reconnect, Android and 7-language UI need separate tests.

**Status:** No Germany/Mexico city asset sets, card definitions, civic sets, localized info, Pulse baselines, income balancing or backend entitlement changes have been authored in this handoff. Do NOT just relabel existing map assets and mark this complete.

## Existing Pawn regression remains OPEN
Returning Main Menu then applying a different Pawn in a new lobby can render the prior Pawn in the next match. Suspect stale `onlineAuthoritativeCosmeticActive` in `PawnCosmeticApplier` (set true during online sync and not automatically cleared). Reset only when session changes, preserve remote-seat authority, fresh selected UID/slot, old eight pawn animations, camera rotation, position safety and owner-only rights. Needs source implementation + owner second/third-game QA.

## Source/license notes
- Kenney https://kenney.nl/assets/emotes-pack — CC0; commercial use allowed; credits optional but retained.
- Ferocious Industries https://assetstore.unity.com/packages/3d/props/pbr-little-games-pack-4k-237164 — Standard Unity Asset Store EULA.
- SR Studios Kerala https://assetstore.unity.com/packages/3d/characters/little-ghost-lowpoly-free-271926 — Standard Unity Asset Store EULA.
- OlyPoly https://assetstore.unity.com/packages/3d/vehicles/land/free-cartoon-car-pack-simple-vehicles-282425 — Standard Unity Asset Store EULA.
- The user is responsible for preserving licensed acquisition entitlement; source models/prefabs from Asset Store cannot be republished standalone even if acquisition price was $0.
