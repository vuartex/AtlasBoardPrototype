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
 if(pass)assert(result.status>=200&&result.status<300,`${label}: expected allow; HTTP ${result.status} ${result.body.slice(0,180)}`);
 else assert(result.status===401||result.status===403,`${label}: expected denied; HTTP ${result.status} ${result.body.slice(0,180)}`);
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
function profile(user,name){
 const at=ms();return {userId:user.uid,playerId:'ATB-'+hash(user.uid).slice(0,20).toUpperCase(),email:user.email,
  displayName:name,createdAt:at-100000,updatedAt:at,lastDisplayNameChangeAt:at-100000,
  general:{schemaVersion:1,accountType:user.email?'registered':'guest',language:'en',countryCode:'US',lastSeenAt:at}};
}
function startWallet(){
 const econ=rules.users.$uid.metaEconomy;const inv=defaults(econ.inventory);
 return {schemaVersion:1,wallet:{gold:0,atlasCoin:0,starterGrantClaimed:false},inventory:inv,
   equipped:{profileFrameId:'',boardId:'',diceId:''},dailyReward:{lastClaimAt:0},
   dev:{grantCount:0,resetCount:0},operation:{id:xid(),type:'INIT',itemId:'',atMs:ms()},rewardHistory:''};
}
function advance(w,type,itemId){const x=structuredClone(w);x.operation={id:xid(),type,itemId:itemId||'',atMs:Math.max(ms(),w.operation.atMs+1)};return x;}
function gold(kind,rounds){return Math.min(50,Math.max(0,rounds)+(kind==='win'?25:kind==='tie'?15:10));}
function xp(kind,rounds,properties,developments){return 25+(kind==='win'?50:kind==='tie'?20:0)+Math.min(100,rounds*3)+Math.min(50,properties*5)+Math.min(75,developments*3);}
(async()=>{
 const alice=await signup('phase19ai-alice');const bob=await signup('phase19ai-bob');
 const guest=await signup('phase19ai-guest',true);const stranger=await signup('phase19ai-stranger');
 const mid='match_'+xid();const room='room_'+xid();const now=ms();
 const roomMembers={
  [alice.uid]:{uid:alice.uid,active:true,role:'host',joinedAt:now-90000,displayName:'Alice'},
  [bob.uid]:{uid:bob.uid,active:true,role:'remoteHuman',joinedAt:now-90000,displayName:'Bob'},
  [guest.uid]:{uid:guest.uid,active:true,role:'remoteHuman',joinedAt:now-90000,displayName:'Guest'}
 };
 const seatWire={seats:[
  {uid:alice.uid,ownerUid:alice.uid,seatId:'seat_0',mode:'hostlocal',name:'Alice',controller:'human',connection:'connected',slotIndex:0,host:true,afkLockedOut:false,reconnectExpiresAt:0,disconnectedAt:0,removalReason:''},
  {uid:bob.uid,ownerUid:bob.uid,seatId:'seat_1',mode:'remotehuman',name:'Bob',controller:'human',connection:'connected',slotIndex:1,host:false,afkLockedOut:false,reconnectExpiresAt:0,disconnectedAt:0,removalReason:''},
  {uid:guest.uid,ownerUid:guest.uid,seatId:'seat_2',mode:'remotehuman',name:'Guest',controller:'human',connection:'connected',slotIndex:2,host:false,afkLockedOut:false,reconnectExpiresAt:0,disconnectedAt:0,removalReason:''}
 ]};
 const match={matchId:mid,lobbyId:room,hostUid:alice.uid,status:'active',phase:'playing',createdAt:now-90000,updatedAt:now,hostHeartbeatAt:now,revision:42,eventSequence:42,authorityEpoch:1,networkSchemaVersion:1,seatsJson:JSON.stringify(seatWire),snapshotJson:'{}'};
 expect(true,await db('PUT','roomMembers/'+room,'owner',roomMembers),'seed room');
 expect(true,await db('PUT','matchState/'+mid,'owner',match),'seed match');
 const participation=(u,slot,role)=>({uid:u.uid,matchId:mid,lobbyId:room,slotIndex:slot,role,atMs:ms()});
 const a=participation(alice,0,'host'),b=participation(bob,1,'remoteHuman'),g=participation(guest,2,'remoteHuman');
 expect(true,await db('PUT','careerParticipation/'+alice.uid+'/'+mid,alice,a),'host signs before finish');
 expect(true,await db('PUT','careerParticipation/'+bob.uid+'/'+mid,bob,b),'registered peer signs before finish');
 expect(true,await db('PUT','careerParticipation/'+guest.uid+'/'+mid,guest,g),'anonymous Firebase Guest signs same durable participation');
 expect(false,await db('PUT','careerParticipation/'+alice.uid+'/'+mid,alice,a),'immutable participation replay');
 expect(false,await db('PUT','careerParticipation/'+bob.uid+'/'+mid,stranger,b),'foreign UID participation');
 const result={valid:true,participating:[true,true,true,false],winnerSlots:[0],netWorth:[1100,400,350,0],cash:[800,300,250,0],propertyCount:[4,2,1,0],developmentLevels:[5,0,1,0]};
 const diceRolls=[20,15,12,0],doubles=[3,2,1,0];
 const frame={phase:'match_complete',currentRound:12,completedTurns:32,roundLimit:20,matchResult:result,careerDiceRolls:diceRolls,careerDoublesRolled:doubles};
 const snapshotJson=JSON.stringify(frame);const seatsJson=JSON.stringify(seatWire);
 expect(true,await db('PATCH','matchState/'+mid,'owner',{status:'complete',phase:'match_complete',revision:43,eventSequence:43,snapshotJson,seatsJson,updatedAt:ms()}),'admin simulates canonical final frame');
 const archive={matchId:mid,lobbyId:room,hostUid:alice.uid,snapshotJson,seatsJson,digest:hash(snapshotJson),mapId:'map_turkey',themeId:'classic_table',roundLimit:20,revision:43,eventSequence:43,authorityEpoch:1,networkSchemaVersion:1,createdAt:match.createdAt,completedAt:ms()};
 expect(false,await db('PUT','completedMatchArchives/'+mid,bob,archive),'non-host cannot create completed archive');
 expect(true,await db('PUT','completedMatchArchives/'+mid,alice,archive),'final host creates immutable completed archive');
 expect(false,await db('PUT','completedMatchArchives/'+mid,alice,{...archive,mapId:'map_usa'}),'completed archive immutable');
 const att=u=>({uid:u.uid,matchId:mid,lobbyId:room,digest:archive.digest,revision:43,atMs:ms()});
 expect(true,await db('PUT','careerAttestations/'+mid+'/'+alice.uid,alice,att(alice)),'host attests archive before proof');
 expect(true,await db('PUT','careerAttestations/'+mid+'/'+bob.uid,bob,att(bob)),'registered peer attests archive before proof');
 expect(true,await db('PUT','careerAttestations/'+mid+'/'+guest.uid,guest,att(guest)),'anonymous Guest attests same archive before proof');
 const seats={};for(let i=0;i<4;i++)seats[String(i)]={result:i===0?'win':'loss',participating:i<3,properties:result.propertyCount[i],developments:result.developmentLevels[i],cash:result.cash[i],netWorth:result.netWorth[i],diceRolls:diceRolls[i],doublesRolled:doubles[i]};
 const proof={matchId:mid,lobbyId:room,hostUid:alice.uid,witnessUid:bob.uid,revision:43,digest:archive.digest,snapshotJson,atMs:ms(),seats,rounds:12,turns:32,playerCount:3,mapId:'map_turkey',themeId:'classic_table',roundLimit:20};
 expect(false,await db('PUT','careerFinalProofs/'+mid,bob,proof),'non-host cannot forge final proof');
 expect(true,await db('PUT','careerFinalProofs/'+mid,alice,proof),'host proof derives from immutable archive plus peer attestation');
 expect(false,await db('PUT','careerFinalProofs/'+mid,alice,{...proof,mapId:'map_usa'}),'completed proof immutable');
 const makeClaim=(user,other,slot,kind,recovered)=>({uid:user.uid,matchId:mid,lobbyId:room,witnessUid:other.uid,digest:proof.digest,revision:43,atMs:ms(),result:kind,slotIndex:slot,rounds:12,turns:32,properties:seats[String(slot)].properties,developments:seats[String(slot)].developments,cash:seats[String(slot)].cash,netWorth:seats[String(slot)].netWorth,playerCount:3,mapId:'map_turkey',themeId:'classic_table',roundLimit:20,hostAccountId:alice.uid,diceRolls:seats[String(slot)].diceRolls,doublesRolled:seats[String(slot)].doublesRolled,recovered,xpAwarded:xp(kind,12,seats[String(slot)].properties,seats[String(slot)].developments),goldAwarded:gold(kind,12)});
 const aClaim=makeClaim(alice,bob,0,'win',false);const bClaim=makeClaim(bob,alice,1,'loss',true);const gClaim=makeClaim(guest,alice,2,'loss',true);
 expect(false,await db('PUT','careerClaims/'+alice.uid+'/'+mid,bob,aClaim),'other UID cannot claim Alice reward');
 expect(false,await db('PUT','careerClaims/'+alice.uid+'/'+mid,alice,{...aClaim,goldAwarded:51}),'Gold cap cannot be forged');
 expect(true,await db('PUT','careerClaims/'+alice.uid+'/'+mid,alice,aClaim),'host full XP+Gold claim');
 expect(true,await db('PUT','careerClaims/'+bob.uid+'/'+mid,bob,bClaim),'registered peer recovers against archive-backed proof');
 expect(true,await db('PUT','careerClaims/'+guest.uid+'/'+mid,guest,gClaim),'anonymous Guest receives same UID-bound claim path');
 // Wallet parent profile remains mandatory for registered and Guest identities.
 const aliceStart=startWallet();
 expect(false,await db('PUT','users/'+alice.uid+'/metaEconomy',alice,aliceStart),'wallet without canonical parent profile denied');
 expect(true,await db('PUT','users/'+alice.uid,'owner',profile(alice,'Alice')),'seed registered profile');
 expect(true,await db('PUT','users/'+alice.uid+'/metaEconomy',alice,aliceStart),'registered wallet init');
 const guestStart=startWallet();
 expect(true,await db('PUT','users/'+guest.uid,'owner',profile(guest,'Guest')),'seed anonymous canonical profile');
 expect(true,await db('PUT','users/'+guest.uid+'/metaEconomy',guest,guestStart),'Guest wallet init under same schema');
 async function rewardWallet(user,start,claim,label){
  const wallet='users/'+user.uid+'/metaEconomy';const first=await db('GET',wallet,user);expect(true,first,label+' read ETag');
  let payout=advance(start,'MATCH_REWARD',mid);payout.wallet.gold+=claim.goldAwarded;payout.rewardHistory='|'+mid+'|';
  expect(true,await db('PUT',wallet,user,payout,first.etag),label+' exact MATCH_REWARD');
  let again=advance(payout,'MATCH_REWARD',mid);again.wallet.gold+=claim.goldAwarded;again.rewardHistory+='|'+mid+'|';
  expect(false,await db('PUT',wallet,user,again),label+' duplicate match reward denied');
  return payout;
 }
 const alicePaid=await rewardWallet(alice,aliceStart,aClaim,'registered');
 const guestPaid=await rewardWallet(guest,guestStart,gClaim,'Guest');
 const foreign=advance(alicePaid,'MATCH_REWARD',mid);foreign.wallet.gold+=aClaim.goldAwarded;foreign.rewardHistory+='|'+mid+'|';
 expect(false,await db('PUT','users/'+alice.uid+'/metaEconomy',stranger,foreign),'foreign UID cannot mint Gold');
 assert.equal(alicePaid.wallet.gold,aClaim.goldAwarded);checks++;
 assert.equal(guestPaid.wallet.gold,gClaim.goldAwarded);checks++;
 console.log(`ATB_CAREER_GOLD_FIREBASE_EMULATOR PASS checks=${checks} claims=3 anonymousGuest=true archive=true preProofAttestation=true duplicateDenied=true registeredGold=${aClaim.goldAwarded} guestGold=${gClaim.goldAwarded} production=NOT_TOUCHED`);
})().catch(e=>{console.error(e.stack||e);process.exitCode=1;});
