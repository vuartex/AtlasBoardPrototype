const fs = require('fs');
const assert = require('assert');

const read = p => fs.readFileSync(p, 'utf8');
const bot = read('Assets/Project/Scripts/Players/BotPlayerController.cs');
const event = read('Assets/Project/Scripts/Cards/EventCardManager.cs');
const lobby = read('Assets/Project/Scripts/UI/Menu/AtlasBoardPrivateLobbyUIController.cs');

let checks = 0;
const ok = (cond, msg) => { assert.ok(cond, msg); checks++; };

ok(event.includes('public bool CanBotContinueEventFor('), 'event continue readiness API missing');
ok(event.includes('effectExecutionCompleted;'), 'event continue readiness must require effect completion');
ok(bot.includes('.CanBotContinueEventFor(playerState)'), 'bot must gate event continue on readiness');
ok(!/TryStartEventContinue\(\)[\s\S]{0,500}HasPendingEventFor\(playerState\)/.test(bot), 'bot must not start event continue merely because event is pending');
ok(lobby.includes('AtlasLobbySnapshot previousSnapshot = backendSnapshot;'), 'lobby must retain previous snapshot for migration reconciliation');
ok(lobby.includes('ATB_LOBBY_SLOT_IDENTITY_RECOVERED'), 'migration identity recovery diagnostic missing');
ok(lobby.includes('source=previous_snapshot'), 'previous authoritative seat identity must be recoverable');
ok(lobby.includes('member.SeatMode == AtlasLobbySeatMode.HostLocal'), 'host slot resolver must prefer HostLocal seat');
ok(lobby.includes('Never overwrite a non-host seat with a duplicated host UID'), 'duplicate host projection guard missing');
ok(lobby.includes('synchronizedRematchContinuity'), 'rematch identity continuity guard missing');

console.log(`ATB_PHASE19AX_SOURCE_GUARD PASS checks=${checks} LIVE_UNITY=NOT_RUN TWO_CLIENT=NOT_RUN`);
