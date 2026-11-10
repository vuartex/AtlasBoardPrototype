'use strict';
// v5.49.12da structural regression gate. Auth/RTDB authorization was
// separately verified by the owner in the official 17/17 emulator run.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const base = path.resolve(__dirname, '..', '..', '..');
const code = fs.readFileSync(path.join(base,
  'Assets/Project/Scripts/UI/Menu/AtlasBoardNewsCommunityHub.cs'), 'utf8');
const feed = JSON.parse(fs.readFileSync(path.join(base,
  'Assets/Project/Resources/AtlasBoardNewsPosts.json'), 'utf8'));
const polls = JSON.parse(fs.readFileSync(path.join(__dirname, '..',
  'communityPolls_initial.json'), 'utf8'));
const rules = require('./phase19cu_news_voting_prepare_rules.cjs');
const ruleSource = fs.readFileSync(path.join(__dirname,
  'phase19cu_news_voting_prepare_rules.cjs'), 'utf8');
const produced = rules.createRules();
let checks = 0;
function test(name, run) { run(); checks++; console.log('PASS', name); }
function copy(x) { return JSON.parse(JSON.stringify(x)); }
function sourceDoc(legacy, legacyWild = false) {
  const generated = copy(legacy);
  if (legacyWild)
    generated.tallies.$pollId.countedUids.$other = { '.validate': false };
  return { rules: {
    newsPosts: { '.read': 'auth != null', '.write': false },
    communityPolls: generated.poll,
    communityVotes: generated.votes,
    communityVoteTallies: generated.tallies,
    unrelatedWallet: { '.read': false, '.write': false },
  } };
}
test('20 curated NEWS posts retained', () => assert.equal(feed.posts.length, 20));
test('Community Voting owner PASS news is newest and historically grounded', () => {
  const post = feed.posts[0];
  assert.equal(post.id, 'atb_dev_21');
  assert.equal(post.date, '2026-11-10');
  assert.match(post.titleTr, /Topluluk Oylaması/);
  assert.match(post.bodyTr, /17 güvenlik testi/);
  assert.match(post.bodyEn, /17 Firebase Auth and RTDB emulator/);
  assert.ok(!feed.posts.some(p => p.id === 'atb_dev_20'));
  assert.equal(new Set(feed.posts.map(p => p.id)).size, 20);
});
test('20-month and max-20 NEWS filters unchanged', () => {
  assert.match(code, /AddMonths\(-20\)/);
  assert.match(code, /Mathf\.Min\(20, fresh\.Count\)/);
});
test('All NEWS posts retain date, title, full text', () => {
  for (const p of feed.posts) assert.ok(p.date && p.titleTr && p.titleEn && p.bodyTr && p.bodyEn);
});
test('Existing Help/Credits entries and attribution tab untouched', () => {
  assert.match(code, /AtlasBoard_Help_Entry/);
  assert.match(code, /AtlasBoard_Credits_Entry/);
  assert.match(code, /savedOriginalHelpAction\.Invoke\(\)/);
});
test('One poll, exactly seven answer choices, no per-category polls', () => {
  assert.equal(Object.keys(polls).length, 1);
  assert.deepEqual(Object.keys(polls[rules.pollId].options), rules.optionIds);
  assert.doesNotMatch(code, /foreach\s*\(string category in Categories\)/);
});
test('One explicit OY VER button, single choice per registered UID', () => {
  assert.match(code, /chosenOptionId = captured/);
  assert.match(code, /"OY VER"/);
  assert.match(code, /SubmitVote\(CurrentPollId, chosenOptionId/);
  assert.match(code, /IsRegistered/);
});
test('Immutable private UID receipt created with server timestamp', () => {
  assert.match(code, /communityVotes\//);
  assert.match(code, /If-Match", "null_etag"/);
  assert.match(code, /\\"\.sv\\":\\"timestamp\\"/);
  assert.match(produced.votes.$pollId.$uid['.write'], /!data\.exists\(\)/);
});
test('Public alias is a GUID, not the Firebase UID', () => {
  assert.match(code, /Guid\.NewGuid\(\)\.ToString\("N"\)/);
  assert.match(code, /recordedAlias/);
  assert.match(code, /IsValidAlias/);
  assert.match(produced.votes.$pollId.$uid.publicAlias['.validate'], /matches/);
});
test('Public receipt validates the exact own verified private alias', () => {
  const wr = produced.publicVotes.$pollId.$alias['.write'];
  assert.match(wr, /child\('publicAlias'\)\.val\(\) === \$alias/);
  assert.match(wr, /child\('optionId'\)\.val\(\) === newData\.child\('optionId'\)\.val\(\)/);
  assert.match(wr, /!data\.exists\(\)/);
});
test('No direct client-writable total or per-choice counters', () => {
  assert.doesNotMatch(code, /communityVoteTallies\//);
  assert.doesNotMatch(code, /ServerValue\.increment/);
  assert.doesNotMatch(JSON.stringify(produced), /numChildren\(\)/);
  assert.ok(!('tallies' in produced));
});
test('Public vote receipts have no UID or other custom fields', () => {
  const node = produced.publicVotes.$pollId.$alias;
  assert.equal(node.$other['.validate'], false);
  assert.deepEqual(Object.keys(node).filter(k => !k.startsWith('.')).sort(),
    ['$other','optionId']);
  assert.equal(produced.publicVotes.$pollId['.read'], 'auth != null');
});
test('Private UID receipt list is not publicly enumerable', () => {
  assert.equal(produced.votes.$pollId['.read'], false);
  assert.equal(produced.votes.$pollId.$uid['.read'], 'auth != null && auth.uid === $uid');
});
test('Totals are calculated only from authenticated immutable alias records', () => {
  assert.match(code, /communityPublicVotes\//);
  assert.match(code, /foreach \(JProperty item in anonymousVotes\.Properties\(\)\)/);
  assert.match(code, /verifiedVoteTotal = total/);
  assert.doesNotMatch(code, /\/countedUids/);
});
test('Account switch clears own recorded vote and alias', () => {
  assert.match(code, /pollsUid = AtlasBoardRtdbMetaEconomy\.CurrentUid;/);
  assert.match(code, /recordedAlias = "";/);
  assert.match(code, /requestGeneration\+\+/);
});
test('Interrupted second publish retries exactly the same saved alias', () => {
  assert.match(code, /PublishOwnAnonymousVote\(generation, uid, token\)/);
  assert.match(code, /recordedAlias = alias; committed = true/);
});
test('All voting rules have unique wildcard per sibling level', () =>
  assert.doesNotThrow(() => rules.validateWildcardSiblings(produced)));
test('Only supported RuleDataSnapshot methods in generated voting rules', () => {
  assert.doesNotMatch(JSON.stringify(produced), /numChildren\(\)|forEach\(/);
  assert.match(JSON.stringify(produced), /hasChildren/);
});
test('Operator-only rules generator never deploys/imports data', () => {
  assert.match(ruleSource, /process\.argv\.includes\('--write'\)/);
  assert.match(ruleSource, /expectedGitBlob = 'b6d40bd5cebdf5bd6ca6778f76249136110149b3'/);
  assert.doesNotMatch(ruleSource, /firebase\.cmd|firebase deploy|spawn\(/);
});
test('Known existing cw rules safely migrate, all unrelated roots survive', () => {
  const doc = sourceDoc(rules.createLegacyRules());
  const result = rules.prepare(Buffer.from(JSON.stringify(doc)));
  assert.ok(result.changed && result.oldCw);
  const after = JSON.parse(result.output).rules;
  assert.deepEqual(after.unrelatedWallet, doc.rules.unrelatedWallet);
  assert.deepEqual(after.newsPosts, doc.rules.newsPosts);
  assert.ok(!('communityVoteTallies' in after));
  assert.ok('communityPublicVotes' in after);
  assert.doesNotMatch(result.output, /numChildren\(\)/);
  assert.ok(result.bytes < 262144);
});
test('Operator-minified existing cw rules are recognized without overwriting unrelated branches', () => {
  const doc = sourceDoc(rules.createLegacyRules());
  function compact(x) {
    if (typeof x === 'string') return x
      .replace(/\s+&&\s+/g, '&&').replace(/\s+\|\|\s+/g, '||')
      .replace(/\s+===\s+/g, '===').replace(/\s+!==\s+/g, '!==')
      .replace(/\s+>=\s+/g, '>=').replace(/\s+<=\s+/g, '<=')
      .replace(/\s+==\s+/g, '==').replace(/\s+!=\s+/g, '!=');
    if (Array.isArray(x)) return x.map(compact);
    if (x && typeof x === 'object') for (const k of Object.keys(x)) x[k] = compact(x[k]);
    return x;
  }
  const compressed = compact(doc);
  const migrated = rules.prepare(Buffer.from(JSON.stringify(compressed)));
  assert.equal(migrated.oldCw, true);
  assert.deepEqual(JSON.parse(migrated.output).rules.unrelatedWallet, compressed.rules.unrelatedWallet);
});
test('Old cv wildcard collision also safely migrates', () => {
  const doc = sourceDoc(rules.createLegacyRules(), true);
  const result = rules.prepare(Buffer.from(JSON.stringify(doc)));
  assert.equal(result.brokenCv, true);
  assert.doesNotThrow(() => rules.validateWildcardSiblings(JSON.parse(result.output)));
});
test('A previously migrated cx rules file is idempotent', () => {
  const first = rules.prepare(Buffer.from(JSON.stringify(sourceDoc(rules.createLegacyRules()))));
  const second = rules.prepare(Buffer.from(first.output));
  assert.equal(second.preparedCx, true);
  assert.equal(second.changed, false);
});
test('Unknown local modified rules never overwritten', () => {
  const doc = sourceDoc(rules.createLegacyRules());
  doc.rules.communityVoteTallies.$pollId['.write'] = 'true';
  assert.throws(() => rules.prepare(Buffer.from(JSON.stringify(doc))),
    /Unknown local rules state/);
});
test('Official emulator test requires demo project and separate Auth/RTDB', () => {
  const emu = fs.readFileSync(path.join(__dirname,
    'phase19cv_news_vote_emulator.test.cjs'), 'utf8');
  assert.match(emu, /FIREBASE_DATABASE_EMULATOR_HOST/);
  assert.match(emu, /FIREBASE_AUTH_EMULATOR_HOST/);
  assert.match(emu, /demo-/);
});
test('Emulator REST writes JSON-encode strings, not raw registered text', () => {
  const emulator = fs.readFileSync(path.join(__dirname,
    'phase19cv_news_vote_emulator.test.cjs'), 'utf8');
  assert.match(emulator, /body:\s*JSON\.stringify\(body\)/);
  assert.doesNotMatch(emulator, /typeof body === 'string' \? body/);
  assert.match(emulator, /'registered'\),/);
});
test('Emulator reports the provider response on unexpected write status', () => {
  const emulator = fs.readFileSync(path.join(__dirname,
    'phase19cv_news_vote_emulator.test.cjs'), 'utf8');
  assert.match(emulator, /async function expectOk\(/);
  assert.match(emulator, /JSON\.stringify\(response\.data\)/);
});
test('Demo admin fixtures use the Bearer owner header, never a user auth query', () => {
  const emulator = fs.readFileSync(path.join(__dirname,
    'phase19cv_news_vote_emulator.test.cjs'), 'utf8');
  assert.match(emulator, /const isEmulatorAdmin = idToken === 'owner'/);
  assert.match(emulator, /if \(idToken && !isEmulatorAdmin\)/);
  assert.match(emulator, /Authorization: 'Bearer owner'/);
  assert.match(emulator, /RTDB demo emulator owner-bearer bootstrap probe/);
  assert.match(emulator, /project\.startsWith\('demo-'\)/);
});
console.log(`SOURCE_GATE=${checks}/${checks} PASS; UNITY_COMPILER=OWNER_PASS_REPORTED; FIREBASE_EMULATOR=OWNER_REPORTED_17_OF_17_PASS; PRODUCTION_RULES=OWNER_REPORTED_DEPLOYED`);
