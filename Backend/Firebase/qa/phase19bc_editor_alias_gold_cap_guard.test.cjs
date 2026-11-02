'use strict';
const fs=require('fs'),path=require('path'),assert=require('assert');
const root=path.resolve(__dirname,'../../..');
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
let checks=0;
const ok=(v,m)=>{assert.ok(v,m);checks++;};
const stable=read('Assets/Project/Scripts/Platform/AtlasBoardStableProfileNameView.cs');
const theme=read('Assets/Project/Scripts/Platform/AtlasBoardUniversalCareerTheme.cs');
const fast=read('Assets/Project/Scripts/QA/AtlasBoardDevelopmentMatchFastForwardQA.cs');
const journal=read('Assets/Project/Scripts/Progression/AtlasBoardRtdbCareerJournal.cs');
const rules=JSON.parse(read('Backend/Firebase/database.rules.json')).rules;

// Editor/Steam duplicate-name regression: a stale P1 alias must prove that its
// own TMP belongs to the authenticated authoritative Host slot before Vuartex
// can be painted over the roster's source text.
for(const token of [
  'IsAuthoritativeLocalHostLobbySeat()',
  '"privateSeatNames"',
  '"backendSnapshot"',
  '"backendLocalAccountId"',
  '"localIsHost"',
  'snapshot.HostAccountId',
  'member.SlotIndex != sourceSlot',
  'member.SeatMode != AtlasLobbySeatMode.HostLocal && !member.IsHost',
  'bool canonicalAllowed = surface != AliasSurface.LocalHostLobbySeat ||'
]) ok(stable.includes(token),'stable alias contract: '+token);
ok(stable.includes('canonicalAllowed && !string.IsNullOrWhiteSpace(canonical)'),
  'lobby alias cannot use canonical name after authority check fails');
for(const token of [
  'ResolveAuthoritativeSteamHostLobbySlot()',
  '"backendSnapshot"',
  '"backendLocalAccountId"',
  'snapshot.HostAccountId',
  'member.SlotIndex',
  'AtlasLobbySeatMode.HostLocal',
  'ATB_STEAM_LOBBY_ALIAS authoritative=true host_slot='
]) ok(theme.includes(token),'Steam theme authoritative host-slot contract: '+token);
ok(!theme.includes('TMP_Text firstSeat ='),'Steam lobby alias no longer hardcodes first seat');

// Guest Development build shortcut: compiled out of release players, host only,
// and deliberately lets the normal TurnManager end-of-match path fire.
ok(fast.startsWith('#if UNITY_EDITOR || DEVELOPMENT_BUILD'),'development only compile guard');
ok(fast.includes('Keyboard.current.f8Key.wasPressedThisFrame'),'F8 final-round hotkey');
ok(fast.includes('Keyboard.current.f9Key.wasPressedThisFrame'),'F9 finish-after-current-turn hotkey');
ok(fast.includes('reason=online_follower use_on_current_host=true'),'online follower denied');
ok(!fast.includes('.EndMatch('),'QA shortcut never calls EndMatch directly');
ok(fast.includes('completedActiveSlotsThisRound'),'uses existing round-completion state');

// Gold: same trusted Career claim -> canonical wallet path, with an absolute 50
// cap. Property/development performance remains XP-only.
for(const token of [
  'int participation = 5;',
  'Mathf.Min(20, Mathf.Max(0, rounds) * 2)',
  'result == "win" ? 25 : result == "tie" ? 15 : 0',
  'Mathf.Min(50, participation + roundBonus + outcome)'
]) ok(journal.includes(token),'Gold formula source: '+token);
ok(!journal.includes('int propertyBonus = Mathf.Min(20'), 'Gold has no property farming bonus');
ok(!journal.includes('int developmentBonus = Mathf.Min(20'), 'Gold has no development farming bonus');
const claim=rules.careerClaims.$uid.$matchId;
ok(claim.goldAwarded['.validate'].includes('<= 50'),'RTDB field rejects >50 Gold');
ok(claim['.validate'].includes("newData.child('rounds').val() * 2 > 20 ? 20"),'RTDB uses capped round bonus');
ok(claim['.validate'].includes("newData.child('result').val() === 'win' ? 25"),'RTDB win bonus 25');
ok(claim['.validate'].includes("newData.child('result').val() === 'tie' ? 15"),'RTDB tie bonus 15');
ok(!claim['.validate'].includes("newData.child('properties').val() * 2"),'RTDB Gold independent of properties');
ok(!claim['.validate'].includes("newData.child('developments').val() * 2"),'RTDB Gold independent of developments');
ok(rules.users.$uid.metaEconomy['.write'].includes("newData.child('operation/type').val() === 'MATCH_REWARD'"),
  'existing canonical MATCH_REWARD wallet path preserved');

console.log(`ATB_PHASE19BC_EDITOR_ALIAS_GOLD50_GUARD PASS checks=${checks} unity=NOT_RUN firebaseEmulator=NOT_RUN`);
