'use strict';
const assert = require('node:assert/strict');
const crypto = require('node:crypto');

function emulatorGuard() {
  if (!process.env.FIREBASE_DATABASE_EMULATOR_HOST ||
      !process.env.FIREBASE_AUTH_EMULATOR_HOST ||
      process.env.GCLOUD_PROJECT !== 'demo-atlasboard') {
    throw new Error('EMULATOR_ONLY: demo-atlasboard auth+database required; production is forbidden.');
  }
}

function percentile(sorted, p) {
  if (!sorted.length) return 0;
  const idx = Math.min(sorted.length - 1, Math.max(0, Math.ceil((p / 100) * sorted.length) - 1));
  return sorted[idx];
}

class QaContext {
  constructor(name) {
    emulatorGuard();
    this.name = name;
    this.dbHost = process.env.FIREBASE_DATABASE_EMULATOR_HOST;
    this.authHost = process.env.FIREBASE_AUTH_EMULATOR_HOST;
    this.base = `http://${this.dbHost}`;
    this.authBase = `http://${this.authHost}`;
    this.ns = 'demo-atlasboard-default-rtdb';
    this.seq = 0;
    this.checks = 0;
    this.httpOps = 0;
    this.metrics = [];
    this.started = Date.now();
    this.stage = 'bootstrap';
  }

  xid(prefix='x') {
    this.seq++;
    return prefix + this.seq.toString(16).padStart(31, '0');
  }

  id(prefix, min=24) {
    const raw = `${prefix}_${crypto.randomBytes(12).toString('hex')}`;
    return raw.length >= min ? raw : raw + 'x'.repeat(min - raw.length);
  }

  now() { return Date.now(); }
  hash(s) { return crypto.createHash('sha256').update(String(s)).digest('hex'); }

  setStage(name) {
    this.stage = name;
    console.log(`\n--- STAGE ${name} ---`);
  }

  async timed(label, actor, resource, fn) {
    const t0 = performance.now();
    try {
      const result = await fn();
      const ms = performance.now() - t0;
      this.metrics.push({label, actor, resource, ms, ok:true, status:result?.status ?? 0});
      return result;
    } catch (e) {
      const ms = performance.now() - t0;
      this.metrics.push({label, actor, resource, ms, ok:false, status:0});
      e.message = `[${this.name} stage=${this.stage} step=${label} actor=${actor} resource=${resource}] ${e.message}`;
      throw e;
    }
  }

  async signup(label, anonymous=false) {
    const actor = anonymous ? `${label}(anonymous)` : label;
    return this.timed('auth.signup', actor, 'AuthEmulator', async () => {
      const body = anonymous ? {returnSecureToken:true} : {
        email: `${label}.${crypto.randomBytes(4).toString('hex')}@atlasboard.invalid`,
        password: 'Atlas-Emulator-Local-Only!12',
        returnSecureToken: true
      };
      const r = await fetch(`${this.authBase}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=local-demo`, {
        method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify(body),
        signal:AbortSignal.timeout(12000)
      });
      this.httpOps++;
      const x = await r.json();
      assert(r.ok && x.idToken && x.localId, `signup failed HTTP ${r.status} ${JSON.stringify(x).slice(0,240)}`);
      this.checks++;
      return {uid:x.localId, token:x.idToken, email:body.email || '', label};
    });
  }

  async db(method, where, user, body, opts={}) {
    const actor = user === 'owner' ? 'emulator-admin' : (user?.label || user?.uid || 'anonymous');
    const token = user === 'owner' ? null : user?.token;
    const endpoint = `${this.base}/${where}.json?ns=${this.ns}${token ? '&auth=' + encodeURIComponent(token) : ''}`;
    const headers = {'Content-Type':'application/json'};
    if (user === 'owner') headers.Authorization = 'Bearer owner';
    if (opts.etag) headers['if-match'] = opts.etag;
    if (method === 'GET' && opts.wantEtag) headers['X-Firebase-ETag'] = 'true';
    return this.timed(opts.label || `${method} ${where}`, actor, where, async () => {
      const r = await fetch(endpoint, {
        method, headers,
        body: body === undefined ? undefined : JSON.stringify(body),
        signal:AbortSignal.timeout(opts.timeoutMs || 12000)
      });
      this.httpOps++;
      return {status:r.status, body:await r.text(), etag:r.headers.get('etag')};
    });
  }

  parse(result) {
    if (!result || result.body === '' || result.body === 'null') return null;
    return JSON.parse(result.body);
  }

  allow(result, label) {
    assert(result.status >= 200 && result.status < 300,
      `${label}: expected ALLOW, got HTTP ${result.status} ${String(result.body).slice(0,260)}`);
    this.checks++;
    return result;
  }

  deny(result, label) {
    assert(result.status === 401 || result.status === 403,
      `${label}: expected DENY, got HTTP ${result.status} ${String(result.body).slice(0,260)}`);
    this.checks++;
    return result;
  }

