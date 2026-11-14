'use strict';
// Static contract and data tests. Unity compiler, RTDB emulator and production
// gates must be verified separately before declaring production PASS.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const base = path.resolve(__dirname, '../../..');
const read = p => fs.readFileSync(path.join(base,p),'utf8');
const badge = read('Assets/Project/Scripts/UI/Menu/AtlasBoardReleaseVersionBadge.cs');
const editor = read('Assets/Project/Editor/AtlasBoardReleaseVersionEditor.cs');
const project = read('ProjectSettings/ProjectSettings.asset');
const roadmap = read('Assets/Project/Documentation/MASTER_ROADMAP.md');
const seed = JSON.parse(read('Backend/Firebase/gameversion_initial.json'));
const preparer = read('Backend/Firebase/qa/phase19ee_gameversion_rules_prepare.cjs');
let count=0;function check(v, name){assert.ok(v,name);console.log('PASS '+name);count++;}
check(seed.currentgameversion==='v0.9.1','currentgameversion exact owner value');
check(seed.lastgameversion==='v.0.9.0','lastgameversion exact owner value');
check(Object.keys(seed).length===2,'only requested gameversion keys');
check(/ReleaseVersion\s*=\s*"0\.9\.1"/.test(badge),'release version 0.9.1');
check(/HistoricalReleases[\s\S]*"0\.1\.0"[\s\S]*"0\.5\.0"[\s\S]*"0\.9\.0-beta\.1"[\s\S]*"0\.9\.1"/.test(badge),'historical build list adjacent to gate');
check(badge.includes('PreviousBetaAlias = "0.9.0-beta.1"'),'previous beta exact mapping');
check(badge.includes('PreviousReleaseFamily = "0.9.0"'),'legacy v.0.9.0 family mapping');
check(badge.includes('currentgameversion')&&badge.includes('lastgameversion'),'both Firebase values validated');
check(badge.includes('UnityWebRequest.Get(PolicyUrl)'),'reads public Spark RTDB gameversion branch');
check(badge.includes('VersionDecision.Current'),'current accepted');
check(badge.includes('VersionDecision.Previous'),'last accepted with warning');
check(badge.includes('previousNoticeDismissedFor'),'dismissible one-time previous alert');
check(badge.includes('VersionDecision.Unsupported'),'old versions blocked');
check(badge.includes('VersionDecision.Offline'),'unavailable policy distinct from version mismatch');
check(badge.includes('AtlasBoardLocalizationManager.LanguageChanged'),'localized popup language refresh');
check(['tr','es','fr','de','ko','ru'].every(x=>badge.includes('case "'+x+'"')),'localized UI for 7 languages including EN');
check(badge.includes('VersionDecision.Checking')&&badge.includes('ShowPolicyNotice(VersionDecision.Checking)'),'fail closed until startup check');
check(badge.includes('private const float CheckIntervalSeconds = 60f'),'manual Firebase changes observed while app running');
check(badge.includes('UnityWebRequest.Result.Success'),'HTTP result validated');
check(badge.includes('ContentVersion = "1"')&&badge.includes('AtlasOnlineDefaults.ProtocolVersion'),'prior room protocol contract preserved');
check(!badge.includes('CloudFunctions'),'no paid Cloud Functions');
check(/bundleVersion:\s*0\.9\.1/.test(project),'Unity PlayerSettings version 0.9.1');
check(/AndroidBundleVersionCode:\s*10/.test(project),'Android versionCode 10');
check(editor.includes('NewVersion = "0.9.1"')&&editor.includes('bundleVersionCode < 10'),'Editor release alignment without forced future override');
check(preparer.includes('AtlasBoard_RtdbRuleBackups')&&preparer.includes('LIVE_DEPLOY=NO'),'Rules full-file backup and no auto deploy');
check(preparer.includes('262144')&&preparer.includes('Unrelated rules changed'),'Rules size check and unrelated-branch audit');
check(roadmap.startsWith('# AtlasBoard v5.49.12eg'),'roadmap canonically updated to AFK release');
check(roadmap.includes('PENDING — MAIN-MENU IDLE DISCONNECT'),'five-minute idle screen explicitly open');
check(roadmap.includes('SERVER-SIDE ADMISSION HARDENING — OPEN'),'old already-distributed binaries not falsely secure');
check(fs.existsSync(path.join(base,'Backend/Firebase/qa/phase19ee_gameversion_auth_rtdb_emulator.test.cjs')),'Auth+RTDB emulator test included');
// Contract data examples. C# compiler and live networking remain separate gates.
function normalize(v) {
  v = String(v).trim().replace(/^v\.?/i,'');
  return v==='0.9.0-beta.1'?'0.9.0':v;
}
function verdict(local,config) {
  const known=['0.1.0','0.5.0','0.9.0-beta.1','0.9.0','0.9.1'];
  if (!config || !config.currentgameversion || !config.lastgameversion) return 'offline';
  if (!known.includes(local)) return 'blocked';
  if (normalize(local)===normalize(config.currentgameversion)) return 'current';
  if (normalize(local)===normalize(config.lastgameversion)) return 'previous';
  return 'blocked';
}
check(verdict('0.9.1',seed)==='current','current release admitted');
check(verdict('0.9.0-beta.1',seed)==='previous','0.9.0 beta legacy alias warned but admitted');
check(verdict('0.5.0',seed)==='blocked','two+ releases behind blocked');
check(verdict('0.9.1',{currentgameversion:'v0.9.2',lastgameversion:'v0.9.1'})==='previous','manual Firebase update makes installed build previous');
check(verdict('0.9.1',{currentgameversion:'v0.9.3',lastgameversion:'v0.9.2'})==='blocked','manual Firebase newer pair blocks installed build');
check(verdict('0.9.1',null)==='offline','missing policy has distinct offline status');

