'use strict';
// REAL LOCAL FIREBASE AUTH + RTDB EMULATOR ACHIEVEMENT AUDIT.
// Never targets production. Intentionally performs many authenticated HTTP
// operations against the emulator to validate durable Career evidence.
// Run from Backend/Firebase with:
// firebase.cmd emulators:exec --only auth,database --project demo-atlasboard "node qa/phase19bi_achievement_emulator_e2e.test.cjs"

const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');

if (!process.env.FIREBASE_DATABASE_EMULATOR_HOST ||
    !process.env.FIREBASE_AUTH_EMULATOR_HOST ||
    process.env.GCLOUD_PROJECT !== 'demo-atlasboard') {
  throw new Error('EMULATOR_ONLY: demo-atlasboard auth+database required; production is forbidden.');
}

const base = 'http://127.0.0.1:9000';
const authBase = 'http://127.0.0.1:9099';
const ns = 'demo-atlasboard-default-rtdb';
const repoRoot = path.resolve(__dirname, '../../..');
const rules = JSON.parse(fs.readFileSync(path.join(__dirname, '../database.rules.json'), 'utf8')).rules;

let checks = 0;
let httpOps = 0;
let seq = 0;
const startedAt = Date.now();

function xid() { return (++seq).toString(16).padStart(32, '0'); }
function ms() { return Date.now(); }
function hash(s) { return crypto.createHash('sha256').update(s).digest('hex'); }
function ok(condition, label) { assert.ok(condition, label); checks++; }

async function signup(label, anonymous = false) {
  const body = anonymous
    ? { returnSecureToken: true }
    : { email: `${label}@atlasboard.invalid`, password: 'Atlas-Emulator-Local-Only!12', returnSecureToken: true };
  const r = await fetch(`${authBase}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=local-demo`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body)
  });
  httpOps++;
  const x = await r.json();
  assert(r.ok && x.idToken && x.localId, `signup ${label}: HTTP ${r.status}`);
  return { uid: x.localId, token: x.idToken, email: body.email || '' };
}

async function db(method, where, user, body, etag) {
  const token = user === 'owner' ? null : user.token;
  const endpoint = `${base}/${where}.json?ns=${ns}${token ? `&auth=${encodeURIComponent(token)}` : ''}`;
  const headers = { 'Content-Type': 'application/json' };
  if (user === 'owner') headers.Authorization = 'Bearer owner';
  if (etag) headers['if-match'] = etag;
  if (method === 'GET') headers['X-Firebase-ETag'] = 'true';
  const r = await fetch(endpoint, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: AbortSignal.timeout(12000)
  });
  httpOps++;
  return { status: r.status, body: await r.text(), etag: r.headers.get('etag') };
}

function expectAllowed(result, label) {
  assert(result.status >= 200 && result.status < 300,
    `${label}: expected allow; HTTP ${result.status} ${result.body.slice(0, 240)}`);
  checks++;
}
function expectDenied(result, label) {
  assert(result.status === 401 || result.status === 403,
    `${label}: expected denied; HTTP ${result.status} ${result.body.slice(0, 240)}`);
  checks++;
}

function defaults(node) {
  const out = {};
  for (const [key, value] of Object.entries(node)) {
    if (key.startsWith('.') || key.startsWith('$')) continue;
    if (value && typeof value === 'object') {
      const children = Object.keys(value).filter(x => !x.startsWith('.') && !x.startsWith('$'));
      if (children.length) out[key] = defaults(value);
      else if (value['.validate']?.includes('isBoolean')) out[key] = false;
      else if (value['.validate']?.includes('isNumber')) out[key] = 0;
      else out[key] = '';
    }
  }
  return out;
}

function profile(user, name) {
  const at = ms();
  return {
    userId: user.uid,
    playerId: `ATB-${hash(user.uid).slice(0, 20).toUpperCase()}`,
    email: user.email,
    displayName: name,
    createdAt: at - 120000,
    updatedAt: at,
    lastDisplayNameChangeAt: at - 120000,
    general: { schemaVersion: 1, accountType: user.email ? 'registered' : 'guest', language: 'en', countryCode: 'US', lastSeenAt: at }
  };
}

