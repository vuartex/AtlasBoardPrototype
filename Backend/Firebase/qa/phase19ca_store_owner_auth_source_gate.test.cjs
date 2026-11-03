'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../../../');
const source = p => fs.readFileSync(path.join(root,p),'utf8');
const wallet = source('Assets/Project/Scripts/Meta/AtlasBoardRtdbMetaEconomy.cs');
const consoleCode = source('Assets/Project/Scripts/UI/Menu/AtlasBoardOwnerConsole.cs');
const roadmap = source('Assets/Project/Documentation/MASTER_ROADMAP.md');
const codebook = source('Assets/Project/Documentation/FRONTEND_ERROR_CODEBOOK.md');
let count=0;const check=(c,msg)=>{assert.ok(c,msg);count++};
[
[wallet.includes('RefreshRegisteredRestBearerAsync(Identity identity)'), 'existing REST bearer helper exists'],
[wallet.includes('!identity.IsRestGuest || identity.IsAnonymous'), 'only non-anonymous REST sessions can refresh'],
[wallet.includes('TryGetVerifiedRegisteredRestPrincipal('), 'only verified registered UID can refresh'],
[wallet.includes('RefreshLiveGuestQaIdentityAsync'), 'canonical lobby refresh reused'],
[wallet.includes('string.Equals(liveUid, identity.Uid'), 'UID continuity enforced'],
[wallet.includes('!string.Equals(CurrentUid, identity.Uid'), 'active principal continuity enforced'],
[wallet.includes('identity.Token = liveToken;'), 'refreshed token only written to same identity'],
[wallet.includes('response.Status == 401 || response.Status == 403'), 'read retry only on Auth / permission error'],
[wallet.includes('permissionRetryUsed = true;'), 'mutation permission retry bounded'],
[wallet.includes('continue; // new ETag read; never reuse a stale PUT body'), 'fresh ETag after auth retry'],
[wallet.includes('if (written.Status == 412) continue;'), 'concurrent-write ETag retry retained'],
[wallet.includes('ATB_STORE_WALLET_READ'), 'wallet read HTTP stage trace'],
[wallet.includes('ATB_STORE_MUTATION stage=wallet_put_failed'), 'write failure HTTP stage trace'],
[wallet.includes('ATB_STORE_AUTH_REFRESH'), 'token refresh stage trace'],
[!wallet.includes('SignInAnonymouslyAsync'), 'no new anonymous account minted'],
[!wallet.includes('signUp?key='), 'no new registered account minted'],
[wallet.includes('IsDeveloperTesterAsync(identity)'), 'original server QA allowlist remains'],
[consoleCode.includes('EqualDigest(entered, UnlockDigest)'), 'hashed local session code required'],
[consoleCode.includes('wallet.WalletVerified && wallet.IsDeveloperTester'), 'server allowlist remains mandatory'],
[consoleCode.includes('ACCESS DENIED — THIS UID HAS NO SERVER QA ALLOWLIST ENTRY'), 'diagnoses missing server QA permit'],
[consoleCode.includes('ATB_OWNER_QA_GATE'), 'safe admin gate logging'],
[consoleCode.includes('typeof(AtlasBoardDevelopmentMatchFastForwardQA)'), 'existing final round host QA type found reliably'],
[consoleCode.includes('typeof(AtlasBoardPhase14JRuntimeHarness)'), 'existing phase14j type found reliably'],
[consoleCode.includes('#if UNITY_EDITOR || DEVELOPMENT_BUILD'), 'nonrelease privileged exclusion'],
[!consoleCode.includes('FirebaseDatabase.DefaultInstance.GetReference("devWalletTesters")'), 'no client-side self-authorization'],
[!consoleCode.includes('DEV_GRANT ACCEPTED') || consoleCode.includes('GrantDevAsync()'), 'grants remain RTDB-validated'],
[roadmap.includes('v5.49.12ca'), 'checkpoint in canonical roadmap'],
[codebook.includes('ATB958') && codebook.includes('ATB962'), 'new support code definitions retained'],
].forEach(([c,m])=>check(c,m));
console.log(`ATB_PHASE19CA_STORE_OWNER_AUTH_SOURCE PASS checks=${count} unity_compile=NOT_RUN emulator=NOT_RUN production=NOT_TOUCHED`);
