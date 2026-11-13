'use strict';
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const repo=path.resolve(__dirname,'../../..');
const get=p=>fs.readFileSync(path.join(repo,p),'utf8');
const client=get('Assets/Project/Scripts/Meta/AtlasBoardPassportFeature.cs');
const museum=get('Assets/Project/Scripts/Meta/AtlasBoardMuseumCollection.cs');
const road=get('Assets/Project/Documentation/MASTER_ROADMAP.md');
const catalog=require('../passportStampCatalog_initial.json');
const {passport,datedPassport,weeklyPassport,catalogueRule,compact,build}=require('./phase19dx_passport_rules_prepare.cjs');
let pass=0;
function ok(c,name){assert.ok(c,name);console.log('PASS '+name);pass++;}
const entries=Object.entries(catalog),languages=['en','tr','es','fr','de','ko','ru'];
ok(entries.length===21,'21 immutable stamp IDs');
ok(new Set(entries.map(([id])=>id)).size===21,'unique stamp IDs');
ok(entries.every(([id,s])=>/^[a-z0-9_]{3,40}$/.test(id)&&s.title&&s.titles&&s.descriptions),
    'non-empty text-only catalogue');
ok(entries.every(([,s])=>languages.every(lang=>s.titles[lang]&&s.descriptions[lang])),
    '21 stamps in seven supported languages');
ok(entries.every(([,s])=>s.map==='*'||['Turkey','Colorado','USA','Germany','Mexico','World'].includes(s.map)),
    'only existing map IDs');
ok(entries.every(([,s])=>['rounds','turns','properties','developments','diceRolls','doublesRolled','netWorth','cash'].includes(s.metric)&&Number.isInteger(s.min)&&s.min>0&&typeof s.win==='boolean'),
    'only existing verified career claim metrics');
ok(entries.every(([,s])=>Object.keys(s).sort().join(',')==='descriptions,map,metric,min,title,titles,win'),
    'no art, sprite, portrait, or image bytes in Firebase catalogue');
ok(client.includes('private const int TotalPages = 18;')&&client.includes('private const int StampCount = 21;'),
    '18 pages and 21 stamps');
ok(client.includes('new[] { "", "" }')&&client.includes('PassportStampSlot_'),
    'exactly two stamp placements per numbered page');
ok(client.includes('case "tr"')&&client.includes('case "es"')&&client.includes('case "fr"')&&client.includes('case "de"')&&client.includes('case "ko"')&&client.includes('case "ru"'),
    'seven-language text selection');
ok(client.includes('GameObject.Find("AtlasBoard_FullScreen_Profile_Route")')&&client.includes('PassportCover'),
    'existing Profile passport route reused');
ok(museum.includes('gameObject.AddComponent<AtlasBoardPassportFeature>()')&&
    museum.includes('gameObject.AddComponent<AtlasBoardMuseumTokenVisuals>()'),
    'single existing Museum/Profile runtime and no duplicate Bootstrap');
ok(!client.includes('RuntimeInitializeOnLoadMethod')&&!client.includes('FirebaseDatabase.DefaultInstance'),
    'no duplicate runtime manager or secondary Firebase wallet');
ok(client.includes('careerClaims/')&&client.includes('proofIds'),
    'Career claims read; local progress is not currency');
ok(client.includes('GetCurrentCallableTokenAsync')&&client.includes('AtlasBoardAppCheckRequestSecurity.ApplyAsync'),
    'existing auth token and App Check request handling');
ok(client.includes('ifMatch:"null_etag"')&&client.includes('result.code == 412')&&
    client.includes('placementHistory/'),
    'immutable ETag weekly placement receipt and conflict response');
ok(client.includes('!owned.Contains(stamp) || PlacedAnywhere(stamp)')&&
    client.includes('StampActionUsedThisWeek(stamp, remove)'),
    'no local stamp duplication, no unowned placement and stamp-scoped weekly limit');
ok(client.includes('Quaternion.Euler(0,0,(code%13)-6f)')&&client.includes('StableHash('),
    'stable slightly imperfect imprint rotation');
ok(!client.includes('Texture2D.LoadImage') && client.includes('Sprite.Create'),
    'art is generated on device, not downloaded from Firebase');
ok(client.includes('page = Mathf.Clamp(page + amount, 0, TotalPages - 1)')&&
    client.includes('portrait = !portrait'),
    'navigation and portrait/landscape controls');
ok(client.includes('displayName')&&client.includes('playerId')&&client.includes('NOT A TRAVEL DOCUMENT'),
    'fictional in-game identity only, not real document replica');
ok(JSON.stringify(passport).includes('careerClaims')&&JSON.stringify(passport).includes('passportStampCatalog'),
    'ownership checks signed claim and immutable catalogue on server');
ok(!passport.pages&&JSON.stringify(passport.placementHistory).includes('atlasPassport/owned')&&
    JSON.stringify(passport.placementHistory).includes('placementHistory')&&
    Boolean(passport.placementHistory.$week.$stampId.$action)&&
    JSON.stringify(passport.placementHistory).includes('newData.child(\'s\')'),
    'legacy pages frozen; per-stamp weekly receipts validate ownership, slot and page');
