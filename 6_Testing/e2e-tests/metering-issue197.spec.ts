import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

/**
 * Issue #197 — Metering nâng cấp E2E (Gate 4: UI changes).
 * Covers:
 *  - Rate-card scope UI: tạo card theo loại hình tenant (TenantType=HKD) → badge "Loại hình: HKD"
 *  - Master switch: toggle bật/tắt metering toàn hệ thống (restore trạng thái gốc trong finally)
 *  - /admin/metering/topups hiện TÊN tenant (không còn GUID prefix)
 *
 * Note: rate-card scope create dùng serviceCode timestamp (chain active không tạo lại được).
 * Master switch test tự restore trạng thái gốc — không để production ở trạng thái sai.
 */

const config = loadEnvConfig();
const reporter = new TestReporter('Metering Issue #197 E2E');

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('Issue #197 — Metering nâng cấp', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting Metering Issue #197 E2E...');
  });

  test('Rate-card scope theo loại hình tenant (TenantType=HKD)', async ({ page, request }) => {
    const login = await request.post(`${config.SHOPERP_URL}/dev/login/systemadmin`);
    expect(login.ok()).toBeTruthy();

    await page.goto(`${config.SHOPERP_URL}/admin/metering/rate-cards`);
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('btn-save-rc')).toBeVisible({ timeout: 20000 });
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
    await expect(row).toBeVisible({ timeout: 15000 });
    await expect(row).toContainText('Loại hình: HKD');
  });

  test('Master switch bật/tắt metering toàn hệ thống (restore gốc)', async ({ page, request }) => {
    const login = await request.post(`${config.SHOPERP_URL}/dev/login/systemadmin`);
    expect(login.ok()).toBeTruthy();

    await page.goto(`${config.SHOPERP_URL}/admin/metering/rate-cards`);
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('metering-master-toggle')).toBeVisible({ timeout: 20000 });

    const wasChecked = await page.getByTestId('metering-master-toggle').isChecked();
    try {
      // Flip trạng thái → verify label đổi → flip lại
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

  test('Topup list hiện tên tenant (không phải GUID prefix)', async ({ page, request }) => {
    const login = await request.post(`${config.SHOPERP_URL}/dev/login/systemadmin`);
    expect(login.ok()).toBeTruthy();

    await page.goto(`${config.SHOPERP_URL}/admin/metering/topups`);
    await page.waitForLoadState('networkidle');

    // Cột tên tenant có data-testid — hiển thị hoặc trạng thái rỗng đều hợp lệ
    await expect(page.getByTestId('topups-table').or(page.getByTestId('topups-empty'))).toBeVisible({ timeout: 20000 });
    const names = page.locator('[data-testid="topup-tenant-name"]');
    if ((await names.count()) > 0) {
      const text = (await names.first().textContent()) ?? '';
      expect(text.trim().length).toBeGreaterThan(0);
      // Không được là GUID prefix dạng "xxxxxxxx…"
      expect(text.trim().endsWith('…')).toBeFalsy();
    }
  });
});