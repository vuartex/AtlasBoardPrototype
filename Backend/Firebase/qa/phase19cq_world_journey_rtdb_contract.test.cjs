"use strict";
const fs = require("fs");
const path = require("path");
const vm = require("vm");
const assert = require("assert");
const project = path.resolve(__dirname,"..");
const rules = JSON.parse(fs.readFileSync(path.join(project,"database.rules.json"),"utf8")).rules;
const base = rules.users.$uid.metaEconomy;
const event = rules.users.$uid.worldJourney;
const uid = "valid_account_uid";
const matchId = "a".repeat(32);
const start=1793664000000, end=1814400000000;
const now=1794300000000;
class Snap {
  constructor(root,keys=[]) {this.root=root;this.keys=keys;}
  child(p) {return new Snap(this.root,this.keys.concat(String(p).split("/")));}
  parent() {return new Snap(this.root,this.keys.slice(0,-1));}
  val() {let v=this.root;for(const key of this.keys){if(v===null||v===undefined||typeof v!=="object"||!Object.prototype.hasOwnProperty.call(v,key))return null;v=v[key];}return v===undefined?null:v;}
  exists() {return this.val()!==null;}
  isString(){return typeof this.val()==="string";}
  isBoolean(){return typeof this.val()==="boolean";}
  isNumber(){return typeof this.val()==="number";}
}
const e=(expression,old,updated,root,authUid=uid)=>{
  const context={auth:{uid:authUid,token:{firebase:{sign_in_provider:"password"}}},
    $uid:uid,now,root:new Snap(root),data:new Snap(old),newData:new Snap(updated)};
  return vm.runInNewContext("String.prototype.matches=function(r){return r.test(this);};String.prototype.contains=function(t){return this.includes(t);};\n"+expression,context,{timeout:1500});
};
let checks=0;
function check(name,expression,old,updated,root,want,authUid=uid){let result;
  try{result=Boolean(e(expression,old,updated,root,authUid));}catch(err){throw new Error(`${name}: expression threw ${err.message}`)}
  assert.strictEqual(result,want,`${name} expected=${want}, got=${result}`);checks++;}
