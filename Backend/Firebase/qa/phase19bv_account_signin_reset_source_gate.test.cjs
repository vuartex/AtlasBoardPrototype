"use strict";
const fs=require('fs'),path=require('path'),assert=require('assert');
const root=path.resolve(__dirname,'../../..');
const q=fs.readFileSync(path.join(root,'Assets/Project/Scripts/Online/Firebase/AtlasBoardAccountFormQualityGate.cs'),'utf8');
const b=fs.readFileSync(path.join(root,'Assets/Project/Documentation/FRONTEND_ERROR_CODEBOOK.md'),'utf8');
const m=fs.readFileSync(path.join(root,'Assets/Project/Documentation/MASTER_ROADMAP.md'),'utf8');
let checks=0;
function has(src,pattern,label){assert(src.includes(pattern),label+': missing '+pattern);checks++}
function absent(src,pattern,label){assert(!src.includes(pattern),label+': unexpected '+pattern);checks++}
const features=[
 ['string.IsNullOrWhiteSpace(GetText(ui, "loginPassword"))','empty password blocked'],
 ['ATB739','empty password support code'],
 ['BindRegisteredLiveGuest(response.localId, restoredName);','REST email login sets registered session'],
 ['AtlasBoardGuestIdentity.NameForAuthenticatedUid(response.localId)','fallback uid-scoped nickname'],
 ['_ = LoadRegisteredGuestNameAsync(response.localId, response.idToken);','async Auth display-name lookup'],
 ['openCareerAfterAuthentication','Career request retained'],
 ['OpenCareerAfterRestAuthentication();','Career opened after login'],
 ['BindRegisteredCareerEntry();','Career button redirected for REST users'],
 ['ShowGlobalAccountNotice(ui, register','successful login visible'],
 ['ShowStatus(ui, string.Empty);','error popup single message'],
 ['ShowPasswordResetDialog(ui)','password reset UI'],
 ['class PasswordResetEmailRequest','reset payload'],
 ['public string requestType = "PASSWORD_RESET";','reset type'],
 ['accounts:sendOobCode','Firebase password reset API'],
 ['EmailFormat.IsMatch(email)','local reset email validation'],
 ['email_redacted=true','redacted request logging'],
 ['FinishPasswordResetNotice(ui)','generic password reset confirmation'],
 ['request.responseCode >= 300','auth rejection handled'],
 ['TrySaveLiveGuestCredentials','existing DPAPI session reused'],
 ['UnityWebRequest.EscapeURL(LiveGuestWebApiKey)','existing API key'],
 ['string.Equals(oldUid, response.localId, StringComparison.Ordinal)','signup UID invariant'],
 ['AtlasBoardRtdbMetaEconomy.LoadAsync();','UID-scoped wallet refresh'],
 ['private static bool IsStrongPassword','registration policy preserved'],
 ['HandleKeyboardTab();','keyboard accessibility preserved'],
 ['MoveCaretToEndAfterFocusAsync','caret owner accepted PASS preserved'],
 ['return registering','error categorization preserved'],
];
for(const [code,label] of features)has(q,code,label);
for(const code of ['ATB731','ATB732','ATB733','ATB734','ATB735','ATB736','ATB737','ATB738','ATB739','ATB741','ATB742'])has(b,'| '+code+' |','error code '+code);
for(const x of ['Phase19BV','DEFERRED — OWNER DECISION','Steam production trusted verification'])has(m,x,'roadmap');
for(const bad of ['Debug.Log(password)','Debug.Log(email)','Debug.Log(token)','accounts:update?key=" +\n                (register'])absent(q,bad,'security guard');
// Verify immediate success-state binding is not inside delayed display-name lookup.
const i=q.indexOf('// IMPORTANT: registeredLiveUid');
const j=q.indexOf('BindRegisteredLiveGuest(response.localId, restoredName);',i);
const k=q.indexOf('_ = LoadRegisteredGuestNameAsync(response.localId, response.idToken);',i);
assert(i>=0 && i<j && j<k,'registered session must bind before name lookup');checks++;
assert.strictEqual((q.match(/class PasswordResetEmailRequest/g)||[]).length,1);checks++;
console.log('ATB_PHASE19BV_ACCOUNT_SIGNIN_RESET_SOURCE PASS checks='+checks+' runtime=NOT_RUN production=NOT_TOUCHED');
