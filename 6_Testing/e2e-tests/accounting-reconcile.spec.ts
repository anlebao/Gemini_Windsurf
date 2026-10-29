import { test, expect } from '@playwright/test';

/**
 * Tra soát số liệu kế toán P4 (SRS v1.1 — Gate 4 UI E2E).
 * SystemAdmin → /admin/reconcile → chọn tenant + kỳ → Tra soát →
 * 4 thẻ summary + bảng check (badge level) + suggestions.
 * Auth: storageState SystemAdmin (rv-metering-admin.json — nhóm SystemAdmin admin pages).
 */
test.describe('Tra soát số liệu kế toán — /admin/reconcile', () => {
  test.use({ storageState: 'auth/rv-metering-admin.json' });

  test('SystemAdmin chạy reconcile hiển thị summary + checks', async ({ page }) => {
    await page.goto('/admin/reconcile');
    await page.waitForLoadState('networkidle');

    // Form phải hiện
    await expect(page.getByTestId('rc-tenant')).toBeVisible();
    await expect(page.getByTestId('btn-run-reconcile')).toBeVisible();

    // Chọn tenant đầu tiên có sẵn
    const tenantSelect = page.getByTestId('rc-tenant');
    const options = tenantSelect.locator('option');
    const count = await options.count();
    test.skip(count < 2, 'Cần ít nhất 1 tenant để chạy reconcile');
    await tenantSelect.selectOption({ index: 1 });
    await page.locator('#rc-period').fill(new Date().toISOString().slice(0, 7));

    await page.getByTestId('btn-run-reconcile').click();

    // Summary 4 thẻ + bảng checks phải xuất hiện
    await expect(page.getByTestId('rc-summary')).toBeVisible({ timeout: 15000 });
    const rows = page.getByTestId('rc-check-row');
    await expect(rows.first()).toBeVisible();
    expect(await rows.count()).toBeGreaterThanOrEqual(10); // R1-R12

    // Nút Chạy lại xuất hiện sau lần chạy đầu
    await expect(page.getByTestId('btn-rerun-reconcile')).toBeVisible();
  });
});