ok(JSON.stringify(passport).includes('proofId')&&JSON.stringify(passport).includes('!data.exists()'),
    'immutable claimed receipt bound to verified Career proof');
ok(JSON.stringify(passport).includes('page')&&JSON.stringify(passport).includes('orientation')&&
    JSON.stringify(catalogueRule)==='{".read":true}',
    'authenticated private preferences and operator-only catalogue');
const simulated={rules:{users:{$uid:{'.read':'auth!=null&&auth.uid==$uid','.write':false,
  collectibleDisplay:{s0:{}},metaEconomy:{},weeklyMissions:{}}},
  careerClaims:{},collectibleTokenCatalog:{}}};
const generated=build(JSON.stringify(simulated));
ok(Boolean(generated.doc.rules.users.$uid.atlasPassport),'full rules builder creates private Passport rules');
ok(build(JSON.stringify(generated.doc)).already,'rules writer is idempotent, no duplicate branches');
const {legacyPassport}=require('./phase19dx_passport_rules_prepare.cjs');
const legacyDoc=JSON.parse(JSON.stringify(generated.doc));
legacyDoc.rules.users.$uid.atlasPassport=legacyPassport;
const upgraded=build(JSON.stringify(legacyDoc));
ok(Boolean(upgraded.doc.rules.users.$uid.atlasPassport.placementHistory)&&
    !upgraded.doc.rules.users.$uid.atlasPassport.pages,
    'existing deployed Passport rules upgrade without extra branches');
const weeklyDoc=JSON.parse(JSON.stringify(generated.doc));
weeklyDoc.rules.users.$uid.atlasPassport=weeklyPassport;
const dated=build(JSON.stringify(weeklyDoc));
ok(Boolean(dated.doc.rules.users.$uid.atlasPassport.placementHistory.$week.$stampId.$action.t)&&
   build(JSON.stringify(dated.doc)).already,
    'dated weekly rules upgrade safely to per-stamp rules and remain idempotent');
ok(road.includes('Atlas Passport')&&road.includes('v5.49.12eb'),
    'canonical roadmap updated in place');
ok(client.includes('private bool BookPortrait => page > 0 && portrait;'),
    'identity page always landscape independent of saved stamp orientation');
ok(client.includes('if (busy || page == 0) return;')&&
    client.includes('rotateButton.interactable=page>0&&!busy;'),
    'first page rotation disabled in action and UI');
ok(client.includes('if (portrait && (previousPage == 0 || page == 0)) RebuildWhenVisible();')&&
    client.includes('previousBookPortrait != BookPortrait'),
    'page transitions and remote view changes rebuild effective orientation');
ok(client.includes('new Vector2(bookX,-24)')&&
    client.includes('new Vector2(invX,-52)')&&client.includes('var bh=565f;'),
    'both Passport panels moved below existing Profile subtitle');
ok(client.includes('ProfileUidField.GetValue(profile) as string, uid')&&
    client.includes('AtlasBoardRtdbMetaEconomy.CurrentUid ?? "", uid'),
    'Passport identity read strictly matches Profile and authenticated UID');
ok(client.includes('ProfileNameField')&&client.includes('ProfilePlayerIdField')&&
    client.includes('SyncCurrentProfileIdentity()'),
    'canonical Profile display name and Player ID are reused without new manager');
ok(client.includes('StringInfo.GetNextTextElement(displayName.Trim()).ToUpperInvariant()')&&
    !client.includes('displayName?"A"'),
    'identity avatar initial follows player name and supports Unicode graphemes');
ok(client.includes('if (string.IsNullOrWhiteSpace(displayName))')&&
    client.includes('if (string.IsNullOrWhiteSpace(playerId))'),
    'RTDB fallback cannot overwrite populated canonical Profile identity');

ok(client.includes('OpenDialog(Local("STAMP EARNED"')&&
   client.includes('if (result.code == 200) { owned.Add(selectedId);'),
   'successful claim opens localized confirmation');
ok(client.includes('EventTriggerType.PointerEnter')&&
   client.includes('Time.unscaledTime - longPressStarted >= 2f')&&
   client.includes('EventTriggerType.PointerUp'),
   'desktop hover and mobile two-second hold tooltip');
ok(client.includes('bestProgress')&&client.includes('tooltipFill.sizeDelta')&&
   client.includes('Math.Min(progress, stamp.min)'),
   'verified career proof progress bar shown by stamp');
ok(client.includes('ConfirmSlotAction(false)')&&client.includes('ConfirmSlotAction(true)')&&
   client.includes('Each stamp can be pressed once per UTC week.')&&
   client.includes('Her damgayı bir UTC haftasında bir kez basabilirsin.'),
   'confirmation explains weekly quota applies to EACH stamp independently');
ok(client.includes('placementHistory.Add(')&&client.includes('foreach (Placement placement in placementHistory')&&
   client.includes('ReadPlacement(week, weekNode, null)')&&
   client.includes('startedRevision != mutationRevision'),
   'legacy page replay, per-stamp actions and stale Firebase response suppression');
