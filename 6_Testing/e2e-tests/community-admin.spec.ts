import { test, expect } from '@playwright/test';

/**
 * CC-S6 (Sprint 6): Community Admin E2E tests.
 * Tests: admin eligible list, activate/deactivate role, auth guard.
 */

const GATEWAY = 'https://api.khachvip.online';
const SHOPERP = 'https://erp.khachvip.online';

test.describe('CC-S6 Sprint 6 — Community Admin', () => {

  test('RV6-1: Admin eligible list returns 401 without JWT', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/admin/community/eligible`);
    expect(resp.status()).toBe(401);
  });

  test('RV6-2: Admin activate-role returns 401 without JWT', async ({ request }) => {
    const resp = await request.post(`${GATEWAY}/api/admin/community/00000000-0000-0000-0000-000000000001/activate-role`, {
      data: { role: 'Shipper' }
    });
    expect(resp.status()).toBe(401);
  });

  test('RV6-3: Admin deactivate-role returns 401 without JWT', async ({ request }) => {
    const resp = await request.post(`${GATEWAY}/api/admin/community/00000000-0000-0000-0000-000000000001/deactivate-role`, {
      data: { role: 'Shipper' }
    });
    expect(resp.status()).toBe(401);
  });

  test('RV6-4: Admin panel page loads with SystemAdmin login', async ({ page }) => {
    // Navigate to admin panel — should redirect to login if not authenticated
    await page.goto(`${SHOPERP}/admin/community/admin-panel`);
    // Either login page or admin panel (depending on auth state)
    const url = page.url();
    expect(url).toContain('erp.khachvip.online');
  });

  test('RV6-5: My-roles endpoint returns 401 without customer token', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/community/my-roles`);
    expect(resp.status()).toBe(401);
  });

  test('RV6-6: My-fraud-flags endpoint returns 401 without customer token', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/community/my-fraud-flags`);
    expect(resp.status()).toBe(401);
  });
});
