'use strict';
// Runs only against local Auth+RTDB emulators. Never touches real credentials.
const assert=require('node:assert/strict'),crypto=require('node:crypto'),fs=require('node:fs'),path=require('node:path');
const host=process.env.FIREBASE_DATABASE_EMULATOR_HOST,ah=process.env.FIREBASE_AUTH_EMULATOR_HOST;
const project=process.env.GCLOUD_PROJECT||process.env.FIREBASE_PROJECT_ID||'demo-atlasboard';
if(!host||!ah||!project.startsWith('demo-'))throw Error('DEMO Firebase Auth+RTDB emulator required');
const base=`http://${host}`,ns=project+'-default-rtdb';
const weeks={...JSON.parse(fs.readFileSync(path.join(__dirname,'..','weeklyMissionCatalog_initial.json'))),...JSON.parse(fs.readFileSync(path.join(__dirname,'..','weeklyMissionCatalog_extension.json')))};
const catalog=JSON.parse(fs.readFileSync(path.join(__dirname,'..','weeklyRewardCatalog_initial.json')));
const week=new Date();week.setUTCHours(0,0,0,0);week.setUTCDate(week.getUTCDate()-((week.getUTCDay()+6)%7));
const weekId=week.toISOString().slice(0,10).replace(/-/g,'');
assert.ok(weeks[weekId]&&catalog[weekId],'Current week is published');
let passCount=0;function pass(label){console.log('PASS:',label);passCount++}
async function req(method,p,token,body,headers={}){
 const url=base+'/'+p+'.json?ns='+encodeURIComponent(ns)+(token&&token!='owner'?'&auth='+encodeURIComponent(token):'');
 const resp=await fetch(url,{method,headers:{...(token==='owner'?{Authorization:'Bearer owner'}:{}),
  ...(body!==undefined?{'Content-Type':'application/json'}:{}),...headers},
  ...(body!==undefined?{body:JSON.stringify(body)}:{})});
 const raw=await resp.text();let value=raw;try{value=JSON.parse(raw)}catch{}
 return {status:resp.status,body:value,etag:resp.headers.get('etag')||''};
}
async function must(p,label){const r=await p;assert.equal(r.status,200,label+' HTTP '+r.status+' '+JSON.stringify(r.body));pass(label);return r;}
async function deny(p,label){const r=await p;assert.ok([400,401,403,412].includes(r.status),label+' unexpectedly '+r.status+' '+JSON.stringify(r.body));pass(label);}
async function signup(){const n=crypto.randomBytes(7).toString('hex');const r=await fetch('http://'+ah+'/identitytoolkit.googleapis.com/v1/accounts:signUp?key=demo',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email:'dj-'+n+'@example.invalid',password:'DemoPass2026!',returnSecureToken:true})});assert.equal(r.status,200);let x=await r.json();return {uid:x.localId,token:x.idToken}}
function variant(uid){return /^[A-Fa-f0-3]/.test(uid)?'a':/^[G-Mg-m4-6]/.test(uid)?'b':/^[N-Tn-t7-8]/.test(uid)?'c':'d'}
function missionReceipt(u,slot,id,override={}){const def=catalog[weekId][slot];return {
 weekId,slot,matchId:id,kind:def.kind,amount:def.amount,claimedAt:{'.sv':'timestamp'},...override};}
const fields={
 profile_frames:['frame_halloween','frame_new_year','frame_turkiye','frame_usa','frame_neon_circuit','frame_royal_crown','frame_ocean_tide','frame_sakura_blossom','frame_cosmic_nova','frame_forest_spirit'],
 boards:['board_midnight_azure','board_ember_maple','board_arctic_aurora'],
 dice:['dice_ruby_red','dice_ocean_blue','dice_midnight_black','dice_marble'],
 pawns:['ghost_01','ghost_02','ghost_03','ghost_04','ghost_05','ghost_06','ghost_07','vehicle_police','vehicle_taxi','vehicle_pickup'],
 emotes:['emote_alert','emote_anger','emote_bars','emote_cash','emote_circle','emote_cloud','emote_cross','emote_dots1','emote_dots2','emote_dots3','emote_drop','emote_drops','emote_exclamation','emote_exclamations','emote_heart','emote_heartBroken','emote_hearts','emote_idea','emote_laugh','emote_music','emote_question','emote_sleep','emote_sleeps','emote_star','emote_stars','emote_swirl'],
 maps:['map_germany','map_mexico']};
