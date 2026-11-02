'use strict';
const assert=require('node:assert/strict');
const fs=require('node:fs');const path=require('node:path');
const p=path.join(__dirname,'../database.rules.json');
const raw=fs.readFileSync(p,'utf8');
const parsed=JSON.parse(raw);const rules=parsed.rules;
let checks=0;function ok(v,m){assert(v,m);checks++;}
function walk(node,pathParts=[]){
 if(!node||typeof node!=='object')return;
 for(const [k,v] of Object.entries(node)){
  if((k==='.read'||k==='.write'||k==='.validate')&&typeof v==='string'){
   ok(!v.includes('!newData.val()'),`unsafe unary ! on newData.val at ${pathParts.join('/')}/${k}`);
   ok(!v.includes('!data.val()'),`unsafe unary ! on data.val at ${pathParts.join('/')}/${k}`);
   ok(!v.includes('.numChildren('),`unsupported numChildren at ${pathParts.join('/')}/${k}`);
  }
  if(v&&typeof v==='object')walk(v,[...pathParts,k]);
 }
}
walk(rules,['rules']);
const d=rules.publicRoomDirectory.$roomId;
ok(d && d['$other'] && d['$other']['.validate']===false,'publicRoomDirectory must reject unknown fields');
ok(d.joinable['.validate'].includes('newData.val() === false'),'joinable must use explicit boolean comparison');
ok(!d.joinable['.validate'].includes('!newData.val()'),'joinable must not use unary ! on val()');
ok(d.hasPassword['.validate'].includes("passwordHash').isString()"),'hasPassword true branch must type-check passwordHash');
ok(d.hasPassword['.validate'].includes("passwordHash').exists()"),'hasPassword false branch must handle absent passwordHash');
console.log(`ATB_PHASE19BJ_00_RULES_PREFLIGHT PASS checks=${checks} json=true unaryValNot=false numChildren=false directoryHardening=true`);
