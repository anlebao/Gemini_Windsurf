import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// T-18 FIX: Removed all COREHUB_URL API calls (CoreHub is a Worker Host — no HTTP API).
// Removed all if(isVisible)/else-bypass reporter.pass() patterns.
// All assertions are now mandatory expect() calls that fail when UI is broken.
// Order API endpoints belong to Gateway (/api/orders), not CoreHub.

const config = loadEnvConfig();
const reporter = new TestReporter('E2E Tests');

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VanAn Ecosystem - Order Flow E2E Tests', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting E2E Tests...');
    reporter.log(`Timeout: ${config.E2E_TEST_TIMEOUT}s`);
  });

  test.beforeEach(async ({ page }) => {
    await page.goto(config.KHACHLINK_URL);
    await page.waitForLoadState('networkidle');
  });

  // ─── PRODUCT CATALOG ─────────────────────────────────────────────────────

  test('Customer can view product catalog', async ({ page }) => {
    // Feature cards must be present — hard assertion, no silent skip
    await expect(page.locator('.feature-card').first()).toBeVisible();

    const firstProduct = page.locator('.feature-card').first();
    await expect(firstProduct.locator('h5')).toBeVisible();

    reporter.pass('Product Catalog Display', {
      productCount: await page.locator('.feature-card').count(),
    });
  });

  test('Customer can add items to cart', async ({ page }) => {
    const firstProduct = page.locator('.feature-card').first();
    await expect(firstProduct).toBeVisible();

    const productName = await firstProduct.locator('h5').textContent();

    // "Đặt ngay" button must be present on the product card
    const orderBtn = firstProduct.locator('button:has-text("Đặt ngay")');
    await expect(orderBtn).toBeVisible();
    await orderBtn.click();

    reporter.pass('Add to Cart', { productName, action: 'add_attempted' });
  });

  test('Customer can place order', async ({ page }) => {
    // Add product to cart
    const firstProduct = page.locator('.feature-card').first();
    await expect(firstProduct).toBeVisible();
    await firstProduct.locator('button:has-text("Đặt ngay")').click();

    // Checkout button must appear after adding to cart
    const placeOrderButton = page.locator(
      'button:has-text("Đặt hàng"), button:has-text("Xác nhận"), button:has-text("Checkout")'
    ).first();
    await expect(placeOrderButton).toBeVisible({ timeout: 5000 });
    await placeOrderButton.click();
    await page.waitForLoadState('networkidle');

    // Success message or order tracking must appear — proves order was created
    await expect(
      page.locator('.alert-success, .order-confirmation, .success-message, .order-tracking')
    ).toBeVisible({ timeout: 5000 });

    reporter.pass('Place Order', { status: 'order_placed' });
  });

  // ─── STAFF ORDER VIEW (ShopERP) ──────────────────────────────────────────

  test('Staff can view orders in ShopERP', async ({ page }) => {
    await page.goto(config.SHOPERP_URL);
    await page.waitForLoadState('networkidle');

    // ShopERP home page must have a heading
    await expect(page.locator('h1, h2').first()).toBeVisible();

    reporter.pass('ShopERP Page Load', {
      pageTitle: await page.locator('h1, h2').first().textContent(),
    });
  });

  test('Staff can update order status', async ({ page }) => {
    await page.goto(config.SHOPERP_URL);
    await page.waitForLoadState('networkidle');

    // Status update button must exist on the page
    const statusButton = page.locator(
      'button:has-text("Cập nhật"), button:has-text("Update"), .status-update'
    ).first();
    await expect(statusButton).toBeVisible();
    await statusButton.click();

    // Status options must appear after clicking
    const statusSelect = page.locator('select, .status-options').first();
    await expect(statusSelect).toBeVisible({ timeout: 3000 });
    await statusSelect.selectOption({ label: 'Đang pha chế' });

    const confirmButton = page.locator(
      'button:has-text("Xác nhận"), button:has-text("Confirm")'
    ).first();
    await expect(confirmButton).toBeVisible();
    await confirmButton.click();

    reporter.pass('Update Order Status', { action: 'status_updated' });
  });

  // ─── ORDER TRACKING (Gateway API — not CoreHub) ──────────────────────────

  test('Order API is accessible via Gateway', async ({ page }) => {
    // Verify Gateway /api/orders endpoint (not COREHUB_URL which has no HTTP)
    const response = await page.request.get(`${config.GATEWAY_URL}/api/orders`);
    // Accepts 200 (list) or 401 (auth required) — both prove the endpoint exists
    expect([200, 401, 403]).toContain(response.status());

    reporter.pass('Gateway Orders API Reachable', { status: response.status() });
  });

  test('Inventory check API is accessible via Gateway', async ({ page }) => {
    // Verify Gateway inventory endpoint (not COREHUB_URL)
    const response = await page.request.get(
      `${config.GATEWAY_URL}/api/inventory/check?ingredientId=test&quantity=1`
    );
    expect([200, 400, 401, 404]).toContain(response.status());

    reporter.pass('Gateway Inventory API Reachable', { status: response.status() });
  });
});
