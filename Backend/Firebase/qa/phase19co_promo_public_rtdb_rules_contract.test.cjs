// AtlasBoard v5.49.12co: pure JS rule-expression smoke tests.
// No user export, Firebase secret, live API, network, or production write.
// NOTE: These tests do NOT replace the official Firebase RTDB Rules emulator.
'use strict';
const fs = require('fs');
const path = require('path');
const assert = require('assert');
const base = path.join(__dirname, '..');
const rules = JSON.parse(fs.readFileSync(path.join(base, 'database.rules.json'), 'utf8')).rules;
const catalog = JSON.parse(fs.readFileSync(path.join(base, 'promoCodes_public.json'), 'utf8'));
const schema = rules.users.$uid.metaEconomy;
const UID = 'test_public_uid';
const moment = 1794236400000; // 2026-11-09 UTC, within code validity window.
const groups = ['profile_frames', 'boards', 'maps', 'dice', 'pawns', 'emotes'];
const inventory = Object.fromEntries(groups.map(g => [g,
  Object.fromEntries(Object.keys(schema.inventory[g]).filter(k => !k.startsWith('.') && !k.startsWith('$')).map(k => [k, false]))]));
const sampleWallet = {
  schemaVersion: 1,
  wallet: { gold: 0, atlasCoin: 0, starterGrantClaimed: false },
  inventory,
  equipped: { profileFrameId: '', boardId: '', diceId: '' },
  dailyReward: { lastClaimAt: 0 },
  dev: { grantCount: 0, resetCount: 0 },
  operation: { id: '0'.repeat(32), type: 'INIT', itemId: '', atMs: moment - 10000 },
  promoXp: 0,
  rewardHistory: ''
};
const OBJ = Symbol('firebase-object');
String.prototype.matches = function (regex) { return regex.test(this); };
String.prototype.contains = function (x) { return this.includes(x); };
function copy(obj) { return JSON.parse(JSON.stringify(obj)); }
function at(obj, pathParts) {
  for (const p of pathParts) { if (obj == null) return null; obj = obj[p]; }
  return obj === undefined ? null : obj;
}
class Snap {
  constructor(root, p = []) { this.root = root; this.p = p; }
  get() { return at(this.root, this.p); }
  child(p) { return new Snap(this.root, [...this.p, ...p.split('/')]); }
  parent() { return new Snap(this.root, this.p.slice(0, -1)); }
  val() { const x = this.get(); return x && typeof x === 'object' ? OBJ : x; }
  exists() { return this.get() !== null; }
  hasChild(p) { return this.child(p).exists(); }
  hasChildren(keys) { return keys.every(k => this.child(k).exists()); }
  isString() { return typeof this.get() === 'string'; }
  isBoolean() { return typeof this.get() === 'boolean'; }
  isNumber() { return typeof this.get() === 'number' && Number.isFinite(this.get()); }
}
const evaluate = (expression, root, before, after, targetUid = UID, relativePath = ['users', UID, 'metaEconomy']) =>
  new Function('auth', 'root', 'data', 'newData', 'now', '$uid', '$code',
    `return (${expression})`)(
      { uid: targetUid, token: { firebase: { sign_in_provider: 'password' } } },
      new Snap(root), new Snap(before, relativePath), new Snap(after, relativePath), moment,
      UID, relativePath[relativePath.length - 1]);
let checks = 0;
function check(label, actual, expected = true) { assert.strictEqual(actual, expected, label); checks++; }
function claimed(code, tamper = () => {}, old = sampleWallet, scope = 'public') {
  const previous = copy(old), next = copy(previous);
  const def = catalog[code] || { reward: { type: 'gold', amount: 50 } };
  const reward = def.reward;
  next.wallet.gold += reward.type === 'gold' ? reward.amount : 0;
  next.wallet.atlasCoin += reward.type === 'atlas_coin' ? reward.amount : 0;
  next.promoXp += reward.type === 'xp' ? reward.amount : 0;
  if (reward.type === 'inventory_item') {
    const group = { profile_frame: 'profile_frames', board_theme: 'boards', map: 'maps',
      dice_skin: 'dice', pawn: 'pawns', emote: 'emotes' }[reward.itemType];
    next.inventory[group][reward.itemId] = true;
  }
  next.rewardHistory += '|' + code + '|';
  next.operation = { id: '1'.repeat(32), type: 'PROMO', itemId: code, atMs: moment };
  tamper(next);
  const c = copy(catalog);
  if (scope !== 'public') for (const k in c) c[k].scope = scope;
  const root = { users: { [UID]: { metaEconomy: previous } }, promoCodes: c };
  const after = copy(root); after.users[UID].metaEconomy = next;
  return { root, after, old: previous, next };
}
function walletAllowed(code, alter, scope, old, actor) {
  const x = claimed(code, alter, old, scope);
  return evaluate(schema['.write'], x.root, x.root, x.after, actor || UID);
}
check('64 definitions', Object.keys(catalog).length, 64);
check('64 public scopes', Object.values(catalog).every(x => x.scope === 'public'));
check('64 per-account limits', Object.values(catalog).every(x => x.perAccountLimit === 1));
for (const code of Object.keys(catalog)) check('first public claim ' + code, walletAllowed(code));
const gold = Object.keys(catalog).find(k => catalog[k].reward.type === 'gold');
const xp = Object.keys(catalog).find(k => catalog[k].reward.type === 'xp');
const coin = Object.keys(catalog).find(k => catalog[k].reward.type === 'atlas_coin');
const item = Object.keys(catalog).find(k => catalog[k].reward.type === 'inventory_item');
check('second claim same UID denied', walletAllowed(gold, undefined, undefined,
  { ...copy(sampleWallet), rewardHistory: '|' + gold + '|' }), false);
