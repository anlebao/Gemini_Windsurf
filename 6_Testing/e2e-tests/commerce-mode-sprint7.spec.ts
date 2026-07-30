import { test, expect } from '@playwright/test';

/**
 * Sprint 7 — Commerce Mode Toggle E2E tests (RV7).
 * Tests: admin auth guard for commerce-mode, community-fund, product-cost-prices endpoints.
 * Tests: admin UI pages load (commerce-mode, community-fund, product-cost-prices).
 */

const GATEWAY = 'https://api.khachvip.online';
const SHOPERP = 'https://erp.khachvip.online';

test.describe('Sprint 7 — Commerce Mode Toggle', () => {

  // === Auth Guard Tests (API) ===

  test('RV7-1: Commerce mode settings returns 401 without JWT', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/admin/commerce-mode`);
    expect(resp.status()).toBe(401);
  });

  test('RV7-2: Set global mode returns 401 without JWT', async ({ request }) => {
    const resp = await request.post(`${GATEWAY}/api/admin/commerce-mode/global`, {
      data: { mode: 'Reseller', platformFeeRate: 0.30, communityFundRate: 0.05, deliveryFee: 15000 }
    });
    expect(resp.status()).toBe(401);
  });

  test('RV7-3: Set tenant override returns 401 without JWT', async ({ request }) => {
    const resp = await request.post(`${GATEWAY}/api/admin/commerce-mode/tenant/00000000-0000-0000-0000-000000000001`, {
      data: { overrideMode: 'Reseller' }
    });
    expect(resp.status()).toBe(401);
  });

  test('RV7-4: Resolve mode returns 401 without JWT', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/admin/commerce-mode/resolve/00000000-0000-0000-0000-000000000001`);
    expect(resp.status()).toBe(401);
  });

  test('RV7-5: Community fund balance returns 401 without JWT', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/admin/community-fund/balance`);
    expect(resp.status()).toBe(401);
  });

  test('RV7-6: Community fund spend returns 401 without JWT', async ({ request }) => {
    const resp = await request.post(`${GATEWAY}/api/admin/community-fund/spend`, {
      data: { amount: 50000, reason: 'Test', recipient: 'Test' }
    });
    expect(resp.status()).toBe(401);
  });

  test('RV7-7: Community fund history returns 401 without JWT', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/admin/community-fund/history`);
    expect(resp.status()).toBe(401);
  });

  test('RV7-8: Product cost prices list returns 401 without JWT', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/admin/product-cost-prices`);
    expect(resp.status()).toBe(401);
  });

  test('RV7-9: Product cost price upsert returns 401 without JWT', async ({ request }) => {
    const resp = await request.post(`${GATEWAY}/api/admin/product-cost-prices`, {
      data: { tenantId: '00000000-0000-0000-0000-000000000001', productId: '00000000-0000-0000-0000-000000000002', costPrice: 50000 }
    });
    expect(resp.status()).toBe(401);
  });

  test('RV7-10: External payment confirmation returns 401 without JWT', async ({ request }) => {
    const resp = await request.post(`${GATEWAY}/api/admin/commerce-mode/confirm-external-payment`, {
      data: { orderId: '00000000-0000-0000-0000-000000000001', amount: 100000, paymentRef: 'VQR-TEST' }
    });
    expect(resp.status()).toBe(401);
  });

  // === UI Page Load Tests ===

  test('RV7-11: Commerce mode admin page loads (redirects to login if not auth)', async ({ page }) => {
    await page.goto(`${SHOPERP}/admin/commerce-mode`);
    const url = page.url();
    expect(url).toContain('erp.khachvip.online');
  });

  test('RV7-12: Community fund admin page loads (redirects to login if not auth)', async ({ page }) => {
    await page.goto(`${SHOPERP}/admin/community-fund`);
    const url = page.url();
    expect(url).toContain('erp.khachvip.online');
  });

  test('RV7-13: Product cost prices admin page loads (redirects to login if not auth)', async ({ page }) => {
    await page.goto(`${SHOPERP}/admin/product-cost-prices`);
    const url = page.url();
    expect(url).toContain('erp.khachvip.online');
  });
});
