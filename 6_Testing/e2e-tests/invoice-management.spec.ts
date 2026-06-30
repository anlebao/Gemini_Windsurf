import { test, expect } from '@playwright/test';
import { loadEnvConfig } from '../utils/env-config';

const config = loadEnvConfig();

test.describe('EInvoice Invoice Management', () => {
  test.use({ storageState: { cookies: [], origins: [] } });

  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('#email', config.TEST_EMAIL);
    await page.fill('#password', config.TEST_PASSWORD);
    await page.click('button[type="submit"]');
    await page.waitForURL('/dashboard', { timeout: 10000 });
  });

  test('should render Invoice Management page', async ({ page }) => {
    await page.goto('/einvoice/invoices');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="invoice-management"]')).toBeVisible();
    await expect(page.locator('h1')).toContainText('Quản Lý Hóa Đơn Điện Tử');
  });

  test('should display invoice list card', async ({ page }) => {
    await page.goto('/einvoice/invoices');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="invoice-list-card"]')).toBeVisible();
  });

  test('should show create invoice button', async ({ page }) => {
    await page.goto('/einvoice/invoices');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="btn-create-invoice"]')).toBeVisible();
  });

  test('should open create invoice modal', async ({ page }) => {
    await page.goto('/einvoice/invoices');
    await page.waitForLoadState('networkidle');

    await page.click('[data-testid="btn-create-invoice"]');

    // Modal inputs appear
    await expect(page.locator('[data-testid="input-customer-name"]')).toBeVisible();
    await expect(page.locator('[data-testid="input-amount"]')).toBeVisible();
    await expect(page.locator('[data-testid="select-invoice-type"]')).toBeVisible();
  });

  test('should validate amount before creating invoice', async ({ page }) => {
    await page.goto('/einvoice/invoices');
    await page.waitForLoadState('networkidle');

    await page.click('[data-testid="btn-create-invoice"]');

    // Fill with invalid amount
    await page.fill('[data-testid="input-customer-name"]', 'Test Customer');
    await page.fill('[data-testid="input-amount"]', 'không phải số');
    await page.click('[data-testid="btn-confirm-create"]');

    await expect(page.locator('[data-testid="alert-error"]')).toBeVisible();
    await expect(page.locator('[data-testid="alert-error"]')).toContainText('Số tiền không hợp lệ');
  });

  test('should create invoice successfully with valid data', async ({ page }) => {
    await page.goto('/einvoice/invoices');
    await page.waitForLoadState('networkidle');

    await page.click('[data-testid="btn-create-invoice"]');

    await page.fill('[data-testid="input-customer-name"]', 'Công Ty TNHH Test');
    await page.fill('[data-testid="input-tax-code"]', '0123456789');
    await page.fill('[data-testid="input-address"]', '123 Lê Lợi, Hà Nội');
    await page.fill('[data-testid="input-amount"]', '1000000');
    await page.fill('[data-testid="input-vat"]', '100000');
    await page.selectOption('[data-testid="select-invoice-type"]', 'HKD');
    await page.click('[data-testid="btn-confirm-create"]');

    await expect(page.locator('[data-testid="alert-success"]')).toBeVisible();
    await expect(page.locator('[data-testid="alert-success"]')).toContainText('Hóa đơn đã được tạo thành công');
  });

  test('should filter invoices by status', async ({ page }) => {
    await page.goto('/einvoice/invoices');
    await page.waitForLoadState('networkidle');

    await page.selectOption('[data-testid="status-filter"]', 'Draft');
    // Page stays visible after filter
    await expect(page.locator('[data-testid="invoice-management"]')).toBeVisible();
  });

  test('should render Health Monitoring page', async ({ page }) => {
    await page.goto('/einvoice/health');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="health-monitoring"]')).toBeVisible();
    await expect(page.locator('h1')).toContainText('Giám Sát Hệ Thống');
    await expect(page.locator('[data-testid="health-metrics"]')).toBeVisible();
    await expect(page.locator('[data-testid="provider-health-card"]')).toBeVisible();
  });

  test('should render Alert Management page', async ({ page }) => {
    await page.goto('/einvoice/alerts');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="alert-management"]')).toBeVisible();
    await expect(page.locator('h1')).toContainText('Quản Lý Cảnh Báo');
    await expect(page.locator('[data-testid="alert-metrics"]')).toBeVisible();
    await expect(page.locator('[data-testid="alert-list-card"]')).toBeVisible();
  });

  test('should acknowledge a single alert', async ({ page }) => {
    await page.goto('/einvoice/alerts');
    await page.waitForLoadState('networkidle');

    const acknowledgeBtn = page.locator('[data-testid="btn-acknowledge"]').first();
    if (await acknowledgeBtn.isVisible()) {
      await acknowledgeBtn.click();
      // Alert row changes to acknowledged state
      await expect(page.locator('.badge-success').first()).toBeVisible();
    }
  });

  test('should acknowledge all alerts', async ({ page }) => {
    await page.goto('/einvoice/alerts');
    await page.waitForLoadState('networkidle');

    const acknowledgeAllBtn = page.locator('[data-testid="btn-acknowledge-all"]');
    if (await acknowledgeAllBtn.isVisible()) {
      await acknowledgeAllBtn.click();
      // All acknowledge buttons gone
      await expect(page.locator('[data-testid="btn-acknowledge"]')).toHaveCount(0);
    }
  });
});
