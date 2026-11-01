# AtlasBoard MASTER_ROADMAP Update Block — 2026-11-01

> Append this block to the single canonical `Assets/Project/Documentation/MASTER_ROADMAP.md`.
> Preserve all existing roadmap history. Do not create a second active roadmap.

---

## v5.49.12ae — 2026-11-01 Owner Retest / Host Identity + Match Gold + Career Details Handoff

### Authority / source checkpoint

- Unity: `6000.5.6f1`
- Repository: `vuartex/AtlasBoardPrototype`
- Branch: `main`
- Live GitHub `main` HEAD rechecked on 2026-11-01: `0bc2679695e9b25e2c75130d7ccc8a9d90fe7f67`
- HEAD title: `docs: v5.49.12t Phase 19AF/19AG handoff, Career XP backlog and Unity compiler issue`
- Local project root: `C:\Users\crazy\AtlasBoardPrototype`
- Main scene: `Assets/Board_Prototype.unity`
- Windows Guest build path: `C:\Users\crazy\AtlasBoard_3D3C_GuestBuild`
- Firebase project: `atlasboard-usa`
- RTDB region: `europe-west1`
- Firebase working directory: `C:\Users\crazy\AtlasBoardPrototype\Backend\Firebase`
- Latest prior-chat owner-tested candidate is newer than the GitHub checkpoint; prior-chat package/version was tracked as `v5.49.12ad`. Treat local installed source as potentially newer than GitHub. Compare before editing; do not overwrite newer accepted local work with older remote files.

### Mandatory delivery contract — unchanged and strengthened

1. Every implementation delivery must be one downloadable ZIP containing COMPLETE changed files at their real project-relative paths.
2. No `.ps1`, `.patch`, `.diff`, installer, source-rewrite helper, partial code fragment, or search/replace script.
3. Do not give PowerShell commands for installing source changes. Owner extracts the ZIP manually over `C:\Users\crazy\AtlasBoardPrototype`.
4. PowerShell is allowed only when Firebase rules/backend deployment is actually required. In that case every command must use the complete local path, beginning from:
   `Set-Location "C:\Users\crazy\AtlasBoardPrototype\Backend\Firebase"`
   and must use `firebase.cmd`, not a bare `firebase`.
5. Do not ask again for project paths, Guest build paths, repository name, main scene, Firebase project, or Firebase working directory.
6. Include the complete updated canonical `Assets/Project/Documentation/MASTER_ROADMAP.md` in every source ZIP.
7. No GitHub push or production Firebase deploy unless owner explicitly authorizes it.
8. Static/source QA, Unity compile, Editor Play Mode, two-client runtime, Firebase Emulator, Windows build and Android build are separate gates. Never call an unrun gate PASS.
9. Read the whole current roadmap and inspect the actual current source before editing. Do not create duplicate managers/services or resurrect obsolete implementations.

### P0 — Host migration / seat identity corruption still FAILING

**Owner retest on 2026-11-01:** latest rebuilt version still fails.

Observed behavior:
- Guest identity itself is not the problem.
- In the Unity instance, after host ownership changes, the local canonical player can appear as `Vuartex` in more than one seat.
- Owner has observed `Vuartex` in both the first and second slot.
- The Unity client appears to re-project itself as though it still owns slot 0 / the original local-host slot.
- This can overwrite or visually replace another player's name.
- Host location is NOT required to be slot 0. After host migration, whichever surviving Human became authoritative must remain in its actual authoritative seat.
- A former slot-2/slot-3/slot-4 player that becomes Host must display Host authority in that same seat. It must not be relocated to slot 0 merely because it is now Host.
- Other players' identities must never be rewritten to the local account display name.

Required engineering approach:
- Do not make another superficial HUD-name patch.
- Trace the complete identity/seat ownership pipeline end-to-end:
  authenticated UID/account -> lobby roster -> `SeatId`/`slotIndex` -> match roster/snapshot -> host migration / authority epoch -> local-player resolver -> HUD/player card projection -> rematch/re-entry reset.
