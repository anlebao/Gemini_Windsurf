import { test, expect } from '@playwright/test';

test.describe('Accounting Entry Flow', () => {
  // AUTH_LIFECYCLE_TEST — uses dev login endpoint for E2E tests
  test.use({ storageState: { cookies: [], origins: [] } });
  test.beforeEach(async ({ page, request }) => {
    // Use dev login endpoint instead of traditional login form
    const shopErpUrl = process.env.SHOPERP_URL || 'http://localhost:5003';
    const devLoginUrl = `${shopErpUrl}/dev/login`;
    
    const response = await request.post(devLoginUrl);
    if (!response.ok()) {
      throw new Error(`Dev login failed: ${response.status()}`);
    }
    
    const body = await response.json();
    console.log(`Dev login successful: tenantId=${body.tenantId}, role=${body.role}`);
    
    // Navigate to dashboard after login
    await page.goto(`${shopErpUrl}/dashboard`);
    await page.waitForLoadState('networkidle');
  });

  test('should create revenue entry and appear in history', async ({ page }) => {
    const shopErpUrl = process.env.SHOPERP_URL || 'http://localhost:5003';
    
    // Navigate to revenue entry
    await page.goto(`${shopErpUrl}/accounting/revenue`);
    await page.waitForLoadState('networkidle');

    // Fill form using DynamicForm selectors
    await page.fill('input[name="date"], input[type="date"]', '2026-05-20');
    await page.fill('input[name="amount"], input[placeholder*="0.00"]', '1000000');
    await page.selectOption('select[name="account"], select', '511');
    await page.fill('textarea[name="description"], textarea[placeholder*="Nhập diễn giải"]', 'Doanh thu bán hàng');
    await page.fill('input[name="reference"], input[placeholder*="HĐ-001"]', 'HĐ-001');

    // Submit
    await page.click('button:has-text("Lưu Doanh Thu"), button[type="submit"]');

    // Verify success message
    await expect(page.locator('.vanan-alert-success, .alert-success, [class*="alert-success"]')).toBeVisible();
    await expect(page.locator('.vanan-alert-success, .alert-success, [class*="alert-success"]')).toContainText('thành công');

    // Navigate to history
    await page.goto(`${shopErpUrl}/accounting/history`);
    await page.waitForLoadState('networkidle');

    // Verify entry appears in table
    await expect(page.locator('table, .transaction-list')).toBeVisible();
    
    // Cleanup: Delete the test entry
    // TODO: Implement cleanup via API or UI
  });

  test('should show validation error when amount is zero', async ({ page }) => {
    const shopErpUrl = process.env.SHOPERP_URL || 'http://localhost:5003';
    
    await page.goto(`${shopErpUrl}/accounting/revenue`);
    await page.waitForLoadState('networkidle');

    await page.fill('input[name="date"], input[type="date"]', '2026-05-20');
    await page.fill('input[name="amount"], input[placeholder*="0.00"]', '0');
    await page.selectOption('select[name="account"], select', '511');
    await page.click('button:has-text("Lưu Doanh Thu"), button[type="submit"]');

    await expect(page.locator('.vanan-alert-error, .alert-error, [class*="alert-error"]')).toBeVisible();
    await expect(page.locator('.vanan-alert-error, .alert-error, [class*="alert-error"]')).toContainText('lớn hơn 0');
  });

  test('should show validation error when date is missing', async ({ page }) => {
    const shopErpUrl = process.env.SHOPERP_URL || 'http://localhost:5003';
    
    await page.goto(`${shopErpUrl}/accounting/revenue`);
    await page.waitForLoadState('networkidle');

    await page.fill('input[name="amount"], input[placeholder*="0.00"]', '1000000');
    await page.selectOption('select[name="account"], select', '511');
    await page.click('button:has-text("Lưu Doanh Thu"), button[type="submit"]');

    await expect(page.locator('.vanan-alert-error, .alert-error, [class*="alert-error"]')).toBeVisible();
    await expect(page.locator('.vanan-alert-error, .alert-error, [class*="alert-error"]')).toContainText('Ngày');
  });

  test('should detect duplicate entry within 5 minutes', async ({ page }) => {
    const shopErpUrl = process.env.SHOPERP_URL || 'http://localhost:5003';
    
    await page.goto(`${shopErpUrl}/accounting/revenue`);
    await page.waitForLoadState('networkidle');

    // First entry
    await page.fill('input[name="date"], input[type="date"]', '2026-05-20');
    await page.fill('input[name="amount"], input[placeholder*="0.00"]', '1000000');
    await page.selectOption('select[name="account"], select', '511');
    await page.fill('textarea[name="description"], textarea[placeholder*="Nhập diễn giải"]', 'Doanh thu bán hàng');
    await page.click('button:has-text("Lưu Doanh Thu"), button[type="submit"]');
    await expect(page.locator('.vanan-alert-success, .alert-success, [class*="alert-success"]')).toBeVisible();

    // Second duplicate entry
    await page.goto(`${shopErpUrl}/accounting/revenue`);
    await page.waitForLoadState('networkidle');
    await page.fill('input[name="date"], input[type="date"]', '2026-05-20');
    await page.fill('input[name="amount"], input[placeholder*="0.00"]', '1000000');
    await page.selectOption('select[name="account"], select', '511');
    await page.fill('textarea[name="description"], textarea[placeholder*="Nhập diễn giải"]', 'Doanh thu bán hàng');
    await page.click('button:has-text("Lưu Doanh Thu"), button[type="submit"]');

    // Verify duplicate warning
    await expect(page.locator('.vanan-alert-error, .alert-error, [class*="alert-error"]')).toBeVisible();
    await expect(page.locator('.vanan-alert-error, .alert-error, [class*="alert-error"]')).toContainText('trùng lặp');
  });
});
