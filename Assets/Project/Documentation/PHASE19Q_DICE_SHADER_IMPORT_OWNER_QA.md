# Phase 19Q — Owner QA: dice material is magenta even in Inspector

Owner screenshot shows **magenta sphere in Material Inspector before Play** for `ATB_Dice_GamePieces_URP_Unlit.mat`. This indicates the shader/pass rendering is invalid; it does NOT prove an FBX or PBR atlas package was imported incompletely. Bots already display original black-pip white dice (owner visual PASS). Vuartex Ruby Red purchase/equip, model references and slot mapping work, but paid dice color/pips are OPEN.

The original `Assets/SubstanceAssets/LittleGamesPack` FBXs and `GamePieces.mat` with `GamePieces_Base_color.png` are retained. The existing Editor repair tool now assigns a serialized, project-owned shader `Assets/Project/Shaders/AtlasBoardDiceAtlasUnlit.shader` to the existing `Assets/Project/Materials/ATB_Dice_GamePieces_URP_Unlit.mat`, in place. No duplicate shader manager, dice controller, or economy. The custom shader samples imported atlas UV directly, with URP Forward pass. If shader is missing or invalid the game renders the original pipped dice tinted for humans; bots continue using the exact original classic.

## Owner steps

1. Exit Play Mode and extract the ZIP at `C:\Users\crazy\AtlasBoardPrototype`. Wait for Unity import/recompile.
2. In Unity open `Assets/Board_Prototype.unity`, run `Atlas Board > QA & Diagnostics > Repair Imported Dice Model References`.
3. Select existing `Assets/Project/Materials/ATB_Dice_GamePieces_URP_Unlit.mat`. Inspector Shader must be **AtlasBoard/Dice/AtlasUnlitURP**, not `Universal Render Pipeline/Unlit`. Material preview sphere must be **not magenta**. Base Map must point to GamePieces_Base_color.png. If material is still magenta, **do not claim fixed**; inspect Console shader compile errors before Play.
4. Save scene Ctrl+S. Play and verify Ruby Red shows red UV atlas faces/pips; bots are original white/black-pip. Verify after two more matches, Store and Inventory previews.
5. Real Unity shader compilation, Android build, and two-client online view are OPEN until tested by owner. Any FBX UV numbering calibration also OPEN.

No Firebase rule changes, deploy, or GitHub push. When later backing up GitHub, include the `.shader`, `.shader.meta`, generated `.mat` with its `.meta`, and saved scene; these do not automatically sync just by pressing the Editor repair menu.
