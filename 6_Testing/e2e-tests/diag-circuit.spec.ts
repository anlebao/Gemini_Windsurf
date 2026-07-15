import { test } from '@playwright/test';

const BASE_URL = 'https://app.khachvip.online';

test('diag-circuit — Blazor hydration on app.khachvip.online', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true });

  // Login
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
  await page.waitForTimeout(8000); // Wait for Blazor circuit

  // Check Blazor state
  const state = await page.evaluate(() => {
    const errorUi = document.getElementById('blazor-error-ui');
    const errorUiVisible = errorUi ? getComputedStyle(errorUi).display !== 'none' : false;
    // Check for blazor event attributes (should be blazor:onclick, not @onclick)
    const blazorClickAttrs = document.querySelectorAll('[blazor\\:onclick]').length;
    const literalOnclick = document.querySelectorAll('button[\\@onclick]').length;
    // Check if Blazor object exists
    const hasBlazor = typeof (window as any).Blazor !== 'undefined';
    // Check for reconnect UI
    const reconnectUi = document.querySelector('#components-reconnect-modal');
    const reconnectVisible = reconnectUi ? getComputedStyle(reconnectUi).display !== 'none' : false;
    return { errorUiVisible, errorUiText: errorUi?.textContent?.substring(0, 200), blazorClickAttrs, literalOnclick, hasBlazor, reconnectVisible };
  });

  // Check if blazor.web.js loaded
  const blazorScriptLoaded = await page.locator('script[src*="blazor"]').count();
  const blazorScriptResponse = await page.evaluate(() => {
    const scripts = document.querySelectorAll('script[src*="blazor"]');
    return Array.from(scripts).map(s => s.getAttribute('src'));
  });

  console.log('State:', JSON.stringify(state));
  console.log('Blazor scripts:', blazorScriptLoaded, JSON.stringify(blazorScriptResponse));
  console.log('All logs:', JSON.stringify(allLogs.filter(l => l.includes('blazor') || l.includes('circuit') || l.includes('Error') || l.includes('error')).slice(0, 10)));

  // Try clicking a button and see if any network activity happens
  let blazorRequestMade = false;
  page.on('request', req => { if (req.url().includes('_blazor')) blazorRequestMade = true; });

  const btn = page.locator('button.vanan-button').first();
  await btn.click({ timeout: 5000 }).catch(e => console.log('Click error:', e.message.substring(0, 100)));
  await page.waitForTimeout(3000);

  console.log('Blazor request after click:', blazorRequestMade);
  console.log('URL after click:', page.url());

  await context.close();
});