  eq(actual, expected, label) { assert.deepEqual(actual, expected, label); this.checks++; }
  ok(value, label) { assert.ok(value, label); this.checks++; }

  member(user, role, joinedAt, active=true, displayName) {
    return {uid:user.uid, active, role, joinedAt, displayName:displayName || user.label};
  }

  roomMeta(host, opts={}) {
    const at = opts.createdAt || this.now();
    return {
      hostUid:host.uid,
      mode:opts.mode || 'privateCasualOnline',
      createdAt:at,
      updatedAt:opts.updatedAt || at,
      closed:false,
      maxPlayers:opts.maxPlayers || 4,
      mapId:opts.mapId || 'Turkey',
      roundLimit:opts.roundLimit || 20,
      themeId:opts.themeId || 'classic_table',
      passwordHash:'',
      inviteCode:'',
      authorityEpoch:opts.authorityEpoch ?? 1,
      activeMatchId:opts.activeMatchId || ''
    };
  }

  seats(users, hostUid) {
    return {seats:users.map((u,i)=>({
      uid:u.uid, ownerUid:u.uid, seatId:`seat_${i}`, mode:u.uid===hostUid?'hostlocal':'remotehuman',
      name:u.label, controller:'human', connection:'connected', slotIndex:i,
      host:u.uid===hostUid, afkLockedOut:false, reconnectExpiresAt:0, disconnectedAt:0, removalReason:''
    }))};
  }

  zeroBankLoans(tileCount=32) {
    const a = new Array(tileCount*6).fill(0);
    for (let t=0;t<tileCount;t++) a[t*6] = -1;
    return a;
  }

  zeroFrame(opts={}) {
    const tileCount = opts.tileCount || 32;
    const users = opts.users || [];
    const money = [-1,-1,-1,-1];
    const bankrupt = [false,false,false,false];
    const pawns = [-1,-1,-1,-1];
    for (let i=0;i<users.length && i<4;i++) { money[i]=1500; pawns[i]=0; }
    return {
      schemaVersion:13,
      phase:opts.phase || 'playing',
      activeSlotIndex:opts.activeSlotIndex ?? 0,
      currentRound:opts.currentRound || 1,
      roundLimit:opts.roundLimit || 20,
      completedTurns:opts.completedTurns || 0,
      currentTurnOrderPosition:0,
      consecutiveDoublesThisTurn:0,
      turnOrderSlotIndices:users.map((_,i)=>i),
      completedActiveSlotsThisRound:[],
      diceSequence:0,dicePlayerSlotIndex:-1,dieOne:0,dieTwo:0,total:0,startingOrderDice:false,
      movementSequence:0,movementPlayerSlotIndex:-1,movementStartTileIndex:-1,movementTargetTileIndex:-1,
      movementSteps:0,movementInProgress:false,movementPassedStart:false,movementUsesSprint:false,
      pawnTileIndices:pawns,
      playerMoney:money,
      playerBankrupt:bankrupt,
      tileOwnerSlotIndices:new Array(tileCount).fill(-1),
      tileDevelopmentLevels:new Array(tileCount).fill(0),
      pulseLevels:new Array(tileCount*4).fill(0),
      pulseDrift:new Array(tileCount*4).fill(0),
      bankLoans:this.zeroBankLoans(tileCount),
      civicState:[-1,-1,-1,-1,-1,-1,-1,-1,-1],
      cityMemory:[1,0],
      careerDiceRolls:[0,0,0,0],careerDoublesRolled:[0,0,0,0],
      matchResult:null,
      decisionKind:'',decisionPlayerSlotIndex:-1,decisionAuxSlotIndex:-1,decisionTileIndex:-1,
      decisionValue0:0,decisionValue1:0,decisionValue2:0,decisionValue3:0,decisionReady:false,
      decisionText0:'',decisionText1:'',decisionText2:'',decisionText3:'',
      pawnCosmeticIds:['','','','']
    };
  }

  matchState(matchId, roomId, host, users, frame, opts={}) {
    const at = opts.createdAt || this.now();
    return {
      matchId,lobbyId:roomId,hostUid:host.uid,status:opts.status || 'active',phase:opts.phase || frame.phase || 'playing',
      revision:opts.revision ?? 1,eventSequence:opts.eventSequence ?? 1,
      snapshotJson:JSON.stringify(frame),seatsJson:JSON.stringify(this.seats(users,host.uid)),
      createdAt:at,updatedAt:opts.updatedAt || at,hostHeartbeatAt:opts.hostHeartbeatAt || at,
      authorityEpoch:opts.authorityEpoch ?? 1,networkSchemaVersion:1
    };
  }

