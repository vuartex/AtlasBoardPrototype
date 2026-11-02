'use strict';
const assert=require('assert'),fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'../../..');
const source=fs.readFileSync(path.join(root,'Assets/Project/Scripts/Progression/AtlasBoardRtdbCareerJournal.cs'),'utf8');
const wallet=fs.readFileSync(path.join(root,'Assets/Project/Scripts/Meta/AtlasBoardRtdbMetaEconomy.cs'),'utf8');
const rules=JSON.parse(fs.readFileSync(path.join(root,'Backend/Firebase/database.rules.json'),'utf8')).rules;
function xp(kind,rounds,properties,developments){
  return 25+(kind==='win'?50:kind==='tie'?20:0)+Math.min(100,Math.max(0,rounds)*3)+Math.min(50,Math.max(0,properties)*5)+Math.min(75,Math.max(0,developments)*3);
}
function gold(kind,rounds){
  if(!['win','tie','loss'].includes(kind))return 0;
  const outcome=kind==='win'?25:kind==='tie'?15:10;
  return Math.min(50,Math.max(0,rounds)+outcome);
}
let checks=0;
for(const kind of ['win','tie','loss'])for(const r of [0,1,5,10,20,30,100]){
  const n=gold(kind,r);assert(n>=10&&n<=50);checks++;
}
assert.strictEqual(gold('win',10),35);
assert.strictEqual(gold('tie',10),25);
assert.strictEqual(gold('loss',10),20);
assert.strictEqual(gold('win',20),45);
assert.strictEqual(gold('tie',20),35);
assert.strictEqual(gold('loss',20),30);
assert.strictEqual(gold('win',30),50);
assert.strictEqual(gold('tie',30),45);
assert.strictEqual(gold('loss',30),40);
assert(gold('win',10)>gold('tie',10)&&gold('tie',10)>gold('loss',10));
assert.strictEqual(gold('win',999),50);
assert.strictEqual(gold('invalid',10),0);
assert.strictEqual(xp('win',100,100,100),300);
assert(source.includes('int roundBonus = Mathf.Max(0, rounds);'));
assert(source.includes('int outcome = result == "win" ? 25 : result == "tie" ? 15 : 10;'));
assert(source.includes('Mathf.Min(50, roundBonus + outcome)'));
assert(!source.includes('int participation = 5;'));
assert(wallet.includes('public static async Task<bool> ClaimMatchGoldAsync('));
assert(wallet.includes('rewardHistory = ledger + marker'));
const claimValidate=rules.careerClaims.$uid.$matchId['.validate'];
assert(claimValidate.includes("result').val() === 'tie' ? 15 : 10"));
assert(claimValidate.includes('> 50 ? 50'));
assert(rules.careerClaims.$uid.$matchId.goldAwarded['.validate'].includes('<= 50'));
console.log(`ATB_GOLD_FORMULA_SOURCE_CONTRACT PASS scenarios=${checks} max=50 r10=35/25/20 r30=50/45/40 xpMaxUnchanged=300 source=peer_attested_rtdb firebaseEmulator=NOT_RUN`);
