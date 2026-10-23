# AtlasBoard v5.40.22 — N4 Final Three-Match QA & Spark RTDB Casual Chat

**Status:** Source candidate / Unity compile **UNTESTED** / RTDB chat Rules deploy **PENDING** / live Editor+Windows Guest **PENDING**. No GitHub push, no Firebase deploy performed by assistant.

## Why this is one patch

- Existing `AtlasBoardChatRuntimeBridge.cs` no longer sends casual chat to paid `europe-west1` Cloud Functions `lobbyChatList/Send`, `matchChatList/Send`. It now uses Spark-compatible Auth + RTDB REST under `chatMessages/{lobby|match}/{scopeId}/{messageId}`.
- `Backend/Firebase/database.rules.json` adds scoped room-member read/write, immutable messages, server-generated timestamps, 1–120 characters, sender/account/name matching, and a server-side single-use 10-second token under `chatThrottle/{kind}/{scopeId}/{uid}`. Existing Rules are preserved from v5.40.17, whose file size matched GitHub main's source at this patch checkpoint. No root/global read/write widening.
- **Important security limitation:** This is a *casual-chat QA candidate*. The existing `AtlasBoardChatModerationBridge` and safety/reporting commands still depend on old Cloud Functions and are NOT migrated. RTDB Rules do not provide equivalent server-side multilingual profanity moderation, retention, appeal/report enforcement or trusted ban enforcement. Until replaced/accepted, release chat safety is **OPEN**. Do not enable rewarded/ranked/production chat based solely on this transport.
- Adds one Editor-only **acceptance orchestrator** `AtlasBoardN4ThreeMatchAcceptanceWindow.cs`, reusing existing `AtlasBoardGameplayRegressionWindow`, `AtlasBoardGameplayRegressionWindowV2`, `AtlasBoardPhase14JRuntimeHarness` and scene systems. No second gameplay manager/authoritative simulation.
- Only `Assets/Project/Documentation/MASTER_ROADMAP.md` remains the canonical roadmap. This guide is a test instruction document, not a second roadmap.

## 0. Installation

1. Back up local project, close Unity Play Mode and extract ZIP to `C:\Users\crazy\AtlasBoardPrototype` preserving full paths.
2. Confirm new `database.rules.json` contains BOTH existing permission subtrees and `chatThrottle`, `chatMessages`. Confirm no local Rules customizations will be overwritten. Back up the current live rules before deploy.
3. Unity 6000.5.6f1 compile: require 0 red errors; run **Atlas Board → QA & Diagnostics → N4 Final - Chat Wire (No Network)**, expect `ATB_N4_CHAT_WIRE_QA PASS mode=local_no_network live_chat=UNTESTED`.
4. Chat is a new RTDB rules branch and WILL NOT work against Live Spark until Rules deployment is successful. **Owner-controlled deploy only**:

```powershell
cd "C:\Users\crazy\AtlasBoardPrototype\Backend\Firebase"
Get-Content .\database.rules.json -Raw | ConvertFrom-Json | Out-Null
firebase.cmd deploy --only database --project atlasboard-usa
```

No Cloud Functions, Blaze, or Firestore Rules deploy is requested. This does not constitute emulator/security QA PASS.

## 1. Editor QA windows

- **Atlas Board → QA & Diagnostics → N4 Final - Three Match Acceptance**. The window saves *manual* PASS/FAIL/N/A per Match 1/2/3 to EditorPrefs. Report Export creates `AtlasBoard_QA_Reports/N4_ThreeMatch_...md` in the project. UI preview clicks do not mark gameplay PASS.
- `Regression v1` / `Focused v2` open the original full and focused regression windows: use them for authoritative scenario definitions and notes.
- `Phase14J UI / F12` opens original seven-language/aspect presentation harness **only in OFFLINE Play**. Original Phase14J Preview code calls ResetForNewMatchSession and must NEVER run in a Live Spark match.
- `Chat UI toggle` and `Trade (normal UI)` target existing UI paths; only legitimate in-game host/remote decisions count as PASS.
- `Event UI preview`, `Special/Tax UI preview`, `Auction UI preview`, `Result UI preview` only toggle existing UI GameObjects; no money, winner or Event effect is fabricated. `Close UI previews` restores visibility. They check layout/renderability only.
- `FREEZE` uses Editor pause and is offline-only; never use it to freeze the live Host. `Random Event` executes a real card **only on Offline** game state after an explicit confirmation; it is not a substitute for two-client Event sync.
- Guest Windows EXE has no Editor QA menus. Guest actions must be performed with normal game controls and cross-checked from Unity Editor logs/observations.

## 2. One full three-match series

Use the new panel to record 35 observations for each match (not all categories apply; N/A only with reason).

**Match 1: Unity host + separate Guest.** Public/Browser creation + Guest join READY; verify lobby bidirectional text chat; match start, dice, purchases, tax, bonus, Event card, auction, trade, development if available; check bidirectional match chat. Real chat test: 1..120, empty, 121 characters, 10-second resend denied, different sender UID, Unicode and timestamp; monitor `ATB_RTDB_CHAT_SEND success=true` on successful send and both clients actually receiving the message. Guest has no QA menu.

**Match 2: Guest host + Unity follower.** Private create / join / READY. Run reverse-direction purchase/trade, Human/Bot auction, rent, Event/Travel/Vacation/Rest, 7-language UI and surface previews *offline separately*; validate only one host and same money/property/turn state. Play through an actual result and verify Guest Leave vs Host rematch behavior. Keep Editor QA read-only inspect if needed.

**Match 3: Menu exit → fresh lobby → new match without restarting Unity or Guest.** Repeat chat, HUD, tablet, ownership/dice and pawn cosmetics. Validate zero leaked money, property development, result modal, event modal, auction/trade state and player name/turn badge from matches 1–2. Exercise Guest Leave, temporary bot, reconnect, safe Host Migration and match finish where applicable. A live 3-hop authority test needs three successive distinct eligible humans; do not claim 3-hop from one epoch increment across separate matches.

At each boundary compare actual `matchId`, UID, room, revision/epoch (sanitized); watch for stale frame warnings, unexpected red errors or frozen modal. A `mode=local_no_network` result cannot stand in for two-client gameplay. Do not force result or currency mutations in Live RTDB using the QA panel.

## 3. Existing N4 scope / remaining gates

Prior owner evidence includes 14/14 legacy orphan room cleanup, new Public match, real first Host A→B handoff + rejoin, and separately observed Guest→Editor takeover with epoch 1→2 and resumed play. The latter is not proof of epoch 3 in the **same** match. Old-host stale-write denial, combined two-hop/three-human handoff, inactive standalone match orphan cleanup, full Private/Browser create and live chat/safety remain open until user evidence arrives.

After passing the 3-game gate with zero critical blockers, proceed to Phase 17 Android FPS profiling and optimization (especially historical S20 FE ~16–23 FPS). Phase 18 gameplay expansion stays PLANNED, not implemented by this patch.
