# AtlasBoard Emotes import — ready to extract (Phase 19X)

This delivery already contains **all 30** `PNG/Vector/Style 1/*.png` images from your Kenney zip. You **do not need to manually import another copy of the original archive**.

1. Close Unity Play Mode. Back up current project or at least Git status.
2. Extract the delivered AtlasBoard Phase 19X ZIP into `C:\Users\crazy\AtlasBoardPrototype`, not into `Assets` itself; retain original internal `Assets/Project/` path. Overwrite matching documentation/license files, preserve others.
3. Unity Project view: `Assets > Project > ThirdParty > Kenney > Emotes > Sprites`. You should see 30 PNG assets, from `emote__.png` through `emote_swirl.png`.
4. Unity menu: `Atlas Board > QA & Diagnostics > Configure Kenney Emote Sprites`. Expected: `ATB_EMOTE_SPRITES status=ready count=30`.
5. Select `emote_faceHappy.png` in Inspector; Texture Type should be `Sprite (2D and UI)`, Sprite Mode `Single`, Alpha transparency on. Repeat spot-check for faceSad/faceAngry.
6. `Assets/Project/ThirdPartyLicenses/Kenney_EmotesPack_Original_License.txt` is a verbatim copy of the original `License.txt`. `Kenney_EmotesPack_CC0.txt` is our project record. Keep both.
7. IMPORTANT: Importing the art does **not** enable Buy/Owned, six-active loadout, own-HUD emote popup, 3-second cooldown, 20 emotes / rolling 3 min, or 3-min mute. Those are separate RTDB/Unity runtime development still to do.
8. Original ZIP optional manual path: extract outside project and copy ONLY `PNG/Vector/Style 1` PNG files into our `Sprites` folder. **Do not copy both archives over each other**. Other 15 styles, `Tilesheets`, `Spritesheets`, `Vector` source and optional PNG variants need not be imported.
9. After Unity generates `.meta` files for the 30 sprites, include `.meta` in a version-controlled project so GUIDs remain stable.

Use this prepared ZIP instead of Assets > Import Package > Custom Package; source is ZIP art, not a `.unitypackage`.
