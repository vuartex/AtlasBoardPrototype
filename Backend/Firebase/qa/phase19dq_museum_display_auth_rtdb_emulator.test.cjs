'use strict';
// Auth + RTDB emulator ONLY. No production RTDB, Cloud Functions or wallet grants.
const {SecurityQa,emulatorGuard}=require('./phase19bj_security_shared.cjs');
const assert=require('node:assert/strict');
const {display}=require('./phase19dq_museum_rules_prepare.cjs');
async function run(){
  emulatorGuard();
  const qa=new SecurityQa('MUSEUM_DQ_EQUIP_AUTH_RTDB');
  qa.ok(JSON.stringify(qa.rules.users.$uid.collectibleDisplay)===JSON.stringify(display),
    'exact Museum rules installed before emulator test');
  qa.setStage('two account identities and operator-only seed');
  const alice=await qa.signup('museum_dq_owner');
  const bob=await qa.signup('museum_dq_other');
  qa.allow(await qa.db('PUT',`users/${alice.uid}`,'owner',qa.profile(alice,'Museum Owner')), 'admin seed owner profile');
  qa.allow(await qa.db('PUT',`users/${bob.uid}`,'owner',qa.profile(bob,'Museum Other')), 'admin seed other profile');
  const id='weekly_20320209';
  const receipt={weekId:'20320209',tokenId:id,source:'museumQa',acquiredAt:Date.now()};
  qa.allow(await qa.db('PUT',`users/${alice.uid}/collectibleTokens/${id}`,'owner',receipt),'operator seed private receipt');
  const own=`users/${alice.uid}/collectibleDisplay`;
  qa.allow(await qa.db('GET',`users/${alice.uid}/collectibleTokens`,alice),'owner can see owned tokens');
  qa.deny(await qa.db('GET',`users/${alice.uid}/collectibleTokens`,bob),'other account denied ownership read');
  qa.deny(await qa.db('PUT',`users/${alice.uid}/collectibleTokens/weekly_20320223`,alice,
      {tokenId:'weekly_20320223',weekId:'20320223',source:'museumQa',acquiredAt:Date.now()}),
    'client may not mint QA ownership');
  qa.setStage('atomic slot writes');
  const first=await qa.db('GET',own,alice,undefined,{wantEtag:true});
  qa.allow(first,'authenticated read first loadout');
  qa.ok(first.etag,'GET returns ETag');
  const original={s0:id,s1:'',s2:''};
  qa.allow(await qa.db('PUT',own,alice,original,{etag:first.etag}),'one owned token in slot 0');
  qa.eq(qa.parse(await qa.db('GET',own,alice)),original,'display persisted');
  qa.deny(await qa.db('PUT',own,bob,{s0:'',s1:'',s2:''}),
    'other account cannot edit display');
  qa.deny(await qa.db('PUT',own,alice,{s0:id,s1:id,s2:''}),
    'same token cannot be duplicated into two slots');
  qa.deny(await qa.db('PUT',own,alice,{s0:'weekly_20320223',s1:'',s2:''}),
    'unowned token rejected');
  qa.deny(await qa.db('PUT',own,alice,{s0:id,s1:''}),
    'missing required slot rejected');
  qa.deny(await qa.db('PUT',own,alice,{s0:id,s1:'',s2:'',s3:'bad'}),
    'unknown fourth slot rejected');
  qa.deny(await qa.db('PUT',own,alice,{s0:id,s1:1,s2:''}),
    'non-string slot rejected');
  const beforeMove=await qa.db('GET',own,alice,undefined,{wantEtag:true});
  qa.allow(beforeMove,'read for atomic move');
  qa.allow(await qa.db('PUT',own,alice,{s0:'',s1:'',s2:id},{etag:beforeMove.etag}),
    'atomic move token from slot 0 to slot 2');
  qa.precondition(await qa.db('PUT',own,alice,original,{etag:beforeMove.etag}),
    'stale ETag cannot overwrite concurrent slot state');
  qa.allow(await qa.db('PUT',own,alice,{s0:'',s1:'',s2:''}),
    'remove owned token from display');
  qa.eq(qa.parse(await qa.db('GET',own,alice)),{s0:'',s1:'',s2:''},
    'removal saved without changing immutable ownership');
  const after=qa.parse(await qa.db('GET',`users/${alice.uid}/collectibleTokens/${id}`,alice));
  qa.eq(after,receipt,'equip and remove never alter ownership');
  qa.report({result:'PASS'});
  console.log('MUSEUM_DQ_AUTH_RTDB_EMULATOR_PASS=YES; LIVE_FIREBASE_DEPLOY=NOT_DONE');
}
if(require.main===module)run().catch(e=>{console.error('MUSEUM_DQ_EMULATOR_FAIL='+e.stack);process.exitCode=1;});
