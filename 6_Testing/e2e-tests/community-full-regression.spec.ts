import { test, expect } from '@playwright/test';

/**
 * CC-S6 (Sprint 6): Full regression E2E tests.
 * Smoke test: verify all community endpoints from S1-S6 are reachable.
 */

const GATEWAY = 'https://api.khachvip.online';
const KHACHLINK = 'https://diemthuong.khachvip.online';

test.describe('CC-S6 Sprint 6 — Full Regression Smoke', () => {

  // Sprint 1-2: Shipper flow
  test('RV6-14: Nearby-orders returns 401 without token (Sprint 1)', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/community/nearby-orders?lat=10.8&lng=106.7&radiusKm=5`);
    expect(resp.status()).toBe(401);
  });

  test('RV6-15: Role endpoint returns 401 without token (Sprint 1)', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/community/role`);
    expect(resp.status()).toBe(401);
  });

  // Sprint 3: Chat
  test('RV6-16: Chat history returns 401 without token (Sprint 3)', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/community/chat/history/00000000-0000-0000-0000-000000000001`);
    expect(resp.status()).toBe(401);
  });

  // Sprint 4: Salesman
  test('RV6-17: Nearby-products returns 401 without token (Sprint 4)', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/community/nearby-products?lat=10.8&lng=106.7&radiusKm=10`);
    expect(resp.status()).toBe(401);
  });

  // Sprint 5: Wallet
  test('RV6-18: Wallet returns 401 without token (Sprint 5)', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/community/wallet`);
    expect(resp.status()).toBe(401);
  });

  // Sprint 6: Admin + Fraud Review
  test('RV6-19: Admin eligible returns 401 without JWT (Sprint 6)', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/admin/community/eligible`);
    expect(resp.status()).toBe(401);
  });

  test('RV6-20: Fraud-flags returns 401 without JWT (Sprint 6)', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/admin/community/fraud-flags`);
    expect(resp.status()).toBe(401);
  });

  test('RV6-21: My-roles returns 401 without token (Sprint 6)', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/community/my-roles`);
    expect(resp.status()).toBe(401);
  });

  test('RV6-22: My-fraud-flags returns 401 without token (Sprint 6)', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/community/my-fraud-flags`);
    expect(resp.status()).toBe(401);
  });

  // KhachLink pages
  test('RV6-23: KhachLink profile page loads', async ({ page }) => {
    await page.goto(`${KHACHLINK}/profile`);
    await expect(page).toHaveURL(/diemthuong\.khachvip\.online/);
  });

  test('RV6-24: KhachLink home page loads', async ({ page }) => {
    await page.goto(`${KHACHLINK}/`);
    await expect(page).toHaveURL(/diemthuong\.khachvip\.online/);
  });
});