- Inspect every write and fallback that can assign local canonical display name or infer "local player = slot 0".
- Search for slot-0 assumptions, host-index assumptions, local-host aliases, stale cached seat indices, fallback display-name writes, rematch/session reset projections and host-transfer reconciliation.
- `Host` is an authority role, not a seat identity.
- Canonical seat ownership must remain stable unless the authoritative roster itself changes.
- A host migration may change `hostUid` / authority epoch, but must not mutate unrelated seat UID, account ID, display name, pawn ownership, money, inventory or result ownership.
- Fix at the earliest canonical source of incorrect identity ownership. Do not paper over the final UI.
- Add source/contract tests specifically for:
  - local user originally slot 0 -> follower after migration;
  - local user originally slot 1/2/3 -> becomes Host and stays in same seat;
  - Guest Host -> Unity Vuartex successor;
  - Unity Vuartex Host -> Guest successor;
  - multiple migrations;
  - rematch after migration;
  - menu exit/new room after migration;
  - no duplicate UID/display name across Human seats;
  - bots unaffected.
- Owner runtime acceptance requires two real clients and must explicitly verify all visible player slots after takeover, not merely host authority logs.

**Status:** OPEN / OWNER FAIL. Do not claim root cause solved until the owner retests the fixed ZIP.

### P0 — Match completion must grant Gold as well as XP

Owner requirement updated 2026-11-01:
- Completed eligible online matches must grant Gold in addition to the existing Career XP award.
- **Maximum permanent Gold award for one match is 50 Gold.**
- No eligible player may receive more than 50 Gold from one completed match.
- Do not invent an independent wallet or currency path.
- Use the existing RTDB account/meta-economy wallet and the same trusted/idempotent match-completion evidence used by Career progression.
- Never award permanent Gold from offline, guest-only, one-human-plus-bots casual/untrusted matches unless the roadmap's trusted eligibility rules explicitly allow that mode later.
- Never trust a host-only client claim for paid/protected currency.
- Gold reward must be idempotent per `(matchId, uid)` and must survive retry/reconnect without duplicate minting.
- Gold receipt/audit data must be tied to the same canonical match result proof and account UID.
- Preserve Atlas Coin separation; do not substitute or mix Atlas Coin and Gold.
- The exact distribution formula below the 50-Gold ceiling must be derived from current game/result design and existing reward architecture during implementation. Do not silently invent a formula in documentation and call it owner-approved.

Required QA:
- eligible win/tie/loss cases;
- 0 < reward <= 50 where applicable and never >50;
- duplicate/replay claim;
- reconnect/retry;
- second/third consecutive match;
- account switch;
- host migration before match end;
- no reward for ineligible/untrusted match;
- wallet/history reflects one correct Gold transaction;
- XP and Gold both correspond to the same canonical completed match.

**Status:** P0 OPEN. XP path exists in the current Career work; Gold match reward is not owner-accepted yet.

### P1 — Career > Match History > Details > Players section is empty

Owner report:
- Career Match History entry exists and Details opens the match/lobby-style details view.
- General match details appear correct.
- The bottom `Players` area is empty.

Required result:
- Populate the `Players` section from the canonical completed-match/history record.
- Show every player that participated in that completed match as separate cards.
- Cards must reflect end-of-match values from the immutable/trusted match result/history record, not current live lobby state and not the viewer's current account values.
- At minimum preserve/display the identity and final result information already available in the canonical completed-match record, including final money/economy/result fields where recorded.
- The screen must remain readable after room cleanup, app restart and account reload.
- Host migration must not change which historical seat/player owns a result.
- Do not reopen a live lobby or depend on live `roomMembers` to render historical players after the match is complete.
- Account switching must not leak another account's private history.

**Priority:** fix only after the P0 host/seat identity bug and P0 Gold reward implementation are stable. Additional owner-reported bugs will be added later.

### Execution order for the next chat

1. Read entire canonical roadmap.
2. Verify GitHub `main` and compare it with the locally installed prior-chat candidate before changing anything.
3. Deep-read all code involved in identity, roster, seat, host migration, local-player resolution and player HUD projection.
4. Fix the duplicate `Vuartex` / slot ownership corruption at its canonical source.
5. Deliver full-file ZIP; owner performs Unity two-client retest.
6. Then implement secure/idempotent match Gold reward with 50 Gold absolute per-match cap, integrated with existing Career/RTDB result proof and wallet.
7. Deliver full-file ZIP; owner retests XP + Gold.
8. Then fix Match History Details -> Players cards.
9. Continue with the additional bugs the owner reports afterward.
10. Preserve all previously owner-accepted shop, dice, pawn, emote, premium map, Career and multiplayer behavior unless a regression is directly implicated.

### Handoff truth

This roadmap update records owner-observed failures and new requirements only. It does not claim that the duplicate-seat identity bug, Gold reward, or Match History Players panel has been fixed or runtime-tested.
