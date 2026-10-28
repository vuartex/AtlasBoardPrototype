'use strict';
const fs=require('fs'), path=require('path'), assert=require('assert');
const root=path.resolve(__dirname,'../../..');
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const rules=JSON.parse(read('Backend/Firebase/database.rules.json')).rules;
const econ=rules.users.$uid.metaEconomy;
const ids=[...Array.from({length:7},(_,i)=>'ghost_'+String(i+1).padStart(2,'0')),
 'vehicle_police','vehicle_taxi','vehicle_pickup'];
const prices=[300,300,300,300,300,300,300,450,400,425];
class Snap{constructor(v){this.v=v;}val(){return this.v===undefined?null:this.v;}exists(){return this.v!=null;}
 isString(){return typeof this.v==='string';}
 child(path){let val=this.v;for(const key of path.split('/'))val=val==null?undefined:val[key];return new Snap(val);}}
const now=1900000000000,uid='player',auth={uid,token:{firebase:{sign_in_provider:'password'}}},
 rootSnap=new Snap({devWalletTesters:{[uid]:true}}),clone=x=>JSON.parse(JSON.stringify(x));
const check=new Function('auth','data','newData','now','root','$uid',`return (${econ['.write']});`);
let tests=0,nonce=0;
function valid(before,after,expected){const ok=!!check(auth,new Snap(before),new Snap(after),now,rootSnap,uid);assert.strictEqual(ok,expected,`RULE ${after.operation.type}:${after.operation.itemId}`);tests++;}
function doc(){const bools=list=>Object.fromEntries(list.map(x=>[x,false]));return {
 schemaVersion:1,wallet:{gold:0,atlasCoin:0,starterGrantClaimed:false},
 inventory:{profile_frames:bools(['frame_halloween','frame_new_year','frame_turkiye','frame_usa','frame_neon_circuit','frame_royal_crown','frame_ocean_tide','frame_sakura_blossom','frame_cosmic_nova','frame_forest_spirit']),
 boards:bools(['board_midnight_azure','board_ember_maple','board_arctic_aurora']),dice:bools(['dice_ruby_red','dice_ocean_blue','dice_midnight_black','dice_marble']),pawns:bools(ids)},
 equipped:{profileFrameId:'',boardId:'',diceId:''},dailyReward:{lastClaimAt:0},dev:{grantCount:0,resetCount:0},
 operation:{id:'0'.repeat(32),type:'INIT',itemId:'',atMs:now-999}};}
function next(before,type,item=''){const d=clone(before);d.operation={id:(++nonce).toString(16).padStart(32,'0'),type,itemId:item,atMs:now-900+nonce};return d;}
let a=doc();valid(undefined,a,true);
let bad=clone(a);bad.inventory.pawns.ghost_01=true;valid(undefined,bad,false);
let b=next(a,'DEV_GRANT');b.wallet.gold=5000;b.wallet.atlasCoin=500;b.dev.grantCount=1;valid(a,b,true);
let first=b;
for(let i=0;i<ids.length;i++){
 const id=ids[i],price=prices[i];
 // replenish via verified tester grant when needed
 if(first.wallet.gold<price){let refill=next(first,'DEV_GRANT');refill.wallet.gold+=5000;refill.wallet.atlasCoin+=500;refill.dev.grantCount++;valid(first,refill,true);first=refill;}
 let bought=next(first,'PURCHASE',id);bought.wallet.gold-=price;bought.inventory.pawns[id]=true;
 valid(first,bought,true);
 let tamper=clone(bought);tamper.wallet.gold++;valid(first,tamper,false);
 tamper=clone(bought);tamper.inventory.pawns[ids[(i+1)%ids.length]]=!tamper.inventory.pawns[ids[(i+1)%ids.length]];valid(first,tamper,false);
 tamper=clone(bought);tamper.inventory.pawns[id]=false;valid(first,tamper,false);
 let duplicate=next(bought,'PURCHASE',id);duplicate.wallet.gold-=price;valid(bought,duplicate,false);
 first=bought;
}
let badItem=next(first,'PURCHASE','pawn_ghost_999');badItem.wallet.gold-=300;valid(first,badItem,false);
let eq=next(first,'EQUIP_DICE','dice_marble');eq.equipped.diceId='dice_marble';valid(first,eq,false);
let reset=next(first,'RESET_PURCHASES');reset.wallet.gold+=prices.reduce((a,b)=>a+b,0);
reset.inventory=doc().inventory;reset.dev.resetCount++;valid(first,reset,true);
let extra=next(reset,'PURCHASE','ghost_02');extra.wallet.gold-=300;extra.inventory.pawns.ghost_02=true;extra.inventory.pawns.ghost_01=true;valid(reset,extra,false);
let migration=doc();delete migration.inventory.pawns;migration.operation.atMs=now-950;
let migrate=next(migration,'DEV_GRANT');migrate.inventory.pawns=doc().inventory.pawns;migrate.wallet.gold=5000;migrate.wallet.atlasCoin=500;migrate.dev.grantCount=1;valid(migration,migrate,true);
for(const id of ids){assert(econ.inventory.pawns[id]['.validate']==='newData.isBoolean()');tests++;}
const src=read('Assets/Project/Scripts/Meta/AtlasBoardRtdbMetaEconomy.cs');
for(const id of ids){assert(src.includes(`case "${id}": return`));assert(read('Assets/Project/Scripts/Meta/AtlasBoardShopContentDraft.cs').includes(`"${id}", "pawn"`));tests+=2;}
assert(read('Assets/Project/Scripts/PawnCustomization/AtlasBoardPawnCustomizationUI.cs').includes('IsPawnOwnedOnThisAccount'));tests++;
assert(read('Assets/Project/Scripts/UI/Meta/AtlasBoardMetaEconomyUI.cs').includes('AtlasBoardPawnShopPreviewArt.Create'));tests++;
assert(read('Assets/Project/Editor/AtlasBoardPhase19TPawnAssetExtension.cs').includes('EnsureCarMaterial'));tests++;
// Validate every RTDB expression's JavaScript syntax so a missing closing
// parenthesis cannot slip through a pure JSON and write-policy simulation.
function syntaxCheck(node, location) {
  if (!node || typeof node !== 'object') return;
  for (const [key, value] of Object.entries(node)) {
    const next = location + '/' + key;
    if (['.read', '.write', '.validate'].includes(key) && typeof value === 'string') {
      assert.doesNotThrow(() => new Function('return (' + value + ');'),
        'Invalid rule expression at ' + next);
      tests++;
    } else if (value && typeof value === 'object') {
      syntaxCheck(value, next);
    }
  }
}
syntaxCheck(rules, '/rules');
assert.strictEqual(econ.inventory.pawns['.validate'],
  "newData.hasChildren(['ghost_01','ghost_02','ghost_03','ghost_04','ghost_05','ghost_06','ghost_07','vehicle_police','vehicle_taxi','vehicle_pickup'])");
tests++;
console.log('ATB_PHASE19U_PAWN_CONTRACT_PASS checks='+tests+' firebaseEmulator=NOT_RUN unity=NOT_RUN');
