# AtlasBoard v5.49.12k — Phase 19U Pawn Store / Preview / Appearance candidate

## Purpose and owner feedback

Owner verified Phase 19T: all 7 Little Ghost prefabs render and rotate in the EXISTING lobby Pawn modal, ghost floating and vehicle motion/turns function, 4 pawns on one tile were non-overlapping. Three vehicles (Police, Taxi, Pickup) appear magenta; ghosts and vehicles are too small; changing pawn between games sometimes leaves the prior pawn. Phase 19T did not include purchases, so paid-ownership testing was impossible. These are owner-observed findings, not assistant Unity tests.

## Scope of this full-source ZIP

- Update **existing** `AtlasBoardPhase19TPawnAssetExtension` menu. Run `Atlas Board > Design > Board & Presentation > Pawns > Extend Existing Catalog - Ghosts and Cars` outside Play Mode **after** installing ZIP. It reuses the canonical catalog's existing eight entries, updates the 10 already-added entries, and creates exactly one AtlasBoard-owned `ATB_PawnCar_ColorPalette_Standard.mat` from the imported OlyPoly `ColorPalette.png`. It does **not** rewrite third-party prefabs. If the texture is missing, it logs a failure; do not fake success.
- Increase new ghost height target to 1.32 / XZ cap to 0.53 world units and new car height to 0.95 / cap 0.56. Existing original-eight pawn dimensions and movement logic untouched; 4-on-tile remains **owner re-test required** after resizing.
- `PawnCosmeticDefinition` has an optional serialized `overrideMaterial` only for new cars; existing `PawnCosmeticApplier` and its lobby 360-degree preview use it, without changing original shaders/skins.
- The normal `AtlasBoardPawnCustomizationUI` now shows **BUY IN STORE** and disables `Use Pawn` on unowned premium IDs; owner-accepted choice events refresh old visual state on rematches instead of being suppressed by a stale authority flag. `AtlasBoardPawnCosmeticService` falls back to an existing free Kenney pawn for an unowned locally cached paid choice. No duplicate modal, pawn system, or collision manager is introduced.
- Store uses the canonical Wallet/Inventory and ETag `PURCHASE` transaction for `ghost_01..ghost_07`, `vehicle_police`, `vehicle_taxi`, `vehicle_pickup`. Seven ghosts: 300 Gold each, Police: 450, Taxi: 400, Pickup: 425. Prices are **candidate prices**, not owner-confirmed; change in both code and RTDB validated rule before production release if desired. Four old unimplemented draft-only `pawn_knight`/`rocket`/`rover`/`compass` entries are hidden from Store (their roadmap design entries remain intact).
- Store uses the **existing** Preview overlay, with a non-spinning 3D GameObject snapshot rendered only when a premium Pawn is inspected (no second Store panel). Career Inventory/Pawns shows server-owned purchases and a static preview. Paid cosmetics can be chosen from the existing Lobby Pawn modal only after the current UID's verified wallet has loaded.
- RTDB schema extends `/users/{uid}/metaEconomy/inventory/pawns` with **ten booleans**. Existing profile frames, dice, boards, and Gold balances stay in the same `metaEconomy` ETag transaction. `INIT`, `PURCHASE`, `DEV_GRANT`, `DAILY`, `RESET_PURCHASES`, `EQUIP*` validations were extended without making arbitrary writes possible. Unauthorized users remain blocked; no Firestore or Cloud Functions added.

## IMPORTANT: Firebase deploy

This is a new RTDB rules change. The included `Backend/Firebase/database.rules.json` **has not been run through Firebase emulator** and is **not deployed**. Local JavaScript tests simulate RTDB rule expressions only, so do not call them Production security PASS. Run an emulator/rules validation before deploying Production. After emulator tests, from PowerShell:

```
Set-Location "C:\Users\crazy\AtlasBoardPrototype\Backend\Firebase"
firebase.cmd deploy --only database --project atlasboard-usa
```

Do not deploy in this chat on the owner's behalf.

## Unity acceptance sequence

1. Leave Play Mode; unzip using original paths; let Unity compile. Check Console for errors. Run ONLY **Extend Existing Catalog - Ghosts and Cars**. Do not run older **Build Pawn Customization v1**.
2. `ATB_PAWN_MATERIAL status=ready texture=true ... supported=True`; inspect `Assets/Project/Materials/ATB_PawnCar_ColorPalette_Standard.mat`. Police/Taxi/Pickup lobby 360° previews must have colors and no magenta.
3. Inspect ghost 9–15 and vehicles 16–18. Compare height against one original Kenney (originals do not change). Ghosts float; vehicles drive, turn left around four corners and in Civic back-and-forth movement. Test all 4 players on **same tile**, with their identity discs, no clipping.
4. Before purchasing, view each Pawn in Shop/Pawns, open existing static 3D preview, verify the image and correct Gold. `Use Pawn` must be locked for unowned premium items.
5. After emulator/approved RTDB rule deployment, buy one Ghost (300 Gold) and one car (e.g. Taxi 400); Gold must decrease **exactly once**, Store shows Owned, Career Inventory/Pawns shows purchased entries. Restart game, confirm persistence. Duplicate purchase must be rejected. Old board/dice/profile purchases still work.
6. In Lobby select an **owned** new pawn, click `Use Pawn`; start match, then return Main Menu, choose a *different* owned/original pawn, click `Use Pawn`, start second/third match: no old pawn should remain; no duplicated pawn models.
7. In online private/public with two actual clients, ensure remote player's pawn is read-only for host and bot seats are not host-editable. **Known OPEN:** existing `lobbySetPawnCosmetic` routes remote-user requests through host-only processing, so remote-owner choice and cross-client persistence still require separate server-authoritative fix. Do not mark multiplayer OWNER PASS before tested.
8. Windows/Steam/Android builds, tablet view, seven locales, cross-device ownership, four-on-tile and movement all remain **OPEN** until owner confirms.

## Source-level validation (not Unity)

`node Backend/Firebase/qa/phase19u_pawn_purchase_contract.test.cjs`: 91 simulated validation/source checks PASS.

`node Backend/Firebase/qa/phase19u_existing_commerce_regression.test.cjs`: 60 migrated dice/board/profile source checks PASS.

`node Backend/Firebase/qa/phase19l_steam_wallet_ui_contract.test.cjs`: 34 source checks PASS (when the matching previous QA fixture is present).

All are static/simulated rule checks; actual Firebase emulator and real Unity are NOT RUN.

## Owner/project workflow

Original Kenney 8 pawns, `PlayerPawnMover`, dice results, RTDB wallet UID checks, guest identity, original lobby modal, seven languages, Steam and Android architecture preserved in this candidate. The canonical `Assets/Project/Documentation/MASTER_ROADMAP.md` is updated append-only. Package includes **only full modified or new files at original paths**. It intentionally excludes FBX/third-party materials and scene overwrites. Once Unity editor generates/updates `Pawn_ATB_*.asset` and the new `.mat`, include their `.meta` in the *owner's local* git backup. No push performed.
