'use strict';
// v5.49.12cx: safe UID-private + public pseudonymous vote proofs. No client-owned counters.
// Does not touch the live Firebase database, import data or deploy rules.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const os = require('os');
const rulesPath = path.join(__dirname, '..', 'database.rules.json');
const expectedGitBlob = 'b6d40bd5cebdf5bd6ca6778f76249136110149b3';
const optionIds = ['maps', 'pawns', 'avatars', 'dice', 'boards', 'profile_frames', 'emotes'];
const pollId = 'community_feature_priority_2026w46';
const pollPath = "root.child('communityPolls').child($pollId)";
const ownReceipt = "root.child('communityVotes').child($pollId).child(auth.uid)";
const eligibility = "auth != null && auth.token.firebase.sign_in_provider != 'anonymous' && root.child('users').child(auth.uid).child('general').child('accountType').val() === 'registered'";

function createLegacyRules() {
  const voter = "auth != null && auth.uid === $uid && auth.token.firebase.sign_in_provider != 'anonymous' && root.child('users').child($uid).child('general').child('accountType').val() === 'registered'";
  const vote = [voter, '!data.exists()', 'newData.exists()',
    `${pollPath}.child('active').val() === true`,
    `${pollPath}.child('startsAtMs').val() <= now`,
    `${pollPath}.child('endsAtMs').val() > now`,
    'newData.child(\'uid\').val() === $uid',
    'newData.child(\'pollId\').val() === $pollId',
    'newData.child(\'optionId\').isString()',
    `${pollPath}.child('options').child(newData.child('optionId').val()).exists()`,
    'newData.child(\'createdAt\').isNumber()',
    'newData.child(\'createdAt\').val() >= now - 30000',
    'newData.child(\'createdAt\').val() <= now + 30000'
  ].join(' && ');
  const optionChecks = optionIds.map(id => {
    const prev = `(data.child('optionCounts/${id}').isNumber() ? data.child('optionCounts/${id}').val() : 0)`;
    const next = `(newData.child('optionCounts/${id}').isNumber() ? newData.child('optionCounts/${id}').val() : 0)`;
    return `${next} === ${prev} + (${ownReceipt}.child('optionId').val() === '${id}' ? 1 : 0)`;
  });
  const count = [eligibility,
    `${ownReceipt}.child('uid').val() === auth.uid`,
    `${ownReceipt}.child('pollId').val() === $pollId`,
    `${pollPath}.child('options').child(${ownReceipt}.child('optionId').val()).exists()`,
    '!data.child(\'countedUids\').child(auth.uid).exists()',
    'newData.child(\'countedUids\').child(auth.uid).val() === true',
    'newData.child(\'countedUids\').numChildren() === data.child(\'countedUids\').numChildren() + 1',
    'newData.child(\'total\').isNumber()',
    "newData.child('total').val() === (data.child('total').isNumber() ? data.child('total').val() : 0) + 1",
    "newData.child('total').val() <= 2000000000",
    "newData.hasChildren(['total','countedUids','optionCounts'])",
    'newData.numChildren() === 3',
    ...optionChecks
  ].join(' && ');
  return {
    poll: { '.read': 'auth != null', '.write': false },
    votes: {
      '.read': false, '.write': false,
      '$pollId': {
        '.read': false, '.write': false,
        '$uid': {
          '.read': 'auth != null && auth.uid === $uid',
          '.write': vote,
          '.validate': "newData.hasChildren(['uid','pollId','optionId','createdAt']) && newData.numChildren() === 4",
          uid: { '.validate': 'newData.isString()' },
          pollId: { '.validate': 'newData.isString()' },
          optionId: { '.validate': 'newData.isString()' },
          createdAt: { '.validate': 'newData.isNumber()' },
          '$other': { '.validate': false }
        }
      }
    },
    tallies: {
      '.read': false, '.write': false,
      '$pollId': {
        '.read': false,
        '.write': count,
        '.validate': "newData.hasChildren(['total','countedUids','optionCounts']) && newData.numChildren() === 3",
        total: {
          '.read': 'auth != null',
          '.validate': 'newData.isNumber() && newData.val() >= 0 && newData.val() <= 2000000000'
        },
        optionCounts: {
          '.read': 'auth != null',
          '$option': {
            '.validate': "root.child('communityPolls').child($pollId).child('options').child($option).exists() && newData.isNumber() && newData.val() >= 0 && newData.val() <= 2000000000"
          }
        },
        countedUids: {
          '.read': false,
          '$uid': {
            // Existing marker must always stay true; only the authenticated
            // caller may add a NEW marker. No child can be deleted without
            // breaking the parent's +1 exact-marker-count condition.
            '.validate': "newData.val() === true && (data.exists() || $uid === auth.uid)"
          },
        },
        '$other': { '.validate': false }
      }
    }
  };
}
// A permanent private receipt is create-only under its verified UID. A second
// immutable, PUBLIC *pseudonym* receipt is published under an unguessable
// per-vote GUID; it contains only the option ID, never a UID. The public
// aggregate is derived from those protected receipts by readers, NOT a
// client-writable mutable counter. Firebase RuleDataSnapshot has NO numChildren()
// method, so the former cross-user counter schema cannot be secured.
function createRules() {
  const old = createLegacyRules();
  const vote = JSON.parse(JSON.stringify(old.votes));
  const receipt = vote.$pollId.$uid;
  receipt['.validate'] = "newData.hasChildren(['uid','pollId','optionId','createdAt','publicAlias'])";
  receipt.publicAlias = {
    '.validate': "newData.isString() && newData.val().matches(/^[a-f0-9]{32}$/)"
  };
  const ownReceipt = "root.child('communityVotes').child($pollId).child(auth.uid)";
  const publicWrite = [
    eligibility, '!data.exists()', 'newData.exists()',
    `${ownReceipt}.child('publicAlias').val() === $alias`,
    `${ownReceipt}.child('optionId').val() === newData.child('optionId').val()`,
    `${pollPath}.child('options').child(newData.child('optionId').val()).exists()`,
    "newData.child('optionId').isString()"
  ].join(' && ');
  const publicVotes = {
    '.read': false, '.write': false,
    '$pollId': {
      '.read': 'auth != null', '.write': false,
      '$alias': {
        '.write': publicWrite,
        '.validate': "newData.hasChildren(['optionId'])",
        optionId: { '.validate': 'newData.isString()' },
        '$other': { '.validate': false }
      }
    }
  };
  return { poll: old.poll, votes: vote, publicVotes };
}

