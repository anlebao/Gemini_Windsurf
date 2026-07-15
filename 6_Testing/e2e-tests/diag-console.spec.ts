import { test } from '@playwright/test';

const BASE_URL = 'https://app.khachvip.online';

test('diag-console — ALL console output during circuit init', async ({ browser }) => {
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
  await page.waitForTimeout(10000); // Wait for circuit

  // Print ALL console output
  console.log('=== ALL CONSOLE OUTPUT ===');
  allLogs.forEach((log, i) => console.log(`[${i}] ${log}`));

  // Check for blazor-error-ui content (might be hidden but contain error)
  const errorUiHtml = await page.locator('#blazor-error-ui').innerHTML().catch(() => 'N/A');
  const errorUiStyle = await page.locator('#blazor-error-ui').getAttribute('style').catch(() => 'N/A');
  console.log('=== BLAZOR ERROR UI ===');
  console.log('Style:', errorUiStyle);
  console.log('HTML:', errorUiHtml?.substring(0, 500));

  // Check for components-reconnect-modal
  const reconnectHtml = await page.locator('#components-reconnect-modal').innerHTML().catch(() => 'N/A');
  const reconnectStyle = await page.locator('#components-reconnect-modal').getAttribute('style').catch(() => 'N/A');
  const reconnectVisible = await page.locator('#components-reconnect-modal').isVisible().catch(() => false);
  console.log('=== RECONNECT MODAL ===');
  console.log('Visible:', reconnectVisible, 'Style:', reconnectStyle);
  console.log('HTML:', reconnectHtml?.substring(0, 300));

  await context.close();
});
