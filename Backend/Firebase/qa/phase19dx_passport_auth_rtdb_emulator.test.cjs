'use strict';
// Safe against accidental use on production: SecurityQa enforces demo emulators.
const {SecurityQa,emulatorGuard}=require('./phase19bj_security_shared.cjs');
const assert=require('node:assert/strict');
const {passport,weeklyPassport,catalogueRule,compact}=require('./phase19dx_passport_rules_prepare.cjs');
const catalog=require('../passportStampCatalog_initial.json');
async function run(){
  emulatorGuard();
  const qa=new SecurityQa('PASSPORT_21_AUTH_RTDB');
  const normalize=node=>JSON.stringify(node, (key,value)=>key.startsWith('.')&&typeof value==='string'?compact(value):value);
  qa.ok(normalize(qa.rules.users.$uid.atlasPassport)===normalize(passport),'passport rules match exactly (ignoring safe whitespace)');
  qa.ok(JSON.stringify(qa.rules.passportStampCatalog)===JSON.stringify(catalogueRule),'locked catalog rules match');
  qa.setStage('operator-only seed & account isolation');
  const alice=await qa.signup('passport_owner');
  const bob=await qa.signup('passport_other');
  qa.allow(await qa.db('PUT',`users/${alice.uid}`,'owner',qa.profile(alice,'Passport Owner')),'operator seed owner');
  qa.allow(await qa.db('PUT',`users/${bob.uid}`,'owner',qa.profile(bob,'Passport Other')),'operator seed second');
  qa.allow(await qa.db('PUT','passportStampCatalog','owner',catalog),'operator seed 21 immutable names');
  qa.ok(Object.keys(catalog).length===21,'exact 21 catalog entries');
  qa.allow(await qa.db('GET','passportStampCatalog',alice),'catalog is readable');
  qa.deny(await qa.db('PUT','passportStampCatalog/tr_horizon',alice,catalog.tr_horizon),'player cannot edit catalog');
  const proof=qa.xid(),bad=qa.xid();
  const career={uid:alice.uid,matchId:proof,atMs:qa.now()-3000,mapId:'Turkey',result:'win',rounds:20,
    turns:25,properties:7,developments:5,diceRolls:12,doublesRolled:4,netWorth:7000,cash:3500};
  qa.allow(await qa.db('PUT',`careerClaims/${alice.uid}/${proof}`,'owner',career),'operator verified Career proof fixture');
  qa.deny(await qa.db('PUT',`careerClaims/${alice.uid}/${bad}`,alice,{...career,matchId:bad}),
    'client cannot fabricate witnessed proof');
  qa.setStage('immutable earned ownership');
  const own=`users/${alice.uid}/atlasPassport/owned/tr_horizon`;
  const receipt={proofId:proof};
  qa.allow(await qa.db('PUT',own,alice,receipt),'verified matching evidence earns stamp');
  qa.deny(await qa.db('PUT',own,alice,receipt),'duplicate stamp award rejected');
  qa.deny(await qa.db('DELETE',own,alice),'cannot delete earned record');
  qa.deny(await qa.db('PUT',`users/${bob.uid}/atlasPassport/owned/tr_horizon`,alice,receipt),'cross-UID ownership write denied');
  qa.deny(await qa.db('GET',`users/${alice.uid}/atlasPassport`,bob),'other UID cannot read passport');
  qa.deny(await qa.db('PUT',`users/${alice.uid}/atlasPassport/owned/mx_sun`,alice,receipt),
    'Mexico map requirement not satisfied with Turkey proof');
  qa.deny(await qa.db('PUT',`users/${alice.uid}/atlasPassport/owned/de_arch`,alice,{proofId:bad}),
    'nonexistent verification proof cannot earn a stamp');
  qa.deny(await qa.db('PUT',`users/${alice.uid}/atlasPassport/owned/tr_champion`,alice,{proofId:proof,xp:10000}),
    'unknown ownership attribute denied');
  qa.deny(await qa.db('PUT',`users/${alice.uid}/atlasPassport/owned/co_trail`,alice,{
    proofId:proof}),'Colorado proof cannot be forged');
  qa.setStage('legacy data, per-stamp UTC week quotas, ETag, removal and replacement');
  const page=`users/${alice.uid}/atlasPassport/pages/1`;
  qa.allow(await qa.db('PUT',page,'owner',{s0:'tr_horizon',s1:''}),
    'operator seeds pre-upgrade legacy page without changing player stamps');
  qa.eq(qa.parse(await qa.db('GET',page,alice)),{s0:'tr_horizon',s1:''},
    'legacy stamped page remains visible');
  qa.deny(await qa.db('PUT',page,alice,{s0:'',s1:''}),
    'legacy page writes are frozen');
  const week=(()=>{const n=qa.now();return n-(n-345600000)%604800000;})();
  const ledger=`users/${alice.uid}/atlasPassport/placementHistory/${week}`;
  const entry=`${ledger}/tr_horizon/p`;
  const action={i:'tr_horizon',p:2,s:0,t:qa.now()};
  qa.allow(await qa.db('PUT',entry,alice,action,{etag:'null_etag'}),
    'owner presses Turkey Horizon this week');
  qa.eq(qa.parse(await qa.db('GET',entry,alice)),action,'immutable stamp receipt persists');
  qa.precondition(await qa.db('PUT',entry,alice,{...action,p:3,s:1},{etag:'null_etag'}),
    'same stamp cannot be pressed twice this week using ETag');
  qa.deny(await qa.db('PUT',entry,alice,{...action,p:3,s:1}),
    'same stamp receipt cannot be overwritten');
  qa.deny(await qa.db('DELETE',entry,alice),
    'same stamp receipt cannot be deleted to reset limit');
  qa.deny(await qa.db('PUT',`${ledger}/tr_horizon/r`,alice,{...action,s:-1}),
    'stamp placed this week cannot be removed until next week');
  qa.allow(await qa.db('PUT',`users/${alice.uid}/atlasPassport/owned/long_voyage`,alice,receipt),
    'verified Career proof also earns a different eligible stamp');
  const otherStamp=`${ledger}/long_voyage/p`;
  const otherAction={...action,i:'long_voyage',p:3,s:1,t:qa.now()};
  qa.allow(await qa.db('PUT',otherStamp,alice,otherAction,{etag:'null_etag'}),
    'SECOND DIFFERENT STAMP can be pressed SAME WEEK without global lock');
  qa.precondition(await qa.db('PUT',otherStamp,alice,otherAction,{etag:'null_etag'}),
    'second stamp has its own immutable single weekly press');
  qa.deny(await qa.db('PUT',`${ledger}/long_voyage/r`,alice,{...otherAction,s:-2}),
    'second stamp also must wait a week before removal');
  qa.deny(await qa.db('PUT',`${ledger}/long_voyage/p`,bob,otherAction),
    'different user cannot overwrite an owned stamp');
  qa.deny(await qa.db('PUT',`users/${alice.uid}/atlasPassport/placementHistory/${week+604800000}/tr_horizon/p`,alice,action),
    'cannot pre-create next week placement');
  qa.deny(await qa.db('PUT',`users/${alice.uid}/atlasPassport/placementHistory/${week-604800000}/tr_horizon/p`,alice,action),
    'cannot backdate placement');
  qa.deny(await qa.db('PUT',`${ledger}/tr_horizon/custom`,alice,action),
    'unknown receipt action forbidden');
  qa.deny(await qa.db('PUT',`${ledger}/tr_horizon/p`,alice,{...action,i:'long_voyage'}),
    'stamp identity cannot be changed to bypass key validation');
  const another=await qa.signup('passport_third');
  qa.allow(await qa.db('PUT',`users/${another.uid}`,'owner',qa.profile(another,'Passport Third')),
    'seed independent third authenticated identity');
  const thirdLedger=`users/${another.uid}/atlasPassport/placementHistory/${week}`;
  qa.deny(await qa.db('PUT',`${thirdLedger}/tr_horizon/p`,another,action),
    'unowned stamp cannot be pressed');
  qa.allow(await qa.db('PUT',`users/${another.uid}/atlasPassport/owned/tr_horizon`,'owner',receipt),
    'operator seeds owned stamp for QA fixtures');
  qa.deny(await qa.db('PUT',`${thirdLedger}/tr_horizon/p`,another,{...action,p:0}),
    'identity page cannot hold a stamp');
  qa.deny(await qa.db('PUT',`${thirdLedger}/tr_horizon/p`,another,{...action,p:18}),
    'page number above 17 rejected');
  qa.deny(await qa.db('PUT',`${thirdLedger}/tr_horizon/p`,another,{...action,s:2}),
    'third slot rejected');
  qa.deny(await qa.db('PUT',`${thirdLedger}/tr_horizon/p`,another,{...action,bonusGold:5000}),
    'extra currency field rejected');
  qa.deny(await qa.db('PUT',`${thirdLedger}/tr_horizon/p`,another,{...action,p:1.5}),
    'non-integer page rejected');
  qa.deny(await qa.db('PUT',`${thirdLedger}/tr_horizon/p`,another,{i:'tr_horizon',p:2,s:0}),
    'stamp issue timestamp required');
  qa.deny(await qa.db('PUT',`${thirdLedger}/tr_horizon/p`,another,{...action,t:qa.now()-86400000}),
    'fake backdated stamp date rejected');
  qa.deny(await qa.db('PUT',`${thirdLedger}/tr_horizon/p`,another,{...action,t:qa.now()+86400000}),
    'fake future stamp date rejected');
  qa.allow(await qa.db('PUT',`users/${another.uid}/atlasPassport/pages/3`,'owner',
    {s0:'tr_horizon',s1:''}),'operator seeds legacy stamped slot');
  const archivedWeek=week-604800000;
  qa.allow(await qa.db('PUT',`users/${another.uid}/atlasPassport/placementHistory/${archivedWeek}`,
    'owner',{i:'tr_horizon',p:3,s:0}),
    'operator seeds previous-week legacy stamp receipt');
  const removePath=`${thirdLedger}/tr_horizon/r`;
  qa.allow(await qa.db('PUT',removePath,another,{...action,p:3,s:-1},{etag:'null_etag'}),
    'AFTER PREVIOUS WEEK, owner can remove the previously stamped seal');
  qa.precondition(await qa.db('PUT',removePath,another,{...action,p:3,s:-1},{etag:'null_etag'}),
    'one removal per stamp per week');
  qa.allow(await qa.db('PUT',`${thirdLedger}/tr_horizon/p`,another,{...action,p:4,s:0},{etag:'null_etag'}),
    'same stamp can be RE-PRESSED AFTER removal in current week');
  qa.deny(await qa.db('PUT',`${thirdLedger}/tr_horizon/r`,another,{...action,p:4,s:-1}),
    'removal still immutable after re-press');
  qa.allow(await qa.db('PUT',`users/${bob.uid}/atlasPassport/owned/tr_horizon`,'owner',receipt),
    'operator seeds fourth fixture: legacy current-week receipt');
  qa.allow(await qa.db('PUT',`users/${bob.uid}/atlasPassport/placementHistory/${week}`,'owner',
    {i:'tr_horizon',p:1,s:0}),
    'operator seeds an original v5.49.12ea week-only receipt');
  qa.deny(await qa.db('PUT',`users/${bob.uid}/atlasPassport/placementHistory/${week}/tr_horizon/p`,bob,
    {...action,p:3,s:0}),
    'legacy stamp already pressed THIS WEEK cannot evade lock with new path');
  qa.allow(await qa.db('PUT',`users/${bob.uid}/atlasPassport/owned/long_voyage`,'owner',receipt),
    'operator seeds another distinct stamp for legacy compatibility');
  qa.allow(await qa.db('PUT',`users/${bob.uid}/atlasPassport/placementHistory/${week}/long_voyage/p`,bob,
    {...otherAction,p:3,s:1}),
    'different stamp remains available even when old global receipt exists');
  qa.allow(await qa.db('PUT',`users/${alice.uid}/atlasPassport/view`,alice,{page:17,orientation:'portrait'}),
    'last page and portrait saved');
  qa.deny(await qa.db('PUT',`users/${alice.uid}/atlasPassport/view`,alice,{page:18,orientation:'portrait'}),
    'cannot navigate beyond 18 pages');
  qa.deny(await qa.db('PUT',`users/${alice.uid}/atlasPassport/view`,alice,{page:3,orientation:'sideways'}),
    'orientation must be portrait or landscape');
  qa.deny(await qa.db('PUT',`users/${alice.uid}/atlasPassport/view`,alice,{page:3,orientation:'landscape',extra:true}),
    'unknown view property rejected');
  qa.allow(await qa.db('PUT',`users/${alice.uid}/atlasPassport/view`,alice,{page:2,orientation:'landscape'}),
    'landscape and page navigation saved');
  qa.eq(qa.parse(await qa.db('GET',`users/${alice.uid}/atlasPassport/view`,alice)),
    {page:2,orientation:'landscape'},'view persists on server');
  qa.report({result:'PASS'});
  console.log('PASSPORT_21_AUTH_RTDB_EMULATOR_PASS=YES LIVE_FIREBASE_DEPLOY=NOT_DONE');
}
if(require.main===module)run().catch(e=>{console.error('PASSPORT_21_EMULATOR_FAIL='+e.stack);process.exitCode=1;});
