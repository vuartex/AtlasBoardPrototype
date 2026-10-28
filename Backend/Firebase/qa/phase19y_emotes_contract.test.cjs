'use strict';
const fs=require('fs'),assert=require('assert'),path=require('path');
const root=path.resolve(__dirname,'../../..');
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const r=JSON.parse(read('Backend/Firebase/database.rules.json')).rules;
const sprites=fs.readdirSync(path.join(root,'Assets/Project/ThirdParty/Kenney/Emotes/Sprites')).filter(n=>n.endsWith('.png'));
assert.strictEqual(sprites.length,30);let checks=1;
const paid=sprites.map(n=>n.slice(0,-4)).filter(x=>x!=='emote__'&&!['emote_faceHappy','emote_faceSad','emote_faceAngry'].includes(x));
assert.strictEqual(paid.length,26);checks++;
const eco=read('Assets/Project/Scripts/Meta/AtlasBoardRtdbMetaEconomy.cs');
const catalog=read('Assets/Project/Scripts/Meta/AtlasBoardEmotes.cs');
const store=read('Assets/Project/Scripts/UI/Meta/AtlasBoardMetaEconomyUI.cs');
const inventory=read('Assets/Project/Scripts/UI/Progression/AtlasBoardProgressionUI.cs');
const hud=read('Assets/Project/Scripts/UI/AtlasBoardEmoteHudRuntime.cs');
const net=read('Assets/Project/Scripts/Meta/AtlasBoardEmoteNetwork.cs');
const emotes=r.users.$uid.metaEconomy.inventory.emotes;
const write=r.users.$uid.metaEconomy['.write'];
for(const id of paid){
  assert(catalog.includes('"'+id+'"'),id+' missing from C# catalog');checks++;
  assert(eco.includes('public bool '+id+';'),id+' missing wallet');checks++;
  assert(write.includes("=== '"+id+"'"),id+' not accepted by purchase wallet');checks++;
  assert(write.includes("inventory/emotes/"+id),id+' refund absent');checks++;
  assert(emotes[id] && emotes[id]['.validate'].includes("=== '"+id+"'"),id+' child validation absent');checks++;
  assert(r.users.$uid.emoteLoadout.slot1['.validate'].includes(id),id+' not validated by loadout');checks++;
}
assert(!eco.includes('case "emote_faceHappy": return 150'));checks++;
assert(store.includes('"map"') && !store.includes('"animation_pack"'));checks++;
assert(store.includes('AtlasBoardEmotePreviewArt.Create'));checks++;
assert(inventory.includes('ToggleOwnedEmoteAsync') && inventory.includes('list.Count >= 6'));checks++;
assert(hud.includes('OnPointerClick')===false && hud.includes('ShowBubble') && hud.includes('IsLocallyControlledHumanSlot'));checks++;
assert(read('Assets/Project/Scripts/UI/PlayerHudPanel.cs').includes('IPointerClickHandler'));checks++;
assert(net.includes('SendAsync') && net.includes('ReadRoomAsync'));checks++;
assert(r.emoteEvents.$roomId.$uid['.write'].includes('+ 3000'));checks++;
assert(r.emoteEvents.$roomId.$uid['.write'].includes('=== 20'));checks++;
assert(r.emoteEvents.$roomId.$uid['.write'].includes('roomMembers'));checks++;
assert(r.emoteEvents.$roomId.$uid['.write'].includes('emoteLoadout'));checks++;
assert(read('Assets/Project/Documentation/MapDrafts/Map_Products_Germany_Mexico_DRAFT.json').includes('OWNER_APPROVED'));checks++;
let expressions=0;
(function walk(v){if(v && typeof v==='object')for(const [k,x] of Object.entries(v)){
  if((k==='.write'||k==='.read'||k==='.validate')&&typeof x==='string'){
   new Function('auth','data','newData','root','now','$uid','$roomId','$kind','$scopeId',`return (${x});`);expressions++;
  }else walk(x);
}})(r);
checks+=expressions;
console.log('ATB_PHASE19Y_EMOTES_SOURCE_PASS checks='+checks+' expressions='+expressions+' firebaseEmulator=NOT_RUN unity=NOT_RUN multiplayer=NOT_RUN');