// A RTDB rules node may have only one wildcard child. In cv, countedUids
// accidentally had BOTH $uid and $other; the emulator correctly rejected it.
// Validate the entire proposed rules tree before writing or deploying.
function validateWildcardSiblings(node, label = 'rules') {
  if (!node || typeof node !== 'object' || Array.isArray(node)) return;
  const wildcards = Object.keys(node).filter(key => key.startsWith('$'));
  if (wildcards.length > 1) throw new Error(
    'Multiple wildcard children under ' + label + ': ' + wildcards.join(', '));
  for (const [key, child] of Object.entries(node)) {
    if (key.startsWith('.')) continue;
    validateWildcardSiblings(child, label + '/' + key);
  }
}

// Explicitly recognize the exact previous cv shape. Avoid treating unknown
// locally edited voting rules as safe to overwrite or "repair".
function sameRules(left, right) {
  return JSON.stringify(normalize(JSON.parse(JSON.stringify(left)))) ===
    JSON.stringify(normalize(JSON.parse(JSON.stringify(right))));
}
function isExistingCv(r, hasWildcard) {
  if (!r.communityPolls || !r.communityVotes || !r.communityVoteTallies ||
      r.communityPublicVotes) return false;
  const expected = createLegacyRules();
  if (hasWildcard) expected.tallies.$pollId.countedUids.$other = { '.validate': false };
  return sameRules(r.communityPolls, expected.poll) &&
    sameRules(r.communityVotes, expected.votes) &&
    sameRules(r.communityVoteTallies, expected.tallies);
}
function isPreparedCx(r) {
  if (!r.communityPolls || !r.communityVotes || !r.communityPublicVotes ||
      r.communityVoteTallies) return false;
  const expected = createRules();
  return sameRules(r.communityPolls, expected.poll) &&
    sameRules(r.communityVotes, expected.votes) &&
    sameRules(r.communityPublicVotes, expected.publicVotes);
}

