const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch({ args: ['--host-resolver-rules=MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119'] });
  const page = await browser.newPage({ acceptDownloads: true, ignoreHTTPSErrors: true });
  await page.goto('https://app2.khachvip.online/login', { waitUntil: 'domcontentloaded' });
  await page.evaluate(async () => {
    await fetch('/api/platform/login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ Username: 'sysadmin@vanan.vn', Password: '2026@vanan' }), credentials: 'include' });
    await fetch('/api/admin/impersonate/7c021960-6d6c-4de4-88a1-cf8373184f49', { method: 'POST', credentials: 'include' });
  });
  const dump = () => page.evaluate(() => ({
    text: sessionStorage.getItem('vanan.smartEntryText'),
    multi: (sessionStorage.getItem('vanan.smartEntryMulti') || '').slice(0, 120),
    processed: sessionStorage.getItem('vanan.smartEntryProcessed'),
  }));
  await page.goto('https://app2.khachvip.online/accounting/revenue', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(3000);
  const toggle = page.getByTestId('entry-mode-toggle');
  try { await toggle.waitFor({ state: 'visible', timeout: 10000 }); } catch { console.log('NO TOGGLE'); await browser.close(); return; }
  await toggle.getByText('Nhập bằng mô tả').click();
  await page.getByTestId('smart-entry-input').fill('thu tiền bán hàng 2 triệu và chi tiền điện 1 triệu');
  await page.getByTestId('smart-entry-ai').click();
  await page.getByTestId('smart-entry-multi').waitFor({ state: 'visible', timeout: 30000 });
  console.log('AFTER PARSE:', JSON.stringify(await dump()));
  await page.getByTestId('smart-entry-multi-apply').first().click();
  await page.getByTestId('smart-entry-applied-feedback').waitFor({ state: 'visible', timeout: 15000 });
  await page.waitForTimeout(1000);
  await page.getByRole('button', { name: 'Lưu Doanh Thu' }).click();
  await page.getByTestId('smart-entry-multi-done').first().waitFor({ state: 'visible', timeout: 30000 }).catch(() => console.log('NO DONE BADGE after save'));
  await page.waitForTimeout(1500);
  console.log('AFTER SAVE:', JSON.stringify(await dump()));
  await browser.close();
})();
