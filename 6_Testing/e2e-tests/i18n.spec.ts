import { test, expect } from '@playwright/test';
import { getTestConfig } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

test.describe('Internationalization (i18n) Tests', () => {
  let config: any;
  let reporter: TestReporter;

  test.beforeAll(async () => {
    config = await getTestConfig();
    reporter = new TestReporter('i18n');
  });

  test.beforeEach(async () => {
    await reporter.startTest('i18n Test');
  });

  test.afterEach(async () => {
    await reporter.endTest();
  });

  test('TC_i18n_Switch - Should display English text when Accept-Language header is en-US', async ({ request }) => {
    try {
      // Test API response with English language
      const response = await request.get(`${config.GATEWAY_URL}/api/v1/vietqr/supported-banks`, {
        headers: {
          'Accept-Language': 'en-US,en;q=0.9'
        }
      });

      expect(response.ok()).toBeTruthy();
      
      // Check if response headers contain Content-Language
      const contentLanguage = response.headers()['content-language'];
      expect(contentLanguage).toBe('en');
      
      await reporter.addResult('i18n Switch', 'pass', 'API responded with English language');
      
    } catch (error) {
      await reporter.addResult('i18n Switch', 'fail', error.message);
      throw error;
    }
  });

  test('TC_i18n_Vietnamese - Should display Vietnamese text when Accept-Language header is vi-VN', async ({ request }) => {
    try {
      // Test API response with Vietnamese language
      const response = await request.get(`${config.GATEWAY_URL}/api/v1/vietqr/supported-banks`, {
        headers: {
          'Accept-Language': 'vi-VN,vi;q=0.9'
        }
      });

      expect(response.ok()).toBeTruthy();
      
      // Check if response headers contain Content-Language
      const contentLanguage = response.headers()['content-language'];
      expect(contentLanguage).toBe('vi');
      
      await reporter.addResult('i18n Vietnamese', 'pass', 'API responded with Vietnamese language');
      
    } catch (error) {
      await reporter.addResult('i18n Vietnamese', 'fail', error.message);
      throw error;
    }
  });

  test('TC_i18n_Fallback - Should fallback to default language when unsupported language is requested', async ({ request }) => {
    try {
      // Test API response with unsupported language
      const response = await request.get(`${config.GATEWAY_URL}/api/v1/vietqr/supported-banks`, {
        headers: {
          'Accept-Language': 'fr-FR,fr;q=0.9' // French (unsupported)
        }
      });

      expect(response.ok()).toBeTruthy();
      
      // Should fallback to default language (vi)
      const contentLanguage = response.headers()['content-language'];
      expect(['vi', 'en']).toContain(contentLanguage);
      
      await reporter.addResult('i18n Fallback', 'pass', 'API fell back to supported language');
      
    } catch (error) {
      await reporter.addResult('i18n Fallback', 'fail', error.message);
      throw error;
    }
  });

  test('TC_i18n_LocalizationService - Should return localized strings', async ({ request }) => {
    try {
      // Test Vietnamese localization
      const viResponse = await request.get(`${config.GATEWAY_URL}/api/v1/localization/strings`, {
        headers: {
          'Accept-Language': 'vi-VN'
        }
      });

      expect(viResponse.ok()).toBeTruthy();
      
      const viStrings = await viResponse.json();
      expect(viStrings).toHaveProperty('Common.Order');
      expect(viStrings['Common.Order']).toBe('Đơn hàng');
      
      // Test English localization
      const enResponse = await request.get(`${config.GATEWAY_URL}/api/v1/localization/strings`, {
        headers: {
          'Accept-Language': 'en-US'
        }
      });

      expect(enResponse.ok()).toBeTruthy();
      
      const enStrings = await enResponse.json();
      expect(enStrings).toHaveProperty('Common.Order');
      expect(enStrings['Common.Order']).toBe('Order');
      
      await reporter.addResult('i18n Localization Service', 'pass', 'Localization service returns correct strings');
      
    } catch (error) {
      await reporter.addResult('i18n Localization Service', 'fail', error.message);
      throw error;
    }
  });

  test('TC_i18n_ProductNames - Should return product names in correct language', async ({ request }) => {
    try {
      // Test Vietnamese product names
      const viResponse = await request.get(`${config.GATEWAY_URL}/api/v1/products`, {
        headers: {
          'Accept-Language': 'vi-VN'
        }
      });

      expect(viResponse.ok()).toBeTruthy();
      
      const viProducts = await viResponse.json();
      if (viProducts.length > 0) {
        const firstProduct = viProducts[0];
        expect(firstProduct).toHaveProperty('Name');
        expect(firstProduct.Name).toBeTruthy();
      }
      
      // Test English product names
      const enResponse = await request.get(`${config.GATEWAY_URL}/api/v1/products`, {
        headers: {
          'Accept-Language': 'en-US'
        }
      });

      expect(enResponse.ok()).toBeTruthy();
      
      const enProducts = await enResponse.json();
      if (enProducts.length > 0) {
        const firstProduct = enProducts[0];
        expect(firstProduct).toHaveProperty('Name_EN');
        expect(firstProduct.Name_EN).toBeTruthy();
      }
      
      await reporter.addResult('i18n Product Names', 'pass', 'Product names available in multiple languages');
      
    } catch (error) {
      await reporter.addResult('i18n Product Names', 'fail', error.message);
      throw error;
    }
  });
});
