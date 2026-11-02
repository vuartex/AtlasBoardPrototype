'use strict';
const fs=require('node:fs');
const path=require('node:path');
const {QaContext}=require('./phase18r_shared.cjs');

function sourceContract(q){
  const root=path.resolve(__dirname,'../../..');
  const coord=fs.readFileSync(path.join(root,'Assets/Project/Scripts/Online/AtlasBoardTurnDiceNetworkCoordinator.cs'),'utf8');
  const economy=fs.readFileSync(path.join(root,'Assets/Project/Scripts/Core/PropertyDevelopmentManager.cs'),'utf8');
  const civic=fs.readFileSync(path.join(root,'Assets/Project/Scripts/Board/AtlasBoardCivicProjectManager.cs'),'utf8');
  for(const token of ['public int schemaVersion = 13','pulseLevels = Array.Empty<int>()','pulseDrift = Array.Empty<int>()','bankLoans = Array.Empty<int>()','civicState = Array.Empty<int>()','cityMemory = Array.Empty<int>()']) q.ok(coord.includes(token),`coordinator source contract: ${token}`);
  q.ok(economy.includes('new int[tileCount * 6]'),'bank snapshot is six ints per tile');
  q.ok(economy.includes('values[o] = loan.ownerSlot') && economy.includes('values[o+5] = loan.ratePercent'),'bank snapshot fields present');
  q.ok(economy.includes('CityMemoryMaxEvents = 6') && economy.includes('CityMemoryMaxTransfers = 5'),'city memory bounds preserved');
  q.ok(civic.includes('return new[] { ownerSlots[0], ownerSlots[1], ownerSlots[2]') && civic.includes('pendingSlot, pendingProject'),'Civic snapshot nine-field contract present');
}

function cityMemoryFixture(){
  // [schema=1,count=2]
  // city 5: owner0 activeUntil11 cooldown14 transfers 3@[4,6,8], events 3
  // city 9: owner1 activeUntil0 cooldown0 transfers 1@[9], events 2
  return [1,2,
    5,0,11,14,3,4,6,8,3, 1,8,120, 4,8,3, 6,10,130,
    9,1,0,0,1,9,2, 2,9,75, 7,12,0];
}

