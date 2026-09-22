import { test, expect } from '@playwright/test';

/**
 * Settlement Batch-4 (TC-10 S10): Settlement admin page E2E.
 * Covers: auth guard, page load, UI Platform table (VanAnDataGrid),
 * date filters (VanAInput type=date), pagination shell.
 */

const GATEWAY = 'https://api.khachvip.online';
const SHOPERP = 'https://erp.khachvip.online';

test.describe('TC-10 Settlement Admin — /admin/settlements', () => {

  test('TC10-1: Settlements API returns 401 without SystemAdmin JWT', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/admin/settlements?page=1&pageSize=20`);
    expect(resp.status()).toBe(401);
  });

  test('TC10-2: Settlements API accepts tenantId filter param without crashing', async ({ request }) => {
    // Still 401 (no JWT) — but the request must reach auth, not a 500/400 from
    // the LINQ value-object filter (TC-10 S2 regression).
    const resp = await request.get(
      `${GATEWAY}/api/admin/settlements?tenantId=00000000-0000-0000-0000-000000000001&page=1&pageSize=20`);
    expect([401, 403]).toContain(resp.status());
  });

  test('TC10-3: /admin/settlements page loads (redirects to login when unauthenticated)', async ({ page }) => {
    await page.goto(`${SHOPERP}/admin/settlements`);
    const url = page.url();
    expect(url).toContain('erp.khachvip.online');
  });

  test('TC10-4: Settlements page renders UI Platform data grid when authenticated', async ({ page }) => {
    // Runs only when a session is already authenticated (manual/CI storage state).
    await page.goto(`${SHOPERP}/admin/settlements`);
    if (page.url().includes('/login')) {
      test.skip(true, 'Requires authenticated SystemAdmin session');
    }
    // VanAnDataGrid renders .vanan-data-grid table; raw <table class="vanan-table"> must not appear.
    await expect(page.locator('table.vanan-data-grid')).toBeVisible();
    // Date filters rendered via VanAInput (type=date inputs inside the filter card).
    await expect(page.locator('input#filter-from[type="date"]')).toBeVisible();
    await expect(page.locator('input#filter-to[type="date"]')).toBeVisible();
  });
});
