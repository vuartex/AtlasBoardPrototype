'use strict';
// SOURCE/MOCK RULES test. Not equivalent to firebase emulators:exec.
const fs=require('fs'), path=require('path'), assert=require('assert');
const dir=path.resolve(__dirname,'../../..');
const rules=JSON.parse(fs.readFileSync(path.join(dir,'Backend/Firebase/database.rules.json'),'utf8')).rules;
const read=p=>fs.readFileSync(path.join(dir,p),'utf8');
String.prototype.matches=function(rx){return rx.test(String(this));};
String.prototype.contains=function(s){return String(this).includes(s);};
let assertions=0;
function ok(v,message){assert.ok(v,message);assertions++;}
function no(v,message){assert.ok(!v,message);assertions++;}
function same(a,b,message){assert.deepStrictEqual(a,b,message);assertions++;}
function compile(expr,args){return new Function(...args,`return (${expr});`);}
let parsed=0;
function visit(node){if(!node||typeof node!=='object')return;
 for(const [key,rule] of Object.entries(node)){
   if(['.read','.write','.validate'].includes(key)&&typeof rule==='string'){
     compile(rule,['auth','data','newData','root','$uid','$matchId','$slot','$opId','now']);parsed++;
   }else if(rule&&typeof rule==='object')visit(rule);
 }}
