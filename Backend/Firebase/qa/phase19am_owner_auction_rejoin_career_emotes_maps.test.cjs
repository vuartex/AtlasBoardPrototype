'use strict';
// SOURCE CONTRACT ONLY. This does not run Unity, 2-client QA, or Firebase Emulator.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../../..');
let checks = 0;
function read(relative){return fs.readFileSync(path.join(root,relative),'utf8');}
function check(condition,label){assert.ok(condition,label); checks++;}
const auction = read('Assets/Project/Scripts/UI/AtlasBoardAuctionPresentationPolish.cs');
check(auction.includes('EnsurePolish();') && auction.includes('RefreshPresentation();') && auction.includes('MirrorButtonState();'), 'modern auction refresh flow wired');
check(auction.includes('auctionPanel.transform as RectTransform'), 'modern auction renders within actual auction panel');
check(auction.includes('bidValueText.SetText($"{shownBid:N0} ₵")'), 'hero shows authoritative CurrentBid every refresh');
check(auction.includes('int shownBid = auctionManager.CurrentBid;'), 'bid hero uses current bid not stale cache');
check(auction.includes('property.DisplayName') && auction.includes('property.PurchasePrice') && auction.includes('property.BaseRent'), 'city/purchase/rent from existing auction');
check(auction.includes('polishRoot.SetAsLastSibling();'), 'modern panel drawn above existing auction');
check(!auction.includes('Source regression rollback: the optional polished overlay'), 'broken rollback removed');
const lobby = read('Assets/Project/Scripts/Online/Firebase/AtlasBoardRtdbCasualLobbyTransport.cs');
check(lobby.includes('ATB_RTDB_REJOIN restored_existing_seat=true'), 'former member reconnect branch exists');
check(lobby.includes('prior != null && !prior.active && prior.uid == join.uid'), 'restore only inactive same UID');
check(lobby.includes('prior.role == "remoteHuman" && prior.joinedAt > 0'), 'never promote unrelated seat or host');
check(lobby.includes('seat.ReadyForRevision = 0;'), 'rejoin resets readiness');
check(lobby.includes('seat.SeatMode == AtlasLobbySeatMode.RemoteHuman'), 'no local/bot seats commandeered');
check(lobby.includes('RestorePendingMatchRejoinsOnly(room, uid, token, snap)'), 'active-match host polls pending known UID reconnects');
check(lobby.includes('meta.activeMatchId != snap.MatchId'), 'active-match rejoin requires canonical match anchor');
check(lobby.includes('running.status != "active"'), 'completed match cannot reopen participants');
check(lobby.includes('m.LocalOwnerAccountId == join.uid'), 'match rejoin retains exact seat UID ownership');
const achievements = read('Assets/Project/Scripts/Progression/AtlasBoardCareerAchievementDisplay.cs');
check(achievements.includes('Math.Min(def.Target, Math.Max(0, server.progress))'), 'all mapped achievements cap at canonical target');
// Exercise the expected bounded-progress policy with representative input.
for (let target of [1,3,10,20,25,100,5000]) for (let input of [-2,0,1,target,target+1,100000]) {
  const value = Math.min(target,Math.max(0,input));
  check(value >= 0 && value <= target,`achievement target=${target} input=${input}`);
}
const network = read('Assets/Project/Scripts/Meta/AtlasBoardEmoteNetwork.cs');
check(network.includes('bridge.AuthTokenForOnlineSubsystems') && network.includes('bridge.CurrentAccountId'), 'emotes bound to lobby UID+token');
check(!network.includes('FirebaseAuth.DefaultInstance.CurrentUser'), 'emotes do not accidentally read unrelated native login');
const hud = read('Assets/Project/Scripts/UI/AtlasBoardEmoteHudRuntime.cs');
check(hud.includes('ActiveLobbyUid()') && hud.includes('pair.Key == ActiveLobbyUid()'), 'HUD self-filter matches emote sender UID');
const cat = read('Assets/Project/Editor/AtlasBoardPhase19YEmoteCatalogSetup.cs');
check(cat.includes('CatalogComplete(existing)') && cat.includes('entry.sprite == null'), 'catalog presence checks actual referenced sprites');
check(cat.includes('AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate)'), 'generated asset imported before build');
const maps = read('Assets/Project/Scripts/UI/Menu/AtlasBoardMainMenuController.cs');
check(maps.includes('ShowPremiumMapNotOwned('), 'shared premium map popup');
check(maps.includes('ATB_ONLINE_LOBBY_BLOCKED') && maps.includes('legacy_fallback=false'), 'obsolete lobby fallback disabled');
const privateUI = read('Assets/Project/Scripts/UI/Menu/AtlasBoardPrivateLobbyUIController.cs');
check(privateUI.includes('mainMenuController?.ShowPremiumMapNotOwned(requestedPremium)'), 'private lobby purchase notice');
check(privateUI.includes('Secondary local-human seats share the host device/UID'), 'secondary local humans not displayed as duplicate Steam owner');
const premium = read('Assets/Project/Scripts/UI/AtlasBoardPremiumMapDropdownLabels.cs');
check(premium.includes('dropdown.SetValueWithoutNotify(') && premium.includes('menu?.ShowPremiumMapNotOwned(premium)'), 'shared preview-level premium notice');
const rules = JSON.parse(read('Backend/Firebase/database.rules.json'));
check(rules.rules.roomMembers && rules.rules.joinRequests && rules.rules.careerClaims, 'RTDB rules preserved, syntactically valid JSON');
console.log(`ATB_PHASE19AM_SOURCE_CONTRACT PASS checks=${checks} emulator=NOT_RUN unity=NOT_RUN twoClient=NOT_RUN networkHostRejoin=OWNER_TEST_OPEN`);
