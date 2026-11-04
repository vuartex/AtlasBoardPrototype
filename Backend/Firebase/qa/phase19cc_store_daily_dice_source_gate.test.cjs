'use strict';
// Offline structural regression only. Does NOT constitute an emulator PASS.
const fs=require('node:fs');
const path=require('node:path');
const assert=require('node:assert/strict');
const root=path.resolve(__dirname,'../../..');
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const rules=JSON.parse(read('Backend/Firebase/database.rules.json')).rules;
const wallet=rules.users.$uid.metaEconomy;
const src=read('Assets/Project/Scripts/Meta/AtlasBoardRtdbMetaEconomy.cs');
const ui=read('Assets/Project/Scripts/UI/Progression/AtlasBoardProgressionUI.cs');
const cloud=read('Assets/Project/Scripts/Progression/AtlasBoardCareerDiceCloud.cs');
const storeUi=read('Assets/Project/Scripts/UI/Meta/AtlasBoardMetaEconomyUI.cs');
let n=0;const test=(x,msg)=>{assert.ok(x,msg);n++};
test(wallet['.validate'].includes("'wallet','inventory','equipped','dev','operation'"),'canonical required wallet fields remain');
test(!wallet['.validate'].includes("'equipped','dailyReward','dev'"),'optional dailyReward for unrelated actions');
test(wallet['.write'].includes("newData.child('operation/type').val() === 'PURCHASE'"),'purchase rule still present');
test(wallet['.write'].includes("newData.child('operation/type').val() === 'DAILY'"),'daily rule still present');
test(wallet['.write'].includes("newData.child('operation/type').val() === 'DEV_GRANT'"),'dev grant rule still present');
test(wallet['.write'].includes("root.child('devWalletTesters').child($uid).val() === true"),'dev tester permission unchanged');
test(wallet['.write'].includes("(!data.child('dailyReward/lastClaimAt').exists() || data.child('dailyReward/lastClaimAt').val() <= now - 86400000)"),'first-time daily timestamp may be absent');
test(wallet['.write'].split("newData.child('dailyReward/lastClaimAt').val() === data.child('dailyReward/lastClaimAt').val()").length-1===7,'seven unchanged daily guards for non-daily cases');
test(wallet['.write'].includes("newData.child('wallet/gold').val() === data.child('wallet/gold').val() + 50"),'exact daily Gold delta');
test(wallet['.write'].includes("newData.child('wallet/gold').val() === data.child('wallet/gold').val() + 5000"),'admin delta only exact');
test(src.includes('if (dailyFieldMissing && kind != "DAILY")') && src.includes('documentJson.Remove("dailyReward")'),'purchase preserves legacy absent daily field');
test(src.includes('dailyFieldMissing && kind == "DAILY"') && src.includes('ReadLastDailyReceiptAtMsAsync(identity'),'daily-only history recovery');
test(src.includes('HistoryPath(identity.Uid) + ".json"'),'immutable receipts queried for daily claim');
test(src.includes('initialJson["dailyReward"] = new JObject { ["lastClaimAt"] = 0L }'),'fresh wallet explicit zero initial timestamp');
test(src.includes('if (identity.IsAnonymous || kind == "DEV_GRANT"')||src.includes('(identity.IsAnonymous || kind == "DEV_GRANT"'),'normal registered purchase not allowlist gated');
test(src.includes('bool privileged = kind == "DEV_GRANT" || kind == "RESET_PURCHASES"'),'normal store errors not labelled dev failures');
const dice=rules.careerDiceObservations;
test(!!dice && !!dice.$uid && !!dice.$uid.$matchId,'dice path exists');
test(dice.$uid['.read'].includes('auth.uid === $uid'),'dice read own UID');
test(dice.$uid.$matchId['.write'].includes('!data.exists()'),'per match append-only');
test(dice.$uid.$matchId['.write'].includes('auth.uid === $uid'),'per match write own UID');
test(dice.$uid.$matchId['.write'].includes("root.child('careerClaims').child($uid).child($matchId)"),'claimed match context required');
test(dice.$uid.$matchId['.write'].includes("root.child('completedMatchArchives').child($matchId)"),'solo completed match context supported');
test(dice.$uid.$matchId['.write'].includes("newData.child('totalDiceValue').val() <= newData.child('diceRolls').val() * 12"),'physical roll upper bound');
test(dice.$uid.$matchId.$other['.validate']===false,'no forged extra dice fields');
test(cloud.includes('null_etag') && cloud.includes('UID-scoped'),'per match immutable client observations');
test(cloud.includes('TryGetVerifiedRegisteredRestPrincipal') && cloud.includes('liveUid != expected'),'registered REST account cloud principal scoped');
test(ui.includes('RequestCloudDiceSync()') && ui.includes('cloudDice.Uid == AtlasBoardRtdbMetaEconomy.CurrentUid'),'career UI UID cloud binding');
test(!cloud.includes('DevGold') && !cloud.includes('xpAwarded'),'dice observation cannot mint reward');
test(storeUi.includes('StoreAccountText("DAILY REWARD CLAIMED", "GÜNLÜK ÖDÜL ALINDI"'),
     'claimed daily button has explicit 7-language status');