// v5.49.12ee regression tests: source and logical decisions. Actual Unity
// screen layout / input interception and RTDB remain separately tested.
check(verdict('0.9.1',{currentgameversion:'v0.9.2',lastgameversion:'v.0.9.0'})==='blocked',
    'owner screenshot: installed 0.9.1 is NOT in the two permitted versions');
check(verdict('0.9.1',{currentgameversion:'v0.9.2',lastgameversion:'v0.9.1'})==='previous',
    '0.9.1 permitted as previous with dismissible update advice');
check(verdict('0.9.1',{currentgameversion:'v0.9.1',lastgameversion:'v.0.9.0'})==='current',
    'current 0.9.1 permitted without popup');
check(badge.includes('versionOverlay = new GameObject("AtlasBoardVersionNotice"'),
    'one existing-version component creates the dialog');
check(!badge.includes('versionOverlay.transform.SetParent(transform'),
    'full-screen overlay is NOT parented inside a scaled menu');
check(badge.includes('Canvas c = versionOverlay.GetComponent<Canvas>();') &&
      badge.includes('c.renderMode = RenderMode.ScreenSpaceOverlay') &&
      badge.includes('c.overrideSorting = true'),
    'standalone high-priority Screen Space Overlay canvas');
check(badge.includes('canvasRect.anchorMin = Vector2.zero') &&
      badge.includes('canvasRect.anchorMax = Vector2.one'),
    'full-screen canvas anchor range, not zero-size inner RectTransform');
check(badge.includes('"FullScreenInputBlocker"') && badge.includes('dim.raycastTarget = true'),
    'dark overlay captures underlying mouse/touch events');
check(badge.includes('SetMenuInputsSuspended(!IsInteractivelyAllowed())') &&
      badge.includes('menuInputGroup.interactable = false') &&
      badge.includes('menuInputGroup.blocksRaycasts = false'),
    'existing main menu inputs also disabled until version clearance');
check(badge.includes('RestoreMenuInputs()') &&
      badge.includes('previousMenuInteractable = menuInputGroup.interactable'),
    'original menu input state restored after clearance');
check(badge.includes('VersionDecision.Unsupported') &&
      badge.includes('noticeClose.gameObject.SetActive(show == VersionDecision.Previous)'),
    'unsupported screen has no dismissible X button');
check(badge.includes('case "quit":') && badge.includes('QuitFromVersionDialog()') &&
      badge.includes('Application.Quit()'),
    'unsupported and offline screens offer explicit Exit Game button');
check(badge.includes('case "later":') && badge.includes('DismissPreviousNotice()'),
    'previous build can dismiss the warning and continue playing');
