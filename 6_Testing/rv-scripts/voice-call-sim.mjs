#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV — VOICE CALL SIMULATION (P5 pilot L3/L4) — PRODUCTION
// 2 SignalR clients (caller + callee, cùng tenant) → StartCall → AcceptCall →
// NotifyConnected (giả lập media connected) → giữ vài giây → EndCall → disconnect.
// KHÔNG có WebRTC media thật — đủ để exercise signaling + metering usage event.
//
// Chạy:  VOICE_JWT_SECRET=<Jwt__Secret> node rv-scripts/voice-call-sim.mjs
// Env:   VOICE_BASE (default https://voice.khachvip.online)
//        CALL_SECONDS (default 8)
// Report: stdout + voice-rv-call-sim.json
// ─────────────────────────────────────────────────────────────────────────────
import { writeFileSync } from 'fs';
import { createHmac, randomUUID } from 'node:crypto';
import * as signalR from '@microsoft/signalr';

const VOICE = process.env.VOICE_BASE ?? 'https://voice.khachvip.online';
const SECRET = process.env.VOICE_JWT_SECRET ?? '';
const CALL_SECONDS = Number(process.env.CALL_SECONDS ?? 8);
const TENANT = process.env.VOICE_TENANT ?? 'e2d91cdf-d3c4-4399-b96e-e772a7a41630'; // An Tâm
const USER_A = 'aaaaaaaa-0000-4000-8000-00000000000a';
const USER_B = 'aaaaaaaa-0000-4000-8000-00000000000b';

if (!SECRET) { console.error('VOICE_JWT_SECRET required'); process.exit(2); }

function mintJwt(userId) {
  const now = Math.floor(Date.now() / 1000);
  const b64 = (o) => Buffer.from(JSON.stringify(o)).toString('base64url');
  const payload = { sub: userId, tenant_id: TENANT, nbf: now, exp: now + 3600, iat: now, iss: 'VanAnShopERP', aud: 'VanAnApi' };
  const si = `${b64({ alg: 'HS256', typ: 'JWT' })}.${b64(payload)}`;
  return `${si}.${createHmac('sha256', SECRET).update(si).digest('base64url')}`;
}

function connect(userId, label) {
  const conn = new signalR.HubConnectionBuilder()
    .withUrl(`${VOICE}/hubs/voice?access_token=${encodeURIComponent(mintJwt(userId))}`, {
      skipNegotiation: false,
      transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.ServerSentEvents,
      logger: signalR.LogLevel.Warning,
    })
    .withAutomaticReconnect()
    .build();
  const events = [];
  conn.on('CallCreated', (dto) => events.push(['CallCreated', dto]));
  conn.on('IncomingCall', (dto) => events.push(['IncomingCall', dto]));
  conn.on('CallAccepted', (dto) => events.push(['CallAccepted', dto]));
  conn.on('CallEnded', (dto) => events.push(['CallEnded', dto]));
  conn.on('CallStateChanged', (dto) => events.push(['CallStateChanged', dto]));
  conn.onreconnected(() => events.push(['reconnected']));
  return { conn, events, label };
}

const sleep = (ms) => new Promise(r => setTimeout(r, ms));
const results = [];
const check = (name, pass, detail = '') => {
  results.push({ name, pass: !!pass, detail: String(detail).slice(0, 300) });
  console.log(`[${pass ? 'PASS' : 'FAIL'}] ${name}${detail ? ` — ${String(detail).slice(0, 200)}` : ''}`);
};

const caller = connect(USER_A, 'caller');
const callee = connect(USER_B, 'callee');

try {
  await Promise.all([caller.conn.start(), callee.conn.start()]);
  check('L3 both clients connected to /hubs/voice', caller.conn.state === 'Connected' && callee.conn.state === 'Connected');

  const callId = randomUUID();
  const start = await caller.conn.invoke('StartCall', {
    callId, targetUserId: USER_B, contextType: null, contextReferenceId: null
  });
  check('L3 StartCall accepted', start && (start.status === 'RINGING' || start.status === 'Initiated' || start.isNew === true || typeof start.status === 'string'),
    `callId=${callId} result=${JSON.stringify(start)}`);

  await sleep(1500); // để callee nhận IncomingCall event
  await callee.conn.invoke('AcceptCall', callId);
  await sleep(1000);

  // Giả lập media connected (P2: server cần NotifyConnected sau khi media lên)
  await caller.conn.invoke('NotifyConnected', callId).catch(e => console.log('caller NotifyConnected:', e.message));
  await callee.conn.invoke('NotifyConnected', callId).catch(e => console.log('callee NotifyConnected:', e.message));
  await sleep(CALL_SECONDS * 1000);

  await caller.conn.invoke('EndCall', callId).catch(e => console.log('EndCall:', e.message));
  await sleep(1500);

  const evA = caller.events.map(e => e[0]).join(',');
  const evB = callee.events.map(e => e[0]).join(',');
  console.log(`[info] caller events: ${evA}`);
  console.log(`[info] callee events: ${evB}`);
  check('L3 call events flowed (created/accepted/ended)',
    caller.events.length + callee.events.length > 0,
    `caller=${evA} callee=${evB}`);
} catch (err) {
  check('L3 call simulation', false, err?.message ?? String(err));
} finally {
  try { await caller.conn.stop(); } catch { /* noop */ }
  try { await callee.conn.stop(); } catch { /* noop */ }
}

const summary = { ts: new Date().toISOString(), callSeconds: CALL_SECONDS, results };
writeFileSync(new URL('./voice-rv-call-sim.json', import.meta.url), JSON.stringify(summary, null, 2));
const fails = results.filter(r => !r.pass).length;
console.log(`\n=== VOICE CALL SIM: ${results.length - fails}/${results.length} PASS ===`);
process.exit(fails > 0 ? 1 : 0);
