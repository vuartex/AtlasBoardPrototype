# Phase 19AB — Germany / Mexico Map source candidate

This package includes full modified source files at original Unity paths and canonical roadmap.

1. Extract ZIP into `C:\Users\crazy\AtlasBoardPrototype` after making a backup, merge/replace exact paths.
2. Exit Unity Play Mode, wait for compile, open `Assets/Board_Prototype.unity`.
3. Run menu **Atlas Board → QA & Diagnostics → Install Germany and Mexico Maps**. Inspector should show 5 map assets in the original MatchSetupManager. Save scene **Ctrl+S**.
4. Shop → Maps: Germany 30,000 Gold, Mexico 25,000 Gold; View uses the existing Store modal. Buy may work only when this new RTDB rule version has been emulator-tested and deployed and the UID has sufficient balance.
5. **DO NOT DEPLOY** the rules merely based on the static parse test. First emulator security tests and `firebase.cmd database:...` validation, then intentional deployment.
6. Verify host-only owned map selection, seven language dropdown/City Info strings, 32-tile board, 24 cities, three off-board Civics, Pulse, host migration, rematch, and two clients. Remote joiners do not need ownership.
7. Prior default maps and economy were preserved; not rebuilt. Unity compilation and actual gameplay are **NOT** tested by the assistant.

The EconomyProfile_Default is intentionally reused; new country-specific price/rent/development numbers come from the serialized tile definitions. Civic prices and effects reuse the existing host-authoritative Civic manager. Map data/City Info represent fictional gameplay balance rather than official statistics.
