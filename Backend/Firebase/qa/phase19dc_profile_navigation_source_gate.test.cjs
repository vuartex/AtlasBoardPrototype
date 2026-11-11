'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../../..');
function read(rel) { return fs.readFileSync(path.join(root, rel), 'utf8'); }
const profile = read('Assets/Project/Scripts/UI/Menu/AtlasBoardUnifiedProfileScreen.cs');
const loginSkin = read('Assets/Project/Scripts/UI/Menu/AtlasBoardLoginVisualRefresh.cs');
const theme = read('Assets/Project/Scripts/Platform/AtlasBoardUniversalCareerTheme.cs');
const career = read('Assets/Project/Scripts/UI/Progression/AtlasBoardProgressionUI.cs');
const settings = read('Assets/Project/Settings/AtlasBoardSettingsV2Controller.cs');
const roadmap = read('Assets/Project/Documentation/MASTER_ROADMAP.md');
let passed = 0;
function test(label, condition) { assert.ok(condition, label); passed++; console.log('PASS '+label); }
test('One full-screen profile UI component', (profile.match(/class AtlasBoardUnifiedProfileScreen/g)||[]).length === 1);
test('Runtime bootstrap without menu/editor rebuild', profile.includes('RuntimeInitializeOnLoadMethod') && profile.includes('AfterSceneLoad'));
test('Career ACCOUNT tab is targeted', profile.includes('Tab_CareerAccountAtlasBoard'));
test('Settings ACCOUNT tab is targeted', profile.includes('Tab_AccountAtlasBoard'));
test('Each tab action is replaced rather than invoking old modal', (profile.match(/new Button\.ButtonClickedEvent\(\)/g)||[]).length === 2 && !profile.includes('ShowSteamAccountNotice()'));
test('Store auth is left to original account controller; old popup never routed from Profile actions', !profile.includes('existing.onClick.Invoke()') && !profile.includes('existing.onClick = new Button.ButtonClickedEvent()') && profile.includes('OpenRenameAccountManager()'));
test('Existing menu tabs are source-owned by UniversalCareerTheme', theme.includes('Tab_CareerAccountAtlasBoard') && theme.includes('Tab_AccountAtlasBoard'));
test('Steam popup source is explicitly identified for routing regression', theme.includes('ShowSteamAccountNotice()'));
test('Career and Settings originals are preserved', career.includes('private AtlasBoardProgressionSnapshot snapshot') && settings.includes('private GameObject settingsRoot'));
test('Profile is opaque full-screen, not a modal backdrop', profile.includes('Canvas_AtlasProfileFullScreen') && profile.includes('screen.anchorMin = Vector2.zero') && !profile.includes('modalBackdrop'));
test('Career and Settings original navigation restored on close', profile.includes('RestoreOriginNextFrame') && profile.includes('previousParent.SetActive(true)'));
test('Escape key is consumed by full screen and delayed restore', profile.includes('AtlasBoardEscapeBlocker') && profile.includes('yield return null'));
test('Four profile navigation routes', profile.includes('"general", "weekly", "museum", "passport"'));
test('General Overview has clickable preview links', profile.includes('OpenWeekly') && profile.includes('OpenMuseum') && profile.includes('OpenPassport'));
test('Monogram initial uses grapheme text element', profile.includes('StringInfo.GetNextTextElement'));
test('PlayerID uses only existing RTDB users current uid playerId', profile.includes('/users/') && profile.includes('ReadOwnRtdbStringAsync(uid, token, "playerId")') && !profile.includes('playerId = Guid'));
test('Account-switch stale results rejected', profile.includes('dataGeneration') && profile.includes('IsCurrent(generation, uid)'));
test('Career XP/level reads canonical ProgressionSnapshot', profile.includes('AtlasBoardProgressionSnapshot') && profile.includes('GetProfileAsync()') && profile.includes('IsSnapshotForUser'));
test('Career XP progress formula matches existing overview', profile.includes('currentSnapshot.TotalXp - currentSnapshot.LevelStartXp') && profile.includes('currentSnapshot.NextLevelXp - currentSnapshot.LevelStartXp') && career.includes('snapshot.TotalXp - snapshot.LevelStartXp'));
test('Steam account verified link required', profile.includes('status.Success && status.Linked') && profile.includes('status.Verified') && profile.includes('status.AccountId, currentUid'));
test('Account name and email are read-only presentations', profile.includes('lockedName') && profile.includes('lockedEmail') && profile.includes('MaskEmail'));
test('Four mission slots and three token slots shown without grants', profile.includes('i < 4') && profile.includes('i < 3') && profile.includes('server ownership verification'));
test('Passport is an entry tab without fabricated stamps', profile.includes('RenderPassport()') && profile.includes('not enabled yet'));
test('No client database write or economy award logic', !/UnityWebRequest\.(Put|Post|Delete)/.test(profile) && !/ServerValue\.increment|wallet\/gold|SetRawJsonValueAsync/.test(profile));
test('Approved design and all old roadmap history retained', roadmap.startsWith('# AtlasBoard v5.49.12dg') && roadmap.includes('# AtlasBoard v5.49.12dc') && roadmap.includes('# AtlasBoard v5.49.12db') && roadmap.includes('v5.49.12ct'));

