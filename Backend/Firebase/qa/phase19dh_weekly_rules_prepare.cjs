'use strict';
// Source-preparation only. NEVER deploys or writes live Firebase data.
// Preserves the entire existing v5.49.12dg rules tree and creates an operator
// backup outside the repository before local file replacement.
const fs = require('fs'), path = require('path'), os = require('os'), crypto = require('crypto');
const rulesFile = path.resolve(__dirname, '..', 'database.rules.json');
const maxBytes = 262144;
const catalog = "root.child('weeklyMissionCatalog').child($weekId)";
const assignment = "root.child('users').child($uid).child('weeklyMissions').child($weekId).child('assignment')";
const variants = ['a','b','c','d'];
const allowedUidVariant = "($uid.matches(/^[A-Fa-f0-3]/)?'a':($uid.matches(/^[G-Mg-m4-6]/)?'b':($uid.matches(/^[N-Tn-t7-8]/)?'c':'d')))";
const compact = s => s.replace(/\s+/g,' ').trim();
const signature = v => crypto.createHash('sha256').update(v).digest('hex');
function safeWildcards(node, trace='rules') {
  if (!node || typeof node !== 'object' || Array.isArray(node)) return;
  const ws = Object.keys(node).filter(k=>k.startsWith('$'));
  if (ws.length > 1) throw Error('Illegal sibling wildcard at '+trace+': '+ws.join(','));
  for (const [k,v] of Object.entries(node)) if (!k.startsWith('.')) safeWildcards(v,trace+'/'+k);
}
const common = ["auth!=null",'auth.uid===$uid',`${catalog}.exists()`,
  `${catalog}.child('startUtcMs').val()<=now`,`${catalog}.child('endUtcMs').val()>now`];
function proofNode(slot) {
  const r = "root.child('careerClaims').child($uid).child(newData.val())";
  const f = name => `${r}.child('${name}').val()`;
  const threshold = `${catalog}.child('variants').child(${assignment}.child('variant').val()).child('${slot}').child('target').val()`;
  const target = slot==='s0' ? `${f('rounds')}>=${threshold}` :
                 slot==='s1' ? `(${f('result')}==='win'||(${threshold}===0&&${f('result')}==='tie'))` :
                 slot==='s2' ? `${f('diceRolls')}>=${threshold}` :
                               `${f('properties')}>=${threshold}`;
  return {'.validate':[
    'newData.isString()',"newData.val().matches(/^[a-f0-9]{32}$/)",
    `${r}.exists()`, `${f('uid')}===$uid`, `${f('matchId')}===newData.val()`,
    `${f('playerCount')}>=2`,
    `${f('atMs')}>=${catalog}.child('startUtcMs').val()`,
    `${f('atMs')}<${catalog}.child('endUtcMs').val()`,
    target
  ].join('&&')};
}

