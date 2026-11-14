'use strict';
// Run against the ACTUAL installed qa helper and the ACTUAL local RTDB rules.
// Source-only diagnostic, never touches Firebase or production data.
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const crypto=require('node:crypto');
const helperFile=path.join(__dirname,'phase19bj_security_shared.cjs');
const rulesFile=path.join(__dirname,'..','database.rules.json');
const marker='ATB_WALLET_PRICE_PARSER_BUILD=v5.49.12eo';
const expectedRulesSha='aadc3be5358737b62775a7d831e4e925543fc1451aaad673c5d7874f4d8da86e';
function sha(bytes){return crypto.createHash('sha256').update(bytes).digest('hex');}
function stop(message){console.error('ATB_WALLET_PRICE_GATE=FAIL: '+message);process.exitCode=1;}
try{
 const helperSource=fs.readFileSync(helperFile,'utf8');
 const helperSha=sha(helperSource);
 const raw=fs.readFileSync(rulesFile);
 const rulesSha=sha(raw);
 console.log('PRICE_PARSER_HELPER_PATH='+helperFile);
 console.log('PRICE_PARSER_HELPER_SHA256='+helperSha);
 console.log('PRICE_PARSER_RULES_SHA256='+rulesSha);
 assert(helperSource.includes(marker), 'Installed shared QA helper is outdated. Extract the FULL v5.49.12eo ZIP into the AtlasBoardPrototype project root, replacing the existing file, then rerun this gate.');
 assert.equal(rulesSha,expectedRulesSha,'Local Rules SHA differs from owner-validated v5.49.12em Rules. Do not blindly overwrite or deploy; review local Rules changes first.');
 const {SecurityQa}=require('./phase19bj_security_shared.cjs');
 const {rules}=JSON.parse(raw.toString('utf8'));
 const src=rules.users.$uid.metaEconomy['.write'];
 assert(src.includes("newData.child('operation/type').val()!='WEEKLY_REWARD'"),'v5.49.12em scoped wallet write fix is missing');
 const prices=SecurityQa.prototype.priceMap.call({rules});
 const expected={frame_halloween:350,frame_new_year:350,frame_turkiye:400,dice_ruby_red:350,board_midnight_azure:1200,map_germany:30000};
 for(const [id,price] of Object.entries(expected))assert.equal(prices[id],price,`Wrong Rules price for ${id}: got ${String(prices[id])}`);
 assert(Object.keys(prices).length>=30,'The current Rules price table was parsed incompletely');
 const compact="newData.child('operation/itemId').val()=='frame_halloween'?350:0";
 const spaced="newData.child('operation/itemId').val() === 'dice_ruby_red' ? 350 : 0";
 const mock={users:{$uid:{metaEconomy:{'.write':compact+'+'+spaced}}}};
 const checks=SecurityQa.prototype.priceMap.call({rules:mock});
 assert.equal(checks.frame_halloween,350);
 assert.equal(checks.dice_ruby_red,350);
 assert.throws(()=>SecurityQa.prototype.priceMap.call({rules:{users:{$uid:{metaEconomy:{'.write':compact+'+'+compact.replace('350:0','351:0')}}}}}),/Conflicting price/);
 console.log('PRICE_PARSER_BUILD=v5.49.12eo');
 console.log('PRICE_MAP_ITEMS='+Object.keys(prices).length+' FRAME_HALLOWEEN='+prices.frame_halloween+' DICE_RUBY_RED='+prices.dice_ruby_red);
 console.log('ATB_WALLET_PRICE_GATE=PASS checks=13 actualRules=true compactFormat=true spacedFormat=true conflictingPriceRejected=true');
 console.log('NEXT=Run Phase19BJ full emulator security suite. Do not deploy production Rules unless all stages pass.');
}catch(e){stop(String(e.message||e));}
