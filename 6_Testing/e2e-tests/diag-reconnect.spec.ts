import { test } from '@playwright/test';

const BASE_URL = 'https://app.khachvip.online';

test('diag-reconnect — inspect reconnect modal + network', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true });
  await context.request.post(`${BASE_URL}/api/platform/login`, {
    data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
    headers: { 'Content-Type': 'application/json' },
  });
  await context.request.post(`${BASE_URL}/api/admin/impersonate/00000000-0000-0000-0000-000000000001`);

  const page = await context.newPage();

  // Track ALL network requests
  const failedRequests: string[] = [];
  const blazorRequests: string[] = [];
  page.on('requestfailed', req => failedRequests.push(`${req.method()} ${req.url()} - ${req.failure()?.errorText}`));
  page.on('request', req => { if (req.url().includes('_blazor')) blazorRequests.push(`${req.method()} ${req.url()}`); });
  page.on('response', resp => { if (resp.url().includes('_blazor')) blazorRequests.push(`RESP ${resp.status()} ${resp.url()}`); });

  const allLogs: string[] = [];
  page.on('console', msg => allLogs.push(`[${msg.type()}] ${msg.text()}`));
  page.on('pageerror', err => allLogs.push(`[pageerror] ${err.message}`));

  await page.goto(`${BASE_URL}/admin/tenants`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(10000);

  // Check reconnect modal details
  const modalInfo = await page.locator('#components-reconnect-modal').evaluate(el => {
    const cs = window.getComputedStyle(el);
    return {
      className: el.className,
      display: cs.display,
      visibility: cs.visibility,
      opacity: cs.opacity,
      zIndex: cs.zIndex,
      innerHTML: el.innerHTML?.substring(0, 300),
      textContent: el.textContent?.trim()?.substring(0, 200),
    };
  }).catch(() => 'N/A');

  // Check if the overlay is actually blocking clicks
  const buttonInfo = await page.locator('button.vanan-button').first().evaluate(el => {
    const rect = el.getBoundingClientRect();
    const elementAtPoint = document.elementFromPoint(rect.x + 5, rect.y + 5);
    return {
      buttonRect: { x: rect.x, y: rect.y, w: rect.width, h: rect.height },
      elementAtButtonPoint: elementAtPoint ? elementAtPoint.tagName + '.' + elementAtPoint.className : 'N/A',
      isButtonOnTop: elementAtPoint === el,
    };
  });

  // Check response headers for CSP
  const response = await page.goto(`${BASE_URL}/admin/tenants`);
  const headers = response?.headers() || {};
  const csp = headers['content-security-policy'] || 'NONE';

  console.log('=== RECONNECT MODAL ===');
  console.log('Modal info:', JSON.stringify(modalInfo));
  console.log('=== BUTTON OVERLAY CHECK ===');
  console.log('Button info:', JSON.stringify(buttonInfo));
  console.log('=== CSP ===');
  console.log('CSP:', csp);
  console.log('=== FAILED REQUESTS ===');
  failedRequests.forEach(r => console.log(r));
  console.log('=== BLAZOR REQUESTS ===');
  blazorRequests.forEach(r => console.log(r));
  console.log('=== ALL CONSOLE ===');
  allLogs.forEach((l, i) => console.log(`[${i}] ${l}`));

  await context.close();
});
