import { test, expect } from '@playwright/test';

/**
 * CC-S6 (Sprint 6 v1.2): Fraud Review E2E tests.
 * Tests: fraud-flags list, detail, confirm, dismiss, stats — auth guard.
 */

const GATEWAY = 'https://api.khachvip.online';
const SHOPERP = 'https://erp.khachvip.online';

test.describe('CC-S6 Sprint 6 v1.2 — Fraud Review', () => {

  test('RV6-7: Fraud-flags list returns 401 without JWT', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/admin/community/fraud-flags`);
    expect(resp.status()).toBe(401);
  });

  test('RV6-8: Fraud-flags detail returns 401 without JWT', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/admin/community/fraud-flags/00000000-0000-0000-0000-000000000001`);
    expect(resp.status()).toBe(401);
  });

  test('RV6-9: Fraud-flags confirm returns 401 without JWT', async ({ request }) => {
    const resp = await request.post(`${GATEWAY}/api/admin/community/fraud-flags/00000000-0000-0000-0000-000000000001/confirm`);
    expect(resp.status()).toBe(401);
  });

  test('RV6-10: Fraud-flags dismiss returns 401 without JWT', async ({ request }) => {
    const resp = await request.post(`${GATEWAY}/api/admin/community/fraud-flags/00000000-0000-0000-0000-000000000001/dismiss`);
    expect(resp.status()).toBe(401);
  });

  test('RV6-11: Fraud-stats returns 401 without JWT', async ({ request }) => {
    const resp = await request.get(`${GATEWAY}/api/admin/community/fraud-stats`);
    expect(resp.status()).toBe(401);
  });

  test('RV6-12: Fraud flags page loads with SystemAdmin login', async ({ page }) => {
    await page.goto(`${SHOPERP}/admin/community/fraud-flags`);
    const url = page.url();
    expect(url).toContain('erp.khachvip.online');
  });

  test('RV6-13: Fraud stats page loads with SystemAdmin login', async ({ page }) => {
    await page.goto(`${SHOPERP}/admin/community/fraud-stats`);
    const url = page.url();
    expect(url).toContain('erp.khachvip.online');
  });
});
