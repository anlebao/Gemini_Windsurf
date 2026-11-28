import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// Store Context wave (task_store_context_back_to_store, Gate 4):
// storefront visit/checkout ghi LastStoreVisit vào localStorage → Home
// "Cửa Hàng Của Bạn" hiện + deep-link về shop; back-chip trên các trang
// account (/my-loyalty, /my-orders, /rewards, /cart); "Tiếp tục mua" trên
// order-tracking; home-settings GET áp InstanceFeatureGate (AND semantics).
//
// Env: STORE_CONTEXT_ORDER_ID (optional) — order id thật để verify nút
// "Tiếp tục mua" trên /order-tracking (không có → skip legit).

const config = loadEnvConfig();
const reporter = new TestReporter('Store Context E2E');
const apiBase = process.env.GATEWAY_URL || config.GATEWAY_URL || 'https://api.khachvip.online';
const khachLinkBase = config.KHACHLINK_URL;

const ANTAM_TENANT = 'e2d91cdf-d3c4-4399-b96e-e772a7a41630';
const SHOPAI_DOMAIN = 'shopai.timlathay.com'; // ShowCampaigns=false trên prod

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VanAn Ecosystem - Store Context (back-to-store + Cửa hàng của bạn)', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting Store Context E2E...');
  });

  test('API: khachlink-home-settings áp InstanceFeatureGate theo X-KhachLink-Domain', async ({ request }) => {
    const plain = await request.get(`${apiBase}/api/platform/khachlink-home-settings`, { timeout: 15000 });
    expect(plain.ok()).toBeTruthy();
    const base = await plain.json();
    expect(base.home_CampaignSection_Enabled ?? base.Home_CampaignSection_Enabled).toBeTruthy();

    // shopai (ShowCampaigns=false) → kill-switch trên endpoint global.
    const gated = await request.get(`${apiBase}/api/platform/khachlink-home-settings`, {
      headers: { 'X-KhachLink-Domain': SHOPAI_DOMAIN }, timeout: 15000,
    });
    expect(gated.ok()).toBeTruthy();
    const eff = await gated.json();
    expect(eff.home_CampaignSection_Enabled ?? eff.Home_CampaignSection_Enabled).toBeFalsy();
    reporter.log('home-settings: home campaign off under shopai domain, unchanged without header');
  });

  test('UI: storefront visit → context recorded → Home section + back chip on loyalty', async ({ browser }) => {
    const context = await browser.newContext();
    const page = await context.newPage();

    // 1) Visit storefront (by-id route — không cần slug).
    await page.goto(`${khachLinkBase}/store/by-id/${ANTAM_TENANT}`);
    await page.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => {});
    await page.waitForFunction(
      () => localStorage.getItem('khachlink_last_store') != null, { timeout: 45000 })
      .catch(() => null);

    const visit = await page.evaluate(() => {
      try { return JSON.parse(localStorage.getItem('khachlink_last_store') || 'null'); }
      catch { return null; }
    });
    expect(visit?.t?.toLowerCase()).toBe(ANTAM_TENANT);
    const legacy = await page.evaluate(() => localStorage.getItem('khachlink_last_tenant_id'));
    expect(legacy?.toLowerCase()).toBe(ANTAM_TENANT);
    reporter.log(`store visit recorded: ${JSON.stringify(visit)}`);

    // 2) Home → "Cửa Hàng Của Bạn" hiện + nút "Vào cửa hàng".
    await page.goto(`${khachLinkBase}/`);
    await page.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => {});
    const storeSection = page.locator('.store-section');
    await expect(storeSection).toBeVisible({ timeout: 45000 });
    await expect(storeSection.locator('text=Cửa Hàng Của Bạn')).toBeVisible();
    const enterBtn = storeSection.locator('text=Vào cửa hàng');
    await expect(enterBtn).toBeVisible();
    await enterBtn.click();
    await page.waitForURL(/\/store\//, { timeout: 15000 });
    reporter.log('home store section → "Vào cửa hàng" navigates to storefront');

    // 3) /my-loyalty → back chip hiện (kể cả guest chưa login) → bấm về storefront.
    await page.goto(`${khachLinkBase}/my-loyalty`);
    await page.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => {});
    const chip = page.locator('[data-testid="back-to-store"]');
    await expect(chip).toBeVisible({ timeout: 45000 });
    await chip.locator('button').click();
    await page.waitForURL(/\/store\//, { timeout: 15000 });
    reporter.log('loyalty page back-chip → navigates to storefront');

    // 4) /my-orders → chip cũng hiện.
    await page.goto(`${khachLinkBase}/my-orders`);
    await page.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => {});
    await expect(page.locator('[data-testid="back-to-store"]')).toBeVisible({ timeout: 45000 });

    await context.close();
  });

  test('UI: order-tracking hiện nút "Tiếp tục mua" → storefront', async ({ browser }) => {
    const orderId = process.env.STORE_CONTEXT_ORDER_ID;
    if (!orderId) {
      test.skip(true, 'STORE_CONTEXT_ORDER_ID chưa set — cần order id thật của tenant (RV setup)');
      return;
    }

    const context = await browser.newContext();
    const page = await context.newPage();
    await page.goto(`${khachLinkBase}/order-tracking/${orderId}`);
    await page.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => {});

    const btn = page.locator('[data-testid="continue-shopping"]');
    await expect(btn).toBeVisible({ timeout: 45000 });
    await btn.locator('button').click();
    await page.waitForURL(/\/store\//, { timeout: 15000 });
    reporter.log('order-tracking "Tiếp tục mua" → navigates to storefront');
    await context.close();
  });
});
