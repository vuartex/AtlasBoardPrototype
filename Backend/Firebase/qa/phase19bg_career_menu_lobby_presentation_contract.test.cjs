const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '../../..');
const read = (p) => fs.readFileSync(path.join(root, p), 'utf8');
const progression = read('Assets/Project/Scripts/UI/Progression/AtlasBoardProgressionUI.cs');
const menu = read('Assets/Project/Scripts/UI/Menu/AtlasBoardMainMenuController.cs');
const lobby = read('Assets/Project/Scripts/UI/Menu/AtlasBoardPrivateLobbyUIController.cs');

let checks = 0;
function check(condition, message) {
  checks++;
  if (!condition) throw new Error(message);
}

check(progression.includes('new Vector2(160f, 30f)'), 'Player result label must have localization-safe width');
check(progression.includes('result.enableAutoSizing = true;'), 'Player result label must autosize');
check(progression.includes('result.fontSizeMin = 7f;'), 'Player result label must support long localized words');
check(progression.includes('result.textWrappingMode = TextWrappingModes.NoWrap;'), 'Player result must stay on one line');
check(progression.includes('new Vector2(-50f, 32f)') && progression.includes('new Vector2(190f, 30f)'), 'Player identity must remain clear of avatar/result columns');
check(progression.includes('"NIEDERLAGE"') === false, 'Result text must still come from localization, not hard-coded one-language copy');
check(progression.includes('"MATCH", "MAÇ", "PARTIDA", "PARTIE"'), 'Details summary must use localized field labels');
check(progression.includes('"GOLD", "ALTIN", "ORO", "OR"'), 'Details summary must show localized Gold label');
check(progression.includes('match.goldAwarded'), 'Details summary must keep earned Gold');
check(progression.includes('detailsBodyText.textWrappingMode = TextWrappingModes.Normal;'), 'Details summary must wrap cleanly');

for (const caption of ['ANA MENÜ','MENÚ PRINCIPAL','MENU PRINCIPAL','HAUPTMENÜ','메인 메뉴','ГЛАВНОЕ МЕНЮ','MAIN MENU']) {
  check(menu.includes(caption), `Missing localized main-menu caption: ${caption}`);
}
check(menu.includes('LocalizedMainMenuTip'), 'Main Menu must show localized game information');
check(menu.includes('(mainMenuFooterTipIndex + 1) % 4'), 'Main Menu game information should rotate across visits');
check(!menu.includes('Prototype • Main Menu v1'), 'Runtime Main Menu must not expose prototype/version copy');

check(lobby.includes('SetText(\n            revisionText,\n            string.Empty);'), 'Settings revision must be hidden from customer Room Code header');
check(!lobby.includes('BACKEND SNAPSHOT CONNECTED'), 'Backend snapshot engineering copy must be hidden from runtime lobby');
check(!lobby.includes('HOST CAN START • OPEN SEATS BECOME BOTS'), 'Host implementation note must not crowd Room Code header');
check(!lobby.includes('ROSTER READY • START AVAILABLE'), 'Roster engineering status must not crowd Room Code header');
check(lobby.includes('AtlasRoomLifecycleState.Starting') && lobby.includes('"STARTING..."'), 'Useful Starting state must remain visible');

console.log(`ATB_PHASE19BG_CAREER_MENU_LOBBY_PRESENTATION_SOURCE PASS checks=${checks} locales=7 rotatingTips=4`);
