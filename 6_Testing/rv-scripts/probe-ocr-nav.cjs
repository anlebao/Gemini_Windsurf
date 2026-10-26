// Probe: SPA nav ?tab=ocr — OnParametersSet có chạy? _activeTab có đổi?
const { chromium } = require('@playwright/test');

const APP2 = 'https://app2.khachvip.online';
const GATEWAY_IP = process.env.GATEWAY_IP ?? '136.85.94.119';

(async () => {
  const browser = await chromium.launch({
    headless: true,
    args: ['--host-resolver-rules=' +
      `MAP app2.khachvip.online ${GATEWAY_IP}, MAP api2.khachvip.online ${GATEWAY_IP}, ` +
      `MAP api.khachvip.online ${GATEWAY_IP}, MAP khachvip.online ${GATEWAY_IP}, MAP www2.khachvip.online ${GATEWAY_IP}`],
  });
  const context = await browser.newContext({
    ignoreHTTPSErrors: true,
    storageState: require('path').join(__dirname, 'auth/rv-ocr-admin.json'),
  });
  const page = await context.newPage();
  page.on('console', (m) => { if (m.type() === 'error') console.log('CONSOLE_ERR:', m.text().slice(0, 200)); });
  page.on('pageerror', (e) => console.log('PAGEERROR:', e.message.slice(0, 200)));

  await page.goto(`${APP2}/admin/tenants`, { waitUntil: 'networkidle', timeout: 60000 });
  await page.waitForTimeout(2500);

  // Trạng thái trước click
  const activeTabBefore = await page.locator('.tenant-tab.active').textContent().catch(() => 'NONE');
  console.log('activeTab before click:', JSON.stringify(activeTabBefore));

  // Đếm locator khớp "Onboarding OCR"
  const count = await page.locator('a, button', { hasText: 'Onboarding OCR' }).count();
  console.log('Onboarding OCR locators:', count);
  for (let i = 0; i < count; i++) {
    const tag = await page.locator('a, button', { hasText: 'Onboarding OCR' }).nth(i).evaluate((el) => el.tagName + '|' + (el.getAttribute('href') ?? '') + '|' + (el.textContent || '').trim().slice(0, 40));
    console.log(`  [${i}]`, tag);
  }

  // Click nav item (thường là index 0 — sidebar)
  await page.locator('a, button', { hasText: 'Onboarding OCR' }).first().click();
  await page.waitForTimeout(2000);
  console.log('URL after click:', page.url());
  const activeTabAfter = await page.locator('.tenant-tab.active').textContent().catch(() => 'NONE');
  console.log('activeTab after click:', JSON.stringify(activeTabAfter));
  const wizardVisible = await page.locator('.ocr-wizard').isVisible().catch(() => false);
  console.log('wizard visible:', wizardVisible);
  const cards = await page.locator('.ocr-case-card').count();
  console.log('case cards:', cards);

  await browser.close();
  process.exit(0);
})();