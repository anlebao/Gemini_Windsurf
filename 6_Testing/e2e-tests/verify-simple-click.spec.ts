import { test, expect } from '@playwright/test';

const BASE_URL = 'https://app.khachvip.online';

test('verify-simple-click — test plain HTML @onclick button', async ({ browser }) => {
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

  // Find the debug test button
  const testBtn = page.getByTestId('simple-test-btn');
  const isVisible = await testBtn.isVisible().catch(() => false);
  console.log('Test button visible:', isVisible);

  if (isVisible) {
    const textBefore = await testBtn.textContent();
    console.log('Button text before click:', textBefore?.trim());

    // Mark timestamp for server log correlation
    console.log('CLICK_TIMESTAMP:', new Date().toISOString());

    await testBtn.click({ timeout: 5000 });
    await page.waitForTimeout(3000);

    const textAfter = await testBtn.textContent();
    console.log('Button text after click:', textAfter?.trim());

    // Check if the click count increased
    const match = textAfter?.match(/clicked: (\d+)/);
    const clickCount = match ? parseInt(match[1]) : -1;
    console.log('Click count after:', clickCount);

    // If clickCount > 0, the @onclick handler ran (interactivity works)
    expect(clickCount).toBeGreaterThan(0);
  } else {
    console.log('Test button NOT visible — page may not have rendered correctly');
    console.log('Console logs:', JSON.stringify(allLogs));
    expect(false).toBe(true);
  }

  await context.close();
});
