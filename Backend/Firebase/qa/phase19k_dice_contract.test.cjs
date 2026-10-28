'use strict';
const fs=require('fs'),path=require('path'),assert=require('assert');
const root=path.resolve(__dirname,'../../..');
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const rules=JSON.parse(read('Backend/Firebase/database.rules.json')).rules;
const econ=rules.users.$uid.metaEconomy;
const dice=['dice_ruby_red','dice_ocean_blue','dice_midnight_black','dice_marble'];
const prices=[350,350,400,400];
const uid='player_a';let counter=0,passes=0;
const now=1900000000000;
class Snap {
  constructor(value){this.value=value;}
  val(){return this.value===undefined?null:this.value;}
  exists(){return this.value!==undefined&&this.value!==null;}
  isString(){return typeof this.value==='string';}
  child(path){let val=this.value;for(const key of path.split('/'))val=val==null?undefined:val[key];return new Snap(val);}
}
const auth={uid,token:{firebase:{sign_in_provider:'password'}}};
const doc=()=>({schemaVersion:1,wallet:{gold:0,atlasCoin:0,starterGrantClaimed:false},inventory:{profile_frames:Object.fromEntries(['frame_halloween','frame_new_year','frame_turkiye','frame_usa','frame_neon_circuit','frame_royal_crown','frame_ocean_tide','frame_sakura_blossom','frame_cosmic_nova','frame_forest_spirit'].map(x=>[x,false])),boards:Object.fromEntries(['board_midnight_azure','board_ember_maple','board_arctic_aurora'].map(x=>[x,false])),dice:Object.fromEntries(dice.map(x=>[x,false]))},equipped:{profileFrameId:'',boardId:'',diceId:''},dailyReward:{lastClaimAt:0},dev:{grantCount:0,resetCount:0},operation:{id:'0'.repeat(32),type:'INIT',itemId:'',atMs:now-1000}});
const evalWrite=new Function('auth','data','newData','now','root','$uid',`return (${econ['.write']});`);
const cloned=x=>JSON.parse(JSON.stringify(x));
const tester=new Snap({devWalletTesters:{[uid]:true}});
function valid(oldDoc,nextDoc,allow=true,who=auth){const actual=!!evalWrite(who,new Snap(oldDoc),new Snap(nextDoc),now,tester,uid);assert.strictEqual(actual,allow,`rule unexpected ${nextDoc.operation.type} ${nextDoc.operation.itemId} expected ${allow}`);passes++;}
function next(from,type,item){let n=cloned(from);n.operation={id:(++counter).toString(16).padStart(32,'0'),type,itemId:item||'',atMs:now-900+counter};return n;}
let a=doc();a.operation.atMs=now-950;
valid(undefined,a,true);let bad=cloned(a);bad.inventory.dice.dice_ruby_red=true;valid(undefined,bad,false);
let b=next(a,'DEV_GRANT','');b.wallet.gold+=5000;b.wallet.atlasCoin+=500;b.dev.grantCount++;valid(a,b,true);valid(a,{...b,wallet:{...b.wallet,gold:999999}},false);
let unowned=next(b,'EQUIP_DICE','dice_ruby_red');unowned.equipped.diceId='dice_ruby_red';valid(b,unowned,false);
let c=next(b,'PURCHASE','frame_halloween');c.wallet.gold-=350;c.inventory.profile_frames.frame_halloween=true;valid(b,c,true);
let d=next(c,'PURCHASE','board_ember_maple');d.wallet.gold-=1500;d.inventory.boards.board_ember_maple=true;valid(c,d,true);
for(let i=0;i<dice.length;i++){
 const before=d;const id=dice[i],price=prices[i];d=next(before,'PURCHASE',id);d.wallet.gold-=price;d.inventory.dice[id]=true;
 valid(before,d,true);let evil=cloned(d);evil.wallet.gold+=1;valid(before,evil,false);
 evil=cloned(d);evil.inventory.dice[dice[(i+1)%dice.length]]=!evil.inventory.dice[dice[(i+1)%dice.length]];valid(before,evil,false);
}
let e=next(d,'EQUIP_DICE','dice_ruby_red');e.equipped.diceId='dice_ruby_red';valid(d,e,true);
let badEquip=next(e,'EQUIP_DICE','dice_marble');badEquip.equipped.diceId='dice_marble';badEquip.wallet.gold++;valid(e,badEquip,false);
let defaults=next(e,'EQUIP_DICE','');defaults.equipped.diceId='';valid(e,defaults,true);
let f=next(defaults,'EQUIP_BOARD','board_ember_maple');f.equipped.boardId='board_ember_maple';valid(defaults,f,true);
let g=next(f,'EQUIP','frame_halloween');g.equipped.profileFrameId='frame_halloween';valid(f,g,true);
let h=next(g,'DAILY','');h.wallet.gold+=50;h.dailyReward.lastClaimAt=h.operation.atMs;valid(g,h,true);
let reset=next(h,'RESET_PURCHASES','');reset.wallet.gold+=350+1500+350+350+400+400;reset.inventory=doc().inventory;reset.equipped=doc().equipped;reset.dev.resetCount++;valid(h,reset,true);
let tamper=next(reset,'PURCHASE','dice_marble');tamper.wallet.gold-=400;tamper.inventory.dice.dice_marble=true;tamper.equipped.boardId='board_arctic_aurora';valid(reset,tamper,false);
let fake=next(reset,'PURCHASE','dice_obsidian_gold');fake.wallet.gold-=450;valid(reset,fake,false);
valid(reset,next(reset,'DEV_GRANT',''),false,{uid:'another',token:{firebase:{sign_in_provider:'password'}}});
for(const id of dice){assert(econ.inventory.dice[id]['.validate']==='newData.isBoolean()');passes++;}
assert(econ.operation.type['.validate'].includes('EQUIP_DICE'));passes++;
const draft=read('Assets/Project/Scripts/Meta/AtlasBoardShopContentDraft.cs');
for(const id of dice){assert(draft.includes('"'+id+'"'));passes++;}
for(const absent of ['"default_dice", "dice_skin"','"dice_obsidian_gold", "dice_skin"','"dice_polar_ice", "dice_skin"','"dice_graphite_gray", "dice_skin"']){assert(!draft.includes(absent));passes++;}
const art=read('Assets/Project/Scripts/UI/Meta/AtlasBoardDiceSkinPreviewRuntime.cs');
for(const str of ['RenderTexture(256, 256','GetComponentsInChildren<Renderer>','previewCamera.enabled = false']){assert(art.includes(str));passes++;}
const scene=read('Assets/Board_Prototype.unity');for(const guid of ['b29be7e578bb89144b97eea319812824','134827ca0f2c9b64386c5a21b0c0c929','4df17728f18d1b74ca6d153adaaf8094','8e59f73a9bfbdad4d9d0da0732cdba21','84faf413a04b5c4458ef525249a41731']){assert(scene.includes(guid));passes++;}
const visual=read('Assets/Project/Scripts/Board/DiceVisualController.cs');const store=read('Assets/Project/Scripts/UI/Meta/AtlasBoardMetaEconomyUI.cs');const inventory=read('Assets/Project/Scripts/UI/Progression/AtlasBoardProgressionUI.cs');
for(const str of ['PrimeOnlineDiceSkins','ApplyImportedDice','rubyDiceModel','GetModelForPreview']){assert(visual.includes(str));passes++;}
for(const str of ['AtlasBoardDiceSkinPreviewArt.Create','ShowFramePreview(item.ItemId)','ItemBuy_']){assert(store.includes(str));passes++;}
for(const str of ['ApplyOwnedDiceAsync','EquipDiceAsync','default_dice']){assert(inventory.includes(str));passes++;}
console.log(`ATB_PHASE19K_SOURCE_RULE_QA PASS checks=${passes} protectedPurchases=true entitlementEquip=true multiSlotProjection=sourceOnly unityEditor=NOT_RUN authEmulator=NOT_RUN`);
