'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../../..');
const form = fs.readFileSync(path.join(root, 'Assets/Project/Scripts/Online/Firebase/AtlasBoardAccountFormQualityGate.cs'), 'utf8');
const wallet = fs.readFileSync(path.join(root, 'Assets/Project/Scripts/Meta/AtlasBoardRtdbMetaEconomy.cs'), 'utf8');
const roadmap = fs.readFileSync(path.join(root, 'Assets/Project/Documentation/MASTER_ROADMAP.md'), 'utf8');
const codes = fs.readFileSync(path.join(root, 'Assets/Project/Documentation/FRONTEND_ERROR_CODEBOOK.md'), 'utf8');
let count = 0;
function check(what, pattern, detail) {
 assert.ok(pattern.test(what), detail);
 count++;
}
function absent(what, pattern, detail) {
 assert.ok(!pattern.test(what), detail);
 count++;
}
// Login/REST consistency and hard-bound canonical UID.
check(form,/TryGetVerifiedRegisteredRestPrincipal\(out string uid\)/,'single REST principal contract');
check(form,/string\.Equals\(liveUid, instance\.registeredLiveUid/,'REST identity bound to current UID');
check(form,/string\.IsNullOrWhiteSpace\(liveToken\)/,'real REST token required');
check(form,/profile\.users\[0\]\.email/,'provider lookup email evidence');
check(form,/ATB_ACCOUNT_PROFILE_SYNC stage=signin/,'login profile reconciliation');
check(form,/ATB_ACCOUNT_PROFILE_SYNC stage=restore/,'restart profile reconciliation');
check(form,/wallet_transfer=false/,'explicit no-transfer contract');
check(wallet,/AtlasBoardAccountFormQualityGate\s*\.TryGetVerifiedRegisteredRestPrincipal\(out _\)/,'Store account gate uses registered REST identity');
check(wallet,/string\.Equals\(guestUid, verifiedUid, StringComparison\.Ordinal\)/,'wallet UID must equal REST registered UID');
check(wallet,/IsAnonymous = !registered/,'wallet must distinguish anonymous and linked user');
check(wallet,/IsRestGuest = true/,'REST principal must remain distinct from native principal');
check(wallet,/identity\.IsRestGuest\)\s*\{/,'REST mutation branch');
check(wallet,/identity\.IsAnonymous \|\| !await RegisteredRestProfileExistsAsync\(identity\)/,'guest blocked before paid operation');
check(wallet,/"\/general\/accountType\.json"/,'server profile check path');
check(wallet,/"\\\"registered\\\""/,'server registered profile value');
check(wallet,/RequestAsync\(identity, "GET"/,'server token used for profile read');
check(wallet,/SyncCurrentUserAsync\("", false\)/,'native flow preserved');
check(wallet,/kind == "DEV_GRANT"/,'existing administrator allowlist remains');
check(wallet,/await IsDeveloperTesterAsync\(identity\)/,'test-wallet allowlist never removed');
check(wallet,/"meta\.error\.registered_only"/,'guest block remains');
// The original wallet rules, order, gold and Atlas Coin constants stay intact.
check(wallet,/case "PURCHASE":/,'purchase branch preserved');
check(wallet,/case "DAILY":/,'daily branch preserved');
check(wallet,/"\/devWalletTesters\/"/,'test allowlist path preserved');
check(wallet,/"MATCH_REWARD"/,'gold reward type preserved');
// Interactive registered-account UI and server-throttled profile rename.
check(form,/OpenRegisteredRestAccountPanel\(ui\)/,'account context now opens management view');
check(form,/"ATB_Registered_Account_Panel"/,'registered-account panel named');
check(form,/"ADI DEĞİŞTİR"/,'rename affordance');
check(form,/"ŞİFREYİ DEĞİŞTİR"/,'password-reset affordance');
check(form,/"HESAPTAN ÇIKIŞ YAP"/,'logout affordance');
check(form,/lastDisplayNameChangeAt/,'24h cooldown timestamp passed to RTDB rules');
check(form,/TrySaveRegisteredDisplayNameInFirebaseAuthAsync\(currentUid, token, newName\)/,'Auth nickname updated only after RTDB accept');
check(form,/ATB_ACCOUNT_RENAME status=denied_or_cooldown/,'rename refusal logging');
check(form,/ATB_ACCOUNT_SIGNOUT result=new_anonymous/,'logout success logging');
check(form,/"TrySaveLiveGuestCredentials"/,'new identity saved encrypted');
check(form,/string\.Equals\(account\.localId, oldUid, StringComparison\.Ordinal\)/,'reject returning same UID as fresh guest');
check(form,/"TrySaveLiveGuestCredentials"[\s\S]{0,700}SetBridgeField\(bridge, "currentAccountId", account\.localId\)/,'persist new identity before switch');
check(form,/registeredLiveVerified = false;[\s\S]{0,220}lastProbedLiveUid = string\.Empty/,'clear former registered session');
check(form,/AtlasBoardGuestIdentity\.BindLiveAuthenticatedUid\(account\.localId\)/,'new guest name bound to new UID');
check(form,/await AtlasBoardRtdbMetaEconomy\.LoadAsync\(\)/,'after logout wallet reloaded');
check(form,/"PASSWORD_RESET"/,'existing password reset kept');
absent(form,/DeleteUserAsync|RemoveUserAsync|deleteAccount\s*\(/i,'must never delete existing registered user on logout');
absent(form,/CopyWallet|TransferGold|MergeGold|ImportGuestGold/i,'must not merge anonymous rewards into registered account');
check(roadmap,/v5\.49\.12bx/,'new canonical roadmap checkpoint');
check(roadmap,/DEFERRED.*OWNER|DEFERRED.*owner/i,'version gate still deferred');
check(codes,/ATB743/,'rename support code registered');
check(codes,/ATB744/,'signout support code registered');
console.log(`ATB_PHASE19BX_REST_ACCOUNT_WALLET_CONTINUITY_SOURCE PASS checks=${count} unity_compile=NOT_RUN firebase_emulator=NOT_RUN production=NOT_TOUCHED`);
