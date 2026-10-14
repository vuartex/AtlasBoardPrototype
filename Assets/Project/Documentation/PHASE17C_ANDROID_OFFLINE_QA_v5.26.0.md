# AtlasBoard Phase 17C — Android Offline/Cost QA v5.26.0

## Scope

This device QA validates only the v5.26.0 changes:
- mobile Apply must not trigger portrait-resolution repair;
- mobile UI uses a slightly larger safe presentation area;
- startup can fall back to Local Offline without manual Continue when online
  identity is unavailable;
- PRIVATE TABLE can route to the existing local lobby with no online identity;
- local/bot gameplay remains explicitly non-rewarded;
- previous Android logging/orientation/back handling remains intact.

It does NOT validate Firebase Realtime Database transport. RTDB migration is the
next Phase 17C networking implementation after this local gate passes.

## Android test

1. Build Development APK with the existing Android QA build command/menu.
2. Install on the physical Android device.
3. Launch with Firebase Emulator and Cloud Functions NOT running.
4. Confirm game reaches the menu without requiring a manual Continue press.
5. Open Settings -> Audio -> Mute All Audio -> Apply.
6. Confirm there is no visible portrait-resolution flash and Settings remains
   reachable after Apply.
7. Confirm UI fills more of the 2400x1080 screen than v5.25.3 while preserving
   safe margins/notch clearance.
8. Open PRIVATE TABLE while no online Atlas identity is ready.
9. Confirm the existing local lobby opens instead of an online room error.
10. Configure one LocalHuman plus one or more Bots and start the match.
11. Confirm the match begins with no Firebase emulator/Cloud Functions process.
12. Complete or exit a short local match and verify no permanent XP/win/wallet
    reward is presented as authenticated progression.
13. Use Share Logs and inspect the session file.

## Expected log markers

- `ATB_MOBILE_LANDSCAPE_LOCK orientation=LandscapeLeft`
- `ATB_MOBILE_CANVAS_FIT ... ref=1920x1160` on a device with no vertical inset
- `ATB_MOBILE_GRAPHICS OS_OWNS_RESOLUTION ... setresolution_repair=disabled`
- no `ATB_MOBILE_RESOLUTION_REPAIR`
- `ATB_SESSION_MODE mode=LocalOffline startup_nonblocking=true ...`
- `ATB_SESSION_MODE mode=LocalOffline source=PRIVATE_TABLE transport=local_only ...`
- `ATB_ANDROID_LOG_SHARE user_requested=true` after Share Logs

A transient OS orientation observation may produce
`ATB_MOBILE_TRANSIENT_PORTRAIT_OBSERVED ... action=none`; that marker is
informational only and must not be followed by a forced `Screen.SetResolution`.

## Fail conditions

- Unity compilation error;
- Settings Apply visibly changes to portrait or leaves the UI cropped;
- local PRIVATE TABLE attempts to call Cloud Functions before the match can
  start;
- authenticated/persistent XP, win, achievement or wallet reward is created for
  a Local Offline session;
- Android logging or landscape lock regresses.
