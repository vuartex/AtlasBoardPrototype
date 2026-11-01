'use strict';
// Read-only source safety contract. No Firebase writes, no account/UID changes.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../../..');
const load = p => fs.readFileSync(path.join(root, p), 'utf8');
const meta = load('Assets/Project/Scripts/Meta/AtlasBoardRtdbMetaEconomy.cs');
const mirror = load('Assets/Project/Scripts/Online/Firebase/AtlasBoardRtdbUserProfileMirror.cs');
const progression = load('Assets/Project/Scripts/UI/Progression/AtlasBoardProgressionUI.cs');
const turn = load('Assets/Project/Scripts/Core/TurnManager.cs');
const coord = load('Assets/Project/Scripts/Online/AtlasBoardTurnDiceNetworkCoordinator.cs');
const transport = load('Assets/Project/Scripts/Online/Firebase/AtlasBoardLobbyRuntimeBridge.cs');
const rules = JSON.parse(load('Backend/Firebase/database.rules.json'));
let checks = 0;
function yes(condition, message) { assert.ok(condition, message); checks++; }
function section(src,start,end) { const p=src.indexOf(start);assert.ok(p>=0,start);const q=end?src.indexOf(end,p+start.length):src.length;return src.slice(p,q<0?src.length:q); }
let s=section(meta,'public static string CurrentUid','public static int Price');
yes(s.includes('IsLiveGuestQaSession') && s.includes('return string.Empty;'),'Live Guest never exposes cached native Firebase UID to wallet');
s=section(meta,'private static async Task<Identity> ResolveIdentityAsync','private static async Task<bool> IsDeveloperTesterAsync');
yes(s.includes('IsLiveGuestQaSession') && s.includes('return null;'),'Live Guest cannot read or mutate SDK wallet');
s=section(meta,'public static async Task<AtlasBoardMetaEconomySnapshot> LoadAsync','public static async Task<AtlasBoardMetaEconomyOperationResult> GrantDevAsync');
for (const c of ['cachedMapUid = ""','CachedOwnedMaps.Clear()','cachedPawnOwnerUid = ""','CachedOwnedPawnIds.Clear()','cachedDiceId = ""','cachedEmoteUid = ""','CachedPaidEmotes.Clear()','snapshot.WalletVerified = false','snapshot.Gold = 0','snapshot.AtlasCoin = 0','native_sdk_wallet=not_read','snapshot.IsGuest = true']) yes(s.includes(c),'Guest wallet missing isolation: '+c);
s=section(meta,'public static bool IsRegistered','public static string CurrentUid');
yes((s.match(/IsLiveGuestQaSession/g)||[]).length===2,'Live Guest cannot appear Firebase registered/Steam approved');
yes(mirror.includes('if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)\n            return Task.FromResult(false);'),'Live Guest cannot mirror a native UID profile');
yes(transport.includes('EnsureLiveGuestQaIdentityAsync()'),'Existing separate REST guest Auth retained');
yes(transport.includes('LiveGuestCredentialsFile'),'Guest DPAPI persistence retained');
for(const id of ['"Turkey"','"Colorado"','"USA"','"Germany"','"Mexico"']) yes(section(progression,'private List<string> BuildSelectableMapIds()', 'private AtlasBoardCareerStats GetDisplayedStats()').includes(id),'Career filter missing '+id);
yes(progression.includes('id.StartsWith("map_", StringComparison.Ordinal)'),'Canonical map-ID aliases');
for(const id of ['map_germany','map_mexico']) yes(progression.includes(`AtlasBoardShopContentDraft.LocalizedName("${id}"`),'localized premium map '+id);
yes(progression.includes('return selected != null && selected.stats != null'),'Empty premium-map stats are zero');
yes(coord.includes('ATB_LIVE_GUEST_SEAT_BOUND'),'Guest seat diagnostic');
yes(turn.includes('ATB_LIVE_GUEST_ROLL_DIAG'),'Follower button diagnostic');
yes(coord.includes('ATB_LIVE_GUEST_ROLL_INTENT'),'Guest roll network diagnostic');
yes(turn.includes('CanFollowerRequestRoll(activePlayer)'), 'Original follower roll gate unchanged');
yes(coord.includes('TryRequestAuthoritativeNetworkRoll('),'Host-authoritative roll remains intact');
yes(rules.rules.users.$uid.metaEconomy['.write'].includes("'MATCH_REWARD'"),'Existing Gold security retained');
console.log(`ATB_PHASE19AJ_SOURCE_QA PASS checks=${checks} guestWallet=isolated maps=5 roll=diagnostic_only rules=unchanged_live unity=NOT_RUN twoClient=NOT_RUN`);
