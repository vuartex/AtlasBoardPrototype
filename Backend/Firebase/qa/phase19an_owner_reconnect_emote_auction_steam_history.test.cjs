'use strict';
// SOURCE CONTRACT ONLY. Not an emulator, build or real client test.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const base = path.resolve(__dirname, '../../..');
const read = name => fs.readFileSync(path.join(base, name), 'utf8');
let checks=0; const ok=(condition,message)=>{assert.ok(condition,message);checks++};
const files={
 auction:read('Assets/Project/Scripts/UI/AtlasBoardAuctionPresentationPolish.cs'),
 transport:read('Assets/Project/Scripts/Online/Firebase/AtlasBoardRtdbCasualLobbyTransport.cs'),
 coordinator:read('Assets/Project/Scripts/Online/AtlasBoardTurnDiceNetworkCoordinator.cs'),
 emote:read('Assets/Project/Scripts/Meta/AtlasBoardEmoteNetwork.cs'),
 loadout:read('Assets/Project/Scripts/Meta/AtlasBoardEmoteLoadoutService.cs'),
 bridge:read('Assets/Project/Scripts/Progression/AtlasBoardProgressionBridge.cs'),
 recorder:read('Assets/Project/Scripts/Progression/AtlasBoardProgressionMatchRecorder.cs'),
 main:read('Assets/Project/Scripts/UI/Menu/AtlasBoardMainMenuController.cs')
};
ok(files.auction.includes('UpdateModernViewport()'), 'modern auction sizes to canvas viewport');
ok(files.auction.includes('canvasRect.rect.size'), 'real canvas bounds used');
ok(files.auction.includes('polishRoot.anchorMin = new Vector2(0.5f, 0.5f)'), 'no shrink to content anchors');
ok(files.auction.includes('polishRoot.sizeDelta = new Vector2'), 'explicit width and height');
ok(files.auction.includes('HideLegacyPresentation()'), 'new auction binds original auction data');
ok(files.auction.includes('auctionManager.CurrentBid'), 'live current bid is consumed');
ok(files.auction.includes('auctionManager.NextSmallBidAmount'), 'live next bid consumed');
ok(files.auction.includes('MirrorButtonState()'), 'legacy button interactability mirrored only');
ok(files.transport.includes('PumpActiveMatchRejoinsAsync'), 'independent active-host pump');
ok(files.transport.includes('RestorePendingMatchRejoinsOnly(room, hostUid, hostToken, snap)'), 'reuses strictly existing seat logic');
ok(files.transport.includes('seat.SeatMode == AtlasLobbySeatMode.RemoteHuman'), 'not allowed to take bots/host/open seat');
ok(files.transport.includes('member.joinedAt <= 0'), 'existing immutable member only');
ok(files.coordinator.includes('nextActiveMatchJoinPumpAt'), 'host periodic pump');
ok(files.coordinator.includes('activeMatchJoinPumpInFlight'), 'no concurrent pumping');
ok(files.coordinator.includes('lobbyBridge.AuthTokenForOnlineSubsystems'), 'uses lobby host token');
ok(files.loadout.includes('bridge.AuthTokenForOnlineSubsystems'), 'emote loadout matches authenticated lobby');
ok(files.loadout.includes('AtlasBoardEmotes.IsPaid(s)'), 'free Guest emotes may be initialized');
ok(files.loadout.includes('AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession'), 'does not use cached native Guest identity');
ok(files.loadout.includes('ATB_EMOTE_LOADOUT status=seed_pending'), 'local admission diagnostics');
ok(files.emote.includes('ATB_EMOTE_SEND stage=read http='), 'GET denial is logged');
ok(files.emote.includes('ATB_EMOTE_SEND stage=write http='), 'PUT denial is logged');
ok(files.bridge.includes('RecordLocalSteamObservation'), 'Steam match observation separate from signed reward');
ok(files.bridge.includes('server_xp_unchanged=true'), 'does not forge verified Steam XP');
ok(files.bridge.includes('local_steam_unverified'), 'local observation distinguishes verification status');
ok(files.bridge.includes('m.matchId == match.matchId'), 'read projection dedups match ids');
ok(files.recorder.includes('SaveLocallyObservedMatch(matchId, false)'), 'Steam pending/ineligible matches recorded locally');
ok(files.recorder.includes('AtlasBoardRtdbCareerJournal.RecordAsync'), 'existing official secure claim unchanged');
ok(files.main.includes('OpenPublicLobbyAfterRoomChoice()'), 'legitimate public lobby transitions retained');
ok(files.main.includes('publicBrowser.HostPublicRoomFromMainMenu()'), 'new public entry remains available');
ok(files.main.includes('privateLobby.ShowRoomEntryFromMainMenu()'), 'new private entry remains available');
const rules = JSON.parse(read('Backend/Firebase/database.rules.json')).rules;
ok(rules.emoteEvents.$roomId.$uid['.write'].includes('root.child(\'roomMembers\')'), 'server emote authority unchanged');
ok(rules.careerClaims.$uid.$matchId['.write'].includes('careerFinalProofs'), 'server Career proof unchanged');
console.log('ATB_PHASE19AN_SOURCE_CONTRACT PASS checks='+checks+' firebaseRules=UNMODIFIED firebaseEmulator=NOT_RUN unity=NOT_RUN live=OWNER_TEST_OPEN');
