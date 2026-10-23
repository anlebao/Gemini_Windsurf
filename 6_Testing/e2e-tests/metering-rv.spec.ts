import { test, expect } from '@playwright/test';

/**
 * Phí nền tảng — RV production (L3/L4). KHÁC metering-wallet/topup.spec.ts:
 * dùng storageState login thật (rv-metering-auth.cjs) vì DevLoginController
 * chỉ compile trong DEBUG — không tồn tại trong Release build production.
 *
 * storageState files:
 *  - auth/rv-metering-admin.json  → SystemAdmin
 *  - auth/rv-metering-tenant.json → impersonate Vạn An Trading
 */

test.describe('Metering RV — tenant wallet page', () => {
  test.use({ storageState: 'auth/rv-metering-tenant.json' });

  test('wallet page renders balance + ledger structure', async ({ page }) => {
    await page.goto('/wallet/phi-nen-tang', { waitUntil: 'networkidle' });
    const summary = page.getByTestId('wallet-summary');
    const unavailable = page.getByTestId('wallet-unavailable');
    await expect(summary.or(unavailable)).toBeVisible({ timeout: 30000 });
    if (await summary.isVisible()) {
      await expect(
        page.getByTestId('ledger-table').or(page.getByText('Chưa có giao dịch.'))
      ).toBeVisible();
      await expect(page.getByTestId('btn-create-topup')).toBeVisible();
    }
  });

  test('create top-up renders reference + VietQR image', async ({ page }) => {
    await page.goto('/wallet/phi-nen-tang', { waitUntil: 'networkidle' });
    const btn = page.getByTestId('btn-create-topup');
    if (!(await btn.isVisible({ timeout: 30000 }).catch(() => false))) {
      test.skip(true, 'Metering unavailable');
    }
    await page.getByTestId('topup-amount').fill('50000');
    await btn.click();
    await expect(page.getByTestId('topup-qr-panel')).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId('topup-reference')).toContainText('TOPUP-');
    const qr = page.getByTestId('topup-qr-image');
    if (await qr.isVisible().catch(() => false)) {
      await expect(qr).toHaveAttribute('src', /img\.vietqr\.io\/image\/.*-compact\.jpg/);
    }
  });

});

test.describe('Metering RV — guard non-admin', () => {
  // Impersonate giữ SystemAdmin role → không test guard được; cần Owner thật.
  test.use({ storageState: 'auth/rv-metering-owner.json' });

  test('tenant Owner is blocked from admin topups page', async ({ page }) => {
    await page.goto('/admin/metering/topups', { waitUntil: 'networkidle' });
    await page.waitForTimeout(3000); // Blazor circuit + policy redirect
    // SystemAdmin policy deny → redirect ra khỏi trang (cookie AccessDeniedPath
    // /Account/AccessDenied → 404 blank là systemic, giống mọi trang /admin/*).
    // Contract: KHÔNG render topups-table + rời khỏi URL gốc.
    await expect(page.getByTestId('topups-table')).not.toBeVisible();
    expect(page.url()).not.toContain('/admin/metering/topups');
  });
});

test.describe('Metering RV — admin pages', () => {
  test.use({ storageState: 'auth/rv-metering-admin.json' });

  test('rate-cards admin page renders head list', async ({ page }) => {
    await page.goto('/admin/metering/rate-cards', { waitUntil: 'networkidle' });
    await expect(page.getByTestId('rc-table').or(page.getByTestId('rc-empty'))).toBeVisible({ timeout: 30000 });
    await expect(page.getByTestId('rc-form-code')).toBeVisible();
  });

  test('topups admin page renders filter + config form', async ({ page }) => {
    await page.goto('/admin/metering/topups', { waitUntil: 'networkidle' });
    await expect(page.getByTestId('topup-status-filter')).toBeVisible({ timeout: 30000 });
    await expect(page.getByTestId('btn-save-config')).toBeVisible();
    await expect(page.getByTestId('cfg-bankcode')).toBeVisible();
    await expect(
      page.getByTestId('topups-table').or(page.getByTestId('topups-empty'))
    ).toBeVisible({ timeout: 15000 });
  });
});
