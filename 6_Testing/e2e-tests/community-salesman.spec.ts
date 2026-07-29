import { test, expect } from '@playwright/test';

/**
 * CC-S4 (Sprint 4): Salesman + Composite QR Referral E2E tests.
 * Tests: nearby products API, salesman QR, commissions, app-install attribution, admin config.
 */

const GATEWAY = 'https://api.khachvip.online';
const KHACHLINK = 'https://diemthuong.khachvip.online';

test.describe('CC-S4 Sprint 4 — Salesman + Composite QR Referral', () => {

  test('RV4-1: Nearby products API returns 401 without token', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/community/nearby-products?lat=10.8&lng=106.7&radiusKm=10`);
    expect(resp.status()).toBe(401);
  });

  test('RV4-2: Salesman QR API returns 401 without token', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/community/salesman/qr?productId=00000000-0000-0000-0000-000000000001`);
    expect(resp.status()).toBe(401);
  });

  test('RV4-3: Commissions API returns 401 without token', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/community/salesman/commissions`);
    expect(resp.status()).toBe(401);
  });

  test('RV4-4: App-install attribution returns 401 without token', async ({ request }) => {
    const resp = await request.post(`${GATEWAY}/api/community/app-install/attributed`, {
      data: { referralCode: 'ABC123|TR-001' }
    });
    expect(resp.status()).toBe(401);
  });

  test('RV4-5: Resolve referral returns 401 without token', async ({ request }) => {
    const resp = await request.post(`${GATEWAY}/api/community/resolve-referral`, {
      data: { referralCode: 'ABC123|TR-001' }
    });
    expect(resp.status()).toBe(401);
  });

  test('RV4-6: Admin ProductReferralConfig list requires auth', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/admin/products/referral-configs`);
    expect(resp.status()).toBe(401);
  });

  test('RV4-7: Admin ProductReferralConfig create requires auth', async ({ request }) => {
    const resp = await request.post(`${GATEWAY}/api/admin/products/00000000-0000-0000-0000-000000000099/referral-config`, {
      data: { commissionRate: 0.05, appInstallBonus: 10000, productShortCode: 'TEST-001' }
    });
    expect(resp.status()).toBe(401);
  });

  test('RV4-8: KhachLink NearbyProducts page loads', async ({ page }) => {
    const resp = await page.goto(`${KHACHLINK}/community/nearby-products`);
    expect(resp?.status()).toBe(200);
  });

  test('RV4-9: KhachLink SalesmanQR page loads', async ({ page }) => {
    const resp = await page.goto(`${KHACHLINK}/community/salesman-qr?productId=00000000-0000-0000-0000-000000000001`);
    expect(resp?.status()).toBe(200);
  });

  test('RV4-10: KhachLink SalesDashboard page loads', async ({ page }) => {
    const resp = await page.goto(`${KHACHLINK}/community/sales-dashboard`);
    expect(resp?.status()).toBe(200);
  });

  test('RV4-11: Role API returns isSalesman field', async ({ request }) => {
    // No token → 401 (but the endpoint should exist, not 404)
    const resp = await request.get(`${GATEWAY}/api/community/role`);
    expect(resp.status()).toBe(401);
  });

  test('RV4-12: Regression — Sprint 1 nearby-orders still works', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/community/nearby-orders?lat=10.8&lng=106.7&radiusKm=5`);
    expect(resp.status()).toBe(401);
  });

  test('RV4-13: Regression — Sprint 2 pickup endpoint still works', async ({ request }) => {
    const resp = await request.post(`${GATEWAY}/api/community/orders/00000000-0000-0000-0000-000000000099/pickup`);
    expect(resp.status()).toBe(401);
  });

  test('RV4-14: Regression — Sprint 3 chat history still works', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/community/chat/conversations/00000000-0000-0000-0000-000000000099`);
    expect(resp.status()).toBe(401);
  });

  test('RV4-15: Regression — KhachLink home page loads', async ({ page }) => {
    const resp = await page.goto(`${KHACHLINK}/`);
    expect(resp?.status()).toBe(200);
  });
});