function blankWallet(){const inventory={};for(const [k,v]of Object.entries(fields))inventory[k]=Object.fromEntries(v.map(x=>[x,false]));return {
 schemaVersion:1,promoXp:0,rewardHistory:'',wallet:{gold:100,atlasCoin:0,starterGrantClaimed:false},
 inventory,equipped:{profileFrameId:'',boardId:'',diceId:''},dailyReward:{lastClaimAt:0},
 dev:{grantCount:0,resetCount:0},operation:{id:crypto.randomBytes(16).toString('hex'),type:'INIT',itemId:'',atMs:Date.now()-5000}
};}
function next(doc,id,def,override={}){let w=structuredClone(doc);w.operation={id:crypto.randomBytes(16).toString('hex'),type:'WEEKLY_REWARD',itemId:id,atMs:Date.now()};w.rewardHistory=(w.rewardHistory||'')+'|'+id+'|';if(def.kind==='xp')w.promoXp=(w.promoXp||0)+def.amount;else w.wallet.gold+=def.amount;return {...w,...override};}
(async()=>{
 const alice=await signup(),bob=await signup();
 for(const u of [alice,bob])await must(req('PUT','users/'+u.uid,'owner',{
  userId:u.uid,playerId:'ATB-'+u.uid.slice(0,8),email:'qa@example.invalid',displayName:'Weekly QA',createdAt:Date.now(),updatedAt:Date.now(),lastDisplayNameChangeAt:0,general:{accountType:'registered'}}),'Admin seeds isolated user');
 await must(req('PUT','weeklyMissionCatalog/'+weekId,'owner',weeks[weekId]),'Admin seeds unchanged mission schedule');
 await must(req('PUT','weeklyRewardCatalog/'+weekId,'owner',catalog[weekId]),'Admin publishes payout amounts');
 await deny(req('PATCH','weeklyRewardCatalog/'+weekId,alice.token,{s0:{kind:'gold',amount:999999}}),'User cannot reroll payout catalog');
 let a='users/'+alice.uid+'/weeklyMissions/'+weekId+'/assignment';
 await must(req('PUT',a,alice.token,{weekId,variant:variant(alice.uid),startUtcMs:weeks[weekId].startUtcMs,endUtcMs:weeks[weekId].endUtcMs,assignedAt:{'.sv':'timestamp'}},{'If-Match':'null_etag'}),'Stable weekly assignment');
 const mid=crypto.randomBytes(16).toString('hex');
 const claim={uid:alice.uid,matchId:mid,atMs:Date.now(),playerCount:3,result:'win',rounds:30,diceRolls:30,properties:5,turns:35};
 await deny(req('PUT','careerClaims/'+alice.uid+'/'+mid,alice.token,claim),'Unverified Career claim denied');
 await must(req('PUT','careerClaims/'+alice.uid+'/'+mid,'owner',claim),'Admin seeds signed Career proof fixture');
 const id0=weekId+'s0',rp='users/'+alice.uid+'/weeklyRewardReceipts/'+id0;
 await deny(req('PUT',rp,bob.token,missionReceipt(alice,'s0',mid)),'No cross-account reward receipt');
 await deny(req('PUT',rp,alice.token,missionReceipt(alice,'s0',crypto.randomBytes(16).toString('hex'))),'No fabricated match proof');
 await deny(req('PUT',rp,alice.token,missionReceipt(alice,'s0',mid,{amount:500000})),'No arbitrary XP amount');
 await deny(req('PUT',rp,alice.token,missionReceipt(alice,'s0',mid,{amount:351})),'351 XP over hard limit is denied');
 assert.ok(catalog[weekId].s0.amount>=150&&catalog[weekId].s0.amount<=350);pass('Operator flagship reward is 150–350 XP in official Auth+RTDB emulator');
 await deny(req('PUT',rp,alice.token,missionReceipt(alice,'s0',mid,{extra:true})),'No extra receipt fields');
 await must(req('PUT',rp,alice.token,missionReceipt(alice,'s0',mid),{'If-Match':'null_etag'}),'Verified mission reward receipt');
 await deny(req('PUT',rp,alice.token,missionReceipt(alice,'s0',mid)),'Receipt cannot be duplicated');
 await deny(req('DELETE',rp,alice.token),'Receipt cannot be deleted');
 await must(req('PUT','users/'+alice.uid+'/metaEconomy','owner',blankWallet()),'Admin creates isolated canonical wallet fixture');
 let pathWallet='users/'+alice.uid+'/metaEconomy',w=(await must(req('GET',pathWallet,alice.token,undefined,{'X-Firebase-ETag':'true'}),'Wallet GET ETag works')).body;
 const walletId=id0,def=catalog[weekId].s0;
 await deny(req('PUT',pathWallet,alice.token,next(w,weekId+'s2',def)),'No payout without matching receipt');
 let cheated=next(w,walletId,def);cheated.promoXp+=5000;
 await deny(req('PUT',pathWallet,alice.token,cheated),'Cannot increase XP beyond published amount');
 cheated=next(w,walletId,def);cheated.wallet.gold+=5000;
 await deny(req('PUT',pathWallet,alice.token,cheated),'Cannot mint Gold with XP reward');
 cheated=next(w,walletId,def);cheated.inventory.pawns.ghost_01=true;
 await deny(req('PUT',pathWallet,alice.token,cheated),'Cannot add cosmetic ownership to payout');
 await must(req('PUT',pathWallet,alice.token,next(w,walletId,def)),'XP credited through canonical ETag wallet');
 w=(await must(req('GET',pathWallet,alice.token),'Wallet state readback')).body;
 assert.equal(w.promoXp,def.amount); assert.ok(w.rewardHistory.includes('|'+walletId+'|'));pass('XP bonus applies once and receipt ledger is exact');
 await deny(req('PUT',pathWallet,alice.token,next(w,walletId,def)),'Duplicate XP wallet payment denied');
 const id3=weekId+'s3',rp3='users/'+alice.uid+'/weeklyRewardReceipts/'+id3;
 await must(req('PUT',rp3,alice.token,missionReceipt(alice,'s3',mid),{'If-Match':'null_etag'}),'Fourth objective verified for Gold');
 const gDef=catalog[weekId].s3;
 await must(req('PUT',pathWallet,alice.token,next(w,id3,gDef)),'Gold credited into real canonical wallet');
 w=(await must(req('GET',pathWallet,alice.token),'Read Gold balance after credit')).body;
 assert.equal(w.wallet.gold,100+gDef.amount);pass('Gold is spendable canonical balance (not UI shadow)');
 await deny(req('PUT',pathWallet,bob.token,next(w,id3,gDef)),'Bob cannot mutate Alice wallet');
 await deny(req('PATCH',rp,alice.token,{amount:9999}),'Receipt cannot be modified to raise reward');
 console.log(`FIREBASE_AUTH_RTDB_WEEKLY_REWARDS=${passCount}/${passCount} PASS`);
})().catch(e=>{console.error('WEEKLY_REWARD_EMULATOR_FAIL:',e.stack||e.message);process.exitCode=1});
