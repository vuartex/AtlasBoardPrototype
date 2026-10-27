'use strict';
// READ-ONLY SOURCE CONTRACT QA. This does not replace Firebase Emulator
// rule evaluation or Unity compile/runtime acceptance.
const fs=require('fs');
const path=require('path');
const base=path.resolve(__dirname,'../../..');
const get=p=>fs.readFileSync(path.join(base,p),'utf8');
let n=0;
function check(ok,message){if(!ok)throw new Error('FAIL '+message);n++;}
const wallet=get('Assets/Project/Scripts/Meta/AtlasBoardRtdbMetaEconomy.cs');
const models=get('Assets/Project/Scripts/Meta/AtlasBoardMetaEconomyModels.cs');
const store=get('Assets/Project/Scripts/UI/Meta/AtlasBoardMetaEconomyUI.cs');
const draft=get('Assets/Project/Scripts/Meta/AtlasBoardShopContentDraft.cs');
const career=get('Assets/Project/Scripts/UI/Progression/AtlasBoardProgressionUI.cs');
const lobby=get('Assets/Project/Scripts/UI/Menu/AtlasBoardPrivateLobbyUIController.cs');
const picker=get('Assets/Project/Scripts/UI/Menu/AtlasBoardLobbyBoardThemePicker.cs');
const visual=get('Assets/Project/Scripts/Board/AtlasBoardBoardThemeVisuals.cs');
const room=get('Assets/Project/Scripts/Online/Firebase/AtlasBoardRtdbCasualLobbyTransport.cs');
const bridge=get('Assets/Project/Scripts/Online/Firebase/AtlasBoardLobbyRuntimeBridge.cs');
const rule=JSON.parse(get('Backend/Firebase/database.rules.json')).rules.users['$uid'].metaEconomy;
const boardPrices={board_midnight_azure:1200,board_ember_maple:1500,board_arctic_aurora:1700};
for (const [id,price] of Object.entries(boardPrices)){
 check(wallet.includes(id),'wallet id '+id);
 check(draft.includes(id),'draft id '+id);
 check(rule['.write'].includes(`=== '${id}'`),'strict entitlement price '+id);
 check(rule['.write'].includes(`? ${price} : 0`),'strict Gold price '+id);
 check(Boolean(rule.inventory.boards[id]),'board property validation '+id);
}
check(rule['.write'].includes('DEV_GRANT'),'old grant preserved');
check(rule['.write'].includes('DAILY'),'daily preserved');
check(rule['.write'].includes('PURCHASE'),'purchases preserved');
check(rule['.write'].includes('EQUIP_BOARD'),'board equip transition');
check(rule['.write'].includes('RESET_PURCHASES'),'reset/refund transition');
check(rule.equipped.boardId['.validate'].includes('inventory/boards'),'equip only owned');
check(wallet.includes('state.ETag'),'atomic ETag in purchase path');
check(wallet.includes('doc.inventory.boards.SetOwned(itemId, true)'),'purchase grants ownership');
check(wallet.includes('doc.inventory.boards.RefundAmount()'),'reset refunds boards');
check(models.includes('EquippedBoardId'),'equipped board in snapshot');
check(store.includes('ItemBuy_'),'single priced checkout row for confirmed board');
check(store.includes('board_theme'),'board store category');
check(career.includes('ApplyOwnedBoardAsync'),'inventory board equip');
check(career.includes('EquipBoardAsync'),'server equip');
check(picker.includes('BoardThemeDropdown'),'separate selector');
check(picker.includes('typeof(TMP_Dropdown)'),'true dropdown');
check(picker.includes('dropdown.SetValueWithoutNotify'),'no callback loop');
check(lobby.includes('EnsureBoardThemePicker'),'existing More Settings overlay');
check(lobby.includes('selection.BoardThemeId = selectedBoardThemeId'),'host selection serialization');
check(room.includes('snap.Settings.BoardThemeId=req.boardThemeId') || room.includes('snap.Settings.BoardThemeId=string.IsNullOrEmpty(req.boardThemeId)'),'RTDB authoritative snapshot field');
check(room.includes('inventory/boards/'),'host-owned selection verified');
check(bridge.includes('boardThemeId = selection.BoardThemeId'),'bridge request field');
check(bridge.includes('BoardThemeId ='),'bridge incoming snapshot');
check(visual.includes('MaterialPropertyBlock'),'non-destructive board rendering');
check(visual.includes('ApplySelection("default_board")') || lobby.includes('ApplySelection("default_board")'),'return to Classic');
check(draft.match(/new DraftRow\("frame_/g).length===10,'10 profile entries preserved');
check(draft.match(/new DraftRow\("board_/g).length===3,'exactly three board entries');
for(let i=0;i<7;i++)check(draft.includes('board_midnight_azure')&&draft.includes('board_ember_maple')&&draft.includes('board_arctic_aurora'),'seven language source exists '+i);
console.log('ATB_PHASE19J_SOURCE_QA PASS assertions='+n+' mode=read_only static=true UnityCompile=OPEN FirebaseEmulator=OPEN');
