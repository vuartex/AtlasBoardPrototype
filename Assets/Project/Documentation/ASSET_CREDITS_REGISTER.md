## 2026-10-26 — Phase 19B nominated third-party packages (NOT IMPORTED)

These are **purchase/import candidates only** and are not bundled or redistributed with this ZIP:

- **PBR Little Games Pack – 4K** — Ferocious Industries — Unity Asset Store Standard EULA — D6 visual candidates: white, ruby/red, blue, black and graphite-gray. Asset Store listing shown by owner. License requires review before game distribution. Large download (~224 MB); extract only required dice meshes, materials and textures to the Unity project after the owner imports it. **Import, face orientation and mobile performance QA OPEN**.
- **Little Ghost lowpoly(FREE)** — SR Studios Kerala — Unity Asset Store Standard EULA — Halloween pawn candidate. Animation availability not yet verified. **NOT IMPORTED**.
- **Cartoon Vehicles Lowpoly** — Lowpoly_Master — Unity Asset Store Standard EULA listing shown by owner — future pawn candidates. **NOT IMPORTED**.

**Do not call these public-domain or copyright-free.** Free price is not unrestricted copyright ownership. The original in-project procedural profile frame art has no external asset dependency.
# AtlasBoard — Asset Credits & License Register

**v5.49.0, 26 October 2026.** This is the new register for Phase 19 cosmetics. Entries below are **candidates only**; none were imported or redistributed in the 19A ZIP. Existing project/package credits must be audited separately before release.

| Proposed use | Provider / pack | URL | Current listing | Asset version | Imported? | Credit / action |
| --- | --- | --- | --- | --- | --- | --- |
| Dice, game tokens, pawn reference | Daniel Riches — Board Game Essentials | https://assetstore.unity.com/packages/3d/props/board-game-essentials-284152 | Free; Standard Unity Asset Store EULA; Built-in/URP/HDRP compatibility listed | 1.0.1 | **NO** | Review imported files, full license, author/publisher, shaders, triangle budget and dice face mapping before use |
| Halloween Ghost pawn candidate | SR Studios Kerala — Little Ghost lowpoly(FREE) | https://assetstore.unity.com/packages/3d/characters/little-ghost-lowpoly-free-271926 | Free; Standard Unity Asset Store EULA; original Unity 2021.3.24; Built-in compatible, URP/HDRP not listed compatible | 1.0 | **NOT VERIFIED IMPORTED** | Owner Package Manager screenshot confirms library listing; verify package assets, animation clip(s), shaders, polygon count, prefab scale and mobile FPS after import; no binary redistributions |
| Future dice candidate | Armor and Rum — Collection Dice set for role-playing games | https://assetstore.unity.com/packages/3d/environments/collection-dice-set-for-role-playing-games-202821 | Free; Standard Unity Asset Store EULA; contains D6 mesh, PBR textures; package ~115 MB | 1.0 | **NO** | Dice-face calibration required; use only D6 after material/mobile review; compare lighter Board Game Essentials before import |
| Stylized pawn/reference props | Warcool — Low-Poly Objects Pack | https://assetstore.unity.com/packages/3d/props/low-poly-objects-pack-241890 | Free; Standard Unity Asset Store EULA; Built-in/URP compatible listing | 1.2 | **NO** | Review actual suitability and components |

## Rules

- *Free download* is not *copyright-free*. Assets are licensed; copyright remains with their creators. Read each listing and any separate EULA before inclusion.
- Only import relevant models/textures, keep dependencies and source URLs, record imported sub-assets and local modifications. Unity Asset Store packages must not be redistributed as standalone assets.
- Track commercial use, attribution requirements and any third-party music/fonts/decals separately; if unsure, exclude the asset.
- Do not add a downloaded pawn/dice model until its identity and folder are checked in the live Unity project; avoid duplicating existing `PawnCosmeticCatalog` assets or `DiceVisualController` face calibration.
- Self-authored palette/material cosmetics should be tagged `AtlasBoard original` and need no external author attribution. National flags are to be drawn using original vector/procedural content or separately license-reviewed sources.
- Phase 19A contains metadata/UI preview only; no licensed art imported yet.

## v5.49.1 — AtlasBoard original profile frame previews
- Four procedural uGUI/TMP frame preview designs: Halloween, New Year, Türkiye and USA. Created by AtlasBoard source code; no third-party image/prefab imported or redistributed.
- These are **visual previews only**. No player entitlement, profile equipment persistence, cross-client visibility or production purchase has been implemented in this revision.
- Owner's Unity Package Manager screenshot shows **Little Ghost lowpoly(FREE)** in My Assets, not proof it was imported into the Unity project. Confirm internal package prefab/animation files before coding actual pawn integration.

