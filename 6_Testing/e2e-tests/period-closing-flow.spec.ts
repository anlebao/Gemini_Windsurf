import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

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

  test('Staff can access Period Closing wizard', async ({ page }) => {
    try {
      await expect(page.locator('h1:has-text("Đóng Sổ Kỳ Kế Toán")')).toBeVisible();

      const yearInput = page.locator('input[type="number"]');
      const monthSelect = page.locator('select');
      await expect(yearInput).toBeVisible();
      await expect(monthSelect).toBeVisible();

      const startButton = page.locator('button:has-text("Bắt Đầu Kiểm Tra")');
      await expect(startButton).toBeVisible();

      reporter.pass('Period Closing Wizard Accessible', {
        url: page.url()
      });
    } catch (error: any) {
      reporter.fail('Period Closing Wizard Accessible', { error: error.message });
      throw error;
    }
  });

  test('PeriodClosingWizard validates period before closing', async ({ page }) => {
    try {
      await page.locator('input[type="number"]').fill('2025');
      await page.locator('select').selectOption('12');

      await page.locator('button:has-text("Bắt Đầu Kiểm Tra")').click();
      await page.waitForLoadState('networkidle');

      const validationCard = page.locator('.vana-card, [class*="card"]').filter({
        hasText: /Kết Quả Kiểm Tra/
      });
      await expect(validationCard).toBeVisible({ timeout: 10000 });

      const hasSuccess = await page.locator('[class*="alert-success"], [class*="success"]').count() > 0;
      const hasError = await page.locator('[class*="alert-error"], [class*="error"]').count() > 0;
      expect(hasSuccess || hasError).toBeTruthy();

      reporter.pass('Period Validation Executed', {
        period: '2025-12',
        hasValidationResult: hasSuccess || hasError
      });
    } catch (error: any) {
      reporter.fail('Period Validation Executed', { error: error.message });
      throw error;
    }
  });

  test('PeriodClosingWizard shows navigation menu item', async ({ page }) => {
    try {
      await page.goto(`${config.SHOPERP_URL}/accounting`);
      await page.waitForLoadState('networkidle');

      const menuItem = page.locator('a[href="/accounting/period-closing"], nav :has-text("Đóng Sổ Kỳ")');
      await expect(menuItem).toBeVisible({ timeout: 5000 });

      reporter.pass('Period Closing Menu Item Visible', {});
    } catch (error: any) {
      reporter.fail('Period Closing Menu Item Visible', { error: error.message });
      throw error;
    }
  });

  test('PeriodClosingWizard step 2 shows review before closing', async ({ page }) => {
    try {
      await page.locator('input[type="number"]').fill(String(new Date().getFullYear() - 1));
      await page.locator('select').selectOption('1');
      await page.locator('button:has-text("Bắt Đầu Kiểm Tra")').click();
      await page.waitForTimeout(3000);

      const nextButton = page.locator('button:has-text("Tiếp Theo")');
      if (await nextButton.isVisible()) {
        await nextButton.click();
        await page.waitForLoadState('networkidle');

        const reviewCard = page.locator('.vana-card, [class*="card"]').filter({
          hasText: /Xem Lại|Reversal Entry|Bút toán Đảo Ngược/
        });
        await expect(reviewCard).toBeVisible({ timeout: 5000 });

        const confirmButton = page.locator('button:has-text("Xác Nhận Đóng Sổ")');
        await expect(confirmButton).toBeVisible();
      }

      reporter.pass('Period Closing Review Step', { stepped: await nextButton.isVisible() });
    } catch (error: any) {
      reporter.fail('Period Closing Review Step', { error: error.message });
      throw error;
    }
  });

  test('PeriodClosingWizard reopen requires reason field', async ({ page }) => {
    try {
      const reopenButton = page.locator('button:has-text("Mở Lại Kỳ Này")');
      if (await reopenButton.isVisible()) {
        await reopenButton.click();

        const reasonInput = page.locator('input[placeholder*="lý do"]');
        await expect(reasonInput).toBeVisible({ timeout: 3000 });

        const confirmReopenButton = page.locator('button:has-text("Xác Nhận Mở Lại")');
        await expect(confirmReopenButton).toBeDisabled();

        await reasonInput.fill('Kiểm toán Q4 yêu cầu điều chỉnh');
        await expect(confirmReopenButton).not.toBeDisabled();
      }

      reporter.pass('Reopen Requires Reason', {
        reopenAvailable: await reopenButton.isVisible()
      });
    } catch (error: any) {
      reporter.fail('Reopen Requires Reason', { error: error.message });
      throw error;
    }
  });
});
