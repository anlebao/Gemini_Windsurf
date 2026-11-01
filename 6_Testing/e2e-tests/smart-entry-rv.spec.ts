import { test, expect, Page } from '@playwright/test';

// Task PG Smart Entry AI — RV production (post-deploy 234d2425, flag SmartEntryEnabled
// widened all tenants qua SystemSetting ShopFeature:{tenant}:SmartEntryEnabled=true).
// READ-ONLY: parse + apply + prefill only — KHÔNG submit phiếu nào lên production.
// Impersonate Cafe Tân Quy (DN TT133, đã pilot account-map) qua platform login + impersonate
// (pattern accounting-issue198.spec.ts — apiRequestContext không qua host-resolver-rules).

const PILOT_TENANT = '7c021960-6d6c-4de4-88a1-cf8373184f49';

async function loginAndImpersonate(page: Page, tenantId: string) {
  await page.goto('/login');
  await page.waitForLoadState('domcontentloaded');
  const loginOk = await page.evaluate(async () => {
    const r = await fetch('/api/platform/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ Username: 'sysadmin@vanan.vn', Password: '2026@vanan' }),
      credentials: 'include',
    });
    return r.ok;
  });
  expect(loginOk, 'platform login should succeed').toBeTruthy();
  const impOk = await page.evaluate(async (t) => {
    const r = await fetch(`/api/admin/impersonate/${t}`, { method: 'POST', credentials: 'include' });
    return r.ok;
  }, tenantId);
  expect(impOk, `impersonate ${tenantId} should succeed`).toBeTruthy();
}

test.describe('RV Task PG — Smart Entry AI (production)', () => {
  test('transfers page: flag ON → toggle + AI parse + apply prefill (read-only)', async ({ page }) => {
    await loginAndImpersonate(page, PILOT_TENANT);
    await page.goto('/accounting/transfers');
    await page.waitForLoadState('networkidle');

    // 1) Route live — form manual render
    await expect(page.getByTestId('transfer-from')).toBeVisible({ timeout: 30000 });
    await expect(page.getByTestId('transfer-save')).toBeVisible();

    // 2) Flag ON → toggle 2 mode hiện
    await expect(page.getByTestId('entry-mode-toggle')).toBeVisible({ timeout: 30000 });

    // 3) AI mode → parse câu chuyển tiền
    await page.getByTestId('entry-mode-toggle').getByText('Nhập bằng mô tả').click();
    await expect(page.getByTestId('smart-entry-input')).toBeVisible({ timeout: 15000 });
    await page.getByTestId('smart-entry-input').fill('Hôm nay rút tiền gửi ngân hàng một trăm triệu nhập vào quỹ tiền mặt');
    await page.getByTestId('smart-entry-parse').click();

    await expect(page.getByTestId('smart-entry-result')).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId('smart-entry-intent')).toHaveText('Chuyển tiền: NH → quỹ');
    // Amount format theo ambient culture (prod invariant → "100,000,000"; local vi-VN → "100.000.000")
    await expect(page.getByTestId('smart-entry-result')).toContainText(/100[.,]000[.,]000/);

    // 4) Apply → prefill amount (không submit — read-only)
    await page.getByTestId('smart-entry-apply').click();
    await expect(page.getByTestId('transfer-amount')).toHaveValue('100000000');
  });

  test('expense page: transfer intent → route link /accounting/transfers?desc=', async ({ page }) => {
    await loginAndImpersonate(page, PILOT_TENANT);
    await page.goto('/accounting/expenses');
    await page.waitForLoadState('networkidle');

    await expect(page.getByTestId('entry-mode-toggle')).toBeVisible({ timeout: 30000 });
    await page.getByTestId('entry-mode-toggle').getByText('Nhập bằng mô tả').click();
    await page.getByTestId('smart-entry-input').fill('rút tiền gửi ngân hàng 50 triệu nhập vào quỹ tiền mặt');
    await page.getByTestId('smart-entry-parse').click();
    await expect(page.getByTestId('smart-entry-result')).toBeVisible({ timeout: 15000 });

    await page.getByTestId('smart-entry-apply').click();
    const routeLink = page.getByTestId('smart-route-link');
    await expect(routeLink).toBeVisible({ timeout: 15000 });
    await expect(routeLink).toHaveAttribute('href', /\/accounting\/transfers\?desc=/);
  });

  test('deep-link ?desc= prefills transfer form', async ({ page }) => {
    await loginAndImpersonate(page, PILOT_TENANT);
    const desc = encodeURIComponent('rút tiền gửi ngân hàng hai trăm triệu nhập vào quỹ tiền mặt');
    await page.goto(`/accounting/transfers?desc=${desc}`);
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('transfer-amount')).toHaveValue('200000000', { timeout: 30000 });
  });
});
