'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../../../');
const read = p => fs.readFileSync(path.join(root, p), 'utf8');
const form = read('Assets/Project/Scripts/Online/Firebase/AtlasBoardAccountFormQualityGate.cs');
const fence = read('Assets/Project/Scripts/Online/Firebase/AtlasBoardSteamIdentitySessionFence.cs');
const menu = read('Assets/Project/Editor/AtlasBoardAccountQaWindowsBuilder.cs');
const roadmap = read('Assets/Project/Documentation/MASTER_ROADMAP.md');
let checks = 0;
function ok(condition, message) { assert.ok(condition, message); checks++; }
for (const [x, name] of [
 ['AuthenticateLiveGuestRestAsync(ui, register, email.Trim())', 'Existing Guest launch can register/sign in'],
 ['accounts:', 'Identity Toolkit REST protocol'],
 ['"update" : "signInWithPassword"', 'Credential link / returning sign-in endpoints'],
 ['TryGetLiveGuestRewardIdentity', 'Uses existing canonical live Guest UID'],
 ['IsStillAnonymousLiveGuestAsync(token)', 'Prevents changing an already registered account'],
 ['accounts:lookup?key=', 'Firebase Auth user lookup'],
 ['!string.Equals(oldUid, response.localId', 'Registration refuses UID change'],
 ['TrySaveLiveGuestCredentials', 'Persist credentials via existing Windows DPAPI'],
 ['SetBridgeField(bridge, "currentAccountId", response.localId)', 'Bridge switched to verified UID'],
 ['SetBridgeField(bridge, "liveGuestRefreshToken", response.refreshToken)', 'Refresh token switched'],
 ['bridge.ClearLocalLobbyState()', 'Old online room not kept across account switch'],
 ['BindLiveAuthenticatedUid(response.localId)', 'Canonical guest identity rebound'],
 ['"general/accountType\\\":\\\"registered\\\"', 'RTDB registered provider projection'],
 ['ApplyRegisteredGuestProfileAsync(', 'Registered RTDB projection'],
 ['"email\\\":\\\"', 'UID scoped email projection'],
 ['AtlasBoardRtdbMetaEconomy.LoadAsync()', 'Wallet reloaded using new UID'],
 ['IsStrongPassword(', 'Strong password validation'],
 ['value.Length < 6', 'Six-character minimum'],
 ['char.IsLetter(ch)', 'Letter required'],
 ['char.IsDigit(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch)', 'Number or symbol required'],
 ['ValidateNewPassword(ui)', 'Password validated on registration'],
 ['ValidateCaptcha(ui)', 'Captcha retained'],
 ['EmailFormat.IsMatch', 'Email syntax gate retained'],
 ['HandleKeyboardTab()', 'Tab navigation dispatcher'],
 ['keyboard.tabKey.wasPressedThisFrame', 'Input System keyboard support'],
 ['Input.GetKeyDown(KeyCode.Tab)', 'Legacy keyboard support'],
 ['"RegisterName", "RegisterEmail", "RegisterPassword", "RegisterPasswordRepeat"', 'Registration tab order'],
 ['"CaptchaAnswer", "Button_CreateAccount", "Button_BackToLogin"', 'Registration buttons tab order'],
 ['EventSystem.current?.SetSelectedGameObject', 'Focus moves through active controls'],
 ['StabilizeStatusText()', 'Status font size pinned'],
 ['status.enableAutoSizing = false', 'No dynamic font oscillation'],
 ['status.fontSize = 16f', 'Stable status font size'],
 ['PasswordVisibility', 'Existing eye toggle preserved'],
 ['RoundedSprite()', 'Existing rounded buttons preserved'],
 ['LinkWithCredentialAsync', 'Native Firebase guest linking preserved']
]) ok(form.includes(x), name);
ok(!form.includes('REST Guest oturumu ayrı bir kimliktir. Aynı UID ile kayıt denemesi'), 'Removed old special Guest registration blocker');
ok(!form.includes('Build Account QA (Windows)'), 'No second Guest build advice');
ok(fence.includes('changedWithinThisProcess'), 'Old Steam session rejected only for actual in-process Steam switch');
ok(fence.includes('startupSessionCleared = true'), 'Startup cached identity still cleared');
ok(fence.includes('verified.SteamId') && fence.includes('verified.AccountId'), 'Steam and Firebase verified pair still required');
ok(fence.includes('FirebaseAuth.DefaultInstance.SignOut()'), 'Unverified Steam cached identities still rejected');
ok(!menu.includes('MenuItem('), 'No extra Account QA menu');
ok(roadmap.includes('DEFERRED') && roadmap.includes('owner'), 'Server version gate deferred');
console.log(`ATB_PHASE19BQ_SOURCE_CONTRACT PASS checks=${checks} liveRegistration=NOT_RUN unityCompile=NOT_RUN firebaseProduction=NOT_TOUCHED`);