function cleanOldCu(r) {
  // A previous v5.49.12cu rules-preparation may already be applied locally.
  // Refuse to replace unrelated/unknown rules under these branches.
  const old = r.communityVotes;
  return r.communityPolls && normalize(r.communityPolls['.read']) === normalize('auth != null') &&
    r.communityPolls['.write'] === false && old && old['.read'] === false &&
    old['.write'] === false && old.$pollId && old.$pollId.$uid &&
    String(old.$pollId.$uid['.write']).includes("!data.exists()") &&
    String(old.$pollId.$uid['.write']).includes("root.child('communityPolls')") &&
    !r.communityVoteTallies && !r.communityPublicVotes;
}
function normalize(value) {
  if (typeof value === 'string') return value
    .replace(/\s+&&\s+/g, '&&').replace(/\s+\|\|\s+/g, '||')
    .replace(/\s+===\s+/g, '===').replace(/\s+!==\s+/g, '!==')
    .replace(/\s+>=\s+/g, '>=').replace(/\s+<=\s+/g, '<=')
    .replace(/\s+==\s+/g, '==').replace(/\s+!=\s+/g, '!=');
  if (Array.isArray(value)) return value.map(normalize);
  if (value && typeof value === 'object') {
    for (const k of Object.keys(value)) value[k] = normalize(value[k]);
  }
  return value;
}
function prepare(source) {
  const raw = Buffer.from(source);
  const sha = crypto.createHash('sha1')
    .update(Buffer.from(`blob ${raw.length}\0`)).update(raw).digest('hex');
  const doc = JSON.parse(raw.toString('utf8'));
  const r = doc.rules;
  const original = sha === expectedGitBlob;
  const priorCu = cleanOldCu(r);
  const brokenCv = isExistingCv(r, true);
  const oldCw = isExistingCv(r, false);
  const preparedCx = isPreparedCx(r);
  if (!original && !priorCu && !brokenCv && !oldCw && !preparedCx)
    throw new Error('Unknown local rules state; refusing to overwrite. SHA: ' + sha);
  if (r.newsPosts &&
      (normalize(r.newsPosts['.read']) !== normalize('auth != null') ||
       r.newsPosts['.write'] !== false))
    throw new Error('News rules differ; refusing to overwrite.');
  if (!r.newsPosts) r.newsPosts = { '.read': 'auth != null', '.write': false };
  if (!preparedCx) {
    // Only the previously-generated news/poll/tally branches are touched.
    // All existing wallets, account rules, Career and game branches are kept.
    const updated = createRules();
    r.communityPolls = updated.poll;
    r.communityVotes = updated.votes;
    r.communityPublicVotes = updated.publicVotes;
    delete r.communityVoteTallies;
  }
  validateWildcardSiblings(r);
  if (JSON.stringify(doc).includes('numChildren()'))
    throw new Error('Unsupported RuleDataSnapshot.numChildren() still present.');
  let output = JSON.stringify(doc);
  if (Buffer.byteLength(output) >= 262144) output = JSON.stringify(normalize(doc));
  const bytes = Buffer.byteLength(output + '\n', 'utf8');
  if (bytes >= 262144) throw new Error('RTDB rules exceed 256 KiB limit: ' + bytes);
  return { output: output + '\n', bytes, sha, original, priorCu,
    brokenCv, oldCw, preparedCx, changed: !preparedCx };
}
module.exports = { prepare, createRules, createLegacyRules, pollId, optionIds,
  isExistingCv, isPreparedCx, validateWildcardSiblings };
if (require.main === module) {
  try {
    if (!fs.existsSync(rulesPath))
      throw new Error('Missing database.rules.json. Run from the complete local project.');
    const r = prepare(fs.readFileSync(rulesPath));
    const mode = r.original ? 'GitHub ct' : r.priorCu ? 'previous cu' :
      r.brokenCv ? 'migrate old cv' : r.oldCw ? 'migrate existing cw' : 'already prepared cx';
    console.log('Validated source SHA:', r.sha, 'Mode:', mode);
    console.log('Generated rules bytes:', r.bytes, '/ 262144');
    console.log('RULES_API_AND_WILDCARD_CHECK=PASS');
    console.log('VOTING_MODEL=IMMUTABLE_PUBLIC_ALIAS_RECEIPTS; NO_CLIENT_TOTAL_WRITES');
    if (process.argv.includes('--write')) {
      if (!r.changed) {
        console.log('LOCAL_RULES_ALREADY_CORRECTED=YES; no write needed.');
        process.exit(0);
      }
      // Preserve the exact owner-local pre-fix rules OUTSIDE the repository.
      const backupDir = path.join(os.homedir(), 'AtlasBoard_RtdbRuleBackups');
      fs.mkdirSync(backupDir, { recursive: true });
      const backup = path.join(backupDir, 'database.rules.before_cx_' + r.sha + '.json');
      fs.copyFileSync(rulesPath, backup, fs.constants.COPYFILE_EXCL);
      fs.writeFileSync(rulesPath, r.output, { flag: 'w' });
      console.log('LOCAL_RULES_MIGRATED=YES; BACKUP=', backup);
      console.log('PRODUCTION_DEPLOY=NOT_DONE; run official emulator before deploying.');
    } else console.log('DRY_RUN_ONLY. Add --write to modify local rules.');
  } catch (e) { console.error('RULES_PREPARE_BLOCKED:', e.message); process.exit(1); }
}
