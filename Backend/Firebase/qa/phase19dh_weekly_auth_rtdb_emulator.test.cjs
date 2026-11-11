'use strict';
// Official Auth + RTDB emulator test. ALL endpoints are localhost emulator
// endpoints; no production project, service account or production data access.
const assert=require('node:assert/strict');
const crypto=require('node:crypto');
const fs=require('node:fs'),path=require('node:path');
const dbHost=process.env.FIREBASE_DATABASE_EMULATOR_HOST;
const authHost=process.env.FIREBASE_AUTH_EMULATOR_HOST;
const project=process.env.GCLOUD_PROJECT||process.env.FIREBASE_PROJECT_ID||'demo-atlasboard';
if(!dbHost||!authHost||!project.startsWith('demo-'))throw Error('DEMO Auth + RTDB emulators required; production prohibited');
const ns=project+'-default-rtdb';
const base=`http://${dbHost}`;
const id=(()=>{const today=new Date();let x=new Date(Date.UTC(today.getUTCFullYear(),today.getUTCMonth(),today.getUTCDate()));x.setUTCDate(x.getUTCDate()-((x.getUTCDay()+6)%7));return x.toISOString().slice(0,10).replace(/-/g,'')})();
const weekMs=604800000;
const fakeUid=()=>crypto.randomBytes(16).toString('hex');
let count=0;
const pass=t=>{count++;console.log('PASS:',t)};
const request=async(method,loc,token,body,headers={})=>{
 const url=base+`/${loc}.json?ns=${encodeURIComponent(ns)}`+
  (token&&token!=='owner'?'&auth='+encodeURIComponent(token):'');
 const r=await fetch(url,{method,headers:{...(body===undefined?{}:{'Content-Type':'application/json'}),
  ...(token==='owner'?{Authorization:'Bearer owner'}:{}),...headers},
  ...(body===undefined?{}:{body:JSON.stringify(body)})});
 let value;const raw=await r.text();try{value=JSON.parse(raw)}catch{value=raw}
 return {status:r.status,value};
};
// Callers supply fetch-backed Promises. Always resolve them BEFORE examining
// status, otherwise r.status is undefined and the suite fails before testing
// a single RTDB rule (v5.49.12dh regression).
const must=async(pending,t)=>{
 const r=await pending;
 assert.ok(r&&typeof r.status==='number',t+': missing resolved HTTP response');
 assert.equal(r.status,200,t+': HTTP '+r.status+' '+JSON.stringify(r.value));
 pass(t);return r.value;
};
const deny=async(pending,t)=>{
 const r=await pending;
 assert.ok(r&&typeof r.status==='number',t+': missing resolved HTTP response');
 assert.ok([400,401,403,412].includes(r.status),t+': expected denial but '+r.status+' '+JSON.stringify(r.value));
 pass(t);
};
const db=(method,p,who,body,h)=>request(method,p,who,body,h);
async function signup(){const em=crypto.randomBytes(7).toString('hex');const r=await fetch(`http://${authHost}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=emulator-key`,{
 method:'POST',headers:{'Content-Type':'application/json'},
 body:JSON.stringify({email:`atlas-weekly-${em}@example.invalid`,password:'safeTest1234',returnSecureToken:true})});
 assert.equal(r.status,200,'Firebase Auth test user registration');const x=await r.json();return {uid:x.localId,token:x.idToken};}
