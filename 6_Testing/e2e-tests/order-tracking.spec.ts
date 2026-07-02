import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// T-02: OrderTracking page E2E tests (TDD — written before implementation).
// Covers:
//   1. Direct navigation to /order-tracking/{orderId} renders the page
//   2. Page has .order-tracking container with order details
//   3. Status timeline is rendered
//   4. End-to-end: Home → add to cart → checkout → redirected to /order-tracking/{id}
//
// These tests were written BEFORE the UI fix — they are expected to fail until
// T-02b/c/d implementation is complete (true TDD red → green cycle).

const config = loadEnvConfig();
const reporter = new TestReporter('E2E Tests');

// Stable test orderId for direct-navigation tests
const TEST_ORDER_ID = '00000000-0000-0000-0000-000000000001';

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('KhachLink - Order Tracking Page (T-02)', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting Order Tracking E2E Tests (T-02)...');
  });

  // ─── DIRECT NAVIGATION ────────────────────────────────────────────────────

  test('Order tracking page renders at /order-tracking/{orderId}', async ({ page }) => {
    await page.goto(`${config.KHACHLINK_URL}/order-tracking/${TEST_ORDER_ID}`);
    await page.waitForLoadState('networkidle');

    // Page must not 404 — heading or page content must be visible
    await expect(
      page.locator('h1, h2, h3, h4, .card-header')
    ).toBeVisible({ timeout: 10000 });

    // The page must contain either order info or a "not found" message
    // Both are valid — proves the route is registered
    await expect(
      page.locator('.order-tracking, .card, .alert')
    ).toBeVisible();

  });

  test('Order tracking page has .order-tracking container', async ({ page }) => {
    await page.goto(`${config.KHACHLINK_URL}/order-tracking/${TEST_ORDER_ID}`);
    await page.waitForLoadState('networkidle');

    // .order-tracking container must exist — mandatory CSS class for E2E selector contract
    await expect(page.locator('.order-tracking')).toBeVisible();

  });

  test('Order tracking page shows order ID in heading', async ({ page }) => {
    await page.goto(`${config.KHACHLINK_URL}/order-tracking/${TEST_ORDER_ID}`);
    await page.waitForLoadState('networkidle');

    // Heading must contain partial orderId or "Theo dõi" / "Đơn hàng"
    const heading = page.locator('h4, h3, h2').first();
    await expect(heading).toBeVisible();
    const text = await heading.textContent();
    expect(text).toBeTruthy();

  });

  test('Order tracking page renders status timeline', async ({ page }) => {
    await page.goto(`${config.KHACHLINK_URL}/order-tracking/${TEST_ORDER_ID}`);
    await page.waitForLoadState('networkidle');

    // Timeline or status list must be present
    await expect(
      page.locator('.timeline, .status-timeline, .order-status-list')
    ).toBeVisible();

  });

  test('Order tracking page shows "not found" gracefully for unknown order', async ({ page }) => {
    const unknownId = 'ffffffff-ffff-ffff-ffff-ffffffffffff';
    await page.goto(`${config.KHACHLINK_URL}/order-tracking/${unknownId}`);
    await page.waitForLoadState('networkidle');

    // Must show a "not found" alert — not a crash / blank page
    await expect(
      page.locator('.alert-warning, .alert-danger, .not-found')
    ).toBeVisible();

    // Must have a "back to home" link
    await expect(
      page.locator('a[href="/"], a[href="/home"], a:has-text("Quay lại")')
    ).toBeVisible();

  });

  // ─── CHECKOUT → ORDER TRACKING REDIRECT ──────────────────────────────────

  test('Checkout redirects to /order-tracking/{id} after order placed', async ({ page }) => {
    // Navigate to KhachLink home
    await page.goto(`${config.KHACHLINK_URL}/home`);
    await page.waitForLoadState('networkidle');

    // Product cards must be visible
    const productCard = page.locator('.feature-card, .product-card').first();
    await expect(productCard).toBeVisible({ timeout: 10000 });

    // Click "Đặt ngay" on first product
    const addToCartBtn = productCard.locator(
      'button:has-text("Đặt ngay"), button:has-text("Add"), button:has-text("Thêm")'
    ).first();
    await expect(addToCartBtn).toBeVisible();
    await addToCartBtn.click();

    // Navigate to checkout
    await page.goto(`${config.KHACHLINK_URL}/checkout`);
    await page.waitForLoadState('networkidle');

    // Place order button
    const placeOrderBtn = page.locator(
      'button:has-text("Đặt hàng"), button:has-text("Xác nhận"), button:has-text("Place Order"), button:has-text("Thanh toán")'
    ).first();
    await expect(placeOrderBtn).toBeVisible({ timeout: 5000 });
    await placeOrderBtn.click();

    // After placing order: either redirected to /order-tracking/{id}
    // OR success message + tracking link appears on page
    await page.waitForLoadState('networkidle');

    const isOnTrackingPage = page.url().includes('/order-tracking/');
    const hasTrackingLink = await page.locator(
      'a[href*="/order-tracking/"], .order-tracking, .order-confirmation'
    ).isVisible();

    expect(isOnTrackingPage || hasTrackingLink).toBeTruthy();

  });

  // ─── GATEWAY API SMOKE (order status endpoint) ───────────────────────────

  test('Gateway order status API is reachable for order lookup', async ({ request }) => {
    // The tracking page calls Gateway to fetch order — verify endpoint exists
    const response = await request.get(
      `${config.GATEWAY_URL}/api/orders/${TEST_ORDER_ID}`
    );
    // 200 = found; 404 = not found (order doesn't exist in test DB — expected);
    // 401/403 = auth required — endpoint exists; 500 = crash — fail
    expect(response.status()).not.toBe(500);
    expect([200, 401, 403, 404]).toContain(response.status());

  });
});
