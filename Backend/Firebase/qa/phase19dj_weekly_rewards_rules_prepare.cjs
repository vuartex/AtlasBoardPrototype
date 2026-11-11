'use strict';
// v5.49.12dj. Revisions to an ALREADY-DEPLOYED v5.49.12dh/di
// weekly-enabled rule tree. Source-only, local-only, with external backup.
// Existing economy predicates, Gold grants, user ownership and votes remain.
const fs=require('fs'),path=require('path'),os=require('os'),crypto=require('crypto');
const rulesPath=path.join(__dirname,'..','database.rules.json');
const MAX=262144;
const hash=s=>crypto.createHash('sha256').update(s).digest('hex');
const op="newData.child('operation/itemId').val()";
const typ="newData.child('operation/type').val()";
const rec=`root.child('users').child($uid).child('weeklyRewardReceipts').child(${op})`;
const cat="root.child('weeklyMissionCatalog').child(newData.child('weekId').val())";
const reward="root.child('weeklyRewardCatalog').child(newData.child('weekId').val()).child(newData.child('slot').val())";
const assignment="root.child('users').child($uid).child('weeklyMissions').child(newData.child('weekId').val()).child('assignment')";
const proof="root.child('careerClaims').child($uid).child(newData.child('matchId').val())";
const claim="root.child('careerClaims').child($uid).child(newData.val())";
function validateWildcards(n,p='rules'){
 if(!n||typeof n!=='object'||Array.isArray(n))return;
 const w=Object.keys(n).filter(x=>x[0]==='$');
 if(w.length>1)throw Error('Multiple wildcard siblings at '+p);
 for(const [k,v]of Object.entries(n))if(k[0]!=='.')validateWildcards(v,p+'/'+k);
}
function proofValidation(){
 const x="root.child('careerClaims').child($uid).child(newData.val())";
 const f=k=>`${x}.child('${k}').val()`;
 const c="root.child('weeklyMissionCatalog').child($weekId)";
 const m=`${c}.child('variants').child(root.child('users').child($uid).child('weeklyMissions').child($weekId).child('assignment/variant').val()).child($slot)`;
 return ["($slot=='s0'||$slot=='s1'||$slot=='s2'||$slot=='s3')",
  'newData.isString()',"newData.val().matches(/^[a-f0-9]{32}$/)",`${x}.exists()`,
  `${f('uid')}==$uid`,`${f('matchId')}==newData.val()`,`${f('playerCount')}>=2`,
  `${f('atMs')}>=${c}.child('startUtcMs').val()`,`${f('atMs')}<${c}.child('endUtcMs').val()`,
  `($slot=='s0'?${f('rounds')}>=${m}.child('target').val():`+
  `$slot=='s1'?(${f('result')}=='win'||(${m}.child('target').val()==0&&${f('result')}=='tie')):`+
  `$slot=='s2'?${f('diceRolls')}>=${m}.child('target').val():${f('properties')}>=${m}.child('target').val())`
 ].join('&&');
}
function receiptRule(){
 const w="newData.child('weekId').val()",s="newData.child('slot').val()";
 const c="root.child('weeklyMissionCatalog/'+newData.child('weekId').val())";
 const r="root.child('weeklyRewardCatalog/'+newData.child('weekId').val()+'/'+newData.child('slot').val())";
 const a="root.child('users/'+$uid+'/weeklyMissions/'+newData.child('weekId').val()+'/assignment')";
 const p="root.child('careerClaims/'+$uid+'/'+newData.child('matchId').val())";
 const v=`${a}.child('variant').val()`;
 const m=`${c}.child('variants').child(${v})`;
 const qualify=[
  `$rewardId==${w}+${s}`,
  `${c}.exists()`,`${a}.exists()`,`${r}.exists()`,
  `newData.child('kind').val()==${r}.child('kind').val()`,
  `newData.child('amount').val()==${r}.child('amount').val()`,
  `${p}.exists()`,
  `${p}.child('atMs').val()>=${c}.child('startUtcMs').val()`,
  `${p}.child('atMs').val()<${c}.child('endUtcMs').val()`,
  `(${s}=='s0'?${p}.child('rounds').val()>=${m}.child('s0/target').val():`+
  `${s}=='s1'?(${p}.child('result').val()=='win'||(${m}.child('s1/target').val()==0&&${p}.child('result').val()=='tie')):`+
  `${s}=='s2'?${p}.child('diceRolls').val()>=${m}.child('s2/target').val():`+
  `${p}.child('properties').val()>=${m}.child('s3/target').val())`
 ];
 // CareerClaims and the UID-derived assignment are independently immutable
 // under the original rules. Reward amounts are fixed by operator catalog.
 return {'$rewardId':{
  '.write':['auth!=null','auth.uid==$uid','!data.exists()','newData.exists()',...qualify].join('&&'),
  '.validate':"newData.hasChildren(['weekId','slot','matchId','kind','amount','claimedAt'])",
  weekId:{'.validate':"newData.isString()&&newData.val().matches(/^[0-9]{8}$/)"},
  slot:{'.validate':"newData.isString()&&newData.val().matches(/^s[0-3]$/)"},
  matchId:{'.validate':"newData.isString()&&newData.val().matches(/^[a-f0-9]{32}$/)"},
  kind:{'.validate':"newData.val()=='xp'||newData.val()=='gold'"},
  amount:{'.validate':'newData.isNumber()&&newData.val()>0&&newData.val()<=350'},
  claimedAt:{'.validate':'newData.isNumber()&&newData.val()==now'},
  '$other':{'.validate':false}
 }};
}

