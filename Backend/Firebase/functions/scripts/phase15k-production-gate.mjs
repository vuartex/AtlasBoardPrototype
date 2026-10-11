/**
 * Read-only reporting of unmet production prerequisites.
 * Never turns a local/emulator PASS into production approval and never deploys.
 */
import {fileURLToPath} from 'node:url';

const requirements = [
  'Real AtlasBoard Steam AppID is issued and verified against published game',
  'Publisher Web API secret exists in approved GCP project with least-privilege IAM',
  'Previously exposed App Check debug token was revoked and rotated privately',
  'Registered Firebase App ID is correct for desktop Steam custom App Check token',
  'App Check enforcement deployed after valid Steam bootstrap acceptance',
  'Deployed valid-token positive and missing/invalid-token negative probe receipts PASS',
  'Verified Steam ticket linking AND returning sign-in demonstrated on deployed target',
  'Dedicated staging and production project boundaries approved',
  'Firestore TTL policies inspected in deployed target with active status',
  'Cloud Monitoring alerts and privacy-safe logging verified with operational owner',
  'IAM, secret rotation, rollback and incident runbook accepted',
  'End-to-end release regression accepted by authorized release owner',
];
export function productionGate({project = '', appId = NaN} = {}) {
  const problems = [];
  if (!project || project === 'atlasboard-usa')
    problems.push('Production project must be separate from shared atlasboard-usa development backend.');
  if (!Number.isSafeInteger(appId) || appId <= 0 || appId === 480)
    problems.push('Real Steam publisher AppID required; 480 is never a production ID.');
  problems.push('Manual deployed/operational acceptance is still required. This CLI cannot certify it.');
  return {ok: false, problems, requirements};
}
if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const get = (name) => process.argv.find((x) => x.startsWith(`--${name}=`))?.split('=').slice(1).join('=') ?? '';
  const gate = productionGate({project: get('project'), appId: Number(get('steam-app-id'))});
  console.log('Phase 15K PRODUCTION RELEASE: BLOCKED (by design; no deploy).');
  for (const p of gate.problems) console.log('BLOCKED ' + p);
  console.log('Human/deployed evidence requirements:');
  gate.requirements.forEach((r, i) => console.log(`  ${i + 1}. ${r}`));
  process.exitCode = 1; // Fail closed for CI and automation; never grants approval.
}
