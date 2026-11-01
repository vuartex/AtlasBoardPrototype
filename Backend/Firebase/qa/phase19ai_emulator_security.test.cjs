'use strict';
// LIVE LOCAL AUTH+DATABASE EMULATOR SECURITY TEST (never targets production).
// Run from Backend/Firebase with:
// firebase.cmd emulators:exec --only auth,database --project demo-atlasboard "node qa/phase19ai_emulator_security.test.cjs"
const assert=require('node:assert/strict');
const crypto=require('node:crypto');
const fs=require('node:fs');const path=require('node:path');
if (!process.env.FIREBASE_DATABASE_EMULATOR_HOST ||
    !process.env.FIREBASE_AUTH_EMULATOR_HOST ||
    process.env.GCLOUD_PROJECT !== 'demo-atlasboard')
  throw new Error('EMULATOR_ONLY: demo-atlasboard auth+database required; will not use production.');
const base='http://127.0.0.1:9000';
const authBase='http://127.0.0.1:9099';
const ns='demo-atlasboard-default-rtdb';
const rules=JSON.parse(fs.readFileSync(path.join(__dirname,'../database.rules.json'),'utf8')).rules;
let checks=0;let seq=0;
function xid(){return (++seq).toString(16).padStart(32,'0');}
function ms(){return Date.now();}
function hash(s){return crypto.createHash('sha256').update(s).digest('hex');}
async function signup(label,anonymous=false){
 const body=anonymous?{returnSecureToken:true}: {email:label+'@atlasboard.invalid',password:'Atlas-Emulator-Local-Only!12',returnSecureToken:true};
 const r=await fetch(authBase+'/identitytoolkit.googleapis.com/v1/accounts:signUp?key=local-demo',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
 const x=await r.json();assert(r.ok&&x.idToken&&x.localId,`signup ${label} ${r.status}`);return {uid:x.localId,token:x.idToken,email:body.email||''};
}
async function db(method,where,user,body,etag){
 const token=user==='owner'?null:user.token;
 const endpoint=base+'/'+where+'.json?ns='+ns+(token?'&auth='+encodeURIComponent(token):'');
 const headers={'Content-Type':'application/json'};
 if(user==='owner')headers.Authorization='Bearer owner';
 if(etag)headers['if-match']=etag;
 if(method==='GET')headers['X-Firebase-ETag']='true';
 const r=await fetch(endpoint,{method,headers,body:body===undefined?undefined:JSON.stringify(body),signal:AbortSignal.timeout(12000)});
 return {status:r.status,body:await r.text(),etag:r.headers.get('etag')};
}
function expect(pass,result,label){
 if(pass)assert(result.status>=200&&result.status<300,`${label}: expected allow; HTTP ${result.status} ${result.body.slice(0,140)}`);
 else assert(result.status===401||result.status===403,`${label}: expected denied; HTTP ${result.status} ${result.body.slice(0,140)}`);
 checks++;
}
function defaults(node){
 const out={};for(const [key,value] of Object.entries(node)){
   if(key.startsWith('.')||key.startsWith('$'))continue;
   if(value&&typeof value==='object'){
      const children=Object.keys(value).filter(x=>!x.startsWith('.')&&!x.startsWith('$'));
      if(children.length)out[key]=defaults(value);
      else if(value['.validate']?.includes('isBoolean'))out[key]=false;
      else if(value['.validate']?.includes('isNumber'))out[key]=0;
      else out[key]='';
   }
 }return out;
}
// A nested wallet write must preserve /users/{uid}'s .validate requirement.
// Reuse the canonical user fixture shape from Phase 19C REST security tests.
// Only the LOCAL demo emulator admin creates this seed profile.
function profile(user){
 const at=ms();
 return {userId:user.uid,playerId:'1234567890123456',email:user.email,
   displayName:'Atlas Tester',createdAt:at-100000,updatedAt:at,
   lastDisplayNameChangeAt:at-100000,
   general:{schemaVersion:1,accountType:user.email?'registered':'guest',
     language:'en',countryCode:'US',lastSeenAt:at}};
}
function startWallet(){
 const econ=rules.users.$uid.metaEconomy;
 const inv=defaults(econ.inventory);
 return {schemaVersion:1,wallet:{gold:0,atlasCoin:0,starterGrantClaimed:false},inventory:inv,
   equipped:{profileFrameId:'',boardId:'',diceId:''},dailyReward:{lastClaimAt:0},
   dev:{grantCount:0,resetCount:0},operation:{id:xid(),type:'INIT',itemId:'',atMs:ms()},rewardHistory:''};
}
function advance(w,type,itemId){const x=structuredClone(w);x.operation={id:xid(),type,itemId:itemId||'',atMs:Math.max(ms(),w.operation.atMs+1)};return x;}
function gold(kind,rounds,properties,developments){return (kind==='win'?50:kind==='tie'?20:5)+Math.min(30,rounds)+Math.min(20,properties*2)+Math.min(20,developments*2);}
function xp(kind,rounds,properties,developments){return 25+(kind==='win'?50:kind==='tie'?20:0)+Math.min(100,rounds*3)+Math.min(50,properties*5)+Math.min(75,developments*3);}
(async()=>{
 const alice=await signup('phase19ai-alice');const bob=await signup('phase19ai-bob');const stranger=await signup('phase19ai-stranger');const guest=await signup('phase19ai-guest',true);
 const mid='match_'+xid();const room='room_'+xid();const now=ms();
 const roomMembers={ [alice.uid]:{uid:alice.uid,active:true,role:'host',joinedAt:now-90000,displayName:'Alice'},[bob.uid]:{uid:bob.uid,active:true,role:'remoteHuman',joinedAt:now-90000,displayName:'Bob'}};
 const match={matchId:mid,lobbyId:room,hostUid:alice.uid,status:'active',phase:'playing',createdAt:now-90000,updatedAt:now,revision:42};
 expect(true,await db('PUT','roomMembers/'+room,'owner',roomMembers),'seed room');
 expect(true,await db('PUT','matchState/'+mid,'owner',match),'seed match');
 const a={uid:alice.uid,matchId:mid,lobbyId:room,slotIndex:0,role:'host',atMs:ms()};
 const b={uid:bob.uid,matchId:mid,lobbyId:room,slotIndex:1,role:'remoteHuman',atMs:ms()};
 expect(true,await db('PUT','careerParticipation/'+alice.uid+'/'+mid,alice,a),'host signs before finish');
 expect(true,await db('PUT','careerParticipation/'+bob.uid+'/'+mid,bob,b),'second human independently signs before finish');
 expect(false,await db('PUT','careerParticipation/'+alice.uid+'/'+mid,alice,a),'immutable participation replay');
 expect(false,await db('PUT','careerParticipation/'+bob.uid+'/'+mid,stranger,b),'foreign UID participation');
 expect(false,await db('PUT','careerParticipation/'+guest.uid+'/'+mid,guest,{...a,uid:guest.uid}),'anonymous without tester cannot sign');
 const result={valid:true,participating:[true,true,false,false],winnerSlots:[0],netWorth:[1100,400,0,0],cash:[800,300,0,0],propertyCount:[4,2,0,0],developmentLevels:[5,0,0,0]};
 const diceRolls=[20,15,0,0],doubles=[3,2,0,0];
 const frame={phase:'match_complete',currentRound:12,completedTurns:32,roundLimit:20,matchResult:result,careerDiceRolls:diceRolls,careerDoublesRolled:doubles};
 const snapshotJson=JSON.stringify(frame);
 expect(true,await db('PATCH','matchState/'+mid,'owner',{status:'complete',phase:'match_complete',revision:43,snapshotJson,updatedAt:ms()}),'admin simulates canonical final (NOT independent replay)');
 const seats={};for(let i=0;i<4;i++)seats[String(i)]={result:i===0?'win':'loss',participating:i<2,properties:result.propertyCount[i],developments:result.developmentLevels[i],cash:result.cash[i],netWorth:result.netWorth[i],diceRolls:diceRolls[i],doublesRolled:doubles[i]};
 const proof={matchId:mid,lobbyId:room,hostUid:alice.uid,witnessUid:bob.uid,revision:43,digest:hash(snapshotJson),snapshotJson,atMs:ms(),seats,rounds:12,turns:32,playerCount:2,mapId:'map_turkey',themeId:'classic_table',roundLimit:20};
 expect(false,await db('PUT','careerFinalProofs/'+mid,bob,proof),'guest cannot forge host proof');
 expect(true,await db('PUT','careerFinalProofs/'+mid,alice,proof),'host publishes immutable completed result proof');
 expect(false,await db('PUT','careerFinalProofs/'+mid,alice,{...proof,mapId:'map_usa'}),'cannot overwrite completed proof');
 // Alice first receives participation floor while waiting for Bob to sign final outcome.
 const part={uid:alice.uid,matchId:mid,witnessUid:bob.uid,digest:proof.digest,xpAwarded:1,atMs:ms()};
 expect(true,await db('PUT','careerParticipationCredits/'+alice.uid+'/'+mid,alice,part),'1 XP protected participation after completion');
 expect(false,await db('PUT','careerParticipationCredits/'+alice.uid+'/'+mid,alice,part),'1 XP cannot be claimed twice');
 const att=uid=>({uid,matchId:mid,lobbyId:room,digest:proof.digest,revision:43,atMs:ms()});
 expect(true,await db('PUT','careerAttestations/'+mid+'/'+alice.uid,alice,att(alice.uid)),'host signs result');
 const makeClaim=(user,other,slot,kind,recovered)=>({uid:user.uid,matchId:mid,lobbyId:room,witnessUid:other.uid,digest:proof.digest,revision:43,atMs:ms(),result:kind,slotIndex:slot,rounds:12,turns:32,properties:seats[String(slot)].properties,developments:seats[String(slot)].developments,cash:seats[String(slot)].cash,netWorth:seats[String(slot)].netWorth,playerCount:2,mapId:'map_turkey',themeId:'classic_table',roundLimit:20,hostAccountId:alice.uid,diceRolls:seats[String(slot)].diceRolls,doublesRolled:seats[String(slot)].doublesRolled,recovered,xpAwarded:xp(kind,12,seats[String(slot)].properties,seats[String(slot)].developments),goldAwarded:gold(kind,12,seats[String(slot)].properties,seats[String(slot)].developments)});
 let aClaim=makeClaim(alice,bob,0,'win',false);
 expect(false,await db('PUT','careerClaims/'+alice.uid+'/'+mid,alice,aClaim),'cannot full claim before Bob signs final');
 expect(true,await db('PUT','careerAttestations/'+mid+'/'+bob.uid,bob,att(bob.uid)),'Bob final attestation');
 aClaim=makeClaim(alice,bob,0,'win',false);
 expect(false,await db('PUT','careerClaims/'+alice.uid+'/'+mid,bob,aClaim),'other UID cannot claim Alice rewards');
 expect(false,await db('PUT','careerClaims/'+alice.uid+'/'+mid,alice,{...aClaim,result:'loss'}),'win cannot be rewritten as loss with same proof');
 expect(false,await db('PUT','careerClaims/'+alice.uid+'/'+mid,alice,{...aClaim,goldAwarded:120}),'Gold cap cannot be forged');
 expect(true,await db('PUT','careerClaims/'+alice.uid+'/'+mid,alice,aClaim),'host full XP+Gold claim');
 expect(false,await db('PUT','careerClaims/'+alice.uid+'/'+mid,alice,aClaim),'full claim immutable');
 const bClaim=makeClaim(bob,alice,1,'loss',true);
 expect(true,await db('PUT','careerClaims/'+bob.uid+'/'+mid,bob,bClaim),'disconnected user can recover against host proof and attestation');
 // Prove INIT is denied without a user profile; seed the profile first.
 const start=startWallet();
 expect(false,await db('PUT','users/'+alice.uid+'/metaEconomy',alice,start),
   'wallet without canonical parent profile must be denied');
 expect(true,await db('PUT','users/'+alice.uid,'owner',profile(alice)),
   'admin seeds canonical registered user profile');
 expect(true,await db('PUT','users/'+alice.uid+'/metaEconomy',alice,start),
   'initialize canonical zero Gold wallet after profile exists');
 const wallet='users/'+alice.uid+'/metaEconomy';const first=await db('GET',wallet,alice);expect(true,first,'read wallet ETag');
 let payout=advance(start,'MATCH_REWARD',mid);payout.wallet.gold+=aClaim.goldAwarded;payout.rewardHistory='|'+mid+'|';
 expect(false,await db('PUT',wallet,stranger,payout,first.etag),'foreign user cannot mint Gold');
 expect(true,await db('PUT',wallet,alice,payout,first.etag),'single exact Gold ETag wallet payout');
 let again=advance(payout,'MATCH_REWARD',mid);again.wallet.gold+=aClaim.goldAwarded;again.rewardHistory+='|'+mid+'|';
 expect(false,await db('PUT',wallet,alice,again),'duplicated same match cannot mint Gold');
 const daily=advance(payout,'DAILY','');daily.wallet.gold+=50;daily.dailyReward.lastClaimAt=daily.operation.atMs;
 expect(true,await db('PUT',wallet,alice,daily),'existing daily claim still works with reward marker');
 const tamper=advance(daily,'EQUIP_DICE','');tamper.rewardHistory='';
 expect(false,await db('PUT',wallet,alice,tamper),'legacy commerce cannot erase reward marker');
 const coins=advance(daily,'MATCH_REWARD',mid);coins.wallet.gold+=aClaim.goldAwarded;coins.wallet.atlasCoin++;
 expect(false,await db('PUT',wallet,alice,coins),'no Atlas Coin from match payout');
 const end=await db('GET',wallet,alice);expect(true,end,'read after payout');
 const goldAfter=JSON.parse(end.body).wallet.gold;assert.equal(goldAfter,aClaim.goldAwarded+50);checks++;
 console.log(`ATB_CAREER_GOLD_FIREBASE_EMULATOR PASS checks=${checks} claims=2 reconnect=true duplicateDenied=true gold=${aClaim.goldAwarded} verified=mock_completed_match antiCheat=PEER_ONLY production=NOT_TOUCHED`);
})().catch(e=>{console.error(e.stack||e);process.exitCode=1;});
