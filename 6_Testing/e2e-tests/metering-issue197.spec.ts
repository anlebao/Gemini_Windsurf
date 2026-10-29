import { test, expect } from '@playwright/test';

/**
 * Issue #197 — Metering nâng cấp E2E (Gate 4 + RV production L3).
 * Covers:
 *  - Rate-card scope UI: tạo card theo loại hình tenant (TenantType=HKD) → badge "Loại hình: HKD"
 *  - Master switch: toggle bật/tắt metering toàn hệ thống (restore trạng thái gốc trong finally)
 *  - /admin/metering/topups hiện TÊN tenant (không còn GUID prefix)
 *
 * Dùng storageState login thật (rv-metering-admin.json — SystemAdmin) vì
 * DevLoginController chỉ compile trong DEBUG, không tồn tại ở production.
 * Note: rate-card scope create dùng serviceCode timestamp (chain active không tạo lại được).
 */

test.describe('Issue #197 — Metering nâng cấp', () => {
  test.use({ storageState: 'auth/rv-metering-admin.json' });

  test('Rate-card scope theo loại hình tenant (TenantType=HKD)', async ({ page }) => {
    await page.goto('/admin/metering/rate-cards', { waitUntil: 'networkidle' });
    await expect(page.getByTestId('btn-save-rc')).toBeVisible({ timeout: 30000 });
    await expect(page.getByTestId('rc-form-scope')).toBeVisible();

    const code = `e2e.issue197.${Date.now()}`;
    await page.getByTestId('rc-form-code').fill(code);
    await page.getByTestId('rc-form-unit').fill('check');
    await page.getByTestId('rc-form-rate').fill('1');
    await page.getByTestId('rc-form-enforcement').selectOption('MeterOnly');
    await page.getByTestId('rc-form-scope').selectOption('TenantType');
    await page.getByTestId('rc-form-scope-value').selectOption('HKD');
    await page.getByTestId('btn-save-rc').click();

    const row = page.locator('[data-testid="rc-row"]', { hasText: code });
    await expect(row).toBeVisible({ timeout: 20000 });
    await expect(row).toContainText('Loại hình: HKD');
  });

  test('Master switch bật/tắt metering toàn hệ thống (restore gốc)', async ({ page }) => {
    await page.goto('/admin/metering/rate-cards', { waitUntil: 'networkidle' });
    await expect(page.getByTestId('metering-master-toggle')).toBeVisible({ timeout: 30000 });

    const wasChecked = await page.getByTestId('metering-master-toggle').isChecked();
    try {
      await page.getByTestId('metering-master-toggle').click();
      await expect(page.getByTestId('metering-master-switch')).toContainText(
        wasChecked ? 'ĐANG TẮT' : 'Đang bật', { timeout: 10000 });

      await page.getByTestId('metering-master-toggle').click();
      await expect(page.getByTestId('metering-master-switch')).toContainText(
        wasChecked ? 'Đang bật' : 'ĐANG TẮT', { timeout: 10000 });
    } finally {
      // FAIL-SAFE: restore trạng thái gốc nếu vẫn lệch
      const nowChecked = await page.getByTestId('metering-master-toggle').isChecked();
      if (nowChecked !== wasChecked) {
        await page.getByTestId('metering-master-toggle').click();
        await expect(page.getByTestId('metering-master-toggle')).toBeChecked({ checked: wasChecked, timeout: 10000 });
      }
    }
  });

  test('Topup list hiện tên tenant (không phải GUID prefix)', async ({ page }) => {
    await page.goto('/admin/metering/topups', { waitUntil: 'networkidle' });

    await expect(page.getByTestId('topups-table').or(page.getByTestId('topups-empty'))).toBeVisible({ timeout: 30000 });
    const names = page.locator('[data-testid="topup-tenant-name"]');
    if ((await names.count()) > 0) {
      const text = (await names.first().textContent()) ?? '';
      expect(text.trim().length).toBeGreaterThan(0);
      expect(text.trim().endsWith('…')).toBeFalsy();
    }
  });
});