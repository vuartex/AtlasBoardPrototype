'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'../../..');
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const rules=JSON.parse(read('Backend/Firebase/database.rules.json')).rules;
const journal=read('Assets/Project/Scripts/Progression/AtlasBoardRtdbCareerJournal.cs');
const meta=read('Assets/Project/Scripts/Meta/AtlasBoardRtdbMetaEconomy.cs');
const lobby=read('Assets/Project/Scripts/Online/Firebase/AtlasBoardRtdbCasualLobbyTransport.cs');
const match=read('Assets/Project/Scripts/Online/Firebase/AtlasBoardSparkRtdbMatchTransport.cs');
const bridge=read('Assets/Project/Scripts/Online/Firebase/AtlasBoardLobbyRuntimeBridge.cs');
const coord=read('Assets/Project/Scripts/Online/AtlasBoardTurnDiceNetworkCoordinator.cs');
const recorder=read('Assets/Project/Scripts/Progression/AtlasBoardProgressionMatchRecorder.cs');
const progression=read('Assets/Project/Scripts/Progression/AtlasBoardProgressionBridge.cs');
const readModel=read('Assets/Project/Scripts/Platform/AtlasBoardRtdbCareerReadModel.cs');
let checks=0,parsed=0;const ok=(x,m)=>{assert.ok(x,m);checks++;};
function visit(node){if(!node||typeof node!=='object')return;for(const [k,v] of Object.entries(node)){
 if(['.read','.write','.validate','.indexOn'].includes(k)&&typeof v==='string'){
  if(k!=='.indexOn'){
   const vars=[...new Set((v.match(/\$[A-Za-z_][A-Za-z0-9_]*/g)||[]))];
   new Function('auth','data','newData','root','query','now',...vars,`return (${v});`);parsed++;
  }
 } else if(v&&typeof v==='object')visit(v);
}}
visit(rules);ok(parsed>300,'all RTDB expressions parse as JavaScript-shaped rule expressions');
for(const t of ['roomPawnSelections','completedMatchArchives','careerParticipation','careerAttestations','careerFinalProofs','careerClaims'])ok(!!rules[t],t+' rule exists');
ok(!rules.careerParticipation.$uid.$matchId['.write'].includes("sign_in_provider !== 'anonymous'"),'anonymous Firebase UID can sign participation');
ok(rules.careerParticipation.$uid.$matchId['.write'].includes("createdAt').val() <= now - 5000"),'five-second completed-match QA floor');
ok(rules.careerAttestations.$matchId.$uid['.write'].includes("completedMatchArchives"),'attestation anchors to immutable archive before proof');
ok(!rules.careerAttestations.$matchId.$uid['.write'].includes("careerFinalProofs').child($matchId).exists()"),'attestation has no proof chicken-and-egg');
ok(rules.careerFinalProofs.$matchId['.write'].includes('careerAttestations'),'final proof requires peer archive attestation');
ok(rules.careerClaims.$uid.$matchId['.write'].includes('careerAttestations'),'claim requires attestation');
ok(!rules.careerClaims.$uid.$matchId['.write'].includes("sign_in_provider !== 'anonymous'"),'Guest claim uses same UID-bound rule');
const wallet=rules.users.$uid.metaEconomy['.write'];const mi=wallet.indexOf("'MATCH_REWARD'");ok(mi>0,'MATCH_REWARD branch exists');ok(!wallet.slice(Math.max(0,mi-350),mi+80).includes("sign_in_provider !== 'anonymous'"),'MATCH_REWARD not blocked solely by anonymous provider');
for(const t of ['TryGetLiveGuestRewardIdentity','EnsureLiveGuestCanonicalProfileAsync','ATB_LIVE_GUEST_PROFILE'])ok(bridge.includes(t),'bridge '+t);
for(const t of ['IsRestGuest','TryGetLiveGuestRewardIdentity','return identity != null;','ATB_MATCH_GOLD_FLOW'])ok(meta.includes(t),'meta '+t);
for(const t of ['ObserveCompletedMatchAsync','EnsureLocalArchiveAttestationAsync','WriteArchiveAttestationAsync','completedMatchArchives','ATB_CAREER_ARCHIVE_ATTEST','PendingFinalObservation','ATB_CAREER_FINAL_OBSERVATION','attestation_resumed=true'])ok(journal.includes(t),'journal '+t);
ok(coord.includes('AtlasBoardRtdbCareerJournal.ObserveCompletedMatchAsync'),'final-frame observation immediately starts attestation');
const observeCalls=(coord.match(/AtlasBoardRtdbCareerJournal\.ObserveCompletedMatchAsync/g)||[]).length;
ok(observeCalls>=2,'both Host and follower final snapshots enter the same durable observation path');
ok(!recorder.includes('No-Steam Guest earns no server XP/Gold'),'old Guest reward bypass removed');
ok(recorder.includes('AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession'),'local Guest history remains UID scoped');
ok(!progression.includes('AtlasBoardProgressionSnapshot guest = ReadLocalGuestProfile(uid);'),'Guest profile no longer short-circuits durable RTDB recovery');
ok(progression.includes('MergeLocalObservedMatches(snapshot, uid, liveGuest)'),'Guest preserves local observation while merging durable server Career');
ok(progression.includes('Provider type is not reward authority'),'progression eligibility is authenticated UID based');
ok(readModel.includes('TryGetLiveGuestRewardIdentity'),'Career reopen can recover REST Guest UID after Leave/Rematch/restart');
ok(readModel.includes('return "uid:" + guestUid'),'REST Guest participates in normal UID refresh cadence');
for(const t of ['RoomPawnSelectionPath','WriteOwnRoomPawnSelectionAsync','ATB_PAWN_LEDGER','ATB_MATCH_ROSTER_COMMIT','ATB_MATCH_ROSTER_INVARIANT','ATB_MATCH_ROSTER_PAWN_REFRESH'])ok(lobby.includes(t),'lobby '+t);
for(const t of ['CompletedMatchArchive','EnsureCompletedArchiveAsync','ATB_MATCH_ARCHIVE','ATB_REMATCH_ARCHIVE_BARRIER'])ok(match.includes(t),'match '+t);
ok(!match.includes('ATB_REMATCH_REWARD_BARRIER'),'gameplay Rematch no longer waits on Career proof');
ok(rules.careerClaims.$uid.$matchId['.validate'].includes("result').val() === 'tie' ? 15 : 10"),'claim rule uses owner-approved win/tie/loss Gold bonuses');
ok(rules.careerClaims.$uid.$matchId['.validate'].includes('> 50 ? 50'),'claim rule hard-caps Gold at 50');
ok(journal.includes('int roundBonus = Mathf.Max(0, rounds);'),'journal awards one Gold per completed round');
console.log(`ATB_CAREER_GOLD_SOURCE_CONTRACT PASS checks=${checks} parsed=${parsed} anonymousGuest=true archiveAttestation=true rematchRewardIndependent=true UNITY_COMPILE=NOT_RUN FIREBASE_EMULATOR=NOT_RUN TWO_CLIENT=NOT_RUN`);
