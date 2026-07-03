import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// T-17 FIX: Replaced all reporter.pass()-only assertions and if(isVisible)/else-bypass
// patterns with mandatory expect() calls. Tests now fail if UI is broken.
// T-07 ADD: Gateway API smoke tests using /api/accounting alias route.

const config = loadEnvConfig();
const reporter = new TestReporter('E2E Tests');

// Skip entire suite if E2E tests are disabled
test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VanAn Ecosystem - Accounting Flow E2E Tests', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting Accounting E2E Tests...');
    reporter.log(`Timeout: ${config.E2E_TEST_TIMEOUT}s`);
  });

  test.beforeEach(async ({ page }) => {
    await page.goto(config.SHOPERP_URL);
    await page.waitForLoadState('networkidle');
  });

  // ─── ACCOUNTING DASHBOARD ────────────────────────────────────────────────

  test('Staff can access Accounting Dashboard', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/accounting`);
    await page.waitForLoadState('networkidle');

    // Page heading must be visible — hard assertion
    await expect(
      page.locator('h1:has-text("Kế Toán"), h1:has-text("Accounting")')
    ).toBeVisible();

    // At least one metrics card must be rendered
    const metricsCards = page.locator('.metrics-card, .vanan-metrics-card');
    await expect(metricsCards.first()).toBeVisible();

  });

  // ─── REVENUE ENTRY ───────────────────────────────────────────────────────

  test('Staff can navigate to Revenue Entry page', async ({ page }) => {
    // Direct navigation — eliminates the if(button visible) split path
    await page.goto(`${config.SHOPERP_URL}/accounting/revenue`);
    await page.waitForLoadState('networkidle');

    await expect(
      page.locator('h1:has-text("Nhập Doanh Thu"), h1:has-text("Revenue Entry")')
    ).toBeVisible();

    // Form must be present
    await expect(page.locator('form, .dynamic-form')).toBeVisible();

  });

  test('Staff can submit Revenue Entry', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/accounting/revenue`);
    await page.waitForLoadState('networkidle');

    // Page heading is mandatory — proves page rendered correctly
    await expect(
      page.locator('h1:has-text("Nhập Doanh Thu"), h1:has-text("Revenue Entry")')
    ).toBeVisible();

    // Fill form — mandatory fields must exist
    const dateInput = page.locator('input[type="date"], input[name*="date"]').first();
    await expect(dateInput).toBeVisible();
    await dateInput.fill(new Date().toISOString().split('T')[0]);

    const amountInput = page.locator('input[name*="amount"], input[placeholder*="Số Tiền"]').first();
    await expect(amountInput).toBeVisible();
    await amountInput.fill('100000');

    const descriptionInput = page.locator(
      'textarea[name*="description"], input[name*="description"]'
    ).first();
    await expect(descriptionInput).toBeVisible();
    await descriptionInput.fill('Test revenue entry E2E');

    // Submit button must exist and be clickable
    const submitButton = page.locator(
      'button:has-text("Lưu"), button:has-text("Save"), button[type="submit"]'
    ).first();
    await expect(submitButton).toBeVisible();
    await submitButton.click();

    // Success alert must appear — proves backend processed the request
    await expect(
      page.locator('.alert-success, .vanan-alert-success, [class*="alert-success"]')
    ).toBeVisible({ timeout: 5000 });

  });

  // ─── EXPENSE ENTRY ───────────────────────────────────────────────────────

  test('Staff can navigate to Expense Entry page', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/accounting/expenses`);
    await page.waitForLoadState('networkidle');

    await expect(
      page.locator('h1:has-text("Nhập Chi Phí"), h1:has-text("Expense Entry")')
    ).toBeVisible();

    await expect(page.locator('form, .dynamic-form')).toBeVisible();

  });

  test('Staff can submit Expense Entry', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/accounting/expenses`);
    await page.waitForLoadState('networkidle');

    await expect(
      page.locator('h1:has-text("Nhập Chi Phí"), h1:has-text("Expense Entry")')
    ).toBeVisible();

    const dateInput = page.locator('input[type="date"], input[name*="date"]').first();
    await expect(dateInput).toBeVisible();
    await dateInput.fill(new Date().toISOString().split('T')[0]);

    const amountInput = page.locator('input[name*="amount"], input[placeholder*="Số Tiền"]').first();
    await expect(amountInput).toBeVisible();
    await amountInput.fill('50000');

    const descriptionInput = page.locator(
      'textarea[name*="description"], input[name*="description"]'
    ).first();
    await expect(descriptionInput).toBeVisible();
    await descriptionInput.fill('Test expense entry E2E');

    const submitButton = page.locator(
      'button:has-text("Lưu"), button:has-text("Save"), button[type="submit"]'
    ).first();
    await expect(submitButton).toBeVisible();
    await submitButton.click();

    await expect(
      page.locator('.alert-success, .vanan-alert-success, [class*="alert-success"]')
    ).toBeVisible({ timeout: 5000 });

  });

  // ─── TRANSACTION HISTORY ─────────────────────────────────────────────────

  test('Staff can view Transaction History', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/accounting/history`);
    await page.waitForLoadState('networkidle');

    await expect(
      page.locator('h1:has-text("Lịch Sử"), h1:has-text("Transaction History")')
    ).toBeVisible();

    // Table must be rendered — proves data layer responded
    await expect(page.locator('table, .transaction-list')).toBeVisible();

  });

  test('Staff can filter Transaction History by month', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/accounting/history`);
    await page.waitForLoadState('networkidle');

    // Month/year filter controls must exist
    const monthFilter = page.locator('select').first();
    await expect(monthFilter).toBeVisible();
    await monthFilter.selectOption({ index: 1 });

    // Page must still show table after filtering
    await expect(page.locator('table, .transaction-list')).toBeVisible();

  });

  // ─── ACCOUNT BALANCE ─────────────────────────────────────────────────────

  test('Staff can view Account Balance', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/accounting/balance`);
    await page.waitForLoadState('networkidle');

    await expect(
      page.locator('h1:has-text("Số Dư"), h1:has-text("Account Balance")')
    ).toBeVisible();

    // Balance display must be present
    await expect(
      page.locator('.balance-amount, .metrics-card, .vanan-metrics-card')
    ).toBeVisible();

  });

  // ─── ACCOUNTING INDEX NAVIGATION ─────────────────────────────────────────

  test('Accounting index shows navigation links', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/accounting`);
    await page.waitForLoadState('networkidle');

    await expect(
      page.locator('h1:has-text("Kế Toán"), h1:has-text("Accounting")')
    ).toBeVisible();

    // At least one nav link to accounting sub-pages must exist
    await expect(
      page.locator(
        'a[href*="/accounting/revenue"], a[href*="/accounting/expenses"], ' +
        'button:has-text("Nhập Doanh Thu"), button:has-text("Nhập Chi Phí")'
      ).first()
    ).toBeVisible();

  });
});

// ─── T-07: GATEWAY ACCOUNTING API SMOKE TESTS ────────────────────────────────
// MOVED to gateway-smoke.spec.ts (Wave 5 Pattern C — consolidated reachability).

