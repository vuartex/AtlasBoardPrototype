const fs=require('fs'),assert=require('assert'),path=require('path');
const root=path.resolve(__dirname,'../../../');
const ui=fs.readFileSync(path.join(root,'Assets/Project/Scripts/UI/Menu/AtlasBoardUnifiedProfileScreen.cs'),'utf8');
const museum=fs.readFileSync(path.join(root,'Assets/Project/Scripts/Meta/AtlasBoardMuseumCollection.cs'),'utf8');
const checks=[
 ['paid check strokes reversed',ui.includes('shortStroke.localRotation = Quaternion.Euler(0, 0, 45)')&&ui.includes('longStroke.localRotation = Quaternion.Euler(0, 0, -42)')],
 ['confusing XP+Gold summary removed',!ui.includes('3 XP + 1 Gold reward')],
 ['Museum inventory under display',ui.includes('MuseumInventory')&&ui.includes('MuseumDisplay')],
 ['three interactive slots',ui.includes('MuseumSlot_')&&ui.includes('museumSelectedSlot = slot')],
 ['verified owned token list',museum.includes('collectibleTokens')&&museum.includes('Owned.AddRange(items)')],
 ['inventory pagination',ui.includes('museumPage * 12')],
 ['equip and remove actions',ui.includes('EquipMuseumAsync(false)')&&ui.includes('EquipMuseumAsync(true)')],
 ['account switching clears museum selection',ui.includes('museumSelectedToken = ""; museumSelectedSlot = -1;')],
 ['ETag slot write',museum.includes('if-match')&&museum.includes('"PUT", payload, old.etag')],
 ['no local grant or purchase',!museum.includes('promoXp')&&!museum.includes('wallet/gold')],
];
for(const [name,pass] of checks){assert(pass,name);console.log('PASS '+name)}
console.log('MUSEUM_UI_SOURCE_GATE='+checks.length+'/'+checks.length+' PASS; UNITY_COMPILER=NOT_RUN; MUSEUM_RULES=NOT_VERIFIED');
