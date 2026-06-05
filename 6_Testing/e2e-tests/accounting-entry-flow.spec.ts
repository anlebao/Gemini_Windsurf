import { test, expect } from '@playwright/test';

test.describe('Accounting Entry Flow', () => {
  // AUTH_LIFECYCLE_TEST — uses accounting role credentials (accounting@vanan.com), not admin storageState
  test.use({ storageState: { cookies: [], origins: [] } });
  test.beforeEach(async ({ page }) => {
    // Login before each test
    await page.goto('/login');
    await page.fill('#email', process.env.TEST_EMAIL || 'accounting@vanan.com');
    await page.fill('#password', process.env.TEST_PASSWORD || 'test123');
    await page.click('button[type="submit"]');
    await page.waitForURL('/dashboard', { timeout: 10000 });
  });

  test('should create revenue entry and appear in history', async ({ page }) => {
    // Navigate to revenue entry
    await page.goto('/accounting/revenue');

    // Fill form
    await page.fill('#date', '2026-05-20');
    await page.fill('#amount', '1000000');
    await page.selectOption('#account', '511');
    await page.fill('#description', 'Doanh thu bán hàng');
    await page.fill('#reference', 'HĐ-001');

    // Submit
    await page.click('button[type="submit"]');

    // Verify success message
    await expect(page.locator('div.alert-success')).toBeVisible();
    await expect(page.locator('div.alert-success')).toContainText('Đã tạo thành công');

    // Navigate to history
    await page.goto('/accounting/history');

    // Verify entry appears in table
    await expect(page.locator('table')).toContainText('1.000.000 ₫');
    await expect(page.locator('table')).toContainText('511');
    await expect(page.locator('table')).toContainText('Doanh thu bán hàng');
    
    // Cleanup: Delete the test entry
    // TODO: Implement cleanup via API or UI
  });

  test('should show validation error when amount is zero', async ({ page }) => {
    await page.goto('/accounting/revenue');

    await page.fill('#date', '2026-05-20');
    await page.fill('#amount', '0');
    await page.selectOption('#account', '511');
    await page.click('button[type="submit"]');

    await expect(page.locator('div.alert-error')).toBeVisible();
    await expect(page.locator('div.alert-error')).toContainText('Số tiền phải > 0');
  });

  test('should show validation error when date is missing', async ({ page }) => {
    await page.goto('/accounting/revenue');

    await page.fill('#amount', '1000000');
    await page.selectOption('#account', '511');
    await page.click('button[type="submit"]');

    await expect(page.locator('div.alert-error')).toBeVisible();
    await expect(page.locator('div.alert-error')).toContainText('Ngày không được để trống');
  });

  test('should detect duplicate entry within 5 minutes', async ({ page }) => {
    await page.goto('/accounting/revenue');

    // First entry
    await page.fill('#date', '2026-05-20');
    await page.fill('#amount', '1000000');
    await page.selectOption('#account', '511');
    await page.fill('#description', 'Doanh thu bán hàng');
    await page.click('button[type="submit"]');
    await expect(page.locator('div.alert-success')).toBeVisible();

    // Second duplicate entry
    await page.goto('/accounting/revenue');
    await page.fill('#date', '2026-05-20');
    await page.fill('#amount', '1000000');
    await page.selectOption('#account', '511');
    await page.fill('#description', 'Doanh thu bán hàng');
    await page.click('button[type="submit"]');

    // Verify duplicate warning
    await expect(page.locator('div.alert-warning')).toBeVisible();
    await expect(page.locator('div.alert-warning')).toContainText('Giao dịch trùng lặp');
  });
});
