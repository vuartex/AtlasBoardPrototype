'use strict';
// Full-file RTDB Rules preparation for the FREE/Spark version-read policy.
// Does not modify the production database or deploy. Never alters room auth,
// wallets, career proofs, inventory or any other existing Rules branch.
// --write backs up the COMPLETE old Rules outside the project and replaces
// the COMPLETE local database.rules.json only after validation.
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const file = path.resolve(__dirname, '../database.rules.json');
const seedFile = path.resolve(__dirname, '../gameversion_initial.json');
const cap = 262144;
const write = process.argv.includes('--write');
const raw = fs.readFileSync(file, 'utf8');
const baseline = crypto.createHash('sha256').update(raw).digest('hex');
const seed = JSON.parse(fs.readFileSync(seedFile, 'utf8'));
assert.deepEqual(Object.keys(seed).sort(), ['currentgameversion', 'lastgameversion']);
assert.equal(seed.currentgameversion, 'v0.9.1');
assert.equal(seed.lastgameversion, 'v.0.9.0');
const rules = JSON.parse(raw);
assert.ok(rules.rules && typeof rules.rules === 'object');
assert.ok(rules.rules.users && rules.rules.roomMeta && rules.rules.joinRequests,
  'Unexpected Firebase Rules structure: abort; do not deploy');
const expected = {'.read': true, '.write': false};
if (Object.prototype.hasOwnProperty.call(rules.rules, 'gameversion'))
  assert.deepEqual(rules.rules.gameversion, expected,
    'Existing gameversion rule differs: stop rather than overwriting policy');
rules.rules.gameversion = expected;
// Preserve the byte-for-byte meaning of every other rules branch.
const updated = JSON.stringify(rules);
const bytes = Buffer.byteLength(updated, 'utf8');
console.log('GAMEVERSION_BASELINE_SHA256=' + baseline);
console.log('GAMEVERSION_RULES_BYTES=' + bytes + '/' + cap);
if (bytes > cap) {
  console.error('GAMEVERSION_RULES=OVER_LIMIT; abort before writing or deploying');
  process.exit(2);
}
const otherKeys = Object.keys(rules.rules).filter(k => k !== 'gameversion');
const before = JSON.parse(raw).rules;
for (const k of otherKeys) assert.deepEqual(rules.rules[k], before[k], 'Unrelated rules changed: ' + k);
console.log('GAMEVERSION_RULES=' + (before.gameversion ? 'ALREADY_PRESENT' : 'READY'));
if (!write) {console.log('DRY_RUN_ONLY=YES; LIVE_DEPLOY=NO');process.exit(0);}
const existing = JSON.parse(raw);
if (existing.rules.gameversion && updated === raw) {
  console.log('LOCAL_RULES_ALREADY_PRESENT=YES; LIVE_DEPLOY=NO');
  process.exit(0);
}
const backupDir = path.join(os.homedir(), 'AtlasBoard_RtdbRuleBackups');
fs.mkdirSync(backupDir, {recursive: true});
const backupPath = path.join(backupDir, 'database.rules.before_gameversion_' + baseline.slice(0, 16) + '.json');
if (!fs.existsSync(backupPath)) fs.writeFileSync(backupPath, raw, 'utf8');
const tmp = file + '.gameversion_tmp';
fs.writeFileSync(tmp, updated, 'utf8');
try {
  assert.deepEqual(JSON.parse(fs.readFileSync(tmp, 'utf8')), rules);
  fs.renameSync(tmp, file);
} finally { if (fs.existsSync(tmp)) fs.unlinkSync(tmp); }
console.log('COMPLETE_LOCAL_RULES_WRITTEN=YES');
console.log('BACKUP=' + backupPath);
console.log('LIVE_DEPLOY=NO');
