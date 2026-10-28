# Phase 19AC — Germany / Mexico Lobby dropdown hotfix

Do not repurchase Germany. The Store **Owned** state is separate from the old three-choice lobby dropdown.

## Install
1. Exit Unity Play Mode and extract this ZIP over `C:\Users\crazy\AtlasBoardPrototype`.
2. Keep your locally saved `Assets/Board_Prototype.unity`; this ZIP deliberately does not replace it.
3. Open Unity, wait for compile, enter Game Lobby → Map. Runtime self-registration now requires no manual Editor installer.
4. Expect console `ATB_MAP_REGISTRY status=ready maps=5 germany=True mexico=True` and `ATB_MAP_CHOICES status=ready count=5`.
5. Optional for serialized scene persistence: Atlas Board → QA & Diagnostics → Install Germany and Mexico Maps; Ctrl+S.

## Test (not yet run in Unity)
- Turkey / Colorado / USA remain indices 0/1/2, Germany 3, Mexico 4.
- Germany is owned for this UID and selectable. Mexico is visible but unowned; selecting it reverts to the previous map. Must NOT be playable until purchase.
- Changing Map in Lobby should preview country cities in board background, then start a match with that map.
- Guest joining should not need to own host map. New room must start from default map and not inherit old host map.
- Match ongoing host handover cannot change active map.
- Seven locale labels and multi-client + Android tests remain OPEN.
- Firebase rules unmodified; DO NOT redeploy because of this hotfix.
