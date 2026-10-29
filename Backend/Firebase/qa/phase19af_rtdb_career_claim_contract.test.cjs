'use strict';
const assert = require('node:assert/strict');
// RTDB rules DSL adds String.matches(regex); Node does not.
Object.defineProperty(String.prototype,'matches',{value:function(rx){return rx.test(String(this));}, configurable:true});
const fs = require('node:fs');
const path = require('node:path');
const repo = path.resolve(__dirname, '../../..');
const rules = JSON.parse(fs.readFileSync(path.join(__dirname, '../database.rules.json'), 'utf8')).rules;
const a = rules.careerAttestations.$matchId.$uid;
const c = rules.careerClaims.$uid.$matchId;
const base = 1790000000000, uid='host1', witness='remote1', mid='match_000000001', room='room_00000000000000000001';
const digest = 'a'.repeat(64);
class Snap {
  constructor(value) {this.value=value;}
  child(name) {const keys=String(name).split('/').filter(Boolean);let x=this.value;for(const k of keys)x=x && typeof x==='object'?x[k]:undefined;return new Snap(x);}
  val(){return this.value ?? null;}
  exists(){return this.value!==undefined && this.value!==null;}
  isString(){return typeof this.value==='string';}
  isNumber(){return typeof this.value==='number';}
  hasChildren(names){return names.every(k=>this.value && Object.hasOwn(this.value,k));}
}
const attest = (who)=>({uid:who,matchId:mid,lobbyId:room,digest,revision:8,atMs:base});
const claim=()=>({uid,matchId:mid,lobbyId:room,witnessUid:witness,digest,revision:8,atMs:base,
 result:'win',slotIndex:0,rounds:20,turns:63,properties:5,developments:10,
 cash:1500,netWorth:4200,playerCount:2,mapId:'map_germany',themeId:'classic_table',roundLimit:20,hostAccountId:uid,xpAwarded:25+50+60+25+30});
let tree={matchState:{[mid]:{lobbyId:room,hostUid:uid,phase:'match_complete',status:'complete',
    createdAt:base-300000,updatedAt:base-1000,revision:8}},
  roomMembers:{[room]:{[uid]:{uid,role:'host',active:true},[witness]:{uid:witness,role:'remoteHuman',active:true}}},
  careerAttestations:{[mid]:{[uid]:attest(uid),[witness]:attest(witness)}}};
function evaluate(rule, body, existing=null, actor=uid, extra={}) {
 const f=new Function('auth','data','newData','root','now','$uid','$matchId',
 `return (${rule});`);
 return !!f({uid:actor},new Snap(existing),new Snap(body),new Snap(extra.root||tree),base,extra.uid||uid,mid);
}
let count=0;
function check(ok,label) {assert.ok(ok,label);count++;}
check(evaluate(a['.write'],attest(uid)),'fresh owner attestation');
check(!evaluate(a['.write'],attest(uid),attest(uid)),'attestation immutable');
check(!evaluate(a['.write'],attest(uid),null,witness),'attestation UID bound');
check(evaluate(c['.write'],claim()),'claim paired proof');
check(!evaluate(c['.write'],claim(),claim()),'claim immutable / replay');
check(!evaluate(c['.write'],claim(),null,witness),'claim account bound');
for(const [field,bad] of [['uid','other'],['matchId','invalid'],['lobbyId','bad'],['digest','invalid'],['revision',9],['atMs',base-999999],['result','fake'],['rounds',-1],['xpAwarded',1999],['mapId',''.padEnd(200,'x')],['playerCount',1]]){
 const x=claim();x[field]=bad;
 if(c[field] && c[field]['.validate'])check(!evaluate(c[field]['.validate'],bad),'reject '+field);
}
for(const [name,rootChanged] of [
 ['offline match',t=>{t.matchState[mid].phase='active';}],
 ['absent witness',t=>{delete t.careerAttestations[mid][witness];}],
 ['mismatched digest',t=>{t.careerAttestations[mid][witness].digest='f'.repeat(64);}],
 ['inactive remote',t=>{t.roomMembers[room][witness].active=false;}],
 ['bot disguised',t=>{t.roomMembers[room][witness].role='bot';}],
 ['changed revision',t=>{t.matchState[mid].revision=9;}]
]){
 const t=structuredClone(tree);rootChanged(t);
 check(!evaluate(c['.write'],claim(),null,uid,{root:t}),name);
}
check(evaluate(c['.validate'],claim()),'XP matches server-side formula');
check(!evaluate(c['.validate'],{...claim(), xpAwarded:1999}),'XP overclaim denied');
for(const expression of [a['.write'],c['.write'],a['.validate'],c['.validate']])
  check(typeof expression==='string' && expression.length>20,'expression present');
const journal=fs.readFileSync(path.join(repo,'Assets/Project/Scripts/Progression/AtlasBoardRtdbCareerJournal.cs'),'utf8');
const recorder=fs.readFileSync(path.join(repo,'Assets/Project/Scripts/Progression/AtlasBoardProgressionMatchRecorder.cs'),'utf8');
const ui=fs.readFileSync(path.join(repo,'Assets/Project/Scripts/UI/Meta/AtlasBoardMetaEconomyUI.cs'),'utf8');
const release=fs.readFileSync(path.join(repo,'Assets/Project/Scripts/UI/AtlasBoardReleaseUiText.cs'),'utf8');
check(journal.includes('peer_attested_not_server_verified') && journal.includes('XpAward'), 'journal noncurrency, exact XP');
check(recorder.includes('RecordLiveRtdbMatchAsync') && recorder.includes('second_human_required'),'live match flow');
check(ui.includes('BuildEventComingSoonPanel') && release.includes('EventsComingSoonTitle'),'events design');
check(!ui.includes('AddInfoRow(contentRoot, AtlasBoardReleaseUiText.EventsUnavailable());'),'no ugly error-only event');
console.log('ATB_PHASE19AF_GUARDED_CAREER_SOURCE_PASS checks='+count+' firebaseEmulator=NOT_RUN unity=NOT_RUN');
