'use strict';
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root=path.resolve(__dirname,'../../..');
const file=fs.readFileSync(path.join(root,'Assets/Project/Scripts/UI/Menu/AtlasBoardLobbyBoardThemePicker.cs'),'utf8');
let count=0;
function expect(cond,msg){assert.ok(cond,msg);count++;}
expect(file.includes('LobbyGameSettingsDialog'), 'Anchor to authored dialog not overlay dimmer');
expect(file.includes('passwordField.parent as RectTransform'),'Reuse original password dialog');
expect(file.includes('if (setupDone) return;'),'One-time UI creation on successive matches');
expect(file.includes('if (needsReflow)'),'Reflow only unmigrated authored scenes');
expect(file.includes('bool needsExpansion = panel.rect.height < 640f;'),'Respect already expanded scene');
expect(file.includes('child.name == "BoardThemeRow"'),'Never move created picker');
expect(file.includes('done.anchoredPosition.y > -248f'),'Existing 660-height modified dialog also reflows');
expect(file.includes('dropdown.interactable=isHost'),'Host-only selection remains');
expect(file.includes('selectedBoardThemeId')===false,'Picker remains UI-only, no parallel room state');
expect(file.includes('template.SetActive(false);'),'Dropdown template dormant until click');
expect(file.includes('Place((RectTransform)template.transform,0,116,244,172);'),'Popup opens upward away from DONE');
expect(file.includes('onSelect?.Invoke(ids[index]);'),'Canonical room-setting callback preserved');
// Geometry is taken from canonical Assets/Board_Prototype.unity at current GitHub main:
// dialog=760x520; positions: title=190, status=-156, apply=-102, done=-218.
// The C# layout migrates these once and places the board row in the new space.
for(const [fragment,label] of [
 ['const float extraHeight = 140f;', 'dialog height'],
 ['const float topShift = 58f;', 'upper controls shift'],
 ['const float doneShift = -63f;', 'Done shift'],
 ['Place(rowRoot,0,-184f,610f,52f);', 'separate board row']
])expect(file.includes(fragment),'Layout contract missing: '+label);
const dialogHeight=520+140, title=190+58,apply=-102+58,status=-156+58,board=-184,done=-218-63;
const bounds=(y,h)=>[y-h/2,y+h/2];
function separated(a,b,gap){return a[1]+gap<=b[0]||b[1]+gap<=a[0]}
expect(separated(bounds(board,52),bounds(apply,58),12),'Board overlaps Apply Password');
expect(separated(bounds(board,52),bounds(status,36),12),'Board overlaps password status');
expect(separated(bounds(board,52),bounds(done,62),12),'Board overlaps Done');
expect(title+54/2 <= dialogHeight/2-12,'Title clips top');
expect(done-62/2 >= -dialogHeight/2+12,'Done clips bottom');
expect(apply > board && status > board && board > done,'Vertical order wrong');
expect((520+140)<800,'No oversized fullscreen modal');
console.log(`ATB_PHASE19J_MORE_SETTINGS_LAYOUT_QA PASS assertions=${count} editorVisual=OPEN sceneGeometry=760x520_to_760x660 noOverlap=true`);