---

## 2026-10-27 — Phase 19W verified current asset usage (supersedes historical candidate status above; DO NOT DELETE history)

| Actual use | Publisher | License | Evidence / owner-confirmed status |
|---|---|---|---|
| Dice cosmetics: 5 six-sided models + shared materials | Ferocious Industries — PBR Little Games Pack - 4K | **Unity Asset Store Standard EULA**; free acquisition, **NOT CC0** | Unity import and owner-verified Ruby Red/Ocean Blue/Midnight Black/Marble faces and Classic bot behavior |
| Ghost pawns: 7 prefabs | SR Studios Kerala — Little Ghost lowpoly(FREE) | **Unity Asset Store Standard EULA**; free acquisition, **NOT CC0** | Owner-approved 360° preview, hover, scale and movement; multiplayer QA open |
| Vehicle pawns: Police, Taxi, Pickup | OlyPoly — FREE Cartoon Car Pack - Simple Vehicles | **Unity Asset Store Standard EULA**; free acquisition, **NOT CC0** | Owner-approved material repair, scale, preview and vehicle movement; rematch Pawn selection regression OPEN |
| Emote reactions and speech balloons (intended use) | Kenney — Emotes Pack | **CC0 1.0**; free to use/modify commercially, attribution not required | Owner selected/downloaded pack preview; precise imported sprites and full runtime integration not verified |

For exact source URLs, versions, restrictions and commercial-use notes see the four matching `Assets/Project/ThirdPartyLicenses/` records. Standard Unity EULA generally permits game-integrated cosmetics and in-game monetization but **does not allow standalone redistribution of the source packs**. Kenney CC0 emotes may be integrated in a monetized game; the Gold price belongs to the game service, not to a resale claim over the underlying artwork. Game source repository access must separately respect third-party redistribution restrictions.

**Emote deduplication:** Kenney's 480 PNG files represent stylistic/background variants of fewer distinct expressions, not 480 necessarily unique emotions. Product identity should be a canonical expression ID, and a single chosen visual style is shown in Shop and HUD. Never charge players multiple times for stylistic duplicate PNG variants unless designed and disclosed as distinct items.


## 2026-10-27 Phase 19X — actual Kenney Emotes PNG import and commercial release preflight

- Kenney Emotes Pack (`https://kenney.nl/assets/emotes-pack`): publisher Kenney Vleugels; CC0 1.0, 480 PNGs across 16 style variants, *30 icons from one style only* now included under `Assets/Project/ThirdParty/Kenney/Emotes/Sprites/`. Original archive `License.txt` copied verbatim to `ThirdPartyLicenses/Kenney_EmotesPack_Original_License.txt`. Commercial use, modification and integrated distribution on Steam/Google Play permitted; credit optional.
- Ferocious Industries `PBR Little Games Pack - 4K`: Free acquisition, Standard Unity Asset Store EULA (NOT CC0), 3D Dice. Product page currently labels the license type `Extension Asset`; verify account seat/license terms before release.
- SR Studios Kerala `Little Ghost lowpoly(FREE)`: Free acquisition, Standard Unity Asset Store EULA (NOT CC0), 7 Ghost pawn prefabs. Product page currently labels license type `Extension Asset`; verify entitlement/seat terms.
- OlyPoly `FREE Cartoon Car Pack - Simple Vehicles`: Free acquisition, Standard Unity Asset Store EULA (NOT CC0), Police/Taxi/Pickup models. Product page currently labels license type `Extension Asset`; verify entitlement/seat terms.
- EULA Section 2.2.1 permits integrated commercial games and game-integrated in-app monetization of qualified Non-Restricted assets. **However** source models/prefabs/textures are not standalone distributable; publicly accessible GitHub history with licensed raw assets should be audited and access restricted as needed. Do not treat embedded object monetization as asset sublicensing.
- LICENSE CLEARANCE STATUS: **PRE-RELEASE REVIEW OPEN** for three Unity Asset Store packs due to listing seat type and repository source exposure; this file is evidence/record, not legal clearance or advice.
- References: `https://unity.com/legal/as-terms`, `https://assetstore.unity.com/ko-KR/browse/eula-faq`, original individual listing URLs in ThirdPartyLicenses records.
