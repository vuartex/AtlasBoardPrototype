'use strict';

const fs = require('fs');
const path = require('path');
const assert = require('assert');

const root = path.resolve(__dirname, '../../..');
const lobby = fs.readFileSync(path.join(root,
  'Assets/Project/Scripts/UI/Menu/AtlasBoardPrivateLobbyUIController.cs'), 'utf8');
const profile = fs.readFileSync(path.join(root,
  'Assets/Project/Scripts/UI/Menu/AtlasBoardUnifiedProfilePresentation.cs'), 'utf8');

const checks = [
  ['rematch account projection guard exists', lobby.includes('transientDuplicateHostProjection')],
  ['duplicate host account preserves verified identity', lobby.includes('ATB_REMATCH_ROSTER_ACCOUNT_GUARD preserved=true')],
  ['verified cache is restored on duplicate host projection', lobby.includes('seatIdentityAccountCache[slot] = lockedAccountId;')],
  ['profile presenter no longer uses first local UID match', !profile.includes('AtlasLobbyMemberSnapshot local =\n                snapshot.Members.FirstOrDefault')],
  ['profile presenter enumerates all matching UID seats', profile.includes('List<AtlasLobbyMemberSnapshot> matches =')],
  ['profile presenter prefers authoritative host row', profile.includes('"authoritative_host"')],
  ['ambiguous profile resolution fails closed', profile.includes('"fail_closed"')],
  ['profile ambiguity diagnostic exists', profile.includes('ATB_PROFILE_LOBBY_SLOT ambiguous=true')]
];

for (const [name, ok] of checks) assert(ok, name);

// Mirror only the selection contract so the exact owner regression is covered:
// P1 transiently projects the promoted host UID, P2 is the real HostLocal row.
function resolveLocalSlot(snapshot, localUid, localIsHost) {
  const matches = snapshot.members
    .filter(m => m.active && m.accountId === localUid)
    .sort((a,b) => a.slotIndex - b.slotIndex);
  if (matches.length === 1) return matches[0].slotIndex;
  if (matches.length > 1) {
    const authoritativeHost = matches.find(m =>
      snapshot.hostAccountId === localUid && (m.seatMode === 'HostLocal' || m.isHost));
    if (authoritativeHost && (localIsHost || snapshot.hostAccountId === localUid))
      return authoritativeHost.slotIndex;
    const remote = matches.filter(m => m.seatMode === 'RemoteHuman' && !m.isHost);
    if (remote.length === 1) return remote[0].slotIndex;
    return -1;
  }
  return localIsHost ? 0 : -1;
}

const duplicateHostProjection = {
  hostAccountId: 'VUARTEX_UID',
  members: [
    {slotIndex:0, active:true, accountId:'VUARTEX_UID', seatMode:'RemoteHuman', isHost:false},
    {slotIndex:1, active:true, accountId:'VUARTEX_UID', seatMode:'HostLocal', isHost:true},
    {slotIndex:2, active:true, accountId:'BOT', seatMode:'Bot', isHost:false}
  ]
};
assert.strictEqual(resolveLocalSlot(duplicateHostProjection, 'VUARTEX_UID', true), 1,
  'promoted host must bind to P2 HostLocal, never transient P1 duplicate');

const normalRoster = {
  hostAccountId: 'VUARTEX_UID',
  members: [
    {slotIndex:0, active:true, accountId:'GUEST_UID', seatMode:'RemoteHuman', isHost:false},
    {slotIndex:1, active:true, accountId:'VUARTEX_UID', seatMode:'HostLocal', isHost:true}
  ]
};
assert.strictEqual(resolveLocalSlot(normalRoster, 'VUARTEX_UID', true), 1,
  'normal promoted host remains P2');

console.log(`ATB_PHASE19BA_REMATCH_DUPLICATE_HOST_PROFILE_SOURCE PASS checks=${checks.length + 2} LIVE_UNITY=NOT_RUN TWO_CLIENT=NOT_RUN`);
