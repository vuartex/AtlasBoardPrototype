/**
 * Read-only environment readiness gate. Never performs deploy or secret access.
 * atlasboard-usa is currently the known shared development backend, NOT a
 * verified staging/production target. Production release remains blocked.
 */
import {fileURLToPath} from 'node:url';

export function reviewTarget({environment, project, steamAppId, emulatorOnly}) {
  const errors = [];
  if (!['development', 'staging', 'production'].includes(environment)) {
    errors.push('Explicit --environment=development|staging|production is required.');
  }
  if (!project) errors.push('Explicit --project=<firebase-project-id> is required.');
  if (environment === 'development') {
    if (!emulatorOnly) errors.push('Development acceptance must use --emulator-only.');
    if (project !== 'atlasboard-usa') errors.push('Unknown development project.');
    if (steamAppId !== 480) errors.push('Development AppID must be 480 for this local checkpoint.');
  }
  if (environment === 'staging') {
    if (project === 'atlasboard-usa') errors.push('Staging must not reuse shared development project.');
    errors.push('Staging environment requires separately provisioned project, secrets, IAM and smoke acceptance.');
  }
  if (environment === 'production') {
    if (steamAppId === 480) errors.push('Steam AppID 480 is development-only.');
    if (project === 'atlasboard-usa') errors.push('Current shared development project is not approved as production.');
    errors.push('PRODUCTION_BLOCKED_UNTIL_15K: App Check deployed enforcement, publisher secret, production Steam AppID, environment IAM and deployed E2E are not accepted.');
  }
  return {ok: errors.length === 0, errors};
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const opt = (key) => process.argv.find(v => v.startsWith(`--${key}=`))?.split('=').slice(1).join('=') ?? '';
  const steamAppId = Number(opt('steam-app-id'));
  const result = reviewTarget({
    environment: opt('environment'),
    project: opt('project'),
    steamAppId,
    emulatorOnly: process.argv.includes('--emulator-only'),
  });
  for (const msg of result.errors) console.error(`BLOCKED ${msg}`);
  if (result.ok) console.log('Phase 15J DEV EMULATOR preflight PASS (no deploy).');
  else process.exitCode = 1;
}
