'use strict';
// Source-level contracts and pure authorization examples only.
// This does NOT run Unity nor RTDB emulator / live multiple clients.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../../../');
const get = p => fs.readFileSync(path.join(root,p),'utf8');
const file = p => get('Assets/Project/'+p);
let checks=0;
function yes(x,msg){assert.ok(x,msg);checks++;}
const shop=file('Scripts/UI/Meta/AtlasBoardMetaEconomyUI.cs');
const lobby=file('Scripts/Online/Firebase/AtlasBoardRtdbCasualLobbyTransport.cs');
const pawn=file('Scripts/PawnCustomization/AtlasBoardPawnCustomizationUI.cs');
const dice=file('Scripts/Board/DiceVisualController.cs');
const auction=file('Scripts/UI/AtlasBoardAuctionPresentationPolish.cs');
const tile=file('Scripts/Board/BoardGenerator.cs');
const guest=file('Scripts/Progression/AtlasBoardProgressionBridge.cs');
const emote=file('Scripts/Meta/AtlasBoardEmoteSpriteCatalog.cs');
const emoteLegacy=file('Scripts/Meta/AtlasBoardEmotes.cs');
const emoteEditor=file('Editor/AtlasBoardPhase19YEmoteCatalogSetup.cs');
const rules=JSON.parse(get('Backend/Firebase/database.rules.json')).rules;
// Role-policy pure model (host+bot, own verified human, no foreign human).
function canChange(host, actor, seat){
 if(seat.isHuman) return seat.uid===actor;
 return host===actor && seat.isBot;
}
for (const scenario of [
 [{isHuman:true,uid:'h'}, 'h',true],
 [{isHuman:true,uid:'p'}, 'h',false],
 [{isHuman:true,uid:'p'}, 'p',true],
 [{isHuman:true,uid:'h'}, 'p',false],
 [{isHuman:false,isBot:true}, 'h',true],
 [{isHuman:false,isBot:true}, 'p',false],
]) yes(canChange('h',scenario[1],scenario[0])===scenario[2], 'pawn owner matrix');
yes(shop.includes('item.ItemType == "board_theme" || item.ItemType == "map"'),'guest BUY for all map/board preview');
yes(shop.includes('OpenStoreSignIn()'),'login gate kept');
yes(shop.includes('if (item.PreviewOnly)'),'preview item never free real purchase');
yes(lobby.includes('RequestOwnPawnChangeAsync('),'guest own pawn request');
yes(lobby.includes('"intents/" + Safe(room) + "/" + Safe(uid)'),'authenticated uid scoped intent');
yes(lobby.includes('seat.AccountId != intent.uid || seat.SeatId != intent.seatId'),'host verifies exact target seat');
yes(lobby.includes('seat.SeatMode != AtlasLobbySeatMode.RemoteHuman'),'host rejects non-remote command');
yes(lobby.includes('target.Active && target.IsHumanSeat'),'host may not change remote player');
yes(lobby.includes('AcceptPendingPawnChanges('),'host polls pawn requests');
yes(lobby.includes('await StoreSnapshot(room,snap,token,state.revision+1)'),'authoritative CAS style commit retained');
yes(lobby.includes('foreach (string acceptedPawn in pawnIntents)'),'post-commit intent cleanup');
yes(lobby.includes('oldMember.joinedAt > 0'),'rejoin preserves immutable timestamp');
yes(lobby.includes('if(oldMember != null && oldMember.active)'),'no second active admission');
yes(rules.roomMembers.$roomId.$uid.joinedAt['.validate'].includes('data.val()'),'joinedAt immutable security unchanged');
yes(rules.intents.$roomId.$uid.$intentId['.write'].includes('auth.uid === $uid'),'uid intent rule enforced');
yes(pawn.includes('editable = localIsHost;'),'host may edit bots, not peers');
yes(dice.includes('PrimeOnlineDiceSkins(seats)'),'online per UID roster primed');
yes(dice.includes('NeedsOnlineDiceRefresh(seats)'),'refresh only when roster changes');
yes(dice.includes('IsOnlineSessionActive'),'do not override offline dice');
yes(dice.includes('nextOnlineSeatProjectionRefreshAt'),'rate limit scene lookup');
yes(dice.includes('if (!botControlled'),'bot always classic');
yes(auction.includes('RestoreLegacyPresentation();'),'auction numbers restored');
yes(auction.includes('HidePolish();'),'faulty overlay hidden');
yes(!/private void LateUpdate\(\)\s*\{[^}]*HideLegacyPresentation\(/s.test(auction),'no main HUD hidden by overlay');
yes(tile.includes('SetPropertyBlock(matte)'),'light city faces matte');
yes(tile.includes('0.08f'),'smoothness reduced');
yes(guest.includes('data.localXp'),'guest XP only device local');
yes(guest.includes('AtlasBoardRtdbCareerJournal.XpAward('),'local XP formula consistent');
yes(guest.includes('local_guest_unverified_xp_no_spendable_gold'),'no false backend rewards');
yes(!emoteLegacy.includes('public sealed class AtlasBoardEmoteSpriteCatalog'),'catalog no longer in wrong script');
yes(emote.includes('public sealed class AtlasBoardEmoteSpriteCatalog : ScriptableObject'),'same-named script');
yes(emoteEditor.includes('AssetDatabase.DeleteAsset(AssetPath)'),'regenerate broken catalog only');
yes(get('Assets/Project/Scripts/Meta/AtlasBoardEmoteSpriteCatalog.cs.meta').includes('MonoImporter:'),'meta for Unity import');
console.log('ATB_PHASE19AL_SOURCE_CONTRACT PASS checks='+checks+' emulator=NOT_RUN unity=NOT_RUN multiplayer=NOT_RUN remotePaidPawnTrust=OPEN');
