'use strict';
// Generates ONE operator-controlled global week schedule. Secure RNG is used
// ONCE per calendar week, NEVER by a player/client. Fails closed if a prior
// schedule exists. No live Firebase operations or credentials.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const base=path.resolve(__dirname,'..');
const weeksFile=path.join(base,'weeklyMissionCatalog_initial.json');
const tokensFile=path.join(base,'collectibleTokenCatalog_initial.json');
const START='2026-11-09T00:00:00.000Z';
const WEEKS=156;
const WEEK_MS=7*24*60*60*1000;
const tier=[
 {rounds:8,win:0,dice:2,properties:1},
 {rounds:10,win:1,dice:3,properties:1},
 {rounds:12,win:0,dice:4,properties:2},
 {rounds:20,win:1,dice:6,properties:3}
];
function key(ms){return new Date(ms).toISOString().slice(0,10).replace(/-/g,'');}
function catalog() {
 const out={},tokens={};
 for(let i=0;i<WEEKS;i++){
  const start=Date.parse(START)+WEEK_MS*i,id=key(start),token=crypto.randomInt(4)===0;
  const vars={};
  for(let v=0;v<4;v++){
   const g=tier[(i+v)%4];
   vars['abcd'[v]]={
    s0:{type:'rounds',target:g.rounds},
    s1:{type:g.win?'win':'winOrTie',target:g.win},
    s2:{type:'dice',target:g.dice},
    s3:{type:'properties',target:g.properties}
   };
  }
  out[id]={startUtcMs:start,endUtcMs:start+WEEK_MS,hasTokenReward:token,
   ...(token?{tokenId:'weekly_'+id}:{}),variants:vars};
  if(token)tokens['weekly_'+id]={weekId:id,title:'Atlas Weekly '+id,
   description:'Limited weekly verified-match collectible. Cosmetic only.',kind:'museum',
   startUtcMs:start};
 }
 return {out,tokens};
}
const a=catalog();
const count=Object.values(a.out).filter(x=>x.hasTokenReward).length;
console.log('WEEKLY_MANIFEST_FROM='+START+' COUNT='+WEEKS+' TOKEN_WEEKS='+count);
console.log('CLIENT_RNG=FORBIDDEN; CALENDAR_AUTHORITY=OPERATOR_PUBLISHED_RTDB');
if(process.argv.includes('--write')){
 if(fs.existsSync(weeksFile)||fs.existsSync(tokensFile))throw Error('Schedule already exists: refusing to reroll. Never overwrite an accepted schedule.');
 fs.writeFileSync(weeksFile,JSON.stringify(a.out,null,2)+'\n',{flag:'wx'});
 fs.writeFileSync(tokensFile,JSON.stringify(a.tokens,null,2)+'\n',{flag:'wx'});
 console.log('LOCAL_CATALOG_FILES_CREATED=YES; FIREBASE_DEPLOY=NOT_DONE');
}else console.log('DRY_RUN_ONLY=YES. Use --write once, publish only after emulator PASS.');
