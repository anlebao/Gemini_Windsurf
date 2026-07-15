import { test } from '@playwright/test';

const BASE_URL = 'https://khachvip.online';

test('diag5 — Blazor circuit state', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true });
  await context.request.post(`${BASE_URL}/api/platform/login`, {
    data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
    headers: { 'Content-Type': 'application/json' },
  });
  await context.request.post(`${BASE_URL}/api/admin/impersonate/00000000-0000-0000-0000-000000000001`);

  const page = await context.newPage();
  const allErrors: string[] = [];
  page.on('console', msg => allErrors.push(`[${msg.type()}] ${msg.text()}`));
  page.on('pageerror', err => allErrors.push(`[pageerror] ${err.message}`));

  await page.goto(`${BASE_URL}/products`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(8000); // Wait longer for Blazor circuit

  // Check Blazor state
  const blazorState = await page.evaluate(() => {
    const errorUi = document.getElementById('blazor-error-ui');
    const errorUiVisible = errorUi ? window.getComputedStyle(errorUi).display !== 'none' : false;
    // Check for Blazor connection indicators
    const hasBlazor = typeof (window as any).Blazor !== 'undefined';
    // Look for SignalR connection elements
    const reconnectModal = document.querySelector('[class*="reconnect"], [id*="reconnect"]');
    // Check if there are any blazor-attribute elements (indicates circuit is active)
    const blazorElements = document.querySelectorAll('[b-]').length;
    const blazorClickHandlers = document.querySelectorAll('[blazor\\:onclick], [blazor\\:onchange]').length;
    return {
      hasBlazor,
      errorUiVisible,
      errorUiText: errorUi?.textContent?.substring(0, 200),
      reconnectModal: reconnectModal ? reconnectModal.className : null,
      blazorElements,
      blazorClickHandlers,
    };
  });

  console.log('Blazor state:', JSON.stringify(blazorState));
  console.log('All errors:', JSON.stringify(allErrors.slice(0, 10)));

  // Try clicking and check if any network request is made
  let circuitActive = false;
  page.on('request', req => {
    if (req.url().includes('_blazor')) circuitActive = true;
  });

  await page.locator('button:has-text("Thêm sản phẩm")').first().click();
  await page.waitForTimeout(3000);

  // Check if _showCreateModal would have been set (look for modal in DOM)
  const modalInDom = await page.locator('[class*="modal"]').count();
  const vananModalInDom = await page.locator('vanan-modal, .vanan-modal, [class*="vanan-modal"]').count();

  // Get the actual HTML around the button
  const buttonHtml = await page.locator('button:has-text("Thêm sản phẩm")').first().evaluate(el => el.outerHTML);

  console.log('Circuit active (blazor requests):', circuitActive);
  console.log('Modal in DOM:', modalInDom);
  console.log('VananModal in DOM:', vananModalInDom);
  console.log('Button HTML:', buttonHtml);

  await context.close();
});
