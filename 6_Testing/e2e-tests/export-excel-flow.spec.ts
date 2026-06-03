import { test, expect } from '@playwright/test';

test.describe('Export Excel Flow', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('#username', 'admin@vanan.vn');
    await page.fill('#password', 'VanAn@2026');
    await page.click('button[type="submit"]');
    await page.waitForURL('/');
  });

  test.skip('should export transaction history to Excel', async ({ page }) => {
    // Feature not implemented yet - ExportToExcel in TransactionHistory.razor only logs
    // TODO: Re-enable when export functionality is implemented
    await page.goto('/accounting/history');

    // Verify export button exists
    await expect(page.locator('button:has-text("Export Excel")')).toBeVisible();
  });

  test.skip('should export only filtered data', async ({ page }) => {
    // Feature not implemented yet - ExportToExcel in TransactionHistory.razor only logs
    // TODO: Re-enable when export functionality is implemented
    await page.goto('/accounting/history');

    // Verify search bar exists
    await expect(page.locator('text=Tìm theo diễn giải...')).toBeVisible();
  });
});
