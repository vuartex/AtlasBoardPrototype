'use strict';
// v5.49.12cx operator audit for pseudonymous public vote records.
// READ-ONLY by default. Private UID receipts are never published or logged.
// Optional repair creates ONLY missing verified public aliases, with a
// concurrent-update guard. No client-writable totals, no Cloud Functions.
const path = require('node:path');
const args = process.argv.slice(2);
const emulator = args.includes('--emulator');
const live = args.includes('--live');
const repair = args.includes('--repair');
if (emulator === live) { console.error('Choose exactly one of --emulator and --live.'); process.exit(2); }
if (emulator && !process.env.FIREBASE_DATABASE_EMULATOR_HOST) {
  console.error('RTDB emulator must be running.'); process.exit(2);
}
if (live && repair && !args.includes('--confirm-live-repair')) {
  console.error('Live repair requires --repair --confirm-live-repair.'); process.exit(2);
}
const admin = require(path.join(__dirname, '..', 'functions', 'node_modules', 'firebase-admin'));
const databaseURL = 'https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app';
if (!admin.apps.length) admin.initializeApp(emulator
  ? { databaseURL, projectId: 'demo-atlasboard' }
  : { credential: admin.credential.applicationDefault(), databaseURL, projectId: 'atlasboard-usa' });
const pollId = 'community_feature_priority_2026w46';
(async () => {
  const db = admin.database();
  const manifest = (await db.ref('communityPolls/' + pollId).get()).val();
  if (!manifest || !manifest.options || Object.keys(manifest.options).length !== 7)
    throw new Error('Expected one seven-choice manifest. No changes made.');
  const privateVotes = (await db.ref('communityVotes/' + pollId).get()).val() || {};
  const publicRef = db.ref('communityPublicVotes/' + pollId);
  const before = (await publicRef.get()).val() || {};
  const expected = {};
  const counts = Object.fromEntries(Object.keys(manifest.options).map(k => [k, 0]));
  for (const [uid, receipt] of Object.entries(privateVotes)) {
    if (!receipt || uid !== receipt.uid || receipt.pollId !== pollId ||
        !Object.hasOwn(counts, receipt.optionId) ||
        !/^[a-f0-9]{32}$/.test(String(receipt.publicAlias || '')) ||
        typeof receipt.createdAt !== 'number' ||
        receipt.createdAt < manifest.startsAtMs ||
        receipt.createdAt >= manifest.endsAtMs)
      throw new Error('Unexpected private receipt; audit blocked without changes.');
    if (Object.hasOwn(expected, receipt.publicAlias))
      throw new Error('Public alias collision detected; repair blocked.');
    expected[receipt.publicAlias] = { optionId: receipt.optionId };
    counts[receipt.optionId]++;
  }
  const missing = {};
  for (const [alias, value] of Object.entries(expected)) {
    if (!Object.hasOwn(before, alias)) missing[alias] = value;
    else if (before[alias]?.optionId !== value.optionId || Object.keys(before[alias]).length !== 1)
      throw new Error('Published anonymous receipt differs from its private proof.');
  }
  for (const alias of Object.keys(before))
    if (!Object.hasOwn(expected, alias))
      throw new Error('Unexpected public alias; manual investigation required.');
  const published = Object.fromEntries(Object.keys(counts).map(k => [k, 0]));
  for (const v of Object.values(before)) published[v.optionId]++;
  console.log(JSON.stringify({ pollId, verifiedVotes: Object.keys(expected).length,
    publishedVotes: Object.keys(before).length, missingToPublish: Object.keys(missing).length,
    verifiedPerOption: counts, publishedPerOption: published, repair }));
  if (!repair || Object.keys(missing).length === 0) return;
  const beforeJson = JSON.stringify(before);
  const outcome = await publicRef.transaction(current => {
    if (JSON.stringify(current || {}) !== beforeJson) return;
    return { ...(current || {}), ...missing };
  }, undefined, false);
  if (!outcome.committed) throw new Error('Concurrent public result update; retry audit.');
  console.log('MISSING_ANONYMOUS_RESULTS_PUBLISHED=' + Object.keys(missing).length);
})().then(() => process.exit(0)).catch(error => {
  console.error('Vote audit error:', error.name, error.message);
  process.exit(1);
});
