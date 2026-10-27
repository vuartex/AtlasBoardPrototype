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
| Stylized pawn/reference props | Warcool — Low-Poly Objects Pack | https://assetstore.unity.com/packages/3d/props/low-poly-objects-pack-241890 | Free; Standard Unity Asset Store EULA; Built-in/URP compatible listing | 1.2 | **NO** | Review actual suitability and components before import; no source/publisher binaries included |

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
