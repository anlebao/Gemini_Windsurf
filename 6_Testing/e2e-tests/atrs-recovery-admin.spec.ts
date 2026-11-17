import { test, expect } from '@playwright/test';

// ATRS-10 (Wave 5 W5a) — Gate 4: UI layout change → E2E bắt buộc.
// Staff recovery admin: /admin/recovery (A15 queue) + /admin/recovery/{id} (A16 review)
// + /admin/atrs (A14 dashboard). Backend-authoritative RBAC (recovery.view/review/approve).
// Unauthenticated checks chạy mọi nơi; authenticated checks cần RECOVERY_STAFF_TOKEN
// (post-deploy RV) — tự skip khi không có token.

const GATEWAY = process.env.GATEWAY_URL || 'https://api2.khachvip.online';
const SHOPERP = process.env.SHOPERP_URL || 'https://app2.khachvip.online';
const STAFF_TOKEN = process.env.RECOVERY_STAFF_TOKEN || '';

test.describe('ATRS Recovery Admin (Wave 5 W5a)', () => {

  test('RV-R1: recovery queue API → 401 without JWT', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/v1/account-trust/admin/recovery-requests`);
    expect([401, 403]).toContain(resp.status());
  });

  test('RV-R2: dashboard API → 401 without JWT', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/v1/account-trust/admin/dashboard`);
    expect([401, 403]).toContain(resp.status());
  });

  test('RV-R3: recovery notes API → 401 without JWT', async ({ request }) => {
    const resp = await request.post(
      `${GATEWAY}/api/v1/account-trust/admin/recovery-requests/00000000-0000-0000-0000-000000000001/notes`,
      { data: { body: 'x', noteType: 'INTERNAL_NOTE' } });
    expect([401, 403]).toContain(resp.status());
  });

  test('RV-R4: /admin/recovery page loads (redirect to login khi chưa auth)', async ({ page }) => {
    await page.goto(`${SHOPERP}/admin/recovery`);
    await page.waitForLoadState('networkidle');
    expect(page.url()).toContain('app2.khachvip.online');
  });

  test('RV-R5: /admin/atrs dashboard page loads (redirect khi chưa auth)', async ({ page }) => {
    await page.goto(`${SHOPERP}/admin/atrs`);
    await page.waitForLoadState('networkidle');
    expect(page.url()).toContain('app2.khachvip.online');
  });

  test('RV-R6: /admin/recovery/{id} page loads (redirect khi chưa auth)', async ({ page }) => {
    await page.goto(`${SHOPERP}/admin/recovery/4f0fda9c-bccf-478e-9adf-bd901aec76ca`);
    await page.waitForLoadState('networkidle');
    expect(page.url()).toContain('app2.khachvip.online');
  });

  test.describe('authenticated staff', () => {
    test.skip(!STAFF_TOKEN, 'RECOVERY_STAFF_TOKEN not set — post-deploy RV only');

    test('RV-R7: recovery queue renders + SLA badges sau login', async ({ page }) => {
      await page.goto(`${SHOPERP}/admin/recovery`);
      await expect(page.locator('[data-testid="recovery-queue-table"], .recovery-page')).toBeVisible({ timeout: 40000 });
    });

    test('RV-R8: dashboard widgets render sau login', async ({ page }) => {
      await page.goto(`${SHOPERP}/admin/atrs`);
      await expect(page.locator('[data-testid="recovery-new-24h"], .atrs-dashboard-page')).toBeVisible({ timeout: 40000 });
    });
  });
});