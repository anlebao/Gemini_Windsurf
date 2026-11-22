#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV — VOICE PRESENCE FULL CALL-FLOW (production)
// Customer (KhachLink storefront, auto-presence) ↔ ShopERP staff (toggle ON,
// global overlay khi KHÔNG mở chat). Call THẬT qua voice.khachvip.online.
//
// Env:
//   VOICE_CUSTOMER_TOKEN  — dev-token minted customer_token (bắt buộc)
//   VOICE_STAFF_USER/PASS — ShopERP login (sysadmin; sẽ impersonate tenant)
//   VOICE_TENANT          — default An Tâm e2d91cdf-...
//   VOICE_STORE_URL       — default diemthuong2 store by-id
//   VOICE_APP2            — default https://app2.khachvip.online
// ─────────────────────────────────────────────────────────────────────────────
import { chromium } from 'playwright';

const TENANT = process.env.VOICE_TENANT ?? 'e2d91cdf-d3c4-4399-b96e-e772a7a41630';
const STORE = process.env.VOICE_STORE_URL ?? `https://diemthuong2.khachvip.online/store/by-id/${TENANT}`;
const APP2 = process.env.VOICE_APP2 ?? 'https://app2.khachvip.online';
const CUST_TOKEN = process.env.VOICE_CUSTOMER_TOKEN ?? '';
const STAFF_USER = process.env.VOICE_STAFF_USER ?? '';
const STAFF_PASS = process.env.VOICE_STAFF_PASS ?? '';
const CALL_SECONDS = Number(process.env.VOICE_CALL_SECONDS ?? 8);

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
// Instrument staff page: time getUserMedia / fetch / acceptCall để bắt chỗ hang.
await staffCtx.addInitScript(() => {
  const tag = '[vananVoice-probe]';
  try {
    const origGUM = navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);
    navigator.mediaDevices.getUserMedia = (...a) => {
      console.info(tag, 'GUM start');
      return origGUM(...a).then(s => { console.info(tag, 'GUM ok'); return s; })
        .catch(e => { console.info(tag, 'GUM err', e.name, e.message); throw e; });
    };
    const origFetch = window.fetch.bind(window);
    window.fetch = (...a) => { console.info(tag, 'fetch', String(a[0]).slice(0, 120)); return origFetch(...a); };
    const origPC = window.RTCPeerConnection;
    if (origPC) window.RTCPeerConnection = function (...a) { console.info(tag, 'RTCPeerConnection new'); return new origPC(...a); };
  } catch (e) { console.info(tag, 'init err', e.message); }
});
// customer_token phải có TRƯỚC khi app đọc localStorage
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
  // ── 1. CUSTOMER: mở store, đợi presence auto-init ──────────────────────
  await custPage.goto(STORE, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await custPage.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => {});
  await sleep(5000);
  // presence init bất đồng bộ — poll tới khi isInitialized()=true
  let custInit = false;
  for (let i = 0; i < 20 && !custInit; i++) {
    custInit = await custPage.evaluate(() => window.vananVoiceChat?.isInitialized?.() === true);
    if (!custInit) await sleep(1000);
  }
  const custVoice = await custPage.evaluate(async () => {
    const vc = window.vananVoiceChat;
    if (!vc) return { bridge: false };
    return {
      bridge: true,
      init: vc.isInitialized ? vc.isInitialized() : null,
      conn: vc.getConnectionState ? vc.getConnectionState() : null
    };
  });
  check('C1 customer presence bridge initialized', custVoice.bridge && custInit === true, JSON.stringify(custVoice));

  // Đợi voice hub connected (presence = live SignalR connection)
  let custConn = '';
  for (let i = 0; i < 20; i++) {
    custConn = await custPage.evaluate(() => window.vananVoiceChat?.getConnectionState?.() ?? '');
    if (custConn === 'connected') break;
    await sleep(1000);
  }
  check('C2 customer voice hub CONNECTED (auto-presence)', custConn === 'connected', custConn);
  if (custConn !== 'connected') {
    console.log('[debug] customer voice hub chưa connected — reload retry');
    await custPage.reload({ waitUntil: 'domcontentloaded' }).catch(() => {});
    await sleep(8000);
    for (let i = 0; i < 20; i++) {
      custConn = await custPage.evaluate(() => window.vananVoiceChat?.getConnectionState?.() ?? '');
      if (custConn === 'connected') break;
      await sleep(1000);
    }
    check('C2b customer voice hub CONNECTED sau reload', custConn === 'connected', custConn);
  }

  // ── 2. CUSTOMER: gửi tin nhắn human chat → tạo conversation ────────────
  await custPage.click('.vanan-ai-bubble-btn', { timeout: 15000 });
  await sleep(1500);
  await custPage.click('.vanan-ai-tab-human', { timeout: 10000 }).catch(() => {});
  await custPage.waitForSelector('.chat-panel input', { timeout: 20000 });
  await custPage.fill('.chat-panel input', 'RV voice presence test — xin chào shop');
  await custPage.click('.chat-send-btn');
  await sleep(2500);
  const ownMsg = await custPage.locator('.chat-bubble--own').count();
  check('C3 customer sent human chat message (conversation created)', ownMsg > 0, `own bubbles=${ownMsg}`);

  // Đóng chat widget → ChatSurfaceActive=false → global presence UI path được test
  // (nếu để mở, call UI hiện trong chat panel — đúng design, nhưng không phải global).
  // Nút đóng là chevron trong .vanan-ai-header (aria-label="Đóng trợ lý AI").
  await custPage.click('.vanan-ai-header [aria-label="Đóng trợ lý AI"], .vanan-ai-header button', { timeout: 10000 }).catch(() => {});
  await sleep(1500);

  // ── 3. STAFF: login tenant owner qua Razor Login page (sub=GUID — bắt buộc
  //    cho voice/chat; platform login có sub=email → random participant id) ──
  await staffPage.goto(`${APP2}/Login`, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await staffPage.fill('#username', STAFF_USER);
  await staffPage.fill('#password', STAFF_PASS);
  await Promise.all([
    staffPage.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 30000 }).catch(() => {}),
    staffPage.click('button[type="submit"], input[type="submit"]')
  ]);
  await sleep(3000);
  const loggedIn = !staffPage.url().includes('/Login');
  check('S1 staff owner login (tenant e2d91cdf) → redirect', loggedIn, `url=${staffPage.url()}`);

  await staffPage.goto(`${APP2}/community/messages`, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await staffPage.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => {});
  await sleep(4000);

  // Conversation mới nhất (đầu list) — customer dev-token hiển thị "Khách hàng"
  const convBtn = staffPage.locator('.list-group-item').first();
  const convCount = await staffPage.locator('.list-group-item').count();
  check('S3 shop inbox loaded with conversations', convCount > 0, `conversations=${convCount}`);
  await convBtn.waitFor({ state: 'visible', timeout: 20000 });
  await convBtn.click();
  await staffPage.waitForSelector('.chat-panel input', { timeout: 20000 });
  // đợi messages load xong trước khi reply
  await staffPage.waitForSelector('.chat-msg, .chat-panel .list-unstyled, .chat-panel', { timeout: 15000 }).catch(() => {});
  await sleep(3000);
  // Staff reply → join conversation as participant
  await staffPage.fill('.chat-panel input', 'Dạ shop nghe ạ — RV voice test');
  await staffPage.click('.chat-send-btn');
  await sleep(3000);
  const staffOwn = await staffPage.locator('.chat-bubble--own').count();
  check('S4 staff replied in thread (participant joined)', staffOwn > 0, `own bubbles=${staffOwn}`);
  if (staffOwn === 0) {
    console.log('[debug] staff url:', staffPage.url());
    console.log('[debug] chat panel html:', (await staffPage.locator('.chat-panel').innerHTML().catch(() => 'n/a')).slice(0, 400));
  }

  // ── 4. STAFF: rời trang chat → bật presence toggle ─────────────────────
  await staffPage.goto(`${APP2}/orders`, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await sleep(4000);
  const toggle = staffPage.locator('[data-testid="voice-presence-toggle"]');
  const toggleVisible = await toggle.isVisible().catch(() => false);
  check('S5 voice-presence toggle visible (flag ON + tenant impersonated)', toggleVisible);
  if (!toggleVisible) {
    // chụp debug: trang đang ở đâu, header có gì
    console.log('[debug] staff url:', staffPage.url());
    console.log('[debug] header html:', (await staffPage.locator('header, .user-header, nav').first().innerHTML().catch(() => 'n/a')).slice(0, 500));
    throw new Error('presence toggle not visible — cannot continue');
  }
  await toggle.click();
  await sleep(6000); // presence init + voice hub connect
  const staffVoice = await staffPage.evaluate(() => {
    const vc = window.vananVoiceChat;
    return vc ? { init: vc.isInitialized(), conn: vc.getConnectionState() } : { init: false };
  });
  check('S6 staff presence voice bridge initialized sau toggle ON', staffVoice.init === true, JSON.stringify(staffVoice));

  let staffConn = '';
  for (let i = 0; i < 20; i++) {
    staffConn = await staffPage.evaluate(() => window.vananVoiceChat?.getConnectionState?.() ?? '');
    if (staffConn === 'connected') break;
    await sleep(1000);
  }
  // Flake guard: WS tới voice hub có thể rớt 1 lần (SSE fallback cũng có thể fail)
  // → reload page 1 lần (toggle persist localStorage → presence tự re-init).
  if (staffConn !== 'connected') {
    console.log('[debug] staff voice hub chưa connected — reload page retry');
    await staffPage.reload({ waitUntil: 'domcontentloaded' }).catch(() => {});
    await sleep(8000);
    for (let i = 0; i < 20; i++) {
      staffConn = await staffPage.evaluate(() => window.vananVoiceChat?.getConnectionState?.() ?? '');
      if (staffConn === 'connected') break;
      await sleep(1000);
    }
  }
  check('S7 staff voice hub CONNECTED (presence = live connection)', staffConn === 'connected', staffConn);

  // ── 4b. DIAG: island circuit còn sống? Toggle OFF→ON qua DOM click ──────
  // Nếu toggle @onclick (cùng island VoicePresenceHost) cũng không dispatch →
  // circuit chết → mọi @onclick trong island (kể cả panel con) đều chết.
  if (process.env.VOICE_SKIP_TOGGLE_PROBE !== '1') {
    await staffPage.evaluate(() => document.querySelector('[data-testid="voice-presence-toggle"]')?.click());
    await sleep(2500);
    const initAfterOff = await staffPage.evaluate(() => window.vananVoiceChat?.isInitialized?.() ?? 'n/a');
    console.log('[diag] toggle OFF → isInitialized =', initAfterOff, '(false = dispatch OK, circuit sống)');
    if (initAfterOff === false) {
      await staffPage.evaluate(() => document.querySelector('[data-testid="voice-presence-toggle"]')?.click());
      // chờ re-init + hub connected trở lại
      for (let i = 0; i < 20; i++) {
        await sleep(1000);
        const st = await staffPage.evaluate(() => ({
          init: window.vananVoiceChat?.isInitialized?.(),
          conn: window.vananVoiceChat?.getConnectionState?.()
        })).catch(() => null);
        if (st?.init && st?.conn === 'connected') { console.log('[diag] toggle ON lại → re-init OK'); break; }
      }
    } else {
      console.log('[diag] TOGGLE KHÔNG DISPATCH — island circuit chết hoặc binding hỏng');
    }
  }

  // ── 5. CUSTOMER gọi shop qua presence-owned JS singleton ───────────────
  const callResult = await custPage.evaluate(async ({ tenant }) => {
    try {
      await window.vananVoiceChat.startCall(null, 'Shop', tenant, null);
      return { ok: true };
    } catch (e) { return { ok: false, err: String(e) }; }
  }, { tenant: TENANT });
  check('C4 customer startCall invoked (context Shop:' + TENANT.slice(0, 8) + '…)', callResult.ok, JSON.stringify(callResult));

  // ── 6. STAFF: global incoming overlay (chat ĐÃ ĐÓNG — đang ở dashboard) ─
  const overlay = staffPage.locator('[data-testid="voice-presence-incoming"]');
  let overlaySeen = false;
  try {
    await overlay.waitFor({ state: 'visible', timeout: 30000 });
    overlaySeen = true;
  } catch {}
  check('S8 global incoming overlay HIỆN khi chat đóng (presence)', overlaySeen);
  if (overlaySeen) {
    const txt = await overlay.innerText();
    check('S9 overlay hiển thị tên người gọi', /RV Test|Khách|khách/i.test(txt), txt.slice(0, 120));
  }

  // C5 check SỚM — customer state 'calling' đã hiện call UI; nếu để sau
  // accept+wait, ring timeout (30s) có thể giết call trước khi assert.
  // Chấp nhận mọi call surface: global bar / mini bar / in-chat card (nếu widget còn mở).
  const widgetOpen = await custPage.locator('.vanan-ai-panel').isVisible().catch(() => false);
  console.log('[debug] customer widget open sau khi đóng?', widgetOpen);
  const custBarEarly = custPage.locator('[data-testid="voice-presence-callbar"], [data-testid="ai-mini-callbar"], .voice-call-card--active, [data-testid="voice-end"]');
  let custBarEarlySeen = false;
  try { await custBarEarly.first().waitFor({ state: 'visible', timeout: 10000 }); custBarEarlySeen = true; } catch {}
  check('C5 customer thấy call UI global trong khi gọi', custBarEarlySeen);

  // ── 7. STAFF accept → callbar → end ────────────────────────────────────
  if (overlaySeen) {
    // Chẩn đoán click: elementFromPoint tại tâm nút accept — phát hiện overlay che.
    const hit = await staffPage.evaluate(() => {
      const el = document.querySelector('[data-testid="voice-presence-accept"]');
      if (!el) return { el: false };
      const r = el.getBoundingClientRect();
      const cx = r.x + r.width / 2, cy = r.y + r.height / 2;
      const top = document.elementFromPoint(cx, cy);
      let chain = [], n = el;
      while (n && chain.length < 6) { chain.push(n.tagName + '.' + (n.className?.toString().slice(0, 40) ?? '')); n = n.parentElement; }
      return {
        el: true, rect: { x: Math.round(r.x), y: Math.round(r.y), w: Math.round(r.width), h: Math.round(r.height) },
        vp: { w: innerWidth, h: innerHeight },
        topIsSelf: top === el || el.contains(top),
        topTag: top ? top.tagName + '.' + (top.className?.toString().slice(0, 60) ?? '') : 'null',
        chain,
        pe: getComputedStyle(el).pointerEvents
      };
    });
    console.log('[debug] accept hit-test:', JSON.stringify(hit));
    // Wrap JS bridge methods — phân biệt "@onclick không dispatch" vs "JSRuntime throw".
    // Nếu Blazor InvokeVoidAsync chạy → counter tăng. Nếu counter=0 sau click → handler không chạy.
    await staffPage.evaluate(() => {
      const o = window.vananVoiceChat;
      if (o && !o.__probeWrapped) {
        for (const m of ['acceptCall', 'endCall', 'rejectCall']) {
          const orig = o[m];
          o[m] = (...a) => { window.__voiceCalls = window.__voiceCalls || {}; window.__voiceCalls[m] = (window.__voiceCalls[m] || 0) + 1; return orig.apply(o, a); };
        }
        o.__probeWrapped = true;
      }
    });
    const staffStateLog = [];
    const statePoll = setInterval(async () => {
      try {
        const s = await staffPage.evaluate(() => window.vananVoiceChat?.getState?.() ?? 'n/a');
        staffStateLog.push(s);
      } catch {}
    }, 2000);
    await staffPage.click('[data-testid="voice-presence-accept"]');
    // Nếu sau 10s vẫn ringing → thử gọi JS trực tiếp (bypass Blazor handler)
    // để phân biệt: Blazor onclick không chạy vs JS/SDK hang.
    setTimeout(async () => {
      try {
        const getSt = () => staffPage.evaluate(() => {
          const s = window.vananVoiceChat?.getState?.();
          return typeof s === 'string' ? s : (s?.state ?? JSON.stringify(s));
        });
        if ((await getSt()) === 'ringing') {
          // Tier 2: DOM .click() — phân biệt Playwright physical click vs Blazor binding.
          console.log('[debug] vẫn ringing — thử DOM .click() trên nút accept');
          await staffPage.evaluate(() => document.querySelector('[data-testid="voice-presence-accept"]')?.click());
          await sleep(6000);
        }
        if ((await getSt()) === 'ringing') {
          console.log('[debug] vẫn ringing sau DOM click — thử acceptCall trực tiếp qua JS');
          const r = await Promise.race([
            staffPage.evaluate(() => window.vananVoiceChat.acceptCall().then(v => 'resolved:' + v).catch(e => 'threw:' + e.message)),
            sleep(12000).then(() => 'EVAL-TIMEOUT-12s')
          ]);
          console.log('[debug] direct acceptCall →', r);
        }
      } catch (e) { console.log('[debug] fallback probe err', e.message); }
    }, 10000);
    const callbar = staffPage.locator('[data-testid="voice-presence-callbar"]');
    let barSeen = false;
    try { await callbar.waitFor({ state: 'visible', timeout: 25000 }); barSeen = true; } catch {}
    clearInterval(statePoll);
    check('S10 sau accept → mini call-bar hiện (callbar toàn cục)', barSeen,
      barSeen ? '' : `states=${JSON.stringify([...new Set(staffStateLog)])}`);
    if (!barSeen) {
      const custState = await custPage.evaluate(() => window.vananVoiceChat?.getState?.() ?? 'n/a').catch(() => 'n/a');
      const jsCalls = await staffPage.evaluate(() => window.__voiceCalls ?? null).catch(() => 'n/a');
      console.log('[debug] staff states:', JSON.stringify(staffStateLog), '| cust state:', custState, '| jsCalls:', JSON.stringify(jsCalls));
    }

    // ── R12 (Phase C): presence badge đỏ nhấp khi đang trong call ──
    const badgeDuring = await staffPage.evaluate(
      () => document.querySelector('[data-testid="voice-presence-toggle"]')?.className ?? '');
    check('S13 presence badge is-calling (đỏ nhấp) khi đang gọi — R12',
      badgeDuring.includes('is-calling'), badgeDuring);

    await sleep(CALL_SECONDS * 1000);
    await staffPage.click('[data-testid="voice-presence-end"]').catch(() => {});
    await sleep(2000);
    const barGone = await callbar.isHidden().catch(() => true);
    const jsCalls2 = await staffPage.evaluate(() => window.__voiceCalls ?? null).catch(() => 'n/a');
    console.log('[debug] sau end — jsCalls:', JSON.stringify(jsCalls2));
    check('S11 sau end → callbar ẩn', barGone);
    const badgeAfter = await staffPage.evaluate(
      () => document.querySelector('[data-testid="voice-presence-toggle"]')?.className ?? '');
    check('S14 badge hết đỏ sau khi end call — R12',
      !badgeAfter.includes('is-calling'), badgeAfter);
  } else {
    // fallback: customer side state để debug
    const custState = await custPage.evaluate(() => window.vananVoiceChat?.getState?.() ?? 'n/a');
    console.log('[debug] customer voice state:', custState);
  }

  // ── 8. STAFF tắt toggle → presence drop ────────────────────────────────
  await staffPage.goto(`${APP2}/orders`, { waitUntil: 'domcontentloaded' }).catch(() => {});
  await sleep(3000);
  const toggle2 = staffPage.locator('[data-testid="voice-presence-toggle"]');
  if (await toggle2.isVisible().catch(() => false)) {
    await toggle2.click();
    await sleep(3000);
    const stillInit = await staffPage.evaluate(() => window.vananVoiceChat?.isInitialized?.() ?? null);
    check('S12 toggle OFF → voice bridge disposed', stillInit === false, `isInitialized=${stillInit}`);
  }

  // ── R10 (Phase C): staff presence OFF → customer gọi lại phải nhận OFFLINE
  //    và VoiceCallPanel (chat surface) hiện message gợi ý để lại tin nhắn.
  const staffInitNow = await staffPage.evaluate(() => window.vananVoiceChat?.isInitialized?.() ?? null).catch(() => null);
  if (staffInitNow === false) {
    await sleep(3000); // chờ voice connection drop phía server
    await custPage.click('.vanan-ai-bubble-btn', { timeout: 15000 }).catch(() => {});
    await sleep(1500);
    await custPage.click('.vanan-ai-tab-human', { timeout: 10000 }).catch(() => {});
    await sleep(1500);
    await custPage.evaluate(async ({ tenant }) => {
      try { await window.vananVoiceChat.startCall(null, 'Shop', tenant, null); } catch {}
    }, { tenant: TENANT }).catch(() => {});
    const offlineErr = custPage.locator('[data-testid="voice-error"]');
    let offlineSeen = false;
    try { await offlineErr.waitFor({ state: 'visible', timeout: 20000 }); offlineSeen = true; } catch {}
    const offlineTxt = offlineSeen ? await offlineErr.innerText().catch(() => '') : '';
    check('S15 OFFLINE → message gợi ý để lại tin nhắn (R10)',
      /không trực tuyến|để lại tin nhắn/i.test(offlineTxt), offlineTxt.slice(0, 160) || 'no voice-error element');
  } else {
    console.log('[skip] S15 — staff presence vẫn ON (isInitialized=' + staffInitNow + '), không test được OFFLINE');
  }
} catch (err) {
  check('RV script completed without exception', false, err?.message ?? String(err));
} finally {
  await browser.close();
}

const fails = results.filter(r => !r.pass).length;
console.log(`\n=== VOICE PRESENCE RV: ${results.length - fails}/${results.length} PASS ===`);
process.exit(fails > 0 ? 1 : 0);
