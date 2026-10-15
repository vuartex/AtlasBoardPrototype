# AtlasBoard v5.30.10 — Unity Editor QA (no APK)

1. Extract full-file ZIP into `C:\Users\crazy\AtlasBoardPrototype`. Keep v5.30.9 and v5.30.8 files not included here.
2. Wait for Unity 6000.5.6f1 Editor scripts to compile. **Zero red console errors required.** Do not build APK for this UI test.
3. Use `Atlas Board > QA & Diagnostics > QA > Travel > 1 - Preview Modern Travel (Safe)`. Open **Game** view: the modern card should fill the wide example tablet. Look for `ATB_TRAVEL_LAYOUT_QA_PREVIEW_READY widthRatio=0.89`; `QA_FAIL` means reject. Preview buttons are deliberately inactive and DO NOT invoke gameplay.
4. Choose `2 - Close Preview` before real gameplay. Start a Local Offline match and trigger a real Travel encounter naturally or via existing supported gameplay test controls. Inspect the Travel panel width, readable text and large two buttons. Verify TRAVEL/STAY decisions function in the real game (never via the safe preview).
5. With Steam initialized, open Main Menu profile card. The Profile modal must use the exact same Steam name shown on the card, without the old Guest alias. Go to Career, Settings, Shop, Private Table and back. Repeat.
6. Test first/second/third match: inspect modern Property, Trade, Travel and City Info; keep tablet open >=30 seconds to rule out HUD flashes; verify no duplicate overlays.
7. Collect new Unity Console output on any failure. Do not mark PASS based on this artifact alone. Current HTTP 404, RTDB 401, Android 20–22 FPS and trusted Career backend remain unaddressed.
