'use strict';
// Generates a COMPLETE local RTDB rules document from the owner's current file.
// It does not deploy, add managers, or touch any live Firebase data.
const fs=require('node:fs');
const path=require('node:path');
const os=require('node:os');
const crypto=require('node:crypto');
const RULES=path.join(__dirname,'..','database.rules.json');
const CATALOG=path.join(__dirname,'..','passportStampCatalog_initial.json');
const LIMIT=262144;
const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
const claim="root.child('careerClaims').child($uid).child(newData.child('proofId').val())";
const stamp="root.child('passportStampCatalog').child($stampId)";
const grant=`auth!=null&&auth.uid==$uid&&!data.exists()&&newData.exists()&&${stamp}.exists()&&${claim}.child('uid').val()==$uid&&${claim}.child(${stamp}.child('metric').val()).val()>=${stamp}.child('min').val()&&(${stamp}.child('map').val()=='*'||${claim}.child('mapId').val()==${stamp}.child('map').val())&&(${stamp}.child('win').val()==false||${claim}.child('result').val()=='win')`;
const own='auth!=null&&auth.uid==$uid';
// Exact original deployed Passport branch is accepted as an upgrade baseline.
const legacyPassport={

  owned:{'$stampId':{
    '.write':grant,
    proofId:{'.validate':"newData.isString()"},
    '$other':{'.validate':false}
  }},
  pages:{'$page':{
    '.write':own+"&&$page.matches(/^([1-9]|1[0-7])$/)&&newData.exists()",
    '.validate':"newData.hasChildren(['s0','s1'])&&(newData.child('s0').val()!=newData.child('s1').val()||newData.child('s0').val()=='')",
    '$slot':{'.validate':"$slot.matches(/^s[01]$/)&&newData.isString()&&(newData.val()==''||root.child('users').child($uid).child('atlasPassport/owned').child(newData.val()).exists())"}
  }},
  view:{
    '.write':own+'&&newData.exists()',
    '.validate':"newData.hasChildren(['page','orientation'])&&newData.child('page').isNumber()&&newData.child('page').val()>=0&&newData.child('page').val()<=17&&(newData.child('orientation').val()=='portrait'||newData.child('orientation').val()=='landscape')",
    '$other':{'.validate':false},
    page:{'.validate':'newData.isNumber()&&newData.val() % 1==0'},
    orientation:{'.validate':"newData.isString()"}
  }
};
// New placements are an immutable single UTC-week receipt. The old pages
// remain readable as legacy snapshots but can no longer be edited by clients.
const weeklyPassport=JSON.parse(JSON.stringify(legacyPassport));
const passport=JSON.parse(JSON.stringify(legacyPassport));
delete passport.pages;
passport.view.orientation={'.validate':true}; // parent already enforces portrait/landscape
const stampOld="root.child('passportStampCatalog').child($stampId)";
const stampNew="root.child('passportStampCatalog/'+$stampId)";
const proofOld="root.child('careerClaims').child($uid).child(newData.child('proofId').val())";
const proofNew="root.child('careerClaims/'+$uid+'/'+newData.child('proofId').val())";
passport.owned.$stampId['.write']=passport.owned.$stampId['.write']
    .split(stampOld).join(stampNew).split(proofOld).join(proofNew);
