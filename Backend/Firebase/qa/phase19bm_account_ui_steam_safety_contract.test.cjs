'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../../../');
const read = p => fs.readFileSync(path.join(root, p), 'utf8');
let checks=0;
function check(condition, label) {assert.ok(condition,label);checks++;}
const ui = read('Assets/Project/Scripts/Online/Firebase/AtlasBoardAccountFormQualityGate.cs');
const steam = read('Assets/Project/Scripts/Online/Firebase/AtlasBoardSteamIdentitySessionFence.cs');
const help=read('Assets/Project/Scripts/UI/Menu/AtlasBoardPlayerHelpCredits.cs');
for(const [fragment,label] of [
 ['EmailFormat.IsMatch','email format required'],['PasswordVisibility','password toggle'],
 ['customCaretColor = true','visible caret'],['caretWidth = 3','caret width'],
 ['selectionColor','visible selection'],['LinkWithCredentialAsync','same-UID guest upgrade'],
 ['GUEST_UID_UPGRADE_CONTRACT_FAILED','UID must be unchanged'],['SignInAnonymouslyAsync','native guest persists'],
 ['IsLiveGuestQaSession','REST guest separation'],['IsLocalEmulatorGuestSession','emulator guest separation'],
 ['original.Invoke()','preserve regular registration flow'],
 ['captchaExpected','captcha still required']]) check(ui.includes(fragment),label);
for(const [fragment,label] of [
 ['verified.SteamId','Steam ID comparison'],['verified.AccountId','linked Auth UID comparison'],
 ['verified.Verified','verified auth only'],['FirebaseAuth.DefaultInstance.SignOut()','clear prior identity'],
 ['ClearLocalLobbyState','clear lobby on account change'],['LoadAsync()','reset wallet caches'],
 ['requires_verified_steam_return=true','fail closed on unknown Steam login']]) check(steam.includes(fragment),label);
check(help.includes('Hesap oluşturmadan oynayabilir miyim?'),'FAQ native guest');
check(help.includes('Gold, Atlas Coin, envanter ve Career'),'FAQ account switching');
check(help.includes('AtlasBoard desteği bunları istemez'),'support security language');
check(!help.includes('Full source/license ledger: Assets/Project/ThirdPartyLicenses/'),'no internal source path to players');
check(!help.includes('Ayrıntılı lisans kayıtları: Assets/Project/ThirdPartyLicenses/'),'no Turkish internal source path');
check(help.includes('ÜÇÜNCÜ TARAF HAKLARI'),'credits public language');
check(!steam.includes('DEV_GRANT') && !ui.includes('DEV_GRANT'),'no arbitrary wallet grant');
console.log(`ATB_PHASE19BM_ACCOUNT_UI_STEAM_SOURCE PASS checks=${checks} runtime=NOT_RUN`);
