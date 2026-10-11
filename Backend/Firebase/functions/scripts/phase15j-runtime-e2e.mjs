/** Safe emulator-only smoke: never calls production or Valve. */
import {strict as assert} from 'node:assert';
const HOST = '127.0.0.1';
const BASE = `http://${HOST}:5001/atlasboard-usa/europe-west1`;
let count = 0;
const hub = await fetch(`http://${HOST}:4400/emulators`, {signal: AbortSignal.timeout(12000)});
assert.ok(hub.ok, 'Firebase Emulator Hub must be available.');
const emulators = await hub.json();
for (const service of ['auth', 'firestore', 'functions']) {
  assert.ok(emulators[service], `${service} emulator not running.`);
  count++;
  console.log(`PASS ${service} emulator available`);
}
async function callable(name, data) {
  const response = await fetch(`${BASE}/${name}`, {
    method: 'POST',
    headers: {'Content-Type': 'application/json'},
    body: JSON.stringify({data}),
    signal: AbortSignal.timeout(18000),
  });
  return {status: response.status, body: await response.text()};
}
for (const name of ['lobbyChatSend', 'lobbyChatList', 'matchChatSend', 'matchChatList']) {
  const response = await callable(name, {scopeId: 'phase15j-smoke-test'});
  assert.ok(response.status === 401 || response.status === 403,
    `${name} unauthenticated response was ${response.status}: ${response.body.slice(0,180)}`);
  console.log(`PASS ${name} blocks unauthenticated traffic`);
  count++;
}
const bootstrap = await callable('platformSteamCreateAppCheckToken', {ticket: ''});
assert.equal(bootstrap.status, 400,
  `Steam App Check bootstrap did not reject malformed ticket: ${bootstrap.status}`);
console.log('PASS Steam App Check bootstrap rejects malformed ticket before verification');
count++;
console.log(`Phase 15J emulator smoke PASS: ${count} checks.`);
