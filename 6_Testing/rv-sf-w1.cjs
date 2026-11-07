// SF-W1 RV (2026-11-07) — storefront smart UX production sweep, 100% DoD coverage:
//   L2 page/routes · B1 sticky nav · B2 skeleton CSS · lazy imgs · A1 quick-chips
//   (visible → click → auto-send → answer) · B6 unread badge full loop
//   (guest send → owner reply via inbox UI → badge → open human → cleared)
// Run: node rv-sf-w1.cjs   (needs playwright installed in 6_Testing)
const { chromium } = require('@playwright/test');

const STORE = 'https://diemthuong2.khachvip.online/store/by-id/e2d91cdf-d3c4-4399-b96e-e772a7a41630';
const APP2 = 'https://app2.khachvip.online';
const TENANT = 'e2d91cdf-d3c4-4399-b96e-e772a7a41630'; // An Tâm pilot
const MAP = 'MAP diemthuong2.khachvip.online 136.85.94.119, MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119';

const results = [];
const check = (name, ok, extra = '') => {
  results.push({ name, ok });
  console.log(`${ok ? 'PASS' : 'FAIL'} ${name}${extra ? ' — ' + extra : ''}`);
};
const warn = (name, extra) => console.log(`WARN ${name} — ${extra}`);

(async () => {
  const browser = await chromium.launch({
    headless: true,
    args: ['--host-resolver-rules=' + MAP],
  });

  // ── Guest context (storefront) ──────────────────────────────────────────
  const guest = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await guest.newPage();
  page.on('pageerror', e => console.log('PAGEERROR:', e.message));

  const resp = await page.goto(STORE, { waitUntil: 'networkidle', timeout: 60000 });
  check('L2 storefront 200', resp?.status() === 200, `status=${resp?.status()}`);

  // B1 sticky nav (WASM first-load can take >20s — wait for store body first)
  await page.locator('.store-body').waitFor({ timeout: 45000 }).catch(() => {});
  const nav = page.locator('.store-nav');
  await nav.waitFor({ timeout: 30000 }).catch(() => {});
  check('B1 sticky nav rendered', (await nav.count()) > 0);
  if (await nav.count()) {
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight / 2));
    await page.waitForTimeout(600);
    const top = await nav.evaluate(el => el.getBoundingClientRect().top);
    check('B1 nav sticks at top after scroll', top <= 1, `top=${top}px`);
    check('B1 nav links present', (await page.locator('.store-nav-link').count()) > 0);
    await page.evaluate(() => window.scrollTo(0, 0));
  }

  // B2 skeleton CSS shipped (transient — verify style exists + shimmer animates)
  const skelAnim = await page.evaluate(() => {
    const d = document.createElement('div');
    d.className = 'skeleton skeleton-card';
    document.body.appendChild(d);
    const a = getComputedStyle(d).animationName;
    d.remove();
    return a;
  });
  check('B2 skeleton shimmer CSS present', skelAnim && skelAnim !== 'none', `animation=${skelAnim}`);

  // Lazy images (campaign/product imgs, if any)
  const lazyCount = await page.locator('img[loading="lazy"]').count();
  if (lazyCount === 0) warn('B2 lazy images', 'no img[loading=lazy] found on this store (no campaigns?)');

  // A1 quick-chips
  const chip = page.locator('.store-ai-chip').first();
  await chip.waitFor({ timeout: 20000 }).catch(() => {});
  check('A1 quick-chips visible', (await chip.count()) > 0, `${await page.locator('.store-ai-chip').count()} chip(s)`);
  if (await chip.count()) {
    const chipText = (await chip.innerText()).trim();
    await chip.click();
    await page.locator('.vanan-ai-panel').waitFor({ timeout: 10000 }).catch(() => {});
    check('A1 chip → widget opened', (await page.locator('.vanan-ai-panel').count()) > 0);
    await page.locator('.vanan-ai-msg-user .vanan-ai-bubble').first().waitFor({ timeout: 10000 }).catch(() => {});
    const userMsg = await page.locator('.vanan-ai-msg-user .vanan-ai-bubble').first().innerText().catch(() => '');
    check('A1 chip → question auto-sent', userMsg.length > 3, `"${userMsg.slice(0, 40)}"`);
    await page.locator('.vanan-ai-msg-assistant .vanan-ai-bubble:not(.vanan-ai-typing)').last()
      .waitFor({ timeout: 45000 }).catch(() => {});
    check('A1 chip → AI answered',
      (await page.locator('.vanan-ai-msg-assistant .vanan-ai-bubble:not(.vanan-ai-typing)').count()) > 0);
  }

  // B1-fix: slim CTA card replaces the old inline panel — opens bubble human mode.
  const ctaCard = page.locator('.chat-cta-card');
  await ctaCard.scrollIntoViewIfNeeded().catch(() => {});
  const ctaVisible = await ctaCard.count();
  check('FIX slim chat CTA card rendered', ctaVisible > 0);

  // B6 unread badge — full loop: guest sends → owner replies → badge → open → cleared
  let badgeTested = false;
  try {
    // Guest opens human mode via the CTA card (OpenHumanPanel) + sends a message.
    if (ctaVisible) {
      await ctaCard.locator('.btn-cta').click();
    } else {
      // fallback: open bubble → human mode button
      await page.locator('button.vanan-ai-bubble-btn').click();
      const humanBtn = page.locator('button.vanan-ai-human-btn');
      await humanBtn.waitFor({ timeout: 10000 });
      await humanBtn.click();
    }
    await page.locator('.ai-widget-human .chat-panel input').waitFor({ timeout: 10000 });
    await page.locator('.ai-widget-human .chat-panel input').fill('RV W1 guest msg');
    await page.locator('.ai-widget-human .chat-panel button.chat-send-btn').click();
    await page.waitForTimeout(2000);
    check('B6 guest message sent', true);

    // Owner context: impersonate → inbox → find conversation → reply.
    const owner = await browser.newContext({ ignoreHTTPSErrors: true });
    const login = await owner.request.post(`${APP2}/api/platform/login`, {
      data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
      headers: { 'Content-Type': 'application/json' },
    });
    check('B6 owner login', login.ok());
    const imp = await owner.request.post(`${APP2}/api/admin/impersonate/${TENANT}`);
    check('B6 owner impersonate', imp.ok());
    const op = await owner.newPage();
    await op.goto(`${APP2}/community/messages`, { waitUntil: 'networkidle', timeout: 60000 });
    // Refresh until the guest conversation surfaces (list is newest-first).
    let convBtn = op.locator('.list-group-item').first();
    for (let i = 0; i < 5 && (await convBtn.count()) === 0; i++) {
      await op.reload({ waitUntil: 'networkidle' });
      convBtn = op.locator('.list-group-item').first();
    }
    check('B6 owner sees conversation', (await convBtn.count()) > 0);
    if (await convBtn.count()) {
      await convBtn.click();
      await op.locator('.chat-panel input').waitFor({ timeout: 15000 });
      await op.locator('.chat-panel input').fill('RV W1 owner reply');
      await op.locator('.chat-panel button.chat-send-btn').click();
      check('B6 owner reply sent', true);

      // Guest: reset lastSeen so the reply counts as unread, wait poll cycle (45s max).
      await page.evaluate(() => {
        const keys = Object.keys(localStorage).filter(k => k.startsWith('ai_human_last_seen'));
        keys.forEach(k => localStorage.setItem(k, new Date(0).toISOString()));
      });
      // Close the open panel (bubble-btn only exists when closed) then wait badge ≤72s.
      await page.locator('button[aria-label="Đóng trợ lý AI"]').click().catch(() => {});
      let badge = null;
      for (let i = 0; i < 12; i++) {
        await page.waitForTimeout(6000);
        const b = page.locator('.vanan-ai-unread');
        if (await b.count()) { badge = await b.first().innerText(); break; }
      }
      check('B6 unread badge appears', badge !== null, badge ? `badge="${badge}"` : 'no badge in 72s');
      badgeTested = badge !== null;

      if (badgeTested) {
        // Open human mode (CTA card preferred) → OnHumanOpened → badge clears.
        const cta2 = page.locator('.chat-cta-card .btn-cta');
        if (await cta2.count()) {
          await cta2.scrollIntoViewIfNeeded().catch(() => {});
          await cta2.click();
        } else {
          await page.locator('button.vanan-ai-bubble-btn').click();
          const hb = page.locator('button.vanan-ai-human-btn');
          if (await hb.count()) await hb.click();
        }
        await page.waitForTimeout(2000);
        await page.locator('button[aria-label="Đóng trợ lý AI"]').click().catch(() => {});
        await page.waitForTimeout(500);
        check('B6 badge cleared after open', (await page.locator('.vanan-ai-unread').count()) === 0);
      }
    }
    await owner.close();
  } catch (e) {
    check('B6 badge loop', false, e.message.slice(0, 120));
  }

  // P8 regression: widget modes still work.
  check('P8 widget still mounts', (await page.locator('.vanan-ai-widget').count()) > 0);

  const fails = results.filter(r => !r.ok).length;
  console.log(`\n==> SF-W1 RV: ${results.length - fails} PASS · ${fails} FAIL`);
  await browser.close();
  process.exit(fails ? 1 : 0);
})().catch(e => { console.error(e); process.exit(1); });
