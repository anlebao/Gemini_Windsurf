import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// "Cửa hàng của tôi" (task_my_stores_profile, Gate 4 — UI layout change):
// GET /api/customers/my-stores — union Orders.CustomerDeviceId ∪ LoyaltyRewards
// ∪ AllianceWallet breakdown → Profile "Cửa hàng của tôi" section (logo + tên +
// N đơn + badge điểm + "Vào cửa hàng" → same-origin /store/{slug}).
//
// Gating: section KHÔNG bị ShowStores kill-switch giấu (Fix-3 — quan hệ đã tồn
// tại); badge điểm theo ShowLoyaltyHistory. Guest (deviceId-only) cũng xem được.
//
// Env: PROFILE_CUSTOMER_TOKEN (optional) — authenticated assertions auto-skip.

const config = loadEnvConfig();
const reporter = new TestReporter('My Stores Profile E2E');
const apiBase = process.env.GATEWAY_URL || config.GATEWAY_URL || 'https://api.khachvip.online';
const khachLinkBase = config.KHACHLINK_URL;
const CUSTOMER_TOKEN = process.env.PROFILE_CUSTOMER_TOKEN || '';

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VanAn Ecosystem - My Stores on Profile', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting My Stores E2E...');
  });

  test('API: my-stores không có identity header → 401', async ({ request }) => {
    const resp = await request.get(`${apiBase}/api/customers/my-stores`, { timeout: 15000 });
    expect(resp.status()).toBe(401);
    reporter.log('my-stores no-identity → 401');
  });

  test('API: my-stores với X-Customer-Device-Id ngẫu nhiên → 200, stores rỗng', async ({ request }) => {
    const resp = await request.get(`${apiBase}/api/customers/my-stores`, {
      headers: { 'X-Customer-Device-Id': crypto.randomUUID() }, timeout: 15000,
    });
    expect(resp.ok()).toBeTruthy();
    const body = await resp.json();
    expect(Array.isArray(body.stores)).toBeTruthy();
    expect(body.stores.length).toBe(0);
    reporter.log('my-stores random deviceId → 200 empty stores (guest path)');
  });

  test('API: token rác + deviceId → 401 (token path ưu tiên, không silent downgrade)', async ({ request }) => {
    const resp = await request.get(`${apiBase}/api/customers/my-stores`, {
      headers: {
        'X-Customer-Token': 'not-a-real-token-zzz',
        'X-Customer-Device-Id': crypto.randomUUID(),
      },
      timeout: 15000,
    });
    expect(resp.status()).toBe(401);
    reporter.log('my-stores invalid token → 401 (no silent downgrade to header)');
  });

  test('UI guest: /profile với deviceId mới → section ẩn (stores rỗng)', async ({ browser }) => {
    const context = await browser.newContext();
    const page = await context.newPage();
    await page.addInitScript(() => {
      try {
        localStorage.clear();
        localStorage.setItem('customer_device_id', crypto.randomUUID());
      } catch { /* noop */ }
    });
    await page.goto(`${khachLinkBase}/profile`, { waitUntil: 'networkidle' });

    // Guest card hiện; random device → không có stores → section ẩn
    await expect(page.locator('text=Chưa đăng nhập')).toBeVisible({ timeout: 40000 });
    await expect(page.locator('[data-testid="my-stores-section"]')).toHaveCount(0);
    await context.close();
  });

  test.describe('authenticated (PROFILE_CUSTOMER_TOKEN)', () => {
    test.skip(!CUSTOMER_TOKEN, 'PROFILE_CUSTOMER_TOKEN not set — post-deploy RV only');

    test('section render: mỗi row có tên + nút Vào cửa hàng same-origin', async ({ browser }) => {
      const context = await browser.newContext();
      const page = await context.newPage();
      await page.addInitScript((token) => {
        try {
          localStorage.clear();
          localStorage.setItem('customer_token', token);
        } catch { /* noop */ }
      }, CUSTOMER_TOKEN);
      await page.goto(`${khachLinkBase}/profile`, { waitUntil: 'networkidle' });
      await page.waitForTimeout(3000); // my-stores load sau profile

      const section = page.locator('[data-testid="my-stores-section"]');
      if (await section.count() === 0) {
        reporter.log('customer chưa có stores — section ẩn (valid state)');
        await context.close();
        return;
      }

      const rows = page.locator('[data-testid^="my-store-go-"]');
      const count = await rows.count();
      expect(count).toBeGreaterThanOrEqual(1);

      // "Vào cửa hàng" → same-origin /store/{slug|by-id} (Fix-1 — không cross-domain)
      await rows.first().click();
      await page.waitForURL(/\/store\//, { timeout: 15000 });
      expect(page.url()).toContain('/store/');
      reporter.log(`my-stores section: ${count} rows, first → ${page.url()}`);
      await context.close();
    });
  });
});
