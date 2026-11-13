#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV — MESSAGING PUSH E2E (2026-11-13 fix) — PRODUCTION api2.khachvip.online
// Verifies the RealtimeController fan-out fix: a STAFF reply to the shared Shop
// thread must reach the CUSTOMER's per-user SignalR group (previously only the
// sender's own group was pushed → customers never saw messages without F5).
// Also checks customer→staff direction still works via the shared group.
//
// Chạy: VOICE_JWT_SECRET=<Jwt__Secret> VOICE_CUSTOMER_TOKEN=<token> \
//        VOICE_STAFF_ID=<staff-guid> node messaging-push-e2e.mjs
// ─────────────────────────────────────────────────────────────────────────────
import { createHmac, randomUUID } from 'node:crypto';
import * as signalR from '@microsoft/signalr';

const API = process.env.API_BASE ?? 'https://api2.khachvip.online';
const SECRET = process.env.VOICE_JWT_SECRET ?? '';
const CUST_TOKEN = process.env.VOICE_CUSTOMER_TOKEN ?? '';
const STAFF_ID = process.env.VOICE_STAFF_ID ?? '5d23ffc3-5ad9-4f19-8af5-401015334882';
const TENANT = process.env.VOICE_TENANT ?? 'e2d91cdf-d3c4-4399-b96e-e772a7a41630';

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

const sleep = (ms) => new Promise(r => setTimeout(r, ms));
const results = [];
const check = (name, pass, detail = '') => {
  results.push({ name, pass: !!pass });
  console.log(`[${pass ? 'PASS' : 'FAIL'}] ${name}${detail ? ` — ${String(detail).slice(0, 200)}` : ''}`);
};

function connectMessaging(qs, label) {
  const conn = new signalR.HubConnectionBuilder()
    .withUrl(`${API}/hubs/messaging?${qs}`, {
      skipNegotiation: false,
      transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.ServerSentEvents,
      logger: signalR.LogLevel.Warning,
    })
    .withAutomaticReconnect()
    .build();
  const events = [];
  conn.on('ReceiveMessage', (messageId, senderId, content, sentAt) =>
    events.push({ messageId, senderId, content, sentAt }));
  return { conn, events, label };
}

async function sendHttp(path, { method = 'GET', body, headers = {} }) {
  const resp = await fetch(`${API}${path}`, {
    method,
    headers: { 'Content-Type': 'application/json', ...headers },
    body: body ? JSON.stringify(body) : undefined
  });
  return { status: resp.status, json: await resp.json().catch(() => null) };
}

const staffJwt = mintStaffJwt(STAFF_ID);
const staff = connectMessaging(`access_token=${encodeURIComponent(staffJwt)}`, 'staff');
const customer = connectMessaging(`customerToken=${encodeURIComponent(CUST_TOKEN)}`, 'customer');

try {
  await Promise.all([staff.conn.start(), customer.conn.start()]);
  check('both messaging clients Connected',
    staff.conn.state === 'Connected' && customer.conn.state === 'Connected',
    `staff=${staff.conn.state} customer=${customer.conn.state}`);

  await Promise.all([
    staff.conn.invoke('JoinConversation', 'Shop', TENANT),
    customer.conn.invoke('JoinConversation', 'Shop', TENANT),
  ]);
  await sleep(500);

  // Direction 1: STAFF sends → customer MUST receive via per-user group (the fix).
  const staffBefore = { s: staff.events.length, c: customer.events.length };
  const tag1 = `rv-staff-${Date.now()}`;
  const r1 = await sendHttp('/api/realtime/conversations/messages', {
    method: 'POST',
    headers: { Authorization: `Bearer ${staffJwt}` },
    body: { subjectType: 'Shop', subjectId: TENANT, content: tag1 }
  });
  check('staff POST message → 200', r1.status === 200, `status=${r1.status} ${JSON.stringify(r1.json)?.slice(0, 120)}`);
  await sleep(2500);
  const staffGot1 = staff.events.slice(staffBefore.s).some(e => e.content === tag1);
  const custGot1 = customer.events.slice(staffBefore.c).some(e => e.content === tag1);
  check('staff→customer: customer received push (per-user group)', custGot1,
    custGot1 ? '' : `customer events=${customer.events.slice(staffBefore.c).map(e => e.content).join('|') || 'none'}`);
  check('staff→customer: staff received push (shared group)', staffGot1);

  // Direction 2: CUSTOMER sends → staff receives (shared group) + customer echo own group.
  const before2 = { s: staff.events.length, c: customer.events.length };
  const tag2 = `rv-cust-${Date.now()}`;
  const r2 = await sendHttp('/api/realtime/conversations/messages', {
    method: 'POST',
    headers: { 'X-Customer-Token': CUST_TOKEN },
    body: { subjectType: 'Shop', subjectId: TENANT, content: tag2 }
  });
  check('customer POST message → 200', r2.status === 200, `status=${r2.status} ${JSON.stringify(r2.json)?.slice(0, 120)}`);
  await sleep(2500);
  const staffGot2 = staff.events.slice(before2.s).some(e => e.content === tag2);
  const custGot2 = customer.events.slice(before2.c).some(e => e.content === tag2);
  check('customer→staff: staff received push', staffGot2);
  check('customer→staff: customer got own echo', custGot2);
} catch (err) {
  check('messaging E2E', false, err?.message ?? String(err));
} finally {
  try { await staff.conn.stop(); } catch {}
  try { await customer.conn.stop(); } catch {}
}

const fails = results.filter(r => !r.pass).length;
console.log(`\n=== MESSAGING PUSH E2E: ${results.length - fails}/${results.length} PASS ===`);
process.exit(fails > 0 ? 1 : 0);
