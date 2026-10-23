import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

/**
 * Phí nền tảng P6 — Metering Rate Card Admin UI E2E (Gate 4: UI changes).
 * Covers:
 *  - /admin/metering/rate-cards renders head-version table for SystemAdmin
 *  - Create rate card via form → new row appears (unique serviceCode per run)
 *  - History toggle shows version states
 *
 * Note: serviceCode uses timestamp suffix — chain cannot be re-created once active
 * (POST returns 409 serviceCode_active_exists), so each run uses a fresh code.
 */

const config = loadEnvConfig();
const reporter = new TestReporter('Metering RateCard E2E');

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('Phí nền tảng P6 — Rate Card Admin UI', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting Metering RateCard E2E...');
  });

  test('SystemAdmin views rate-card list and creates a new item', async ({ page, request }) => {
    const login = await request.post(`${config.SHOPERP_URL}/dev/login/systemadmin`);
    expect(login.ok()).toBeTruthy();

    await page.goto(`${config.SHOPERP_URL}/admin/metering/rate-cards`);
    await page.waitForLoadState('networkidle');

    // Page renders — table (or empty state) + create form
    await expect(page.getByTestId('btn-save-rc')).toBeVisible({ timeout: 20000 });
    await expect(page.getByTestId('rc-form-code')).toBeVisible();
    await expect(page.getByTestId('rc-form-unit')).toBeVisible();
    await expect(page.getByTestId('rc-form-rate')).toBeVisible();
    await expect(page.getByTestId('rc-form-enforcement')).toBeVisible();

    // Create a unique rate card (code per-run — rerun would 409 otherwise)
    const code = `e2e.rc.${Date.now()}`;
    await page.getByTestId('rc-form-code').fill(code);
    await page.getByTestId('rc-form-unit').fill('check');
    await page.getByTestId('rc-form-rate').fill('1');
    await page.getByTestId('rc-form-enforcement').selectOption('MeterOnly');
    await page.getByTestId('btn-save-rc').click();

    // Row appears in the head table
    const row = page.locator('[data-testid="rc-row"]', { hasText: code });
    await expect(row).toBeVisible({ timeout: 15000 });
    await expect(row).toContainText('check');
    await expect(row).toContainText('MeterOnly');
  });

  test('History toggle reveals version state column', async ({ page, request }) => {
    const login = await request.post(`${config.SHOPERP_URL}/dev/login/systemadmin`);
    expect(login.ok()).toBeTruthy();

    await page.goto(`${config.SHOPERP_URL}/admin/metering/rate-cards`);
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('btn-save-rc')).toBeVisible({ timeout: 20000 });

    await page.getByTestId('rc-history-toggle').check();
    // History mode adds status column — at least the header renders
    await expect(page.locator('th', { hasText: 'Trạng thái' })).toBeVisible();
  });
});
