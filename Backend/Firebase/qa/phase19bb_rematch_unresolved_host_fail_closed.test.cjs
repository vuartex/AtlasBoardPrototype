'use strict';

const fs = require('fs');
const path = require('path');
const assert = require('assert');

const root = path.resolve(__dirname, '../../..');
const profile = fs.readFileSync(path.join(root,
  'Assets/Project/Scripts/UI/Menu/AtlasBoardUnifiedProfilePresentation.cs'), 'utf8');
const lobby = fs.readFileSync(path.join(root,
  'Assets/Project/Scripts/UI/Menu/AtlasBoardPrivateLobbyUIController.cs'), 'utf8');

const checks = [
  ['shared profile presenter no longer calls lobby seat writer from LateUpdate',
    !/SynchronizeSharedProfileCards\(\);\s*SynchronizeLocalLobbySeat\(\);/.test(profile)],
  ['host slot zero fallback removed',
    !profile.includes('return localIsHost\n            ? 0\n            : -1;')],
  ['unresolved host fails closed', profile.includes('no_local_uid_match_fail_closed')],
  ['missing local uid fails closed', profile.includes('missing_local_uid_fail_closed')],
  ['private lobby remains authoritative seat writer', lobby.includes('private void SetSeat(int index, string name, string status)')],
  ['rematch verified identity lock still present', lobby.includes('ATB_REMATCH_ROSTER_IDENTITY_LOCK captured=true')],
  ['duplicate host projection guard still present', lobby.includes('ATB_REMATCH_ROSTER_ACCOUNT_GUARD preserved=true')]
];
for (const [name, ok] of checks) assert(ok, name);

// Mirror the hardened presentation contract. A local host is never equivalent
// to P1. Without an exact UID-backed member the presentation helper writes no
// lobby row and leaves the authoritative controller's current render intact.
function resolveLocalSlot(snapshot, localUid, localIsHost) {
  if (!snapshot || !Array.isArray(snapshot.members) || !localUid) return -1;
  const matches = snapshot.members
    .filter(m => m && m.active && m.slotIndex >= 0 && m.accountId === localUid)
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
  return -1;
}

assert.strictEqual(resolveLocalSlot({hostAccountId:'V', members:[]}, 'V', true), -1,
  'promoted host with zero UID matches must not fall back to P1');
assert.strictEqual(resolveLocalSlot({hostAccountId:'V', members:[
  {slotIndex:0, active:true, accountId:'G', seatMode:'RemoteHuman', isHost:false},
  {slotIndex:1, active:true, accountId:'V', seatMode:'HostLocal', isHost:true}
]}, 'V', true), 1, 'normal migrated host must resolve P2');
assert.strictEqual(resolveLocalSlot({hostAccountId:'V', members:[
  {slotIndex:0, active:true, accountId:'V', seatMode:'RemoteHuman', isHost:false},
  {slotIndex:1, active:true, accountId:'V', seatMode:'HostLocal', isHost:true}
]}, 'V', true), 1, 'duplicate host projection must resolve HostLocal P2');

console.log(`ATB_PHASE19BB_REMATCH_UNRESOLVED_HOST_FAIL_CLOSED PASS checks=${checks.length + 3} LIVE_UNITY=NOT_RUN TWO_CLIENT=NOT_RUN`);
