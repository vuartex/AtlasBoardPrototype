'use strict';
// Produces the COMPLETE existing RTDB rules document locally, never a diff.
// Owner explicitly chooses --write; no network operations or Firebase deploy.
const fs=require('node:fs');
const path=require('node:path');
const os=require('node:os');
const crypto=require('node:crypto');
const rulesFile=path.join(__dirname,'..','database.rules.json');
const LIMIT=262144;
const hash=s=>crypto.createHash('sha256').update(s).digest('hex');
const display={
  '.write': 'auth!=null&&auth.uid==$uid&&newData.exists()',
  '.validate': "newData.hasChildren(['s0','s1','s2'])&&(newData.child('s0').val()==''||newData.child('s0').val()!=newData.child('s1').val()&&newData.child('s0').val()!=newData.child('s2').val())&&(newData.child('s1').val()==''||newData.child('s1').val()!=newData.child('s2').val())",
  '$slot':{'.validate':"$slot.matches(/^s[012]$/)&&newData.isString()&&(newData.val()==''||root.child('users').child($uid).child('collectibleTokens').child(newData.val()).exists())"}
};
function validateWildcards(node,p='rules'){
  if(!node||typeof node!=='object'||Array.isArray(node))return;
  const wildcards=Object.keys(node).filter(x=>x.startsWith('$'));
  if(wildcards.length>1)throw Error('Multiple wildcard siblings at '+p);
  for(const [k,v]of Object.entries(node))if(!k.startsWith('.'))validateWildcards(v,p+'/'+k);
}
function install(doc){
  const rules=doc&&doc.rules, user=rules&&rules.users&&rules.users.$uid;
  if(!user||!user.collectibleTokens||!user.weeklyMissions||!user.metaEconomy||!rules.collectibleTokenCatalog)
    throw Error('Missing existing Museum/Weekly/Economy branches; refusing unknown rules baseline');
  if(user.collectibleDisplay){
    if(JSON.stringify(user.collectibleDisplay)===JSON.stringify(display))return {doc,installed:false};
    throw Error('Found unknown existing collectibleDisplay rule; no overwrite');
  }
  const originalTokenWrite=user.collectibleTokens.$tokenId&&user.collectibleTokens.$tokenId['.write'];
  if(!originalTokenWrite||!originalTokenWrite.includes("newData.child('source').val()=='weeklyMissions'"))
    throw Error('Existing immutable token ownership guard unexpectedly changed');
  if(user['.read']!=='auth!=null&&auth.uid==$uid')
    throw Error('Unknown users/$uid read boundary; no changes');
  if(!user.metaEconomy['.write'].includes('MATCH_REWARD'))
    throw Error('Unexpected canonical wallet predicate; no changes');
  user.collectibleDisplay=JSON.parse(JSON.stringify(display));
  validateWildcards(rules);
  return {doc,installed:true};
}
function build(before){
  const beforeDoc=JSON.parse(before);
  const outcome=install(beforeDoc);
  // Whitespace compaction is semantics-neutral in Firebase rule expressions;
  // no branch, predicate or existing currency state is intentionally changed.
  let text=JSON.stringify(outcome.doc);
  if(Buffer.byteLength(text)>LIMIT){
    const expr=outcome.doc.rules.users.$uid.metaEconomy['.write'];
    outcome.doc.rules.users.$uid.metaEconomy['.write']=expr.replace(/ \+ /g,'+');
    text=JSON.stringify(outcome.doc);
  }
  if(Buffer.byteLength(text)>LIMIT)throw Error('Exceeds 256 KiB limit; no changes');
  if(!outcome.doc.rules.users.$uid.collectibleDisplay.$slot['.validate'].includes('collectibleTokens'))
    throw Error('Owned-token check missing');
  return {text,installed:outcome.installed};
}
function main(){
  if(!fs.existsSync(rulesFile))throw Error('Original complete database.rules.json not found');
  const before=fs.readFileSync(rulesFile,'utf8');
  const result=build(before);
  console.log('RULES_BASELINE_SHA256='+hash(before));
  console.log('MUSEUM_RULES_BYTES='+Buffer.byteLength(result.text)+'/'+LIMIT);
  console.log('MUSEUM_DISPLAY_RULES='+(!result.installed?'ALREADY_PRESENT':'READY'));
  if(!process.argv.includes('--write')){
    console.log('DRY_RUN_ONLY=YES; PRODUCTION_DEPLOY=NOT_DONE');return;
  }
  if(!result.installed){console.log('NO_CHANGE_ALREADY_INSTALLED');return;}
  const folder=path.join(os.homedir(),'AtlasBoard_RtdbRuleBackups');
  fs.mkdirSync(folder,{recursive:true});
  const backup=path.join(folder,'database.rules.before_dq_'+hash(before).slice(0,16)+'.json');
  fs.writeFileSync(backup,before,{flag:'wx'});
  const tmp=rulesFile+'.museum_dq.tmp';
  fs.writeFileSync(tmp,result.text);
  fs.renameSync(tmp,rulesFile);
  console.log('COMPLETE_LOCAL_RULES_WRITTEN=YES');
  console.log('BACKUP='+backup);
  console.log('PRODUCTION_DEPLOY=NOT_DONE');
}
if(require.main===module){try{main()}catch(e){console.error('MUSEUM_RULES_PREPARE_ABORT='+e.message);process.exitCode=1;}}
module.exports={install,build,display};
