import { test } from '@playwright/test';

const BASE_URL = 'https://app.khachvip.online';

test('verify-modal-state — click VanAButton and check _showCreateModal state', async ({ browser }) => {
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

  // Check initial state
  const stateBefore = await page.getByTestId('modal-state').textContent();
  console.log('Modal state before:', stateBefore?.trim());

  // Click "Tạo tenant mới" VanAButton
  const createBtn = page.locator('button.vanan-button:has-text("Tạo tenant")').first();
  console.log('CLICK_TIMESTAMP:', new Date().toISOString());
  await createBtn.click({ timeout: 5000 });
  await page.waitForTimeout(3000);

  // Check state after click
  const stateAfter = await page.getByTestId('modal-state').textContent();
  console.log('Modal state after:', stateAfter?.trim());

  // Check if modal appeared
  const modalVisible = await page.locator('.vanan-modal:visible, .modal:visible, [class*="modal"]:visible').first().isVisible().catch(() => false);
  console.log('Modal visible:', modalVisible);

  await page.screenshot({ path: 'reports/modal-state-test.png' });

  await context.close();
});
