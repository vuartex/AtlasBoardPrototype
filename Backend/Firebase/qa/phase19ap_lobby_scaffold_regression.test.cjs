// This is a SOURCE contract only: not a Unity render/dual-client PASS.
'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../../..');
const get = p => fs.readFileSync(path.join(root,p),'utf8');
const menu = get('Assets/Project/Scripts/UI/Menu/AtlasBoardMainMenuController.cs');
const builder = get('Assets/Project/Editor/AtlasBoardPrivateLobbyUIV1Setup.cs');
const scaffold = get('Assets/Project/Editor/AtlasBoardMainMenuV1Setup.cs');
let n=0;
function verify(ok,label){assert.ok(ok,label);n++;}
verify(!menu.includes('SuppressRetiredLobbyChildren'), 'removed blanket-hiding regression');
verify(menu.includes('HasModernLobbyScaffold()'), 'guard requires modern online components');
verify(menu.includes('RestoreOnlineLobbyScaffold()'), 'restores required scene children');
for (const x of ['PrivateOnlineRoot','Panel_GameSettings','Panel_Players','Button_Back','Button_StartMatch']) {
 verify(menu.includes('"'+x+'"'), 'menu requires '+x);
}
verify(scaffold.includes('"Panel_GameSettings"'), 'settings panel is a real Lobby sibling');
verify(scaffold.includes('"Panel_Players"'), 'player cards are real Lobby siblings');
verify(builder.includes('CreateStretchRoot(') && builder.includes('RootName'), 'online root is additive, not replacement');
verify(menu.includes('private_controller_missing=true legacy_fallback=false'), 'private fallback stays blocked');
verify(menu.includes('public_controller_missing=true legacy_fallback=false'), 'public fallback stays blocked');
console.log('ATB_PHASE19AP_LOBBY_SOURCE PASS checks='+n+' UNITY_RENDER=NOT_RUN TWO_CLIENT=NOT_RUN');
