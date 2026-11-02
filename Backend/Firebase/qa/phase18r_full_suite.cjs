'use strict';
const {spawnSync}=require('node:child_process');
const path=require('node:path');
const suites=[
  require('./phase18r_01_connectivity_ownership_e2e.test.cjs'),
  require('./phase18r_02_match_lifecycle_migration_e2e.test.cjs'),
  require('./phase18r_03_economy_pulse_bank_civic_e2e.test.cjs'),
  require('./phase18r_04_trade_auction_event_intents_e2e.test.cjs'),
  require('./phase18r_05_stress_reset_latency_e2e.test.cjs')
];
(async()=>{
  const started=Date.now();
  let checks=0,httpOps=0;
  console.log('============================================================');
  console.log('ATB_PHASE18R_FULL_SUITE START auth+database emulator only');
  console.log('============================================================');
  for(let i=0;i<suites.length;i++){
    console.log(`\n######## SUITE ${i+1}/${suites.length} ########`);
    const r=await suites[i].run();checks+=r.checks||0;httpOps+=r.httpOps||0;
  }
  console.log('\n######## REGRESSION: phase19ai_emulator_security ########');
  const existing=path.join(__dirname,'phase19ai_emulator_security.test.cjs');
  const child=spawnSync(process.execPath,[existing],{stdio:'inherit',env:process.env});
  if(child.status!==0) throw new Error(`phase19ai_emulator_security regression failed exit=${child.status}`);
  console.log('\n============================================================');
  console.log(`ATB_PHASE18R_FULL_SUITE PASS phase18Checks=${checks} phase18HttpOps=${httpOps} durationMs=${Date.now()-started} phase19ai=PASS production=NOT_TOUCHED UNITY_RUNTIME=NOT_RUN`);
  console.log('============================================================');
})().catch(e=>{console.error(e.stack||e);process.exitCode=1;});
