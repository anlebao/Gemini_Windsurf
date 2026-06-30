import { test, expect } from '@playwright/test';
import { loadEnvConfig } from '../utils/env-config';

const config = loadEnvConfig();

test.describe('EInvoice Dashboard', () => {
  test.use({ storageState: { cookies: [], origins: [] } });

  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('#email', config.TEST_EMAIL);
    await page.fill('#password', config.TEST_PASSWORD);
    await page.click('button[type="submit"]');
    await page.waitForURL('/dashboard', { timeout: 10000 });
  });

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
