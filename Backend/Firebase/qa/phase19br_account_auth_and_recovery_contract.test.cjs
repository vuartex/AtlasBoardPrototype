'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../../../');
const source = fs.readFileSync(path.join(root,'Assets/Project/Scripts/Online/Firebase/AtlasBoardAccountFormQualityGate.cs'),'utf8');
const recovery = require('./phase19br_owner_steam_uid_recovery.cjs');
const steamFence = fs.readFileSync(path.join(root,'Assets/Project/Scripts/Online/Firebase/AtlasBoardSteamIdentitySessionFence.cs'),'utf8');
const roadmap = fs.readFileSync(path.join(root,'Assets/Project/Documentation/MASTER_ROADMAP.md'),'utf8');
let checks = 0;
const ok = (value, name) => { assert.ok(value,name); checks++; };
for (const [text,desc] of [
 ['RefreshLiveGuestQaIdentityAsync','Refresh same Guest identity before updating email'],
 ['string.Equals(oldUid, refreshedUid','No UID switch on refresh'],
 ['string.Equals(oldUid, response.localId','No UID switch after registration'],
 ['accounts:','REST Auth API'],
 ['"update" : "signInWithPassword"','Upgrade rather than new account creation'],
 ['TrySaveLiveGuestCredentials','Preserve Windows protected refresh token'],
 ['ApplyRegisteredGuestProfileAsync','Update existing RTDB profile'],
 ['general/accountType','registered profile claim'],
 ['uid_unchanged=true','Fail-safe diagnostic for unchanged UID'],
 ['SafeFirebaseErrorCode','Sanitized Firebase error label'],
 ['DescribeAuthFailure','Informative error UI'],
 ['CREDENTIAL_TOO_OLD_LOGIN_AGAIN','Stale credential case explicitly handled'],
 ['EMAIL_EXISTS','Existing email case explicitly handled'],
 ['INVALID_ID_TOKEN','Expired credential case explicitly handled'],
 ['firebase_code=','Log code without credentials'],
 ['status.alignment = TextAlignmentOptions.Top','Top-align status to stop vertical motion'],
 ['TextOverflowModes.Ellipsis','Prevent status line height shift'],
 ['StabilizeStatusText();','Use fixed status layout'],
 ['private void LateUpdate()','Reapply after UI view rebuilding'],
 ['input.caretPosition = input.text.Length','Return to end of entered input'],
 ['IsStrongPassword','Password complexity retained'],
 ['HandleKeyboardTab','Keyboard form navigation retained'],
 ['RoundedSprite','Rounded UI retained'],
 ]) ok(source.includes(text),desc);
const validArgs={uid:'5CXqXPXlG7_valid_example',steamId:'76561198038313931',appId:480};
ok(recovery.docId(validArgs.uid,validArgs.steamId).provider === 'steam_'+validArgs.steamId,'Matching Steam provider doc id');
ok(recovery.docId(validArgs.uid,validArgs.steamId).account === validArgs.uid+'_steam','Matching Atlas account doc id');
ok(recovery.parseArguments(['--uid',validArgs.uid,'--steam-id',validArgs.steamId,'--plan']).apply === false,'Safe dry-run default');
assert.throws(()=>recovery.parseArguments(['--uid',validArgs.uid,'--steam-id',validArgs.steamId,'--apply']),/attestation/); checks++;
ok(recovery.parseArguments(['--uid',validArgs.uid,'--steam-id',validArgs.steamId,'--apply','--owner-approval','I_VERIFIED_STEAM_ID_AND_ORIGINAL_UID']).apply,'Explicit owner approval');
assert.throws(()=>recovery.checkCollision(validArgs.uid,validArgs.steamId,{accountId:'otherUID',providerUserId:validArgs.steamId},null),/BLOCKED/); checks++;
assert.throws(()=>recovery.checkCollision(validArgs.uid,validArgs.steamId,null,{accountId:validArgs.uid,providerUserId:'76561198000000000'}),/BLOCKED/); checks++;
recovery.checkCollision(validArgs.uid,validArgs.steamId,{accountId:validArgs.uid,providerUserId:validArgs.steamId},null); checks++;
ok(recovery.eventId(validArgs.uid,validArgs.steamId).length===64,'Auditable event ID SHA256');
ok(steamFence.includes('ATB_STEAM_RETURNING_AUTH_FAIL category='),'Steam return emits safe diagnostic');
ok(steamFence.includes('reportedReturningFailure'),'Steam return diagnostic is bounded');
ok(roadmap.includes('v5.49.12br'),'Current checkpoint in roadmap');
ok(roadmap.includes('DEFERRED — owner decision'),'Server version enforcement remains deferred');
console.log(`ATB_PHASE19BR_SOURCE_CONTRACT PASS checks=${checks} unityCompile=NOT_RUN liveFirebase=NOT_RUN steamRecovery=NOT_APPLIED production=NOT_TOUCHED`);
