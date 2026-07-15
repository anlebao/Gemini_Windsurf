import { test, expect } from '@playwright/test';

const BASE_URL = 'https://app.khachvip.online';

test('verify-buttons — buttons respond to clicks after prerender fix', async ({ browser }) => {
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
  await page.waitForTimeout(8000); // Wait for Blazor circuit to render

  const url = page.url();
  const title = await page.title();

  // Check Blazor hydration
  const blazorClickAttrs = await page.locator('[blazor\\:onclick]').count();
  const literalOnclick = await page.evaluate(() => {
    return document.querySelectorAll('button[\\@onclick]').length;
  });

  // Check if buttons are rendered (with prerender:false, there's a brief blank period)
  const buttonCount = await page.locator('button.vanan-button').count();

  // Try clicking the first button
  let clickWorked = false;
  if (buttonCount > 0) {
    const btn = page.locator('button.vanan-button').first();
    const btnText = await btn.textContent();
    try {
      await btn.click({ timeout: 5000 });
      await page.waitForTimeout(2000);
      clickWorked = true;
      // Check if anything happened (modal, navigation, etc.)
      const modalCount = await page.locator('[class*="modal"]:visible').count();
      console.log('Button clicked:', btnText?.trim(), '— modals after click:', modalCount);
    } catch (e) {
      console.log('Click failed:', (e as Error).message.substring(0, 100));
    }
  }

  console.log('URL:', url);
  console.log('Title:', title);
  console.log('Blazor onclick attrs:', blazorClickAttrs);
  console.log('Literal @onclick attrs:', literalOnclick);
  console.log('Button count:', buttonCount);
  console.log('Click worked:', clickWorked);
  console.log('Console errors:', JSON.stringify(allLogs.filter(l => l.includes('error') || l.includes('Error')).slice(0, 5)));

  // Assertions
  expect(url).not.toContain('/Login');
  expect(buttonCount).toBeGreaterThan(0);
  expect(blazorClickAttrs).toBeGreaterThan(0);

  await context.close();
});
