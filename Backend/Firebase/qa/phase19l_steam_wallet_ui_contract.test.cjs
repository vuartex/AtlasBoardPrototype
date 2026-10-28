'use strict';
// Source contract assertions. These do NOT compile Unity or authenticate Steam.
const fs = require('fs'), path = require('path'), assert = require('assert');
const root = path.resolve(__dirname, '../../..');
const read = p => fs.readFileSync(path.join(root, p), 'utf8');
const identity = read('Assets/Project/Scripts/UI/AtlasBoardCanonicalPlayerIdentity.cs');
const ui = read('Assets/Project/Scripts/UI/Meta/AtlasBoardMetaEconomyUI.cs');
const economy = read('Assets/Project/Scripts/Meta/AtlasBoardRtdbMetaEconomy.cs');
const rules = JSON.parse(read('Backend/Firebase/database.rules.json')).rules;
let checks = 0;
function ok(x, desc) { assert(x, desc); checks++; }
function contains(source, needle) { ok(source.includes(needle), `Missing: ${needle}`); }
// Distinct Steam and verified RTDB account display sources.
contains(identity, 'string steam = ValidSteamPersona();');
contains(identity, 'if (!string.IsNullOrWhiteSpace(steam)) return steam;');
contains(identity, 'string canonical = instance != null');
contains(identity, 'instance.ReadCanonicalHeaderName()');
contains(identity, 'string observedUid = ProfileHeaderAccountField.GetValue(canonicalHeader) as string;');
contains(identity, 'string.Equals(observedUid, account.CurrentAccountId, StringComparison.Ordinal)');
ok(!/return "Steam Player";/.test(identity), 'Offline Steam must not display a fake Steam persona');
contains(identity, 'return string.IsNullOrWhiteSpace(pending) || pending == "menu.player"');
// v5.49.12c: prevent C# CS0136 name shadowing in Resolve().
const resolver = identity.slice(identity.indexOf('public static string Resolve()'), identity.indexOf('private string ReadCanonicalHeaderName()'));
ok((resolver.match(/AtlasBoardAccountService account =/g) || []).length === 1, 'Exactly one `account` declaration in resolver scope');
contains(resolver, 'AtlasBoardAccountService steamPendingAccount = AtlasBoardAccountService.Instance;');
contains(resolver, 'steamPendingAccount != null && steamPendingAccount.IsSignedIn');
// The UI accepts a server-verified tester UID, independent of SteamAPI.Init.
contains(ui, 'private bool HasVerifiedApprovedTestWallet()');
contains(ui, '!string.IsNullOrWhiteSpace(uid)');
contains(ui, 'string.Equals(lastLiveWalletUid, uid, StringComparison.Ordinal)');
contains(ui, 'snapshot != null && snapshot.Success && snapshot.WalletVerified &&');
contains(ui, 'snapshot.IsDeveloperTester;');
contains(ui, 'if (HasVerifiedApprovedTestWallet()) return true;');
ok(!/if \(AtlasBoardRtdbMetaEconomy.IsSteamSession &&\s*snapshot != null && snapshot.WalletVerified &&\s*snapshot.IsDeveloperTester\) return true;/.test(ui), 'Tester permission must not be gated on Steamworks');
contains(ui, 'bool approvedTestWallet = HasVerifiedApprovedTestWallet();');
contains(ui, '!approvedTestWallet;');
contains(ui, 'if (!AtlasBoardRtdbMetaEconomy.IsRegistered &&\n                        !HasVerifiedApprovedTestWallet())');
contains(ui, 'string requestedUid = AtlasBoardRtdbMetaEconomy.CurrentUid;');
contains(ui, 'if (requestedUid != AtlasBoardRtdbMetaEconomy.CurrentUid)');
// Never infer backend permission from Steam state: unchanged RTDB grant controls.
contains(economy, 'snapshot.IsDeveloperTester = await IsDeveloperTesterAsync(identity);');
contains(economy, 'if ((identity.User.IsAnonymous || kind == "DEV_GRANT" ||');
contains(economy, '!await IsDeveloperTesterAsync(identity))');
const write = rules.users.$uid.metaEconomy['.write'];
ok(write.includes("root.child('devWalletTesters').child($uid).val() === true"), 'Rules must check approved exact UID');
ok(write.includes("newData.child('operation/type').val() === 'PURCHASE'"), 'Existing purchase transaction must remain server guarded');
ok(write.includes('newData.child(\'wallet/gold\').val()'), 'Do not bypass RTDB money validation');
function approved({verified, tester, storedUid, uid, success=true}) {
  return !!uid && storedUid === uid && success && verified && tester;
}
ok(approved({verified:true,tester:true,storedUid:'A',uid:'A'}), 'Verified tester allowed with Steam down');
ok(!approved({verified:true,tester:false,storedUid:'A',uid:'A'}), 'Unapproved Guest denied');
ok(!approved({verified:false,tester:true,storedUid:'A',uid:'A'}), 'Unverified wallet denied');
ok(!approved({verified:true,tester:true,storedUid:'A',uid:'B'}), 'Account switching denies stale tester');
ok(!approved({verified:true,tester:true,storedUid:'',uid:'A'}), 'No UID binding denied');
console.log(`ATB_PHASE19L_STEAM_WALLET_UI_SOURCE_QA PASS checks=${checks} unityEditor=NOT_RUN steamSDK=NOT_RUN firebaseEmulator=NOT_RUN`);
