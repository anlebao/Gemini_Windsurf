import { test, expect, request } from '@playwright/test';
import { loadEnvConfig } from '../utils/env-config';

// ============================================================
// PROD SMOKE — scheduled READ-ONLY verification against production VPS.
// Runs via .github/workflows/prod-smoke.yml (cron 2x/day + workflow_dispatch).
//
// HARD RULES:
//   - NO writes: no order creation, no data mutation, no config changes.
//     (The full write-path E2E belongs to post-deploy RV, run manually.)
//   - Tolerant asserts on data values (config drift is LOGGED, not failed —
//     the point is availability + shell integrity, not business data).
//   - Catches the #185-class drift (config served to clients) + WASM blank
//     shells + auth-path death — the cheap early-warning layer.
// ============================================================

const config = loadEnvConfig();
const TENANT_ID = '00000000-0000-0000-0000-000000000001';
// Same creds already committed in the other prod specs (RV protocol §CREDENTIALS)
const SYSADMIN_USER = 'sysadmin@vanan.vn';
const SYSADMIN_PASS = '2026@vanan';

test.describe('Production Smoke — read-only (scheduled)', () => {
  test('All VPS health endpoints reachable', async ({ request }) => {
    const endpoints: Array<[string, string]> = [
      ['Gateway', config.GATEWAY_URL],
      ['ShopERP', config.SHOPERP_URL],
      ['KhachLink', config.KHACHLINK_URL],
    ];
    for (const [name, url] of endpoints) {
      const r = await request.get(`${url}/health`, { timeout: 15000 });
      expect(r.ok(), `${name} /health should be 2xx, got ${r.status()}`).toBeTruthy();
    }
  });

  test('SystemAdmin login + tenant impersonate (auth path alive)', async () => {
    const api = await request.newContext({
      baseURL: config.SHOPERP_URL,
      storageState: undefined,
      ignoreHTTPSErrors: true,
    });
    try {
      const login = await api.post(`${config.SHOPERP_URL}/api/platform/login`, {
        data: { Username: SYSADMIN_USER, Password: SYSADMIN_PASS },
        headers: { 'Content-Type': 'application/json' },
      });
      expect(login.ok(), `platform login should succeed, got ${login.status()}`).toBeTruthy();

      const imp = await api.post(`${config.SHOPERP_URL}/api/admin/impersonate/${TENANT_ID}`);
      expect(imp.ok(), `impersonate should succeed, got ${imp.status()}`).toBeTruthy();
    } finally {
      await api.dispose();
    }
  });

  test('KhachLink home renders (no blank WASM shell, no page errors)', async ({ browser }) => {
    const context = await browser.newContext({ ignoreHTTPSErrors: true });
    const page = await context.newPage();
    const pageErrors: string[] = [];
    page.on('pageerror', (err) => pageErrors.push(err.message));

    await page.goto(config.KHACHLINK_URL, { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.waitForTimeout(6000); // Blazor WASM boot

    const text = (await page.textContent('body')) || '';
    expect(text.length, `page should render content (>100 chars), got ${text.length}`).toBeGreaterThan(100);
    console.log(`[prod-smoke] KhachLink rendered ${text.length} chars; title=${await page.title()}`);

    await page.waitForTimeout(2000); // let late page errors surface
    expect(pageErrors, `no unhandled page errors (got ${pageErrors.length})`).toEqual([]);

    await context.close();
  });

  test('Gateway instance config reachable (drift detector — #185 class)', async ({ request }) => {
    let domain = '';
    try {
      domain = new URL(config.KHACHLINK_URL).hostname;
    } catch {
      domain = 'timlathay.com';
    }
    const r = await request.get(
      `${config.GATEWAY_URL}/api/v1/khachlink-instances/by-domain/${domain}`,
      { headers: { Origin: config.KHACHLINK_URL }, timeout: 15000 }
    );
    // Tolerant: 200 is healthy; 404 = domain not registered (still not a 5xx);
    // 401/403 = endpoint exists but auth shape differs. 5xx = real outage.
    expect(r.status(), `by-domain should not be 5xx, got ${r.status()}`).not.toBeGreaterThanOrEqual(500);
    expect(r.status(), `by-domain should not 404 for known domain ${domain}`).not.toBe(404);

    const body = await r.json().catch(() => null);
    if (body) {
      console.log(`[prod-smoke] instance config: isActive=${body.isActive} profile=${body.profile} navFlags=${JSON.stringify(body.navFlags)}`);
      expect(body.isActive).toBeDefined();
    }
  });
});
