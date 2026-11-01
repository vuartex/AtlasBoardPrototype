'use strict';
const fs=require('fs'), path=require('path'), assert=require('assert');
const root=path.resolve(__dirname,'../../..');
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
let checks=0; const has=(src,needle,msg)=>{assert(src.includes(needle),msg);checks++;};
const not=(src,needle,msg)=>{assert(!src.includes(needle),msg);checks++;};

const loading=read('Assets/Project/Scripts/UI/Loading/AtlasBoardLoadingExperience.cs');
const menu=read('Assets/Project/Scripts/UI/Menu/AtlasBoardMainMenuController.cs');
const priv=read('Assets/Project/Scripts/UI/Menu/AtlasBoardPrivateLobbyUIController.cs');
const pub=read('Assets/Project/Scripts/UI/Menu/AtlasBoardPublicLobbyBrowserController.cs');
const auction=read('Assets/Project/Scripts/UI/AtlasBoardAuctionPresentationPolish.cs');
const emote=read('Assets/Project/Scripts/Meta/AtlasBoardEmoteNetwork.cs');
const hud=read('Assets/Project/Scripts/UI/AtlasBoardEmoteHudRuntime.cs');
const economy=read('Assets/Project/Scripts/Meta/AtlasBoardRtdbMetaEconomy.cs');
const progression=read('Assets/Project/Scripts/Progression/AtlasBoardProgressionBridge.cs');
const recorder=read('Assets/Project/Scripts/Progression/AtlasBoardProgressionMatchRecorder.cs');
const career=read('Assets/Project/Scripts/Platform/AtlasBoardRtdbCareerReadModel.cs');

has(loading,'MenuReset = 3','menu soft reset mode missing');
has(loading,'BeginMenuReturnReset()','menu reset entry missing');
has(menu,'MainMenuReturnRoutine','main menu transition gate missing');
has(menu,'InvalidatePendingRoomOperations','private navigation invalidation missing');
has(pub,'InvalidateForMainMenuReturn','public navigation invalidation missing');
has(pub,'mainMenuController.ShowMainMenu()','public back must use central reset gate');
has(priv,'stale_create_discarded=true','stale private create guard missing');
has(priv,'stale_join_discarded=true','stale private join guard missing');
has(pub,'stale_create_discarded=true','stale public create guard missing');
has(pub,'stale_join_discarded=true','stale public join guard missing');

has(auction,'SuppressLegacyGraphics()','legacy auction visual suppression missing');
has(auction,'legacy_graphics_suppressed=true','modern-only auction diagnostic missing');
has(auction,'auctionPanel.transform as RectTransform','modern auction must use authoritative Panel_Auction lifetime');
has(auction,'graphic.enabled = false','legacy auction graphics must be invisible');
has(auction,'graphic.raycastTarget = false','legacy auction must not intercept modern input');

has(emote,'ATB_EMOTE_GUEST_FREE path=direct_server_validated','Guest free emote direct path missing');
has(hud,'ATB_EMOTE_UI_SEND_FAILED reason=','emote failure reason diagnostic missing');

has(economy,'Ordinary wallet refresh is strictly read-only','wallet refresh receipt replay guard missing');
const receiptCalls=(economy.match(/TryWriteReceiptAsync\(identity,/g)||[]).length;
assert.strictEqual(receiptCalls,2,'receipt writes should remain only at real mutation commit sites'); checks++;

has(progression,'local_steam_provisional_progress_no_spendable_gold','Steam provisional Career state missing');
has(progression,'verified.TotalXp = observed.TotalXp','empty server profile must preserve local provisional XP');
has(recorder,'SaveLocallyObservedMatch(matchId, false);\n            RecordLiveRtdbMatchAsync(matchId);','Steam observation must persist before remote claim');
has(career,'FindAnyObjectByType<AtlasBoardProgressionBridge>()','Career refresh must use merged canonical bridge');

console.log(`ATB_PHASE19AQ_SOURCE_GUARD PASS checks=${checks} LIVE_UNITY=NOT_RUN TWO_CLIENT=NOT_RUN`);
