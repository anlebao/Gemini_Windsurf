const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  const errors = [];
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
  page.on('pageerror', e => errors.push(String(e)));
  await page.goto('https://timlathay.com/', { waitUntil: 'networkidle', timeout: 90000 });
  // chờ Blazor Server circuit connect (OnClick chạy server-side)
  await page.waitForTimeout(8000);
  const btn = page.getByRole('button', { name: /Đăng ký ngay/ });
  await btn.waitFor({ state: 'visible', timeout: 30000 });
  console.log('button visible, clicking...');
  await btn.click();
  // forceLoad → full navigation; chờ URL đổi hoặc timeout
  await page.waitForURL(/onboard/, { timeout: 30000 }).catch(() => console.log('no nav to /onboard'));
  await page.waitForTimeout(2000);
  console.log('LANDED URL:', page.url());
  console.log('CONSOLE ERRORS:', errors.length ? errors.slice(0,5) : 'none');
  await browser.close();
})().catch(e => { console.error('FAIL', e.message); process.exit(1); });