check('same code another UID accepted', walletAllowed(gold)); // distinct empty ledger
check('owned item coupon denied by server', walletAllowed(item, undefined, undefined, (() => { const x=copy(sampleWallet);x.inventory.emotes.emote_drop=true;return x; })()), false);
check('owner QA restricted code rejected', walletAllowed(gold, undefined, 'owner_qa'), false);
check('unknown code rejected', walletAllowed('ATLAS-G-111111111111111111'), false);
check('other UID cannot write', walletAllowed(gold, undefined, undefined, undefined, 'forged_uid'), false);
check('gold extra amount denied', walletAllowed(gold, x => x.wallet.gold += 12), false);
check('XP extra amount denied', walletAllowed(xp, x => x.promoXp += 12), false);
check('coin extra amount denied', walletAllowed(coin, x => x.wallet.atlasCoin += 12), false);
check('unrelated inventory unlock denied', walletAllowed(gold, x => x.inventory.maps.map_mexico = true), false);
check('missing one-use ledger denied', walletAllowed(gold, x => x.rewardHistory = ''), false);
check('changing unrelated DEV counter denied', walletAllowed(gold, x => x.dev.grantCount += 1), false);
check('changing equipped board denied', walletAllowed(gold, x => x.equipped.boardId = 'board_midnight_azure'), false);
check('wrong bonus in currency claim denied', walletAllowed(xp, x => x.wallet.gold += 1), false);
// The nested emote validation is separately enforced by Firebase and must allow PROMO.
const emote = Object.keys(catalog).find(k => catalog[k].reward.itemType === 'emote');
const e = claimed(emote), emoteId = catalog[emote].reward.itemId;
const field = ['users', UID, 'metaEconomy', 'inventory', 'emotes', emoteId];
check('emote nested .validate allows correct PROMO', evaluate(
  schema.inventory.emotes[emoteId]['.validate'], e.root, e.root, e.after, UID, field));
const text = fs.readFileSync(path.join(base, '..', '..', 'Assets', 'Project', 'Scripts', 'Meta', 'AtlasBoardMetaEconomyBridge.cs'), 'utf8');
check('no promo cloud callable in Bridge', !text.includes('metaPromoRedeem') && !text.includes('promoTestRedeem'));
check('public catalog subtree is not list-readable', rules.promoCodes['.read'] === false);
check('client cannot write public catalog', rules.promoCodes['.write'] === false);
// Regression checks: existing purchase/daily rules still accept the ordinary wallet path.
function legacyKind(kind) {
  const old = copy(sampleWallet), afterWallet = copy(old);
  old.wallet.gold = 500;
  afterWallet.wallet.gold = kind === 'DAILY' ? 550 : 150;
  if (kind === 'DAILY') afterWallet.dailyReward.lastClaimAt = moment;
  else afterWallet.inventory.profile_frames.frame_halloween = true;
  afterWallet.operation = { id: '2'.repeat(32), type: kind,
    itemId: kind === 'DAILY' ? '' : 'frame_halloween', atMs: moment };
  const root = {users:{[UID]:{metaEconomy:old}},promoCodes:catalog};
  const after = copy(root); after.users[UID].metaEconomy=afterWallet;
  return evaluate(schema['.write'],root,root,after);
}
check('existing DAILY remains allowed',legacyKind('DAILY'));
check('existing PURCHASE remains allowed',legacyKind('PURCHASE'));
console.log(`ATB_19CO_PUBLIC_PROMO ${checks}/${checks} synthetic checks PASS (official emulator still required)`);
