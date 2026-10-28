# Phase 19R — Imported Dice Face Calibration (v5.49.12i)

**Scope:** One existing `DiceVisualController.cs`. No FBX/model/UV/material/scene edits. Existing `faceLocalEulerRotations` authored in `Board_Prototype.unity` continue to calibrate **original Classic**. The new imported-only remap reuses those rotations in the permutation 6,4,2,5,3,1 so that the imported PBR Little Games Pack D6 top faces display values 1..6 correctly. Ruby Red and Ocean Blue supplied the six-face evidence. Black/Marble use the same remap but are not yet owner visually verified.

## Exact evidence and correction

| Requested/authoritative value | Owner observed imported upper face (before) | Rotation formerly used for Classic face | Expected imported face (after) |
| --- | --- | --- | --- |
| 1 | 6 | 6 | 1 |
| 2 | 3 | 4 | 2 |
| 3 | 5 | 2 | 3 |
| 4 | 2 | 5 | 4 |
| 5 | 4 | 3 | 5 |
| 6 | 1 | 1 | 6 |

The two dice are separately set using their own passed values; do **not** swap left/right dice, change `TurnManager`, or modify random generation. No cosmetic value alters gameplay.

## Manual test (Unity, real display)

1. Close Play Mode, import the complete source ZIP at project root. Do not overwrite third-party assets or run the material repair unless the material itself has broken again.
2. Confirm there is no Console compile error; Ruby Red remains in Career Inventory and is equipped.
3. Roll Ruby Red several times: the top numbers from **left die + right die** should match `Dice visual result: A + B = total`. Look for `ATB_DICE_FACE_CALIBRATION mode=imported_19r target=A+B` and `ATB_DICE_SKIN_RENDER ... imported=True`.
4. Equip Ocean Blue and repeat, including doubles 1+1 and mixed results.
5. Bot turn must show original White/Classic, black pips, with `ATB_DICE_FACE_CALIBRATION mode=classic_unchanged`. Switch your human inventory back to Classic and verify it also uses the original die (no new FBX).
6. Open a second and third match to confirm no previous human skin appears on bot turns. Verify first/second die are not swapped.
7. Separately test Black and Marble to confirm they have the same FBX orientation. Test 2 real UIDs (each with different equipped dice) and Windows/Steam/Android builds; these tests remain **OPEN** until actual owner evidence.

**Known limitation:** The first v5.49.12i pass calibrates **top-face numbers** only. It does not assert in-world rolling physics or a particular dot orientation around the vertical axis. Existing animation and RNG remain unchanged. `originalClassic=True` and fallback pipped renderers bypass the new remap.

**Static QA:** `node Backend/Firebase/qa/phase19r_dice_face_calibration_contract.test.cjs`. This checks source contract and owner-derived permutation; **not** a Unity compilation/visual PASS. No RTDB rules change, no Firebase deploy, no GitHub push.
