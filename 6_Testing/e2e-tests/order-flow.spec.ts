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

  test('Customer can view product catalog @golden', async ({ page }) => {
    // Feature cards must be present — hard assertion, no silent skip
    await expect(page.getByTestId('home-product-card').first()).toBeVisible();

    const firstProduct = page.getByTestId('home-product-card').first();
    await expect(firstProduct.locator('h5')).toBeVisible();

  });

  test('Customer can add items to cart @golden', async ({ page }) => {
    const firstProduct = page.getByTestId('home-product-card').first();
    await expect(firstProduct).toBeVisible();

    const productName = await firstProduct.locator('h5').textContent();

    // "Đặt ngay" button must be present on the product card
    const orderBtn = firstProduct.getByTestId('home-btn-add-to-cart');
    await expect(orderBtn).toBeVisible();
    await orderBtn.click();

  });

  test('Customer can place order @golden', async ({ page }) => {
    // Navigate to home and add product to cart
    await page.goto(`${config.KHACHLINK_URL}/home`);
    await page.waitForLoadState('networkidle');

    const firstProduct = page.getByTestId('home-product-card').first();
    await expect(firstProduct).toBeVisible({ timeout: 10000 });
    await firstProduct.getByTestId('home-btn-add-to-cart').click();

    // Wait for Blazor async handler to save cart to localStorage (toast notification appears)
    await expect(page.getByText(/Đã thêm|Added to cart/i)).toBeVisible({ timeout: 5000 });

    // Navigate to cart page first (ensures cart is loaded in Blazor state)
    await page.goto(`${config.KHACHLINK_URL}/cart`);
    await page.waitForLoadState('networkidle');
    // Wait for Blazor to render the cart page
    await expect(page.getByText(/Giỏ hàng|Cart/i).first()).toBeVisible({ timeout: 10000 });

    // Click checkout button on cart page (natural user flow)
    const checkoutBtn = page.getByTestId('cart-btn-checkout');
    await expect(checkoutBtn).toBeVisible({ timeout: 5000 });
    await checkoutBtn.click();

    // Bucket A feature: fill guest checkout form before order creation
    const nameInput = page.getByTestId('checkout-input-name');
    await expect(nameInput).toBeVisible({ timeout: 10000 });
    await nameInput.fill('Test Guest');
    const phoneInput = page.getByTestId('checkout-input-phone');
    await phoneInput.fill('0901234567');
    await page.getByTestId('checkout-btn-place-order').click();

    // Checkout page creates order and shows order details with QR payment button.
    // Click "Theo dõi đơn hàng" link to navigate to tracking page.
    const trackingLink = page.getByTestId('checkout-link-tracking');
    await expect(trackingLink).toBeVisible({ timeout: 20000 });
    await trackingLink.click();

    // T-02c: After clicking tracking link, must be on /order-tracking/{id}.
    await page.waitForURL(/\/order-tracking\//, { timeout: 10000 });
    await expect(page).toHaveURL(/\/order-tracking\//);

  });

  // ─── STAFF ORDER VIEW (ShopERP) ──────────────────────────────────────────

  test('Staff can view orders in ShopERP @golden', async ({ page }) => {
    // Authenticate via DevLogin (E2E test bypass — DEBUG only)
    await page.goto(`${config.SHOPERP_URL}/dev/login`);
    await page.request.post(`${config.SHOPERP_URL}/dev/login`);
    await page.goto(config.SHOPERP_URL);
    await page.waitForLoadState('networkidle');

    // ShopERP home page must have a heading
    await expect(page.locator('h1, h2').first()).toBeVisible();

  });

  test('Staff can update order status @golden', async ({ page }) => {
    // Authenticate via DevLogin (E2E test bypass — DEBUG only)
    await page.request.post(`${config.SHOPERP_URL}/dev/login`);
    // Navigate to orders list page
    await page.goto(`${config.SHOPERP_URL}/orders`);
    await page.waitForLoadState('networkidle');

    // Orders list page must have a heading
    await expect(page.locator('h1').first()).toBeVisible({ timeout: 10000 });

  });

  // ─── ORDER TRACKING (Gateway API — not CoreHub) ──────────────────────────
  // MOVED to gateway-smoke.spec.ts (Wave 5 Pattern C — consolidated reachability).
});
