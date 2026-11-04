'use strict';
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
// Install devDependencies in Backend/Firebase/functions before running this QA.
let ts;
try { ts = require('../functions/node_modules/typescript'); }
catch { ts = require('typescript'); }
const root = path.resolve(__dirname, '../functions/src/economy');
const realDateNow = Date.now;
// Test during the event, independently of when this QA file is run.
Date.now = () => Date.UTC(2026,10,4,12,0,0);
class HttpsError extends Error { constructor(code,message,details){super(message);this.code=code;this.details=details;} }
function valAt(obj,p){return p.split('/').filter(Boolean).reduce((v,k)=>v?.[k],obj);}
function setAt(obj,p,v){const parts=p.split('/').filter(Boolean); let node=obj;for(let i=0;i<parts.length-1;i++){node[parts[i]]??={};node=node[parts[i]];}const k=parts[parts.length-1]; if(v===null)delete node[k];else node[k]=v;}
const data={
 devWalletTesters:{owner:true}, users:{owner:{userId:'owner',metaEconomy:{wallet:{gold:200,atlasCoin:20},inventory:{maps:{},boards:{},emotes:{},profile_frames:{},pawns:{},dice:{}}},displayName:'Owner'}},
 careerClaims:{owner:{}}
};
const db={ref(p){return {
 child(child){return db.ref(`${p}/${child}`)},
 async get(){return {val:()=>structuredClone(valAt(data,p)??null)}},
 async update(items){for(const [key,value] of Object.entries(items))setAt(data,`${p}/${key}`,structuredClone(value));},
 async transaction(callback){const prev=structuredClone(valAt(data,p)??null), updated=callback(prev);if(updated===undefined)return {committed:false,snapshot:{val:()=>structuredClone(valAt(data,p)??null)}};setAt(data,p,structuredClone(updated));return {committed:true,snapshot:{val:()=>structuredClone(updated)}};}
}}};
// The imported manifest is read by the transpiled server module.
const cache={};
function makeModule(basename){if(cache[basename])return cache[basename];const tsSource=fs.readFileSync(path.join(root,basename+'.ts'),'utf8');const js=ts.transpileModule(tsSource,{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022,strict:true}}).outputText;const mod={exports:{}};const req=id=>{
 if(id==='firebase-admin/database')return {getDatabaseWithUrl:(url)=>{assert.equal(url,'https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app');return db;}};
 if(id==='firebase-functions/v2/https')return {HttpsError};
 if(id==='crypto')return require('crypto');
 if(id.startsWith('./'))return makeModule(id.slice(2));
 throw new Error('unmocked require '+id);
};
new Function('require','module','exports',js)(req,mod,mod.exports);cache[basename]=mod.exports;return mod.exports;}
const live=makeModule('world-journey-live');
// Deliberately synthetic fixtures: no production codes or seed manifest in source.
const codes=[
 {code:'ATLAS-QA-GOLD-EXAMPLE', type:'gold', amount:50},
 {code:'ATLAS-QA-XP-EXAMPLE', type:'xp', amount:500},
 {code:'ATLAS-QA-FRAME-EXAMPLE', type:'inventory_item', amount:1, itemId:'frame_test_dummy', itemType:'profile_frame'},
];
for (const c of codes) {
 data.promoCodes??={};
 data.promoCodes[c.code]={
  code:c.code, schemaVersion:2, active:true, scope:'owner_qa',
  startsAtEpochMs:0, endsAtEpochMs:Date.UTC(2030,0,1),
  perAccountLimit:1, quantity:c.amount,
  reward:{type:c.type,amount:c.amount,
   ...(c.itemId?{itemId:c.itemId,itemType:c.itemType}:{})},
 };
}
let passed=0;
async function check(name,fn){await fn();passed++; console.log('PASS',name)}
async function rejectsCode(name,action,code){await check(name,async()=>assert.equal((await action().then(()=>null,e=>e)).code,code));}
(async()=>{
 await check('operator-managed catalog has no seed API; fixture uses synthetic codes',async()=>{
  assert.equal(typeof live.worldJourneySeedOwnerPromoCodes,'undefined');
  assert.equal(Object.keys(data.promoCodes).length,3);
 });
 await rejectsCode('owner-only catalog rejects non-test UID',()=>live.worldJourneyRedeem('other',codes[0].code),'permission-denied');
 await check('gold promo credited exactly once and transaction recorded',async()=>{const result=await live.worldJourneyRedeem('owner',codes[0].code);assert.equal(result.applied,true);assert.equal(data.users.owner.metaEconomy.wallet.gold,250);assert.equal(Object.keys(data.users.owner.promoRedemptions).length,1);assert.equal(Object.values(data.users.owner.promoRedemptions)[0].code,codes[0].code);});
 await rejectsCode('repeat redeem same uid denied',()=>live.worldJourneyRedeem('owner',codes[0].code),'already-exists');
 await rejectsCode('unissued code fails without grant',()=>live.worldJourneyRedeem('owner','ATLAS-G-XXXXXXXXXXXXXXXXXX'),'not-found');
 await rejectsCode('wrong reward quantity blocked',async()=>{data.promoCodes[codes[1].code].quantity=10;return live.worldJourneyRedeem('owner',codes[1].code)},'failed-precondition');
 await check('one inventory grant is recorded and owned',async()=>{const code=codes.find(x=>x.type==='inventory_item'&&x.itemType==='profile_frame').code;const r=await live.worldJourneyRedeem('owner',code);assert.equal(r.applied,true);assert.equal(Object.values(data.users.owner.metaEconomy.inventory.profile_frames).filter(Boolean).length,1);});
 await rejectsCode('event-only world map cannot be granted through promo',async()=>{const code=codes[1].code;data.promoCodes[code].quantity=1;data.promoCodes[code].reward={type:'inventory_item',amount:1,itemId:'map_world',itemType:'map'};return live.worldJourneyRedeem('owner',code)},'failed-precondition');
 // Three accepted matches contain all seven non-exclusive milestones.
 for(let n=0;n<3;n++){const matchId=`testmatch${n}`;data.careerClaims.owner[matchId]={uid:'owner',matchId,atMs:Date.now(),result:'win',diceRolls:20,developments:4,turns:8,mapId:['map_turkey','map_usa','map_germany'][n],netWorth:6000};}
 await check('verified matches unlock missions but do not auto-pay',async()=>{const before=data.users.owner.metaEconomy.wallet.gold;const overview=await live.worldJourneyOverview('owner');assert.equal(overview.missions.length,8);assert.equal(overview.missions.filter(x=>x.claimable).length,7);assert.equal(overview.completedMissions,7);assert.equal(overview.claimedRewards,0);assert.equal(data.users.owner.metaEconomy.wallet.gold,before);});
 await check('first seven missions are claimed only after explicit request',async()=>{let result;for(const id of ['two_matches','fifty_rolls','ten_developments','three_wins','twenty_turns','three_maps','networth_5000'])result=await live.worldJourneyClaim('owner',id);assert.equal(result.applied,true);assert.equal(Object.keys(data.users.owner.seasonalEvents[live.WORLD_EVENT_ID].history).length,7);});
 await check('world map is award 8; exclusive board is separate bonus',async()=>{let p=await live.worldJourneyOverview('owner');assert.equal(p.missions[7].claimable,true);await live.worldJourneyClaim('owner','world_explorer');assert.equal(data.users.owner.metaEconomy.inventory.maps.map_world,true);p=await live.worldJourneyOverview('owner');assert.equal(p.bonus.claimable,true);await live.worldJourneyClaim('owner','completion_bonus');assert.equal(data.users.owner.metaEconomy.inventory.boards.board_atlas_horizon,true);assert.equal(Object.keys(data.users.owner.seasonalEvents[live.WORLD_EVENT_ID].history).length,9);});
 await check('duplicate event reward is idempotent and never credits twice',async()=>{const gold=data.users.owner.metaEconomy.wallet.gold;const r=await live.worldJourneyClaim('owner','two_matches');assert.equal(r.applied,false);assert.equal(r.idempotentReplay,true);assert.equal(data.users.owner.metaEconomy.wallet.gold,gold);});
 await check('XP from claimed rewards remains UID-specific bonus source',async()=>assert.equal(data.users.owner.seasonalRewards.xp,1500));
 await check('server date gate closes on July 1, 2027 UTC',async()=>{Date.now=()=>Date.UTC(2027,6,1,0,0,0);const view=await live.worldJourneyOverview('owner');assert.equal(view.event.active,false);assert.equal(view.missions.length,0);});
 await rejectsCode('reward cannot be claimed after event ends',()=>live.worldJourneyClaim('owner','completion_bonus'),'failed-precondition');
 await check('RTDB rules protect promo definitions and allow only own receipts',async()=>{const rules=JSON.parse(fs.readFileSync(path.resolve(__dirname,'../database.rules.json'),'utf8')).rules;assert.equal(rules.promoCodes['.read'],false);assert.equal(rules.promoCodes['.write'],false);for(const k of ['promoRedemptions','seasonalRewards','seasonalEvents']){assert.equal(rules.users['$uid'][k]['.write'],false);assert.ok(rules.users['$uid'][k]['.read'].includes('auth.uid === $uid'));}});
 console.log('WORLD PROMO CONTRACT PASS checks='+passed);
})().catch(e=>{console.error(e);process.exitCode=1}).finally(()=>{Date.now=realDateNow;});
