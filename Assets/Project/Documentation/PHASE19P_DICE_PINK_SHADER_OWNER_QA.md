# Phase 19P — Owner QA: Fix pink imported dice material

The prior runtime-created `Universal Render Pipeline/Lit` material rendered magenta even though the albedo reference was present. This package changes the existing Unity Editor reference binder to create one persistent `ATB_Dice_GamePieces_URP_Unlit.mat` in `Assets/Project/Materials/` and serialize it on the **existing** DiceVisualController. Its UV base texture comes directly from the imported `GamePieces.mat`; FBX meshes are not altered. The material is intentionally URP/Unlit first, to prioritize readable colors/pips across Windows and Android before PBR polish and face calibration.

1. Exit Play Mode. Extract the ZIP at `C:\Users\crazy\AtlasBoardPrototype` with original paths and overwrite the complete source files.
2. Wait for Unity C# recompilation, and run **Atlas Board → QA & Diagnostics → Repair Imported Dice Model References** in the scene `Assets/Board_Prototype.unity`.
3. In Project search for `ATB_Dice_GamePieces_URP_Unlit.mat`. It should be created under `Assets/Project/Materials`. Select existing `DiceVisualController` and confirm `Imported Dice Render Material` is assigned to this asset (not null).
4. Save the open scene with **Ctrl+S**. On Play, look for `ATB_DICE_ATLAS status=ready source=serialized_material shader=Universal Render Pipeline/Unlit` and Ruby Red `ATB_DICE_SKIN_RENDER ... imported=True`.
5. Verify Vuartex's equipped Ruby Red now has red faces with visible dice pips and BOT uses the original white/black-pip dice; check switch-back, Store/Career preview, second/third game. Face orientation calibration and online two-device tests still OPEN.
6. **If magenta persists:** report Console errors mentioning shader compile, `ATB_DICE_ATLAS`, renderer asset and `ATB_DICE_RENDER_MATERIAL_BIND`; capture the newly generated material's Inspector shader. This material fix has not been Unity Play Mode verified by the assistant.

Do not run Firebase deploy; no database rules changed. Do not push GitHub without owner approval. The generated material + its `.meta` and updated saved scene must be included at the next owner-approved backup/build.
