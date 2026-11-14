'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const rulesPath = path.join(__dirname, '../database.rules.json');
const raw = fs.readFileSync(rulesPath, 'utf8');
const rules = JSON.parse(raw).rules;
let checks = 0;
function ok(condition, description) {
  assert(condition, description);
  checks++;
}

function walk(node, pathParts = []) {
  if (!node || typeof node !== 'object') return;
  for (const [key, value] of Object.entries(node)) {
    if ((key === '.read' || key === '.write' || key === '.validate') &&
        typeof value === 'string') {
      ok(!value.includes('!newData.val()'),
        `unsafe unary ! on newData.val at ${pathParts.join('/')}/${key}`);
      ok(!value.includes('!data.val()'),
        `unsafe unary ! on data.val at ${pathParts.join('/')}/${key}`);
      ok(!value.includes('.numChildren('),
        `unsupported numChildren at ${pathParts.join('/')}/${key}`);
    }
    if (value && typeof value === 'object') walk(value, [...pathParts, key]);
  }
}
walk(rules, ['rules']);

const directory = rules.publicRoomDirectory && rules.publicRoomDirectory.$roomId;
ok(directory && directory.$other && directory.$other['.validate'] === false,
  'publicRoomDirectory must reject unknown fields');

// Security semantics matter, not the formatter's whitespace or == vs ===.
// The RTDB JSON validator first requires a boolean; comparison with false
// therefore cannot accept a string, a number or an unspecified value.
// The previous test searched only the literal 'newData.val() === false',
// producing a false failure after the existing rules were compacted.
const joinable = directory.joinable && directory.joinable['.validate'];
ok(typeof joinable === 'string', 'joinable must have a validation expression');
const compactJoinable = joinable.replace(/\s+/g, '');
ok(compactJoinable.includes('newData.isBoolean()'),
  'joinable must validate boolean type');
ok(/newData\.val\(\)={2,3}false(?![A-Za-z0-9_])/.test(compactJoinable),
  'joinable must use an explicit false comparison');
ok(!compactJoinable.includes('!newData.val()'),
  'joinable must not use unary ! on val()');
ok(compactJoinable.includes("root.child('roomMeta').child($roomId).child('closed').val()"),
  'joinable must check room closure');
ok(compactJoinable.includes("newData.parent().child('openOnlineSeatCount').val()>0"),
  'joinable must require an available online seat');

const hasPassword = directory.hasPassword && directory.hasPassword['.validate'];
ok(typeof hasPassword === 'string',
  'hasPassword must have a validation expression');
ok(hasPassword.includes("passwordHash').isString()"),
  'hasPassword true branch must type-check passwordHash');
ok(hasPassword.includes("passwordHash').exists()"),
  'hasPassword false branch must handle absent passwordHash');

console.log(`ATB_PHASE19BJ_00_RULES_PREFLIGHT PASS checks=${checks} json=true unaryValNot=false numChildren=false directoryHardening=true booleanType=true explicitFalse=true rulesChanged=false`);
