# AtlasBoard Phase 19O — Owner 3D Dice Rendering QA

Fix scope: imported PBR Little Games Pack UV atlas + Classic bot regression only. Existing Shop, RTDB MetaEconomy, secure rules, UID-seat selection and gameplay RNG are unchanged.

1. Exit Play Mode, unzip into `C:\Users\crazy\AtlasBoardPrototype` (preserve older installed 19K–19N code). Wait for compilation. Run **Atlas Board > QA & Diagnostics > Repair Imported Dice Model References** from Unity's top menu if the automatic binder has not yet run. Confirm `ATB_DICE_MATERIAL_BIND status=resolved` or `ATB_DICE_MODEL_BIND status=already_valid`; save the active Board_Prototype scene (Ctrl+S) before entering Play Mode.
2. In Career Inventory > Dice keep **Ruby Red [ON]**. Open Private Room with Vuartex human slot 0 and BOT 2 slot 1; roll from both seats. Vuartex must show a RED textured die with pip markings; bot must show the original WHITE numbered/pipped dice, not the imported blank White FBX.
3. Watch the exact log pairs, not just modelAvailable:
   - Human `ATB_DICE_SKIN_ROLL ... slot=0 bot=False skin=dice_ruby_red modelAvailable=True` and `ATB_DICE_SKIN_RENDER skin=dice_ruby_red originalDefault=False atlas=True imported=True`.
   - Bot `ATB_DICE_SKIN_ROLL ... slot=1 bot=True skin=classic modelAvailable=True originalClassic=True` and `ATB_DICE_SKIN_RENDER skin=classic originalDefault=True atlas=False imported=False`.
   - `ATB_DICE_ATLAS status=ready shader=Universal Render Pipeline/Lit albedo=true` once per loaded controller in URP.
4. Switch Ruby to Classic; confirm original white with pips. Re-equip Ruby, retest after reopening the lobby and across second/third matches. Test two distinct real humans/UIDs with different equipped dice in Private/Public. Check Windows/Steam and Android display & FPS.
5. If atlas is missing, `ATB_DICE_SKIN_FALLBACK ... render=original_pipped_tinted` preserves readable original pips with tinted bodies; this is a temporary safe fallback. Verify package `Assets/SubstanceAssets/LittleGamesPack/Materials/GamePieces.mat` and its `Textures/GamePieces/GamePieces_Base_color.png` have been imported. The ZIP intentionally does not redistribute third-party binaries.

**Do not mark calibrated 1–6 face mapping PASS yet.** The imported FBX UV orientation versus legacy faceLocalEulerRotations needs owner calibration later. Source contract checks are not real Unity Play Mode, Steamworks, Android or multiplayer tests. No Firebase redeploy and no GitHub push.
