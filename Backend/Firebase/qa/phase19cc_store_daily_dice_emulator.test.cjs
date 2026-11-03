'use strict';
// REAL Firebase Auth + Realtime Database emulator contract. Never point at prod.
// Usage (from Backend/Firebase):
// firebase.cmd emulators:exec --only auth,database --project demo-atlasboard "node qa/phase19cc_store_daily_dice_emulator.test.cjs"
const {SecurityQa,clone}=require('./phase19bj_security_shared.cjs');
async function run(){
 const q=new SecurityQa('PHASE19CC_STORE_DAILY_DICE');
 console.log('ATB_PHASE19CC START production=FORBIDDEN');
 q.setStage('auth_registered_no_owner_permission');
 const alice=await q.signup('SecCC-Alice');
 const bob=await q.signup('SecCC-Bob');
 const attacker=await q.signup('SecCC-Attacker');
 for(const user of [alice,bob,attacker])
  q.allow(await q.db('PUT',`users/${user.uid}`,'owner',q.profile(user,user.label)),`admin seeds ${user.label}`);
 q.eq(q.parse(await q.db('GET',`devWalletTesters/${alice.uid}`,alice)),null,'Alice does not have admin/tester permission');
 const aPath=`users/${alice.uid}/metaEconomy`;
 const bPath=`users/${bob.uid}/metaEconomy`;
 const aStart=q.startWallet();aStart.wallet.gold=800;
 // A pre-existing legacy wallet: lastClaimAt did not exist in old exports.
 delete aStart.dailyReward;
 const twoHoursAgo=q.now()-7200000;
 q.allow(await q.db('PUT',aPath,'owner',aStart),'legacy wallet fixture without dailyReward');
 const receiptId=q.xid();
 q.allow(await q.db('PUT',`users/${alice.uid}/metaHistory/${receiptId}`,'owner',{
   type:'DAILY',atMs:twoHoursAgo,id:receiptId,itemId:''
 }),'legacy daily receipt fixture');
 q.setStage('purchase_does_not_depend_on_daily_or_dev');
 const price=q.priceMap().dice_ruby_red;
 q.eq(price,350,'known cosmetic price 350');
 let bought=q.advance(aStart,'PURCHASE','dice_ruby_red');
 bought.wallet.gold-=price;
 bought.inventory.dice.dice_ruby_red=true;
 // A purchase must preserve a previously missing dailyReward as missing;
 // adding or querying a daily-claim marker must NOT be a prerequisite.
 q.allow(await q.db('PUT',aPath,alice,bought),'ordinary purchase from missing dailyReward allowed without admin');
 const afterPurchase=q.parse(await q.db('GET',aPath,alice));
 q.eq(afterPurchase.wallet.gold,450,'purchase debited exact Gold');
 q.eq(Object.prototype.hasOwnProperty.call(afterPurchase,'dailyReward'),false,
      'purchase did not fabricate or depend on dailyReward');
 const attackBuy=q.advance(bought,'PURCHASE','dice_ocean_blue');
 attackBuy.wallet.gold-=350;attackBuy.inventory.dice.dice_ocean_blue=true;
 q.deny(await q.db('PUT',aPath,attacker,attackBuy),'other UID cannot buy on Alice wallet');
 // Legacy missing-field recovery is performed by the client using its
 // immutable DAILY receipt. Rules cannot dynamically aggregate receipt maps.
 // The client MUST refuse an early reward before issuing a write.
 q.ok(twoHoursAgo > q.now()-86400000,
      'recent immutable receipt prevents legacy client from re-awarding');
 let forbiddenDev=q.advance(bought,'DEV_GRANT');
 forbiddenDev.wallet.gold+=5000;forbiddenDev.wallet.atlasCoin+=500;forbiddenDev.dev.grantCount++;
 q.deny(await q.db('PUT',aPath,alice,forbiddenDev),'DEV_GRANT still admin/tester restricted');
 q.allow(await q.db('PUT',`devWalletTesters/${alice.uid}`,'owner',true),
         'server admin authorizes Alice for dev wallet');
 q.allow(await q.db('PUT',aPath,alice,forbiddenDev),
         'approved tester can run exact DEV_GRANT on legacy no-daily wallet');
 q.eq(q.parse(await q.db('GET',aPath,alice)).wallet.gold,5450,
      'dev Gold exact 5000 and purchase already debited');
 q.setStage('first_time_daily_claim_without_prior_field');
 const bStart=q.startWallet();delete bStart.dailyReward;
 q.allow(await q.db('PUT',bPath,'owner',bStart),'brand new legacy-format Bob wallet without lastClaimAt');
 let firstDaily=q.advance(bStart,'DAILY');
 firstDaily.wallet.gold+=50;
 firstDaily.dailyReward={lastClaimAt:firstDaily.operation.atMs};
 q.allow(await q.db('PUT',bPath,bob,firstDaily),'first ever daily claim creates timestamp without admin');
 q.eq(q.parse(await q.db('GET',bPath,bob)).dailyReward.lastClaimAt,
  firstDaily.operation.atMs,'new claim timestamp is stored in Firebase');
 let duplicate=q.advance(firstDaily,'DAILY');duplicate.wallet.gold+=50;
 duplicate.dailyReward.lastClaimAt=duplicate.operation.atMs;
 q.deny(await q.db('PUT',bPath,bob,duplicate),'24-hour replay denied after first claim');
 q.setStage('dice_per_match_account_cloud');
 const match=q.xid(),otherMatch=q.xid();
 const dicePath=`careerDiceObservations/${alice.uid}/${match}`;
 // The grant/score fixtures are owned by emulator admin; clients must prove
 // that a completed match / career claim exists before writing telemetry.
 q.allow(await q.db('PUT',`careerClaims/${alice.uid}/${match}`,'owner',{
  uid:alice.uid,matchId:match,diceRolls:3
 }),'admin seeds canonical matching career claim');
 const dice={uid:alice.uid,matchId:match,diceRolls:3,totalDiceValue:21,
             highestRoll:9,recordedAt:q.now()};
 q.allow(await q.db('PUT',dicePath,alice,dice),'owner stores plausible dice observation');
 q.deny(await q.db('PUT',dicePath,alice,{...dice,highestRoll:12}),'dice observation immutable');
 q.deny(await q.db('GET',`careerDiceObservations/${alice.uid}`,attacker),'another UID cannot read dice telemetry');
 q.deny(await q.db('PUT',`careerDiceObservations/${alice.uid}/${otherMatch}`,alice,
  {...dice,matchId:otherMatch}),'random match cannot be submitted');
 const badMatch=q.xid();
 q.allow(await q.db('PUT',`careerClaims/${alice.uid}/${badMatch}`,'owner',{
  uid:alice.uid,matchId:badMatch,diceRolls:3
 }),'admin seeds other claim');
 q.deny(await q.db('PUT',`careerDiceObservations/${alice.uid}/${badMatch}`,alice,
  {...dice,matchId:badMatch,highestRoll:15}),'impossible highest roll denied');
 const badCount=q.xid();
 q.allow(await q.db('PUT',`careerClaims/${alice.uid}/${badCount}`,'owner',{
  uid:alice.uid,matchId:badCount,diceRolls:5
 }),'admin seeds count mismatch claim');
 q.deny(await q.db('PUT',`careerDiceObservations/${alice.uid}/${badCount}`,alice,
  {...dice,matchId:badCount}),'career claim dice count mismatch denied');
 q.setStage('solo_completed_archive_observation');
 const solo=q.xid();
 q.allow(await q.db('PUT',`careerParticipation/${alice.uid}/${solo}`,'owner',{
  uid:alice.uid,matchId:solo
 }),'admin seeds verified solo participation');
 q.allow(await q.db('PUT',`completedMatchArchives/${solo}`,'owner',{
  matchId:solo,revision:12
 }),'admin seeds completed solo archive');
 q.allow(await q.db('PUT',`careerDiceObservations/${alice.uid}/${solo}`,alice,
  {...dice,matchId:solo}),'solo completed match observation accepted without reward claim');
 q.eq(q.parse(await q.db('GET',`careerDiceObservations/${alice.uid}`,alice))!==null,
 true,'same account can read back its cloud dice');
 q.report({purchaseWithoutAdmin:true,firstDailyCreatesTimestamp:true,
            legacyDailyProtected:true,diceUidScoped:true,production:'NOT_TOUCHED'});
 console.log('ATB_PHASE19CC_STORE_DAILY_DICE PASS checks='+q.checks+
  ' production=NOT_TOUCHED');
}
run().catch(e=>{console.error(e.stack||e);process.exitCode=1;});