check(badge.includes('noticeBody.fontSizeMin = 18') &&
      badge.includes('TextWrappingModes.Normal'),
    'long localized descriptions have a readable wrapped text region');
check(badge.includes('case "tr": return "İzin verilen önceki sürüm: "') &&
      badge.includes('policy.lastgameversion'),
    'blocking popup describes both permitted versions');
check(badge.includes('OnLobbySnapshotChanged(bridge.CurrentSnapshot)') &&
      badge.includes('ExitIncompatibleRoomAsync'),
    'newly detected mismatch exits an active incompatible lobby');
check(roadmap.startsWith('# AtlasBoard v5.49.12eg'),
    'canonical roadmap updated for full-screen version admission UX');
check(roadmap.includes('MAIN-MENU IDLE DISCONNECT') &&
      roadmap.includes('SERVER-SIDE ADMISSION HARDENING'),
    'idle and true server-side legacy binary admission remain OPEN');



// v5.49.12ef: AFK implementation contracts. Original 54 version checks remain.
check(badge.includes('IdleAfterSeconds = 300f'),'main-menu idle threshold exactly five minutes');
check(badge.includes('IdleWallpaperChangeSeconds = 180f'),'wallpaper changes every three minutes');
check(badge.includes('IdleWallpapersPerLanguage = 4') &&
      badge.includes('IdleTurkishWallpaperCount = 6'),
      'exact localized wallpaper counts: 4 per language, 6 Turkish');
check(badge.includes('private void UpdateMenuIdle()'),'existing UI component owns AFK update');
check(badge.includes('IsEligibleForMenuIdle()') &&
      badge.includes('bridge != null && bridge.HasLobby') &&
      badge.includes('IsMainMenuTransitionSettled'),'active lobby and unsettled transitions protect matches');
check(badge.includes('AtlasBoardEscapeBlocker') && badge.includes('ModalPanel'),
      'modal and tablet screens are protected from idle activation');
check(badge.includes('WasIdleInputPressed()') &&
      badge.includes('Touchscreen.current') && badge.includes('Gamepad.current'),
      'keyboard mouse touch and gamepad activity supported');
check(badge.includes('lastUserActionAt = Time.unscaledTime') &&
      badge.includes('Time.unscaledTime - lastUserActionAt < IdleAfterSeconds'),
      'AFK timer is based on deliberate action only');
check(badge.includes('if (!IsInPureMainMenu())') && badge.includes('if (!IsEligibleForMenuIdle()) return;'),
      '60-second policy refresh does not restart 300-second idle timer');
check(badge.includes('nextCheckAt = float.PositiveInfinity'),
      'existing version RTDB poll pauses during idle');
check(badge.includes('WakeFromMenuIdleAsync') &&
      badge.includes('await RefreshPolicyAsync()') &&
      badge.includes('ShowPolicyNotice(VersionDecision.Checking)'),
      'waking revalidates server version in existing blocker');
check(badge.includes('IdleExitActualButton') &&
      badge.includes('quit.onClick.AddListener(QuitFromVersionDialog)') &&
      badge.includes('IsIdleExitPointerPressed()'),
      'real localized exit button not a fake bitmap hot spot');
check(badge.includes('LoadWallpaperResource(filename, out nextTexture)') &&
      badge.includes('Resources.UnloadAsset(previous)'),
      'load one wallpaper on demand and unload previous texture');
check(!badge.includes('FirebaseDatabase.GoOffline(') &&
      !badge.includes('SignInAnonymouslyAsync('),
      'no global RTDB side-effects or duplicate Guest UID');
check(badge.includes('canvas.sortingOrder = 31900') &&
      badge.includes('c.sortingOrder = 32000'),
      'version blocker retains priority over AFK wallpaper');
// v5.49.12eg: file names match the owner's actual Resources/AtlasBoard/AFK
// screenshots. No fake images or stale Events/AFK folder should be shipped.
check(badge.includes('IdleResourceBase = "AtlasBoard/AFK/AtlasBoard_AFK_"'),
  'correct owner-supplied Unity Resources folder');
check(!badge.includes('AtlasBoard/Events/AFK/AtlasBoard_AFK_'),
  'old /Events/AFK image path completely removed');
