'use strict';
// Read-only source compatibility audit. It does not run Unity's C# compiler.
const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '../../..');
const get = p => fs.readFileSync(path.join(root, p), 'utf8');
let total = 0;
function assert(ok, explanation) {
  if (!ok) throw new Error('ATB_PHASE19J_COMPILE_COMPAT_QA FAIL ' + explanation);
  ++total;
}
const prefix = 'Assets/Project/Scripts/';
const pager = get(prefix+'Online/Firebase/AtlasBoardPublicLobbyPagingClient.cs');
const bridge = get(prefix+'Online/Firebase/AtlasBoardLobbyRuntimeBridge.cs');
const board = get(prefix+'Board/AtlasBoardBoardThemeVisuals.cs');
const picker = get(prefix+'UI/Menu/AtlasBoardLobbyBoardThemePicker.cs');
const lobby = get(prefix+'UI/Menu/AtlasBoardPrivateLobbyUIController.cs');
const career = get(prefix+'UI/Progression/AtlasBoardProgressionUI.cs');
const wallet = get(prefix+'Meta/AtlasBoardRtdbMetaEconomy.cs');
assert(bridge.includes('Task<bool> EnsureOnlineIdentityAsync()'), 'canonical bridge takes zero arguments');
assert(pager.includes('runtimeBridge.EnsureOnlineIdentityAsync()'), 'pager must call canonical bridge');
assert(!pager.includes('allowAndroidGuestSignIn:'), 'obsolete named argument must not be used');
assert(board.includes('FindObjectsByType<Renderer>()'), 'Unity 6 renderer API');
assert(!board.includes('FindObjectsSortMode'), 'obsolete renderer sort mode removed');
assert(picker.includes('textWrappingMode=TextWrappingModes.NoWrap'), 'TMP dropdown label nowrap');
assert(!picker.includes('enableWordWrapping'), 'obsolete TMP field removed from dropdown');
assert(lobby.includes('UnityEngine.Object.FindAnyObjectByType<Canvas>()'), 'Unity 6 Canvas fallback');
assert(!lobby.includes('UnityEngine.Object.FindFirstObjectByType<Canvas>()'), 'obsolete Canvas API removed');
assert(career.includes('info.textWrappingMode = TextWrappingModes.Normal;'), 'career info wrap');
assert(career.includes('status.textWrappingMode = TextWrappingModes.Normal;'), 'career status wrap');
assert(!career.includes('info.enableWordWrapping') && !career.includes('status.enableWordWrapping'), 'obsolete career TMP props removed');
assert(/\[Serializable\]\s*private sealed class Inventory\b/.test(wallet), 'nested economy inventory is serializable');
assert(wallet.includes('public Frames profile_frames = new Frames();'), 'profile inventory preserved');
assert(wallet.includes('public Boards boards = new Boards();'), 'board inventory preserved');
assert(board.includes('MaterialPropertyBlock'), 'existing board material applier preserved');
assert(lobby.includes('EnsureBoardThemePicker'), 'existing More Settings board control preserved');
console.log('ATB_PHASE19J_COMPILE_COMPAT_QA PASS assertions='+total+' sourceOnly=true UnityEditorCompile=OPEN');
