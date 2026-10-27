# Phase 19 Asset Import Notes — user-owned packages

This file lists user-side Unity Import Package selections. It does not contain, redistribute, or claim access to third-party FBX/prefab/texture binaries. Keep source publisher/EULA details in ASSET_CREDITS_REGISTER.md when actually used.

## PBR Little Games Pack – 4K (Ferocious Industries)
Select `SubstanceAssets/LittleGamesPack/Materials/GamePieces.mat`, `Models/GamePieces/Dice_White.fbx`, `Dice_Red.fbx`, `Dice_Blue.fbx`, `Dice_Black.fbx`, `Dice_Marble.fbx`, and the needed `Textures/GamePieces/*` source textures. Do not import the example scenes, dominos, playing cards, or unrelated prefabs. Compare/adjust D6 face orientation against the existing verified die results; do not change actual roll logic.

## Little Ghost lowpoly(FREE) (SR Studios Kerala)
Select `Animations/Ghost_Kid.controller`, `FBX/LittleGhost_LP.fbx`, the source `Prefabs/LittleGhost_LP (1..7)` candidates and Ghost Kid/Blue/Devil/Hat materials+textures needed by chosen prefabs. `Demo.unity`, `Floor Rounded`, and unrelated Axe mesh/material are optional/unneeded for normal and hat-wearing pawns; an axe-wielding prefab may require its axe files. Actual Animator state, material references and chosen prefab-to-catalog mapping must be validated in Unity.

## FREE Cartoon Car Pack - Simple Vehicles (OlyPoly)
Select `Materials/Car_Body.mat`, `Materials/ColorPalette.mat`, `Textures/ColorPalette.png`, `Models/Cars.fbx`, `Prefabs/Police.prefab`, `Taxi.prefab`, and `Pickup.prefab`. Deselect `Editor` folder including preview scripts, `FREECarScene.unity`, `preview.png` and unrelated vehicle prefabs. Verify 3D model/material references in Unity.

Use Unity Project search: `Dice_Black`, `LittleGhost_LP`, `Police`, then select asset and choose **Show in Explorer**. This locates the actual imported files; they are not included in the source ZIP.
