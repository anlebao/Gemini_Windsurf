import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// T-03d: Updated selectors to match real rendered HTML after QrPaymentModal integration.
// Original spec had: waitForSelector('#qrPaymentModal') — not wired to any page.
// Fixed: #qrPaymentModal now exists on Checkout page when modal is open (T-03b).
//
// UI flow tests moved to qr-payment-ui.spec.ts (TDD spec, T-03a).
// This file focuses on Gateway API contract tests only.

const config = loadEnvConfig();
const reporter = new TestReporter('E2E Tests');

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VietQR Gateway API Tests', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting VietQR Gateway API Tests...');
  });

  // ─── GATEWAY API CONTRACT ─────────────────────────────────────────────────

  test('TC_QR_Generation - Gateway /api/v1/vietqr/generate returns valid response', async ({ request }) => {
    const qrRequest = {
      Amount: 50000,
      OrderDescription: 'TEST_ORDER_123',
      BankConfig: {
        BankId: '970422',
        AccountNo: '1234567890',
        AccountName: 'TEST SHOP',
      },
    };

    const response = await request.post(`${config.GATEWAY_URL}/api/v1/vietqr/generate`, {
      data: qrRequest,
    });

    expect(response.ok()).toBeTruthy();

    const result = await response.json();

    // Response must have these fields (case-insensitive JSON keys from .NET camelCase)
    const qrImageUrl = result.qrImageUrl ?? result.QrImageUrl;
    const amount     = result.amount ?? result.Amount;
    const orderId    = result.orderId ?? result.OrderId;

    expect(qrImageUrl).toBeTruthy();
    expect(amount).toBe(50000);
    expect(orderId).toBe('TEST_ORDER_123');

    // Verify VietQR URL format
    expect(qrImageUrl).toContain('img.vietqr.io/image/970422-1234567890');
    expect(qrImageUrl).toContain('amount=50000');

  });

  test('TC_QR_Validation - Gateway /api/v1/vietqr/validate-bank validates correctly', async ({ request }) => {
    // Valid bank
    const validResponse = await request.post(`${config.GATEWAY_URL}/api/v1/vietqr/validate-bank`, {
      data: { BankId: '970422', AccountNo: '1234567890', AccountName: 'VALID BANK' },
    });
    expect(validResponse.ok()).toBeTruthy();
    const validResult = await validResponse.json();
    expect(validResult).toBe(true);

    // Invalid bank
    const invalidResponse = await request.post(`${config.GATEWAY_URL}/api/v1/vietqr/validate-bank`, {
      data: { BankId: '999999', AccountNo: '123', AccountName: 'INVALID BANK' },
    });
    expect(invalidResponse.ok()).toBeTruthy();
    const invalidResult = await invalidResponse.json();
    expect(invalidResult).toBe(false);

  });

  test('TC_QR_SupportedBanks - Gateway /api/v1/vietqr/supported-banks returns bank list', async ({ request }) => {
    const response = await request.get(`${config.GATEWAY_URL}/api/v1/vietqr/supported-banks`);
    expect(response.ok()).toBeTruthy();

    const banks = await response.json();
    expect(Array.isArray(banks)).toBeTruthy();
    expect(banks.length).toBeGreaterThan(0);

    // Vietcombank must be in the list
    const vietcombank = banks.find((b: any) =>
      (b.Id ?? b.id) === '970422'
    );
    expect(vietcombank).toBeTruthy();
    expect(vietcombank.Name ?? vietcombank.name).toBe('Vietcombank');

  });

  // ─── UI SMOKE: QR button on checkout page ────────────────────────────────
  // Full modal flow is in qr-payment-ui.spec.ts (T-03a).

  test('TC_QR_Display - Checkout page renders QR trigger button', async ({ page }) => {
    // Navigate to home and add to cart
    await page.goto(`${config.KHACHLINK_URL}/home`);
    await page.waitForLoadState('networkidle');

    const productCard = page.locator('.feature-card').first();
    await expect(productCard).toBeVisible({ timeout: 10000 });
    await productCard.locator('button:has-text("Đặt ngay")').click();

    await page.goto(`${config.KHACHLINK_URL}/checkout`);
    await page.waitForLoadState('networkidle');

    // "Thanh toán QR" button must be present (T-03c integration)
    const qrButton = page.locator('button:has-text("Thanh toán QR")');
    await expect(qrButton).toBeVisible({ timeout: 8000 });

  });
});