function copy(x){return JSON.parse(JSON.stringify(x));}
function wallet(){
 const inventory={};
 for(const category of ["profile_frames","boards","maps","dice","pawns","emotes"]){
   const rules=base.inventory[category];inventory[category]={};for(const key of Object.keys(rules))if(!key.startsWith("." )&&key!=="$other")inventory[category][key]=false;
 }
 return {schemaVersion:1,wallet:{gold:100,atlasCoin:0,starterGrantClaimed:false},promoXp:0,
   inventory,equipped:{profileFrameId:"",boardId:"",diceId:""},dailyReward:{lastClaimAt:0},
   dev:{grantCount:0,resetCount:0},operation:{id:"f".repeat(32),type:"DAILY",itemId:"",atMs:now-1000},rewardHistory:"",worldClaims:""};
}
const root={users:{[uid]:{worldJourney:{matches:2,wins:3,diceRolls:50,developments:10,turns:20,mapCount:3,highestNetWorth:5000}}},careerClaims:{[uid]:{[matchId]:{uid,matchId,atMs:now-5000,result:"win",diceRolls:5,developments:1,turns:4,mapId:"Turkey",netWorth:1510}}},devWalletTesters:{[uid]:true}};
let old=wallet(), next=copy(old);
next.wallet.gold+=500;next.worldClaims+="|two_matches|";next.operation={id:"1".repeat(32),type:"EVENT_CLAIM",itemId:"two_matches",atMs:now};
check("first gold claim accepted",base[".write"],old,next,root,true);
check("foreign user denied",base[".write"],old,next,root,false,"different_uid");
let forged=copy(next);forged.wallet.gold+=1000;
check("extra gold rejected",base[".write"],old,forged,root,false);
let duplicate=copy(next);duplicate.operation.id="2".repeat(32);duplicate.operation.atMs++;
check("same reward rejected",base[".write"],next,duplicate,root,false);
let coin=copy(old);coin.wallet.atlasCoin+=100;coin.worldClaims="|ten_developments|";
coin.operation={id:"3".repeat(32),type:"EVENT_CLAIM",itemId:"ten_developments",atMs:now};
check("Atlas Coin claim accepted",base[".write"],old,coin,root,true);
let xp=copy(old);xp.promoXp=500;xp.worldClaims="|fifty_rolls|";
xp.operation={id:"4".repeat(32),type:"EVENT_CLAIM",itemId:"fifty_rolls",atMs:now};
check("XP claim accepted",base[".write"],old,xp,root,true);
let mapOld=copy(old);mapOld.worldClaims="|two_matches||fifty_rolls||ten_developments||three_wins||twenty_turns||three_maps||networth_5000|";
let mapNew=copy(mapOld);mapNew.worldClaims+="|world_explorer|";
mapNew.inventory.maps.map_world=true;
mapNew.operation={id:"5".repeat(32),type:"EVENT_CLAIM",itemId:"world_explorer",atMs:now};
check("world map only after 7 claims",base[".write"],mapOld,mapNew,root,true);
let notSeven=copy(mapOld);notSeven.worldClaims=notSeven.worldClaims.replace("|three_wins|","");
check("world map locked before 7",base[".write"],notSeven,mapNew,root,false);
let boardOld=copy(mapNew),boardNew=copy(boardOld);boardNew.worldClaims+="|completion_bonus|";
boardNew.inventory.boards.board_atlas_horizon=true;
boardNew.operation={id:"6".repeat(32),type:"EVENT_CLAIM",itemId:"completion_bonus",atMs:now+1};
check("Horizon board after map",base[".write"],boardOld,boardNew,root,true);
let oldProof=null;
let newProof={schemaVersion:1,proofHistory:"|"+matchId+"|",matches:1,wins:1,diceRolls:5,developments:1,turns:4,maps:"|Turkey|",mapCount:1,highestNetWorth:1510,lastMatchId:matchId,atMs:now,qaMode:false};
check("career-backed proof accepted",event[".write"],oldProof,newProof,root,true);
check("other account cannot submit proof",event[".write"],oldProof,newProof,root,false,"other_uid");
let doubleProof=copy(newProof);doubleProof.matches++;doubleProof.proofHistory+="|"+matchId+"|";
check("same match cannot add twice",event[".write"],newProof,doubleProof,root,false);
let fakeProof=copy(newProof);fakeProof.diceRolls=500;
check("forged dice count denied",event[".write"],oldProof,fakeProof,root,false);
const qa={schemaVersion:1,proofHistory:"",matches:3,wins:3,diceRolls:50,developments:10,turns:20,maps:"|Turkey||USA||Colorado|",mapCount:3,highestNetWorth:5000,lastMatchId:"editor_qa",atMs:now,qaMode:true};
check("QA accepted for dev tester",event[".write"],null,qa,root,true);
const notTester=copy(root);notTester.devWalletTesters[uid]=false;
check("QA denied for normal player",event[".write"],null,qa,notTester,false);
let reset=copy(qa);Object.assign(reset,{matches:0,wins:0,diceRolls:0,developments:0,turns:0,maps:"",mapCount:0,highestNetWorth:0,atMs:now});
check("QA progress reset",event[".write"],qa,reset,root,true);
const size=Buffer.byteLength(JSON.stringify({rules}));assert(size<262144);checks++;
assert(base.operation.type[".validate"].includes("EVENT_CLAIM"));checks++;
assert(!base[".write"].includes("metaWorldJourneyGetOverview"));checks++;
for(const key of ["promoCodes","careerClaims","careerDiceObservations","devWalletTesters","roomMeta"]){assert(rules[key]);checks++;}
console.log(`WORLD JOURNEY RTDB static contract: ${checks}/${checks} PASS; serialized rule bytes=${size}`);