test(storeUi.includes('DailyAwardAlreadyClaimed(snapshot)') &&
     storeUi.includes('bool canClick = eligible && !claimed'),
     'claimed daily reward cannot be clicked again');
test(storeUi.includes('preOperationButtonStates[button] = button.interactable') &&
     storeUi.includes('state.Key.interactable = state.Value'),
     'busy state restores original disabled/owned button states');
test(storeUi.includes('bool changed = HasMaterialStoreChange(snapshot, fresh)') &&
     storeUi.includes('if (changed && storeRoot != null && storeRoot.activeSelf && !busy)'),
     'no full store rebuild on unchanged wallet polling');
test(storeUi.includes('if (uid != lastLiveWalletUid)') &&
     storeUi.includes('snapshot = null;') && storeUi.includes('dailyClaimButton = null;'),
     'UID changes clear previous wallet and daily status');
test(storeUi.includes('if (!result.AlreadyClaimed && snapshot != null') &&
     storeUi.includes('snapshot.DailyClaimedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()'),
     'daily UI only locks optimistically after confirmed server success');
test(storeUi.includes('RebuildHistoryPage();') && storeUi.includes('HistoryDetail(item)'),
     'purchase history view and transaction details remain available');
// v5.49.12ce: existing Career inventory and dice-cloud regression gates.
const frameHeader=read('Assets/Project/Scripts/UI/Menu/AtlasBoardCanonicalProfileHeader.cs');
const recorder=read('Assets/Project/Scripts/Progression/AtlasBoardProgressionMatchRecorder.cs');
const coordinator=read('Assets/Project/Scripts/Online/AtlasBoardTurnDiceNetworkCoordinator.cs');
test(ui.includes('(inventoryAccount != null || restVerifiedInventory)') &&
     !ui.includes('restVerifiedInventory && inventoryCategory != "profile_frame"'),
     'registered REST Career profile-frame Apply not blocked by missing native account');
test(ui.includes('TryGetVerifiedRegisteredRestPrincipal(') &&
     ui.includes('inventoryServer != null && inventoryServer.Success && inventoryServer.WalletVerified'),
     'frame equip requires verified REST principal and actual canonical wallet');
test(ui.includes('await AtlasBoardRtdbMetaEconomy.EquipAsync(') &&
     ui.includes('inventoryServer.EquippedFrameId = isDefault ? "" : chosenId'),
     'frame equipped only following wallet mutation');
test(src.includes('PublishEquippedFrameAsync(itemId ?? "")') &&
     src.includes('var result = await MutateAsync("EQUIP"'),
     'existing ETag-guarded EQUIP writes frame public projection after server success');
test(src.includes('Response existing = await RequestAsync(identity, "GET", path, null, null)') &&
     src.includes('profile.ToString(Newtonsoft.Json.Formatting.None), "null_etag"') &&
     src.includes('Response retry = await RequestAsync(identity, "PATCH", path,'),
     'public profile is bootstrapped only if absent using conditional create');
test(src.includes('"/users/" + UnityWebRequest.EscapeURL(identity.Uid) +') &&
     src.includes('"/displayName.json"') &&
     src.includes('if (nameToken.Type != JTokenType.String) return false;'),
     'public profile bootstrap uses owner RTDB display name, not a new identity');
test(rules.publicProfiles.$uid.profileFrameId['.validate'].includes('metaEconomy/equipped/profileFrameId') &&
     rules.publicProfiles.$uid.profileFrameId['.validate'].includes('metaEconomy/inventory/profile_frames'),
     'existing RTDB rules prevent unowned public frame projection');
test(frameHeader.includes('UpdateRestProfileFrame()') &&
     frameHeader.includes('revision != restFrameRevision') &&
     frameHeader.includes('current.WalletVerified'),
     'live REST avatar frame follows current verified UID without stale async projection');
test(coordinator.includes('public int[] careerDiceTotals = new int[4]') &&
     coordinator.includes('public int[] careerDiceHighest = new int[4]'),
     'host frames carry sum/high for all seats');
