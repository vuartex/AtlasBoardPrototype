'use strict';
const {SecurityQa,clone}=require('./phase19bj_security_shared.cjs');
function leaves(obj,prefix='',out=[]){for(const[k,v]of Object.entries(obj||{})){const p=prefix?`${prefix}/${k}`:k;if(v&&typeof v==='object'&&!Array.isArray(v))leaves(v,p,out);else if(typeof v==='boolean')out.push(p);}return out;}
function setPath(obj,path,value){const a=path.split('/');let x=obj;for(let i=0;i<a.length-1;i++)x=x[a[i]];x[a[a.length-1]]=value;}
async function run(){
 const q=new SecurityQa('PHASE19BJ_04_WALLET_INVENTORY_FUZZ');console.log('ATB_PHASE19BJ_04 START production=FORBIDDEN');
 q.setStage('auth_profiles_wallet_init');
 const alice=await q.signup('Sec04-Alice');const attacker=await q.signup('Sec04-Attacker');const guest=await q.signup('Sec04-Guest',true);
 q.allow(await q.db('PUT',`users/${alice.uid}`,'owner',q.profile(alice,'Alice'),{label:'seed.alice_profile'}),'seed Alice profile');
 q.allow(await q.db('PUT',`users/${guest.uid}`,'owner',q.profile(guest,'Guest'),{label:'seed.guest_profile'}),'seed Guest profile');
 let w=q.startWallet();q.allow(await q.db('PUT',`users/${alice.uid}/metaEconomy`,alice,w,{label:'wallet.init'}),'registered wallet INIT allowed');
 q.deny(await q.db('PUT',`users/${alice.uid}/metaEconomy`,attacker,w,{label:'wallet.cross_uid_init'}),'foreign UID wallet write denied');
 q.deny(await q.db('PUT',`devWalletTesters/${alice.uid}`,alice,true,{label:'tester.self_grant'}),'client cannot self grant dev tester');
 q.allow(await q.db('PUT',`devWalletTesters/${alice.uid}`,'owner',true,{label:'fixture.tester_grant'}),'admin fixture grants tester permission');
 q.setStage('legitimate_dev_daily_purchase');
 let grant=q.advance(w,'DEV_GRANT');grant.wallet.gold+=5000;grant.wallet.atlasCoin+=500;grant.dev.grantCount+=1;
 q.allow(await q.db('PUT',`users/${alice.uid}/metaEconomy`,alice,grant,{label:'wallet.dev_grant'}),'exact DEV_GRANT allowed');w=grant;
 let daily=q.advance(w,'DAILY');daily.wallet.gold+=50;daily.dailyReward.lastClaimAt=daily.operation.atMs;
 q.allow(await q.db('PUT',`users/${alice.uid}/metaEconomy`,alice,daily,{label:'wallet.daily'}),'exact DAILY allowed');w=daily;
 let dailyReplay=q.advance(w,'DAILY');dailyReplay.wallet.gold+=50;dailyReplay.dailyReward.lastClaimAt=dailyReplay.operation.atMs;
 q.deny(await q.db('PUT',`users/${alice.uid}/metaEconomy`,alice,dailyReplay,{label:'wallet.daily_replay'}),'daily replay inside 24h denied');
 const prices=q.priceMap();q.ok(prices.frame_halloween===350,'price parser sees frame_halloween=350');const item='frame_halloween',price=prices[item];
 let wrong=q.advance(w,'PURCHASE',item);wrong.wallet.gold-=price-1;wrong.inventory.profile_frames[item]=true;
 q.deny(await q.db('PUT',`users/${alice.uid}/metaEconomy`,alice,wrong,{label:'purchase.wrong_price'}),'underpay purchase denied');
 let purchase=q.advance(w,'PURCHASE',item);purchase.wallet.gold-=price;purchase.inventory.profile_frames[item]=true;
 q.allow(await q.db('PUT',`users/${alice.uid}/metaEconomy`,alice,purchase,{label:'purchase.valid'}),'exact server-priced purchase allowed');w=purchase;
 let dup=q.advance(w,'PURCHASE',item);dup.wallet.gold-=price;
 q.deny(await q.db('PUT',`users/${alice.uid}/metaEconomy`,alice,dup,{label:'purchase.duplicate'}),'duplicate owned purchase denied');
 let extra=q.advance(w,'PURCHASE','dice_ruby_red');extra.wallet.gold-=prices.dice_ruby_red;extra.inventory.dice.dice_ruby_red=true;extra.inventory.pawns.ghost_01=true;
 q.deny(await q.db('PUT',`users/${alice.uid}/metaEconomy`,alice,extra,{label:'purchase.extra_entitlement'}),'purchase cannot smuggle second entitlement');
 let coin=q.advance(w,'DEV_GRANT');coin.wallet.gold+=5000;coin.wallet.atlasCoin+=501;coin.dev.grantCount+=1;
 q.deny(await q.db('PUT',`users/${alice.uid}/metaEconomy`,alice,coin,{label:'grant.atlascoin_tamper'}),'DEV_GRANT exact Atlas Coin delta enforced');
 let raw=q.advance(w,'DEV_GRANT');raw.wallet.gold+=999999;raw.wallet.atlasCoin+=500;raw.dev.grantCount+=1;
 q.deny(await q.db('PUT',`users/${alice.uid}/metaEconomy`,alice,raw,{label:'grant.gold_tamper'}),'arbitrary Gold mint denied');
 q.setStage('meta_history_receipt_binding');
 const receipt={id:w.operation.id,type:w.operation.type,itemId:w.operation.itemId,atMs:w.operation.atMs};
 q.allow(await q.db('PUT',`users/${alice.uid}/metaHistory/${receipt.id}`,alice,receipt,{label:'history.purchase_receipt'}),'receipt matching current operation allowed');
 q.deny(await q.db('PUT',`users/${alice.uid}/metaHistory/${receipt.id}`,alice,receipt,{label:'history.receipt_replay'}),'receipt immutable/replay denied');
 q.deny(await q.db('PUT',`users/${alice.uid}/metaHistory/${q.xid()}`,alice,{...receipt,id:q.xid(),itemId:'dice_ruby_red'},{label:'history.forged_receipt'}),'receipt not matching wallet operation denied');
 q.deny(await q.db('GET',`users/${alice.uid}/metaHistory`,attacker,undefined,{label:'history.foreign_read'}),'foreign transaction history read denied');
 q.setStage('inventory_mutation_matrix');
 const bools=leaves(w.inventory);let mutationAttempts=0;
 for(const p of bools){let cur=w.inventory;for(const k of p.split('/'))cur=cur[k];if(cur===true)continue;const candidate=q.advance(w,'DEV_GRANT');candidate.wallet.gold+=5000;candidate.wallet.atlasCoin+=500;candidate.dev.grantCount+=1;setPath(candidate.inventory,p,true);q.deny(await q.db('PUT',`users/${alice.uid}/metaEconomy`,alice,candidate,{label:`fuzz.inventory.${p.replaceAll('/','.')}`}),`DEV_GRANT cannot flip inventory ${p}`);mutationAttempts++;}
 q.ok(mutationAttempts>=20,`broad inventory mutation matrix executed (${mutationAttempts})`);
 for(const [label,mutate] of [
  ['equipped_frame',x=>x.equipped.profileFrameId='frame_halloween'],['equipped_board',x=>x.equipped.boardId='board_midnight_azure'],['equipped_dice',x=>x.equipped.diceId='dice_ruby_red'],['starter_flag',x=>x.wallet.starterGrantClaimed=true],['schema',x=>x.schemaVersion=2],['reward_history',x=>x.rewardHistory='|forged|']]){
   const c=q.advance(w,'DEV_GRANT');c.wallet.gold+=5000;c.wallet.atlasCoin+=500;c.dev.grantCount+=1;mutate(c);q.deny(await q.db('PUT',`users/${alice.uid}/metaEconomy`,alice,c,{label:`fuzz.${label}`}),`DEV_GRANT invariant ${label}`);
 }
 q.setStage('equip_and_etag_concurrency');
 const read=q.allow(await q.db('GET',`users/${alice.uid}/metaEconomy`,alice,undefined,{label:'etag.read',wantEtag:true}),'read wallet ETag');q.ok(!!read.etag,'ETag returned');
 let equip=q.advance(w,'EQUIP',item);equip.equipped.profileFrameId=item;
 q.allow(await q.db('PUT',`users/${alice.uid}/metaEconomy`,alice,equip,{label:'equip.owned_frame',etag:read.etag}),'owned frame equip with fresh ETag allowed');w=equip;
 let stale=q.advance(w,'EQUIP','');stale.equipped.profileFrameId='';
 q.precondition(await q.db('PUT',`users/${alice.uid}/metaEconomy`,alice,stale,{label:'etag.stale_write',etag:read.etag}),'stale concurrent wallet write rejected');
 let badEquip=q.advance(w,'EQUIP','frame_new_year');badEquip.equipped.profileFrameId='frame_new_year';
 q.deny(await q.db('PUT',`users/${alice.uid}/metaEconomy`,alice,badEquip,{label:'equip.unowned_frame'}),'cannot equip unowned frame');
 const publicProfile={displayName:'Alice',avatarId:'default',profileFrameId:item,diceSkinId:'',updatedAt:q.now()};
 q.allow(await q.db('PUT',`publicProfiles/${alice.uid}`,alice,publicProfile,{label:'public.equipped_frame'}),'public projection accepts server-owned equipped frame');
 q.setStage('anonymous_purchase_gate');
 let gw=q.startWallet();q.allow(await q.db('PUT',`users/${guest.uid}/metaEconomy`,guest,gw,{label:'guest.wallet_init'}),'anonymous zero wallet init allowed');
 gw=clone(gw);gw.wallet.gold=1000;q.allow(await q.db('PUT',`users/${guest.uid}/metaEconomy`,'owner',gw,{label:'fixture.guest_gold'}),'admin fixture gives test balance without changing rules');
 let gp=q.advance(gw,'PURCHASE',item);gp.wallet.gold-=price;gp.inventory.profile_frames[item]=true;
 q.deny(await q.db('PUT',`users/${guest.uid}/metaEconomy`,guest,gp,{label:'guest.purchase_not_allowlisted'}),'ordinary anonymous Guest cannot spend/mutate paid inventory');
 q.allow(await q.db('PUT',`devWalletTesters/${guest.uid}`,'owner',true,{label:'fixture.guest_tester'}),'admin explicitly allowlists QA guest');
 q.allow(await q.db('PUT',`users/${guest.uid}/metaEconomy`,guest,gp,{label:'guest.purchase_allowlisted'}),'explicit tester Guest uses same exact-price rule');
 const report=q.report({mutationAttempts,priceItems:Object.keys(prices).length});
 console.log(`ATB_PHASE19BJ_04_WALLET_INVENTORY_FUZZ PASS checks=${report.checks} httpOps=${report.httpOps} durationMs=${report.durationMs} inventoryMutationAttempts=${mutationAttempts} etag412=true anonymousGate=true production=NOT_TOUCHED`);return report;
}
module.exports={run};if(require.main===module)run().catch(e=>{console.error(e.stack||e);process.exitCode=1;});
