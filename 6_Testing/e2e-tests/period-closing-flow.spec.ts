import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// T-21 FIX: Added mandatory expect() assertions after every conditional step.
// Removed if(isVisible)/reporter.pass()-only patterns in step 2 and reopen tests.
// Tests now fail clearly when wizard flow is broken.

const config = loadEnvConfig();
const reporter = new TestReporter('Period Closing E2E');

test.describe.configure({ mode: 'parallel' });

test.describe('VanAn ShopERP - Period Closing Wizard E2E Tests', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting Period Closing E2E Tests...');
  });

  test.beforeEach(async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/accounting/period-closing`);
    await page.waitForLoadState('networkidle');
  });

  // ─── WIZARD ACCESSIBLE ───────────────────────────────────────────────────

  test('Staff can access Period Closing wizard', async ({ page }) => {
    // All mandatory UI elements must be visible — hard assertions
    await expect(page.locator('h1:has-text("Đóng Sổ Kỳ Kế Toán")')).toBeVisible();

    await expect(page.locator('input[type="number"]')).toBeVisible();
    await expect(page.locator('select')).toBeVisible();
    await expect(page.locator('button:has-text("Bắt Đầu Kiểm Tra")')).toBeVisible();

  });

  // ─── STEP 1: VALIDATION ──────────────────────────────────────────────────

  test('PeriodClosingWizard validates period before closing', async ({ page }) => {
    await page.locator('input[type="number"]').fill('2025');
    await page.locator('select').selectOption('12');
    await page.locator('button:has-text("Bắt Đầu Kiểm Tra")').click();
    await page.waitForLoadState('networkidle');

    // Validation result card must appear — proves backend ran validation
    const validationCard = page.locator('.vana-card, [class*="card"]').filter({
      hasText: /Kết Quả Kiểm Tra/,
    });
    await expect(validationCard).toBeVisible({ timeout: 10000 });

    // Validation card must contain a specific outcome alert (success or error).
    // Scoped to validation card — NOT a page-wide tautology.
    // PeriodClosing.razor L80: VanAAlert Type="success" | L96: VanAAlert Type="error"
    // VanAAlert renders .alert-success or .alert-danger inside the "Kết Quả Kiểm Tra" card.
    await expect(
      validationCard.locator('.alert-success, .alert-danger')
    ).toBeVisible();

  });

  // ─── NAVIGATION MENU ITEM ────────────────────────────────────────────────

  test('PeriodClosingWizard shows navigation menu item', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/accounting`);
    await page.waitForLoadState('networkidle');

    await expect(
      page.locator('a[href="/accounting/period-closing"], nav :has-text("Đóng Sổ Kỳ")')
    ).toBeVisible({ timeout: 5000 });

  });

  // ─── STEP 2: REVIEW BEFORE CLOSE ─────────────────────────────────────────

  test('PeriodClosingWizard step 2 shows review before closing', async ({ page }) => {
    await page.locator('input[type="number"]').fill(String(new Date().getFullYear() - 1));
    await page.locator('select').selectOption('1');
    await page.locator('button:has-text("Bắt Đầu Kiểm Tra")').click();
    // Wait for validation to complete — fluent wait for "Tiếp Theo" button
    await expect(page.locator('button:has-text("Tiếp Theo")')).toBeVisible({ timeout: 10000 });
    await nextButton.click();
    await page.waitForLoadState('networkidle');

    // Review card must appear — proves wizard advanced to step 2
    const reviewCard = page.locator('.vana-card, [class*="card"]').filter({
      hasText: /Xem Lại|Reversal Entry|Bút toán Đảo Ngược/,
    });
    await expect(reviewCard).toBeVisible({ timeout: 5000 });

    // Confirm button must be present in step 2
    await expect(page.locator('button:has-text("Xác Nhận Đóng Sổ")')).toBeVisible();

  });

  // ─── REOPEN: REASON FIELD REQUIRED ──────────────────────────────────────

  test('PeriodClosingWizard reopen requires reason field', async ({ page }) => {
    // Reopen button must exist on the page for a previously-closed period
    const reopenButton = page.locator('button:has-text("Mở Lại Kỳ Này")');
    await expect(reopenButton).toBeVisible({ timeout: 5000 });
    await reopenButton.click();

    // Reason input must appear after clicking reopen
    const reasonInput = page.locator('input[placeholder*="lý do"]');
    await expect(reasonInput).toBeVisible({ timeout: 3000 });

    // Confirm button must be disabled while reason is empty
    const confirmReopenButton = page.locator('button:has-text("Xác Nhận Mở Lại")');
    await expect(confirmReopenButton).toBeDisabled();

    // After filling reason, button must become enabled
    await reasonInput.fill('Kiểm toán Q4 yêu cầu điều chỉnh');
    await expect(confirmReopenButton).not.toBeDisabled();

  });
});
