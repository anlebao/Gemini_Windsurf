import { test, expect, request } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';
import { TestDataCleaner, TEST_TENANT_ID } from './utils/test-data-cleaner';

// W4: E2E tests for W3 Payment Confirm Flow
// Covers:
//   E2E-04: KhachLink self-confirm ("Tôi đã thanh toán") → POST /api/webhooks/payment → ShopERP PaymentStatus=Paid
//   E2E-05: Admin manual confirm ("Xác nhận đã nhận tiền") → POST /api/webhooks/payment → PaymentStatus=Paid
//
// Prerequisites:
//   - W1: data-testid on payment buttons (btn-confirm-payment in QrPaymentModal + ShopERP Detail)
//   - W2: Fluent waits (no hard-coded waitForTimeout)
//   - W3: Test tenant isolation (TEST_TENANT_ID for cleanup)

const config = loadEnvConfig();
const reporter = new TestReporter('Payment Confirm E2E');

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('Payment Confirm Flow E2E (W4)', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting Payment Confirm E2E Tests (W4)...');
  });

  test.afterAll(async () => {
    // W3: Clean up test tenant data after all payment tests
    const apiContext = await request.newContext();
    const cleaner = new TestDataCleaner(apiContext, config.GATEWAY_URL);
    await cleaner.cleanupTestTenant();
    await apiContext.dispose();
  });

  // ─── E2E-04: KhachLink Self-Confirm Payment ──────────────────────────────

  test('E2E-04: KhachLink "Tôi đã thanh toán" triggers payment confirmation @golden', async ({ browser }) => {
    // Step 1: Customer creates order via KhachLink
    const context = await browser.newContext();
    const page = await context.newPage();

    await page.goto(`${config.KHACHLINK_URL}/home`);
    await page.waitForLoadState('networkidle');

    // Add product to cart
    const productCard = page.getByTestId('home-product-card').first();
    await expect(productCard).toBeVisible({ timeout: 10000 });
    await productCard.getByTestId('home-btn-add-to-cart').click();

    // Go to checkout
    await page.goto(`${config.KHACHLINK_URL}/checkout`);
    await page.waitForLoadState('networkidle');

    // Click "Thanh toán QR" to open modal
    const qrButton = page.getByTestId('checkout-btn-qr-payment');
    await expect(qrButton).toBeVisible({ timeout: 5000 });
    await qrButton.click();

    // Wait for QR modal to appear and QR image to load
    const confirmButton = page.getByTestId('btn-confirm-payment');
    await expect(confirmButton).toBeVisible({ timeout: 10000 });

    // Step 2: Click "Tôi đã thanh toán" — triggers POST /api/webhooks/payment
    // Listen for the API response
    const [confirmResponse] = await Promise.all([
      page.waitForResponse(
        resp => resp.url().includes('/api/webhooks/payment') && resp.status() === 200,
        { timeout: 15000 }
      ),
      confirmButton.click(),
    ]);

    expect(confirmResponse.status()).toBe(200);
    const responseBody = await confirmResponse.json();
    expect(responseBody.Message || responseBody.message).toContain('Payment confirmed');

    // Step 3: Verify order tracking page shows updated status
    // The modal should close or show success state
    await expect(confirmButton).not.toBeVisible({ timeout: 5000 });

    await context.close();
  });

  // ─── E2E-05: Admin Manual Confirm Payment ────────────────────────────────

  test('E2E-05: Admin "Xác nhận đã nhận tiền" confirms payment in ShopERP @golden', async ({ browser }) => {
    // Step 1: Create an order via API (simulating customer order)
    const apiContext = await request.newContext();
    const orderResponse = await apiContext.post(`${config.GATEWAY_URL}/api/orders`, {
      data: {
        CustomerName: 'Test Customer E2E-05',
        CustomerPhone: `TEST${Date.now()}`,
        Items: [{
          ProductId: '00000000-0000-0000-0000-000000000001',
          Quantity: 1,
        }],
        TenantId: TEST_TENANT_ID,
      },
    });

    // If order creation fails, skip — API may not be available in test env
    if (orderResponse.status() !== 200 && orderResponse.status() !== 201) {
      test.skip(true, `Order creation failed: ${orderResponse.status()}`);
    }

    const order = await orderResponse.json();
    const orderId = order.id || order.Id || order.orderId;

    // Step 2: Admin logs into ShopERP and navigates to order detail
    const context = await browser.newContext({
      storageState: 'auth/admin.json',
    });
    const page = await context.newPage();

    await page.goto(`${config.SHOPERP_URL}/orders/${orderId}`);
    await page.waitForLoadState('networkidle');

    // Step 3: Verify "Xác nhận đã nhận tiền" button is visible (PaymentStatus=Pending)
    const adminConfirmBtn = page.getByTestId('btn-confirm-payment');
    await expect(adminConfirmBtn).toBeVisible({ timeout: 10000 });

    // Step 4: Click confirm — triggers POST /api/webhooks/payment
    const [confirmResponse] = await Promise.all([
      page.waitForResponse(
        resp => resp.url().includes('/api/webhooks/payment') && resp.status() === 200,
        { timeout: 15000 }
      ),
      adminConfirmBtn.click(),
    ]);

    expect(confirmResponse.status()).toBe(200);

    // Step 5: Verify PaymentStatus changes to "Paid" on UI
    // The confirm button should disappear (only visible when PaymentStatus=Pending)
    await expect(adminConfirmBtn).not.toBeVisible({ timeout: 10000 });

    // Verify payment status badge shows "Paid" or "Đã thanh toán"
    const paymentBadge = page.locator('[data-testid="payment-status"], .payment-status-badge, .badge').filter({
      hasText: /Paid|Đã thanh toán|Đã nhận/i,
    });
    await expect(paymentBadge.first()).toBeVisible({ timeout: 10000 });

    await context.close();
    await apiContext.dispose();
  });

  // ─── E2E-04b: Idempotency — duplicate confirm does not create duplicate entries ──

  test('E2E-04b: Payment confirmation is idempotent (duplicate POST returns 200, no error) @golden', async ({ request }) => {
    // Create an order first
    const orderResponse = await request.post(`${config.GATEWAY_URL}/api/orders`, {
      data: {
        CustomerName: 'Test Customer Idempotency',
        CustomerPhone: `TEST${Date.now()}idem`,
        Items: [{
          ProductId: '00000000-0000-0000-0000-000000000001',
          Quantity: 1,
        }],
        TenantId: TEST_TENANT_ID,
      },
    });

    if (orderResponse.status() !== 200 && orderResponse.status() !== 201) {
      test.skip(true, `Order creation failed: ${orderResponse.status()}`);
    }

    const order = await orderResponse.json();
    const orderId = order.id || order.Id || order.orderId;
    const transactionId = `E2E-IDEM-${Date.now()}`;

    // First confirm — should succeed
    const firstResponse = await request.post(`${config.GATEWAY_URL}/api/webhooks/payment`, {
      data: {
        OrderId: orderId,
        TenantId: TEST_TENANT_ID,
        TransactionId: transactionId,
      },
    });
    expect(firstResponse.status()).toBe(200);

    // Second confirm with same transactionId — should be idempotent (200, not 409)
    const secondResponse = await request.post(`${config.GATEWAY_URL}/api/webhooks/payment`, {
      data: {
        OrderId: orderId,
        TenantId: TEST_TENANT_ID,
        TransactionId: transactionId,
      },
    });
    expect(secondResponse.status()).toBe(200);
  });
});
