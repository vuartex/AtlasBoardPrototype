#!/usr/bin/env node
'use strict';
/*
 * AtlasBoard security release preflight. READ ONLY: no account writes or deploy.
 * Runs in Backend/Firebase as node qa/phase19bl_identity_release_preflight.test.cjs.
 * Intentionally exits nonzero until ALL four release gates have real source fixes.
 * This is NOT an emulator acceptance test or runtime account-switch test.
 */
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..', '..', '..');
const load = (rel) => {
  const p = path.join(root, rel);
  if (!fs.existsSync(p)) throw new Error('REQUIRED_SOURCE_MISSING ' + rel);
  return fs.readFileSync(p,'utf8');
};
const service=load('Assets/Project/Scripts/Online/Firebase/AtlasBoardAccountService.cs');
const profile=load('Assets/Project/Scripts/Online/Firebase/AtlasBoardRtdbUserProfileMirror.cs');
const platform=load('Assets/Project/Scripts/Platform/AtlasBoardPlatformRuntime.cs');
const transport=load('Assets/Project/Scripts/Online/Firebase/AtlasBoardRtdbCasualLobbyTransport.cs');
const rules=JSON.parse(load('Backend/Firebase/database.rules.json'));
const checks=[];
const record=(id,pass,reason) => checks.push({id,pass,reason});
const room=rules.rules?.roomMeta?.$roomId;
const joins=rules.rules?.joinRequests?.$roomId?.$uid;
const linkInRegistration=/LinkWithCredentialAsync\s*\(/.test(service);
record('P0_GUEST_IN_PLACE_UPGRADE', linkInRegistration,
  linkInRegistration?'Guest Auth credential linking found; needs emulator proof':'Registration does not include LinkWithCredentialAsync (new UID risk)');
const provenance=/guestOrigin/.test(profile) && !!(rules.rules?.users?.$uid?.guestOrigin);
record('P0_GUEST_PROVENANCE_RULES', provenance,
  provenance?'Guest provenance schema present; needs forgery tests':'Immutable guestOrigin schema/rules not present');
const steamFailClosed= !/Continuing with the\s*['"\s+]*current Atlas identity/.test(platform) &&
  /identity|steam|account/i.test(platform);
record('P0_STEAM_FAILED_RETURN_NO_STALE_FALLBACK', steamFailClosed,
  steamFailClosed?'No known silent fallback phrase; needs Steam A/B proof':'Steam fallback may continue with an unrelated cached Firebase identity');
const logoutSafe= /SignOut\s*\(\s*\)/.test(service) &&
 /SignInAnonymouslyAsync\s*\(|GuestIdentityTransition|SwitchToGuest|GuestSession/i.test(service);
record('P0_LOGOUT_VERIFIED_GUEST_SESSION',logoutSafe,
 logoutSafe?'Guest-session transition code found; needs runtime QA':'SignOut does not establish a verified distinct Guest principal');
const metadataVersion = !!(room && room.gameVersion && room.protocolVersion && room.contentVersion);
const requestVersion = !!(joins && joins.gameVersion && /gameVersion/.test(joins['.write']||'') && /protocolVersion/.test(joins['.write']||''));
record('P1_ROOMMETA_SERVER_VERSION_CONTRACT',metadataVersion,
 metadataVersion?'roomMeta version validators found; emulator tests still needed':'roomMeta does not enforce the room version contract');
record('P1_SERVER_REJECTS_OLD_JOIN_REQUEST',requestVersion,
 requestVersion?'Server-side join version checks found; emulator proof needed':'joinRequests does not enforce a compatible game/protocol version');
const result={status:checks.every(x=>x.pass)?'SOURCE_CANDIDATE_ONLY':'BLOCKED',
 production:'NOT_TOUCHED',checks:checks.map(x=>({id:x.id,result:x.pass?'SOURCE_CANDIDATE':'BLOCKED',reason:x.reason}))};
console.log(JSON.stringify(result,null,2));
console.log(`ATB_PHASE19BL_IDENTITY_RELEASE_PREFLIGHT ${result.status} passed=${checks.filter(x=>x.pass).length} blocked=${checks.filter(x=>!x.pass).length} source_only=true production=NOT_TOUCHED`);
if(result.status!=='SOURCE_CANDIDATE_ONLY') process.exitCode=2;