ok(client.includes('RenderStampImprint(btn.transform')&&client.includes('SealArt(index)')&&
   client.includes('InteriorLegend(fullTitle,narrow)')&&client.includes('RegionCode('),
   'all 21 individually themed procedurally rendered stamps');
ok(client.includes('new Vector2(-bw*.32f,30)')&&
   client.includes('float lineX=bookPortrait?70:118;'),
   'identity avatar separated farther from player name');
ok(client.includes('PassportStampTooltip')&&client.includes('PassportConfirmationOverlay')&&
   !client.includes('selectedLabel=Label(inventory'),
   'nonmodal floating details and clear inventory action footer');
ok(JSON.stringify(passport.placementHistory).includes('345600000')&&
   JSON.stringify(passport.placementHistory).includes('604800000')&&
   Boolean(passport.placementHistory.$week.$stampId.$action)&&
   !JSON.stringify(passport).includes('"pages"'),
   'per-stamp UTC weekly permissions with immutable legacy pages');
ok(client.includes('RegisterStampTooltip(btn, stampedIndex, true)')&&
   client.includes('RegisterStampTooltip(listButtons[i], n)'),
   'floating stamp progress details work on inventory and imprints');
ok(client.includes('nextRefresh = Time.unscaledTime + 10f')&&
   client.includes('if (!loaded) Draw();'),
   'failed network refresh retains visible stamps and avoids request loops');
ok(entries.every(([id],i)=>client.includes('case "'+id+'": return '+i+';')),
   'all 21 stable stamp IDs map to distinct in-game art themes');
ok(client.includes('enum SealKind { Round, Rectangle, Oval, Hexagon, Diamond, Ticket }')&&
   client.includes('private static Sprite SealArt(int index)')&&
   client.includes('sealSprites[index]=sprite'),
   'six ink silhouette families and cached on-device sprites');
ok(client.includes('new Vector2(0,top)')&&client.includes('new Vector2(0,middle)')&&
   client.includes('new Vector2(0,bottom)')&&client.includes('StampImprintDate'),
   'interior legends separated from outlines and stamped date printed in slot');
ok(client.includes('StringInfo.GetNextTextElement')&&
   client.includes('Local("EARLIER ISSUE", "ESKİ BASIM"')&&
   client.includes('if (latest.atMs <= 0) return Local("EARLIER ISSUE"'),
   'legacy stamps never display invented exact dates; weekly dates retained');
ok(client.includes('["t"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()')&&
   JSON.stringify(passport.placementHistory).includes("newData.child('t').val()>=now-300000")&&
   JSON.stringify(passport.placementHistory).includes("newData.child('t').val()<=now+60000"),
   'new printed-on date stored only with server-verified UTC time');
ok(client.includes('WeeklyActionHint(stamp)')&&client.includes('NextStampDate()')&&
   client.includes('if(removeButton!=null)removeButton.interactable=selectedSpace'),
   'ownership and weekly placement limit have actionable next-date explanation');
ok(client.includes('Doğrulanmış tek bir online maçta')&&
   client.includes('Ayrıca o maçı kazanmalısın.')&&
   !client.includes('LocalMetric(s.metric)+" >= "'),
   'natural language achievements replace inequality-formulas');
ok(!JSON.stringify(passport).includes('"pages"')&&
   !JSON.stringify(passport).includes('"bonusGold"')&&
   JSON.stringify(passport.placementHistory.$week.$stampId.$action).includes("newData.child('s').val()<=1"),
   'legacy pages remain immutable and weekly receipts cannot award currency');
ok(client.includes('week.ToString(CultureInfo.InvariantCulture) + "/" + Esc(stamp)')&&
   client.includes('(remove ? "/r" : "/p")'),
   'per-stamp placement and removal use independent server keys');
ok(client.includes('foreach (var stampEntry in weekNode.Properties())')&&
   client.includes('ReadPlacement(week, weekNode, null)')&&
   client.includes('ReadPlacement(week, removed, stampEntry.Name)')&&
   client.includes('ReadPlacement(week, pressed, stampEntry.Name)'),
   'old global receipts and new per-stamp records replay together');
ok(!client.includes('ActionUsedThisWeek()')&&
   client.includes('StampActionUsedThisWeek(selected.id, false)')&&
   client.includes('This stamp was already pressed this week'),
   'other earned stamps never inherit another stamp weekly lock');
ok(JSON.stringify(passport.placementHistory).includes("$stampId+'/p'")&&
   JSON.stringify(passport.placementHistory).includes(".child('i').val()!=$stampId")&&
   JSON.stringify(passport.placementHistory).includes("$action=='p'"),
   'server prevents same-stamp weekly replay and honors legacy current-week receipts');
ok(client.includes('"BASIM TARİHİ"')&&
   client.includes('if (latest.atMs <= 0)'),
   'actual stamp date used; no fabricated exact date for legacy receipts');
console.log('PASSPORT_21_SOURCE_GATE='+pass+'/'+pass+' PASS; UNITY_COMPILER=NOT_RUN; RTDB_EMULATOR=NOT_RUN');
