import {strict as assert} from 'node:assert';
import {reviewTarget} from './phase15j-release-preflight.mjs';
let count = 0;
function check(label, data, expected) {
  assert.equal(reviewTarget(data).ok, expected, label);
  count++;
  console.log(`PASS ${label}`);
}
check('Local emulator AppID 480 accepted only for development',
  {environment: 'development', project: 'atlasboard-usa', steamAppId: 480, emulatorOnly: true}, true);
check('Development cannot silently target deployed services',
  {environment: 'development', project: 'atlasboard-usa', steamAppId: 480, emulatorOnly: false}, false);
check('Missing target fails closed',
  {environment: '', project: '', steamAppId: 0, emulatorOnly: true}, false);
check('Staging cannot reuse development project',
  {environment: 'staging', project: 'atlasboard-usa', steamAppId: 0, emulatorOnly: false}, false);
check('Staging requires external operational acceptance',
  {environment: 'staging', project: 'atlasboard-staging', steamAppId: 123, emulatorOnly: false}, false);
check('Production rejects Steam AppID 480',
  {environment: 'production', project: 'atlasboard-prod', steamAppId: 480, emulatorOnly: false}, false);
check('Production remains blocked even with candidate real AppID',
  {environment: 'production', project: 'atlasboard-prod', steamAppId: 999, emulatorOnly: false}, false);
console.log(`Phase 15J preflight tests PASS: ${count} checks.`);
