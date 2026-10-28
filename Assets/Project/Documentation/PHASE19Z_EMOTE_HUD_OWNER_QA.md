# AtlasBoard v5.49.12o — Phase 19Z — Emote HUD Click Fix

## Owner-verified baseline (v5.49.12n)

- Emote purchase succeeds and appears in Career Inventory.
- Emote loadout accepts six active items and refuses a seventh.
- The in-match player HUD was not opening the picker. This is the only fix scope.

## Root cause and change

- `PlayerHudPanel` already implemented `IPointerClickHandler`, but `EnsureRuntimeLayout()` explicitly set the HUD's root background Image `raycastTarget=false` at runtime. The HUD labels, icon, and turn badge do not catch UI rays either, so uGUI could not deliver the click event to its handler.
- The HUD's original root Image is the click target again; no duplicate HUD, button, global input listener, or overlay manager was created.
- The picker now appears immediately from cached account selections, rather than appearing only after a remote load request finishes. After a successful asynchronous refresh, it updates only if the same picker/UID/player is still open.
- Sending, ownership validation, 6 active emotes, 3-second cooldown, 20-send threshold and 3-minute mute stay unchanged. No Firebase rule or database migration needed.

## Unity manual acceptance (NOT RUN by assistant)

1. Install ZIP into `C:\Users\crazy\AtlasBoardPrototype` while Unity is **not** in Play Mode; do **not** run an Editor builder. Recompile, verify Unity Console has no compiler errors.
2. In Career → Inventory → Emotes, keep six owned/free Emotes active. Start a Private (online) match.
3. Left-click **your own** P1 HUD in the match, including its empty background area. Expect a small six-emote picker at your HUD's upper side (for a lower HUD). It should open promptly. Click outside/close and reopen repeatedly.
4. Console must show `ATB_EMOTE_HUD_CLICK slot=0`, followed by `ATB_EMOTE_PICKER status=open slot=0 count=6`. If only the first appears, inspect `ATB_EMOTE_PICKER status=blocked reason=...` for ownership/state; if neither appears, check EventSystem and raycast blocking by a higher canvas.
5. Choose a free or purchased Emote. Expect `ATB_EMOTE_SENT mode=online ...` and a bubble near the selected player's HUD for about 2.5 s. Test rapid double-click: second send must be refused for at least 3 s.
6. Clicking a bot or another player's HUD must not enable a send. Verify upper/lower HUD anchor positions. A different online client should see the emote on the same player's HUD; two-client and 20-use/3-min server throttle **remain OPEN until tested**.
7. End match, return to Main Menu, start a second and third match; verify click/popup works without duplicate UI.

## Test classification

- `Backend/Firebase/qa/phase19z_emote_hud_entry.test.cjs`: static source contract test only.
- Previous v5.49.12n Emotes source/economy contract: existing static test.
- Actual Unity compilation, GUI click, network sending, server emulator and two-device multiplayer are NOT asserted PASS.
- Existing Pawn session-switch regression, Career data pipeline, Germany/Mexico map playability and third-party release-license audit remain separately OPEN.
