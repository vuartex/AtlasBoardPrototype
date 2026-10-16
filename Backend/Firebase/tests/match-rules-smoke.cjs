'use strict';
// AtlasBoard Spark RTDB permissions smoke. Runs ONLY against localhost emulators.
// No dependencies outside Node 18+ and firebase-tools CLI. Never touches live RTDB.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');

const project = process.env.GCLOUD_PROJECT || 'demo-atlasboard';
const namespace = `${project}-default-rtdb`;
const authHost = process.env.FIREBASE_AUTH_EMULATOR_HOST || '127.0.0.1:9099';
const dbHost = process.env.FIREBASE_DATABASE_EMULATOR_HOST || '127.0.0.1:9000';
const safeHost = host => /^(localhost|127\.0\.0\.1):(\d{2,5})$/.test(host);
if (!safeHost(authHost) || !safeHost(dbHost) || !project.startsWith('demo-')) {
  console.error('ATB_RULES_QA REFUSED: demo-* project and localhost emulators required.');
  process.exit(2);
}

const sessionId = crypto.randomUUID().replaceAll('-', '');
const roomId = crypto.randomUUID().replaceAll('-', '');
const matchId = crypto.randomUUID().replaceAll('-', '');
const authURL = `http://${authHost}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=demo-api-key`;
const dbURL = `http://${dbHost}`;
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
let stage = 'startup';
async function signInAnonymous() {
  const resp = await fetch(authURL, {
    method: 'POST', headers: {'Content-Type':'application/json'},
    body: JSON.stringify({returnSecureToken: true})
  });
  const body = await resp.json();
  assert.equal(resp.status, 200, `Auth emulator sign-up ${resp.status}: ${JSON.stringify(body).slice(0,150)}`);
  assert.ok(body.localId && body.idToken, 'Auth emulator returned no identity');
  return {uid:body.localId, token:body.idToken};
}
async function rtRequest(path, method, identity, value, headers={}) {
  const url = `${dbURL}/${path}.json?ns=${encodeURIComponent(namespace)}&auth=${encodeURIComponent(identity.token)}`;
  const opts = {method, headers: {...headers}};
  if (value !== undefined) {
    opts.headers['Content-Type'] = 'application/json';
    opts.body = JSON.stringify(value);
  }
  const resp = await fetch(url, opts);
  const text = await resp.text();
  return {status:resp.status, body:text, etag:resp.headers.get('etag')};
}
function requireStatus(response, expected, name) {
  assert.equal(response.status, expected,
    `${name} expected=${expected} actual=${response.status}, response=${response.body.slice(0,180)}`);
}
async function run() {
  const host = await signInAnonymous();
  const outsider = await signInAnonymous();
  let metaReady = false;
  const now = Date.now();
  try {
    stage = 'room_meta_create';
    requireStatus(await rtRequest(`roomMeta/${roomId}`, 'PUT', host, {
      hostUid:host.uid, mode:'privateCasualOnline', createdAt:now, updatedAt:now,
      closed:false, maxPlayers:2, mapId:'Turkey', roundLimit:20
    }), 200, stage);
    metaReady = true;
    stage = 'room_host_member_create';
    requireStatus(await rtRequest(`roomMembers/${roomId}/${host.uid}`, 'PUT', host, {
      uid:host.uid, active:true, role:'host', joinedAt:now, displayName:'QA Host'
    }), 200, stage);
    stage = 'match_absent_etag_read';
    // Conditional PUT may need to establish the absent resource's ETag.
    // The nonexistent match contains no private data to disclose.
    const absent = await rtRequest(`matchState/${matchId}`, 'GET', host, undefined,
      {'X-Firebase-ETag':'true'});
    requireStatus(absent, 200, stage);
    assert.equal(absent.body.trim(), 'null');
    stage = 'match_atomic_create';
    const payload = {
      matchId, lobbyId:roomId, hostUid:host.uid, status:'active', phase:'starting',
      turnSeatId:'', snapshotJson:'',
      seatsJson:JSON.stringify({seats:[{uid:host.uid, ownerUid:host.uid,
        seatId:'seat_0',slotIndex:0,mode:'hostlocal',name:'QA Host',host:true,
        controller:'human',connection:'connected',afkLockedOut:false,
        removalReason:'',reconnectExpiresAt:0}]}),
      revision:0,eventSequence:0,authorityEpoch:1,networkSchemaVersion:1,
      createdAt:now,updatedAt:now,hostHeartbeatAt:now
    };
    requireStatus(await rtRequest(`matchState/${matchId}`, 'PUT', host, payload,
      {'if-match':'null_etag'}), 200, stage);
    stage = 'read_authorized';
    let read = await rtRequest(`matchState/${matchId}`, 'GET', host, undefined,
      {'X-Firebase-ETag':'true'});
    requireStatus(read, 200, stage);
    assert.ok(read.etag, 'RTDB did not supply an ETag');
    stage = 'reject_duplicate_match';
    const duplicate = await rtRequest(`matchState/${matchId}`, 'PUT', host, payload,
      {'if-match':'null_etag'});
    requireStatus(duplicate, 412, stage);
    stage = 'reject_outsider_read';
    const outsiderRead = await rtRequest(`matchState/${matchId}`, 'GET', outsider);
    assert.ok([401,403].includes(outsiderRead.status), `Outsider read unexpectedly ${outsiderRead.status}`);
    stage = 'reject_outsider_write';
    const outsiderWrite = await rtRequest(`matchState/${matchId}`, 'PUT', outsider, payload);
    assert.ok([401,403].includes(outsiderWrite.status), `Outsider write unexpectedly ${outsiderWrite.status}`);
    stage = 'host_cas_publish';
    payload.revision=1; payload.eventSequence=1; payload.phase='awaiting_roll';
    payload.snapshotJson='{"schemaVersion":1,"qaOnly":true}';
    payload.updatedAt=Date.now();payload.hostHeartbeatAt=payload.updatedAt;
    requireStatus(await rtRequest(`matchState/${matchId}`, 'PUT', host, payload,
      {'if-match':read.etag}), 200, stage);
    stage = 'host_heartbeat_patch';
    requireStatus(await rtRequest(`matchState/${matchId}`, 'PATCH', host, {
      hostHeartbeatAt:Date.now(),updatedAt:Date.now()
    }), 200, stage);
    stage = 'host_read_published';
    read = await rtRequest(`matchState/${matchId}`, 'GET', host);
    requireStatus(read, 200, stage);
    assert.equal(JSON.parse(read.body).revision,1);
    console.log('ATB_RULES_QA PASS stages=auth_room_match_atomic_create_cas_publish_heartbeat_outsider_denied');
  } finally {
    stage = 'cleanup';
    // Test data stays on the local emulator. Clean only records created here.
    await rtRequest(`matchState/${matchId}`, 'DELETE', host).catch(()=>{});
    if (metaReady) {
      await rtRequest(`roomMembers/${roomId}/${host.uid}`, 'DELETE', host).catch(()=>{});
      await rtRequest(`roomMeta/${roomId}`, 'DELETE', host).catch(()=>{});
    }
  }
}
run().catch(e => { console.error(`ATB_RULES_QA FAIL stage=${stage} ${e.message}`);process.exitCode=1; });
