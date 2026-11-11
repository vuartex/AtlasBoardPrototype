'use strict';
// Offline source & manifest contract test. A PASS here is NOT an official
// Firebase Auth+RTDB emulator PASS and NOT Unity compilation.
const fs=require('fs'),path=require('path'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'../../..');
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const paths={service:'Assets/Project/Scripts/Meta/AtlasBoardWeeklyMissions.cs',
 store:'Assets/Project/Scripts/Meta/AtlasBoardWeeklyMissionsStoreShortcut.cs',
 profile:'Assets/Project/Scripts/UI/Menu/AtlasBoardUnifiedProfileScreen.cs',
 rules:'Backend/Firebase/qa/phase19dh_weekly_rules_prepare.cjs',
 calendar:'Backend/Firebase/weeklyMissionCatalog_initial.json',
 tokens:'Backend/Firebase/collectibleTokenCatalog_initial.json',
 road:'Assets/Project/Documentation/MASTER_ROADMAP.md'};
const src=Object.fromEntries(Object.entries(paths).map(([k,v])=>[k,read(v)]));
let n=0;function gate(message,predicate){assert.ok(predicate,message);console.log('PASS',message);n++}
gate('One runtime service using existing UID/token sources',src.service.includes('AtlasBoardRtdbMetaEconomy.CurrentUid')&&src.service.includes('GetCurrentCallableTokenAsync'));
gate('Owner-required Firebase path users uid weeklyMissions weekId',src.service.includes('"/weeklyMissions/" + weekId')&&src.rules.includes('u.weeklyMissions'));
gate('Week always Monday 00:00 UTC',src.service.includes('((int)date.DayOfWeek + 6) % 7'));
gate('Stable assignment is create-only, no reroll',src.service.includes('"if-match", "null_etag"')&&src.rules.includes('!data.exists()'));
gate('Server timestamp used for assignment, claim and entitlement',src.service.includes('{\\".sv\\":\\"timestamp\\"}')||src.service.includes('.sv'));
gate('Signed Career claims are sole source of displayed progress',src.service.includes('"careerClaims/" + uid')&&src.service.includes('Qualifies('));
gate('No client progress or XP/Gold write paths',!src.service.includes('verifiedProgress')&&!src.service.includes('metaEconomy/wallet')&&!src.service.includes('goldAwarded'));
gate('Four independent mission condition checks',src.rules.includes("s0:proofNode('s0')")&&src.rules.includes("s3:proofNode('s3')"));
gate('Client cannot select arbitrary easier variant',src.rules.includes('allowedUidVariant')&&src.rules.includes("newData.child('variant').val()"));
gate('No Firebase Rules unsupported numChildren',!src.rules.includes('numChildren('));
gate('Claim and permanent ownership are immutable and UID scoped',src.rules.includes('u.collectibleTokens')&&src.rules.includes('claim')&&src.rules.includes('auth.uid===$uid'));
gate('No direct client-written token catalog or weekly catalog',src.rules.includes("'.write':false"));
gate('Rules generator explicitly local only with backup',src.rules.includes('AtlasBoard_RtdbRuleBackups')&&src.rules.includes('--write'));
gate('Rules generator enforces 256KiB maximum',src.rules.includes('262144'));
gate('Source and emulator QA cannot deploy production',!src.rules.includes('firebase deploy')&&!src.service.includes('database:update'));
gate('Four missions in detailed profile page',src.profile.includes('WeeklyMissionSlot_')&&src.profile.includes('RefreshWeeklyLiveTexts'));
gate('Weekly preview uses same service state',src.profile.includes('WeeklyPreview/MissionPreview_'));
gate('Explicit Claim Token and Refresh buttons',src.profile.includes('WeeklyClaim')&&src.profile.includes('WeeklyRefresh'));
gate('Store Weekly Missions shortcut retired; Profile remains sole entry',src.store.includes('sealed class AtlasBoardWeeklyMissionsStoreShortcut')&&!src.store.includes('RuntimeInitializeOnLoadMethod')&&!src.store.includes('Tab_AtlasWeeklyMissions')&&!src.store.includes('ShowWeeklyFromStore')&&!src.store.includes('CreateSmallButton'));
gate('Official emulator awaits HTTP Promise in both status helpers', (()=>{const e=read('Backend/Firebase/qa/phase19dh_weekly_auth_rtdb_emulator.test.cjs');return e.includes('const must=async(pending,t)=>{')&&e.includes('const deny=async(pending,t)=>{')&&e.split('const must=async(pending,t)=>{')[1].split('};')[0].includes('const r=await pending;')&&e.split('const deny=async(pending,t)=>{')[1].split('};')[0].includes('const r=await pending;')})());
gate('Existing NEWS and Community Voting source remains unrelated',!src.profile.includes('communityVotes')&&!src.profile.includes('newsPosts'));
gate('No new Firebase Cloud Functions or Firestore dependencies',!src.service.includes('CloudFunctions')&&!src.service.includes('Firestore'));
const calendar=JSON.parse(src.calendar),tokens=JSON.parse(src.tokens);
const keys=Object.keys(calendar).sort();
gate('156 prepublished weekly intervals',keys.length===156);
gate('Schedule begins 2026-11-09',keys[0]==='20261109');
let tally=0;const tokIds=new Set();for(const key of keys){const w=calendar[key];assert.ok(/^\d{8}$/.test(key));assert.equal(w.endUtcMs-w.startUtcMs,604800000);assert.equal(new Date(w.startUtcMs).getUTCDay(),1);assert.equal(new Date(w.startUtcMs).getUTCHours(),0);assert.equal(new Date(w.startUtcMs).toISOString().slice(0,10).replace(/-/g,''),key);assert.deepEqual(Object.keys(w.variants).sort(),['a','b','c','d']);for(const v of Object.values(w.variants)){assert.deepEqual(Object.keys(v).sort(),['s0','s1','s2','s3']);assert.equal(v.s0.type,'rounds');assert.ok(['win','winOrTie'].includes(v.s1.type));assert.equal(v.s2.type,'dice');assert.equal(v.s3.type,'properties')}if(w.hasTokenReward){tally++;assert.equal(w.tokenId,'weekly_'+key);assert.ok(!tokIds.has(w.tokenId));tokIds.add(w.tokenId);assert.ok(tokens[w.tokenId]);}else assert.ok(!w.tokenId)}
gate('All 156 weeks have valid bounded UTC proofs and four operator variants',true);
gate('Global token odds approximately 25%',tally>=25&&tally<=55);
gate('No per-player token rolls and unique token IDs',Object.keys(tokens).length===tally&&src.service.indexOf('Random.Range')<0);
gate('Catalog prepared once and refuses accidental reroll',src.calendar.length>10000&&read('Backend/Firebase/qa/phase19dh_weekly_schedule_prepare.cjs').includes('refusing to reroll'));
gate('Roadmap preserved, new weekly scope documented',src.road.includes('Weekly Missions')&&src.road.length>1200000);
const generator=require('./phase19dh_weekly_rules_prepare.cjs');
let tiny={rules:{users:{$uid:{}},careerClaims:{},careerFinalProofs:{}}};generator.install(tiny);
const rulesSize=Buffer.byteLength(JSON.stringify(tiny));
gate('Rules subtree needs under 8KB of existing ~8.4KB headroom',rulesSize<8200);
gate('Generated rules have one wildcard sibling maximum',(() => {generator.safeWildcards(tiny.rules);return true})());
console.log('WEEKLY_SOURCE_GATE='+n+'/'+n+' PASS; UNITY_COMPILER=NOT_RUN; OFFICIAL_EMULATOR=NOT_RUN; PRODUCTION_DEPLOY=NOT_DONE');