  async createRoom(users, opts={}) {
    assert(users.length >= 2 && users.length <= 4);
    const host = users[0];
    const roomId = opts.roomId || this.id('room', 28);
    const at = this.now();
    let meta = this.roomMeta(host,{...opts,createdAt:at,updatedAt:at,maxPlayers:users.length});
    this.allow(await this.db('PUT',`roomMeta/${roomId}`,host,meta,{label:'room.create'}),'host creates roomMeta');
    this.allow(await this.db('PUT',`roomMembers/${roomId}/${host.uid}`,host,this.member(host,'host',at,true,host.label),{label:'room.host_member'}),'host creates own member');
    for (let i=1;i<users.length;i++) {
      const u=users[i];
      const req={uid:u.uid,requestedAt:this.now(),displayName:u.label};
      this.allow(await this.db('PUT',`joinRequests/${roomId}/${u.uid}`,u,req,{label:`join.request.${i}`}),'remote creates own join request');
      this.allow(await this.db('PUT',`roomMembers/${roomId}/${u.uid}`,host,this.member(u,'remoteHuman',at+i,true,u.label),{label:`join.admit.${i}`}),'host admits remote');
      this.allow(await this.db('DELETE',`joinRequests/${roomId}/${u.uid}`,host,undefined,{label:`join.cleanup.${i}`}),'host removes join request');
    }
    for (let i=0;i<users.length;i++) {
      const u=users[i];
      this.allow(await this.db('PUT',`presence/${roomId}/${u.uid}`,u,{online:true,lastSeenAt:this.now(),connectionId:`conn-${i}-${this.xid('c')}`},{label:`presence.${i}`}),'member writes own presence');
      this.allow(await this.db('PUT',`roomReady/${roomId}/${u.uid}`,u,{uid:u.uid,revision:1,ready:true},{label:`ready.${i}`}),'member writes own ready');
    }
    this.allow(await this.db('PUT',`roomState/${roomId}`,host,{revision:1,eventSequence:1,phase:'waiting',snapshotJson:JSON.stringify({members:users.map((u,i)=>({uid:u.uid,slotIndex:i}))}),updatedAt:this.now()},{label:'room.state'}),'host publishes roomState');
    return {roomId,host,users,meta,joinedAt:at};
  }

  async createMatch(room, opts={}) {
    const matchId = opts.matchId || this.id('match', 20);
    const frame = opts.frame || this.zeroFrame({users:room.users,roundLimit:opts.roundLimit || 20});
    let state = this.matchState(matchId,room.roomId,room.host,room.users,frame,opts);
    this.allow(await this.db('PUT',`matchState/${matchId}`,room.host,state,{label:'match.create'}),'host creates matchState');
    const meta = {...room.meta,activeMatchId:matchId,updatedAt:this.now()};
    this.allow(await this.db('PUT',`roomMeta/${room.roomId}`,room.host,meta,{label:'room.active_match'}),'host links activeMatchId');
    room.meta=meta;
    room.matchId=matchId;
    room.matchState=state;
    return {matchId,frame,state};
  }

  async getJson(path,user,label) {
    const r=this.allow(await this.db('GET',path,user,undefined,{label:label||`GET ${path}`}),label||`read ${path}`);
    return this.parse(r);
  }

  intent(uid, type, payload, extra={}) {
    return {uid:uid.uid,type,clientCommandId:extra.clientCommandId || this.id('cmd',16),payload:JSON.stringify(payload),createdAt:extra.createdAt || this.now(),...(extra.seatId?{seatId:extra.seatId}:{})};
  }

  report(extra={}) {
    const durations=this.metrics.map(x=>x.ms).sort((a,b)=>a-b);
    const failures=this.metrics.filter(x=>!x.ok || (x.status>=400 && x.status!==401 && x.status!==403));
    const byActor={};
    for(const m of this.metrics){
      if(!byActor[m.actor])byActor[m.actor]=[];
      byActor[m.actor].push(m.ms);
    }
    console.log(`\n=== ${this.name} LATENCY ===`);
    console.log(`httpOps=${this.httpOps} checks=${this.checks} durationMs=${Date.now()-this.started}`);
    console.log(`latencyMs min=${durations.length?durations[0].toFixed(1):0} median=${percentile(durations,50).toFixed(1)} p95=${percentile(durations,95).toFixed(1)} max=${durations.length?durations[durations.length-1].toFixed(1):0}`);
    for(const [actor,arr] of Object.entries(byActor)){
      arr.sort((a,b)=>a-b);
      console.log(`actor=${actor} ops=${arr.length} medianMs=${percentile(arr,50).toFixed(1)} p95Ms=${percentile(arr,95).toFixed(1)} maxMs=${arr[arr.length-1].toFixed(1)}`);
    }
    if(failures.length) console.log(`unexpectedTransportFailures=${failures.length}`);
    return {name:this.name,checks:this.checks,httpOps:this.httpOps,durationMs:Date.now()-this.started,metrics:this.metrics,...extra};
  }
}

module.exports={QaContext,emulatorGuard};