check(badge.includes('"_" + requestedLanguage') &&
      ['TR','EN','ES','FR','DE','KO','RU'].every(code =>
        badge.includes('return "'+code+'"')),
  'current language selects exact _TR/_EN/_ES/_FR/_DE/_KO/_RU file');
check(badge.includes('idleWallpaperIndex = -1;') &&
      badge.includes('IdleWallpaperCount(requestedLanguage)'),
  'correct 01..04 or 01..06 cycle per locale');
check(badge.includes('Resources.Load<Texture2D>(resourcePath)') &&
      badge.includes('Resources.Load<Sprite>(resourcePath)'),
  'both Default and Sprite Unity PNG import types supported');
check(badge.includes('idleWallpaper.uvRect = new Rect(0f, 0f, 1f, 1f)') &&
      !badge.includes('0.155f'),
  'original owner wallpaper dimensions visible without old crop');
check(badge.includes('idleMissingArtworkLogged') &&
      badge.includes('fallback=EN'),
  'missing translations explicitly logged; fail gracefully with EN');
const afkEditor = read('Assets/Project/Editor/AtlasBoardMenuAfkQa.cs');
check(afkEditor.includes('Atlas Board/Design/UI & UX/UX/AFK Test - Simulate 5 Minutes'),
  'QA command inside existing Design > UI & UX > UX group');
check(afkEditor.includes('EditorApplication.isPlaying') &&
      afkEditor.includes('EditorSimulateFiveMinutesOfMenuIdle()'),
  'Editor QA only works in Play Mode and reuses runtime guard');
check(badge.includes('#if UNITY_EDITOR') &&
      badge.includes('EditorSimulateFiveMinutesOfMenuIdle()') &&
      badge.includes('!IsEligibleForMenuIdle()'),
  '5 minute test does not bypass lobby/version protection');
check(roadmap.includes('v5.49.12eg') &&
      roadmap.includes('6 + 6*4 = 30'),
  'new canonical roadmap records exact 30 files and local QA');

// Image bytes are user-supplied and were not part of this code-only ZIP.
// Ordinary QA on the owner's machine requires 30 genuine PNGs in the right
// folder. The opt-in env flag is ONLY for source-only CI lacking their art.
const locales = ['TR','EN','ES','FR','DE','KO','RU'];
const imageRoot = path.join(base,'Assets/Project/Resources/AtlasBoard/AFK');
const expectedArt = locales.flatMap(locale => Array.from(
  {length:locale==='TR'?6:4},(_,i) =>
  path.join(imageRoot,`AtlasBoard_AFK_${String(i+1).padStart(2,'0')}_${locale}.png`)));
check(expectedArt.length===30,'30 distinct expected localized wallpaper paths');
if(process.env.ATB_SOURCE_ONLY_NO_ART === '1') {
  console.log('AFK_IMAGE_BYTES=NOT_TESTED (source-only environment; player assets required on owner machine)');
} else {
  const missing = expectedArt.filter(f => !fs.existsSync(f));
  assert.equal(missing.length,0, 'missing user wallpaper(s):\n'+missing.join('\n'));
  const signature=Buffer.from([137,80,78,71,13,10,26,10]);
  expectedArt.forEach(f=> {
    const data=fs.readFileSync(f);
    assert.ok(data.length>24&&data.subarray(0,8).equals(signature),
      'invalid PNG signature: '+f);
    const w=data.readUInt32BE(16),h=data.readUInt32BE(20);
    assert.ok(w>=640&&h>=360&&w/h>1.55&&w/h<2.05,
      'invalid horizontal wallpaper resolution/aspect: '+f);
  });
  console.log('AFK_ASSETS=30/30 PNG_SIGNATURE_AND_DIMENSIONS_PASS');
}
check(roadmap.includes('SOURCE UPDATED') &&
      roadmap.includes('UNITY COMPILER AND VISUAL QA OPEN'),
      'canonical roadmap clearly separates source updates from runtime QA');

console.log('GAMEVERSION_AFK_SOURCE_GATE='+count+'/'+count+' PASS; UNITY_COMPILER=NOT_RUN; FIREBASE_DEPLOY=NOT_REQUIRED');
