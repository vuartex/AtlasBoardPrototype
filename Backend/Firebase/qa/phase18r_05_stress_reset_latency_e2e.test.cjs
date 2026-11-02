'use strict';
const {QaContext}=require('./phase18r_shared.cjs');

async function run(){
  const q=new QaContext('PHASE18R_05_STRESS_RESET_LATENCY');
  console.log('ATB_PHASE18R_05 START production=FORBIDDEN');
  q.setStage('auth_room_match');
  const users=[
    await q.signup('P18R05-P1'),await q.signup('P18R05-P2'),
    await q.signup('P18R05-P3'),await q.signup('P18R05-P4')
  ];
  const room=await q.createRoom(users,{mapId:'USA',roundLimit:50});
  let created=await q.createMatch(room,{roundLimit:50});
  let state=created.state;let frame=created.frame;

  q.setStage('credential_and_auth_failures');
  q.deny(await q.db('GET',`roomState/${room.roomId}`,null,undefined,{label:'unauth.read'}),'unauthenticated roomState read denied');
  const fake={uid:'fake',token:'invalid-token',label:'invalid-token'};
  q.deny(await q.db('GET',`roomState/${room.roomId}`,fake,undefined,{label:'invalid_token.read'}),'invalid auth token denied');

  q.setStage('high_frequency_match_publish_read_intent');
  const loops=120;
  let intentCount=0;
  for(let i=1;i<=loops;i++){
    frame={...frame,completedTurns:i,currentRound:1+Math.floor((i-1)/4),activeSlotIndex:(i-1)%4,diceSequence:i,
      dieOne:1+(i%6),dieTwo:1+((i*3)%6),total:(1+(i%6))+(1+((i*3)%6)),dicePlayerSlotIndex:(i-1)%4};
    frame.playerMoney=[1500-i,1400+i,1300+(i%20),1200-(i%10)];
    frame.pulseLevels=[...frame.pulseLevels]; frame.pulseDrift=[...frame.pulseDrift];
    const tile=i%24,cat=i%4;frame.pulseLevels[tile*4+cat]=(frame.pulseLevels[tile*4+cat]+1)%5;frame.pulseDrift[tile*4+cat]=(frame.pulseDrift[tile*4+cat]+7)%95;
    state={...state,revision:state.revision+1,eventSequence:state.eventSequence+1,snapshotJson:JSON.stringify(frame),updatedAt:q.now(),hostHeartbeatAt:q.now()};
    q.allow(await q.db('PUT',`matchState/${created.matchId}`,users[0],state,{label:'stress.host_publish'}),'stress Host publish');
    for(let u=1;u<users.length;u++){
      const mirror=await q.getJson(`matchState/${created.matchId}`,users[u],`stress.follower_read.${u}`);
      if(i%10===0){q.eq(mirror.revision,state.revision,'stress follower revision exact');q.eq(JSON.parse(mirror.snapshotJson).completedTurns,i,'stress follower frame exact');}
    }
    if(i%4===0){
      const actor=users[1+(i%3)];const slot=users.indexOf(actor);const id=q.id(`stressIntent${i}`,20);
      const intent=q.intent(actor,'decision',{kind:i%8===0?'auction':'trade',action:i%12===0?'pass':'accept',slotIndex:slot,tileIndex:i%24,targetSlotIndex:(slot+1)%4},{seatId:`seat_${slot}`});
      q.allow(await q.db('PUT',`intents/${room.roomId}/${actor.uid}/${id}`,actor,intent,{label:'stress.intent.put'}),'stress intent write');
      q.allow(await q.db('GET',`intents/${room.roomId}/${actor.uid}/${id}`,users[0],undefined,{label:'stress.intent.host_read'}),'stress Host reads intent');
      q.allow(await q.db('PUT',`intentAcks/${room.roomId}/${actor.uid}/${id}`,users[0],{accepted:true,revision:state.revision,processedAt:q.now()},{label:'stress.intent.ack'}),'stress Host ack');
      q.allow(await q.db('GET',`intentAcks/${room.roomId}/${actor.uid}/${id}`,actor,undefined,{label:'stress.intent.ack_read'}),'stress actor reads ack');
      q.allow(await q.db('DELETE',`intents/${room.roomId}/${actor.uid}/${id}`,users[0],undefined,{label:'stress.intent.cleanup'}),'stress Host cleans intent');
      intentCount++;
    }
    if(i%10===0){
      for(const u of users) q.allow(await q.db('PUT',`matchPresence/${created.matchId}/${u.uid}`,u,{online:true,lastSeenAt:q.now()},{label:'stress.heartbeat'}),'stress matchPresence heartbeat');
      console.log(`STRESS_PROGRESS cycle=${i}/${loops} revision=${state.revision} httpOps=${q.httpOps}`);
    }
  }

  q.setStage('disconnect_reconnect_churn');
  for(let cycle=0;cycle<12;cycle++){
    const idx=1+(cycle%3);const u=users[idx];
    const off=q.member(u,'remoteHuman',room.joinedAt+idx,false,u.label);
    q.allow(await q.db('PUT',`roomMembers/${room.roomId}/${u.uid}`,u,off,{label:'churn.self_off'}),'member deactivates during churn');
    q.deny(await q.db('GET',`roomState/${room.roomId}`,u,undefined,{label:'churn.read_while_off'}),'inactive member loses room read');
    const on=q.member(u,'remoteHuman',room.joinedAt+idx,true,u.label);
    q.allow(await q.db('PUT',`roomMembers/${room.roomId}/${u.uid}`,users[0],on,{label:'churn.host_on'}),'Host reactivates churn member');
    q.allow(await q.db('GET',`roomState/${room.roomId}`,u,undefined,{label:'churn.read_after_on'}),'reactivated member regains room read');
  }

  q.setStage('snapshot_size_limits');
  const base={...frame,qaPadding:''};
  let raw=JSON.stringify(base);const target=64000;
  base.qaPadding='x'.repeat(Math.max(0,target-raw.length-32));
  let near=JSON.stringify(base);
  q.ok(near.length<65536 && near.length>62000,`near-limit snapshot size ${near.length}`);
  let nearState={...state,revision:state.revision+1,eventSequence:state.eventSequence+1,snapshotJson:near,updatedAt:q.now(),hostHeartbeatAt:q.now()};
  q.allow(await q.db('PUT',`matchState/${created.matchId}`,users[0],nearState,{label:'snapshot.near_limit'}),'near-limit snapshotJson accepted');
  const too={...base,qaPadding:'y'.repeat(66000)};const tooRaw=JSON.stringify(too);
  q.ok(tooRaw.length>65536,'oversize fixture really exceeds rule limit');
  q.deny(await q.db('PUT',`matchState/${created.matchId}`,users[0],{...nearState,revision:nearState.revision+1,eventSequence:nearState.eventSequence+1,snapshotJson:tooRaw,updatedAt:q.now()},{label:'snapshot.over_limit'}),'oversize snapshotJson denied');
  state={...nearState,revision:nearState.revision+1,eventSequence:nearState.eventSequence+1,snapshotJson:JSON.stringify(frame),updatedAt:q.now(),hostHeartbeatAt:q.now()};
  q.allow(await q.db('PUT',`matchState/${created.matchId}`,users[0],state,{label:'snapshot.restore'}),'restore normal frame');

  q.setStage('multi_match_reset_isolation');
  // Seed obvious Phase18 state in current match, then create five fresh IDs and prove each starts clean.
  frame.pulseLevels[0]=4;frame.bankLoans[0]=0;frame.bankLoans[1]=120;frame.bankLoans[2]=130;frame.bankLoans[3]=2;frame.bankLoans[4]=2;frame.bankLoans[5]=8;
  frame.civicState=[0,1,2,0,1,2,-1,-1,-1];frame.cityMemory=[1,1,5,0,11,14,3,4,6,8,1,1,8,120];
  state={...state,revision:state.revision+1,eventSequence:state.eventSequence+1,snapshotJson:JSON.stringify(frame),updatedAt:q.now()};
  q.allow(await q.db('PUT',`matchState/${created.matchId}`,users[0],state,{label:'reset.seed_old_state'}),'old match stores nonzero Phase18 state');
  const oldMatch=created.matchId;
  const newMatchIds=[];
  for(let m=1;m<=5;m++){
    const matchId=q.id(`resetMatch${m}`,20);newMatchIds.push(matchId);
    const clean=q.zeroFrame({users,roundLimit:50});
    const st=q.matchState(matchId,room.roomId,users[0],users,clean,{revision:1,eventSequence:1,authorityEpoch:room.meta.authorityEpoch});
    q.allow(await q.db('PUT',`matchState/${matchId}`,users[0],st,{label:'reset.new_match_create'}),'Host creates clean new match');
    room.meta={...room.meta,activeMatchId:matchId,updatedAt:q.now()};
    q.allow(await q.db('PUT',`roomMeta/${room.roomId}`,users[0],room.meta,{label:'reset.link_match'}),'Host links clean new match');
    const read=await q.getJson(`matchState/${matchId}`,users[2],'reset.read_clean');const f=JSON.parse(read.snapshotJson);
    q.eq(f.pulseLevels.reduce((a,b)=>a+b,0),0,`reset ${m} Pulse clean`);
    q.ok(f.bankLoans.every((v,i)=>i%6===0?v===-1:v===0),`reset ${m} bank clean`);
    q.eq(f.civicState,[-1,-1,-1,-1,-1,-1,-1,-1,-1],`reset ${m} Civic clean`);
    q.eq(f.cityMemory,[1,0],`reset ${m} cityMemory clean`);
  }
  const oldRead=await q.getJson(`matchState/${oldMatch}`,users[0],'reset.old_match_still_isolated');const oldF=JSON.parse(oldRead.snapshotJson);
  q.eq(oldF.pulseLevels[0],4,'old match retains its own historical live state while new IDs are clean');

  q.setStage('latency_tail_report');
  const slow=[...q.metrics].sort((a,b)=>b.ms-a.ms).slice(0,10);
  console.log('SLOWEST_OPERATIONS');
  slow.forEach((x,i)=>console.log(`${i+1}. ${x.ms.toFixed(1)}ms actor=${x.actor} step=${x.label} resource=${x.resource} status=${x.status}`));

  const report=q.report({loops,intentCount,newMatches:newMatchIds.length,disconnectCycles:12});
  console.log(`ATB_PHASE18R_05_STRESS_RESET_LATENCY PASS checks=${report.checks} httpOps=${report.httpOps} durationMs=${report.durationMs} publishLoops=${loops} intents=${intentCount} disconnectCycles=12 newMatches=5 production=NOT_TOUCHED UNITY_RUNTIME=NOT_RUN`);
  return report;
}
module.exports={run};
if(require.main===module)run().catch(e=>{console.error(e.stack||e);process.exitCode=1;});