test(coordinator.includes('careerDiceTotals = AtlasBoardProgressionMatchRecorder.HostDiceValues(false)') &&
     coordinator.includes('careerDiceHighest = AtlasBoardProgressionMatchRecorder.HostDiceValues(true)'),
     'host sources exact observed dice values');
test(coordinator.includes('frame.careerDiceTotals, frame.careerDiceHighest') &&
     recorder.includes('HydrateCareerDiceCounts(int[] rolls, int[] doubles,') &&
     recorder.includes('int[] sums = null, int[] highest = null'),
     'followers restore dice sums/high from host without breaking old snapshots');
test(recorder.includes('if (count == 0 || sum < count * 2 || sum > count * 12') &&
     recorder.includes('sum < high + (count - 1) * 2 || sum > high * count'),
     'invalid or incomplete host dice data is not manufactured');
test(recorder.includes('"career_and_gold_saved"') &&
     recorder.includes('AtlasBoardCareerDiceCloud.SynchronizeAsync('),
     'server-finalized result proactively queues the existing Firebase observation upload');
test(cloud.includes('RefreshRegisteredRestTokenForCurrentUidAsync(principal.Uid)') &&
     cloud.includes('bool allowRefresh = true') &&
     cloud.includes('allowRefresh && principal.RegisteredRest'),
     'same REST UID has bounded token refresh for dice observation retry');
test(cloud.includes('recordedAt') && cloud.includes('"null_etag"') &&
     !cloud.includes('goldAwarded'),
     'dice cloud remains append-only observational data independent of Gold');
// v5.49.12cf: reuse the canonical local lobby and frame presenter.
const privateLobby=read('Assets/Project/Scripts/UI/Menu/AtlasBoardPrivateLobbyUIController.cs');
const offline=read('Assets/Project/Scripts/Online/AtlasBoardOfflineModeBootstrap.cs');
const resultUi=read('Assets/Project/Scripts/Core/MatchResultManager.cs');
test(privateLobby.includes('AtlasBoardCanonicalPlayerIdentity.ValidSteamPersona()') &&
     privateLobby.includes('AtlasBoardCanonicalPlayerIdentity.CurrentDisplayName'),
     'local host seat uses existing Steam/canonical display name, not PLAYER fallback');
test(privateLobby.includes('backendSnapshot == null && string.IsNullOrEmpty(roomCode)') &&
     privateLobby.includes('hostMember.DisplayName'),
     'local-only name fallback cannot override a backend online lobby identity');
test(resultUi.includes('AtlasBoardOfflineModeBootstrap.TryReturnLocalMatchToLobby()') &&
     resultUi.includes('SceneManager.LoadScene('),
     'offline REMATCH reuses local lobby before unchanged legacy fallback');
test(offline.includes('ReadPrivateField<bool>(owner.offlinePrivateLobby, "roomActive")') &&
     offline.includes('ReadPrivateField<bool>(owner.offlinePrivateLobby, "localIsHost")') &&
     offline.includes('!string.IsNullOrWhiteSpace(ReadPrivateField<string>('),
     'local rematch requires an active owner-only offline room without online code');
test(offline.includes('online.IsPreparedOnlineMatch') &&
     offline.includes('owner.offlineMainMenu.ReturnOnlineMatchToLobby();'),
     'online rematch untouched and existing gameplay reset used');
test(offline.includes('owner.offlineStartButton.interactable = true;') &&
     offline.includes('owner.offlineStartButton.onClick.AddListener(owner.BeginOfflineStartCountdown);') &&
     offline.includes('InvokeNoArgs(owner.offlinePrivateLobby, "RefreshHostSeatRows")'),
     'offline lobby start/countdown restored and local name repainted after rematch');
test(frameHeader.includes('LoadRestProfileFrameAsync(currentAccountId, restFrameRevision)') &&
     frameHeader.includes('Time.unscaledTime + 20f'),
     'native Steam frame periodically reconciles with canonical wallet without new writer');
test(frameHeader.includes('restFrameRevision++;') &&
     frameHeader.includes('revision != restFrameRevision') &&
     frameHeader.includes('current.WalletVerified'),
     'native and REST frame reads remain UID-scoped and reject stale wallet snapshots');

console.log(`ATB_PHASE19CF_MENU_FRAME_REMATCH_DICE_SOURCE PASS total_checks=${n} firebase_emulator=NOT_RUN unity_compile=NOT_RUN`);
console.log(`ATB_PHASE19CC_STORE_DAILY_DICE_SOURCE PASS checks=${n} unity_compile=NOT_RUN emulator=NOT_RUN production=NOT_TOUCHED`);