passport.placementHistory={'$week':{
  '.write':"auth!=null&&auth.uid==$uid&&!data.exists()&&newData.exists()&&$week==(now-(now-345600000)%604800000)+''&&root.child('users/'+$uid+'/atlasPassport/owned/'+newData.child('i').val()).exists()",
  '.validate':"newData.child('i').isString()&&newData.child('p').isNumber()&&newData.child('p').val()>=1&&newData.child('p').val()<=17&&newData.child('p').val()%1==0&&newData.child('s').isNumber()&&newData.child('s').val()>=-2&&newData.child('s').val()<=1&&newData.child('s').val()%1==0",
  i:{'.validate':true},p:{'.validate':true},s:{'.validate':true},'$other':{'.validate':false}
}};
// Deployed v5.49.12ea branch (dated one-global-action/week) remains an
// accepted upgrade baseline; never erase or modify its existing receipts.
// Preserve the EXACT deployed dz branch for upgrade comparison. The new
// timestamped weekly receipt is compatible with all old immutable receipts.
weeklyPassport.pages=undefined;
// Reuse the prior weekly format without constructing a second ruleset at runtime.
weeklyPassport.owned.$stampId['.write']=passport.owned.$stampId['.write'];
weeklyPassport.view.orientation={'.validate':true};
weeklyPassport.placementHistory=JSON.parse(JSON.stringify(passport.placementHistory));
// New receipts must carry an authenticated UTC write timestamp. Existing dz
// receipts without the field are untouched and remain readable forever.
passport.view.orientation={}; // parent validates the two permitted values.
passport.view['.validate']=passport.view['.validate'].replace("newData.child('page').isNumber()&&",'');
const weeklyRecord=passport.placementHistory.$week;
weeklyRecord['.write']=weeklyRecord['.write'].replace('&&newData.exists()','');
weeklyRecord['.validate']+="&&newData.child('t').isNumber()&&newData.child('t').val()>=now-300000&&newData.child('t').val()<=now+60000";
// The parent validates every field; empty child nodes are required so
// $other rejects extra keys while preserving the strict byte cap.
weeklyRecord.i={};weeklyRecord.p={};weeklyRecord.s={};weeklyRecord.t={};
const datedPassport=JSON.parse(JSON.stringify(passport));
// v5.49.12eb: receipts are grouped by UTC week, then by immutable stamp ID.
// A stamp has one placement (p) per UTC week and one removal (r). Removing
// a stamp placed in a PREVIOUS week can be followed by one press this week.
// Other stamps have independent permissions, even on the same day.
// Legacy receipts remain at /placementHistory/<week>/{i,p,s,t} unchanged.
const ledger="root.child('users/'+$uid+'/atlasPassport/placementHistory/'+$week)";
passport.placementHistory={'$week':{'$stampId':{'$action':{
  '.write':"auth!=null&&auth.uid==$uid&&!data.exists()&&newData.exists()&&$week==(now-(now-345600000)%604800000)+''&&root.child('users/'+$uid+'/atlasPassport/owned/'+$stampId).exists()&&newData.child('i').val()==$stampId&&("+
    ledger+".child('i').val()!=$stampId||($action=='p'&&"+ledger+".child('s').val()<0))&&($action=='p'?newData.child('s').val()>=0:$action=='r'&&newData.child('s').val()<0&&!"+ledger+".child($stampId+'/p').exists())",
  '.validate':"newData.hasChildren(['i','p','s','t'])&&newData.child('i').isString()&&newData.child('p').isNumber()&&newData.child('p').val()>=1&&newData.child('p').val()<=17&&newData.child('p').val()%1==0&&newData.child('s').isNumber()&&newData.child('s').val()>=-2&&newData.child('s').val()<=1&&newData.child('s').val()%1==0&&newData.child('t').isNumber()&&newData.child('t').val()>=now-300000&&newData.child('t').val()<=now+60000",
  i:{},p:{},s:{},t:{},'$other':{'.validate':false}
}}}};
const catalogueRule={'.read':true};
function countWildcards(t,p='rules'){
  if(!t||typeof t!=='object'||Array.isArray(t))return;
  const keys=Object.keys(t).filter(k=>k.startsWith('$'));
  if(keys.length>1)throw Error('Multiple wildcard siblings at '+p);
  for(const [k,v] of Object.entries(t))if(!k.startsWith('.'))countWildcards(v,p+'/'+k);
}
// Compact harmless spacing OUTSIDE quoted rule strings and regexes; preserve
// `in`, `new`, any significant alphanumeric-token separation and regex bodies.
function compact(s){
  let q=false,regexp=false,esc=false,out='';
  for(let i=0;i<s.length;i++){
    let ch=s[i];
    if(esc){out+=ch;esc=false;continue;}
    if(q||regexp){out+=ch;if(ch==='\\'){esc=true;continue;}if(q&&ch==="'")q=false;else if(regexp&&ch==='/')regexp=false;continue;}
    if(ch==="'"){q=true;out+=ch;continue;}
    if(ch==='/' && s[i-1]==='('){regexp=true;out+=ch;continue;}
    if(/\s/.test(ch)){
      const last=out[out.length-1]||'';
      const next=(s.slice(i+1).match(/\S/)||[''])[0];
      // Only punctuation-adjacent spaces are removed.
      if(!(/[A-Za-z_0-9$]/.test(last)&&/[A-Za-z_0-9$]/.test(next)))continue;
      if(last===' ')continue;
      out+=' ';continue;
    }
    out+=ch;
  }
  return out;
}
function build(input){
  const document=JSON.parse(input),root=document.rules||{},users=root.users&&root.users.$uid;
  if(!users||users['.write']!==false||users['.read']!==own||
      !users.collectibleDisplay||!root.careerClaims||!root.collectibleTokenCatalog)
    throw Error('Unknown current rules baseline; no changes made');
  let already=false;
  if(root.passportStampCatalog||users.atlasPassport){
    if(JSON.stringify(root.passportStampCatalog)!==JSON.stringify(catalogueRule))
      throw Error('Unknown existing passport catalog rules; refusing to overwrite');
    // A comparison of complete nested rules prevents overwriting a newer
    // feature or altering the user's other Firebase security settings.
    const normalize=x=>JSON.stringify(x,(k,v)=>k.startsWith('.')&&typeof v==='string'?compact(v):v);
    if(normalize(users.atlasPassport)===normalize(passport))return {doc:document,already:true};
    if(normalize(users.atlasPassport)!==normalize(datedPassport)&&
        normalize(users.atlasPassport)!==normalize(weeklyPassport)&&
        normalize(users.atlasPassport)!==normalize(legacyPassport))
      throw Error('Unexpected Passport rules; cannot upgrade safely');
  }
  const cat=JSON.parse(fs.readFileSync(CATALOG,'utf8'));
  if(Object.keys(cat).length!==21)throw Error('Expected 21 trusted stamp definitions');
  root.passportStampCatalog=catalogueRule;
  users.atlasPassport=JSON.parse(JSON.stringify(passport));
  countWildcards(root);
  // The existing RTDB file is close to 256 KiB. Compact rule expressions only,
  // never data paths, permitted operations, predicates or any string literals.
  function walk(node){if(!node||typeof node!=='object')return;
    for(const [k,v]of Object.entries(node)){
      if(k.startsWith('.')&&typeof v==='string')node[k]=compact(v);
      else if(v&&typeof v==='object')walk(v);
    }}
  let data=JSON.stringify(document);
  if(Buffer.byteLength(data)>LIMIT){walk(root);data=JSON.stringify(document)}
  // Retain the accepted emote entitlement checks exactly. Firebase RTDB
  // root.child('users').child($uid) and root.child('users/'+$uid)
  // resolve the same path for an authenticated UID (which cannot contain '/').
  // This expression-only compression recovers enough bytes for per-stamp
  // receipts without weakening or removing Store, Career or emote rules.
  if(Buffer.byteLength(data)>LIMIT){
    const emotes=users.emoteLoadout;
    if(!emotes)throw Error('Emote rules missing; cannot safely fit new Passport rules');
    for(const slot of ['slot1','slot2','slot3','slot4','slot5','slot6']){
      if(!emotes[slot]||typeof emotes[slot]['.validate']!=='string')
        throw Error('Unknown emote rules at '+slot+'; refuse to compact');
      emotes[slot]['.validate']=emotes[slot]['.validate'].split("root.child('users').child($uid)")
        .join("root.child('users/'+$uid)");
    }
    data=JSON.stringify(document);
  }
  if(Buffer.byteLength(data)>LIMIT)throw Error('Rules exceed Firebase 256 KiB limit ('+Buffer.byteLength(data)+'); no write');
  return {doc:document,already:false};
}
function main(){
  if(!fs.existsSync(RULES))throw Error('Full current database.rules.json missing');
  const before=fs.readFileSync(RULES,'utf8');
  const result=build(before),after=JSON.stringify(result.doc);
  console.log('PASSPORT_STAMPS=21 RULES_BASELINE_SHA256='+sha(before));
  console.log('RULES_SIZE='+Buffer.byteLength(after)+'/'+LIMIT+' PASSPORT_RULES='+ (result.already?'ALREADY_PRESENT':'READY'));
  if(!process.argv.includes('--write')){console.log('DRY_RUN_ONLY=YES LIVE_DEPLOY=NO');return;}
  if(result.already){console.log('NO_CHANGE_ALREADY_INSTALLED');return;}
  const dir=path.join(os.homedir(),'AtlasBoard_RtdbRuleBackups');fs.mkdirSync(dir,{recursive:true});
  const backup=path.join(dir,'database.rules.before_passport_per_stamp_'+sha(before).slice(0,16)+'.json');
  fs.writeFileSync(backup,before,{flag:'wx'});
  const temp=RULES+'.passport_per_stamp.tmp';fs.writeFileSync(temp,after,'utf8');fs.renameSync(temp,RULES);
  console.log('COMPLETE_LOCAL_RULES_WRITTEN=YES BACKUP='+backup+' LIVE_DEPLOY=NO');
}
if(require.main===module){try{main()}catch(e){console.error('PASSPORT_RULES_PREPARE_ABORT='+e.stack);process.exitCode=1;}}
module.exports={passport,datedPassport,weeklyPassport,legacyPassport,catalogueRule,build,compact};
