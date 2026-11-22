#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV — VIDEO CALL MVP FULL CALL-FLOW (production)
// Kế thừa sweep voice-presence-rv.mjs (2026-11-21): Customer (KhachLink storefront,
// auto-presence) ↔ ShopERP staff (toggle ON, global overlay khi KHÔNG mở chat).
// Video call THẬT qua voice.khachvip.online — fake camera cả 2 phía.
//
// Coverage (acceptance criteria task_v1_video_mvp):
//   AC1 video 1:1 remote tile + local PiP · AC2 camera toggle mid-call
//   AC3 audio regression · AC4 metrics videoCalls (CallType persist)
//   AC6 stunUrls trong /v1/turn/credentials · AC7 guest gate (readiness parity)
//
// Env:
//   VOICE_CUSTOMER_TOKEN  — dev-token minted customer_token (bắt buộc)
//   VOICE_STAFF_USER/PASS — ShopERP login (sysadmin; sẽ impersonate tenant)
//   VOICE_TENANT          — default An Tâm e2d91cdf-...
//   VOICE_STORE_URL       — default diemthuong2 store by-id
//   VOICE_APP2            — default https://app2.khachvip.online
//   VOICE_BASE            — voice service base (metrics + turn creds), default https://voice.khachvip.online
// ─────────────────────────────────────────────────────────────────────────────
import { chromium } from 'playwright';
import fs from 'fs';

// Creds fallback: reuse files minted for the voice-presence RV (An Tâm e2d91cdf).
//   .devin/rv_cust_token.json      — { customerToken } dev-token customer (365d)
//   .devin/rv_presence_creds.json  — { owner_user, owner_pass } staff owner login
const loadJson = (p) => { try { return JSON.parse(fs.readFileSync(p, 'utf8')); } catch { return {}; } };
const credsFile = loadJson('.devin/rv_presence_creds.json');
const custFile = loadJson('.devin/rv_cust_token.json');

const TENANT = process.env.VOICE_TENANT ?? 'e2d91cdf-d3c4-4399-b96e-e772a7a41630';
const STORE = process.env.VOICE_STORE_URL ?? `https://diemthuong2.khachvip.online/store/by-id/${TENANT}`;
const APP2 = process.env.VOICE_APP2 ?? 'https://app2.khachvip.online';
const VOICE_BASE = (process.env.VOICE_BASE ?? 'https://voice.khachvip.online').replace(/\/+$/, '');
const CUST_TOKEN = process.env.VOICE_CUSTOMER_TOKEN ?? custFile.customerToken ?? '';
const STAFF_USER = process.env.VOICE_STAFF_USER ?? credsFile.owner_user ?? '';
const STAFF_PASS = process.env.VOICE_STAFF_PASS ?? credsFile.owner_pass ?? '';
const CALL_SECONDS = Number(process.env.VOICE_CALL_SECONDS ?? 6);

if (!CUST_TOKEN || !STAFF_USER || !STAFF_PASS) {
  console.error('VOICE_CUSTOMER_TOKEN + VOICE_STAFF_USER + VOICE_STAFF_PASS required');
  process.exit(2);
}

const results = [];
const check = (name, pass, detail = '') => {
  results.push({ name, pass: !!pass });
  console.log(`[${pass ? 'PASS' : 'FAIL'}] ${name}${detail ? ` — ${String(detail).slice(0, 240)}` : ''}`);
};
const sleep = (ms) => new Promise(r => setTimeout(r, ms));
const pollState = (pg, timeoutMs = 25000) => new Promise(async (resolve) => {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const s = await pg.evaluate(() => window.vananVoiceChat?.getState?.() ?? null).catch(() => null);
    if (s && typeof s === 'object') return resolve(s);
    await sleep(1000);
  }
  resolve(null);
});

const browser = await chromium.launch({
  headless: true,
  args: [
    '--use-fake-device-for-media-stream',
    '--use-fake-ui-for-media-stream',
    '--autoplay-policy=no-user-gesture-required',
    '--host-resolver-rules=MAP diemthuong2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, MAP app2.khachvip.online 136.85.94.119'
  ]
});

const staffCtx = await browser.newContext({ permissions: ['microphone', 'camera'] });
const custCtx = await browser.newContext({ permissions: ['microphone', 'camera'] });
await custCtx.addInitScript((t) => {
  try { localStorage.setItem('customer_token', t); } catch {}
}, CUST_TOKEN);

