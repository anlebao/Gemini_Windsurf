#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV — VOICE REAL WEBRTC E2E (media + audio playback) — PRODUCTION
// Two real Chromium pages (fake audio device) run the ACTUAL deployed
// voice-sdk.js/voice-media.js against wss://voice.khachvip.online.
// Verifies: SignalR call flow AND RTCPeerConnection 'connected' (required for
// server CallConnected) AND remote MediaStream attached to playing <audio>.
//
// Chạy:  VOICE_JWT_SECRET=<Jwt__Secret> VOICE_CUSTOMER_TOKEN=<token> \
//          VOICE_STAFF_ID=<staff-guid> node voice-webrtc-real-rv.mjs
// Env:   PAGE_URL (default file:///…/voice-webrtc-rv/page.html),
//        CHAT_CONTEXT (default Shop:e2d91cdf-...), CALL_TIMEOUT_MS
// ─────────────────────────────────────────────────────────────────────────────
import { chromium } from 'playwright';
import { createHmac } from 'node:crypto';
import { readFileSync } from 'node:fs';

const SECRET = process.env.VOICE_JWT_SECRET ?? '';
const CUST_TOKEN = process.env.VOICE_CUSTOMER_TOKEN ?? readFileSync('/tmp/voice-rv/cust.token', 'utf8').trim();
const STAFF_ID = process.env.VOICE_STAFF_ID ?? '5d23ffc3-5ad9-4f19-8af5-401015334882';
const TENANT = process.env.VOICE_TENANT ?? 'e2d91cdf-d3c4-4399-b96e-e772a7a41630';
const PAGE_URL = process.env.PAGE_URL ?? 'file:///C:/Users/lebao/AppData/Local/Temp/voice-webrtc-rv/page.html';
const CALL_TIMEOUT = Number(process.env.CALL_TIMEOUT_MS ?? 40000);

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
  console.log(`[${pass ? 'PASS' : 'FAIL'}] ${name}${detail ? ` — ${String(detail).slice(0, 300)}` : ''}`);
};

async function waitFor(fn, timeoutMs, label) {
  const t0 = Date.now();
  while (Date.now() - t0 < timeoutMs) {
    const v = await fn();
    if (v) return v;
    await sleep(500);
  }
  throw new Error(`timeout waiting: ${label}`);
}

const launchArgs = [
  '--use-fake-device-for-media-stream',
  '--use-fake-ui-for-media-stream',
  '--autoplay-policy=no-user-gesture-required',
  '--disable-web-security',
  '--disable-features=WebRtcHideLocalIpsWithMdns' // keep host candidates reachable
];

const browser = await chromium.launch({ args: launchArgs });

async function openSide(name, token) {
  const ctx = await browser.newContext({ permissions: ['microphone'] });
  const page = await ctx.newPage();
  page.on('console', m => { if (m.type() === 'error') console.log(`  [${name}:console.error]`, m.text().slice(0, 200)); });
  page.on('pageerror', e => console.log(`  [${name}:pageerror]`, String(e).slice(0, 200)));
  await page.addInitScript(t => { window.__TOKEN = t; }, token);
  await page.goto(PAGE_URL);
  await waitFor(async () => (await page.evaluate(() => window.rv && window.rv.hub)) === 'connected', 30000, `${name} hub connect`);
  console.log(`${name}: hub connected`);
  return { ctx, page, name };
}

async function runDirection(caller, callee, label) {
  console.log(`\n=== ${label} ===`);
  // callee auto-accepts on incoming
  const acceptPromise = waitFor(async () => {
    const inc = await callee.page.evaluate(() => window.rv.incoming);
    if (inc) { await callee.page.evaluate(() => window.rvAccept()); return true; }
    return null;
  }, CALL_TIMEOUT, `${callee.name} incoming`).catch(e => null);

  await caller.page.evaluate(() => window.rvStart());
  await acceptPromise;
  await sleep(300);

  // Wait for BOTH SDK states to reach 'connected' (server CallConnected —
  // only fires after RTCPeerConnection.connectionState === 'connected').
  let bothConnected = false;
  try {
    await waitFor(async () => {
      const cs = await caller.page.evaluate(() => window.rv.state);
      const ss = await callee.page.evaluate(() => window.rv.state);
      return cs === 'connected' && ss === 'connected' ? true : null;
    }, CALL_TIMEOUT, 'both connected');
    bothConnected = true;
  } catch { /* fall through to diagnostics */ }
  check(`${label}: both peers CONNECTED (real WebRTC)`, bothConnected,
    `caller=${await caller.page.evaluate(() => window.rv.state)} callee=${await callee.page.evaluate(() => window.rv.state)}`);

  // Remote audio must be attached + playing on BOTH sides (poll — ontrack can
  // fire shortly after CallConnected).
  for (const side of [caller, callee]) {
    let a = null;
    try {
      await waitFor(async () => {
        const v = await side.page.evaluate(() => window.rv.remoteAudio);
        if (v && v.exists) { a = v; return true; }
        return null;
      }, 8000, `${side.name} remoteAudio element`);
    } catch { /* leave a=null */ }
    const tracks = await side.page.evaluate(() => window.rv.remoteStreamInfo);
    check(`${label}: ${side.name} remote audio playing`,
      !!(a && a.srcObjectTracks > 0 && !a.paused),
      JSON.stringify({ remoteAudio: a, tracks }));
  }

  // End the call cleanly before next direction.
  await caller.page.evaluate(() => window.rvEnd()).catch(() => {});
  await sleep(800);
}

try {
  const staffJwt = mintStaffJwt(STAFF_ID);
  const customer = await openSide('customer', CUST_TOKEN);
  const staff = await openSide('staff', staffJwt);

  await runDirection(customer, staff, 'customer→staff');
  // reset callee incoming flag between directions
  await staff.page.evaluate(() => { window.rv.incoming = null; window.rv.events = []; });
  await customer.page.evaluate(() => { window.rv.incoming = null; window.rv.events = []; });
  await runDirection(staff, customer, 'staff→customer');
} finally {
  await browser.close();
}

const failed = results.filter(r => !r.pass);
console.log(`\n=== VOICE WEBRTC REAL RV: ${results.length - failed.length}/${results.length} PASS ===`);
process.exit(failed.length ? 1 : 0);