function startWallet() {
  const econ = rules.users.$uid.metaEconomy;
  const inv = defaults(econ.inventory);
  return {
    schemaVersion: 1,
    wallet: { gold: 0, atlasCoin: 0, starterGrantClaimed: false },
    inventory: inv,
    equipped: { profileFrameId: '', boardId: '', diceId: '' },
    dailyReward: { lastClaimAt: 0 },
    dev: { grantCount: 0, resetCount: 0 },
    operation: { id: xid(), type: 'INIT', itemId: '', atMs: ms() },
    rewardHistory: ''
  };
}

function advance(wallet, type, itemId) {
  const x = structuredClone(wallet);
  x.operation = {
    id: xid(), type, itemId: itemId || '',
    atMs: Math.max(ms(), Number(wallet.operation.atMs || 0) + 1)
  };
  return x;
}

function gold(kind, rounds) {
  return Math.min(50, Math.max(0, rounds) + (kind === 'win' ? 25 : kind === 'tie' ? 15 : 10));
}
function xp(kind, rounds, properties, developments) {
  return 25 + (kind === 'win' ? 50 : kind === 'tie' ? 20 : 0) +
    Math.min(100, Math.max(0, rounds) * 3) +
    Math.min(50, Math.max(0, properties) * 5) +
    Math.min(75, Math.max(0, developments) * 3);
}

