'use strict';

const fs = require('fs');
const path = require('path');
const assert = require('assert');

const root = path.resolve(__dirname, '..', '..', '..');
const file = path.join(root, 'Assets', 'Project', 'Scripts', 'UI', 'Menu', 'AtlasBoardPrivateLobbyUIController.cs');
const src = fs.readFileSync(file, 'utf8');

const checks = [
  ['rematch verification is scheduled', 'BeginRematchRosterVerification();'],
  ['authoritative snapshot is re-read', 'await runtimeBridge.GetSnapshotAsync();'],
  ['stability guard exists', 'IsAuthoritativeRosterStable('],
  ['duplicate UID is rejected', 'reason = "duplicate_human_uid";'],
  ['host projection count is checked', 'host_projection_count_'],
  ['stable roster rebuild exists', 'RebuildRosterFromAuthoritativeSnapshot(result.Snapshot);'],
  ['seat identity caches are cleared', 'seatIdentityAccountCache[i] = string.Empty;'],
  ['rematch refresh is bounded', 'int[] delaysMs = { 350, 650, 1000, 1500 };'],
  ['refresh diagnostic exists', 'ATB_REMATCH_ROSTER_REFRESH'],
  ['navigation cancellation exists', 'expectedNavigationEpoch != navigationEpoch'],
  ['room cancellation exists', '!roomActive'],
  ['host rows repaint after rebuild', 'RefreshHostSeatRows();']
];

for (const [name, needle] of checks) {
  assert(src.includes(needle), `${name}: missing ${needle}`);
}

console.log(`ATB_PHASE19AY_REMATCH_ROSTER_SOURCE PASS checks=${checks.length} LIVE_UNITY=NOT_RUN TWO_CLIENT=NOT_RUN`);
