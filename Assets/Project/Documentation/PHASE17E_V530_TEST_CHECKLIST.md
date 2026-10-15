# AtlasBoard v5.30.0 — One-build acceptance checklist

## Before building APK
- [ ] Extract ZIP into project root and overwrite files.
- [ ] Unity finishes compilation with ZERO red errors.
- [ ] If any red error exists: STOP; do not spend time building APK.
- [ ] Deploy RTDB rules if account mirror will be tested:
  `cd Backend/Firebase`
  `firebase.cmd deploy --only database --project atlasboard-usa`

## One Android APK test
- [ ] Shop orange; Career violet/indigo.
- [ ] Guest name format is `Guest#####X` and survives app restart.
- [ ] Career while unsigned opens Sign In instead of hanging.
- [ ] Settings exposes Sign In / account access.
- [ ] Register: email + password + repeat + easy math question.
- [ ] Wrong math/password mismatch do not submit.
- [ ] Submit shows loading and cannot be spam-clicked.
- [ ] Valid account signs in; duplicate email is rejected.
- [ ] RTDB `/users/{uid}` contains userId/playerId/email/general/platform/device; contains NO password.
- [ ] Relaunch app: remains signed in and same name appears everywhere.
- [ ] Password reset email path works.
- [ ] Name rename shows 24h cooldown after a successful change.
- [ ] PRIVATE TABLE uses `+ -> Local Player / Bot` modern seats.
- [ ] Player Count 4 + one extra Local + one explicit Bot + one unresolved -> unresolved becomes Bot only.
- [ ] Start displays 5 -> 1 countdown.
- [ ] Roll button position good; Trade button does not overlap P2 HUD.
- [ ] Trade/tablet text and touch fields readable on phone.
- [ ] One-finger rotate, two-finger pinch zoom, two-finger pan work.
- [ ] City info opens and vertical touch scroll works.
- [ ] Exit match and start match #2 and #3: modern tablet and City Info still work.
- [ ] FPS measured for at least 60 seconds; Share Logs afterward.

## Send back
- Share Logs file.
- Screenshot of RTDB `/users/{uid}` with email blurred if desired.
- Screenshot only for any visual failure.
