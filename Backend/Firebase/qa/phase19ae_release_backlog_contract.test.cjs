'use strict';
const fs=require('fs'),path=require('path'),assert=require('assert');
const root=path.resolve(__dirname,'../../..');
const read=f=>fs.readFileSync(path.join(root,f),'utf8');
const rules=JSON.parse(read('Backend/Firebase/database.rules.json')).rules;
let syntaxCount=0;
function check(o){if(!o||typeof o!=='object')return;for(const [k,v] of Object.entries(o)){if(['.read','.write','.validate'].includes(k)&&typeof v==='string'){new Function('return ('+v+');');syntaxCount++;}else if(typeof v==='object')check(v);}}
check(rules);assert(syntaxCount>300);
const hist=rules.users.$uid.metaHistory;
assert.deepEqual(hist['.indexOn'],['atMs']);
// Model Firebase Realtime Database string.matches in the local rule harness.
String.prototype.matches=function(pattern){return pattern.test(String(this));};
// Actual function with the wildcard variable explicitly supplied.
const write=new Function('auth','data','newData','root','$uid','$opId','now',`return (${hist.$opId['.write']});`);
class Snap{constructor(v){this.v=v;} val(){return this.v==null?null:this.v;} exists(){return this.v!=null;} child(path){let v=this.v;for(const k of String(path).split('/'))v=v==null?undefined:v[k];return new Snap(v);} }
const op='1'.repeat(32),uid='owner',atMs=1800000001000;
const rootData={users:{owner:{metaEconomy:{operation:{id:op,type:'PURCHASE',itemId:'map_germany',atMs}}}}};
const auth={uid:'owner'},data=new Snap(null),rootSnap=new Snap(rootData);
const receipt={id:op,type:'PURCHASE',itemId:'map_germany',atMs};
const verify=(before,after,u='owner',opId=op,a=auth,rd=rootSnap)=>Boolean(write(a,new Snap(before),new Snap(after),rd,u,opId,atMs));
assert.equal(verify(null,receipt),true,'matching legitimate purchase receipt');
assert.equal(verify(receipt,receipt),false,'immutable receipt');
assert.equal(verify(null,{...receipt,itemId:'map_mexico'}),false,'cannot substitute an item');
assert.equal(verify(null,{...receipt,type:'DAILY'}),false,'cannot substitute receipt type');
assert.equal(verify(null,{...receipt,atMs:atMs+1}),false,'cannot change timestamp');
assert.equal(verify(null,receipt,'another'),false,'no other UID');
assert.equal(verify(null,receipt,'owner','not-an-op-id'),false,'invalid ID rejected');
assert.equal(verify(null,receipt,'owner',op,{uid:'attacker'}),false,'foreign auth denied');
const econ=read('Assets/Project/Scripts/Meta/AtlasBoardRtdbMetaEconomy.cs');
for(const token of ['ReadHistoryAsync','TryWriteReceiptAsync','HistoryPath','wallet remains committed','limitToLast=100','Type = receipt.type']) assert(econ.includes(token),token);
const ach=read('Assets/Project/Scripts/Progression/AtlasBoardCareerAchievementDisplay.cs');
const steam=read('Assets/Project/Scripts/Platform/AtlasBoardSteamAchievementCatalog.cs');
const ids=[...steam.matchAll(/new Entry\(\s*"([a-z0-9_]+)"/g)].map(x=>x[1]);
assert.equal(ids.length,14,'canonical 14 IDs');
for(const id of ids)assert(ach.includes('D("'+id+'"'),id);
assert(ach.includes('unlocked = server != null && server.unlocked'));
const ui=read('Assets/Project/Scripts/UI/Progression/AtlasBoardProgressionUI.cs');
assert(ui.includes('MergeVerified('));assert(ui.includes('AtlasBoardCareerAchievementDisplay.Title'));
const store=read('Assets/Project/Scripts/UI/Meta/AtlasBoardMetaEconomyUI.cs');
assert(store.includes('AtlasBoardReleaseUiText.NoEvents()'));
const recorder=read('Assets/Project/Scripts/Progression/AtlasBoardProgressionMatchRecorder.cs');
assert(recorder.includes('trusted_verifier_required'));
console.log(`Phase 19AE SOURCE CONTRACT PASS: ${syntaxCount} RTDB expression syntax checks, 8 receipt attack/positive cases, 14 achievements, 4 UI/backend invariants`);
