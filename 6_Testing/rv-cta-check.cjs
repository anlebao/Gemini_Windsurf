const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  const errors = [];
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
  page.on('pageerror', e => errors.push(String(e)));
  await page.goto('https://timlathay.com/', { waitUntil: 'networkidle', timeout: 60000 });
  const btn = page.getByRole('button', { name: /Đăng ký ngay/ });
  await btn.waitFor({ state: 'visible', timeout: 30000 });
  await Promise.all([
    page.waitForNavigation({ timeout: 30000 }).catch(() => null),
    btn.click(),
  ]);
  await page.waitForTimeout(3000);
  console.log('LANDED URL:', page.url());
  console.log('CONSOLE ERRORS:', errors.length ? errors.slice(0,5) : 'none');
  await browser.close();
})().catch(e => { console.error('FAIL', e.message); process.exit(1); });
