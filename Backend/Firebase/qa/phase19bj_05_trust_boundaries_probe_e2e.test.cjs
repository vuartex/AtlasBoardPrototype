'use strict';
const {SecurityQa}=require('./phase19bj_security_shared.cjs');
async function run(){
 const q=new SecurityQa('PHASE19BJ_05_TRUST_BOUNDARIES');console.log('ATB_PHASE19BJ_05 START production=FORBIDDEN expected_open_boundaries=true');
 q.setStage('auth_room_match');
 const host=await q.signup('Sec05-Host');const peer=await q.signup('Sec05-Peer');const attacker=await q.signup('Sec05-Attacker');
 const room=await q.createRoom([host,peer],{maxPlayers:2});const match=await q.createMatch(room,{createdAt:q.now()-20000});
 q.setStage('modified_host_semantic_forgery_probe');
 const forged=q.zeroFrame(room.users);forged.playerMoney=[99999999,0,-1,-1];forged.pulseLevels[0]=999;forged.bankLoans[0]=0;forged.bankLoans[1]=99999999;forged.civicState[0]=3;forged.cityMemory=[1,0];
 const hostForged={...match.state,revision:match.state.revision+1,eventSequence:match.state.eventSequence+1,snapshotJson:JSON.stringify(forged),updatedAt:q.now(),hostHeartbeatAt:q.now()};
 q.allow(await q.db('PUT',`matchState/${match.matchId}`,host,hostForged,{label:'boundary.host_semantic_forgery'}),'current authoritative Host can publish syntactically valid but semantically forged snapshot');
 q.deny(await q.db('PUT',`matchState/${match.matchId}`,peer,{...hostForged,revision:hostForged.revision+1,updatedAt:q.now()},{label:'boundary.peer_state_forgery'}),'non-host cannot publish forged match state');
 console.log('OPEN_BOUNDARY modified_host_gameplay_semantics_not_server_validated=true mitigation=server_authoritative_validation_or_replay_required');
 q.setStage('authenticated_intent_flood_probe');
 let allowed=0;const n=120;
 for(let i=0;i<n;i++){
   const id=q.id(`flood${i}`,20);const intent=q.intent(peer,'decision',{kind:'auction',action:'bid',value:i});
   const r=await q.db('PUT',`intents/${room.roomId}/${peer.uid}/${id}`,peer,intent,{label:'boundary.intent_flood'});
   if(r.status>=200&&r.status<300)allowed++;else q.deny(r,'unexpected intent flood denial');
 }
 q.eq(allowed,n,'current rules allow many unique outstanding intents from one authenticated active UID');
 console.log(`OPEN_BOUNDARY authenticated_intent_rate_limit_absent=true acceptedBurst=${allowed} mitigation=client_protocol_plus_server_throttle_or_backend_authority`);
 q.setStage('outsider_still_blocked');
 const fid=q.id('outside',20);q.deny(await q.db('PUT',`intents/${room.roomId}/${peer.uid}/${fid}`,attacker,q.intent(peer,'decision',{kind:'trade',action:'accept'}),{label:'boundary.outsider_forge'}),'outsider cannot exploit peer intent path');
 q.deny(await q.db('GET',`matchState/${match.matchId}`,attacker,undefined,{label:'boundary.outsider_read_match'}),'outsider cannot read active match');
 const report=q.report({openBoundaries:2,intentBurst:allowed});
 console.log(`ATB_PHASE19BJ_05_TRUST_BOUNDARIES REPRODUCED checks=${report.checks} httpOps=${report.httpOps} durationMs=${report.durationMs} modifiedHostSemanticTrust=OPEN intentRateLimit=OPEN production=NOT_TOUCHED`);return report;
}
module.exports={run};if(require.main===module)run().catch(e=>{console.error(e.stack||e);process.exitCode=1;});