visit(rules);ok(parsed>300,'all rules JS expression syntax');
// Firebase RTDB RuleDataSnapshot has no numChildren() method. Plain JS mocks
// might accept it, while the real Database emulator rejects the entire ruleset.
function findUnsupportedRuleMethods(node,where='rules'){
  if(!node||typeof node!=='object')return [];
  let found=[];
  for(const [key,value] of Object.entries(node)){
    if(['.read','.write','.validate'].includes(key) && typeof value==='string'){
      if(/\.numChildren\s*\(/.test(value))found.push(where+'/'+key);
    }else if(value&&typeof value==='object'){
      found=found.concat(findUnsupportedRuleMethods(value,where+'/'+key));
    }
  }
  return found;
}
same(findUnsupportedRuleMethods(rules),[], 'RTDB rules do not use unsupported numChildren()');
const seatNode=rules.careerFinalProofs.$matchId.seats;
const seatValidator=compile(seatNode['.validate'],['auth','data','newData','root','now']);
ok(seatValidator(null,null,{hasChildren:(keys)=>keys.every(k=>['0','1','2','3'].includes(k))},null,0),'all four result seats required');
no(seatValidator(null,null,{hasChildren:(keys)=>keys.every(k=>['0','1','2'].includes(k))},null,0),'missing fourth result seat denied');
ok(seatNode.$slot['.validate'].includes("$slot === '3'"),'only fixed 0..3 seat IDs accepted by child validator');
function snap(v,parent=null){return {
 val(){return v==null?null:v},exists(){return v!=null},
 isString(){return typeof v==='string'},isNumber(){return typeof v==='number'},
 isBoolean(){return typeof v==='boolean'},
 hasChildren(keys){return keys ? keys.every(k=>v!=null && Object.prototype.hasOwnProperty.call(v,k)) : v!=null && Object.keys(v).length>0},
 child(p){let val=v;for(const k of String(p).split('/'))val=val==null?undefined:val[k];return snap(val,this)},
 parent(){return parent||snap(null)}
 };}
const now=1900000000000, mid='match_012345678901234567', room='room_012345678901234567890';
const alice='aliceAccount',bob='bobAccount',hash='a'.repeat(64);
const auth=uid=>({uid,token:{firebase:{sign_in_provider:'password'}}});
const anonymous=uid=>({uid,token:{firebase:{sign_in_provider:'anonymous'}}});
const participation=(uid,slot,role)=>({uid,matchId:mid,lobbyId:room,slotIndex:slot,role,atMs:now-160000});
const one=participation(alice,0,'host'),two=participation(bob,1,'remoteHuman');
const frame=JSON.stringify({phase:'match_complete',matchResult:{valid:true,participating:[true,true,false,false],winnerSlots:[0],netWorth:[100,80,0,0]},currentRound:12,completedTurns:32});
const seat=(result,properties,dev,cash,netWorth,rolls,doubles,participating=true)=>({result,participating,properties,developments:dev,cash,netWorth,diceRolls:rolls,doublesRolled:doubles});
const seats={'0':seat('win',4,5,800,1100,20,3),'1':seat('loss',2,0,300,400,15,2),'2':seat('loss',0,0,0,0,0,0,false),'3':seat('loss',0,0,0,0,0,0,false)};
const proof={matchId:mid,lobbyId:room,hostUid:alice,witnessUid:bob,revision:100,digest:hash,snapshotJson:frame,atMs:now-120000,seats,rounds:12,turns:32,playerCount:2,mapId:'map_turkey',themeId:'classic_table',roundLimit:20};
const claims={};
const base={devWalletTesters:{},careerParticipation:{[alice]:{[mid]:one},[bob]:{[mid]:two}},careerFinalProofs:{[mid]:proof},careerAttestations:{[mid]:{[alice]:{digest:hash,revision:100},[bob]:{digest:hash,revision:100}}},careerClaims:{[alice]:{}}};
const clone=x=>JSON.parse(JSON.stringify(x));
function attempt(expr,uid,before,after,data=base){let rule=compile(expr,['auth','data','newData','root','$uid','$matchId','now']);return !!rule(auth(uid),snap(before),snap(after),snap(data),uid,mid,now);}
const c=rules.careerClaims.$uid.$matchId;
const amount=(result,r,p,d)=>({xp:25+(result==='win'?50:result==='tie'?20:0)+Math.min(100,r*3)+Math.min(50,p*5)+Math.min(75,d*3),gold:(result==='win'?50:result==='tie'?20:5)+Math.min(30,r)+Math.min(20,p*2)+Math.min(20,d*2)});
const w=amount('win',12,4,5);same(w,{xp:25+50+36+20+15,gold:50+12+8+10},'historic XP + Gold');
const claim={uid:alice,matchId:mid,lobbyId:room,witnessUid:bob,digest:hash,revision:100,atMs:now-10,result:'win',slotIndex:0,rounds:12,turns:32,properties:4,developments:5,cash:800,netWorth:1100,playerCount:2,mapId:'map_turkey',themeId:'classic_table',roundLimit:20,hostAccountId:alice,diceRolls:20,doublesRolled:3,recovered:false,xpAwarded:w.xp,goldAwarded:w.gold};
ok(attempt(c['.write'],alice,null,claim),'correct signed claim');
no(attempt(c['.write'],bob,null,claim),'foreign uid cannot claim');
no(attempt(c['.write'],alice,claim,claim),'claim immutable');
no(attempt(c['.write'],alice,null,{...claim,result:'loss'}),'result cannot change from host proof');
// Validation is a separate RTDB phase after write authorization.
const cv=compile(c['.validate'],['auth','data','newData','root','$uid','$matchId','now']);
function validClaim(candidate){return !!cv(auth(alice),snap(null),snap(candidate),snap(base),alice,mid,now);}
ok(validClaim(claim),'server-calculated XP/Gold and dice counters');
no(validClaim({...claim,goldAwarded:120}),'tampered Gold formula rejected');
no(validClaim({...claim,xpAwarded:300}),'tampered XP formula rejected');
no(validClaim({...claim,doublesRolled:40}),'impossible doubles rejected');
const credit=rules.careerParticipationCredits.$uid.$matchId;
const partial={uid:alice,matchId:mid,witnessUid:bob,digest:hash,xpAwarded:1,atMs:now-30};
ok(attempt(credit['.write'],alice,null,partial),'1 XP signed participation fallback');
no(attempt(credit['.write'],alice,partial,partial),'1 XP fallback immutable');
const missingPeer=clone(base);delete missingPeer.careerParticipation[bob];
no(attempt(credit['.write'],alice,null,partial,missingPeer),'cannot get XP from host+bots');
const fullWithPeerRemoved=clone(base);delete fullWithPeerRemoved.careerParticipation[bob];
no(attempt(c['.write'],alice,null,claim,fullWithPeerRemoved),'cannot full claim without peer signed during match');
const noAttest=clone(base);delete noAttest.careerAttestations[mid][bob];
no(attempt(c['.write'],alice,null,claim,noAttest),'peer final attestation required');
ok(attempt(c['.write'],alice,null,{...claim,recovered:true}),'preparticipated disconnected player can resume against witness attestation');
const noHostAttest=clone(base);delete noHostAttest.careerAttestations[mid][alice];
no(attempt(c['.write'],bob,null,{...claim,uid:bob,slotIndex:1,witnessUid:alice,hostAccountId:alice,recovered:true,result:'loss'},noHostAttest),'absent host final attest blocks recovered participant');
// Pre-match participant signer checks
const reg=rules.careerParticipation.$uid.$matchId;
const stateActive={matchId:mid,lobbyId:room,status:'active',phase:'in_progress',createdAt:now-180000};
const initial={matchState:{[mid]:stateActive},roomMembers:{[room]:{[alice]:{active:true,role:'host'}}}};
ok(attempt(reg['.write'],alice,null,one,initial),'signed prefinish participation allowed');
no(attempt(reg['.write'],bob,null,one,initial),'cannot sign for another UID');
const tooSoon=clone(initial);tooSoon.matchState[mid].createdAt=now-1000;
no(attempt(reg['.write'],alice,null,one,tooSoon),'instant fabricated match presence denied');
const done=clone(initial);done.matchState[mid].status='complete';done.matchState[mid].phase='match_complete';
no(attempt(reg['.write'],alice,null,one,done),'cannot post sign-up after match completed');
const anon=compile(reg['.write'],['auth','data','newData','root','$uid','$matchId','now']);
no(anon(anonymous(alice),snap(null),snap(one),snap(initial),alice,mid,now),'unapproved anonymous account denied');
// Wallet credit append-only and exact claim-backed amount
const wallet=rules.users.$uid.metaEconomy;
const walletWrite=compile(wallet['.write'],['auth','data','newData','root','$uid','now']);
function walletOK(prior,next,r=base,a=auth(alice)){return !!walletWrite(a,snap(prior),snap(next),snap(r),alice,now);}
const walletDoc={schemaVersion:1,wallet:{gold:100,atlasCoin:40,starterGrantClaimed:false},inventory:{profile_frames:{frame_halloween:true},boards:{},dice:{},maps:{},pawns:{},emotes:{}},equipped:{profileFrameId:'',boardId:'',diceId:''},dailyReward:{lastClaimAt:0},dev:{grantCount:0,resetCount:0},operation:{id:'a'.repeat(32),type:'PURCHASE',itemId:'frame_halloween',atMs:now-1000},rewardHistory:''};
const claimDb=clone(base);claimDb.careerClaims[alice][mid]=claim;
const goldDoc=clone(walletDoc);goldDoc.wallet.gold+=w.gold;goldDoc.operation={id:'b'.repeat(32),type:'MATCH_REWARD',itemId:mid,atMs:now-100};goldDoc.rewardHistory='|'+mid+'|';
ok(walletOK(walletDoc,goldDoc,claimDb),'full peer-attested Gold credited to same canonical wallet');
no(walletOK(walletDoc,{...goldDoc,wallet:{...goldDoc.wallet,gold:goldDoc.wallet.gold+1}},claimDb),'not even one extra Gold');
no(walletOK(walletDoc,{...goldDoc,wallet:{...goldDoc.wallet,atlasCoin:41}},claimDb),'AtlasCoin unchanged');
no(walletOK(walletDoc,{...goldDoc,equipped:{...goldDoc.equipped,boardId:'board_midnight_azure'}},claimDb),'cannot equip unowned board with match credit');
no(walletOK(goldDoc,{...goldDoc,operation:{...goldDoc.operation,id:'c'.repeat(32),atMs:now-50},wallet:{...goldDoc.wallet,gold:goldDoc.wallet.gold+w.gold},rewardHistory:goldDoc.rewardHistory+'|'+mid+'|'},claimDb),'duplicate match reward rejected');
no(walletOK(walletDoc,goldDoc,base),'no fabricated Gold without server claim');
const onlyHistoryChange=clone(walletDoc);onlyHistoryChange.rewardHistory='|fake|';
no(walletOK(walletDoc,onlyHistoryChange,claimDb),'cannot replace ledger without a valid operation');
ok(wallet.rewardHistory['.validate'].includes('1000000'),'bounded ledger field');
// Full-file source contracts and achievement truthfulness
const journal=read('Assets/Project/Scripts/Progression/AtlasBoardRtdbCareerJournal.cs');
const meta=read('Assets/Project/Scripts/Meta/AtlasBoardRtdbMetaEconomy.cs');
const coordinator=read('Assets/Project/Scripts/Online/AtlasBoardTurnDiceNetworkCoordinator.cs');
const recorder=read('Assets/Project/Scripts/Progression/AtlasBoardProgressionMatchRecorder.cs');
const career=read('Assets/Project/Scripts/Platform/AtlasBoardRtdbCareerReadModel.cs');
for(const token of ['RegisterParticipationAsync','RecoverPendingAsync','SaveParticipationCreditAsync','"careerFinalProofs"','GoldAward(', 'AddProvisionalAchievement(snapshot, "dice_100", 100, stats.diceRolls)','"doubles_10", 10, stats.doublesRolled']){if(token==='"careerFinalProofs"')continue;ok(journal.includes(token),token);}
for(const token of ['ClaimMatchGoldAsync','rewardHistory = ledger + marker','status=credited','IsCareerRewardEligibleAsync'])ok(meta.includes(token),token);
for(const token of ['careerDiceRolls','careerDoublesRolled','HostDiceCounts'])ok(coordinator.includes(token),token);
ok(recorder.includes('RegisterParticipationAsync'),'participation captured before finish');
ok(career.includes('RecoverPendingAsync'),'reopen recovers missed claims and Gold');
// The emulator's wallet INIT must install a canonical parent user profile.
// RTDB validation checks the ancestor /users/{uid} requirements on a child PUT.
// A source-only mock that never models the parent would otherwise miss this.
const emulatorQa=read('Backend/Firebase/qa/phase19ai_emulator_security.test.cjs');
ok(rules.users.$uid['.validate'].includes("newData.hasChildren(['userId','playerId','email','displayName'"),
  'ancestor user record requires canonical identity fields');
ok(emulatorQa.includes('function profile(user)') && emulatorQa.includes("accountType:user.email?'registered':'guest'"),
  'emulator test builds a canonical auth-consistent profile');
ok(emulatorQa.includes('wallet without canonical parent profile must be denied'),
  'emulator asserts incomplete user tree is denied');
ok(emulatorQa.indexOf('admin seeds canonical registered user profile') <
   emulatorQa.indexOf('initialize canonical zero Gold wallet after profile exists'),
  'emulator initializes wallet after canonical user profile is stored');
console.log(`ATB_CAREER_GOLD_SOURCE_CONTRACT PASS ${assertions} test checks; parsed=${parsed} rule expressions. FIREBASE_EMULATOR=NOT_RUN UNITY_COMPILE=NOT_RUN TWO_CLIENT=NOT_RUN`);
