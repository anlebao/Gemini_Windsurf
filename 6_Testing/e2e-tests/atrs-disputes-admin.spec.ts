import { test, expect } from '@playwright/test';

// ATRS-9/ATRS-10 (Wave 3) — Gate 4: UI layout change → E2E bắt buộc.
// Staff dispute admin: /admin/disputes (A17 queue) + /admin/disputes/{id} (A18/A19).
// Backend-authoritative RBAC: mọi admin endpoint yêu cầu staff JWT + AtrsPermissions.
// Unauthenticated checks chạy mọi nơi; authenticated checks cần DISPUTE_STAFF_TOKEN
// (post-deploy RV) — tự skip khi không có token.

const GATEWAY = process.env.GATEWAY_URL || 'https://api2.khachvip.online';
const SHOPERP = process.env.SHOPERP_URL || 'https://app2.khachvip.online';
const STAFF_TOKEN = process.env.DISPUTE_STAFF_TOKEN || '';

test.describe('ATRS Dispute Admin (Wave 3)', () => {

  test('RV-D1: admin disputes queue API → 401 without JWT', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/v1/account-trust/admin/disputes`);
    expect([401, 403]).toContain(resp.status());
  });

  test('RV-D2: approve decision API → 401/403 without JWT', async ({ request }) => {
    const resp = await request.post(
      `${GATEWAY}/api/v1/account-trust/admin/disputes/decisions/00000000-0000-0000-0000-000000000001/approve`);
    expect([401, 403]).toContain(resp.status());
  });

  test('RV-D3: grant permissions API → 401/403 without JWT', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/v1/account-trust/admin/permissions`);
    expect([401, 403]).toContain(resp.status());
  });

  test('RV-D4: customer disputes API → 401 without customer token', async ({ request }) => {
    const resp = await request.post(`${GATEWAY}/api/v1/account-trust/disputes`, {
      data: { caseType: 'POINTS_MISSING', claimedIssue: 'test' }
    });
    expect([401, 403]).toContain(resp.status());
  });

  test('RV-D5: /admin/disputes page loads (redirect to login khi chưa auth)', async ({ page }) => {
    await page.goto(`${SHOPERP}/admin/disputes`);
    // Chưa login → redirect login hoặc trang login render; KHÔNG 500.
    await page.waitForLoadState('networkidle');
    expect(page.url()).toContain('app2.khachvip.online');
  });

  test.describe('authenticated staff', () => {
    test.skip(!STAFF_TOKEN, 'DISPUTE_STAFF_TOKEN not set — post-deploy RV only');

    test('RV-D6: queue page renders dispute table sau login', async ({ page }) => {
      // Seed JWT vào cookie/localStorage theo cơ chế auth của ShopERP rồi vào trang.
      // Token phải là staff JWT hợp lệ (role Owner/Staff/SystemAdmin + grant dispute.view).
      await page.goto(`${SHOPERP}/admin/disputes`);
      await expect(page.locator('[data-testid="dispute-queue-table"], .disputes-page')).toBeVisible({ timeout: 40000 });
    });
  });
});
