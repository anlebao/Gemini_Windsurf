import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// Wave 8: HKD Book UI page E2E tests — /accounting/hkd-books list + detail + export buttons.
// Validates TT 152/2025/TT-BTC layout render (header + table + footer) + DOCX/XLSX export buttons.

const config = loadEnvConfig();
const reporter = new TestReporter('HKD Books E2E');

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VanAn Ecosystem - HKD Books UI E2E Tests', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting HKD Books E2E Tests...');
  });

  test.beforeEach(async ({ page }) => {
    await page.goto(config.SHOPERP_URL);
    await page.waitForLoadState('networkidle');
  });

  // ─── HKD BOOKS LIST PAGE ─────────────────────────────────────────────────

  test('Owner can access HKD Books list page', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/accounting/hkd-books`);
    await page.waitForLoadState('networkidle');

    // Page heading must be visible
    await expect(
      page.locator('h1:has-text("Sổ Kế Toán HKD"), h1:has-text("HKD Books")')
    ).toBeVisible();

    // The page must render either the template list (VanAnDataGrid/table) or an empty/alert state.
    // A data grid or card must be present — proves the page rendered its content section.
    await expect(
      page.locator('table, .vanan-card, .vanan-data-grid, [class*="card"]')
    ).toBeVisible();
  });

  test('HKD Books list shows template rows or empty state', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/accounting/hkd-books`);
    await page.waitForLoadState('networkidle');

    await expect(
      page.locator('h1:has-text("Sổ Kế Toán HKD")')
    ).toBeVisible();

    // Either template rows are present (table with rows) OR an empty/alert message is shown.
    // Both states are valid — the test verifies the page did not crash.
    const tableRows = page.locator('table tbody tr');
    const alertOrEmpty = page.locator('text=Không có sổ, .vanan-alert, [class*="alert"]');

    const rowCount = await tableRows.count();
    if (rowCount === 0) {
      await expect(alertOrEmpty.first()).toBeVisible();
    }
  });

  // ─── HKD BOOK DETAIL PAGE ────────────────────────────────────────────────

  test('Owner can navigate to HKD Book detail page', async ({ page }) => {
    // S1a_HKD is the Group1 template — always available for Group1 tenants.
    await page.goto(`${config.SHOPERP_URL}/accounting/hkd-books/S1a_HKD`);
    await page.waitForLoadState('networkidle');

    // Page heading must include the template code
    await expect(
      page.locator('h1:has-text("S1a_HKD"), h1:has-text("HKD")')
    ).toBeVisible();
  });

  test('HKD Book detail page renders TT 152 layout elements', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/accounting/hkd-books/S1a_HKD`);
    await page.waitForLoadState('networkidle');

    await expect(
      page.locator('h1:has-text("S1a_HKD")')
    ).toBeVisible();

    // TT 152 header: "Mẫu số" reference to TT 152
    await expect(
      page.locator('text=152/2025/TT-BTC')
    ).toBeVisible();

    // TT 152 footer: NGƯỜI ĐẠI DIỆN HỘ KINH DOANH signature block
    await expect(
      page.locator('text=NGƯỜI ĐẠI DIỆN HỘ KINH DOANH')
    ).toBeVisible();

    // Table (VanAnDataGrid renders a <table>) must be present
    await expect(page.locator('table').first()).toBeVisible();
  });

  test('HKD Book detail page has Export DOCX and XLSX buttons', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/accounting/hkd-books/S1a_HKD`);
    await page.waitForLoadState('networkidle');

    await expect(
      page.locator('h1:has-text("S1a_HKD")')
    ).toBeVisible();

    // Export DOCX button must be visible
    await expect(
      page.locator('button:has-text("DOCX"), button:has-text("Word")')
    ).toBeVisible();

    // Export XLSX button must be visible
    await expect(
      page.locator('button:has-text("XLSX"), button:has-text("Excel")')
    ).toBeVisible();
  });

  // ─── ACCOUNTING INDEX NAVIGATION ─────────────────────────────────────────

  test('Accounting index has link to HKD Books', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/accounting`);
    await page.waitForLoadState('networkidle');

    await expect(
      page.locator('h1:has-text("Kế Toán"), h1:has-text("Accounting")')
    ).toBeVisible();

    // The dashboard must have a button/link to HKD books
    await expect(
      page.locator(
        'a[href*="/accounting/hkd-books"], button:has-text("Sổ HKD"), button:has-text("HKD")'
      )
    ).toBeVisible();
  });
});
