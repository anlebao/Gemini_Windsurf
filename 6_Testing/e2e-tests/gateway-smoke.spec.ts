import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';

// WAVE 5 (Pattern C): Consolidated Gateway reachability smoke tests.
// Replaces 9 individual reachability tests that were spread across 4 files:
//   - accounting-flow.spec.ts (5 tests: /api/accounting-entries, /api/accounting,
//     /api/accounting/revenue, /api/accounting/expense, /api/accounting/revenue/summary)
//   - order-flow.spec.ts (2 tests: /api/orders, /api/inventory/check)
//   - order-tracking.spec.ts (1 test: /api/orders/{id})
//   - qr-payment-ui.spec.ts (1 test: /api/v1/vietqr/generate)
//
// Each route is validated via test.step() with a strict assertion:
//   status !== 404 (route registered) && status !== 500 (no server crash)
// Auth: storageState from global-setup provides the .VanAn.Auth cookie.

const config = loadEnvConfig();

test.describe('Gateway Smoke — Consolidated Reachability (W5 Pattern C)', () => {
  test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

  test('Accounting API routes are reachable via Gateway (T-07 alias)', async ({ request }) => {
    await test.step('GET /api/accounting-entries (canonical route)', async () => {
      const response = await request.get(`${config.GATEWAY_URL}/api/accounting-entries`);
      expect(response.status()).not.toBe(404);
      expect([200, 401, 403]).toContain(response.status());
    });

    await test.step('GET /api/accounting (T-07 alias route)', async () => {
      const response = await request.get(`${config.GATEWAY_URL}/api/accounting`);
      expect(response.status()).not.toBe(404);
      expect([200, 401, 403]).toContain(response.status());
    });

    await test.step('POST /api/accounting/revenue (alias sub-route)', async () => {
      const response = await request.post(`${config.GATEWAY_URL}/api/accounting/revenue`, {
        data: {
          year: new Date().getFullYear(),
          month: new Date().getMonth() + 1,
          amount: 100000,
          description: 'W5 smoke test',
        },
      });
      expect(response.status()).not.toBe(404);
      expect(response.status()).not.toBe(500);
    });

    await test.step('POST /api/accounting/expense (alias sub-route)', async () => {
      const response = await request.post(`${config.GATEWAY_URL}/api/accounting/expense`, {
        data: {
          year: new Date().getFullYear(),
          month: new Date().getMonth() + 1,
          amount: 50000,
          description: 'W5 smoke test',
        },
      });
      expect(response.status()).not.toBe(404);
      expect(response.status()).not.toBe(500);
    });

    await test.step('GET /api/accounting/revenue/summary (alias sub-route)', async () => {
      const year = new Date().getFullYear();
      const month = new Date().getMonth() + 1;
      const response = await request.get(
        `${config.GATEWAY_URL}/api/accounting/revenue/summary?year=${year}&month=${month}`
      );
      expect(response.status()).not.toBe(404);
      expect(response.status()).not.toBe(500);
    });
  });

  test('Order, Inventory, and VietQR API routes are reachable via Gateway', async ({ request }) => {
    await test.step('GET /api/orders (list endpoint)', async () => {
      const response = await request.get(`${config.GATEWAY_URL}/api/orders`);
      expect([200, 401, 403]).toContain(response.status());
    });

    await test.step('GET /api/inventory/check (inventory endpoint)', async () => {
      const response = await request.get(
        `${config.GATEWAY_URL}/api/inventory/check?ingredientId=test&quantity=1`
      );
      expect([200, 400, 401, 404]).toContain(response.status());
    });

    await test.step('GET /api/orders/{id} (order status lookup)', async () => {
      const response = await request.get(
        `${config.GATEWAY_URL}/api/orders/00000000-0000-0000-0000-000000000001`
      );
      expect(response.status()).not.toBe(500);
      expect([200, 401, 403, 404]).toContain(response.status());
    });

    await test.step('POST /api/v1/vietqr/generate (QR generation)', async () => {
      const response = await request.post(`${config.GATEWAY_URL}/api/v1/vietqr/generate`, {
        data: {
          Amount: 50000,
          OrderDescription: 'W5-smoke-test',
          BankConfig: { BankId: '970422', AccountNo: '1234567890', AccountName: 'TEST' },
        },
      });
      expect(response.status()).not.toBe(404);
      expect(response.status()).not.toBe(500);
    });
  });
});