// Phase19DD screenshot regressions: all positions are in the 1920x1080
// CanvasScaler coordinate system; compare actual source-defined rectangles.
function geometry(name) {
  const offset=profile.indexOf('"'+name+'"');
  assert.ok(offset>=0,'Missing source element '+name);
  const match=profile.slice(offset,offset+240).match(/new Vector2\((-?\d+),\s*(-?\d+)\),\s*new Vector2\((\d+),\s*(\d+)\)/);
  assert.ok(match, 'Missing geometry for '+name);
  return {x:+match[1], y:+match[2], w:+match[3], h:+match[4]};
}
const stageRect=geometry('ProfileStage');
const identityRect=geometry('IdentityCard');
const accountRect=geometry('ReadOnlyIdentity');
const steamRect=geometry('SteamLine');
const weeklyRect=geometry('WeeklyPreview');
const museumRect=geometry('MuseumPreview');
const passportRect=geometry('PassportPreview');
const subtitleRect=geometry('RouteSubtitle');
const stageBottom=stageRect.y-stageRect.h/2;
const top=(b)=>stageRect.y+b.y+b.h/2;
const bottom=(b)=>stageRect.y+b.y-b.h/2;
test('Profile heading subtitle clears both top cards by >= 16 canvas units',
  subtitleRect.y-subtitleRect.h/2-top(identityRect)>=16 &&
  subtitleRect.y-subtitleRect.h/2-top(accountRect)>=16);
test('Steam status strip clears account cards without overlap',
  bottom(identityRect)-top(steamRect)>=10 && bottom(accountRect)-top(steamRect)>=10);
test('All three overview previews clear Steam strip',
  [weeklyRect,museumRect,passportRect].every(b => bottom(steamRect)-top(b)>=10));
test('All three preview cards stay inside content stage and align vertically',
  [weeklyRect,museumRect,passportRect].every(b=>bottom(b)>=stageBottom+8 && Math.abs(b.y-weeklyRect.y)<1));
test('All overview cards stay horizontally inside stage',
  [identityRect,accountRect,weeklyRect,museumRect,passportRect].every(b=>
    b.x-b.w/2>=-stageRect.w/2+6 && b.x+b.w/2<=stageRect.w/2-6));
test('Four side menu items have five reusable font-independent sprite types',
  profile.includes('new Sprite[5]') && profile.includes('GlyphSprite(int kind)') &&
  profile.includes('AddGlyph(tab.transform, "TabIcon", i') &&
  profile.includes('texture.Apply(false, true)'));
test('Sidebar labels use separate icon and text placement',
  profile.includes('label.alignment = TextAlignmentOptions.MidlineLeft;') &&
  profile.includes('label.rectTransform.anchoredPosition = new Vector2(15, 0)'));
