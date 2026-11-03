'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../../../');
const load = rel => fs.readFileSync(path.join(root, rel), 'utf8');
const inv = load('Assets/Project/Scripts/UI/Progression/AtlasBoardProgressionUI.cs');
const server = load('Assets/Project/Scripts/Platform/AtlasBoardServerStatusUI.cs');
const emote = load('Assets/Project/Scripts/Meta/AtlasBoardEmoteLoadoutService.cs');
const wallet = load('Assets/Project/Scripts/Meta/AtlasBoardRtdbMetaEconomy.cs');
const roadmap = load('Assets/Project/Documentation/MASTER_ROADMAP.md');
const codebook = load('Assets/Project/Documentation/FRONTEND_ERROR_CODEBOOK.md');
const check = (condition, info) => { assert.ok(condition, info); checks++; };
let checks = 0;
const guards = [
 [inv.includes('TryGetLiveGuestRewardIdentity('), 'REST UID accepted by Career'],
 [inv.includes('uid != AtlasBoardRtdbMetaEconomy.CurrentUid'), 'cross-UID inventory denied'],
 [inv.includes('read.WalletVerified'), 'wallet proof required before owned flags'],
 [inv.includes('inventoryServer.WalletVerified'), 'wallet data not treated as speculative'],
 [inv.includes('item.ItemType == inventoryCategory'), 'category identity check'],
 [inv.includes('item != null && item.Owned'), 'owned server entitlement required'],
 [!inv.includes('item.Owned && !item.PreviewOnly'), 'PreviewOnly no longer hides owned purchases'],
 [inv.includes('AtlasBoardEmoteLoadoutService.LoadAsync()'), 'paid/free emotes loaded after wallet'],
 [inv.includes('paid_emotes='), 'redacted paid ownership diagnostic'],
 [inv.includes('activeEmoteCanBeRemoved'), 'minimum emote UI guard'],
 [inv.includes('AtlasBoardEmoteLoadoutService.Active.Count > 1'), 'last emote cannot be removed'],
 [inv.includes('restVerifiedInventory'), 'REST registered inventory button enabled'],
 [inv.includes('inventorySelectedId == InventoryDefaultFor'), 'default cosmetic remains available'],
 [inv.includes('!inventoryLoading && !inventoryWriting'), 'apply disabled during server check'],
 [inv.includes('InventoryIsEquipped'), 'selection state computed from profile'],
 [inv.includes('TryReadObservedDice'), 'local authentic dice fallback'],
 [inv.includes('snapshot.RecentMatches'), 'same-match evidence for local dice'],
 [inv.includes('thisUidMatches.Contains(m.matchId)'), 'cross-match dice prevented'],
 [inv.includes('m.totalDiceValue < m.diceRolls * 2'), 'dice lower bound validation'],
 [inv.includes('m.totalDiceValue > m.diceRolls * 12'), 'dice upper bound validation'],
 [inv.includes('"—"'), 'unavailable historic dice not zeroed'],
 [inv.includes('"*Bu cihazda kaydedilen maçlar"'), 'local provenance label'],
 [emote.includes('MinActiveEmotes = 1'), 'emote service minimum active'],
 [emote.includes('if-match'), 'emote write ETag'],
 [emote.includes('IsEmoteOwnedForCurrentUid'), 'emote paid ownership guard'],
 [wallet.includes('ATB_EMOTE_INVENTORY source=canonical_wallet'), 'wallet provenance diagnostic'],
 [server.includes('TryGetLiveGuestRewardIdentity(out uid,out token)'), 'server status REST identity'],
 [server.includes('FirebaseAuth.DefaultInstance.CurrentUser'), 'server status native identity'],
 [server.includes('uid, AtlasBoardRtdbMetaEconomy.CurrentUid'), 'server status wallet identity match'],
 [server.includes('"/users/" + escaped + "/general.json"'), 'authenticated account probe'],
 [server.includes('"/publicRoomDirectory.json"'), 'real public rooms probe'],
 [server.includes('"/careerProfiles/"'), 'real Career read probe'],
 [server.includes('state[3] = accountCode == 200 && walletUid ? Health.Limited'), 'shop does not claim verified purchase'],
 [server.includes('HasLiveMatch()'), 'green online requires actual match samples'],
 [server.includes('checkAt = now + 60f'), 'bounded frequency'],
 [server.includes('"BULUT VERİLERİ"'), 'friendly cloud label'],
 [!server.includes('"RTDB BAĞLANTISI"'), 'no public RTDB display'],
 [!server.includes('"RTDB odaları'), 'no public RTDB details'],
 [server.includes('Word("checking")'), 'checking state'],
 [server.includes('Word("login_required")') === false, 'message selection uses note keys, no hardcoded hack'],
 [server.includes('"no_network"'), 'offline response'],
 [server.includes('"rooms_ready"'), 'limited online response'],
 [server.includes('"account_expired"'), 'auth expiration response'],
 [server.includes('"shop_browse"'), 'shop limited until actual transaction'],
 [server.includes('"match_verified"'), 'verified active match response'],
 [server.includes('ATB_SERVER_PROBE'), 'redacted trace'],
 [!server.includes('Debug.Log(url)'), 'no token URLs in logs'],
 [roadmap.includes('v5.49.12bz'), 'roadmap checkpoint'],
 [codebook.includes('v5.49.12bz'), 'codebook updated'],
];
for (const [ok,info] of guards) check(ok,info);
console.log(`ATB_PHASE19BZ_INVENTORY_STATUS_DICE_SOURCE PASS checks=${checks} unity_compile=NOT_RUN emulator=NOT_RUN production=NOT_TOUCHED`);
