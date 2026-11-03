'use strict';
/**
 * AUTH + RTDB EMULATORS ONLY. No production API keys, tokens, or real accounts.
 * Verifies the canonical anonymous->email upgrade does not create a second UID.
 * Does not claim to reproduce every production email-enumeration setting.
 */
const assert = require('node:assert/strict');
const env = process.env;
const PROJECT = 'demo-atlasboard';
assert.equal(env.GCLOUD_PROJECT, PROJECT, 'Refusing non-demo Firebase project');
assert.equal(env.FIREBASE_AUTH_EMULATOR_HOST, '127.0.0.1:9099', 'Refusing non-local Auth');
assert.equal(env.FIREBASE_DATABASE_EMULATOR_HOST, '127.0.0.1:9000', 'Refusing non-local RTDB');
const api = 'http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:';
const suffix = '?key=not-a-production-key';
const rt = 'http://127.0.0.1:9000/users/';
let assertions=0;
async function request(url, data, expected=200, method='POST') {
 const r=await fetch(url,{method,headers:{'Content-Type':'application/json'},body:JSON.stringify(data)});
 const text=await r.text();
 let json;try{json=JSON.parse(text)}catch{json={error:text.slice(0,100)}}
 assert.equal(r.status,expected,`HTTP mismatch ${method} ${url.split('?')[0]} status=${r.status} cause=${json.error?.message||'unknown'}`);
 return json;
}
async function main(){
 const first = await request(api+'signUp'+suffix,{returnSecureToken:true});
 assert.ok(first.localId && first.idToken && first.refreshToken);assertions++;
 const uid=first.localId;const now=Date.now();
 const profile={userId:uid,playerId:'ATB-LOCALQA-0001',email:'',displayName:'GuestQA',
  createdAt:now,updatedAt:now,lastDisplayNameChangeAt:now,
  general:{schemaVersion:1,accountType:'guest',language:'en',countryCode:'ZZ',lastSeenAt:now}};
 await request(rt+encodeURIComponent(uid)+'.json?auth='+encodeURIComponent(first.idToken)+'&ns='+PROJECT+'-default-rtdb',profile,200,'PUT');
 const email='atb-emulator-'+Date.now()+'@example.com'; const password='qaStrong!987654';
 const linked = await request(api+'signUp'+suffix,{idToken:first.idToken,email,password,returnSecureToken:true});
 assert.equal(linked.localId,uid,'CRITICAL: upgrade must preserve anonymous UID');assertions++;
 assert.ok(linked.idToken && linked.refreshToken);assertions++;
 const patch={'email':email,'general/accountType':'registered','updatedAt':Date.now(),
  'general/lastSeenAt':Date.now()};
 await request(rt+encodeURIComponent(uid)+'.json?auth='+encodeURIComponent(linked.idToken)+'&ns='+PROJECT+'-default-rtdb',patch,200,'PATCH');
 const again = await request(api+'signInWithPassword'+suffix,{email,password,returnSecureToken:true});
 assert.equal(again.localId,uid,'CRITICAL: registered sign-in must restore original UID');assertions++;
 const profileResponse=await fetch(rt+encodeURIComponent(uid)+'.json?auth='+encodeURIComponent(again.idToken)+'&ns='+PROJECT+'-default-rtdb');
 const persisted=await profileResponse.json();
 assert.equal(persisted.general.accountType,'registered');assertions++;
 assert.equal(persisted.userId,uid);assertions++;
 console.log(`ATB_PHASE19BS_GUEST_UPGRADE_EMULATOR PASS checks=${assertions} uid_same=true profile_registered=true production=NOT_TOUCHED`);
}
main().catch(e=>{console.error('ATB_PHASE19BS_GUEST_UPGRADE_EMULATOR FAIL',e.message);process.exitCode=1});
