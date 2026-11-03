'use strict';
/*
 * OWNER-ONLY, ONE-TIME RECOVERY. This tool is NOT part of the Unity runtime.
 * It does not touch wallet/career/commerce records, delete users, or select
 * accounts by display name. It writes Firestore Steam link records ONLY after
 * deliberate project-owner approval and collision checks.
 *
 * Requires Firebase Admin service-account/ADC credentials in the owner's
 * environment and firebase-admin installed in Backend/Firebase/functions.
 */
const {createHash} = require('node:crypto');
const path = require('node:path');
const PROJECT = 'atlasboard-usa';
const DATABASE = 'https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app';
const OWNER_APPROVAL = 'I_VERIFIED_STEAM_ID_AND_ORIGINAL_UID';

function parseArguments(argv) {
  if (argv.includes('--help')) return {help:true};
  const fields = new Map();
  for (let i = 0; i < argv.length; i++) {
    const token = argv[i];
    if (!token.startsWith('--')) throw new Error('Unexpected argument');
    if (token === '--apply') { fields.set('apply', true); continue; }
    if (token === '--plan') { fields.set('apply', false); continue; }
    if (i + 1 >= argv.length || argv[i + 1].startsWith('--'))
      throw new Error(`Missing value for ${token}`);
    fields.set(token.slice(2), argv[++i]);
  }
  const uid = fields.get('uid') ?? '';
  const steamId = fields.get('steam-id') ?? '';
  const appId = Number(fields.get('app-id') ?? 480);
  if (!/^[A-Za-z0-9_-]{8,128}$/.test(uid))
    throw new Error('A complete exact Firebase Authentication UID is required; never use a display name');
  if (!/^7656119\d{10}$/.test(steamId))
    throw new Error('An exact 17-digit SteamID64 is required');
  if (!Number.isSafeInteger(appId) || appId <= 0)
    throw new Error('Steam appId must be a positive integer');
  const apply = fields.get('apply') === true;
  if (apply && fields.get('owner-approval') !== OWNER_APPROVAL)
    throw new Error('Explicit project-owner attestation required for --apply');
  const allowed = new Set(['uid','steam-id','app-id','apply','owner-approval']);
  for (const field of fields.keys()) if (!allowed.has(field))
    throw new Error('Unknown argument: ' + field);
  return {uid,steamId,appId,apply};
}

function docId(uid, steamId) {
  return {provider: `steam_${steamId}`, account: `${uid}_steam`};
}
function checkCollision(uid, steamId, providerData, accountData) {
  if (providerData &&
      (providerData.accountId !== uid || providerData.providerUserId !== steamId))
    throw new Error('BLOCKED: SteamID is already mapped to a different UID; no changes made');
  if (accountData &&
      (accountData.accountId !== uid || accountData.providerUserId !== steamId))
    throw new Error('BLOCKED: original UID is already mapped to another SteamID; no changes made');
}
function eventId(uid, steamId) {
  return createHash('sha256')
    .update(`atlasboard:platform-link:${uid}:${steamId}:owner_manual_recovery_v1`, 'utf8')
    .digest('hex');
}
function loadAdmin() {
  try {return require(path.resolve(__dirname, '../functions/node_modules/firebase-admin'));}
  catch {
    throw new Error('firebase-admin not installed under Backend/Firebase/functions/node_modules; run npm install inside functions with approved dependencies first');
  }
}

