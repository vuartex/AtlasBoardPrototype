'use strict';
const {QaContext}=require('./phase18r_shared.cjs');

async function run(){
  const q=new QaContext('PHASE18R_01_CONNECTIVITY_OWNERSHIP');
  console.log('ATB_PHASE18R_01 START production=FORBIDDEN');
  q.setStage('auth');
  const host=await q.signup('P18R01-Host');
  const bob=await q.signup('P18R01-Bob');
  const carol=await q.signup('P18R01-Carol');
  const stranger=await q.signup('P18R01-Stranger');

  q.setStage('room_join_presence_ready');
  const room=await q.createRoom([host,bob,carol],{mapId:'Turkey',roundLimit:20});

  q.setStage('ownership_denials');
  q.deny(await q.db('GET',`roomMeta/${room.roomId}`,stranger,undefined,{label:'outsider.room.read'}),'outsider cannot read room');
  const badMeta={...room.meta,themeId:'foreign_edit',updatedAt:q.now()};
  q.deny(await q.db('PUT',`roomMeta/${room.roomId}`,bob,badMeta,{label:'remote.room.edit'}),'remote cannot edit roomMeta');
  const hostMember=q.member(host,'host',room.joinedAt,true,'Host');
  q.deny(await q.db('PUT',`roomMembers/${room.roomId}/${host.uid}`,bob,hostMember,{label:'remote.foreign_member'}),'remote cannot write foreign member');
  q.deny(await q.db('PUT',`presence/${room.roomId}/${host.uid}`,bob,{online:true,lastSeenAt:q.now(),connectionId:'forged'},{label:'remote.foreign_presence'}),'remote cannot forge foreign presence');
  q.deny(await q.db('PUT',`roomReady/${room.roomId}/${host.uid}`,bob,{uid:host.uid,revision:2,ready:false},{label:'remote.foreign_ready'}),'remote cannot forge foreign ready');

  q.setStage('pawn_ownership');
  const bobPawn={uid:bob.uid,slotIndex:1,pawnCosmeticId:'ghost_03',updatedAt:q.now()};
  q.allow(await q.db('PUT',`roomPawnSelections/${room.roomId}/${bob.uid}/1`,bob,bobPawn,{label:'bob.pawn.write'}),'Bob writes own pawn');
  q.deny(await q.db('PUT',`roomPawnSelections/${room.roomId}/${bob.uid}/1`,carol,{...bobPawn,pawnCosmeticId:'vehicle_taxi',updatedAt:q.now()},{label:'carol.forge_bob_pawn'}),'Carol cannot overwrite Bob pawn');
  q.deny(await q.db('PUT',`roomPawnSelections/${room.roomId}/${bob.uid}/1`,host,{...bobPawn,pawnCosmeticId:'vehicle_taxi',updatedAt:q.now()},{label:'host.cannot_set_foreign_pawn'}),'Host cannot set another UID pawn');
  q.allow(await q.db('GET',`roomPawnSelections/${room.roomId}/${bob.uid}`,host,undefined,{label:'host.read_bob_pawn'}),'Host reads Bob pawn');

  q.setStage('intents_ack_roundtrip');
  const types=[
    ['phase18_pulse',{slotIndex:1,tileIndex:5,category:2}],
    ['phase18_bank',{slotIndex:1,tileIndex:8,amount:120,termKind:0,action:'open'}],
    ['phase18_civic',{slotIndex:1,projectIndex:2,action:'buy'}],
    ['decision',{kind:'trade',action:'accept',slotIndex:1,targetSlotIndex:0}]
  ];
  for(let i=0;i<types.length;i++){
    const [type,payload]=types[i];
    const id=q.id(`intent${i}`,20);
    const intent=q.intent(bob,type,payload,{seatId:'seat_1'});
    const t0=performance.now();
    q.allow(await q.db('PUT',`intents/${room.roomId}/${bob.uid}/${id}`,bob,intent,{label:`intent.${type}.put`}),'Bob submits own intent');
    const seen=await q.getJson(`intents/${room.roomId}/${bob.uid}/${id}`,host,`intent.${type}.host_read`);
    q.eq(seen.uid,bob.uid,`${type} intent owner preserved`);
    q.eq(JSON.parse(seen.payload),payload,`${type} payload preserved`);
    q.deny(await q.db('GET',`intents/${room.roomId}/${bob.uid}/${id}`,carol,undefined,{label:`intent.${type}.foreign_read`}),'other remote cannot read Bob intent');
    const ack={accepted:i%2===0,revision:10+i,processedAt:q.now(),...(i%2?{errorCode:'QA_REJECT'}:{})};
    q.allow(await q.db('PUT',`intentAcks/${room.roomId}/${bob.uid}/${id}`,host,ack,{label:`intent.${type}.ack`}),'Host writes ack');
    const bobAck=await q.getJson(`intentAcks/${room.roomId}/${bob.uid}/${id}`,bob,`intent.${type}.ack_read`);
    q.eq(bobAck.accepted,ack.accepted,`${type} ack exact`);
    q.deny(await q.db('GET',`intentAcks/${room.roomId}/${bob.uid}/${id}`,carol,undefined,{label:`intent.${type}.foreign_ack_read`}),'other remote cannot read Bob ack');
    q.allow(await q.db('DELETE',`intents/${room.roomId}/${bob.uid}/${id}`,host,undefined,{label:`intent.${type}.cleanup`}),'Host deletes processed intent');
    console.log(`INTENT type=${type} owner=Bob accepted=${ack.accepted} roundTripObservedMs=${(performance.now()-t0).toFixed(1)}`);
  }

  q.setStage('malformed_and_stale_intents');
  let id=q.id('badshort',20);
  q.deny(await q.db('PUT',`intents/${room.roomId}/${bob.uid}/${id}`,bob,{uid:bob.uid,type:'decision',clientCommandId:'short',payload:'{}',createdAt:q.now()},{label:'intent.short_command'}),'short clientCommandId denied');
  id=q.id('badstale',20);
  q.deny(await q.db('PUT',`intents/${room.roomId}/${bob.uid}/${id}`,bob,{uid:bob.uid,type:'decision',clientCommandId:q.id('cmd',16),payload:'{}',createdAt:q.now()-301000},{label:'intent.stale'}),'stale intent denied');
  id=q.id('badfuture',20);
  q.deny(await q.db('PUT',`intents/${room.roomId}/${bob.uid}/${id}`,bob,{uid:bob.uid,type:'decision',clientCommandId:q.id('cmd',16),payload:'{}',createdAt:q.now()+61000},{label:'intent.future'}),'future intent denied');
  id=q.id('badlarge',20);
  q.deny(await q.db('PUT',`intents/${room.roomId}/${bob.uid}/${id}`,bob,{uid:bob.uid,type:'decision',clientCommandId:q.id('cmd',16),payload:'x'.repeat(16385),createdAt:q.now()},{label:'intent.oversize'}),'oversize payload denied');
  id=q.id('baduid',20);
  q.deny(await q.db('PUT',`intents/${room.roomId}/${bob.uid}/${id}`,carol,{uid:bob.uid,type:'decision',clientCommandId:q.id('cmd',16),payload:'{}',createdAt:q.now()},{label:'intent.foreign_uid'}),'foreign UID intent denied');

  q.setStage('disconnect_reconnect_authority');
  const bobOffline=q.member(bob,'remoteHuman',room.joinedAt+1,false,'P18R01-Bob');
  q.allow(await q.db('PUT',`roomMembers/${room.roomId}/${bob.uid}`,bob,bobOffline,{label:'bob.self_leave'}),'Bob can deactivate own membership');
  id=q.id('disconnected',20);
  q.deny(await q.db('PUT',`intents/${room.roomId}/${bob.uid}/${id}`,bob,q.intent(bob,'decision',{kind:'auction',action:'bid'}),{label:'bob.intent_while_inactive'}),'inactive Bob cannot submit intent');
  const bobOnline=q.member(bob,'remoteHuman',room.joinedAt+1,true,'P18R01-Bob');
  q.allow(await q.db('PUT',`roomMembers/${room.roomId}/${bob.uid}`,host,bobOnline,{label:'host.reactivate_bob'}),'Host reactivates Bob');
  q.allow(await q.db('PUT',`presence/${room.roomId}/${bob.uid}`,bob,{online:true,lastSeenAt:q.now(),connectionId:'reconnected'},{label:'bob.presence_reconnect'}),'Bob presence returns');
  id=q.id('afterreconnect',20);
  q.allow(await q.db('PUT',`intents/${room.roomId}/${bob.uid}/${id}`,bob,q.intent(bob,'decision',{kind:'auction',action:'pass'}),{label:'bob.intent_after_reconnect'}),'reactivated Bob can submit again');

  q.setStage('latency_sampling');
  for(let i=0;i<30;i++){
    for(const u of [host,bob,carol]) q.allow(await q.db('GET',`roomState/${room.roomId}`,u,undefined,{label:`latency.roomState.${u.label}`}),'active member reads roomState');
  }

  const report=q.report({users:4,room:room.roomId});
  console.log(`ATB_PHASE18R_01_CONNECTIVITY_OWNERSHIP PASS checks=${report.checks} httpOps=${report.httpOps} durationMs=${report.durationMs} authUsers=4 production=NOT_TOUCHED UNITY_RUNTIME=NOT_RUN`);
  return report;
}

module.exports={run};
if(require.main===module)run().catch(e=>{console.error(e.stack||e);process.exitCode=1;});
