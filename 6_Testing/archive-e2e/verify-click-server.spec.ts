import { test } from '@playwright/test';

const BASE_URL = 'https://app.khachvip.online';

test('verify-click-server — click button and check server logs', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true });
  await context.request.post(`${BASE_URL}/api/platform/login`, {
    data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
    headers: { 'Content-Type': 'application/json' },
  });
  await context.request.post(`${BASE_URL}/api/admin/impersonate/00000000-0000-0000-0000-000000000001`);

  const page = await context.newPage();
  const allLogs: string[] = [];
  page.on('console', msg => allLogs.push(`[${msg.type()}] ${msg.text()}`));
  page.on('pageerror', err => allLogs.push(`[pageerror] ${err.message}`));

  await page.goto(`${BASE_URL}/admin/tenants`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(8000);

  // Get button info before click
  const buttons = await page.locator('button.vanan-button').allTextContents();
  console.log('Buttons found:', JSON.stringify(buttons.map(b => b.trim().substring(0, 30))));

  // Click the first button
  const firstBtn = page.locator('button.vanan-button').first();
  const btnText = await firstBtn.textContent();
  console.log('Clicking button:', btnText?.trim());

  // Mark the time for server log correlation
  console.log('CLICK_TIMESTAMP:', new Date().toISOString());

  await firstBtn.click({ timeout: 5000 });
  await page.waitForTimeout(5000);

  // Check what happened
  const modalVisible = await page.locator('.modal:visible, [class*="modal"]:visible, .vanan-modal:visible').first().isVisible().catch(() => false);
  console.log('Modal visible after click:', modalVisible);

  // Check DOM for blazor event attributes
  const domInfo = await page.evaluate(() => {
    const btn = document.querySelector('button.vanan-button');
    if (!btn) return null;
    const attrs = Array.from(btn.attributes).map(a => `${a.name}="${a.value.substring(0, 50)}"`);
    return { attrs, hasBlazorOnclick: btn.hasAttribute('blazor:onclick') };
  });
  console.log('Button DOM attrs:', JSON.stringify(domInfo?.attrs));
  console.log('Has blazor:onclick:', domInfo?.hasBlazorOnclick);

  console.log('Console logs:', JSON.stringify(allLogs));

  await context.close();
});
