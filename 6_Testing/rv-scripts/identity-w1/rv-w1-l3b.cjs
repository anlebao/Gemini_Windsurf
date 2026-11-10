// RV W1 L3b — /account/mfa state owner (adminvanan1 UI login) + login regression
const { chromium } = require('@playwright/test');
const APP2 = 'https://app2.khachvip.online';
const IP = '136.85.94.119';

(async () => {
  const browser = await chromium.launch({ headless: true, args: [`--host-resolver-rules=MAP app2.khachvip.online ${IP}`] });
  const results = [];
  const check = (name, ok, detail = '') => {
    results.push({ name, ok });
    console.log(`${ok ? '[PASS]' : '[FAIL]'} ${name}${detail ? ' — ' + detail : ''}`);
  };

  const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await ctx.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));

  // 1. login owner
  await page.goto(`${APP2}/Login`, { waitUntil: 'networkidle' });
  await page.fill('#username', 'adminvanan1');
  await page.fill('#password', '2026@vanan');
  await Promise.all([
    page.waitForResponse((r) => r.request().method() === 'POST' && r.url().includes('/Login'), { timeout: 20000 }).catch(() => null),
    page.click('button[type="submit"]'),
  ]);
  await page.waitForLoadState('networkidle', { timeout: 45000 }).catch(() => {});
  check('owner login → /sitemap (không MFA step)', page.url().includes('/sitemap'), page.url());
  check('login không pageerror', errors.length === 0, errors.join(' | '));

  // 2. /account/mfa state owner (MfaEnabled=false → nút Tạo mã QR)
  const resp = await page.goto(`${APP2}/account/mfa`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(5000); // Blazor circuit + load user
  check('/account/mfa HTTP 200', resp?.status() === 200, `status=${resp?.status()}`);
  const body = await page.locator('body').innerText();
  check('render Xác thực 2 lớp', body.includes('Xác thực 2 lớp'), '');
  const qrBtn = await page.locator('text=Tạo mã QR').count();
  check('state chưa bật MFA (nút Tạo mã QR)', qrBtn > 0, `qrBtn=${qrBtn}`);
  check('/account/mfa không pageerror', errors.length === 0, errors.join(' | '));

  // 3. click tạo QR → hiện QR + secret + input code
  if (qrBtn > 0) {
    await page.click('text=Tạo mã QR');
    await page.waitForTimeout(2500);
    const hasQr = await page.locator('img[data-testid="mfa-qr"]').count();
    const hasSecret = await page.locator('code.user-select-all').count();
    check('QR + secret hiển thị sau khi tạo', hasQr > 0 && hasSecret > 0, `qr=${hasQr} secret=${hasSecret}`);
  }

  await ctx.close();
  await browser.close();
  const failed = results.filter((r) => !r.ok).length;
  console.log(`\n=== RV W1 L3b: ${results.length - failed}/${results.length} PASS ===`);
  process.exit(failed ? 1 : 0);
})().catch((e) => { console.error(e); process.exit(1); });