async function main(argv) {
  const args = parseArguments(argv);
  if (args.help) {
    process.stdout.write('Usage: node qa/phase19br_owner_steam_uid_recovery.cjs --uid <EXACT_ORIGINAL_UID> --steam-id <STEAMID64> --app-id 480 --plan\n' +
      'Apply requires --apply --owner-approval ' + OWNER_APPROVAL + '\n' +
      'Read QA/STEAM_OWNER_RECOVERY.md before use. Never delete or merge UID records.\n');
    return;
  }
  const admin = loadAdmin();
  if (!admin.apps.length) admin.initializeApp({
    credential:admin.credential.applicationDefault(),
    projectId:PROJECT,
    databaseURL:DATABASE
  });
  const authUser = await admin.auth().getUser(args.uid);
  if (!authUser || authUser.uid !== args.uid || authUser.disabled)
    throw new Error('Original Firebase Authentication user missing or disabled; stop');
  const originalProfile = await admin.database().ref(`users/${args.uid}`).get();
  const user = originalProfile.val();
  if (!user || user.userId !== args.uid || !user.metaEconomy)
    throw new Error('Original RTDB user/wallet missing or identity contract invalid; stop');
  const ids = docId(args.uid,args.steamId);
  const firestore = admin.firestore();
  const provider = firestore.collection('platform_identity_links').doc(ids.provider);
  const account = firestore.collection('account_platform_links').doc(ids.account);
  const [p,a] = await Promise.all([provider.get(),account.get()]);
  checkCollision(args.uid,args.steamId,p.exists?p.data():null,a.exists?a.data():null);
  const wallet = user.metaEconomy.wallet || {};
  process.stdout.write(`ATB_OWNER_STEAM_RECOVERY PLAN project=${PROJECT} uid_prefix=${args.uid.slice(0,8)} steam_id=${args.steamId} app_id=${args.appId} `+
    `gold=${Number(wallet.gold)||0} atlas_coin=${Number(wallet.atlasCoin)||0} provider_exists=${p.exists} account_exists=${a.exists} `+
    `write=${args.apply} wallet_mutation=false\n`);
  if (!args.apply) return;
  if (p.exists && a.exists && p.data().verified === true && a.data().verified === true) {
    process.stdout.write('ATB_OWNER_STEAM_RECOVERY IDEMPOTENT already_verified=true write=false\n');
    return;
  }
  // Preflight may race with a concurrent link. Repeat collision checks within
  // the transaction. Never steal a mapping or change currency/inventory.
  await firestore.runTransaction(async tx => {
    const [ps,as] = await Promise.all([tx.get(provider),tx.get(account)]);
    checkCollision(args.uid,args.steamId,ps.exists?ps.data():null,as.exists?as.data():null);
    const now = admin.firestore.FieldValue.serverTimestamp();
    const common = {
      schemaVersion:1,provider:'steam',providerUserId:args.steamId,
      ownerProviderUserId:args.steamId,accountId:args.uid,appId:args.appId,
      verified:true,developmentOnly:false,
      verificationMode:'owner_manual_recovery_v1',
      vacBanned:false,publisherBanned:false,updatedAt:now,
      ...(ps.exists?{}:{createdAt:now})
    };
    tx.set(provider,common,{merge:true});
    tx.set(account,{...common,...(as.exists?{}:{createdAt:now})},{merge:true});
    const event = firestore.collection('platform_link_events').doc(eventId(args.uid,args.steamId));
    tx.set(event,{
      schemaVersion:1,eventType:'steam_owner_manual_recovery',
      provider:'steam',providerUserId:args.steamId,
      ownerProviderUserId:args.steamId,accountId:args.uid,
      appId:args.appId,verified:true,developmentOnly:false,
      verificationMode:'owner_manual_recovery_v1',
      proofHash:'owner_attestation_not_steam_ticket',serverTime:now
    },{merge:true});
  });
  process.stdout.write('ATB_OWNER_STEAM_RECOVERY APPLIED firestore_mapping=true original_uid_preserved=true wallet_untouched=true\n');
  process.stdout.write('NEXT: verify Steam ticket callable and Firebase custom-token sign-in; this operation does not deploy Functions.\n');
}

if (require.main === module) main(process.argv.slice(2)).catch(e=>{
  process.stderr.write('ATB_OWNER_STEAM_RECOVERY ERROR '+e.message+'\n');
  process.exitCode=1;
});
module.exports={parseArguments,docId,checkCollision,eventId};
