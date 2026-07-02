import { test, expect } from '@playwright/test';

test.describe('EInvoice Provider Management', () => {
  // Auth: global storageState (auth/admin.json) applied via playwright.config.ts L34+L56.
  // ShopERP uses Cookie auth via /dev/login — no login form exists.

  test('should render Provider Management page', async ({ page }) => {
    await page.goto('/einvoice/providers');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="provider-management"]')).toBeVisible();
    await expect(page.locator('h1')).toContainText('Quản Lý Nhà Cung Cấp');
  });

  test('should display einvoice providers section', async ({ page }) => {
    await page.goto('/einvoice/providers');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="einvoice-providers-section"]')).toBeVisible();
  });

  test('should display Viettel provider in list', async ({ page }) => {
    await page.goto('/einvoice/providers');
    await page.waitForLoadState('networkidle');

    const providerNames = page.locator('[data-testid="provider-name"]');
    await expect(providerNames.first()).toBeVisible();
    // At least one provider row present
    await expect(providerNames).not.toHaveCount(0);
  });

  test('should show provider status badges', async ({ page }) => {
    await page.goto('/einvoice/providers');
    await page.waitForLoadState('networkidle');

    const statusBadges = page.locator('[data-testid="provider-status"]');
    await expect(statusBadges.first()).toBeVisible();
  });

  test('should navigate to configuration on configure button click', async ({ page }) => {
    await page.goto('/einvoice/providers');
    await page.waitForLoadState('networkidle');

    await page.click('[data-testid="btn-configure"]');
    await expect(page).toHaveURL(/\/einvoice\/configuration/);
  });

  test('should refresh provider list', async ({ page }) => {
    await page.goto('/einvoice/providers');
    await page.waitForLoadState('networkidle');

    await page.click('[data-testid="btn-refresh"]');
    await expect(page.locator('[data-testid="provider-management"]')).toBeVisible();
  });

  test('should render Provider Configuration page', async ({ page }) => {
    await page.goto('/einvoice/configuration');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="provider-configuration"]')).toBeVisible();
    await expect(page.locator('h1')).toContainText('Cấu Hình Nhà Cung Cấp');
  });

  test('should show config form when provider is selected', async ({ page }) => {
    await page.goto('/einvoice/configuration?provider=viettel');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="basic-config-card"]')).toBeVisible();
    await expect(page.locator('[data-testid="input-display-name"]')).toBeVisible();
  });

  test('should save configuration', async ({ page }) => {
    await page.goto('/einvoice/configuration?provider=viettel');
    await page.waitForLoadState('networkidle');

    await page.click('[data-testid="btn-save"]');
    await expect(page.locator('[data-testid="alert-success"]')).toBeVisible();
    await expect(page.locator('[data-testid="alert-success"]')).toContainText('Cấu hình đã được lưu thành công');
  });
});