function parseAchievementCatalog() {
  const src = fs.readFileSync(path.join(repoRoot,
    'Assets/Project/Scripts/Progression/AtlasBoardCareerAchievementDisplay.cs'), 'utf8');
  const found = [...src.matchAll(/D\("([^"]+)",\s*(\d+),/g)]
    .map(m => ({ id: m[1], target: Number(m[2]) }));
  assert.equal(found.length, 14, `expected 14 achievements, found ${found.length}`);
  checks++;
  return found;
}

const scenarioPlan = [
  { result: 'win',  mapId: 'map_turkey',   rounds: 10, turns: 10, dice: 9, doubles: 1, properties: 3, developments: 2, netWorth: 1600, cash: 900 },
  { result: 'win',  mapId: 'map_colorado', rounds: 10, turns: 10, dice: 9, doubles: 1, properties: 3, developments: 2, netWorth: 1700, cash: 950 },
  { result: 'win',  mapId: 'map_usa',      rounds: 10, turns: 10, dice: 9, doubles: 1, properties: 3, developments: 2, netWorth: 1800, cash: 1000 },
  { result: 'tie',  mapId: 'map_turkey',   rounds: 10, turns: 10, dice: 9, doubles: 1, properties: 3, developments: 2, netWorth: 1900, cash: 1050 },
  { result: 'loss', mapId: 'map_colorado', rounds: 10, turns: 10, dice: 9, doubles: 1, properties: 3, developments: 2, netWorth: 2000, cash: 1100 },
  { result: 'win',  mapId: 'map_usa',      rounds: 10, turns: 10, dice: 9, doubles: 1, properties: 3, developments: 2, netWorth: 2100, cash: 1150 },
  { result: 'win',  mapId: 'map_turkey',   rounds: 10, turns: 10, dice: 9, doubles: 1, properties: 3, developments: 2, netWorth: 2200, cash: 1200 },
  { result: 'win',  mapId: 'map_colorado', rounds: 10, turns: 10, dice: 9, doubles: 1, properties: 3, developments: 2, netWorth: 2300, cash: 1250 },
  { result: 'win',  mapId: 'map_usa',      rounds: 10, turns: 10, dice: 9, doubles: 1, properties: 3, developments: 2, netWorth: 2400, cash: 1300 },
  { result: 'win',  mapId: 'map_turkey',   rounds: 10, turns: 10, dice: 9, doubles: 1, properties: 3, developments: 2, netWorth: 6000, cash: 3600 },
  { result: 'win',  mapId: 'map_colorado', rounds: 10, turns: 10, dice: 9, doubles: 1, properties: 3, developments: 2, netWorth: 2700, cash: 1400 },
  { result: 'win',  mapId: 'map_usa',      rounds: 10, turns: 10, dice: 9, doubles: 1, properties: 3, developments: 2, netWorth: 2900, cash: 1500 }
];

function expectedAggregate(plan) {
  const stats = {
    matchesPlayed: 0, wins: 0, ties: 0, losses: 0,
    bestWinStreak: 0, currentWinStreak: 0,
    diceRolls: 0, doublesRolled: 0,
    propertiesAtFinishTotal: 0, developmentLevelsAtFinishTotal: 0,
    highestNetWorth: 0, mapTurkey: 0, mapColorado: 0, mapUsa: 0,
    totalXp: 0, totalGold: 0
  };
  let streak = 0;
  for (const s of plan) {
    stats.matchesPlayed++;
    if (s.result === 'win') { stats.wins++; streak++; stats.bestWinStreak = Math.max(stats.bestWinStreak, streak); }
    else { if (s.result === 'tie') stats.ties++; else stats.losses++; streak = 0; }
    stats.diceRolls += s.dice;
    stats.doublesRolled += s.doubles;
    stats.propertiesAtFinishTotal += s.properties;
    stats.developmentLevelsAtFinishTotal += s.developments;
    stats.highestNetWorth = Math.max(stats.highestNetWorth, s.netWorth);
    if (s.mapId.includes('turkey')) stats.mapTurkey++;
    else if (s.mapId.includes('colorado')) stats.mapColorado++;
    else if (s.mapId.includes('usa')) stats.mapUsa++;
    stats.totalXp += xp(s.result, s.rounds, s.properties, s.developments);
    stats.totalGold += gold(s.result, s.rounds);
  }
  stats.currentWinStreak = streak;
  return stats;
}

function achievementProgress(stats) {
  return {
    first_match: stats.matchesPlayed,
    first_win: stats.wins,
    win_streak_3: stats.bestWinStreak,
    matches_10: stats.matchesPlayed,
    wins_10: stats.wins,
    dice_100: stats.diceRolls,
    doubles_10: stats.doublesRolled,
    properties_25: stats.propertiesAtFinishTotal,
    builder_20: stats.developmentLevelsAtFinishTotal,
    networth_5000: stats.highestNetWorth,
    turkey_explorer: stats.mapTurkey,
    colorado_explorer: stats.mapColorado,
    usa_explorer: stats.mapUsa,
    atlas_traveler: (stats.mapTurkey > 0 ? 1 : 0) + (stats.mapColorado > 0 ? 1 : 0) + (stats.mapUsa > 0 ? 1 : 0)
  };
}

async function createCompletedMatch({ host, peer, stranger, index, scenario }) {
  const mid = `ach_e2e_${String(index + 1).padStart(2, '0')}_${xid()}`;
  const room = `ach_room_${String(index + 1).padStart(2, '0')}_${xid()}`;
  const now = ms();
  const revision = 100 + index;

  const roomMembers = {
    [host.uid]: { uid: host.uid, active: true, role: 'host', joinedAt: now - 90000, displayName: 'Achievement Alice' },
    [peer.uid]: { uid: peer.uid, active: true, role: 'remoteHuman', joinedAt: now - 90000, displayName: 'Witness Bob' }
  };
  const seatWire = { seats: [
    { uid: host.uid, ownerUid: host.uid, seatId: 'seat_0', mode: 'hostlocal', name: 'Achievement Alice', controller: 'human', connection: 'connected', slotIndex: 0, host: true, afkLockedOut: false, reconnectExpiresAt: 0, disconnectedAt: 0, removalReason: '' },
    { uid: peer.uid, ownerUid: peer.uid, seatId: 'seat_1', mode: 'remotehuman', name: 'Witness Bob', controller: 'human', connection: 'connected', slotIndex: 1, host: false, afkLockedOut: false, reconnectExpiresAt: 0, disconnectedAt: 0, removalReason: '' }
  ]};

  const match = {
    matchId: mid, lobbyId: room, hostUid: host.uid,
    status: 'active', phase: 'playing', createdAt: now - 90000,
    updatedAt: now, hostHeartbeatAt: now, revision: revision - 1,
    eventSequence: revision - 1, authorityEpoch: 1, networkSchemaVersion: 1,
    seatsJson: JSON.stringify(seatWire), snapshotJson: '{}'
  };
  expectAllowed(await db('PUT', `roomMembers/${room}`, 'owner', roomMembers), `${mid}: seed room`);
  expectAllowed(await db('PUT', `matchState/${mid}`, 'owner', match), `${mid}: seed active match`);

  const pHost = { uid: host.uid, matchId: mid, lobbyId: room, slotIndex: 0, role: 'host', atMs: ms() };
  const pPeer = { uid: peer.uid, matchId: mid, lobbyId: room, slotIndex: 1, role: 'remoteHuman', atMs: ms() };
  expectAllowed(await db('PUT', `careerParticipation/${host.uid}/${mid}`, host, pHost), `${mid}: host participation`);
  expectAllowed(await db('PUT', `careerParticipation/${peer.uid}/${mid}`, peer, pPeer), `${mid}: peer participation`);
  expectDenied(await db('PUT', `careerParticipation/${host.uid}/${mid}`, peer, pHost), `${mid}: foreign participation denied`);

  const hostWin = scenario.result === 'win';
  const hostTie = scenario.result === 'tie';
  const peerResult = hostTie ? 'tie' : (hostWin ? 'loss' : 'win');
  const result = {
    valid: true,
    participating: [true, true, false, false],
    winnerSlots: hostTie ? [0, 1] : (hostWin ? [0] : [1]),
    netWorth: [scenario.netWorth, 1500, 0, 0],
    cash: [scenario.cash, 800, 0, 0],
    propertyCount: [scenario.properties, 2, 0, 0],
    propertyValue: [scenario.properties * 200, 400, 0, 0],
    developmentLevels: [scenario.developments, 1, 0, 0],
    developmentValue: [scenario.developments * 150, 150, 0, 0],
    bankrupt: [false, false, false, false]
  };
  const diceRolls = [scenario.dice, 6, 0, 0];
  const doubles = [scenario.doubles, 0, 0, 0];
  const frame = {
    phase: 'match_complete', currentRound: scenario.rounds, completedTurns: scenario.turns,
    roundLimit: 30, matchResult: result, careerDiceRolls: diceRolls, careerDoublesRolled: doubles
  };
  const snapshotJson = JSON.stringify(frame);
  const seatsJson = JSON.stringify(seatWire);
  expectAllowed(await db('PATCH', `matchState/${mid}`, 'owner', {
    status: 'complete', phase: 'match_complete', revision, eventSequence: revision,
    snapshotJson, seatsJson, updatedAt: ms()
  }), `${mid}: canonical final frame`);

  const archive = {
    matchId: mid, lobbyId: room, hostUid: host.uid, snapshotJson, seatsJson,
    digest: hash(snapshotJson), mapId: scenario.mapId, themeId: 'classic_table',
    roundLimit: 30, revision, eventSequence: revision, authorityEpoch: 1,
    networkSchemaVersion: 1, createdAt: match.createdAt, completedAt: ms()
  };
  expectDenied(await db('PUT', `completedMatchArchives/${mid}`, peer, archive), `${mid}: non-host archive denied`);
  expectAllowed(await db('PUT', `completedMatchArchives/${mid}`, host, archive), `${mid}: immutable archive`);
  expectDenied(await db('PUT', `completedMatchArchives/${mid}`, host, { ...archive, mapId: 'map_forged' }), `${mid}: archive mutation denied`);

  const att = user => ({ uid: user.uid, matchId: mid, lobbyId: room, digest: archive.digest, revision, atMs: ms() });
  expectDenied(await db('PUT', `careerAttestations/${mid}/${peer.uid}`, peer,
    { ...att(peer), digest: hash('forged') }), `${mid}: wrong digest attestation denied`);
  expectAllowed(await db('PUT', `careerAttestations/${mid}/${host.uid}`, host, att(host)), `${mid}: host attestation`);
  expectAllowed(await db('PUT', `careerAttestations/${mid}/${peer.uid}`, peer, att(peer)), `${mid}: peer attestation`);

  const seats = {
    '0': { result: scenario.result, participating: true, properties: scenario.properties, developments: scenario.developments, cash: scenario.cash, netWorth: scenario.netWorth, diceRolls: scenario.dice, doublesRolled: scenario.doubles },
    '1': { result: peerResult, participating: true, properties: 2, developments: 1, cash: 800, netWorth: 1500, diceRolls: 6, doublesRolled: 0 },
    '2': { result: 'loss', participating: false, properties: 0, developments: 0, cash: 0, netWorth: 0, diceRolls: 0, doublesRolled: 0 },
    '3': { result: 'loss', participating: false, properties: 0, developments: 0, cash: 0, netWorth: 0, diceRolls: 0, doublesRolled: 0 }
  };
  const proof = {
    matchId: mid, lobbyId: room, hostUid: host.uid, witnessUid: peer.uid,
    revision, digest: archive.digest, snapshotJson, atMs: ms(), seats,
    rounds: scenario.rounds, turns: scenario.turns, playerCount: 2,
    mapId: scenario.mapId, themeId: 'classic_table', roundLimit: 30
  };
  expectDenied(await db('PUT', `careerFinalProofs/${mid}`, peer, proof), `${mid}: non-host proof denied`);
  expectAllowed(await db('PUT', `careerFinalProofs/${mid}`, host, proof), `${mid}: final proof`);

  const hostClaim = {
    uid: host.uid, matchId: mid, lobbyId: room, witnessUid: peer.uid,
    digest: proof.digest, revision, atMs: ms(), result: scenario.result,
    slotIndex: 0, rounds: scenario.rounds, turns: scenario.turns,
    properties: scenario.properties, developments: scenario.developments,
    cash: scenario.cash, netWorth: scenario.netWorth, playerCount: 2,
    mapId: scenario.mapId, themeId: 'classic_table', roundLimit: 30,
    hostAccountId: host.uid, diceRolls: scenario.dice, doublesRolled: scenario.doubles,
    recovered: false,
    xpAwarded: xp(scenario.result, scenario.rounds, scenario.properties, scenario.developments),
    goldAwarded: gold(scenario.result, scenario.rounds)
  };
  expectDenied(await db('PUT', `careerClaims/${host.uid}/${mid}`, peer, hostClaim), `${mid}: foreign claim denied`);
  expectDenied(await db('PUT', `careerClaims/${host.uid}/${mid}`, host, { ...hostClaim, result: 'win', goldAwarded: 50 }), `${mid}: mismatched/forged claim denied`);
  expectAllowed(await db('PUT', `careerClaims/${host.uid}/${mid}`, host, hostClaim), `${mid}: valid claim`);
  expectDenied(await db('PUT', `careerClaims/${host.uid}/${mid}`, host, hostClaim), `${mid}: immutable duplicate claim denied`);

  const stored = await db('GET', `careerClaims/${host.uid}/${mid}`, host);
  expectAllowed(stored, `${mid}: read back own claim`);
  const parsed = JSON.parse(stored.body);
  assert.equal(parsed.matchId, mid); checks++;
  assert.equal(parsed.result, scenario.result); checks++;
  assert.equal(parsed.goldAwarded, gold(scenario.result, scenario.rounds)); checks++;
  assert.equal(parsed.xpAwarded, xp(scenario.result, scenario.rounds, scenario.properties, scenario.developments)); checks++;

  return { matchId: mid, claim: hostClaim };
}

async function creditWallet(user, currentWallet, claim) {
  const walletPath = `users/${user.uid}/metaEconomy`;
  const read = await db('GET', walletPath, user);
  expectAllowed(read, `${claim.matchId}: wallet read ETag`);
  const next = advance(currentWallet, 'MATCH_REWARD', claim.matchId);
  next.wallet.gold += claim.goldAwarded;
  next.rewardHistory = `${currentWallet.rewardHistory || ''}|${claim.matchId}|`;
  expectAllowed(await db('PUT', walletPath, user, next, read.etag), `${claim.matchId}: exact wallet reward`);
  const duplicate = advance(next, 'MATCH_REWARD', claim.matchId);
  duplicate.wallet.gold += claim.goldAwarded;
  duplicate.rewardHistory += `|${claim.matchId}|`;
  expectDenied(await db('PUT', walletPath, user, duplicate), `${claim.matchId}: duplicate wallet reward denied`);
  return next;
}

(async () => {
  console.log('ATB_PHASE19BI_REAL_EMULATOR_AUDIT START');
  console.log(`emulator database=${process.env.FIREBASE_DATABASE_EMULATOR_HOST} auth=${process.env.FIREBASE_AUTH_EMULATOR_HOST}`);

  const catalog = parseAchievementCatalog();
  const targetById = Object.fromEntries(catalog.map(x => [x.id, x.target]));
  const host = await signup('phase19bi-achievement-host');
  const peer = await signup('phase19bi-achievement-peer');
  const guest = await signup('phase19bi-achievement-guest', true);
  const stranger = await signup('phase19bi-achievement-stranger');

  expectAllowed(await db('PUT', `users/${host.uid}`, 'owner', profile(host, 'Achievement Alice')), 'seed host profile');
  expectAllowed(await db('PUT', `users/${peer.uid}`, 'owner', profile(peer, 'Witness Bob')), 'seed peer profile');
  expectAllowed(await db('PUT', `users/${guest.uid}`, 'owner', profile(guest, 'Anonymous Guest')), 'seed guest profile');

  let wallet = startWallet();
  expectAllowed(await db('PUT', `users/${host.uid}/metaEconomy`, host, wallet), 'host wallet init');

  const created = [];
  for (let i = 0; i < scenarioPlan.length; i++) {
    const result = await createCompletedMatch({ host, peer, stranger, index: i, scenario: scenarioPlan[i] });
    created.push(result);
    wallet = await creditWallet(host, wallet, result.claim);
    console.log(`MATCH ${String(i + 1).padStart(2, '0')}/${scenarioPlan.length} PASS result=${scenarioPlan[i].result} map=${scenarioPlan[i].mapId} xp=${result.claim.xpAwarded} gold=${result.claim.goldAwarded}`);
  }

  // Anonymous Guest smoke match: same authenticated UID-bound path, but kept
  // outside the host achievement aggregate so the main scenario remains deterministic.
  const guestScenario = { result: 'tie', mapId: 'map_turkey', rounds: 5, turns: 6, dice: 5, doubles: 1, properties: 1, developments: 1, netWorth: 1200, cash: 700 };
  const guestMatch = await createCompletedMatch({ host: guest, peer, stranger, index: 99, scenario: guestScenario });
  ok(guestMatch.claim.result === 'tie', 'anonymous Guest completed one real emulator-backed claim');

  const claimsRead = await db('GET', `careerClaims/${host.uid}`, host);
  expectAllowed(claimsRead, 'read all host claims from RTDB');
  const claimMap = JSON.parse(claimsRead.body || '{}') || {};
  const claims = Object.values(claimMap);
  assert.equal(claims.length, scenarioPlan.length, 'all scenario claims persisted'); checks++;

  // Aggregate ONLY from values read back from RTDB, not from the input plan.
  const rebuilt = {
    matchesPlayed: 0, wins: 0, ties: 0, losses: 0,
    bestWinStreak: 0, currentWinStreak: 0,
    diceRolls: 0, doublesRolled: 0,
    propertiesAtFinishTotal: 0, developmentLevelsAtFinishTotal: 0,
    highestNetWorth: 0, mapTurkey: 0, mapColorado: 0, mapUsa: 0,
    totalXp: 0, totalGold: 0
  };
  let streak = 0;
  const ordered = claims.slice().sort((a, b) => Number(a.atMs || 0) - Number(b.atMs || 0));
  for (const c of ordered) {
    rebuilt.matchesPlayed++;
    if (c.result === 'win') { rebuilt.wins++; streak++; rebuilt.bestWinStreak = Math.max(rebuilt.bestWinStreak, streak); }
    else { if (c.result === 'tie') rebuilt.ties++; else rebuilt.losses++; streak = 0; }
    rebuilt.diceRolls += Number(c.diceRolls || 0);
    rebuilt.doublesRolled += Number(c.doublesRolled || 0);
    rebuilt.propertiesAtFinishTotal += Number(c.properties || 0);
    rebuilt.developmentLevelsAtFinishTotal += Number(c.developments || 0);
    rebuilt.highestNetWorth = Math.max(rebuilt.highestNetWorth, Number(c.netWorth || 0));
    const map = String(c.mapId || '').toLowerCase();
    if (map.includes('turkey')) rebuilt.mapTurkey++;
    else if (map.includes('colorado')) rebuilt.mapColorado++;
    else if (map.includes('usa')) rebuilt.mapUsa++;
    rebuilt.totalXp += Number(c.xpAwarded || 0);
    rebuilt.totalGold += Number(c.goldAwarded || 0);
  }
  rebuilt.currentWinStreak = streak;

  const expected = expectedAggregate(scenarioPlan);
  for (const [k, v] of Object.entries(expected)) {
    assert.equal(rebuilt[k], v, `rebuilt ${k}`); checks++;
  }

  const progress = achievementProgress(rebuilt);
  console.log('\n=== REAL RTDB REBUILT ACHIEVEMENT PROGRESS ===');
  for (const { id, target } of catalog) {
    const raw = progress[id];
    const capped = Math.min(target, raw);
    const unlocked = raw >= target;
    console.log(`${id.padEnd(20)} progress=${String(raw).padStart(4)} display=${String(capped).padStart(4)}/${target} unlocked=${unlocked}`);
    assert.equal(targetById[id], target); checks++;
    assert(raw >= target, `${id} should unlock in scenario matrix; got ${raw}/${target}`); checks++;
  }

  const walletRead = await db('GET', `users/${host.uid}/metaEconomy`, host);
  expectAllowed(walletRead, 'read final host wallet');
  const walletDb = JSON.parse(walletRead.body);
  assert.equal(walletDb.wallet.gold, expected.totalGold, 'wallet Gold equals sum of real persisted match rewards'); checks++;
  assert.equal(walletDb.wallet.atlasCoin, 0, 'achievement audit never mints Atlas Coin'); checks++;

  const guestClaimRead = await db('GET', `careerClaims/${guest.uid}/${guestMatch.matchId}`, guest);
  expectAllowed(guestClaimRead, 'anonymous Guest reads own persisted claim');
  const foreignRead = await db('GET', `careerClaims/${host.uid}`, stranger);
  expectDenied(foreignRead, 'foreign UID cannot read host Career claims');

  const durationMs = Date.now() - startedAt;
  console.log('\n=== FINAL ASSERTIONS ===');
  console.log(`matches=${scenarioPlan.length} wins=${rebuilt.wins} ties=${rebuilt.ties} losses=${rebuilt.losses} bestWinStreak=${rebuilt.bestWinStreak}`);
  console.log(`dice=${rebuilt.diceRolls} doubles=${rebuilt.doublesRolled} properties=${rebuilt.propertiesAtFinishTotal} developments=${rebuilt.developmentLevelsAtFinishTotal} highestNetWorth=${rebuilt.highestNetWorth}`);
  console.log(`maps turkey=${rebuilt.mapTurkey} colorado=${rebuilt.mapColorado} usa=${rebuilt.mapUsa} totalXp=${rebuilt.totalXp} walletGold=${walletDb.wallet.gold}`);
  console.log(`ATB_PHASE19BI_ACHIEVEMENT_FIREBASE_E2E PASS checks=${checks} httpOps=${httpOps} durationMs=${durationMs} authUsers=4 matches=${scenarioPlan.length + 1} production=NOT_TOUCHED UNITY_RUNTIME=NOT_RUN`);
})().catch(e => {
  console.error('ATB_PHASE19BI_ACHIEVEMENT_FIREBASE_E2E FAIL');
  console.error(e.stack || e);
  process.exitCode = 1;
});
