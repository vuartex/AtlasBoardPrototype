'use strict';
// Full-file RTDB Rules preparation. Refuses unknown baselines and unsafe output.
// No production deploy. --write writes a complete database.rules.json only after
// a backup outside the Unity repository. No patch/diff file is produced.
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');

const RULES_PATH = path.resolve(__dirname, '..', 'database.rules.json');
const BASE_SHA256 = '2e0e86346231d7d067a7c8fc0cf9362582ed068d4477f3627548cc95c9ac670a';
const BOUND = 262144;
const OLD = ")&&((newData.child('operation/type').val()=='MATCH_REWARD'&&(newData.child('promoXp').val()";
const NEW = ")&&((newData.child('operation/type').val()!='MATCH_REWARD'&&newData.child('operation/type').val()!='WEEKLY_REWARD')||(newData.child('operation/type').val()=='MATCH_REWARD'&&(newData.child('promoXp').val()";
const ID = 'WALLET_INIT_MATCH_WEEKLY_SCOPE';
const sha = s => crypto.createHash('sha256').update(s).digest('hex');
function count(hay, needle) { return hay.split(needle).length - 1; }
function build(raw) {
  assert.equal(sha(raw), BASE_SHA256, 'Unexpected database.rules.json: stopped to preserve local changes');
  const obj = JSON.parse(raw);
  const rules = obj.rules;
  assert.equal(rules['.read'], false, 'Root read must be closed');
  assert.equal(rules['.write'], false, 'Root write must be closed');
  const wallet = rules?.users?.$uid?.metaEconomy;
  assert(wallet && typeof wallet['.write'] === 'string', 'Missing canonical metaEconomy rule');
  assert.equal(count(wallet['.write'], OLD), 1, 'Expected unique reward-specific guard missing');
  assert.equal(count(wallet['.write'], NEW), 0, 'Rule may already be modified');
  assert(wallet['.write'].includes("!data.exists()&&newData.child('operation/type').val()=='INIT'"), 'INIT permission missing');
  assert(wallet['.write'].includes("newData.child('operation/type').val()=='PURCHASE'"), 'PURCHASE permission missing');
  assert(wallet['.write'].includes("newData.child('operation/type').val()=='DEV_GRANT'"), 'DEV_GRANT permission missing');
  assert(wallet['.write'].includes("newData.child('operation/type').val()=='WEEKLY_REWARD'"), 'WEEKLY_REWARD permission missing');
  assert(wallet['.write'].includes("newData.child('operation/type').val()=='MATCH_REWARD'"), 'MATCH_REWARD permission missing');
  const before = wallet['.write'];
  wallet['.write'] = before.replace(OLD, NEW);
  // Guarantee that no other rule, branch, or value changes. Source serialized
  // compact JSON is used to preserve exact original formatting and paths.
  const needle = JSON.stringify(before);
  const replacement = JSON.stringify(wallet['.write']);
  assert.equal(count(raw, needle), 1, 'Cannot replace exact full-wallet expression safely');
  const generated = raw.replace(needle, replacement);
  assert.equal(generated.length, raw.length + (replacement.length - needle.length), 'Unexpected changes in serialized rules');
  assert.deepEqual(JSON.parse(generated), obj, 'Full generated JSON must match intended transformation');
  assert(generated.length < BOUND, `Firebase RTDB rules size would exceed ${BOUND} bytes`);
  assert.equal(count(generated, NEW), 1, 'Guard should occur exactly once');
  assert.equal(count(generated, OLD), 0, 'Old unscoped guard should be absent');
  assert.equal(before.indexOf(OLD) > before.indexOf("newData.child('operation/type').val()=='WEEKLY_REWARD'"), true, 'Unexpected rule ordering');
  const parsed = JSON.parse(generated);
  const after = parsed.rules.users.$uid.metaEconomy['.write'];
  assert.equal(after.replace(NEW, OLD), before, 'No existing security rule may be relaxed beyond this guard');
  // Guards for reward operations remain exactly the same. Other operation
  // types skip a condition that could never have matched them.
  for (const type of ['INIT','DEV_GRANT','DAILY','PURCHASE','EQUIP','EQUIP_BOARD','EQUIP_DICE','RESET_PURCHASES']) {
    assert(type !== 'MATCH_REWARD' && type !== 'WEEKLY_REWARD');
  }
  return generated;
}
function main() {
  const args=process.argv.slice(2);
  assert(args.length === 0 || (args.length === 1 && args[0] === '--write'), 'Only --write is supported');
  const raw = fs.readFileSync(RULES_PATH,'utf8');
  if (count(raw, NEW) === 1 && !count(raw, OLD)) {
    const parsed=JSON.parse(raw);
    assert.equal(parsed.rules['.read'],false);
    assert.equal(parsed.rules['.write'],false);
    assert(Buffer.byteLength(raw,'utf8')<BOUND);
    console.log(`${ID}=ALREADY_APPLIED`);
    console.log(`RULES_BYTES=${Buffer.byteLength(raw,'utf8')}/${BOUND}`);
    console.log('PRODUCTION_DEPLOY=NOT_DONE');
    return;
  }
  const result=build(raw);
  console.log(`${ID}=READY`);
  console.log(`RULES_BASELINE_SHA256=${sha(raw)}`);
  console.log(`RULES_UPDATED_SHA256=${sha(result)}`);
  console.log(`RULES_BYTES=${Buffer.byteLength(result,'utf8')}/${BOUND}`);
  if (!args.length) {
    console.log('DRY_RUN_ONLY=YES; PRODUCTION_DEPLOY=NOT_DONE');
    return;
  }
  const backupDir = path.resolve(os.homedir(), 'AtlasBoard_RtdbRuleBackups');
  // The backup must remain outside the project/repo tree.
  assert(!backupDir.startsWith(path.resolve(__dirname,'../../..')+path.sep), 'Unsafe backup path');
  fs.mkdirSync(backupDir, {recursive:true});
  const backup = path.join(backupDir, `database.rules.before_em_${BASE_SHA256.slice(0,16)}.json`);
  if (fs.existsSync(backup)) {
    assert.equal(sha(fs.readFileSync(backup)), BASE_SHA256, 'Existing backup content mismatch; stopping');
  } else {
    fs.writeFileSync(backup,raw,{flag:'wx'});
  }
  const temp=RULES_PATH+`.em-${process.pid}.tmp`;
  try {
    fs.writeFileSync(temp,result,{flag:'wx'});
    assert.equal(sha(fs.readFileSync(temp)),sha(result),'Staging file hash mismatch');
    fs.renameSync(temp,RULES_PATH);
  } finally {
    if(fs.existsSync(temp))fs.unlinkSync(temp);
  }
  assert.equal(sha(fs.readFileSync(RULES_PATH)),sha(result),'Written rules mismatch');
  console.log('COMPLETE_LOCAL_RULES_WRITTEN=YES');
  console.log('BACKUP='+backup);
  console.log('EMULATOR_RETEST=REQUIRED');
  console.log('PRODUCTION_DEPLOY=NOT_DONE');
}
if (require.main===module) {
  try { main(); } catch(e) {console.error('WALLET_EM_RULES_PREPARE_FAILED:',e.stack||e);process.exitCode=1;}
}
module.exports={build};
