import { test, expect } from '@playwright/test';
import { getTestConfig } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

test.describe('VietQR Payment Tests', () => {
  let config: any;
  let reporter: TestReporter;

  test.beforeAll(async () => {
    config = await getTestConfig();
    reporter = new TestReporter('qr-payment');
  });

  test.beforeEach(async () => {
    await reporter.startTest('QR Payment Test');
  });

  test.afterEach(async () => {
    await reporter.endTest();
  });

  test('TC_QR_Display - Should display QR code image with correct URL', async ({ page }) => {
    try {
      // Navigate to KhachLink
      await page.goto(config.KHACHLINK_URL);
      
      // Wait for products to load
      await page.waitForSelector('.feature-card');
      
      // Add a product to cart
      const firstProduct = page.locator('.feature-card').first();
      await firstProduct.locator('button').click();
      
      // Wait for cart to update
      await page.waitForTimeout(1000);
      
      // Click QR Payment button
      const qrButton = page.locator('button:has-text("Thanh toán QR")');
      await qrButton.click();
      
      // Wait for modal to appear
      await page.waitForSelector('#qrPaymentModal', { state: 'visible' });
      
      // Check if QR image is displayed
      const qrImage = page.locator('.qr-image');
      await expect(qrImage).toBeVisible();
      
      // Get the image source URL
      const imgSrc = await qrImage.getAttribute('src');
      expect(imgSrc).toBeTruthy();
      
      // Verify VietQR URL format
      expect(imgSrc).toContain('img.vietqr.io/image/');
      expect(imgSrc).toContain('-template.jpg');
      expect(imgSrc).toContain('amount=');
      expect(imgSrc).toContain('addInfo=');
      
      // Verify bank info is displayed
      await expect(page.locator('text=Thông tin thanh toán')).toBeVisible();
      await expect(page.locator('text=Số tiền:')).toBeVisible();
      await expect(page.locator('text=Mã đơn hàng:')).toBeVisible();
      await expect(page.locator('text=Ngân hàng:')).toBeVisible();
      
      await reporter.addResult('QR Display', 'pass', 'QR code displayed with correct VietQR URL format');
      
    } catch (error) {
      await reporter.addResult('QR Display', 'fail', error.message);
      throw error;
    }
  });

  test('TC_QR_Generation - Should generate QR with correct parameters', async ({ request }) => {
    try {
      const qrRequest = {
        Amount: 50000,
        OrderDescription: 'TEST_ORDER_123',
        BankConfig: {
          BankId: '970422',
          AccountNo: '1234567890',
          AccountName: 'TEST SHOP'
        }
      };

      const response = await request.post(`${config.GATEWAY_URL}/api/v1/vietqr/generate`, {
        data: qrRequest
      });

      expect(response.ok()).toBeTruthy();
      
      const result = await response.json();
      
      // Verify response structure
      expect(result).toHaveProperty('QrImageUrl');
      expect(result).toHaveProperty('PaymentUrl');
      expect(result).toHaveProperty('Amount', 50000);
      expect(result).toHaveProperty('OrderId', 'TEST_ORDER_123');
      expect(result).toHaveProperty('GeneratedAt');
      
      // Verify QR URL format
      expect(result.QrImageUrl).toContain('img.vietqr.io/image/970422-1234567890-template.jpg');
      expect(result.QrImageUrl).toContain('amount=50000');
      expect(result.QrImageUrl).toContain('addInfo=TEST_ORDER_123');
      
      await reporter.addResult('QR Generation', 'pass', 'QR generated with correct parameters');
      
    } catch (error) {
      await reporter.addResult('QR Generation', 'fail', error.message);
      throw error;
    }
  });

  test('TC_QR_Validation - Should validate bank configuration', async ({ request }) => {
    try {
      // Test valid bank config
      const validBankConfig = {
        BankId: '970422',
        AccountNo: '1234567890',
        AccountName: 'VALID BANK'
      };

      const validResponse = await request.post(`${config.GATEWAY_URL}/api/v1/vietqr/validate-bank`, {
        data: validBankConfig
      });

      expect(validResponse.ok()).toBeTruthy();
      const validResult = await validResponse.json();
      expect(validResult).toBe(true);

      // Test invalid bank config
      const invalidBankConfig = {
        BankId: '999999',
        AccountNo: '123',
        AccountName: 'INVALID BANK'
      };

      const invalidResponse = await request.post(`${config.GATEWAY_URL}/api/v1/vietqr/validate-bank`, {
        data: invalidBankConfig
      });

      expect(invalidResponse.ok()).toBeTruthy();
      const invalidResult = await invalidResponse.json();
      expect(invalidResult).toBe(false);

      await reporter.addResult('QR Validation', 'pass', 'Bank validation working correctly');
      
    } catch (error) {
      await reporter.addResult('QR Validation', 'fail', error.message);
      throw error;
    }
  });

  test('TC_QR_SupportedBanks - Should return list of supported banks', async ({ request }) => {
    try {
      const response = await request.get(`${config.GATEWAY_URL}/api/v1/vietqr/supported-banks`);
      
      expect(response.ok()).toBeTruthy();
      
      const banks = await response.json();
      expect(banks).toBeInstanceOf(Array);
      expect(banks.length).toBeGreaterThan(0);
      
      // Check if Vietcombank is in the list
      const vietcombank = banks.find((bank: any) => bank.Id === '970422');
      expect(vietcombank).toBeTruthy();
      expect(vietcombank.Name).toBe('Vietcombank');
      
      await reporter.addResult('QR Supported Banks', 'pass', 'Supported banks list retrieved successfully');
      
    } catch (error) {
      await reporter.addResult('QR Supported Banks', 'fail', error.message);
      throw error;
    }
  });
});
