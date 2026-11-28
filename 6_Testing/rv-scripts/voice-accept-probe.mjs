#!/usr/bin/env node
// PROBE — raw SignalR AcceptCall path (no Blazor/JS SDK).
// customer_token + minted staff JWT (sub = real staff participant id).
// Env: VOICE_JWT_SECRET, VOICE_CUSTOMER_TOKEN, VOICE_STAFF_ID, VOICE_TENANT
import { createHmac, randomUUID } from 'node:crypto';
import * as signalR from '@microsoft/signalr';
import fs from 'fs';

const VOICE = 'https://voice.khachvip.online';
const SECRET = process.env.VOICE_JWT_SECRET ?? '';
const CUST = JSON.parse(fs.readFileSync('../.devin/rv_cust_token.json', 'utf8')).customerToken;
const STAFF_ID = process.env.VOICE_STAFF_ID ?? 'f01b9649-938f-4cd1-af71-6fe5d3700e88';
const TENANT = process.env.VOICE_TENANT ?? 'e2d91cdf-d3c4-4399-b96e-e772a7a41630';
if (!SECRET) { console.error('VOICE_JWT_SECRET required'); process.exit(2); }

const b64 = (o) => Buffer.from(JSON.stringify(o)).toString('base64url');
function mintJwt() {
  const now = Math.floor(Date.now() / 1000);
  const payload = { sub: STAFF_ID, tenant_id: TENANT, DisplayName: 'RV Probe Staff', nbf: now, exp: now + 3600, iat: now, iss: 'VanAnShopERP', aud: 'VanAnApi' };
  const si = `${b64({ alg: 'HS256', typ: 'JWT' })}.${b64(payload)}`;
  return `${si}.${createHmac('sha256', SECRET).update(si).digest('base64url')}`;
}
const sleep = (ms) => new Promise(r => setTimeout(r, ms));

function connect(token, label) {
  const conn = new signalR.HubConnectionBuilder()
    .withUrl(`${VOICE}/hubs/voice?access_token=${encodeURIComponent(token)}`, {
      skipNegotiation: false,
      transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.ServerSentEvents,
      logger: signalR.LogLevel.Warning,
    }).withAutomaticReconnect().build();
  conn.on('IncomingCall', d => console.log(`[${label}] IncomingCall`, JSON.stringify(d).slice(0, 300)));
  conn.on('CallAccepted', d => console.log(`[${label}] CallAccepted`, JSON.stringify(d)));
  conn.on('CallCreated', d => console.log(`[${label}] CallCreated`, JSON.stringify(d).slice(0, 200)));
  conn.on('CallMissed', d => console.log(`[${label}] CallMissed`, JSON.stringify(d)));
  conn.on('CallEnded', d => console.log(`[${label}] CallEnded`, JSON.stringify(d)));
  conn.on('CallBusy', d => console.log(`[${label}] CallBusy`, JSON.stringify(d)));
  conn.on('CallOffline', d => console.log(`[${label}] CallOffline`, JSON.stringify(d)));
  conn.on('CallFailed', d => console.log(`[${label}] CallFailed`, JSON.stringify(d)));
  return conn;
}

const cust = connect(CUST, 'cust');
const staff = connect(mintJwt(), 'staff');
await Promise.all([cust.start(), staff.start()]);
console.log('both connected:', cust.state, staff.state);
await sleep(1000);

const callId = randomUUID();
const t0 = Date.now();
const start = await cust.invoke('StartCall', {
  callId, targetUserId: '00000000-0000-0000-0000-000000000000',
  contextType: 'chat', contextReferenceId: `Shop:${TENANT}`
});
console.log('StartCall →', JSON.stringify(start), `(${Date.now() - t0}ms)`);
await sleep(1500);

const t1 = Date.now();
try {
  const r = await Promise.race([
    staff.invoke('AcceptCall', callId).then(v => `resolved:${v}`),
    sleep(15000).then(() => 'TIMEOUT-15s')
  ]);
  console.log(`AcceptCall → ${r} (${Date.now() - t1}ms)`);
} catch (e) { console.log('AcceptCall threw:', e.message); }
await sleep(3000);
try { await cust.invoke('EndCall', callId); console.log('EndCall ok'); } catch (e) { console.log('EndCall:', e.message); }
await sleep(1000);
await cust.stop(); await staff.stop();
process.exit(0);