async function run(){
  const q=new QaContext('PHASE18R_03_ECONOMY_PULSE_BANK_CIVIC');
  console.log('ATB_PHASE18R_03 START production=FORBIDDEN');
  q.setStage('source_contract_matches_firebase_fixture');
  sourceContract(q);

  q.setStage('auth_room_match');
  const host=await q.signup('P18R03-Host');
  const remote=await q.signup('P18R03-Remote');
  const attacker=await q.signup('P18R03-Attacker');
  const room=await q.createRoom([host,remote],{mapId:'USA',roundLimit:20});
  const created=await q.createMatch(room);
  let frame=created.frame; let state=created.state;

  q.setStage('authoritative_phase18_roundtrip');
  for(let cycle=1;cycle<=40;cycle++){
    const tile=cycle%24;
    const category=cycle%4;
    frame={...frame,currentRound:1+Math.floor(cycle/4),completedTurns:cycle};
    frame.pulseLevels=[...frame.pulseLevels];
    frame.pulseDrift=[...frame.pulseDrift];
    frame.tileOwnerSlotIndices=[...frame.tileOwnerSlotIndices];
    frame.tileDevelopmentLevels=[...frame.tileDevelopmentLevels];
    frame.bankLoans=[...frame.bankLoans];
    frame.civicState=[...frame.civicState];
    frame.pulseLevels[tile*4+category]=cycle%5;
    frame.pulseDrift[tile*4+category]=(cycle*7)%95;
    frame.tileOwnerSlotIndices[tile]=cycle%2;
    frame.tileDevelopmentLevels[tile]=cycle%6;
    if(cycle===8){
      const t=5,o=t*6;frame.bankLoans[o]=0;frame.bankLoans[o+1]=120;frame.bankLoans[o+2]=130;frame.bankLoans[o+3]=2;frame.bankLoans[o+4]=2;frame.bankLoans[o+5]=8;
    }
    if(cycle===16){
      const t=9,o=t*6;frame.bankLoans[o]=1;frame.bankLoans[o+1]=180;frame.bankLoans[o+2]=205;frame.bankLoans[o+3]=4;frame.bankLoans[o+4]=4;frame.bankLoans[o+5]=10;
      frame.civicState=[0,1,-1,-1,1,-1,-1,1,2];
    }
    if(cycle===24) frame.cityMemory=cityMemoryFixture();
    if(cycle===32){
      const o=5*6;frame.bankLoans[o]=-1;for(let k=1;k<6;k++)frame.bankLoans[o+k]=0;
      frame.civicState=[0,1,0,-1,-1,-1,-1,-1,-1];
    }
    state={...state,revision:state.revision+1,eventSequence:state.eventSequence+1,snapshotJson:JSON.stringify(frame),updatedAt:q.now(),hostHeartbeatAt:q.now()};
    q.allow(await q.db('PUT',`matchState/${created.matchId}`,host,state,{label:'phase18.host_publish'}),'Host publishes Phase18 frame');
    const mirror=await q.getJson(`matchState/${created.matchId}`,remote,'phase18.remote_read');
    const got=JSON.parse(mirror.snapshotJson);
    q.eq(got.schemaVersion,13,'frame schema 13 preserved');
    q.eq(got.pulseLevels,frame.pulseLevels,'Pulse levels exact roundtrip');
    q.eq(got.pulseDrift,frame.pulseDrift,'Pulse drift exact roundtrip');
    q.eq(got.bankLoans,frame.bankLoans,'Bank snapshot exact roundtrip');
    q.eq(got.civicState,frame.civicState,'Civic snapshot exact roundtrip');
    q.eq(got.cityMemory,frame.cityMemory,'City memory exact roundtrip');
  }

  q.setStage('shape_bounds_and_ownership');
  q.eq(frame.pulseLevels.length,128,'32 tiles x 4 Pulse categories');
  q.eq(frame.pulseDrift.length,128,'32 tiles x 4 Pulse drift values');
  q.eq(frame.bankLoans.length,192,'32 tiles x 6 bank fields');
  q.eq(frame.civicState.length,9,'Civic snapshot has 9 ints');
  q.ok(frame.pulseLevels.every(x=>x>=0&&x<=4),'Pulse level fixture within 0..4');
  q.ok(frame.pulseDrift.every(x=>x>=0&&x<=94),'Pulse metric fixture within 0..94');
  q.deny(await q.db('PUT',`matchState/${created.matchId}`,remote,{...state,revision:state.revision+1,eventSequence:state.eventSequence+1,snapshotJson:JSON.stringify({...frame,pulseLevels:new Array(128).fill(4)}),updatedAt:q.now()},{label:'remote.forge_phase18'}),'Remote cannot forge authoritative Pulse/Bank/Civic state');
  q.deny(await q.db('PUT',`matchState/${created.matchId}`,attacker,{...state,revision:state.revision+1,eventSequence:state.eventSequence+1,updatedAt:q.now()},{label:'attacker.forge_phase18'}),'Outsider cannot forge match state');

  q.setStage('phase18_intent_ownership');
  const intents=[
    ['pulse_build',{slotIndex:1,tileIndex:4,category:0}],
    ['pulse_build',{slotIndex:1,tileIndex:4,category:3}],
    ['bank',{slotIndex:1,tileIndex:9,amount:180,termKind:1,action:'open'}],
    ['bank',{slotIndex:1,tileIndex:9,amount:0,termKind:1,action:'repay'}],
    ['civic',{slotIndex:1,projectIndex:2,action:'travel'}],
    ['civic',{slotIndex:1,projectIndex:2,action:'buy'}],
    ['civic',{slotIndex:1,projectIndex:2,action:'decline'}]
  ];
  for(let i=0;i<intents.length;i++){
    const [type,payload]=intents[i];const id=q.id(`p18econ${i}`,20);
    const intent=q.intent(remote,type,payload,{seatId:'seat_1'});
    q.allow(await q.db('PUT',`intents/${room.roomId}/${remote.uid}/${id}`,remote,intent,{label:`econIntent.${type}.put`}),'Remote writes own economy intent');
    const got=await q.getJson(`intents/${room.roomId}/${remote.uid}/${id}`,host,`econIntent.${type}.host_read`);
    q.eq(JSON.parse(got.payload),payload,`${type} payload ownership exact`);
    q.allow(await q.db('PUT',`intentAcks/${room.roomId}/${remote.uid}/${id}`,host,{accepted:true,revision:state.revision,processedAt:q.now()},{label:`econIntent.${type}.ack`}),'Host acks economy intent');
    q.allow(await q.db('DELETE',`intents/${room.roomId}/${remote.uid}/${id}`,host,undefined,{label:`econIntent.${type}.cleanup`}),'Host cleans economy intent');
  }

  q.setStage('firebase_semantic_boundary_probe');
  // Important boundary: database.rules authorizes WHO may publish snapshotJson and its size,
  // but cannot inspect the semantic contents of the JSON string. A malicious current Host
  // can therefore publish internally nonsensical Phase18 arrays. Unity-side validation and
  // final release security must treat Host as gameplay authority, not RTDB as a semantic verifier.
  const malformed={...frame,pulseLevels:[999],bankLoans:[999],civicState:[999],cityMemory:[999]};
  const malformedState={...state,revision:state.revision+1,eventSequence:state.eventSequence+1,snapshotJson:JSON.stringify(malformed),updatedAt:q.now(),hostHeartbeatAt:q.now()};
  q.allow(await q.db('PUT',`matchState/${created.matchId}`,host,malformedState,{label:'boundary.host_semantic_malformed'}),'current Host may publish semantically opaque snapshotJson by design');
  console.log('BOUNDARY expected=true detail=RTDB_validates_authority_and_size_not_internal_snapshot_semantics securityFollowup=UNITY_HOST_VALIDATION_REQUIRED');
  state={...malformedState,revision:malformedState.revision+1,eventSequence:malformedState.eventSequence+1,snapshotJson:JSON.stringify(frame),updatedAt:q.now(),hostHeartbeatAt:q.now()};
  q.allow(await q.db('PUT',`matchState/${created.matchId}`,host,state,{label:'boundary.restore_valid'}),'Host restores valid canonical frame');

  const report=q.report({roundTrips:40,phase18Fields:['pulseLevels','pulseDrift','bankLoans','civicState','cityMemory']});
  console.log(`ATB_PHASE18R_03_ECONOMY_PULSE_BANK_CIVIC PASS checks=${report.checks} httpOps=${report.httpOps} durationMs=${report.durationMs} roundTrips=40 semanticBoundary=HOST_AUTHORITY production=NOT_TOUCHED UNITY_RUNTIME=NOT_RUN`);
  return report;
}
module.exports={run};
if(require.main===module)run().catch(e=>{console.error(e.stack||e);process.exitCode=1;});
