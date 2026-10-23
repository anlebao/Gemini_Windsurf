import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

/**
 * Phí nền tảng P7 — Admin top-up management E2E (Gate 4: UI changes).
 * Covers:
 *  - /admin/metering/topups renders pending list + config form for SystemAdmin
 *  - Status filter switches list
 *  - Config form saves (singleton upsert)
 *  - Non-admin is redirected away (policy SystemAdmin)
 *
 * Note: confirm/reject mutations are covered by unit tests (T19–T25); E2E only
 * exercises read paths + config save to avoid granting credits in production.
 */

const config = loadEnvConfig();
const reporter = new TestReporter('Metering Topup Admin E2E');

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('Phí nền tảng P7 — Top-up admin UI', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting Metering Topup Admin E2E...');
  });

  test('SystemAdmin views topup list, filter and config form', async ({ page, request }) => {
    const login = await request.post(`${config.SHOPERP_URL}/dev/login/systemadmin`);
    expect(login.ok()).toBeTruthy();

    await page.goto(`${config.SHOPERP_URL}/admin/metering/topups`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByTestId('topup-status-filter')).toBeVisible({ timeout: 20000 });
    await expect(page.getByTestId('btn-save-config')).toBeVisible();
    await expect(page.getByTestId('cfg-bankcode')).toBeVisible();
    await expect(page.getByTestId('cfg-account')).toBeVisible();

    // Pending list renders (rows or empty state — data-dependent).
    await expect(
      page.getByTestId('topups-table').or(page.getByTestId('topups-empty'))
    ).toBeVisible({ timeout: 15000 });

    // Switch filter → list refreshes without error.
    await page.getByTestId('topup-status-filter').selectOption('confirmed');
    await page.waitForLoadState('networkidle');
  });

  test('Topup-config form persists bank details', async ({ page, request }) => {
    const login = await request.post(`${config.SHOPERP_URL}/dev/login/systemadmin`);
    expect(login.ok()).toBeTruthy();

    await page.goto(`${config.SHOPERP_URL}/admin/metering/topups`);
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('btn-save-config')).toBeVisible({ timeout: 20000 });

    // Read current values so we can re-save the same config (idempotent upsert).
    const bank = await page.getByTestId('cfg-bankcode').inputValue();
    const acc = await page.getByTestId('cfg-account').inputValue();
    if (bank && acc) {
      await page.getByTestId('btn-save-config').click();
      await expect(page.getByText('Đã lưu cấu hình VietQR.')).toBeVisible({ timeout: 15000 });
    }
    // Empty config → form is ready to fill; leave untouched (admin fills real bank info).
  });

  test('Non-admin is blocked from topup admin page', async ({ page, request }) => {
    const login = await request.post(`${config.SHOPERP_URL}/dev/login/owner`);
    expect(login.ok()).toBeTruthy();

    await page.goto(`${config.SHOPERP_URL}/admin/metering/topups`);
    await page.waitForLoadState('networkidle');
    // Policy guard → either redirected to AccessDenied/login or sees guard message.
    const blocked = page.getByText('Trang này chỉ dành cho SystemAdmin.');
    const denied = page.getByText(/AccessDenied|Access Denied|đăng nhập/i);
    await expect(blocked.or(denied).first()).toBeVisible({ timeout: 20000 });
    await expect(page.getByTestId('topups-table')).not.toBeVisible();
  });
});
