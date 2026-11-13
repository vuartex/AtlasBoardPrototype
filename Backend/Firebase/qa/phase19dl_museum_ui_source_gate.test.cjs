const fs=require('fs'),assert=require('assert'),path=require('path');
const root=path.resolve(__dirname,'../../../');
const ui=fs.readFileSync(path.join(root,'Assets/Project/Scripts/UI/Menu/AtlasBoardUnifiedProfileScreen.cs'),'utf8');
const museum=fs.readFileSync(path.join(root,'Assets/Project/Scripts/Meta/AtlasBoardMuseumCollection.cs'),'utf8');
const visual=fs.readFileSync(path.join(root,'Assets/Project/Scripts/Meta/AtlasBoardMuseumTokenVisuals.cs'),'utf8');
const initial=JSON.parse(fs.readFileSync(path.join(root,'Backend/Firebase/collectibleTokenCatalog_initial.json'),'utf8'));
const extension=JSON.parse(fs.readFileSync(path.join(root,'Backend/Firebase/collectibleTokenCatalog_extension.json'),'utf8'));
const all={...initial,...extension};
const keys=Object.keys(all).sort();
const categories=keys.reduce((acc,id)=>{let theme=all[id].theme;acc[theme]=(acc[theme]||0)+1;return acc},{}) ;
const titles=keys.map(id=>all[id].title);
const checks=[
 ['paid check strokes reversed',ui.includes('shortStroke.localRotation = Quaternion.Euler(0, 0, 45)')&&ui.includes('longStroke.localRotation = Quaternion.Euler(0, 0, -42)')],
 ['confusing XP+Gold summary removed',!ui.includes('3 XP + 1 Gold reward')],
 ['Museum inventory under display',ui.includes('MuseumInventory')&&ui.includes('MuseumDisplay')],
 ['three interactive slots',ui.includes('MuseumSlot_')&&ui.includes('museumSelectedSlot = slot')],
 ['verified owned token list',museum.includes('collectibleTokens')&&museum.includes('Owned.AddRange(items)')],
 ['QA imports and earned tokens both visible',museum.includes('(string)owned["source"] != "weeklyMissions"')&&museum.includes('(string)owned["source"] != "museumQa"')&&museum.includes('(string)owned["tokenId"] != tokenId')&&museum.includes('tokenId != "weekly_" + weekId')],
 ['inventory pagination',ui.includes('museumPage * 12')],
 ['equip and remove actions',ui.includes('EquipMuseumAsync(false)')&&ui.includes('EquipMuseumAsync(true)')],
 ['account switching clears museum selection',ui.includes('museumSelectedToken = ""; museumSelectedSlot = -1;')],
 ['ETag slot write',museum.includes('if-match')&&museum.includes('"PUT", payload, old.etag')],
 ['no local grant or purchase',!museum.includes('promoXp')&&!museum.includes('wallet/gold')],
 ['dedicated 71 names (38 original plus 33 extension)',Object.keys(initial).length==38&&Object.keys(extension).length==33&&keys.length==71&&new Set(titles).size==71],
 ['20 cities 11 historic figures 20 objects 20 commemorations',categories.city===20&&categories.name===11&&categories.object===20&&categories.holiday===20],
 ['no monotonous triple category sequence',keys.every((id,i)=>i<2||!(all[keys[i-1]].theme===all[keys[i-2]].theme&&all[keys[i-1]].theme===all[id].theme))],
 ['Berlin Monument Token included',titles.includes('Berlin Monument Token')],
 ['unchanged stable token keys and Monday week IDs',keys.every(id=>/^weekly_[0-9]{8}$/.test(id)&&all[id].weekId===id.slice(7)&&all[id].kind==='museum'&&new Date(all[id].startUtcMs).toISOString().slice(0,10).replace(/-/g,'')===id.slice(7))],
 ['dated English default descriptions',keys.every(id=>all[id].description.startsWith('This token is dedicated to the week of ')&&all[id].description.includes(id.substring(7,11)))],
 ['catalog tooltip and theme read without grant',museum.includes('LocalDescriptions = LocalFields(')&&museum.includes('Theme = Clean(')],
 ['in-place chooser and actual circular coin',visual.includes('OpenChooser(false)')&&visual.includes('ChooseSlot_')&&visual.includes('ApplyToSlot(')&&visual.includes('Texture2D(size, size')&&visual.includes('CoinSprite()')],
 ['hover works on tokens and displayed coins',visual.includes('EventTriggerType.PointerEnter')&&visual.includes('EventTriggerType.PointerExit')&&visual.includes('MuseumTokenHoverCard')],
 ['display persists through existing ETag only',visual.includes('await museum.EquipAsync(slot, target)')&&!visual.includes('RequestAsync(')],
 ['TMP hover text declaration matches MakeText return type',visual.includes('private TMP_Text hoverText;')&&visual.includes('private static TMP_Text MakeText(')&&!visual.includes('private TextMeshProUGUI hoverText;')],
 ['general profile uses same saved Museum slots',ui.includes('DrawMuseumCoins(museum, 12, false)')&&visual.includes('RefreshOverviewPreview()')&&visual.includes('OwnedById(museum.Slots[i])')&&visual.includes('CollectionSlot_')],
 ['general profile refreshes owned slots without local grants',visual.includes('_ = museum.RefreshAsync()')&&visual.includes('!museum.Loaded')&&!visual.includes('users/" +')],
 ['general profile preview renders minted circular tokens',visual.includes('overviewRims[i]')&&visual.includes('rim.sprite = CoinSprite()')&&visual.includes('Monogram(token.Title)')],
 ['general profile hover shows catalog description',visual.includes('ShowOverviewHover(slot)')&&visual.includes('token.Title + "\\n" + token.Description')&&visual.includes('HideOverviewHover()')],
 ['general profile preview respects UID account switching',visual.includes('overviewPreview = null;')&&visual.includes('nextOverviewLoadAt = 0;')],
 ['exactly eleven historically identified figures',keys.filter(id=>all[id].theme==='name').length===11&&[
   'Mustafa Kemal Atatürk','Leonardo da Vinci','Marie Curie','Isaac Newton','Ada Lovelace',
   'Nikola Tesla','Alan Turing','Johannes Gutenberg','Vincent van Gogh','Albert Einstein',
   'Rosalind Franklin'].every(n=>keys.some(id=>all[id].theme==='name'&&all[id].title===n+' Token'))],
 ['Atatürk uniquely represented',keys.filter(id=>all[id].title.includes('Atatürk')).length===1],
 ['no prior invented person names',!titles.some(s=>/Atlas Rüzgâr|Yaman Kıyı|Nora Rota|Elif Işık|Selin Koru|Leya Yıldız/.test(s))],
 ['seven language catalog title and description keys',keys.every(id=>['en','tr','es','fr','de','ko','ru'].every(lang=>typeof all[id].titles[lang]==='string'&&all[id].titles[lang].length>3&&typeof all[id].descriptions[lang]==='string'&&all[id].descriptions[lang].length>50))],
 ['English default is consistent with translated catalog',keys.every(id=>all[id].title===all[id].titles.en&&all[id].description===all[id].descriptions.en)],
 ['all seven title translations unique per token', ['en','tr','es','fr','de','ko','ru'].every(lang=>new Set(keys.map(id=>all[id].titles[lang])).size===71)],
 ['full per-language week-date descriptions',keys.every(id=>['en','tr','es','fr','de','ko','ru'].every(lang=>all[id].descriptions[lang].includes(id.substring(7,11))))],
 ['existing client reads nested language data',museum.includes('LocalFields(publicEntry, "titles", "title")')&&museum.includes('LocalFields(publicEntry, "descriptions", "description")')],
 ['language change updates owned token labels without network',museum.includes('if (language != displayLanguage)')&&museum.includes('Localize(Owned[i], language)')&&museum.includes('Notify();')],
 ['unsupported locale falls back to canonical English',museum.includes('return "en";')&&museum.includes('TryGetValue("en", out string fallback)')],
 ['seven-language Museum slot action feedback',museum.includes('case "equipped"')&&museum.includes('case "removed"')&&museum.includes('case "rejected"')&&museum.includes('AtlasBoardL.R(')],
 ['seven-language Museum slot selection and cancellation',visual.includes('JETON HANGİ YUVADA')&&visual.includes('¿EN QUÉ ESPACIO')&&visual.includes('"ANNULER"')&&visual.includes('"ОТМЕНА"')],
 ['general profile title and weekly payment labels localized in place',visual.includes('RefreshProfileChromeLocale()')&&visual.includes('"ПРОФИЛЬ"')&&visual.includes('"[PAID] "')&&visual.includes('"[READY] "')],
 ['CJK and Cyrillic dynamic UI fonts use localization profile',visual.includes('manager.ResolveFont(label.font)')&&visual.includes('ApplyFont(t);')],
 ['profile paid/ready flicker fix runs in existing LateUpdate before frame render',visual.includes('private void LateUpdate()')&&visual.includes('RefreshProfileChromeLocale();')&&visual.includes('private void RefreshProfileChromeLocale()')&&visual.includes('string localizedPrefix = "[" + (received ? paid : ready) + "] ";')],
 ['weekly prefix localization is not delayed by the old 0.25s time gate',!visual.includes('if (Time.unscaledTime < nextProfileLocaleAt) return;\n        nextProfileLocaleAt = Time.unscaledTime + 0.25f;\n        GameObject profile = GameObject.Find(')&&visual.includes('if (prefix == localizedPrefix) continue;')],
 ['seven-language paid and ready labels remain available',[
   '"PAID", "ALINDI", "COBRADO", "REÇU"',
   '"ERHALTEN", "수령", "ПОЛУЧЕНО"',
   '"READY", "HAZIR", "LISTO", "PRÊT"',
   '"BEREIT", "준비", "ГОТОВО"'
 ].every(t=>visual.includes(t))],
 ['profile locale refresh reuses cached UI references without adding managers',visual.includes('private GameObject profileChromeRoot;')&&visual.includes('private readonly TMP_Text[] profileChromeMissionLabels = new TMP_Text[4];')&&visual.includes('profileChromeWeekly = profileChromeRoot.transform.Find("ProfileStage/WeeklyPreview")')&&visual.includes('profileChromeRoot = GameObject.Find("AtlasProfile")')&&!visual.includes('new AtlasBoardMuseumCollection(')],
 ['weekly receipt truth remains sourced from existing verified paid status',ui.includes('ready && v.paid[i]')&&visual.includes('if (!received && !completed) continue;')&&visual.includes('label.text = localizedPrefix + value.Substring(end + 2);')],
 ['no duplicated slot storage or token grant path',!visual.includes('collectibleTokens.json')&&museum.includes('"PUT", payload, old.etag')&&!visual.includes('AddToken')],
];
for(const [name,pass] of checks){assert(pass,name);console.log('PASS '+name)}
console.log('MUSEUM_UI_SOURCE_GATE='+checks.length+'/'+checks.length+' PASS; UNITY_COMPILER=NOT_RUN; LIVE_RULES=NOT_CHECKED_BY_SOURCE_GATE');
