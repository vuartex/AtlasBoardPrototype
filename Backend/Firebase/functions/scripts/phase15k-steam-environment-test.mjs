/** Runtime guard acceptance against compiled TypeScript. Run after npm build. */
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
const require = createRequire(import.meta.url);
const {assertSteamVerifierEnvironment} =
  require('../lib/security/steam-environment.js');
const original = process.env.FUNCTIONS_EMULATOR;
let checks = 0;
function test(label, body) { body(); console.log('PASS ' + label); checks++; }
try {
  process.env.FUNCTIONS_EMULATOR = 'true';
  test('Steam AppID 480 is allowed only inside verified Functions emulator process',
    () => assert.doesNotThrow(() => assertSteamVerifierEnvironment(480)));
  delete process.env.FUNCTIONS_EMULATOR;
  test('Steam AppID 480 is rejected by deployed runtime',
    () => assert.throws(() => assertSteamVerifierEnvironment(480),
      (error) => error.message === 'STEAM_DEVELOPMENT_APP_ID_FORBIDDEN'));
  test('Missing or invalid Steam AppID fails closed', () => {
    for (const value of [0, -1, Number.NaN, 1.5, Number.MAX_SAFE_INTEGER + 1]) {
      assert.throws(() => assertSteamVerifierEnvironment(value));
    }
  });
  test('Real candidate AppID may proceed to independent publisher verification',
    () => assert.doesNotThrow(() => assertSteamVerifierEnvironment(123456)));
  console.log(`Phase 15K Steam runtime environment unit PASS: ${checks} checks.`);
} finally {
  if (original === undefined) delete process.env.FUNCTIONS_EMULATOR;
  else process.env.FUNCTIONS_EMULATOR = original;
}
