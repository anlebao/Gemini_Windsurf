import { test } from '@playwright/test';

const BASE_URL = 'https://app.khachvip.online';

test('verify-both-buttons — click both simple and VanAButton, check which works', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true });
  await context.request.post(`${BASE_URL}/api/platform/login`, {
    data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
    headers: { 'Content-Type': 'application/json' },
  });
  await context.request.post(`${BASE_URL}/api/admin/impersonate/00000000-0000-0000-0000-000000000001`);

  const page = await context.newPage();
  await page.goto(`${BASE_URL}/admin/tenants`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(8000);

  // 1. Click the simple test button
  const testBtn = page.getByTestId('simple-test-btn');
  const textBefore = await testBtn.textContent();
  console.log('Simple btn before:', textBefore?.trim());
  console.log('SIMPLE_CLICK_TIMESTAMP:', new Date().toISOString());
  await testBtn.click({ timeout: 5000 });
  await page.waitForTimeout(3000);
  const textAfter = await testBtn.textContent();
  console.log('Simple btn after:', textAfter?.trim());

  // 2. Click a VanAButton (Làm mới = Refresh)
  const refreshBtn = page.locator('button.vanan-button:has-text("Làm mới")').first();
  const refreshVisible = await refreshBtn.isVisible().catch(() => false);
  console.log('VanAButton (Làm mới) visible:', refreshVisible);
  if (refreshVisible) {
    console.log('VANABUTTON_CLICK_TIMESTAMP:', new Date().toISOString());
    await refreshBtn.click({ timeout: 5000 });
    await page.waitForTimeout(3000);
    console.log('VanAButton clicked');
  }

  await context.close();
});
