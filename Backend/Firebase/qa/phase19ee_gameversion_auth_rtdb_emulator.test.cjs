'use strict';
// EMULATOR ONLY. Tests the public read and client write-deny contract for the
// exact two-value policy, not an unattainable binary-authentication guarantee.
const {SecurityQa, emulatorGuard} = require('./phase19bj_security_shared.cjs');
const assert = require('node:assert/strict');
const config = require('../gameversion_initial.json');
async function run() {
  emulatorGuard();
  const qa = new SecurityQa('GAMEVERSION_READ_ONLY');
  assert.deepEqual(qa.rules.gameversion, {'.read':true,'.write':false},
    'Run the local Rules preparer --write before this emulator test');
  qa.setStage('public read and owner seed');
  qa.allow(await qa.db('PUT', 'gameversion', 'owner', config),
    'emulator administrator seeds the current/last version');
  qa.eq(qa.parse(qa.allow(await qa.db('GET','gameversion',null),
    'anonymous startup read allowed')), config,
    'startup read receives exact two-value policy');
  const alice=await qa.signup('gameversion_alice');
  const guest=await qa.signup('gameversion_guest',true);
  qa.eq(qa.parse(qa.allow(await qa.db('GET','gameversion',alice),
    'registered identity reads version policy')), config,
    'registered read matches canonical values');
  qa.allow(await qa.db('GET','gameversion',guest),'anonymous Auth identity reads policy');
  qa.setStage('client policy tamper denial');
  qa.deny(await qa.db('PUT','gameversion/currentgameversion',alice,'v0.9.9'),
    'registered client cannot advance server current version');
  qa.deny(await qa.db('PUT','gameversion/lastgameversion',guest,'v0.1.0'),
    'guest cannot downgrade previous version');
  qa.deny(await qa.db('DELETE','gameversion',alice),
    'client cannot delete server policy');
  qa.deny(await qa.db('PATCH','gameversion',alice,{currentgameversion:'v9.9.9'}),
    'client cannot patch server values');
  qa.eq(qa.parse(qa.allow(await qa.db('GET','gameversion',null),
    'policy remains readable')),config,'policy was not mutated by clients');
  qa.report({result:'PASS'});
  console.log('GAMEVERSION_AUTH_RTDB_EMULATOR_PASS=YES; PRODUCTION_DEPLOY=NOT_DONE');
}
if (require.main===module) run().catch(e=>{console.error('GAMEVERSION_EMULATOR_FAIL='+e.stack);process.exitCode=1;});
