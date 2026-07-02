import { test, expect } from '@playwright/test';

test.describe('EInvoice Dashboard', () => {
  // Auth: global storageState (auth/admin.json) applied via playwright.config.ts L34+L56.
  // ShopERP uses Cookie auth via /dev/login — no login form exists.

  test('should render EInvoice Dashboard page', async ({ page }) => {
    await page.goto('/einvoice');
    await page.waitForLoadState('networkidle');

    // Page renders
    await expect(page.locator('[data-testid="einvoice-dashboard"]')).toBeVisible();
    await expect(page.locator('h1')).toContainText('Dashboard Hóa Đơn Điện Tử');
  });

  test('should display metrics section', async ({ page }) => {
    await page.goto('/einvoice');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="metrics-section"]')).toBeVisible();
  });

  test('should display provider status card', async ({ page }) => {
    await page.goto('/einvoice');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="provider-status-card"]')).toBeVisible();
  });

  test('should display recent activity card', async ({ page }) => {
    await page.goto('/einvoice');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="activity-card"]')).toBeVisible();
  });

  test('should navigate to invoice list on create button click', async ({ page }) => {
    await page.goto('/einvoice');
    await page.waitForLoadState('networkidle');

    await page.click('[data-testid="btn-new-invoice"]');
    await expect(page).toHaveURL(/\/einvoice\/invoices/);
  });

  test('should refresh dashboard data', async ({ page }) => {
    await page.goto('/einvoice');
    await page.waitForLoadState('networkidle');

    await page.click('[data-testid="btn-refresh"]');
    // Dashboard remains on same page after refresh
    await expect(page.locator('[data-testid="einvoice-dashboard"]')).toBeVisible();
  });
});