function variant(uid){return /^[A-Fa-f0-3]/.test(uid)?'a':/^[G-Mg-m4-6]/.test(uid)?'b':/^[N-Tn-t7-8]/.test(uid)?'c':'d'}
function assignment(user,w,override){return {weekId:id,variant:override||variant(user.uid),startUtcMs:w.startUtcMs,endUtcMs:w.endUtcMs,assignedAt:{'.sv':'timestamp'}};}
function proof(uid,matchId,values={}){return {uid,matchId,atMs:Date.now(),playerCount:3,result:'win',rounds:25,diceRolls:30,properties:10,doublesRolled:2,turns:40,...values}}
const claim=(id0,tokenId,extra={})=>({proofs:{s0:id0,s1:id0,s2:id0,s3:id0},tokenId,claimedAt:{'.sv':'timestamp'},...extra});
const token=(id,tokenId)=>({weekId:id,tokenId,acquiredAt:{'.sv':'timestamp'},source:'weeklyMissions'});
const cond='If-Match';
(async()=>{
 const cat=JSON.parse(fs.readFileSync(path.join(__dirname,'..','weeklyMissionCatalog_initial.json'),'utf8'));
 assert.ok(cat[id], 'Current UTC week must be in the prepublished calendar');
 const alice=await signup(),bob=await signup(),eve=await signup();
 const week={...cat[id],hasTokenReward:true,tokenId:'weekly_'+id};
 await must(db('GET','users/'+alice.uid,'owner'),'Demo owner bearer allowed for setup');
 for(const u of [alice,bob,eve]){
  await must(db('PUT','users/'+u.uid,'owner',{
   userId:u.uid,playerId:'ATB-'+u.uid.substring(0,8),email:'test@example.invalid',displayName:'QA User',
   createdAt:Date.now(),updatedAt:Date.now(),lastDisplayNameChangeAt:0,
   general:{accountType:'registered'}
  }),'Admin seeds isolated QA user');
 }
 await must(db('PUT','weeklyMissionCatalog/'+id,'owner',week),'Admin publishes fixed weekly manifest');
 await deny(db('PUT','weeklyMissionCatalog/'+id,alice.token,{hacked:true}),'Players cannot alter token-week schedule');
 const a=`users/${alice.uid}/weeklyMissions/${id}/assignment`;
 const b=`users/${bob.uid}/weeklyMissions/${id}/assignment`;
 await deny(db('PUT',b,alice.token,assignment(bob,week)),'No cross-UID mission assignment');
 const wrong=['a','b','c','d'].filter(x=>x!==variant(alice.uid))[0];
 await deny(db('PUT',a,alice.token,assignment(alice,week,wrong)),'Cannot pick an easier UID variant');
 await deny(db('PUT',a,alice.token,{...assignment(alice,week),extraGold:50000}),'No injected assignment fields');
 await must(db('PUT',a,alice.token,assignment(alice,week),{[cond]:'null_etag'}),'Verified UID gets stable first weekly assignment');
 await deny(db('PUT',a,alice.token,assignment(alice,week)),'Assignment cannot be rerolled');
 await deny(db('DELETE',a,alice.token),'Assignment cannot be deleted');
 await must(db('PUT',`users/${alice.uid}/weeklyMissions/${id}/lastSeenServerAt`,
   alice.token,{'.sv':'timestamp'}),'Server timestamp recorded on meaningful refresh');
 await deny(db('PUT',`users/${alice.uid}/weeklyMissions/${id}/lastSeenServerAt`,
   alice.token,12345),'Client clock cannot forge lastSeenServerAt');
 const match=fakeUid(),p=`careerClaims/${alice.uid}/${match}`;
 await deny(db('PUT',p,alice.token,proof(alice.uid,match)),'Forged unsigned Career claim denied by existing rules');
 await must(db('PUT',p,'owner',proof(alice.uid,match)),'Admin seeds previously verified claim fixture');
 const receipt=`users/${alice.uid}/weeklyMissions/${id}/claim`;
 await deny(db('PUT',receipt,bob.token,claim(match,week.tokenId)),'Another UID cannot claim Alice reward');
 await deny(db('PUT',receipt,alice.token,claim(fakeUid(),week.tokenId)),'Claim without signed match denied');
 await deny(db('PUT',receipt,alice.token,claim(match,week.tokenId,{bonus:999})),'Extra reward payload fields forbidden');
 await deny(db('PUT',receipt,alice.token,claim(match,'weekly_unrelated')),'Unpublished token cannot be requested');
 await must(db('PUT',receipt,alice.token,claim(match,week.tokenId),{[cond]:'null_etag'}),'Four manifest objectives validated by signed Career claim');
 await deny(db('PUT',receipt,alice.token,claim(match,week.tokenId)),'Reward receipt immutable once per UID/week');
 await deny(db('DELETE',receipt,alice.token),'Cannot remove reward receipt');
 const owned=`users/${alice.uid}/collectibleTokens/${week.tokenId}`;
 await deny(db('PUT',`users/${eve.uid}/collectibleTokens/${week.tokenId}`,
  eve.token,token(id,week.tokenId)),'No collectible entitlement without own verified claim');
 await deny(db('PUT',owned,bob.token,token(id,week.tokenId)),'No foreign UID token grant');
 await must(db('PUT',owned,alice.token,token(id,week.tokenId),{[cond]:'null_etag'}),'Permanent cosmetic token owned through verified receipt');
 await deny(db('PUT',owned,alice.token,token(id,week.tokenId)),'No duplicate token ownership writes');
 await deny(db('DELETE',owned,alice.token),'Ownership is permanent and nondeletable by client');
 await deny(db('PUT',`users/${alice.uid}/collectibleTokens/forged_token`,alice.token,
  token(id,'forged_token')),'Cannot manufacture arbitrary cosmetic tokens');
 // A no-token week is playable but may not mint a token. Owner fixture
 // temporarily changes this demo manifest; user clients cannot change it.
 await must(db('PUT',`weeklyMissionCatalog/${id}`,'owner',{...week,hasTokenReward:false,tokenId:''}),
   'Demo-only administrator switches to a no-token scenario');
 await must(db('PUT',b,bob.token,assignment(bob,week),{[cond]:'null_etag'}),
   'Normal mission assignment remains available on non-token week');
 const bm=fakeUid();await must(db('PUT',`careerClaims/${bob.uid}/${bm}`,'owner',proof(bob.uid,bm)),
   'Demo verified Career fixture for Bob');
 await deny(db('PUT',`users/${bob.uid}/weeklyMissions/${id}/claim`,bob.token,
   claim(bm,week.tokenId)),'Non-token weeks can never grant collectibles');
 await must(db('PUT',`weeklyMissionCatalog/${id}`,'owner',{...week,endUtcMs:Date.now()-1}),
   'Demo-only weekly expiry scenario published');
 await deny(db('PUT',`users/${eve.uid}/weeklyMissions/${id}/assignment`,eve.token,
   assignment(eve,week)),'Expired week cannot be newly assigned');
 console.log(`FIREBASE_AUTH_RTDB_WEEKLY_EMULATOR=${count}/${count} PASS`);
})().catch(e=>{console.error('WEEKLY_EMULATOR_FAIL:',e.stack||e.message);process.exitCode=1});
