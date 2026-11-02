'use strict';
const {QaContext}=require('./phase18r_shared.cjs');

async function updateMemberRole(q,room,user,newHost,role){
  const body=q.member(user,role,room.joinedAt+room.users.indexOf(user),true,user.label);
  q.allow(await q.db('PUT',`roomMembers/${room.roomId}/${user.uid}`,newHost,body,{label:`role.${user.label}.${role}`}),`${newHost.label} updates ${user.label} role=${role}`);
}

async function migrateMatchState(q,room,oldState,newHost,newFrame,label){
  const next={...oldState,hostUid:newHost.uid,revision:oldState.revision+1,eventSequence:oldState.eventSequence+1,
    snapshotJson:JSON.stringify(newFrame),updatedAt:q.now(),hostHeartbeatAt:q.now(),authorityEpoch:oldState.authorityEpoch+1};
  q.allow(await q.db('PUT',`matchState/${oldState.matchId}`,newHost,next,{label}),`${newHost.label} migrates matchState`);
  return next;
}

async function run(){
  const q=new QaContext('PHASE18R_02_MATCH_LIFECYCLE_MIGRATION');
  console.log('ATB_PHASE18R_02 START production=FORBIDDEN');
  q.setStage('auth_room_match');
  const a=await q.signup('P18R02-Alice');
  const b=await q.signup('P18R02-Bob');
  const c=await q.signup('P18R02-Carol');
  const d=await q.signup('P18R02-Dave');
  const outsider=await q.signup('P18R02-Outsider');
  const room=await q.createRoom([a,b,c,d],{mapId:'Colorado',roundLimit:20});
  const created=await q.createMatch(room);
  let state=created.state; let frame=created.frame;

  q.setStage('presence_and_access');
  for(const u of room.users){
    q.allow(await q.db('PUT',`matchPresence/${created.matchId}/${u.uid}`,u,{online:true,lastSeenAt:q.now()},{label:`matchPresence.${u.label}`}),'member writes own matchPresence');
    const read=await q.getJson(`matchState/${created.matchId}`,u,`matchRead.${u.label}`);
    q.eq(read.hostUid,a.uid,'all members see canonical initial Host');
  }
  q.deny(await q.db('GET',`matchState/${created.matchId}`,outsider,undefined,{label:'outsider.match.read'}),'outsider cannot read active match');
  const remoteForge={...state,revision:state.revision+1,eventSequence:state.eventSequence+1,updatedAt:q.now(),snapshotJson:JSON.stringify({...frame,currentRound:2})};
  q.deny(await q.db('PUT',`matchState/${created.matchId}`,b,remoteForge,{label:'remote.match.write'}),'RemoteHuman cannot publish authoritative matchState');
  q.deny(await q.db('PUT',`matchPresence/${created.matchId}/${b.uid}`,c,{online:false,lastSeenAt:q.now()},{label:'foreign.matchPresence'}),'remote cannot forge another match presence');

  q.setStage('host_publish_monotonic_stream');
  for(let i=0;i<30;i++){
    frame={...frame,currentRound:1+Math.floor(i/4),completedTurns:i+1,diceSequence:i+1,activeSlotIndex:i%4};
    state={...state,revision:state.revision+1,eventSequence:state.eventSequence+1,snapshotJson:JSON.stringify(frame),updatedAt:q.now(),hostHeartbeatAt:q.now()};
    q.allow(await q.db('PUT',`matchState/${created.matchId}`,a,state,{label:'host.publish.stream'}),'Host publishes match frame');
    if(i%5===0){
      const mirror=await q.getJson(`matchState/${created.matchId}`,c,'follower.mirror.sample');
      const mf=JSON.parse(mirror.snapshotJson);
      q.eq(mf.completedTurns,frame.completedTurns,'follower sees exact completedTurns');
      q.eq(mirror.revision,state.revision,'follower sees exact revision');
    }
  }

  q.setStage('graceful_host_migration_A_to_B');
  const badTakeover={...room.meta,hostUid:c.uid,authorityEpoch:room.meta.authorityEpoch+1,activeMatchId:'wrong_match',updatedAt:q.now()};
  q.deny(await q.db('PUT',`roomMeta/${room.roomId}`,c,badTakeover,{label:'migration.bad_active_match'}),'new Host cannot migrate with wrong activeMatchId');
  let meta={...room.meta,hostUid:b.uid,authorityEpoch:room.meta.authorityEpoch+1,updatedAt:q.now()};
  q.allow(await q.db('PUT',`roomMeta/${room.roomId}`,a,meta,{label:'migration.graceful.roomMeta'}),'old Host hands room authority to Bob');
  room.meta=meta;
  await updateMemberRole(q,room,b,b,'host');
  await updateMemberRole(q,room,a,b,'remoteHuman');
  frame={...frame,activeSlotIndex:1};
  state=await migrateMatchState(q,room,state,b,frame,'migration.graceful.matchState');
  q.deny(await q.db('PUT',`matchState/${created.matchId}`,a,{...state,revision:state.revision+1,eventSequence:state.eventSequence+1,updatedAt:q.now()},{label:'old_host.write_after_migration'}),'old Host loses match write authority');
  q.allow(await q.db('PUT',`matchState/${created.matchId}`,b,{...state,revision:state.revision+1,eventSequence:state.eventSequence+1,updatedAt:q.now(),hostHeartbeatAt:q.now()},{label:'new_host.publish'}),'new Host publishes after migration');
  state={...state,revision:state.revision+1,eventSequence:state.eventSequence+1,updatedAt:q.now(),hostHeartbeatAt:q.now()};

  q.setStage('crash_takeover_B_to_C');
  state={...state,revision:state.revision+1,eventSequence:state.eventSequence+1,updatedAt:q.now(),hostHeartbeatAt:q.now()-13000};
  q.allow(await q.db('PUT',`matchState/${created.matchId}`,b,state,{label:'host.stale_heartbeat_fixture'}),'current Host publishes stale heartbeat fixture');
  const tooEarlyMeta={...room.meta,hostUid:d.uid,authorityEpoch:room.meta.authorityEpoch+1,updatedAt:q.now()};
  // Dave is a legitimate candidate, but Bob state is stale now. Use a fresh heartbeat first to prove early takeover denial.
  let fresh={...state,revision:state.revision+1,eventSequence:state.eventSequence+1,updatedAt:q.now(),hostHeartbeatAt:q.now()};
  q.allow(await q.db('PUT',`matchState/${created.matchId}`,b,fresh,{label:'host.fresh_heartbeat'}),'Bob refreshes heartbeat');
  state=fresh;
  q.deny(await q.db('PUT',`roomMeta/${room.roomId}`,d,tooEarlyMeta,{label:'migration.too_early'}),'candidate cannot crash-takeover while heartbeat fresh');
  state={...state,revision:state.revision+1,eventSequence:state.eventSequence+1,updatedAt:q.now(),hostHeartbeatAt:q.now()-13000};
  q.allow(await q.db('PUT',`matchState/${created.matchId}`,b,state,{label:'host.stale_heartbeat'}),'Bob heartbeat becomes stale');
  meta={...room.meta,hostUid:c.uid,authorityEpoch:room.meta.authorityEpoch+1,updatedAt:q.now()};
  q.allow(await q.db('PUT',`roomMeta/${room.roomId}`,c,meta,{label:'migration.crash.roomMeta'}),'Carol takes over stale Host');
  room.meta=meta;
  await updateMemberRole(q,room,c,c,'host');
  await updateMemberRole(q,room,b,c,'remoteHuman');
  frame={...frame,activeSlotIndex:2};
  state=await migrateMatchState(q,room,state,c,frame,'migration.crash.matchState');
  q.deny(await q.db('PUT',`matchState/${created.matchId}`,b,{...state,revision:state.revision+1,eventSequence:state.eventSequence+1,updatedAt:q.now()},{label:'former_host_b.write'}),'former Host Bob cannot write after crash takeover');

  q.setStage('disconnect_reconnect_during_match');
  const dOff=q.member(d,'remoteHuman',room.joinedAt+3,false,d.label);
  q.allow(await q.db('PUT',`roomMembers/${room.roomId}/${d.uid}`,d,dOff,{label:'dave.self_disconnect'}),'Dave deactivates own membership');
  q.deny(await q.db('PUT',`matchPresence/${created.matchId}/${d.uid}`,d,{online:true,lastSeenAt:q.now()},{label:'dave.presence_while_inactive'}),'inactive Dave cannot heartbeat');
  const dOn=q.member(d,'remoteHuman',room.joinedAt+3,true,d.label);
  q.allow(await q.db('PUT',`roomMembers/${room.roomId}/${d.uid}`,c,dOn,{label:'host.reactivate_dave'}),'current Host reactivates Dave');
  q.allow(await q.db('PUT',`matchPresence/${created.matchId}/${d.uid}`,d,{online:true,lastSeenAt:q.now()},{label:'dave.reconnected_presence'}),'Dave heartbeats after reconnect');

  q.setStage('phase18_snapshot_survives_migration');
  frame.pulseLevels[5*4+2]=3;
  frame.pulseDrift[5*4+2]=21;
  frame.bankLoans[8*6]=1;frame.bankLoans[8*6+1]=120;frame.bankLoans[8*6+2]=130;frame.bankLoans[8*6+3]=2;frame.bankLoans[8*6+4]=2;frame.bankLoans[8*6+5]=8;
  frame.civicState=[0,2,-1,-1,1,-1,-1,-1,-1];
  frame.cityMemory=[1,1,5,0,8,14,3,4,6,8,2,1,8,120,4,8,3];
  state={...state,revision:state.revision+1,eventSequence:state.eventSequence+1,snapshotJson:JSON.stringify(frame),updatedAt:q.now(),hostHeartbeatAt:q.now()};
  q.allow(await q.db('PUT',`matchState/${created.matchId}`,c,state,{label:'phase18.publish_after_migration'}),'Carol publishes Phase18 state');
  for(const u of [a,b,c,d]){
    const mirror=await q.getJson(`matchState/${created.matchId}`,u,`phase18.read.${u.label}`);
    const got=JSON.parse(mirror.snapshotJson);
    q.eq(got.pulseLevels[22],3,'Pulse level survives migration');
    q.eq(got.bankLoans[48],1,'Bank loan owner survives migration');
    q.eq(got.civicState[0],0,'Civic owner survives migration');
    q.eq(got.cityMemory,frame.cityMemory,'City memory survives migration');
  }

  q.setStage('new_match_storage_isolation');
  const match2=q.id('match2',20);
  const clean=q.zeroFrame({users:room.users,roundLimit:20});
  const state2=q.matchState(match2,room.roomId,c,room.users,clean,{revision:1,eventSequence:1,authorityEpoch:room.meta.authorityEpoch});
  q.allow(await q.db('PUT',`matchState/${match2}`,c,state2,{label:'match2.create'}),'current Host creates second match');
  const meta2={...room.meta,activeMatchId:match2,updatedAt:q.now()};
  q.allow(await q.db('PUT',`roomMeta/${room.roomId}`,c,meta2,{label:'match2.link'}),'room links second match');
  const read2=await q.getJson(`matchState/${match2}`,a,'match2.read');
  const f2=JSON.parse(read2.snapshotJson);
  q.eq(f2.pulseLevels.reduce((x,y)=>x+y,0),0,'new match Pulse starts clean');
  q.ok(f2.bankLoans.every((v,i)=>i%6===0?v===-1:v===0),'new match bank loans start clean');
  q.eq(f2.civicState,[-1,-1,-1,-1,-1,-1,-1,-1,-1],'new match Civic starts clean');
  q.eq(f2.cityMemory,[1,0],'new match city memory starts clean');

  const report=q.report({users:5,migrations:2,publishes:30});
  console.log(`ATB_PHASE18R_02_MATCH_LIFECYCLE_MIGRATION PASS checks=${report.checks} httpOps=${report.httpOps} durationMs=${report.durationMs} gracefulMigration=true crashTakeover=true multiClient=true production=NOT_TOUCHED UNITY_RUNTIME=NOT_RUN`);
  return report;
}
module.exports={run};
if(require.main===module)run().catch(e=>{console.error(e.stack||e);process.exitCode=1;});
