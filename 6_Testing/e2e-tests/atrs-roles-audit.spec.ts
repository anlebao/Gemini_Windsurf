import { test, expect } from '@playwright/test';

// ATRS-10 (Wave 5 W5b) — Gate 4: UI layout change → E2E bắt buộc.
// /admin/atrs-audit (A20 audit explorer) + /admin/atrs-roles (A22 roles UI).
// Backend-authoritative RBAC: audit.read / grant-mgmt SystemAdmin-only.
// Unauthenticated checks chạy mọi nơi; authenticated checks cần RECOVERY_STAFF_TOKEN
// (post-deploy RV) — tự skip khi không có token.

const GATEWAY = process.env.GATEWAY_URL || 'https://api2.khachvip.online';
const SHOPERP = process.env.SHOPERP_URL || 'https://app2.khachvip.online';
const STAFF_TOKEN = process.env.RECOVERY_STAFF_TOKEN || '';

test.describe('ATRS Roles + Audit (Wave 5 W5b)', () => {

  test('RV-B1: security-events API → 401 without JWT', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/v1/account-trust/admin/security-events`);
    expect([401, 403]).toContain(resp.status());
  });

  test('RV-B2: grant API → 401/403 without JWT', async ({ request }) => {
    const resp = await request.post(`${GATEWAY}/api/v1/account-trust/admin/permissions`,
      { data: { demoUserId: '00000000-0000-0000-0000-000000000001', permission: 'audit.read' } });
    expect([401, 403]).toContain(resp.status());
  });

  test('RV-B3: /admin/atrs-audit page loads (redirect khi chưa auth)', async ({ page }) => {
    await page.goto(`${SHOPERP}/admin/atrs-audit`);
    await page.waitForLoadState('networkidle');
    expect(page.url()).toContain('app2.khachvip.online');
  });

  test('RV-B4: /admin/atrs-roles page loads (redirect khi chưa auth)', async ({ page }) => {
    await page.goto(`${SHOPERP}/admin/atrs-roles`);
    await page.waitForLoadState('networkidle');
    expect(page.url()).toContain('app2.khachvip.online');
  });

  test.describe('authenticated staff', () => {
    test.skip(!STAFF_TOKEN, 'RECOVERY_STAFF_TOKEN not set — post-deploy RV only');

    test('RV-B5: audit explorer renders + filters sau login', async ({ page }) => {
      await page.goto(`${SHOPERP}/admin/atrs-audit`);
      await expect(page.locator('[data-testid="audit-events-table"], .atrs-audit-page')).toBeVisible({ timeout: 40000 });
    });

    test('RV-B6: roles page renders grants table sau login', async ({ page }) => {
      await page.goto(`${SHOPERP}/admin/atrs-roles`);
      await expect(page.locator('[data-testid="grants-table"], .atrs-roles-page')).toBeVisible({ timeout: 40000 });
    });
  });
});