'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../../..');
const quality = fs.readFileSync(path.join(root, 'Assets/Project/Scripts/Online/Firebase/AtlasBoardAccountFormQualityGate.cs'), 'utf8');
const rematch = fs.readFileSync(path.join(root, 'Assets/Project/Editor/AtlasBoardRematchFastForwardQA.cs'), 'utf8');
let checks = 0;
function must(pattern, message) { assert.match(quality, pattern, message); checks++; }
function mustNot(pattern, message) { assert.doesNotMatch(quality, pattern, message); checks++; }
assert(!rematch.includes('FindFirstObjectByType<'), 'obsolete rematch API remains');checks++;
assert(rematch.includes('FindAnyObjectByType<TurnManager>()'));checks++;
assert(rematch.includes('FindAnyObjectByType<AtlasBoardTurnDiceNetworkCoordinator>()'));checks++;
must(/TryGetVerifiedRegisteredRestPrincipal\(out string uid\)/, 'no central verified rest session contract');
must(/instance\.registeredLiveVerified/, 'no server verified session marker');
must(/TryGetLiveGuestRewardIdentity\([\s\S]{0,120}liveToken/, 'auth gate not bound to current live token');
must(/string\.Equals\(liveUid, instance\.registeredLiveUid/, 'session UID continuity check missing');
must(/registeredLiveVerified = false/, 'session reset missing');
must(/registeredLiveVerified = true/, 'verified binding missing');
must(/BindRegisteredAccountContext\(\)/, 'settings account hook missing');
must(/screen=settings_account state=callback_bound/, 'settings trace missing');
must(/screen=account state=registered_verified/, 'account trace missing');
must(/screen=bootstrap state=registered_verified/, 'restart lookup trace missing');
must(/screen=global auth=/, 'periodic audit missing');
must(/store_native_registered_gate=/, 'store gate logging missing');
must(/registeredLiveEmail = profile\.users\[0\]\.email/, 'email profile continuity missing');
must(/name = AtlasBoardGuestIdentity\.NameForAuthenticatedUid\(uid\)/, 'fallback display-name handling missing');
mustNot(/if \(!string\.IsNullOrWhiteSpace\(stored\)\) BindRegisteredLiveGuest\(uid, stored\)/,
  'unverified local cached nickname treated as authenticated');
must(/title\.rectTransform\.anchoredPosition = new Vector2\(0f, 145f\)/,
 'reset title placement missing');
must(/message\.rectTransform\.anchoredPosition = new Vector2\(0f, 71f\)/,
 'reset message placement missing');
must(/new Vector2\(560f, 55f\)/, 'reset subtitle height missing');
must(/Spam\/Gereksiz/, 'spam folder reminder missing');
must(/"PASSWORD_RESET"/, 'Firebase reset operation missing');
must(/!EmailFormat\.IsMatch\(email\)/, 'invalid email guard missing');
must(/ShowGlobalAccountNotice\(ui, summary\)/, 'account UI notice missing');
must(/language == "ko"/, 'other-language account label missing');
console.log(`ATB_PHASE19BW_REST_ACCOUNT_UI_SOURCE PASS checks=${checks} runtime=NOT_RUN store_wallet_gate=OPEN production=NOT_TOUCHED`);
