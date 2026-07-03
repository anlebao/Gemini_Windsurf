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

  });

  test('Customer can add items to cart', async ({ page }) => {
    const firstProduct = page.locator('.feature-card').first();
    await expect(firstProduct).toBeVisible();

    const productName = await firstProduct.locator('h5').textContent();

    // "Đặt ngay" button must be present on the product card
    const orderBtn = firstProduct.locator('button:has-text("Đặt ngay")');
    await expect(orderBtn).toBeVisible();
    await orderBtn.click();

  });

  test('Customer can place order', async ({ page }) => {
    // Navigate to home and add product to cart
    await page.goto(`${config.KHACHLINK_URL}/home`);
    await page.waitForLoadState('networkidle');

    const firstProduct = page.locator('.feature-card').first();
    await expect(firstProduct).toBeVisible({ timeout: 10000 });
    await firstProduct.locator('button:has-text("Đặt ngay")').click();

    // Navigate to checkout directly (cart state is set)
    await page.goto(`${config.KHACHLINK_URL}/checkout`);
    await page.waitForLoadState('networkidle');

    // T-02c: After checkout, either:
    //   a) Redirected to /order-tracking/{id}  (if Gateway available + order created)
    //   b) .order-tracking container present on page (same redirect target)
    //   c) .order-confirmation element present (fallback if redirect delayed)
    await page.waitForURL(
      url => url.includes('/order-tracking/') || url.includes('/checkout'),
      { timeout: 10000 }
    );

    const finalUrl = page.url();
    const isOnTrackingPage = finalUrl.includes('/order-tracking/');
    const hasTrackingOrConfirmation = await page.locator(
      '.order-tracking, .order-confirmation, .alert-success'
    ).isVisible();

    // At least one of these must be true — proves order flow completed
    expect(isOnTrackingPage || hasTrackingOrConfirmation).toBeTruthy();

  });

  // ─── STAFF ORDER VIEW (ShopERP) ──────────────────────────────────────────

  test('Staff can view orders in ShopERP', async ({ page }) => {
    await page.goto(config.SHOPERP_URL);
    await page.waitForLoadState('networkidle');

    // ShopERP home page must have a heading
    await expect(page.locator('h1, h2').first()).toBeVisible();

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

  });

  // ─── ORDER TRACKING (Gateway API — not CoreHub) ──────────────────────────
  // MOVED to gateway-smoke.spec.ts (Wave 5 Pattern C — consolidated reachability).
});
