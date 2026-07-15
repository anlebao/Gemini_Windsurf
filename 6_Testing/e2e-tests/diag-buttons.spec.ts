import { test } from '@playwright/test';

const BASE_URL = 'https://app.khachvip.online';

test('diag-buttons — capture disabled state on /admin/tenants', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true });

  // Login
  await context.request.post(`${BASE_URL}/api/platform/login`, {
    data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
    headers: { 'Content-Type': 'application/json' },
  });
  await context.request.post(`${BASE_URL}/api/admin/impersonate/00000000-0000-0000-0000-000000000001`);

  const page = await context.newPage();
  const consoleErrors: string[] = [];
  page.on('console', msg => { if (msg.type() === 'error') consoleErrors.push(msg.text()); });

  await page.goto(`${BASE_URL}/admin/tenants`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(5000);

  const url = page.url();
  const title = await page.title();

  // Capture ALL buttons with their state
  const buttons = await page.locator('button').evaluateAll(els =>
    els.map(e => ({
      text: e.textContent?.trim().substring(0, 40),
      disabled: e.disabled,
      disabledAttr: e.getAttribute('disabled'),
      class: e.className,
      type: e.getAttribute('type'),
      onclick: e.getAttribute('@onclick') || e.getAttribute('blazor:onclick') || 'NONE',
      html: e.outerHTML.substring(0, 200),
    }))
  );

  // Check VanAButton specifically
  const vananButtons = buttons.filter(b => b.class.includes('vanan-button'));

  console.log('URL:', url);
  console.log('Title:', title);
  console.log('Total buttons:', buttons.length);
  console.log('VananButtons:', vananButtons.length);
  console.log('Disabled buttons:', buttons.filter(b => b.disabled).length);
  console.log('Console errors:', JSON.stringify(consoleErrors.slice(0, 5)));

  // Log all vanan buttons with disabled state
  vananButtons.forEach((b, i) => {
    console.log(`[VBtn ${i}] disabled=${b.disabled} attr="${b.disabledAttr}" onclick=${b.onclick} text="${b.text}"`);
  });

  // Log non-vanan buttons
  buttons.filter(b => !b.class.includes('vanan-button')).forEach((b, i) => {
    console.log(`[Btn ${i}] disabled=${b.disabled} attr="${b.disabledAttr}" text="${b.text}"`);
  });

  // Check if blazor circuit is active
  const blazorClickHandlers = await page.locator('[blazor\\:onclick]').count();
  const literalOnclick = await page.locator('button[@onclick]').count();
  console.log('Blazor onclick handlers:', blazorClickHandlers);
  console.log('Literal @onclick attrs:', literalOnclick);

  await context.close();
});
