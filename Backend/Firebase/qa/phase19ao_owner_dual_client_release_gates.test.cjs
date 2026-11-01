'use strict';
// Source-contract regressions ONLY. Does NOT run Unity or the RTDB emulator.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../../..');
const read = p => fs.readFileSync(path.join(root,p), 'utf8');
const src = {
  auction:read('Assets/Project/Scripts/UI/AtlasBoardAuctionPresentationPolish.cs'),
  lobby:read('Assets/Project/Scripts/UI/Menu/AtlasBoardMainMenuController.cs'),
  transport:read('Assets/Project/Scripts/Online/Firebase/AtlasBoardRtdbCasualLobbyTransport.cs'),
  coordinator:read('Assets/Project/Scripts/Online/AtlasBoardTurnDiceNetworkCoordinator.cs'),
  sender:read('Assets/Project/Scripts/Meta/AtlasBoardEmoteNetwork.cs'),
  loadout:read('Assets/Project/Scripts/Meta/AtlasBoardEmoteLoadoutService.cs'),
  hud:read('Assets/Project/Scripts/UI/AtlasBoardEmoteHudRuntime.cs')
};
let checks=0;
function ok(condition,label) {assert.ok(condition,label);checks++;}
ok(src.auction.includes('canvas.rootCanvas.transform'), 'modern auction uses an independent root viewport');
ok(src.auction.includes('overlayCanvas.overrideSorting = true'), 'modern auction has separate canvas');
ok(src.auction.includes('rootObject.AddComponent<GraphicRaycaster>()'), 'modern action buttons remain raycast targets');
ok(src.auction.includes('HideLegacyCanvasGroup()'), 'legacy panel fully hidden');
ok(src.auction.includes('SuppressLegacyGraphics()'), 'legacy art is permanently suppressed behind modern');
ok(!src.auction.includes('AddComponent<CanvasGroup>()'), 'no runtime CanvasGroup add/remove race on legacy auction');
ok(src.auction.includes('auctionManager.CurrentBid'), 'live bid used, no fixed stale cached price');
ok(src.auction.includes('legacySmallButton.interactable'), 'existing bid authority maintained');
ok(!src.lobby.includes('SuppressRetiredLobbyChildren'), 'do not blanket-disable settings and player widgets');
ok(src.lobby.includes('lobbyPanel.transform.Find("PrivateOnlineRoot")'), 'require modern private room root');
ok(src.lobby.includes('RestoreOnlineLobbyScaffold()'), 'required real online panels explicitly restored');
ok(src.lobby.includes('lobbyPanel.transform.Find("Panel_GameSettings")'), 'online game settings present');
ok(src.lobby.includes('lobbyPanel.transform.Find("Panel_Players")'), 'online players panel present');
ok(src.lobby.includes('ATB_ONLINE_LOBBY_BLOCKED public_controller_missing=true legacy_fallback=false'), 'no public-play legacy fallback');
ok(src.lobby.includes('ATB_ONLINE_LOBBY_BLOCKED private_controller_missing=true legacy_fallback=false'), 'no private legacy fallback');
ok(src.transport.includes('snap.LifecycleState != AtlasRoomLifecycleState.Starting'), 'Starting is recognized as live match during rejoin');
ok(src.transport.includes('meta.activeMatchId != snap.MatchId'), 'exact active match validated');
ok(src.transport.includes('seat.AccountId == join.uid && m.LocalOwnerAccountId')===false, 'avoid accidental mixed UID check');
ok(src.transport.includes('m.AccountId == join.uid && m.LocalOwnerAccountId == join.uid'), 'only existing UID-owned reservation');
ok(src.transport.includes('existingJoin.passwordHash == request.passwordHash'), 'retry only the same immutable join request');
ok(src.transport.includes('pending_request_reused=true'), 'retry existing immutable request');
ok(src.transport.includes('host_cleanup_required=true'), 'stale immutable request identified');
ok(src.coordinator.includes('PumpActiveMatchRejoinsAsync'), 'host game loop pumps rejoin');
ok(src.sender.includes('stage=loadout_seed'), 'missing initial free guest loadout is diagnosed');
ok(src.sender.includes('AtlasBoardEmoteLoadoutService.SaveAsync'), 'free Guest loadout seeded by authorized account');
ok(src.sender.includes('serverHasEmote'), 'RTDB emote requires server-owned slot');
ok(src.sender.includes('previous.etag'), 'existing event write uses conditional update');
ok(src.hud.includes('ActiveLobbyUid()'), 'emote cooldown keys by live lobby UID');
ok(src.hud.includes('pair.Key == ActiveLobbyUid()'), 'emote receiver deduplicates same live UID');
console.log('ATB_PHASE19AO_SOURCE_CHECK '+checks+' checks OK; unity=NOT_RUN rtdb_emulator=NOT_RUN dual_client=NOT_RUN');
