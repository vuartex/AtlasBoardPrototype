/** Append-only local acceptance checkpoint; only called after full local suite PASS. */
import {readFileSync, appendFileSync} from 'node:fs';
import {dirname, resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const functionsDir = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const file = resolve(functionsDir, '../../../Assets/Project/Documentation/MASTER_ROADMAP.md');
const marker = '<!-- ATLAS_PHASE15K_LOCAL_CHECKPOINT_V1 -->';
try {
  const current = readFileSync(file, 'utf8');
  if (current.includes(marker)) {
    console.log('Phase 15K roadmap checkpoint ALREADY PRESENT (no change).');
  } else {
    if (!current.includes('Phase 15I') || !current.includes('Phase 15J') ||
        !current.includes('Phase 15K')) {
      throw new Error('Unknown roadmap structure; refusing to append.');
    }
    const date = new Date().toISOString();
    appendFileSync(file, `\n\n${marker}\n` +
      `## Local security acceptance checkpoint (${date})\n\n` +
      `- **Phase 15I**: DONE locally; 15 static security, 49 moderation E2E, ` +
        `17 Firestore Rules E2E PASS. Open report/evidence retention and TTL activation still need cloud inspection.\n` +
      `- **Phase 15J**: DONE locally; 16 static operations, 7 environment, ` +
        `8 emulator smoke, 17 rules PASS. Local emulator network access is loopback-only.\n` +
      `- **Phase 15K**: LOCAL SECURITY REGRESSION ACCEPTED; production Steam/backend ` +
        `security close is **BLOCKED** until actual AppID, publisher secret, deployed ` +
        `App Check positive/negative cryptographic proof, IAM, TTL, monitoring and Steam ` +
        `runtime evidence are accepted by the release owner.\n` +
      `- Local App Check validation includes missing-token rejection in emulator; ` +
        `the emulator may accept invalid non-empty tokens and is NOT production cryptographic proof.\n` +
      `- Original .env.local restored; no deployment, git commit, or git push performed.\n` +
      `- **Next work:** production 15K gates remain open; safe local Phase 16 ` +
        `build/QA planning may proceed without interpreting it as permission to deploy.\n`, 'utf8');
    console.log('Phase 15K roadmap local checkpoint APPENDED (production still blocked).');
  }
} catch (error) {
  console.error('Phase 15K roadmap checkpoint FAILED:', error.message);
  process.exitCode = 1;
}