test('Name and email use independent graphical lock icons',
  profile.includes('AddGlyph(field, "LockIcon", 4') &&
  profile.includes('new Vector2(359, 31), 17, Muted') &&
  (profile.match(/ReadOnlyRow\(account,/g)||[]).length===2);
test('No emoji or missing Unicode navigation glyph remains in Profile source',
  !/[🔒◈▣▤◉]/u.test(profile));
test('Both Passport covers use native graphical emblem',
  (profile.match(/AddGlyph\(book, "PassportMark", 3/g)||[]).length===2);
test('XP/Level calculation and account ID fetch were not replaced by visual defaults',
  profile.includes('currentSnapshot.TotalXp - currentSnapshot.LevelStartXp') &&
  profile.includes('ReadOwnRtdbStringAsync(uid, token, "playerId")') && profile.includes('status.Success && status.Linked'));
test('Profile action and frame QA checkpoint retains prior canonical roadmap',
  roadmap.startsWith('# AtlasBoard v5.49.12dg') && roadmap.includes('# AtlasBoard v5.49.12dc') &&
  roadmap.includes('NEWS=OWNER_PASS_CLOSED') && roadmap.includes('WORLD_JOURNEY'));


// Phase19DE owner screenshot: top title, X, left sidebar and right stage must
// all fit the SAME opaque body frame, not just pass inter-card spacing.
const bodyRect=geometry('Body');
const borderRect=geometry('AccentBorder');
const titleRect=geometry('Title');
const closeRect=geometry('CloseProfile');
const navRect=geometry('LeftMenu');
const topRect=(r)=>r.y+r.h/2;
const bottomRect=(r)=>r.y-r.h/2;
const leftRect=(r)=>r.x-r.w/2;
const rightRect=(r)=>r.x+r.w/2;
test('Right stage and left nav fit inside visible body frame',
  leftRect(navRect)>=leftRect(bodyRect)+5 && rightRect(stageRect)<=rightRect(bodyRect)-8 &&
  topRect(stageRect)<topRect(bodyRect) && bottomRect(stageRect)>bottomRect(bodyRect));
test('Right/left outer gaps nearly equal and exact top/bottom stage alignment',
  Math.abs((leftRect(navRect)-leftRect(bodyRect))-(rightRect(bodyRect)-rightRect(stageRect)))<=8 &&
  navRect.y===stageRect.y && navRect.h===stageRect.h);
test('Centered profile title fully below upper border, above inner stage',
  titleRect.x===0 && topRect(titleRect)<topRect(bodyRect)-8 &&
  bottomRect(titleRect)>=topRect(stageRect)+20);
test('X button completely inside frame, clear of top and right edges',
  topRect(closeRect)<=topRect(bodyRect)-12 &&
  rightRect(closeRect)<=rightRect(bodyRect)-12 &&
  bottomRect(closeRect)>topRect(stageRect));
test('Outer body is framed by border on all four sides',
  borderRect.w>bodyRect.w && borderRect.h>bodyRect.h &&
  Math.abs(borderRect.x-bodyRect.x)<2 && Math.abs(borderRect.y-bodyRect.y)<2);
test('Only two read-only personal fields remain',
  profile.includes('lockedName = ReadOnlyRow(account,') &&
  profile.includes('lockedEmail = ReadOnlyRow(account,') &&
  !profile.includes('ReadOnlyRow(account, -56') &&
  (profile.match(/ReadOnlyRow\(account,/g)||[]).length===2);
test('Account action buttons are inside General Profile identity card',
  profile.includes('Btn(account, "RenameAccount"') &&
  profile.includes('Btn(account, "ChangePassword"') &&
  profile.includes('Btn(account, "SignOutAccount"'));
test('Rename action reuses canonical account-management listener and cooldown',
  profile.includes('OpenRenameAccountManager') &&
  profile.includes('\"RenameRegisteredRestAccountAsync\", PrivateInstance') &&
  profile.includes('\"RenameAsync\", PrivateInstance') && !profile.includes('existing.onClick.Invoke()'));
test('Password change sends reset link through existing native or REST auth',
  profile.includes('SendPasswordResetEmailAsync(email)') &&
  profile.includes('"SendPasswordResetRestAsync"') &&
  profile.includes('GetVerifiedCurrentEmail()') &&
  !profile.includes('UpdatePasswordAsync('));
test('Steam session has no sign-out action and forbids identity mutation',
  profile.includes('IsSteamSession() || string.IsNullOrWhiteSpace(currentUid)') &&
  profile.includes('if (signOutAction != null && signOutAction.gameObject.activeSelf != allowed)') &&
  profile.includes('signOutAction.gameObject.SetActive(allowed)') &&
  profile.includes('IsSteamSession()'));
test('Native and REST sign out delegate directly without the old account popup',
  profile.includes('"SignOut", PrivateInstance') &&
  profile.includes('"SignOutRestToNewGuestAsync", PrivateInstance') &&
  !profile.includes('"ConfirmRegisteredRestSignOut", PrivateInstance') &&
  !profile.includes('"OpenRegisteredRestAccountPanel", PrivateInstance'));
test('Sign-out and reset confirmation are UID scoped and use blocking overlay',
  profile.includes('verifiedUidAtPrompt') &&
  profile.includes('string.Equals(uid, currentUid, StringComparison.Ordinal)') &&
  profile.includes('"ProfileAccountBackdrop"') &&
  profile.includes('accountDialogBackdrop.GetComponent<Image>().raycastTarget = true'));
test('Source does not duplicate Store/Gold/XP or change RTDB grants',
  !profile.includes('wallet/gold') &&
  !profile.includes('SetRawJsonValueAsync') &&
  !profile.includes('ServerValue.increment') &&
  !profile.includes('UnityWebRequest.Put('));

test('Account rename now uses independent profile edit input',
  profile.includes('ProfileRenameField') && profile.includes('ApplyRenameFromProfileAsync'));
test('Registered REST email displayed through UID-bound native/REST source',
  profile.includes('registeredLiveEmail') && profile.includes('ReadOwnRtdbStringAsync(uid, token, "email")') &&
  profile.includes('MaskEmail(ownEmail)'));
test('Sign-out never invokes old REST account popup from Profile',
  profile.includes('"SignOutRestToNewGuestAsync", PrivateInstance') &&
  !profile.includes('"OpenRegisteredRestAccountPanel", PrivateInstance'));
test('No duplicate account controllers or authenticated login callbacks',
  !loginSkin.includes('AddComponent<AtlasBoardAccountAccessUI>') &&
  !loginSkin.includes('.onClick.AddListener'));
test('Modern login board illustration drawn in Unity UI',
  loginSkin.includes('RouteTile_') && loginSkin.includes('GoldenDice') &&
  loginSkin.includes('BoardIllustration'));
test('Login switch to registration restores normal form',
  loginSkin.includes('ResetOriginalLayout()') && loginSkin.includes('new Vector2(980f, 900f)'));

console.log('PROFILE_SOURCE_GATE='+passed+'/'+passed+' PASS; UNITY_COMPILER=NOT_RUN; DEVICE_QA=NOT_RUN; REWARD_BACKENDS=NOT_IMPLEMENTED');
