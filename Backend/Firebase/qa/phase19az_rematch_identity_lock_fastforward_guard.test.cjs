const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '../../..');
const lobby = fs.readFileSync(path.join(root, 'Assets/Project/Scripts/UI/Menu/AtlasBoardPrivateLobbyUIController.cs'), 'utf8');
const qa = fs.readFileSync(path.join(root, 'Assets/Project/Editor/AtlasBoardRematchFastForwardQA.cs'), 'utf8');
const checks = [
  ['rematch identity lock field', lobby.includes('rematchVerifiedIdentityLockActive')],
  ['captures verified roster names', lobby.includes('CaptureRematchVerifiedIdentityLock')],
  ['guards later polling display name', lobby.includes('ApplyRematchVerifiedIdentityGuard(snapshot)')],
  ['only corrects display name', lobby.includes('member.DisplayName = lockedName;')],
  ['name guard diagnostic', lobby.includes('ATB_REMATCH_ROSTER_NAME_GUARD corrected=true')],
  ['identity lock diagnostic', lobby.includes('ATB_REMATCH_ROSTER_IDENTITY_LOCK captured=true')],
  ['final round menu exists', qa.includes('Set Current Match To Final Round')],
  ['finish current turn menu exists', qa.includes('Finish After Current Turn For Rematch QA')],
  ['online follower guard exists', qa.includes('editor_is_online_follower')],
  ['round set to round limit', qa.includes('currentRoundField.SetValue(turnManager, roundLimit)')],
  ['current-turn shortcut precompletes others', qa.includes('completedSlots.Add(state.PlayerSlotIndex)')],
  ['qa log exists', qa.includes('ATB_REMATCH_QA_FAST_FORWARD applied=true')]
];
for (const [name, ok] of checks) {
  if (!ok) throw new Error('FAIL: ' + name);
}
console.log(`ATB_PHASE19AZ_REMATCH_IDENTITY_FASTFORWARD_SOURCE PASS checks=${checks.length} LIVE_UNITY=NOT_RUN TWO_CLIENT=NOT_RUN`);
