#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV — VOICE CHAT-FIRST CALL E2E (S8/S9) — PRODUCTION
// Customer (DataProtection customer token) + Shop staff (staff JWT) both connect
// to /hubs/voice, then CUSTOMER calls with EMPTY targetUserId + chat context —
// server must resolve counterpart = the staff participant who most recently
// sent a message in the thread AND has a live voice connection.
// Then staff→customer direction is exercised on a second call.
//
// Chạy:  VOICE_JWT_SECRET=<Jwt__Secret> VOICE_CUSTOMER_TOKEN=<token> \
//          VOICE_STAFF_ID=<staff-guid> node voice-chat-e2e.mjs
// Env:   VOICE_BASE, CHAT_CONTEXT (default Shop:e2d91cdf-...), CALL_SECONDS
// ─────────────────────────────────────────────────────────────────────────────
import { randomUUID } from 'node:crypto';
import { createHmac } from 'node:crypto';
import * as signalR from '@microsoft/signalr';

const VOICE = process.env.VOICE_BASE ?? 'https://voice.khachvip.online';
const SECRET = process.env.VOICE_JWT_SECRET ?? '';
const CUST_TOKEN = process.env.VOICE_CUSTOMER_TOKEN ?? '';
const STAFF_ID = process.env.VOICE_STAFF_ID ?? 'bd144983-dd13-4fb0-8f60-aa0620bea1bf';
const TENANT = process.env.VOICE_TENANT ?? 'e2d91cdf-d3c4-4399-b96e-e772a7a41630';
const CTX = process.env.CHAT_CONTEXT ?? `Shop:${TENANT}`;
const CALL_SECONDS = Number(process.env.CALL_SECONDS ?? 5);
const CUSTOMER_ID = process.env.VOICE_CUSTOMER_ID ?? 'aa260799-fa5e-4cb9-9a8b-b4e7487e0bd8';

if (!SECRET || !CUST_TOKEN) { console.error('VOICE_JWT_SECRET + VOICE_CUSTOMER_TOKEN required'); process.exit(2); }

function mintStaffJwt(userId) {
  const now = Math.floor(Date.now() / 1000);
  const b64 = (o) => Buffer.from(JSON.stringify(o)).toString('base64url');
  const payload = {
    sub: userId, email: 'rv@vanan.vn', tenant_id: TENANT, TenantId: TENANT,
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'Owner',
    nbf: now, exp: now + 3600, iat: now, iss: 'VanAnShopERP', aud: 'VanAnApi'
  };
  const si = `${b64({ alg: 'HS256', typ: 'JWT' })}.${b64(payload)}`;
  return `${si}.${createHmac('sha256', SECRET).update(si).digest('base64url')}`;
}

function connect(token, label) {
  const conn = new signalR.HubConnectionBuilder()
    .withUrl(`${VOICE}/hubs/voice?access_token=${encodeURIComponent(token)}`, {
      skipNegotiation: false,
      transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.ServerSentEvents,
      logger: signalR.LogLevel.Warning,
    })
    .withAutomaticReconnect()
    .build();
  const events = [];
  for (const name of ['CallCreated', 'IncomingCall', 'CallAccepted', 'CallEnded', 'CallConnected', 'CallStateChanged', 'CallOffline'])
    conn.on(name, (dto) => events.push([name, dto]));
  return { conn, events, label };
}

const sleep = (ms) => new Promise(r => setTimeout(r, ms));
const results = [];
const check = (name, pass, detail = '') => {
  results.push({ name, pass: !!pass });
  console.log(`[${pass ? 'PASS' : 'FAIL'}] ${name}${detail ? ` — ${String(detail).slice(0, 220)}` : ''}`);
};

const staff = connect(mintStaffJwt(STAFF_ID), 'staff');
const customer = connect(CUST_TOKEN, 'customer');

async function runCall(direction, caller, callee, callerLabel, calleeLabel) {
  const callId = randomUUID();
  const calleeBefore = callee.events.length;
  const r = await caller.conn.invoke('StartCall', {
    callId, targetUserId: '00000000-0000-0000-0000-000000000000',
    contextType: 'chat', contextReferenceId: CTX
  });
  check(`${direction}: StartCall → ${r?.status ?? JSON.stringify(r)}`,
    ['RINGING', 'Initiated'].includes(r?.status) || r?.isNew === true,
    JSON.stringify(r)?.slice(0, 200));
  await sleep(1500);
  const incoming = callee.events.slice(calleeBefore).filter(e => e[0] === 'IncomingCall');
  check(`${direction}: callee got IncomingCall`, incoming.length > 0,
    incoming.length ? `callId=${incoming[0][1]?.callId ?? callId}` : `events=${callee.events.slice(calleeBefore).map(e => e[0]).join(',') || 'none'}`);
  if (incoming.length === 0) return false;

  await callee.conn.invoke('AcceptCall', callId);
  await sleep(800);
  const sdp = 'v=0\r\no=- 0 0 IN IP4 127.0.0.1\r\ns=-\r\nt=0 0\r\n';
  await caller.conn.invoke('SendOffer', { callId, sdp }).catch(e => console.log('SendOffer:', e.message));
  await sleep(400);
  await callee.conn.invoke('SendAnswer', { callId, sdp }).catch(e => console.log('SendAnswer:', e.message));
  await sleep(400);
  await caller.conn.invoke('NotifyConnected', callId).catch(() => {});
  await callee.conn.invoke('NotifyConnected', callId).catch(() => {});
  await sleep(1500);
  const connected = [...caller.events, ...callee.events].some(e => e[0] === 'CallConnected');
  check(`${direction}: reached CONNECTED`, connected,
    `caller=${caller.events.map(e => e[0]).slice(-4).join(',')} callee=${callee.events.map(e => e[0]).slice(-4).join(',')}`);
  await caller.conn.invoke('EndCall', callId).catch(() => {});
  await sleep(800);
  return connected;
}

try {
  await Promise.all([staff.conn.start(), customer.conn.start()]);
  check('both clients Connected', staff.conn.state === 'Connected' && customer.conn.state === 'Connected');

  // Direction 1: customer → staff (the production failing direction)
  await runCall('customer→staff', customer, staff, 'customer', 'staff');
  await sleep(1000);
  // Direction 2: staff → customer
  await runCall('staff→customer', staff, customer, 'staff', 'customer');
} catch (err) {
  check('E2E call flow', false, err?.message ?? String(err));
} finally {
  try { await staff.conn.stop(); } catch {}
  try { await customer.conn.stop(); } catch {}
}

const fails = results.filter(r => !r.pass).length;
console.log(`\n=== VOICE CHAT E2E: ${results.length - fails}/${results.length} PASS ===`);
process.exit(fails > 0 ? 1 : 0);
