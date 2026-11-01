'use strict';
const assert=require('node:assert/strict');
if(!process.env.FIREBASE_DATABASE_EMULATOR_HOST||!process.env.FIREBASE_AUTH_EMULATOR_HOST||process.env.GCLOUD_PROJECT!=='demo-atlasboard')
  throw new Error('EMULATOR_ONLY: demo-atlasboard auth+database required; production is forbidden.');
const base='http://127.0.0.1:9000', authBase='http://127.0.0.1:9099', ns='demo-atlasboard-default-rtdb';
let checks=0;
async function signup(){const r=await fetch(authBase+'/identitytoolkit.googleapis.com/v1/accounts:signUp?key=local-demo',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({returnSecureToken:true})});const x=await r.json();assert(r.ok&&x.idToken&&x.localId);return{uid:x.localId,token:x.idToken};}
async function db(method,path,user,body){const headers={'Content-Type':'application/json'};let q='?ns='+ns;if(user==='owner')headers.Authorization='Bearer owner';else q+='&auth='+encodeURIComponent(user.token);const r=await fetch(base+'/'+path+'.json'+q,{method,headers,body:body===undefined?undefined:JSON.stringify(body)});return{status:r.status,body:await r.text()};}
function allow(r,label){assert(r.status>=200&&r.status<300,`${label}: expected allow HTTP ${r.status} ${r.body}`);checks++;}
function deny(r,label){assert(r.status===401||r.status===403,`${label}: expected deny HTTP ${r.status} ${r.body}`);checks++;}
(async()=>{
 const guest=await signup(); const room='room_guest_emote'; const match='match_guest_emote'; const now=Date.now();
 await allow(await db('PUT','roomMembers/'+room,'owner',{[guest.uid]:{uid:guest.uid,active:true,role:'host',joinedAt:now-1000,displayName:'Guest'}}),'seed room member');
 await allow(await db('PUT','roomMeta/'+room,'owner',{closed:false,activeMatchId:match}),'seed room meta');
 await allow(await db('PUT','matchState/'+match,'owner',{matchId:match,lobbyId:room,hostUid:guest.uid,status:'active',phase:'playing',revision:1,createdAt:now-1000,updatedAt:now}),'seed active match');
 const event=id=>({emoteId:id,atMs:Date.now(),seq:1,count:1,windowStart:Date.now(),muteUntil:0});
 await allow(await db('PUT','emoteEvents/'+room+'/'+guest.uid,guest,event('emote_faceHappy')),'anonymous active member can send bundled free emote without loadout');
 await allow(await db('DELETE','emoteEvents/'+room+'/'+guest.uid,'owner'),'admin clears fixture event');
 await deny(await db('PUT','emoteEvents/'+room+'/'+guest.uid,guest,event('emote_alert')),'anonymous user cannot send paid emote without equipped loadout');
 console.log(`ATB_PHASE19AS_GUEST_EMOTE_EMULATOR PASS checks=${checks} free_without_loadout=true paid_without_loadout_denied=true persistent_loadout_not_required=true production=NOT_TOUCHED`);
})().catch(e=>{console.error(e.stack||e);process.exitCode=1;});
