# AtlasBoard — Phase 17D Mobile Gameplay UX v5.27.0

Date: 2026-10-14
Baseline GitHub HEAD: `f170229ca4e20c255d605a35249939f516a863b3`

## Purpose

Make the Android/iOS gameplay path touch-first without changing the existing
Windows/Steam desktop behavior. This phase also restores the current modern
PRIVATE TABLE seat UI for Local Offline sessions.

## Mobile-only gameplay controls

- Create a large `ROLL DICE` action to the right of the lower-left player HUD.
- Create a large `TRADE` action to the left of the lower-right player HUD.
- Keep the existing desktop/scene buttons intact; the mobile buttons are an
  additional mobile presentation layer only.
- Hide mobile actions while a tablet/modal is open.
- Roll availability follows `TurnManager.CanPlayerRequestRoll`.
- Trade availability follows `TurnManager.CanStartManagementAction`.

## Touch camera

When a match is active and no UI modal/city panel owns the touches:

- one-finger horizontal drag rotates the board camera;
- two-finger pinch zooms the camera;
- two-finger average drag pans the camera;
- touches that begin over UI are ignored by the board camera.

Mouse wheel, right-drag, middle-drag and keyboard reset behavior remain
unchanged on desktop.

## Tablet/trade and city information

On phones/tablets only:

- tablet presentation scales to 1.12x to increase touch targets and text;
- city information grows to 570x700 reference units;
- city body text minimum is increased;
- city ScrollRect uses touch inertia and higher touch scroll sensitivity;
- while city/tablet UI is open, board-camera touch gestures are suppressed.

## PRIVATE TABLE regression fix

The v5.26 Local Offline fallback routed to `OpenPrivateLobbyAfterRoomChoice()`
without activating the current private seat presentation. That exposed the old
prototype `Human/Bot` dropdowns.

v5.27.0 reuses the existing current private-lobby controller in local mode:

1. close the online create/join chooser;
2. set Local Offline host state with no backend snapshot;
3. open the PRIVATE TABLE lobby screen;
4. enable the current private seat presentation;
5. run `ConfigureHostLobbyPreview()` with a null backend snapshot;
6. expose the existing `+ -> Local Player / Bot` actions;
7. synchronize those seat selections into the existing legacy match-start
   controls only as an internal compatibility bridge.

No Cloud Functions/Firestore/RTDB gameplay traffic is created for this Local
Offline path and no persistent XP/reward authority is granted.

## Mobile frame-rate telemetry

The previous Android log did not contain an FPS counter. v5.27.0 records a
10-second performance window:

- average FPS;
- worst frame time in milliseconds;
- configured target FPS;
- MSAA level;
- pixel-light count;
- allocated memory;
- current resolution.

Log marker:

`ATB_MOBILE_PERF avgFps=... worstFrameMs=...`

If average FPS stays below 42 for two consecutive windows and MSAA is above
2x, Android/iOS alone falls back to 2x MSAA and records
`ATB_MOBILE_PERF_ADAPT`. Gameplay timers, bot logic, dice results and pawn move
rules are not changed by this fallback.

## Device acceptance checklist

1. Build the Android Development APK successfully.
2. Start with Firebase emulator closed.
3. Open PRIVATE TABLE and confirm no old Human/Bot dropdown lobby appears.
4. Confirm the modern `+` seat action exposes Local Player and Bot choices.
5. Start 1 local human + 3 bots.
6. Confirm `ROLL DICE` is beside the lower-left HUD and is only enabled when a
   local human can roll.
7. Confirm `TRADE` is beside the lower-right HUD and is enabled only before a
   valid local-human roll/management action.
8. Confirm one-finger board rotate.
9. Confirm two-finger pinch zoom and two-finger pan.
10. Open a city card, drag vertically through its content, and confirm the
    board camera does not move.
11. Open trade/purchase/auction/travel/event/development tablet surfaces and
    confirm text/touch targets are materially larger and do not clip.
12. Play for at least 30 seconds and share logs containing at least two
    `ATB_MOBILE_PERF` lines.
13. If `ATB_MOBILE_PERF_ADAPT` appears, compare FPS before/after it.
14. Verify desktop Editor/Windows still use the original mouse/keyboard UI and
    receive no mobile action overlay.

## Not completed by this phase

- RTDB remote Private Casual multiplayer transport;
- rewarded-online authoritative replay verification;
- Google Play release AAB/signing validation;
- APK size optimization;
- final production App Check enforcement.
