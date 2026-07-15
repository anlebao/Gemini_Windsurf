import { test, expect } from '@playwright/test';

const BASE_URL = 'https://app.khachvip.online';

test('verify-vanabutton-modal — click VanAButton and check modal appears', async ({ browser }) => {
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

  // Click "Tạo tenant mới" button
  const createBtn = page.locator('button.vanan-button:has-text("Tạo tenant")').first();
  await createBtn.click({ timeout: 5000 });
  await page.waitForTimeout(3000);

  // Check if modal appeared
  const modalVisible = await page.locator('.vanan-modal:visible, .modal:visible, [class*="modal"]:visible').first().isVisible().catch(() => false);
  console.log('Modal visible:', modalVisible);

  // Also check for any form elements that would appear in the create modal
  const formInputs = await page.locator('input:visible').count();
  console.log('Visible inputs:', formInputs);

  // Take screenshot for evidence
  await page.screenshot({ path: 'reports/vanabutton-modal-test.png' });

  expect(modalVisible).toBe(true);

  await context.close();
});
