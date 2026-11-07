// Task AI-P9 RV (2026-11-07) — ShopInbox tab "AI trả lời khách" trên production.
// Flow: login sysadmin → impersonate tenant An Tâm (e2d91cdf) → /community/messages
// → tab "AI trả lời khách" (flag TenantAiEnabled=true) → sessions list → drill 1 session.
const { chromium } = require('@playwright/test');

const APP2 = 'https://app2.khachvip.online';
const TENANT = 'e2d91cdf-d3c4-4399-b96e-e772a7a41630'; // An Tâm pilot

(async () => {
  const browser = await chromium.launch({
    headless: true,
    args: ['--host-resolver-rules=MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119'],
  });
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const results = [];
  const check = (name, ok, extra = '') => {
    results.push({ name, ok });
    console.log(`${ok ? 'PASS' : 'FAIL'} ${name}${extra ? ' — ' + extra : ''}`);
  };

  const login = await context.request.post(`${APP2}/api/platform/login`, {
    data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
    headers: { 'Content-Type': 'application/json' },
  });
  check('login sysadmin', login.ok());

  const imp = await context.request.post(`${APP2}/api/admin/impersonate/${TENANT}`);
  check('impersonate An Tâm', imp.ok());

  const page = await context.newPage();
  page.on('pageerror', e => console.log('PAGEERROR:', e.message));

  const resp = await page.goto(`${APP2}/community/messages`, { waitUntil: 'networkidle', timeout: 60000 });
  check('inbox page 200', resp?.status() === 200, `status=${resp?.status()}`);

  // Tab "AI trả lời khách" hiện khi TenantAiEnabled ON.
  const aiTab = page.locator('.inbox-tabs button', { hasText: 'AI trả lời khách' });
  await aiTab.waitFor({ timeout: 15000 }).catch(() => {});
  check('tab AI visible', (await aiTab.count()) > 0);

  if ((await aiTab.count()) > 0) {
    await aiTab.click();
    await page.waitForTimeout(4000); // lazy load sessions
    const sessions = page.locator('.list-group-item');
    const n = await sessions.count();
    check('sessions loaded', n > 0, `${n} session(s)`);
    if (n > 0) {
      check('badges present', (await page.locator('.badge').count()) > 0);
      await sessions.first().click();
      await page.waitForTimeout(3000);
      const bubbles = await page.locator('.ai-conv-messages .rounded').count();
      check('drill session messages', bubbles > 0, `${bubbles} bubble(s)`);
      check('read-only footer', (await page.getByText('chỉ xem').count()) > 0);
    }
  }

  const fails = results.filter(r => !r.ok).length;
  console.log(`\n==> ${results.length - fails} PASS · ${fails} FAIL`);
  await browser.close();
  process.exit(fails ? 1 : 0);
})().catch(e => { console.error(e); process.exit(1); });
