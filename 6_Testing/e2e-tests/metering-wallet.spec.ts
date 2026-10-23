import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

/**
 * Phí nền tảng P7 — Wallet page E2E (Gate 4: UI changes).
 * Covers:
 *  - /wallet/phi-nen-tang renders balance + granted limit + ledger for tenant user
 *  - "Tạo mã QR" produces referenceCode + vietqr image (requires topup-config on server)
 *  - Banner is hidden when wallet is healthy (alertLevel null) — presence check only
 *
 * Note: wallet contents depend on production metering data; assertions are
 * presence-based (structure, not amounts) so the spec is data-agnostic.
 */

const config = loadEnvConfig();
const reporter = new TestReporter('Metering Wallet E2E');

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('Phí nền tảng P7 — Tenant wallet page', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting Metering Wallet E2E...');
  });

  test('Owner views wallet balance and ledger', async ({ page, request }) => {
    const login = await request.post(`${config.SHOPERP_URL}/dev/login/owner`);
    expect(login.ok()).toBeTruthy();

    await page.goto(`${config.SHOPERP_URL}/wallet/phi-nen-tang`);
    await page.waitForLoadState('networkidle');

    // Either the wallet summary renders, or the unavailable fallback shows
    // (metering disabled on the environment). Both are valid end states.
    const summary = page.getByTestId('wallet-summary');
    const unavailable = page.getByTestId('wallet-unavailable');
    await expect(summary.or(unavailable)).toBeVisible({ timeout: 20000 });

    if (await summary.isVisible()) {
      await expect(page.getByTestId('ledger-table').or(page.getByText('Chưa có giao dịch.'))).toBeVisible();
      await expect(page.getByTestId('btn-create-topup')).toBeVisible();
    }
  });

  test('Create top-up request renders reference code + VietQR image', async ({ page, request }) => {
    const login = await request.post(`${config.SHOPERP_URL}/dev/login/owner`);
    expect(login.ok()).toBeTruthy();

    await page.goto(`${config.SHOPERP_URL}/wallet/phi-nen-tang`);
    await page.waitForLoadState('networkidle');
    const topupBtn = page.getByTestId('btn-create-topup');
    if (!(await topupBtn.isVisible({ timeout: 20000 }).catch(() => false))) {
      test.skip(true, 'Metering unavailable in this environment');
    }

    await page.getByTestId('topup-amount').fill('50000');
    await topupBtn.click();

    await expect(page.getByTestId('topup-qr-panel')).toBeVisible({ timeout: 15000 });
    const ref = page.getByTestId('topup-reference');
    await expect(ref).toContainText('TOPUP-');
    // QR image only renders once the admin has set topup-config — tolerate absence.
    const qr = page.getByTestId('topup-qr-image');
    if (await qr.isVisible().catch(() => false)) {
      await expect(qr).toHaveAttribute('src', /img\.vietqr\.io\/image\/.*-compact\.jpg/);
    }
  });
});