function install(doc) {
  const root = doc.rules;
  if (!root || !root.users || !root.users.$uid || !root.careerClaims || !root.careerFinalProofs)
    throw Error('Unexpected baseline rules: career/UID contracts missing');
  const u = root.users.$uid;
  if (u.weeklyMissions || u.collectibleTokens || root.weeklyMissionCatalog || root.collectibleTokenCatalog)
    throw Error('Weekly mission rules already installed; refusing to overwrite');
  root.weeklyMissionCatalog = {'.read':'auth!=null','.write':false};
  root.collectibleTokenCatalog = {'.read':'auth!=null','.write':false};
  u.weeklyMissions = {
    '$weekId': {
      'assignment': {
        '.write': [ ...common, '!data.exists()','newData.exists()',
          `newData.child('weekId').val()===$weekId`,
          `newData.child('variant').val()===${allowedUidVariant}`,
          `newData.child('startUtcMs').val()===${catalog}.child('startUtcMs').val()`,
          `newData.child('endUtcMs').val()===${catalog}.child('endUtcMs').val()`,
          "newData.child('assignedAt').val()===now" ].join('&&'),
        '.validate':"newData.hasChildren(['weekId','variant','startUtcMs','endUtcMs','assignedAt'])",
        weekId:{'.validate':'newData.isString()&&newData.val()===$weekId'},
        variant:{'.validate':`newData.isString()&&newData.val()===${allowedUidVariant}`},
        startUtcMs:{'.validate':'newData.isNumber()'},endUtcMs:{'.validate':'newData.isNumber()'},
        assignedAt:{'.validate':'newData.isNumber()'},'$other':{'.validate':false}
      },
      'lastSeenServerAt': {
        '.write':[...common, `${assignment}.exists()`,
          'newData.isNumber()', 'newData.val()===now',
          '(!data.exists()||newData.val()>=data.val())'].join('&&'),
        '.validate':'newData.isNumber()&&newData.val()===now'
      },
      'claim': {
        '.write':[...common,'!data.exists()','newData.exists()',`${assignment}.exists()`,
          `${catalog}.child('hasTokenReward').val()===true`,
          `newData.child('tokenId').val()===${catalog}.child('tokenId').val()`,
          "newData.child('claimedAt').val()===now"].join('&&'),
        '.validate':"newData.hasChildren(['proofs','tokenId','claimedAt'])",
        tokenId:{'.validate':`newData.isString()&&newData.val()===${catalog}.child('tokenId').val()`},
        claimedAt:{'.validate':'newData.isNumber()&&newData.val()===now'},
        proofs:{
          '.validate':"newData.hasChildren(['s0','s1','s2','s3'])",
          s0:proofNode('s0'),s1:proofNode('s1'),s2:proofNode('s2'),s3:proofNode('s3'),
          '$other':{'.validate':false}
        },
        '$other':{'.validate':false}
      },
      '$other':{'.validate':false}
    }
  };
  // A valid claim is immutable. The separate permanent token entitlement is
  // created only from that SAME UID's immutable claim. A network interruption
  // between the two writes is repaired safely on the next profile refresh.
  const owningClaim = "root.child('users').child($uid).child('weeklyMissions').child(newData.child('weekId').val()).child('claim')";
  u.collectibleTokens = {
    '$tokenId': {
      '.write':[
        'auth!=null','auth.uid===$uid','!data.exists()','newData.exists()',
        "newData.child('weekId').isString()",
        "newData.child('tokenId').val()===$tokenId",
        `${owningClaim}.child('tokenId').val()===$tokenId`,
        "newData.child('acquiredAt').val()===now",
        "newData.child('source').val()==='weeklyMissions'"
      ].join('&&'),
      '.validate':"newData.hasChildren(['weekId','tokenId','acquiredAt','source'])",
      weekId:{'.validate':'newData.isString()&&newData.val().matches(/^[0-9]{8}$/)'},
      tokenId:{'.validate':'newData.isString()&&newData.val()===$tokenId'},
      acquiredAt:{'.validate':'newData.isNumber()&&newData.val()===now'},
      source:{'.validate':"newData.val()==='weeklyMissions'"},
      '$other':{'.validate':false}
    }
  };
  safeWildcards(root);
  return doc;
}
function main() {
  const current = fs.readFileSync(rulesFile,'utf8');
  const previous = JSON.parse(current);
  const ours = previous.rules?.users?.$uid?.weeklyMissions;
  if (ours) {
    const clean = JSON.parse(current);
    delete clean.rules.users.$uid.weeklyMissions;
    delete clean.rules.users.$uid.collectibleTokens;
    delete clean.rules.weeklyMissionCatalog;
    delete clean.rules.collectibleTokenCatalog;
    const expected = install(clean);
    if (JSON.stringify(expected) === JSON.stringify(previous)) {
      console.log('WEEKLY_RULES_ALREADY_PREPARED=YES; IDEMPOTENT_NO_WRITE');
      return;
    }
    throw Error('Unknown modified weekly rules: refusing to overwrite');
  }
  const revised = install(previous);
  const compactRules = JSON.stringify(revised);
  const size = Buffer.byteLength(compactRules);
  console.log('EXISTING_RULES_SHA256='+signature(current));
  console.log('WEEKLY_RULES_BYTES='+size+'/'+maxBytes);
  if (size > maxBytes) throw Error('Firebase 256 KiB rules limit exceeded; ORIGINAL LEFT UNCHANGED');
  if (process.argv.includes('--write')) {
    const backupDir = path.join(os.homedir(),'AtlasBoard_RtdbRuleBackups');
    fs.mkdirSync(backupDir,{recursive:true});
    const backup = path.join(backupDir,'database.rules.before_weekly_'+signature(current).slice(0,16)+'.json');
    fs.writeFileSync(backup,current,{flag:'wx'});
    const tmp = rulesFile + '.weekly.tmp';
    fs.writeFileSync(tmp,compactRules);
    fs.renameSync(tmp,rulesFile);
    console.log('LOCAL_RULES_UPDATED=YES; BACKUP='+backup);
  } else console.log('DRY_RUN_ONLY=YES; pass --write only after reviewed');
  console.log('PRODUCTION_DEPLOY=NOT_DONE; use official emulator first');
}
if (require.main===module) {try{main()}catch(e){console.error('WEEKLY_RULES_ABORT='+e.message);process.exitCode=1;}}
module.exports={install,safeWildcards};
