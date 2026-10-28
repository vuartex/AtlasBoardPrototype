const fs=require('fs');const rules=JSON.parse(fs.readFileSync('/mnt/data/atb_maps_work/Backend/Firebase/database.rules.json','utf8')).rules;
const paths=[...new Set([...rules.users.$uid.metaEconomy['.write'].matchAll(/(?:newData|data)\.child\('([^']+)'\)/g)].map(m=>m[1]))];
function put(obj,path,val){const parts=path.split('/');let ptr=obj;for(const part of parts.slice(0,-1)){if(!ptr[part]||typeof ptr[part]!=='object')ptr[part]={};ptr=ptr[part];}ptr[parts.at(-1)]=val;}
function get(obj,path){let cur=obj;for(const p of path.split('/')){if(cur==null||!Object.prototype.hasOwnProperty.call(cur,p))return undefined;cur=cur[p];}return cur;}
class Snap{constructor(v){this.value=v;}child(path){return new Snap(get(this.value,path));}val(){return this.value===undefined?null:this.value;}exists(){return this.value!==undefined&&this.value!==null;}isString(){return typeof this.value==='string';}isNumber(){return typeof this.value==='number';}isBoolean(){return typeof this.value==='boolean';}hasChildren(keys){return keys.every(k=>get(this.value,k)!==undefined);} }
function base(){const doc={};for(const path of paths){if(path.startsWith('inventory/'))put(doc,path,false);if(path.startsWith('equipped/'))put(doc,path,'');}
put(doc,'schemaVersion',1);put(doc,'wallet/gold',50000);put(doc,'wallet/atlasCoin',500);put(doc,'wallet/starterGrantClaimed',false);put(doc,'operation/id','aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa');put(doc,'operation/type','DEV_GRANT');put(doc,'operation/itemId','');put(doc,'operation/atMs',49999000);put(doc,'dailyReward/lastClaimAt',0);put(doc,'dev/grantCount',0);put(doc,'dev/resetCount',0);return doc;}
const evalRule=new Function('auth','data','newData','root','now','query','$uid','$roomId','return ('+rules.users.$uid.metaEconomy['.write']+');');
const root = {users:{abc:{metaEconomy:{inventory:{maps:{map_germany:true,map_mexico:false}}}}},devWalletTesters:{abc:true}};
function check(docBefore,after){const auth={uid:'abc',token:{firebase:{sign_in_provider:'password'}}};return evalRule(auth,new Snap(docBefore),new Snap(after),new Snap(root),50000000,{},'abc','r1');}
let before=base(),after=JSON.parse(JSON.stringify(before));after.operation={id:'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',type:'PURCHASE',itemId:'map_germany',atMs:50000000};after.wallet.gold=20000;after.inventory.maps.map_germany=true;
function assert(tag,b,expected){console.log(tag, b===expected?'PASS':'FAIL',b);if(b!==expected)process.exitCode=1;}
assert('Germany 30000 Gold + ownership',check(before,after),true);
let wrong=JSON.parse(JSON.stringify(after));wrong.wallet.gold=49999;assert('Discount spoof denied',check(before,wrong),false);
wrong=JSON.parse(JSON.stringify(after));wrong.inventory.maps.map_mexico=true;assert('Second map mint denied',check(before,wrong),false);
wrong=JSON.parse(JSON.stringify(after));wrong.inventory.maps.map_germany=false;assert('Charge without entitlement denied',check(before,wrong),false);
let mx=JSON.parse(JSON.stringify(after));mx.operation.itemId='map_mexico';mx.inventory.maps.map_germany=false;mx.inventory.maps.map_mexico=true;mx.wallet.gold=25000;assert('Mexico 25000 Gold + ownership',check(before,mx),true);
let same=JSON.parse(JSON.stringify(after));same.inventory.maps.map_germany=true;assert('Map not double-purchasable',check(after,same),false);
let older=JSON.parse(JSON.stringify(before));delete older.inventory.maps;assert('Legacy wallet can purchase map',check(older,after),true);