function replaceOnce(s,oldValue,newValue,label){
 const a=s.indexOf(oldValue); if(a<0||s.indexOf(oldValue,a+oldValue.length)>=0)throw Error('Unexpected rule fingerprint: '+label);
 return s.slice(0,a)+newValue+s.slice(a+oldValue.length);
}
function patchWallet(rules){
 const eco=rules.users.$uid.metaEconomy;
 let src=eco['.write'];
 if(src.includes("'WEEKLY_REWARD'"))throw Error('Already installed. Refusing repeat write.');
 const start=src.indexOf("||(auth!=null&&auth.uid===$uid&&data.exists()&&newData.exists()&&newData.child('operation/type').val()==='MATCH_REWARD'");
 const end=src.indexOf("||((auth!=null&&auth.uid===$uid)&&(data.exists()&&newData.exists())&&(newData.child('operation/type').val()==='PROMO')",start);
 if(start<0||end<start||end-start<7500)throw Error('Unexpected MATCH_REWARD predicate boundary; do not change rules');
 let x=src.slice(start,end);
 const oldGold="root.child('careerClaims').child($uid).child("+op+").child('goldAwarded').val()";
 const oldExists="root.child('careerClaims').child($uid).child("+op+").child('goldAwarded').exists()";
 x=replaceOnce(x,`${typ}==='MATCH_REWARD'`,`(${typ}=='MATCH_REWARD'||${typ}=='WEEKLY_REWARD')`,'operation type');
 x=replaceOnce(x,`${op}.matches(/^[A-Za-z0-9_-]{12,64}$/)`,`((${typ}=='MATCH_REWARD'&&${op}.matches(/^[A-Za-z0-9_-]{12,64}$/))||(${typ}=='WEEKLY_REWARD'&&${op}.matches(/^[0-9]{8}s[0-3]$/)))`,'id format');
 x=replaceOnce(x,oldExists,`((${typ}=='MATCH_REWARD'&&${oldExists})||(${typ}=='WEEKLY_REWARD'&&${rec}.exists()))`,'proof lookup');
 x=replaceOnce(x,`newData.child('wallet/gold').val()===data.child('wallet/gold').val() + ${oldGold}`,
  `newData.child('wallet/gold').val()==data.child('wallet/gold').val()+(${typ}=='MATCH_REWARD'?${oldGold}:(${rec}.child('kind').val()=='gold'?${rec}.child('amount').val():0))`,'Gold amount');
 const oldXp="(newData.child('promoXp').val()===data.child('promoXp').val()||(!data.child('promoXp').exists()&&newData.child('promoXp').val()===0))";
 const xpAmount=`(${rec}.child('kind').val()=='xp'?${rec}.child('amount').val():0)`;
 x=replaceOnce(x,oldXp,`((${typ}=='MATCH_REWARD'&&${oldXp})||(${typ}=='WEEKLY_REWARD'&&newData.child('promoXp').val()==(data.child('promoXp').exists()?data.child('promoXp').val():0)+${xpAmount}&&newData.child('worldClaims').val()==data.child('worldClaims').val()))`,'bonus XP invariants');
 src=src.slice(0,start)+x+src.slice(end);
 // The official rules language specifies == and === identical (and != and !==).
 // Save ~3KiB without altering the logical truth conditions of any existing rule.
 src=src.replace(/!==/g,'!=').replace(/===/g,'==');
 eco['.write']=src;
 eco.operation.type['.validate']=eco.operation.type['.validate'].replace("newData.val()=='EVENT_CLAIM'", "newData.val()=='EVENT_CLAIM'||newData.val()=='WEEKLY_REWARD'");
 // Some legacy rules still use strict notation before the == compaction.
 if(!eco.operation.type['.validate'].includes('WEEKLY_REWARD'))
   eco.operation.type['.validate']=eco.operation.type['.validate'].replace("newData.val()==='EVENT_CLAIM'", "newData.val()==='EVENT_CLAIM'||newData.val()==='WEEKLY_REWARD'");
 if(!eco.operation.type['.validate'].includes('WEEKLY_REWARD'))throw Error('Unexpected operation/type whitelist');
}
function install(doc){
 const r=doc.rules,u=r?.users?.$uid;
 if(!r?.careerClaims||!r?.weeklyMissionCatalog||!u?.weeklyMissions?.$weekId?.claim?.proofs||!u?.collectibleTokens)throw Error('Requires existing DI weekly rules');
 if(r.weeklyRewardCatalog||u.weeklyRewardReceipts)throw Error('DJ rules already installed');
 const proofs=u.weeklyMissions.$weekId.claim.proofs;
 if(!proofs.s0||!proofs.s1||!proofs.s2||!proofs.s3||!proofs.$other)throw Error('Existing signed token proof guard changed');
 u.weeklyMissions.$weekId.claim.proofs={
  '.validate':proofs['.validate'],
  '$slot':{'.validate':proofValidation()}
 };
 r.weeklyRewardCatalog={'.read':'auth!=null','.write':false};
 u.weeklyRewardReceipts=receiptRule();
 patchWallet(r);
 validateWildcards(r);
 return doc;
}
function main(){
 const before=fs.readFileSync(rulesPath,'utf8');
 const doc=JSON.parse(before);const revised=install(doc);
 let data=JSON.stringify(revised);
 // == is exactly === in Firebase Rules; compact everywhere (not only wallet).
 data=data.replace(/!==/g,'!=').replace(/===/g,'==');
 const size=Buffer.byteLength(data);
 console.log('PREVIOUS_RULES_SHA256='+hash(before));
 console.log('WEEKLY_DJ_RULES_BYTES='+size+'/'+MAX);
 if(size>MAX)throw Error('Rules exceed 256 KiB; NO CHANGES MADE');
 if(!process.argv.includes('--write')){console.log('DRY_RUN_ONLY=YES');return;}
 const dir=path.join(os.homedir(),'AtlasBoard_RtdbRuleBackups');fs.mkdirSync(dir,{recursive:true});
 const backup=path.join(dir,'database.rules.before_dj_'+hash(before).slice(0,16)+'.json');
 fs.writeFileSync(backup,before,{flag:'wx'});
 const tmp=rulesPath+'.dj.tmp';fs.writeFileSync(tmp,data);fs.renameSync(tmp,rulesPath);
 console.log('LOCAL_RULES_UPDATED=YES; BACKUP='+backup);
 console.log('PRODUCTION_DEPLOY=NOT_DONE; official Auth+RTDB emulator REQUIRED');
}
if(require.main===module){try{main()}catch(e){console.error('WEEKLY_DJ_RULES_ABORT='+e.message);process.exitCode=1;}}
module.exports={install,patchWallet,proofValidation,receiptRule,validateWildcards};
