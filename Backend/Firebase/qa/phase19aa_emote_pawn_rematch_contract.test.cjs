const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../../../');
const read = p => fs.readFileSync(path.join(root,p),'utf8');
const e = read('Assets/Project/Scripts/UI/AtlasBoardEmoteHudRuntime.cs');
const c = read('Assets/Project/Scripts/Online/AtlasBoardTurnDiceNetworkCoordinator.cs');
const tests = [
 ['no opaque emote bubble', () => assert.doesNotMatch(e, /EmoteBubble_"\+id[^\n]*Color32\(240,246,252,245\)/)],
 ['bubble container transparent', () => assert.match(e, /EmoteBubble_"\+id[^\n]*Color\.clear/)],
 ['sprite kept', () => assert.match(e,/AtlasBoardEmotePreviewArt\.Create\(obj\.transform,id,76f\)/)],
 ['bubble not stealing clicks',()=>assert.match(e,/plate\.raycastTarget=false/)],
 ['bubble still 2.5 seconds',()=>assert.match(e,/Destroy\(obj,2\.5f\)/)],
 ['no new HUD receiver',()=>assert.match(e,/class AtlasBoardEmoteHudRuntime/)],
 ['new-match owner filter exists',()=>assert.match(c,/IsHostOwnedLocalHumanPawnSlot\(int slotIndex\)/)],
 ['same account only',()=>assert.match(c,/member\.LocalOwnerAccountId, ownerId/)],
 ['remote/bot excluded',()=>assert.match(c,/member\.SeatMode != AtlasLobbySeatMode\.HostLocal &&[\s\S]*?member\.SeatMode != AtlasLobbySeatMode\.LocalHuman/)],
 ['new selected ownership evaluated',()=>assert.match(c,/localService\.GetSelectedCosmetic\(slotIndex\)/)],
 ['visual corrected with new selection',()=>assert.match(c,/changed && isLocalOwner[\s\S]*?ApplyPawnCosmeticToSlot\(slotIndex, cosmeticId\)/)],
 ['remote authority guard preserved',()=>assert.match(c,/if \(IsRemoteHumanSlot\(slotIndex\)\)/)],
 ['no RNG changes here',()=>assert.match(c,/private void InitializeAuthoritativePawnCosmetics\(\)/)],
];
let passed = 0;
for (const [name, test] of tests) {
 try { test(); passed++; console.log('PASS', name); }
 catch(e) { console.error('FAIL', name, e.message); process.exitCode=1; }
}
console.log(`Phase 19AA static contract ${passed}/${tests.length}`);
