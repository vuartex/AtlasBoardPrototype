'use strict';
const {QaContext}=require('./phase18r_shared.cjs');

async function run(){
  const q=new QaContext('PHASE18R_04_TRADE_AUCTION_EVENT_INTENTS');
  console.log('ATB_PHASE18R_04 START production=FORBIDDEN');
  q.setStage('auth_room_match');
  const host=await q.signup('P18R04-Host');
  const p2=await q.signup('P18R04-P2');
  const p3=await q.signup('P18R04-P3');
  const attacker=await q.signup('P18R04-Attacker');
  const room=await q.createRoom([host,p2,p3],{mapId:'Turkey',roundLimit:20});
  const created=await q.createMatch(room);
  let frame=created.frame;let state=created.state;

  const scenarios=[
    {name:'purchase',kind:'purchase',slot:1,aux:-1,tile:4,v:[250,0,0,0],texts:['BUY','DECLINE','',''],actions:['buy','decline']},
    {name:'auction',kind:'auction',slot:1,aux:2,tile:7,v:[100,25,500,0],texts:['CITY_07','P2','P3',''],actions:['bid','pass']},
    {name:'trade_offer',kind:'trade',slot:1,aux:2,tile:6,v:[150,80,0,0],texts:['OFFER','REQUEST','',''],actions:['accept','reject']},
    {name:'travel',kind:'travel',slot:2,aux:-1,tile:12,v:[75,2,0,0],texts:['TRAVEL','CIVIC_2','',''],actions:['accept','decline']},
    {name:'event_choice',kind:'event',slot:1,aux:-1,tile:18,v:[1,2,3,4],texts:['EVENT','A','B','C'],actions:['choice_a','choice_b']},
    {name:'special',kind:'special',slot:2,aux:-1,tile:21,v:[10,20,0,0],texts:['SPECIAL','','',''],actions:['continue']}
  ];

  q.setStage('blocking_decision_mirror_and_intents');
  let intentCounter=0;
  for(const s of scenarios){
    frame={...frame,decisionKind:s.kind,decisionPlayerSlotIndex:s.slot,decisionAuxSlotIndex:s.aux,decisionTileIndex:s.tile,
      decisionValue0:s.v[0],decisionValue1:s.v[1],decisionValue2:s.v[2],decisionValue3:s.v[3],decisionReady:true,
      decisionText0:s.texts[0],decisionText1:s.texts[1],decisionText2:s.texts[2],decisionText3:s.texts[3]};
    state={...state,revision:state.revision+1,eventSequence:state.eventSequence+1,snapshotJson:JSON.stringify(frame),updatedAt:q.now(),hostHeartbeatAt:q.now()};
    const mirrorStart=performance.now();
    q.allow(await q.db('PUT',`matchState/${created.matchId}`,host,state,{label:`decision.${s.name}.publish`}),'Host publishes blocking decision');
    const target=s.slot===1?p2:p3;
    const seen=await q.getJson(`matchState/${created.matchId}`,target,`decision.${s.name}.read`);
    const sf=JSON.parse(seen.snapshotJson);
    q.eq(sf.decisionKind,s.kind,`${s.name} decisionKind mirror`);
    q.eq(sf.decisionPlayerSlotIndex,s.slot,`${s.name} target slot mirror`);
    q.eq(sf.decisionTileIndex,s.tile,`${s.name} tile mirror`);
    console.log(`DECISION_MIRROR kind=${s.kind} target=${target.label} firebaseMirrorMs=${(performance.now()-mirrorStart).toFixed(1)}`);

    for(const action of s.actions){
      const id=q.id(`decision${++intentCounter}`,20);
      const payload={kind:s.kind,action,slotIndex:s.slot,tileIndex:s.tile,targetSlotIndex:s.aux,offeredTileIndex:s.kind==='trade'?6:-1,offeredCash:s.kind==='trade'?150:0,requestedTileIndex:s.kind==='trade'?9:-1,requestedCash:s.kind==='trade'?80:0,value:''};
      const intent=q.intent(target,'decision',payload,{seatId:`seat_${s.slot}`});
      const t0=performance.now();
      q.allow(await q.db('PUT',`intents/${room.roomId}/${target.uid}/${id}`,target,intent,{label:`decision.${s.name}.${action}.put`}),'target submits own decision intent');
      q.deny(await q.db('PUT',`intents/${room.roomId}/${target.uid}/${id}`,target,intent,{label:`decision.${s.name}.${action}.duplicate`}),'same intent path is immutable');
      const hostRead=await q.getJson(`intents/${room.roomId}/${target.uid}/${id}`,host,`decision.${s.name}.${action}.host_read`);
      q.eq(JSON.parse(hostRead.payload).action,action,'Host sees exact decision action');
      q.deny(await q.db('PUT',`intentAcks/${room.roomId}/${target.uid}/${id}`,target,{accepted:true,revision:state.revision,processedAt:q.now()},{label:`decision.${s.name}.${action}.fake_ack`}),'client cannot forge its own Host ack');
      const accepted=action!=='reject'&&action!=='decline'&&action!=='pass';
      q.allow(await q.db('PUT',`intentAcks/${room.roomId}/${target.uid}/${id}`,host,{accepted,revision:state.revision,processedAt:q.now(),...(!accepted?{errorCode:'DECLINED'}:{})},{label:`decision.${s.name}.${action}.host_ack`}),'Host writes decision ack');
      const ack=await q.getJson(`intentAcks/${room.roomId}/${target.uid}/${id}`,target,`decision.${s.name}.${action}.ack_read`);
      q.eq(ack.accepted,accepted,'target receives exact ack result');
      q.deny(await q.db('GET',`intentAcks/${room.roomId}/${target.uid}/${id}`,attacker,undefined,{label:`decision.${s.name}.${action}.attacker_ack`}),'outsider cannot read ack');
      q.allow(await q.db('DELETE',`intents/${room.roomId}/${target.uid}/${id}`,host,undefined,{label:`decision.${s.name}.${action}.cleanup`}),'Host cleans processed intent');
      console.log(`DECISION_RTT kind=${s.kind} action=${action} owner=${target.label} accepted=${accepted} ms=${(performance.now()-t0).toFixed(1)}`);
    }
  }

  q.setStage('invalid_decision_attack_matrix');
  let id=q.id('attack_foreign',20);
  const p2intent=q.intent(p2,'decision',{kind:'auction',action:'bid',slotIndex:1,tileIndex:7});
  q.deny(await q.db('PUT',`intents/${room.roomId}/${p2.uid}/${id}`,p3,p2intent,{label:'attack.foreign_uid_intent'}),'P3 cannot submit as P2');
  id=q.id('attack_old',20);
  q.deny(await q.db('PUT',`intents/${room.roomId}/${p2.uid}/${id}`,p2,{...p2intent,clientCommandId:q.id('cmd',16),createdAt:q.now()-301000},{label:'attack.stale_intent'}),'stale replay denied');
  id=q.id('attack_future',20);
  q.deny(await q.db('PUT',`intents/${room.roomId}/${p2.uid}/${id}`,p2,{...p2intent,clientCommandId:q.id('cmd',16),createdAt:q.now()+61000},{label:'attack.future_intent'}),'future-dated intent denied');
  id=q.id('attack_oversize',20);
  q.deny(await q.db('PUT',`intents/${room.roomId}/${p2.uid}/${id}`,p2,{...p2intent,clientCommandId:q.id('cmd',16),payload:'X'.repeat(16385),createdAt:q.now()},{label:'attack.oversize_payload'}),'oversize decision payload denied');
  id=q.id('attack_bad_uid_body',20);
  q.deny(await q.db('PUT',`intents/${room.roomId}/${p2.uid}/${id}`,p2,{...p2intent,uid:p3.uid,clientCommandId:q.id('cmd',16),createdAt:q.now()},{label:'attack.uid_body_mismatch'}),'intent body uid must match path uid');

  q.setStage('disconnect_during_blocking_decision');
  const p2Off=q.member(p2,'remoteHuman',room.joinedAt+1,false,p2.label);
  q.allow(await q.db('PUT',`roomMembers/${room.roomId}/${p2.uid}`,p2,p2Off,{label:'p2.disconnect'}),'P2 disconnects');
  id=q.id('offline_decision',20);
  q.deny(await q.db('PUT',`intents/${room.roomId}/${p2.uid}/${id}`,p2,q.intent(p2,'decision',{kind:'trade',action:'accept',slotIndex:1}),{label:'offline.intent'}),'disconnected decision cannot enter queue');
  const p2On=q.member(p2,'remoteHuman',room.joinedAt+1,true,p2.label);
  q.allow(await q.db('PUT',`roomMembers/${room.roomId}/${p2.uid}`,host,p2On,{label:'p2.host_reactivate'}),'Host reactivates P2');
  id=q.id('online_decision',20);
  q.allow(await q.db('PUT',`intents/${room.roomId}/${p2.uid}/${id}`,p2,q.intent(p2,'decision',{kind:'trade',action:'accept',slotIndex:1}),{label:'online.intent'}),'reconnected P2 submits decision');

  q.setStage('ack_mutability_boundary');
  q.allow(await q.db('PUT',`intentAcks/${room.roomId}/${p2.uid}/${id}`,host,{accepted:true,revision:state.revision,processedAt:q.now()},{label:'ack.first'}),'Host writes first ack');
  q.allow(await q.db('PUT',`intentAcks/${room.roomId}/${p2.uid}/${id}`,host,{accepted:false,revision:state.revision+1,processedAt:q.now(),errorCode:'HOST_REVISED'},{label:'ack.rewrite'}),'Host can revise ack by current RTDB design');
  console.log('BOUNDARY expected=true detail=intentAck_is_host_mutable client_must_treat_latest_authoritative_ack_consistently');

  const report=q.report({decisionScenarios:scenarios.length,decisionIntents:intentCounter});
  console.log(`ATB_PHASE18R_04_TRADE_AUCTION_EVENT_INTENTS PASS checks=${report.checks} httpOps=${report.httpOps} durationMs=${report.durationMs} decisions=${scenarios.length} intents=${intentCounter} production=NOT_TOUCHED UNITY_RUNTIME=NOT_RUN`);
  return report;
}
module.exports={run};
if(require.main===module)run().catch(e=>{console.error(e.stack||e);process.exitCode=1;});
