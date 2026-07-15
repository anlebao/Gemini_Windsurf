import { test, expect } from '@playwright/test';
import { createAuthenticatedPage } from './utils/prod-auth';

// Test via khachvip.online directly (has /_blazor configured in nginx)
const BASE_URL = 'https://khachvip.online';

test('diag4 — Blazor circuit via khachvip.online', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true });

  // Login via API
  const loginResp = await context.request.post(`${BASE_URL}/api/platform/login`, {
    data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
    headers: { 'Content-Type': 'application/json' },
  });
  console.log('Login:', loginResp.status());
  await context.request.post(`${BASE_URL}/api/admin/impersonate/00000000-0000-0000-0000-000000000001`);

  const page = await context.newPage();
  const consoleErrors: string[] = [];
  page.on('console', msg => { if (msg.type() === 'error') consoleErrors.push(msg.text()); });

  try {
    await page.goto(`${BASE_URL}/products`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(5000);

    const title = await page.title();
    const h1Texts = await page.locator('h1').allTextContents();
    const btnCount = await page.locator('button').count();

    // Click create button
    const createBtn = page.locator('button:has-text("Thêm sản phẩm")').first();
    if (await createBtn.isVisible()) {
      await createBtn.click();
      await page.waitForTimeout(3000);
    }

    const modalCount = await page.locator('[class*="modal"]:visible').count();
    const formCount = await page.locator('form:visible').count();

    console.log('Title:', title);
    console.log('H1:', JSON.stringify(h1Texts));
    console.log('Buttons:', btnCount);
    console.log('Console errors:', JSON.stringify(consoleErrors.filter(e => e.includes('circuit') || e.includes('blazor')).slice(0, 3)));
    console.log('Modal after click:', modalCount);
    console.log('Forms after click:', formCount);
  } finally {
    await context.close();
  }
});
