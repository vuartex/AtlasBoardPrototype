'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'../../..'),read=p=>fs.readFileSync(path.join(root,p),'utf8');
const career=read('Assets/Project/Scripts/Progression/AtlasBoardRtdbCareerJournal.cs');
const lobby=read('Assets/Project/Scripts/Online/Firebase/AtlasBoardRtdbCasualLobbyTransport.cs');
const setup=read('Assets/Project/Scripts/UI/MatchSetupManager.cs');
const coord=read('Assets/Project/Scripts/Online/AtlasBoardTurnDiceNetworkCoordinator.cs');
const stable=read('Assets/Project/Scripts/Platform/AtlasBoardStableProfileNameView.cs');
const meta=read('Assets/Project/Scripts/Meta/AtlasBoardRtdbMetaEconomy.cs');
let n=0; const ok=(x,m)=>{assert.ok(x,m);n++;};

ok(career.includes('private static JToken IndexedToken'),'career has Firebase dense-array/object index adapter');
ok(career.includes('collection is JArray array'),'career accepts dense RTDB arrays');
ok(career.includes('collection is JObject map'),'career accepts object-form numeric seats');
ok(career.includes('JObject seat = IndexedObject(proof, "seats", slot);'),'claim uses robust seat reader');
ok(!career.includes('proof["seats"]?[slot.ToString()]'),'claim no longer string-indexes JArray');
ok(career.includes('fallback=immutable_final_proof'),'local scene mismatch falls back to immutable proof');
ok(!career.includes('outcome.Reason = "result_mismatch"; return outcome;'),'mutable result UI cannot permanently reject an otherwise verified claim');
ok(!career.includes('stage=archive_wait attempt='),'per-attempt Career retry log spam removed');
ok(career.includes('null,\n                    1);'),'historical recovery performs bounded single archive lookup');
ok(career.includes('JObject existingClaim = ObjectOrNull'),'recovery short-circuits already-created claims');

ok(lobby.includes('human_control_restored=true'),'Waiting membership reconciliation restores Human control');
ok(lobby.includes('seat.ControllerKind = AtlasSeatControllerKind.Human;'),'active human membership restores controller kind');
ok(lobby.includes('seat.ConnectionState = AtlasSeatConnectionState.Connected;'),'active human membership restores connected state');
ok(lobby.includes('seat.SeatMode = expectedMode;'),'active human membership restores Host/Remote seat mode');
ok(lobby.includes('seat.LocalOwnerAccountId = seat.AccountId;'),'human owner UID is restored canonically');

const fn=setup.slice(setup.indexOf('private bool TryResolveOnlineAuthoritativeBotState'),setup.indexOf('private void ResetBoardEconomyForNewMatch'));
ok(fn.indexOf('AtlasBoardLobbyRuntimeBridge') < fn.indexOf('AtlasBoardTurnDiceNetworkCoordinator'),'new-match lobby truth is checked before stale coordinator state');
ok(fn.includes('bool authenticatedHuman ='),'setup explicitly identifies connected authenticated Human');
ok(fn.includes('if (authenticatedHuman)'),'connected Human has a fail-safe no-Bot path');
ok(fn.includes('shouldEnableLocalBot = false;'),'connected Human cannot inherit prior match Bot AI');

ok(coord.includes('lastPawnOwnerSourceLogBySlot'),'pawn source diagnostic is change-throttled');
ok(coord.includes('lastPawnOwnerGuardLogBySlot'),'pawn guard diagnostic is change-throttled');
ok(coord.includes('LogPawnOwnerSourceIfChanged'),'host frame does not print pawn source every publish');
ok(stable.includes('return localHostLobbyAliasAllowed;'),'stable alias authority guard remains');
ok(!stable.includes('ATB_STABLE_LOBBY_ALIAS authoritative=false'),'LateUpdate negative alias spam removed');
ok(meta.includes('goldAwarded"] ?? 0, 0, 50'),'history display respects 50 Gold absolute cap');
ok(meta.includes('ConfirmedMatchGoldKeys'),'confirmed match reward is cached per UID/match for this session');
ok(!meta.includes('stage=already_credited'),'already-paid reward recovery no longer spams the development log');

console.log(`ATB_PHASE19BE_HUMAN_CONTROL_GOLD_PARSER_LOG_SOURCE PASS checks=${n} rtdbSeatArray=true humanControlRestore=true quietDiagnostics=true UNITY_COMPILE=NOT_RUN TWO_CLIENT=NOT_RUN`);
