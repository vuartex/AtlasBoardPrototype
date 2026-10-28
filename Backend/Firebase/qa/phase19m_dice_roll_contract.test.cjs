'use strict';
// Source contract only. Unity player animation, Steam API and multi-client
// material results require an actual owner test.
const fs = require('fs'), path = require('path'), assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../../..');
const read = p => fs.readFileSync(path.join(root, p), 'utf8');
const visual = read('Assets/Project/Scripts/Board/DiceVisualController.cs');
const turn = read('Assets/Project/Scripts/Core/TurnManager.cs');
const offline = read('Assets/Project/Scripts/Online/AtlasBoardOfflineModeBootstrap.cs');
const econ = read('Assets/Project/Scripts/Meta/AtlasBoardRtdbMetaEconomy.cs');
const steam = read('Assets/Project/Scripts/Platform/AtlasBoardPlatformRuntime.cs');
let passes=0;
function verify(condition, label) { assert.ok(condition, label); passes++; }
function has(s,x) { verify(s.includes(x), `Missing ${x}`); }
has(offline, 'dice?.BeginLocalOfflineDiceSession();');
has(offline, 'offlineMainMenu.StartMatchAfterPrivateBackendAuthorization();');
verify(offline.indexOf('dice?.BeginLocalOfflineDiceSession();') < offline.indexOf('offlineMainMenu.StartMatchAfterPrivateBackendAuthorization();'), 'offline cosmetics reset must precede match');
has(visual, 'public void BeginLocalOfflineDiceSession()');
has(visual, 'ResetForNewMatchSession();');
has(visual, 'localOnlineSlotIndex = -1;');
has(visual, 'onlineDiceSession = false;');
has(visual, 'localDiceProjection = AtlasBoardRtdbMetaEconomy.HasCurrentDiceCache');
has(visual, 'AtlasBoardRtdbMetaEconomy.CurrentEquippedDiceId');
has(visual, 'bool botControlled = false');
has(visual, 'if (!botControlled && stableSlotIndex >= 0');
has(visual, 'if (!AtlasBoardRtdbMetaEconomy.IsDice(selected)) selected = "";');
has(visual, 'stableSlotIndex == localOnlineSlotIndex');
has(visual, 'slotDiceIds[stableSlotIndex]');
has(visual, 'member.IsHumanSeat');
has(visual, 'ReadPublicDiceAsync(member.AccountId)');
has(visual, 'ATB_DICE_SKIN_ROLL mode=');
has(visual, 'modelAvailable=');
has(turn, 'rollingBot != null && rollingBot.BotEnabled');
has(turn, 'rollingSeat.IsOnlineBotControlled');
has(turn, 'online.IsPreparedOnlineMatch');
has(turn, 'slotIndex,\n            botControlled');
has(turn, 'AuthoritativeDiceCommitted?.Invoke(');
has(turn, 'CompleteActiveRollAfterVisual(');
has(econ, 'ReadPublicDiceAsync');
has(econ, 'return profile != null && IsDice(profile.diceSkinId)');
has(econ, 'currentDiceUid == CurrentUid');
has(econ, 'return await MutateAsync("PURCHASE", itemId, false);');
has(steam, 'SteamLastError');
has(steam, 'IsSteamProviderActive');
verify(!offline.includes('firebase.cmd'), 'offline script must not deploy');
console.log(`ATB_PHASE19M_DICE_VISUAL_SOURCE_QA PASS checks=${passes} unityPlayMode=OPEN multiClient=OPEN steamInit=OPEN`);
