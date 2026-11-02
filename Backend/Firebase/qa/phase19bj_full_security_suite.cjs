'use strict';
const {spawnSync}=require('node:child_process');
const path=require('node:path');
const {emulatorGuard}=require('./phase19bj_security_shared.cjs');
const s1=require('./phase19bj_01_identity_privacy_profiles_e2e.test.cjs');
const s2=require('./phase19bj_02_room_authority_directory_replay_e2e.test.cjs');
const s3=require('./phase19bj_03_chat_emote_abuse_e2e.test.cjs');
const s4=require('./phase19bj_04_wallet_inventory_fuzz_e2e.test.cjs');
const s5=require('./phase19bj_05_trust_boundaries_probe_e2e.test.cjs');
async function run(){
 emulatorGuard();const started=Date.now();let checks=0,httpOps=0;
 console.log('============================================================');console.log('ATB_PHASE19BJ_FULL_SECURITY_SUITE START');console.log('============================================================');
 for(const [i,fn] of [s1.run,s2.run,s3.run,s4.run].entries()){console.log(`\n######## SECURITY SUITE ${i+1}/4 ########`);const r=await fn();checks+=r.checks;httpOps+=r.httpOps;}
 console.log('\n######## REGRESSION: phase19ai Career/Gold security ########');
 const legacy=spawnSync(process.execPath,[path.join(__dirname,'phase19ai_emulator_security.test.cjs')],{stdio:'inherit',env:process.env});if(legacy.status!==0)throw new Error(`phase19ai_emulator_security failed exit=${legacy.status}`);
 console.log('\n######## TRUST BOUNDARY PROBE ########');const b=await s5.run();checks+=b.checks;httpOps+=b.httpOps;
 console.log('\n============================================================');
 console.log(`ATB_PHASE19BJ_FULL_SECURITY_SUITE HARDENING_PASS checks=${checks} httpOps=${httpOps} durationMs=${Date.now()-started} phase19ai=PASS publicDirectoryHardened=true production=NOT_TOUCHED`);
 console.log('SECURITY_OPEN_BOUNDARIES modifiedHostSemanticTrust=true authenticatedIntentRateLimit=true appCheckProductionEnforcement=MANUAL_CONSOLE_VERIFY');
 console.log('RELEASE_SECURITY_STATUS=NOT_FINAL_PASS_UNTIL_OPEN_BOUNDARIES_AND_APPCHECK_DECISION_REVIEWED');
 console.log('============================================================');
 return{checks,httpOps};
}
module.exports={run};if(require.main===module)run().catch(e=>{console.error(e.stack||e);process.exitCode=1;});
