'use strict';
// Emulator ONLY. Diagnostic overlays are restored before exit, including on errors.
// Never load this script into live Firebase and never relax production Rules.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');
const {SecurityQa, emulatorGuard, clone} = require('./phase19bj_security_shared.cjs');

const LOCAL = /^(127\.0\.0\.1|localhost):[0-9]+$/;
function localOnly() {
  emulatorGuard();
  assert(LOCAL.test(process.env.FIREBASE_DATABASE_EMULATOR_HOST || ''), 'RTDB must be a loopback emulator');
  assert(LOCAL.test(process.env.FIREBASE_AUTH_EMULATOR_HOST || ''), 'Auth must be a loopback emulator');
  assert.equal(process.env.GCLOUD_PROJECT, 'demo-atlasboard', 'Demo project required');
}
function canonical(x) {return JSON.stringify(x);}
function hex(x) {return crypto.createHash('sha256').update(canonical(x)).digest('hex');}
function disableMetaValidation(n) {
  for (const [key,value] of Object.entries(n)) {
    if (key === '.validate') n[key] = 'true';
    else if (value && typeof value === 'object') disableMetaValidation(value);
  }
}
function variant(original,name) {
  const rules=clone(original);
  const user=rules.users.$uid;
  const wallet=user.metaEconomy;
  if (name==='write_bypass' || name==='both_bypass') {
    wallet['.write']='auth!=null&&auth.uid==$uid';
  }
  if (name==='meta_validate_bypass' || name==='both_bypass') {
    disableMetaValidation(wallet);
  }
  if (name==='user_validate_bypass' || name==='both_bypass') {
    user['.validate']='true';
  }
  return rules;
}
async function run() {
  localOnly();
  const q=new SecurityQa('PHASE19EL_WALLET_INIT_PERMISSION_DIAGNOSTIC');
  const original=clone(q.rules);
  const rulesPath=path.join(__dirname,'../database.rules.json');
  const raw=fs.readFileSync(rulesPath,'utf8');
  const fixtureSha=crypto.createHash('sha256').update(raw).digest('hex');
  let rulesTouched=false;
  const results=[];
  async function inspectActiveRules() {
    const x=await q.db('GET','.settings/rules','owner',undefined,{label:'emulator.rules.inspect'});
    assert.equal(x.status,200,'Cannot read emulator rules; refusing to alter rules');
    const active=JSON.parse(x.body);
    assert(active && active.rules && active.rules.users, 'Unexpected emulator Rules response');
    return active.rules;
  }
  async function install(rules) {
    const response=await q.db('PUT','.settings/rules','owner',{rules},{label:'emulator.rules.replace'});
    assert(response.status>=200 && response.status<300,'Cannot replace emulator rules: HTTP '+response.status);
    rulesTouched=true;
  }
  async function probe(name) {
    const user=await q.signup('EL-'+name.replaceAll('_','').slice(0,14));
    const profile=q.profile(user,'Diagnostic');
    const seed=await q.db('PUT',`users/${user.uid}`,'owner',profile,{label:`${name}.profile_seed`});
    assert(seed.status>=200 && seed.status<300,`Cannot seed ${name} profile: HTTP ${seed.status}`);
    const wallet=q.startWallet();
    const r=await q.db('PUT',`users/${user.uid}/metaEconomy`,user,wallet,{label:`${name}.wallet_init`});
    results.push({name,status:r.status,allowed:r.status>=200&&r.status<300});
    console.log(`WALLET_INIT_PROBE ${name} HTTP=${r.status} accepted=${r.status>=200&&r.status<300}`);
    if (r.status!==200 && r.status!==401 && r.status!==403) {
      console.log(`WALLET_INIT_UNEXPECTED_STATUS=${r.status} body=${String(r.body).slice(0,170)}`);
    }
  }
  console.log('PRODUCTION=FORBIDDEN EMULATOR_ONLY=YES');
  console.log('LOCAL_DATABASE_RULES_SHA256='+fixtureSha);
  const before=await inspectActiveRules();
  assert.deepStrictEqual(before, original, 'Active emulator Rules differ from local database.rules.json; refusing overrides');
  try {
    await probe('baseline');
    for(const name of ['write_bypass','meta_validate_bypass','user_validate_bypass','both_bypass']) {
      await install(variant(original,name));
      await probe(name);
    }
  } finally {
    if (rulesTouched) {
      // Restore in a finally block even if a diagnostic probe failed.
      await install(original);
      const restored=await inspectActiveRules();
      assert.deepStrictEqual(restored,original,'CRITICAL: emulator rules restore mismatch');
      console.log('EMULATOR_RULES_RESTORED=YES');
    }
  }
  const status=Object.fromEntries(results.map(r=>[r.name,r.allowed]));
  let cause='INSUFFICIENT_EVIDENCE';
  if(status.baseline) cause='INTERMITTENT_OR_CONTEXT_DIFFERENCE';
  else if(status.write_bypass && !status.meta_validate_bypass) cause='ORIGINAL_META_WRITE_EXPRESSION';
  else if(status.meta_validate_bypass && !status.write_bypass) cause='META_ECONOMY_VALIDATION';
  else if(status.user_validate_bypass && !status.write_bypass && !status.meta_validate_bypass) cause='USER_ROOT_VALIDATION';
  else if(!status.write_bypass && !status.meta_validate_bypass && status.both_bypass) cause='MULTIPLE_RESTRICTIONS';
  else if(status.write_bypass && status.meta_validate_bypass) cause='WRITE_AND_VALIDATION_BOTH_CAN_BYPASS';
  console.log('WALLET_INIT_DIAGNOSTIC_CLASSIFICATION='+cause);
  console.log('WALLET_INIT_SECURITY_PASS=NOT_CLAIMED');
  console.log('FIREBASE_PRODUCTION_CHANGED=NO');
  console.log('NEXT=Inspect classification before changing production intended Rules or tests');
}
if(require.main===module) run().catch(err=>{console.error('DIAGNOSTIC_FAILED:',err.stack||err);process.exitCode=1});
module.exports={run,variant,disableMetaValidation};
