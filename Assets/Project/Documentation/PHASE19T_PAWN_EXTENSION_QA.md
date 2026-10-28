# Phase 19T Pawn Extension — Install and Owner QA

1. Exit Unity Play mode. Overlay the ZIP to `C:\Users\crazy\AtlasBoardPrototype`. All files are complete sources at their original paths.
2. **Do not** run "Build Pawn Customization v1"; it reconstructs the original eight-pawn catalog. Use **Atlas Board → Design → Board & Presentation → Pawns → Extend Existing Catalog - Ghosts and Cars** exactly once. Re-running this *new* option is safe/idempotent.
3. Console: `ATB_PAWN_IMPORT status=ready original_preserved=true existing=8 appended=10 missing=0 catalog_total=18` on first successful extension. Any missing prefab is explicitly reported and skipped; stop and inspect before testing its pawn.
4. Existing Private/Public Lobby pawn avatar button opens the SAME selector. Pages 1–8 should still display original pawns; 9–15 are ghost models, 16–18 are Police/Taxi/Pickup. Large preview rotates around vertical axis. If models are magenta check the third-party material shader and assets, do not recreate a duplicate cosmetic manager.
5. Start local game: slot 0 may select ghost/car. Bots/other slots cannot be changed from slot 0's selector. Verify original 8 movement still works; ghost floats softly, car moves without walk bob. Confirm facing on every square corner, civic/forward move, four players on same city and colored identity discs.
6. The old user profile/Gold/RTDB/board/dice systems are unchanged. The 10 imported models have **no invented Gold entitlement or Pawn Store Buy flow** in this package.
7. Two-client remote self-selection requires an additional secure network owner-write/host-read protocol; the current host-only lobby operation is a **known open blocker**, not a tested PASS. Do not relax RTDB security rules. Test Private and Public after this protocol is correctly implemented.
8. Save project asset modifications. When GitHub backing up, include the updated canonical `PawnCosmeticCatalog_Default.asset`, all `Pawn_ATB_*.asset` files with `.meta`, scripts, roadmap, and any modified scene saved by Unity. The Editor menu does not add a new scene object. GitHub push not performed by assistant.

## Quality status

- Source/static invariants: checked in Python on provided/GitHub-matched source.
- Unity compiler: not available in assistant runtime.
- Unity 360 preview, motion, layout: owner test OPEN.
- Two Unity clients / Firebase security: OPEN.
- Windows/Steam/Android build: OPEN.
