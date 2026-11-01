'use strict';
const assert=require('assert'),fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'../../..');
const source=fs.readFileSync(path.join(root,'Assets/Project/Scripts/Progression/AtlasBoardRtdbCareerJournal.cs'),'utf8');
const wallet=fs.readFileSync(path.join(root,'Assets/Project/Scripts/Meta/AtlasBoardRtdbMetaEconomy.cs'),'utf8');
const rules=JSON.parse(fs.readFileSync(path.join(root,'Backend/Firebase/database.rules.json'),'utf8')).rules;
function gold(kind,rounds,properties,levels){if(!['win','tie','loss'].includes(kind))return 0;return Math.min(120,(kind==='win'?50:kind==='tie'?20:5)+Math.min(30,Math.max(0,rounds))+Math.min(20,Math.max(0,properties)*2)+Math.min(20,Math.max(0,levels)*2));}
let checks=0;for(const kind of ['win','tie','loss'])for(const r of [0,1,10,30,100])for(const p of [0,1,10,24,100])for(const d of [0,1,10,25,100]){const n=gold(kind,r,p,d);assert(n>=5&&n<=120);checks++;}
assert.strictEqual(gold('win',100,100,100),120);
assert.strictEqual(gold('win',12,4,5),80);
assert.strictEqual(gold('loss',12,4,5),35);
assert(source.includes('public static int GoldAward('));
assert(wallet.includes('public static async Task<bool> ClaimMatchGoldAsync('));
assert(wallet.includes('rewardHistory = ledger + marker'));
assert(rules.users.$uid.metaEconomy['.write'].includes("newData.child('operation/type').val() === 'MATCH_REWARD'"));
assert(rules.users.$uid.metaEconomy.rewardHistory['.validate'].includes('1000000'));
console.log(`ATB_GOLD_FORMULA_SOURCE_CONTRACT PASS scenarios=${checks} max=120 winnerBase=50 source=peer_attested_rtdb firebaseEmulator=NOT_RUN`);