const staffPage = await staffCtx.newPage();
const custPage = await custCtx.newPage();
staffPage.on('pageerror', e => console.log('[staff pageerror]', String(e).slice(0, 200)));
custPage.on('pageerror', e => console.log('[cust pageerror]', String(e).slice(0, 200)));
for (const [tag, pg] of [['staff', staffPage], ['cust', custPage]]) {
  pg.on('console', m => {
    const t = m.text();
    if (m.type() === 'error' || /voice|vananVoice/i.test(t))
      console.log(`[${tag} console.${m.type()}]`, t.slice(0, 200));
  });
}

try {
  // ── 0. STUN contract (AC6) + metrics baseline ──────────────────────────
  const credResp = await fetch(`${VOICE_BASE}/v1/turn/credentials`, {
    headers: { Accept: 'application/json', Authorization: `Bearer ${CUST_TOKEN}` }
  }).catch(e => ({ ok: false, err: String(e) }));
  const cred = credResp.ok ? await credResp.json() : null;
  check('V0 /v1/turn/credentials trả stunUrls (AC6)', !!(cred?.stunUrls?.length),
    cred ? `stunUrls=${JSON.stringify(cred.stunUrls)}` : `http=${credResp.status}`);

  const baseMetrics = await fetch(`${VOICE_BASE}/v1/metrics/summary`)
    .then(r => r.ok ? r.json() : null).catch(() => null);
  const baseVideoCalls = baseMetrics?.videoCalls ?? 0;
  console.log('[info] metrics baseline videoCalls =', baseVideoCalls);

  // ── 1. CUSTOMER: mở store, đợi presence auto-init ──────────────────────
  await custPage.goto(STORE, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await custPage.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => {});
  await sleep(5000);
  let custInit = false;
  for (let i = 0; i < 30 && !custInit; i++) {
    custInit = await custPage.evaluate(() => window.vananVoiceChat?.isInitialized?.() === true
      || window.vananVoiceChat?.getConnectionState?.() === 'connected');
    if (!custInit) await sleep(1000);
  }
  check('C1 customer presence bridge initialized', custInit === true);

  let custConn = '';
  for (let i = 0; i < 20; i++) {
    custConn = await custPage.evaluate(() => window.vananVoiceChat?.getConnectionState?.() ?? '');
    if (custConn === 'connected') break;
    await sleep(1000);
  }
  check('C2 customer voice hub CONNECTED', custConn === 'connected', custConn);

  // ── 2. CUSTOMER: gửi tin nhắn → tạo conversation ───────────────────────
  await custPage.click('.vanan-ai-bubble-btn', { timeout: 15000 });
  await sleep(1500);
  await custPage.click('.vanan-ai-tab-human', { timeout: 10000 }).catch(() => {});
  await custPage.waitForSelector('.chat-panel input', { timeout: 20000 });
  await custPage.fill('.chat-panel input', 'RV video call test — xin chào shop');
  await custPage.click('.chat-send-btn');
  await sleep(2500);
  const ownMsg = await custPage.locator('.chat-bubble--own').count();
  check('C3 customer sent human chat message', ownMsg > 0, `own bubbles=${ownMsg}`);
  await custPage.click('.vanan-ai-header [aria-label="Đóng trợ lý AI"], .vanan-ai-header button', { timeout: 10000 }).catch(() => {});
  await sleep(1500);

  // ── 3. STAFF: login → inbox → reply → presence ON ──────────────────────
  await staffPage.goto(`${APP2}/Login`, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await staffPage.fill('#username', STAFF_USER);
  await staffPage.fill('#password', STAFF_PASS);
  await Promise.all([
    staffPage.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 30000 }).catch(() => {}),
    staffPage.click('button[type="submit"], input[type="submit"]')
  ]);
  await sleep(3000);
  check('S1 staff owner login', !staffPage.url().includes('/Login'), `url=${staffPage.url()}`);

  await staffPage.goto(`${APP2}/community/messages`, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await staffPage.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => {});
  await sleep(4000);
  const convBtn = staffPage.locator('.list-group-item').first();
  const convCount = await staffPage.locator('.list-group-item').count();
  check('S3 shop inbox loaded', convCount > 0, `conversations=${convCount}`);
  await convBtn.waitFor({ state: 'visible', timeout: 20000 });
  await convBtn.click();
  await staffPage.waitForSelector('.chat-panel input', { timeout: 20000 });
  await sleep(3000);
  await staffPage.fill('.chat-panel input', 'Dạ shop nghe ạ — RV video test');
  await staffPage.click('.chat-send-btn');
  await sleep(3000);
  check('S4 staff replied in thread', (await staffPage.locator('.chat-bubble--own').count()) > 0);

  await staffPage.goto(`${APP2}/orders`, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await sleep(4000);
  const toggle = staffPage.locator('[data-testid="voice-presence-toggle"]');
  if (!(await toggle.isVisible().catch(() => false))) {
    check('S5 voice-presence toggle visible', false);
    throw new Error('presence toggle not visible — cannot continue');
  }
  check('S5 voice-presence toggle visible', true);
  await toggle.click();
  await sleep(6000);
  let staffConn = '';
  for (let i = 0; i < 20; i++) {
    staffConn = await staffPage.evaluate(() => window.vananVoiceChat?.getConnectionState?.() ?? '');
    if (staffConn === 'connected') break;
    await sleep(1000);
  }
  check('S7 staff voice hub CONNECTED', staffConn === 'connected', staffConn);

  // ── 4. CUSTOMER: VIDEO call (callType='video') ─────────────────────────
  const callResult = await custPage.evaluate(async ({ tenant }) => {
    try {
      await window.vananVoiceChat.startCall(null, 'Shop', tenant, null, 'video');
      return { ok: true };
    } catch (e) { return { ok: false, err: String(e) }; }
  }, { tenant: TENANT });
  check('V1 customer startCall VIDEO invoked', callResult.ok, JSON.stringify(callResult));

  const custStateEarly = await pollState(custPage, 10000);
  check('V2 customer state.callType = video', custStateEarly?.callType === 'video', JSON.stringify(custStateEarly));

  // ── 5. STAFF: incoming overlay → accept ────────────────────────────────
  const overlay = staffPage.locator('[data-testid="voice-presence-incoming"]');
  let overlaySeen = false;
  try { await overlay.waitFor({ state: 'visible', timeout: 30000 }); overlaySeen = true; } catch {}
  check('V3 staff global incoming overlay HIỆN', overlaySeen);
  if (!overlaySeen) {
    const custState = await custPage.evaluate(() => window.vananVoiceChat?.getState?.() ?? 'n/a');
    console.log('[debug] customer state:', JSON.stringify(custState));
    throw new Error('no incoming overlay — abort video assertions');
  }
  await staffPage.click('[data-testid="voice-presence-accept"]');

  const callbar = staffPage.locator('[data-testid="voice-presence-callbar"]');
  let barSeen = false;
  try { await callbar.waitFor({ state: 'visible', timeout: 25000 }); barSeen = true; } catch {}
  check('V4 staff mini call-bar hiện sau accept', barSeen);

  // ── 6. VIDEO UI assertions (AC1) ───────────────────────────────────────
  const videoOverlay = staffPage.locator('[data-testid="voice-presence-video"]');
  let vidSeen = false;
  try { await videoOverlay.waitFor({ state: 'visible', timeout: 15000 }); vidSeen = true; } catch {}
  check('V5 staff global video overlay hiện (callType=video)', vidSeen);

  // Local PiP: camera bật ngay từ accept → #voice-local-video-host có <video>
  let localVid = false;
  for (let i = 0; i < 20 && !localVid; i++) {
    localVid = await staffPage.evaluate(() => {
      const host = document.getElementById('voice-local-video-host');
      const v = host?.querySelector('video');
      return !!(v && v.srcObject && v.srcObject.getVideoTracks().length > 0);
    });
    if (!localVid) await sleep(1000);
  }
  check('V6 staff local PiP <video> có video track (AC1)', localVid);

  // Remote tile: customer fake camera → remote track live
  let remoteVid = false;
  for (let i = 0; i < 25 && !remoteVid; i++) {
    remoteVid = await staffPage.evaluate(() => {
      const host = document.getElementById('voice-remote-video-host');
      const v = host?.querySelector('video');
      // replaceTrack toggle: remote track live+unmuted = camera đang bật thật.
      return !!(v && v.srcObject && v.srcObject.getVideoTracks().some(t => t.readyState === 'live' && !t.muted));
    });
    if (!remoteVid) await sleep(1000);
  }
  check('V7 staff remote tile <video> LIVE (AC1)', remoteVid);

  // ── 7. Camera toggle mid-call (AC2): customer OFF → staff remote mất ──
  if (vidSeen) {
    const camBefore = await custPage.evaluate(() => window.vananVoiceChat?.isCameraOn?.());
    await custPage.evaluate(() => window.vananVoiceChat.toggleCamera());
    await sleep(4000); // re-offer + answer + remote track end
    const camAfter = await custPage.evaluate(() => window.vananVoiceChat?.isCameraOn?.());
    check('V8 customer toggleCamera → cameraOn false', camBefore === true && camAfter === false,
      `before=${camBefore} after=${camAfter}`);

    let staffRemoteGone = false;
    for (let i = 0; i < 20 && !staffRemoteGone; i++) {
      staffRemoteGone = await staffPage.evaluate(() => {
        const host = document.getElementById('voice-remote-video-host');
        const v = host?.querySelector('video');
        // OFF = không còn track nào live VÀ unmuted (ended hoặc muted đều tính tắt).
        return !v || !v.srcObject
          || v.srcObject.getVideoTracks().every(t => t.readyState !== 'live' || t.muted);
      });
      if (!staffRemoteGone) await sleep(1000);
    }
    check('V9 staff remote video OFF sau customer toggle (AC2)', staffRemoteGone);

    await custPage.evaluate(() => window.vananVoiceChat.toggleCamera());
    await sleep(4000);
    let staffRemoteBack = false;
    for (let i = 0; i < 20 && !staffRemoteBack; i++) {
      staffRemoteBack = await staffPage.evaluate(() => {
        const v = document.getElementById('voice-remote-video-host')?.querySelector('video');
        return !!(v && v.srcObject && v.srcObject.getVideoTracks().some(t => t.readyState === 'live' && !t.muted));
      });
      if (!staffRemoteBack) await sleep(1000);
    }
    check('V10 staff remote video BACK sau customer toggle on (AC2)', staffRemoteBack);
  }

  await sleep(CALL_SECONDS * 1000);

  // ── 8. End call → UI dọn sạch ──────────────────────────────────────────
  await staffPage.click('[data-testid="voice-presence-end"]').catch(() => {});
  let barGone = false;
  try { await callbar.waitFor({ state: 'hidden', timeout: 15000 }); barGone = true; } catch {}
  check('V11 sau end → callbar ẩn', barGone);
  const vidGone = await videoOverlay.isHidden().catch(() => true);
  check('V12 sau end → video overlay ẩn', vidGone);

  // ── 9. Metrics: CallType persist (AC4) ─────────────────────────────────
  await sleep(2000); // server finalize
  const metrics = await fetch(`${VOICE_BASE}/v1/metrics/summary`).then(r => r.ok ? r.json() : null).catch(() => null);
  check('V13 metrics videoCalls tăng (CallType=video persisted, AC4)',
    !!metrics && (metrics.videoCalls ?? 0) > baseVideoCalls,
    `baseline=${baseVideoCalls} now=${metrics?.videoCalls}`);

  // ── 10. AUDIO regression (AC3): call audio thường vẫn hoạt động ────────
  const audioResult = await custPage.evaluate(async ({ tenant }) => {
    try {
      await window.vananVoiceChat.startCall(null, 'Shop', tenant, null, 'audio');
      return { ok: true };
    } catch (e) { return { ok: false, err: String(e) }; }
  }, { tenant: TENANT });
  check('V14 audio call vẫn start được (AC3)', audioResult.ok, JSON.stringify(audioResult));

  let overlay2 = false;
  try { await staffPage.locator('[data-testid="voice-presence-incoming"]').waitFor({ state: 'visible', timeout: 30000 }); overlay2 = true; } catch {}
  check('V15 staff thấy incoming audio call', overlay2);
  if (overlay2) {
    await staffPage.click('[data-testid="voice-presence-accept"]');
    try { await staffPage.locator('[data-testid="voice-presence-callbar"]').waitFor({ state: 'visible', timeout: 25000 }); } catch {}
    await sleep(4000);
    const vidOverlayAudio = await staffPage.locator('[data-testid="voice-presence-video"]').isVisible().catch(() => false);
    check('V16 audio call KHÔNG hiện video overlay (AC3)', !vidOverlayAudio);
    await staffPage.click('[data-testid="voice-presence-end"]').catch(() => {});
    await sleep(3000);
  }

} catch (err) {
  check('RV script completed without exception', false, err?.message ?? String(err));
} finally {
  await browser.close();
}

const fails = results.filter(r => !r.pass).length;
console.log(`\n=== VIDEO CALL RV: ${results.length - fails}/${results.length} PASS ===`);
process.exit(fails > 0 ? 1 : 0);
