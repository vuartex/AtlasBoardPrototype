'use strict';
const fs=require('fs'),path=require('path'),assert=require('node:assert/strict'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'../../..');
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const val=p=>JSON.parse(read(p));
const source=read('Assets/Project/Scripts/Meta/AtlasBoardWeeklyMissions.cs');
const wallet=read('Assets/Project/Scripts/Meta/AtlasBoardWeeklyRewardWallet.cs');
const ui=read('Assets/Project/Scripts/UI/Menu/AtlasBoardUnifiedProfileScreen.cs');
const rules=read('Backend/Firebase/qa/phase19dj_weekly_rewards_rules_prepare.cjs');
const original=val('Backend/Firebase/weeklyMissionCatalog_initial.json'),extension=val('Backend/Firebase/weeklyMissionCatalog_extension.json'),
 oldTokens=val('Backend/Firebase/collectibleTokenCatalog_initial.json'),newTokens=val('Backend/Firebase/collectibleTokenCatalog_extension.json'),
 rewards=val('Backend/Firebase/weeklyRewardCatalog_initial.json');
let n=0;function test(s,x){assert.ok(x,s);console.log('PASS',s);n++;}
const weeks={...original,...extension};const dates=Object.keys(weeks).sort();
test('Previously published 156 week nodes remain untouched',Object.keys(original).length===156);
test('Another 156 disjoint calendar weeks supplied',Object.keys(extension).length===156&&Object.keys(extension).every(k=>!original[k]));
test('312 weeks total prepublished',dates.length===312&&dates[0]==='20261109');
test('End of schedule is October 2032',dates.at(-1)==='20321025');
test('Every week aligned Monday 00:00 UTC',dates.every((d,i)=>{let w=weeks[d];return w.endUtcMs-w.startUtcMs===604800000&&new Date(w.startUtcMs).getUTCDay()===1&&(i===0||w.startUtcMs===weeks[dates[i-1]].startUtcMs+604800000)}));
test('Current 156 week token decisions are preserved',Object.values(oldTokens).length===Object.values(original).filter(w=>w.hasTokenReward).length);
test('Extension collectible token IDs disjoint',Object.keys(newTokens).every(k=>!oldTokens[k]));
test('New token schedule roughly one quarter of extension',Object.keys(newTokens).length>=22&&Object.keys(newTokens).length<=56);
test('Reward catalog covers all 312 Monday dates',Object.keys(rewards).length===312&&dates.every(k=>rewards[k]));
test('Every week has exactly 3 XP plus 1 Gold payout',dates.every(k=>['s0','s1','s2','s3'].every((s,i)=>rewards[k][s]?.kind===(i===3?'gold':'xp'))));
test('All 312 weeks: one flagship XP task pays 150–350 XP',dates.every(k=>Number.isInteger(rewards[k].s0.amount)&&rewards[k].s0.amount>=150&&rewards[k].s0.amount<=350)&&new Set(dates.map(k=>rewards[k].s0.amount)).size>=6);
test('Two additional XP tasks pay 75–125 XP and 90–140 XP',dates.every(k=>Number.isInteger(rewards[k].s1.amount)&&rewards[k].s1.amount>=75&&rewards[k].s1.amount<=125&&Number.isInteger(rewards[k].s2.amount)&&rewards[k].s2.amount>=90&&rewards[k].s2.amount<=140));
test('Gold task amount remains 15–30 and is not randomized on the client',dates.every(k=>Number.isInteger(rewards[k].s3.amount)&&rewards[k].s3.amount>=15&&rewards[k].s3.amount<=30));
test('Reward limits agree in Unity client, wallet and Firebase receipt validation',source.includes('r.amount > 350')&&wallet.includes('amount > 350')&&rules.includes('newData.val()<=350'));
test('Every reward amount is a positive integer within hard 350 cap',dates.every(k=>Object.values(rewards[k]).every(r=>Number.isInteger(r.amount)&&r.amount>0&&r.amount<=350)));
test('XP and Gold do not depend on token weeks',dates.some(k=>!weeks[k].hasTokenReward&&rewards[k].s0.amount>0));
test('No local RNG for reward rerolls',!source.includes('Random.Range(')&&!wallet.includes('Random.Range('));
test('User UID and witnessed Career proof remain required',source.includes('careerClaims/')&&rules.includes('careerClaims/'));
test('New immutable single reward receipt per UID/week/slot',rules.includes('weeklyRewardReceipts')&&rules.includes('!data.exists()'));
test('Reward amount and currency tied to immutable manifest',rules.includes("child('kind').val()")&&rules.includes("child('amount').val()")&&rules.includes('weeklyRewardCatalog'));
test('Reward proof validates round/dice/win/property stats',rules.includes("child('rounds').val()")&&rules.includes("child('diceRolls').val()")&&rules.includes("child('result').val()")&&rules.includes("child('properties').val()"));
test('Original token receipts and owned collectibles preserved',rules.includes('collectibleTokens')&&source.includes('PublishOwnershipAsync'));
test('Canonical economy payout type guarded by exact receipt',rules.includes('WEEKLY_REWARD')&&rules.includes('MATCH_REWARD')&&rules.includes('goldAwarded'));
test('Wallet XP and Gold use existing promoXp and wallet/gold',wallet.includes('wallet["promoXp"]')&&wallet.includes('balance["gold"]'));
test('Reward wallet uses ETag and 412 retry',wallet.includes('X-Firebase-ETag')&&wallet.includes('if-match')&&wallet.includes('status == 412'));
test('Append-only wallet ledger is idempotency authority',wallet.includes('rewardHistory')&&wallet.includes('history.Contains(marker)'));
test('No duplicate wallet, account, Career or Store controller instantiated',!wallet.includes('new GameObject(')&&!wallet.includes('new AtlasBoardMetaEconomyBridge'));
test('Wallet actively verifies unchanged UID',wallet.includes('SameUser(uid)')&&source.includes('SameUid(uid)'));
test('Old missing promoXp initialized safely',wallet.includes('wallet["promoXp"] == null'));
test('Four claim button gathers independently eligible completed tasks',source.includes('CollectRewardsAsync()')&&source.includes('v.complete[i]')&&source.includes('v.paid[i]'));
test('Claimed green check depends on credited wallet history',ui.includes('WeeklyPaidCheck')&&ui.includes('v.paid[i]')&&source.includes('rewardHistory'));
test('No more No Token This Week disabling rewards',!ui.includes('NO TOKEN THIS WEEK')&&!ui.includes('No token reward this week'));
test('One COLLECT REWARDS button with REFRESH remains',ui.includes('ÖDÜLLERİ AL')&&ui.includes('WeeklyRefresh'));
test('One-time payments disable claim button after payment',ui.includes('v.CanCollect')&&ui.includes('v.PaidCount == 4'));
test('Missing catalog says Yeni haftalık görev bulunamadı',ui.includes('Yeni haftalık görev bulunamadı')&&source.includes('scheduleMissing'));
test('Bonus token remains rare and is optional',source.includes('v.hasTokenReward')&&source.includes('ClaimAsync()'));
test('Career XP refreshed through original Progression Bridge',ui.includes('RefreshCareerAfterWeeklyPayout')&&source.includes('RefreshCareerAfterWeeklyPayout'));
test('Store weekly tab stays retired',!read('Assets/Project/Scripts/Meta/AtlasBoardWeeklyMissionsStoreShortcut.cs').includes('RuntimeInitializeOnLoadMethod'));
test('No Firebase deploy commands inside code generator',!rules.includes('firebase.cmd deploy')&&!rules.includes('database:update /'));
test('Rules generator is local-only, backup-protected, max 256 KiB',rules.includes('AtlasBoard_RtdbRuleBackups')&&rules.includes('262144')&&rules.includes('--write'));
const mock={rules:{careerClaims:{},weeklyMissionCatalog:{},users:{$uid:{
 weeklyMissions:{$weekId:{claim:{proofs:{'.validate':"newData.hasChildren(['s0','s1','s2','s3'])",s0:{'.validate':'true'},s1:{'.validate':'true'},s2:{'.validate':'true'},s3:{'.validate':'true'},'$other':{'.validate':false}}}}},
 collectibleTokens:{},metaEconomy:{'.write':"x",operation:{type:{'.validate':"newData.val()==='MATCH_REWARD'||newData.val()==='EVENT_CLAIM'"}}}
}}}};
const g=require('./phase19dj_weekly_rewards_rules_prepare.cjs');
test('Consolidated four proof validation is wildcard-safe',g.proofValidation().includes('$slot')&&g.proofValidation().includes("child('properties')"));
test('Receipt rules contain no extra wildcard siblings',(()=>{g.validateWildcards(g.receiptRule());return true})());
test('No unsupported numChildren() in new rules',!rules.includes('numChildren('));
test('Roadmap retains historical development log',read('Assets/Project/Documentation/MASTER_ROADMAP.md').length>1200000);
console.log(`WEEKLY_REWARD_SOURCE_GATE=${n}/${n} PASS; UNITY_COMPILER=NOT_RUN; OFFICIAL_EMULATOR=NOT_RUN; PRODUCTION_DEPLOY=NOT_DONE`);
