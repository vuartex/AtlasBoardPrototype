import assert from 'node:assert/strict';
import {validateInputs} from './phase15k-deployed-appcheck-probe.mjs';
const base = {project:'atlasboard-production-example', appId:123456,
  target:'https://europe-west1-atlasboard-production-example.cloudfunctions.net/economyHealthCheck',
  isLive:true};
let count = 0;
function bad(obj, label) {
  assert.throws(() => validateInputs({...base,...obj}));
  console.log('PASS block ' + label); count++;
}
bad({project:'atlasboard-usa'}, 'shared development project');
bad({appId:480}, 'Steam AppID 480');
bad({target:'http://127.0.0.1:5001/economyHealthCheck'}, 'localhost');
bad({target:'https://evil.example.com/economyHealthCheck'}, 'untrusted hostname');
bad({target:'https://europe-west1-another-project.cloudfunctions.net/economyHealthCheck'}, 'mismatched project');
bad({isLive:false}, 'missing explicit live-network acknowledgement');
assert.match(validateInputs(base), /^https:\/\//);
console.log('PASS known approved-shape Cloud Functions URL'); count++;
console.log(`Phase 15K deployed probe input unit PASS: ${count} checks (no network).`);
