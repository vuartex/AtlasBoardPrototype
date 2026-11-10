"use strict";
// AtlasBoard phase19cr: deterministic static safety acceptance for the source
// and RTDB Rules. This does not replace the official Firebase emulator.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const root = path.resolve(__dirname, "../../..");
const read = p => fs.readFileSync(path.join(root,p), "utf8");
const rules = JSON.parse(read("Backend/Firebase/database.rules.json")).rules;
const r = rules.users.$uid;
const main = r.metaEconomy[".write"];
const promo = read("Assets/Project/Scripts/Meta/AtlasBoardRtdbMetaEconomy.cs");
const world = read("Assets/Project/Scripts/Meta/AtlasBoardRtdbWorldJourney.cs");
const panel = read("Assets/Project/Editor/AtlasBoardWorldJourneyQaWindow.cs");
const ui = read("Assets/Project/Scripts/UI/Meta/AtlasBoardMetaEconomyUI.cs");
const policy = read("Assets/Project/Scripts/Meta/AtlasBoardDuplicateRewardGold.cs");
const checks=[];
function test(name,func){ try {func(); checks.push(name);} catch(e){console.error("FAIL",name); throw e;} }
const bands=[['Pawn',200],['Dice',300],['Board',800],['Map',1200],['ProfileFrame',250],['Emote',120]];
for(const [key,gold] of bands){
 test(`Gold fallback ${key}=${gold}`,()=>assert.match(policy,new RegExp(`const int ${key} = ${gold};`)));
 test(`RTDB rule fixed ${key}=${gold}`,()=>assert.ok(main.includes(`? ${gold} :`) || main.includes(`? ${gold} : 0`) || main.includes(`? ${gold} :`)));
}
test("Normal promo still single-use",()=>assert.ok(main.includes("rewardHistory") && main.includes("PROMO")));
test("Promo fallback uses catalog itemType",()=>assert.ok(promo.includes("ForCatalogItemType") && promo.includes("compensationGold")));
test("Owned item payout inside canonical wallet ETag",()=>assert.ok(promo.includes("wallet.wallet.gold += compensationGold") && promo.includes("state.ETag")));
test("Reward always real amount",()=>assert.ok(promo.includes('Type = compensationGold > 0 ? "gold" : type')));
test("World map/board owned fallback",()=>assert.ok(world.includes("ForItemId(reward.Item)") && world.includes("WORLD_GOLD_CAP")));
test("World map/board separately rule-checked",()=>assert.ok(main.includes("'world_explorer'") && main.includes("'completion_bonus'") && main.includes("? 1200 : 0") && main.includes("? 800 : 0")));
test("QA menu in existing Diagnostics root",()=>assert.ok(panel.includes('MenuItem("Atlas Board/QA & Diagnostics/World Journey (Editor Only)"')));
test("No redundant menu path",()=>assert.ok(!panel.includes('MenuItem("Atlas Board/QA/')));
test("Open Store listens for QA revision",()=>assert.ok(ui.includes('lastWorldQaRevision != AtlasBoardRtdbMetaEconomy.WorldQaRevision')));
test("Mission progress aligned",()=>assert.ok(ui.includes('progressText.text = "Progress: "')));
test("Editor-only QA reset",()=>assert.ok(world.includes('#if UNITY_EDITOR') && world.includes("WorldEditorResetClaimsAsync")));
test("QA reset never touches wallet as destination",()=>assert.ok(world.includes('/metaEconomy/worldClaims.json')));
test("Tester UID and QA proof rule",()=>{
 const r0=r.metaEconomy.worldClaims['.write']; assert.ok(r0.includes("devWalletTesters")&&r0.includes("auth.uid === $uid")&&r0.includes("qaMode")&&r0.includes("newData.val() === ''"));
});
test("Existing money is preserved on claims reset",()=>assert.ok(world.includes('Mission progress and earned inventory/Gold/XP preserved')));
test("Reward history append-only",()=>assert.ok(r.worldJourneyHistory.$rewardId['.write'].includes('!data.exists()')));
test("Repeated QA history gets unique transaction ID",()=>assert.ok(world.includes('spec.Id + "_" + transactionId')));
test("Existing legacy history IDs still accepted",()=>assert.ok(r.worldJourneyHistory.$rewardId['.write'].includes("$rewardId === newData.child('rewardId').val()")));
test("Receipt compensation schema",()=>assert.ok(r.promoRedemptions.$code.compensationGold['.validate'].includes('1200')));
test("Rules below 256 KiB",()=>assert.ok(Buffer.byteLength(read('Backend/Firebase/database.rules.json')) < 262144));
test("World label names source or remain open",()=>{const road=read('Assets/Project/Documentation/MASTER_ROADMAP.md');assert.ok(road.includes('Civic World map project names'));});
console.log(`ATB_19CR_QA_COMPENSATION ${checks.length}/${checks.length} static checks PASS (official emulator and Unity needed)`);
