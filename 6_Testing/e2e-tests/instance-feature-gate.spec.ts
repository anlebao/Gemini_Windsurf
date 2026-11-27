import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// Instance Feature Gate (2026-11-27, Gate 4): SysAdmin NavFlags (KhachLinkInstance)
// kill-switch AND onto tenant ShopFeatureSettings. KhachLink WASM gửi X-KhachLink-Domain
// (hostname) trên mọi request → server resolve portal → effective flags.
//
// Test dùng extraHTTPHeaders để giả lập request từ portal shopai.timlathay.com
// (instance prod có ShowCampaigns/ShowScan/ShowQrClaim/ShowMissions = false).
// Tenant dùng để verify: An Tâm (Campaign_Section_Enabled=true trên PG).

const config = loadEnvConfig();
const reporter = new TestReporter('Instance Feature Gate E2E');
const apiBase = process.env.GATEWAY_URL || config.GATEWAY_URL || 'https://api.khachvip.online';
const khachLinkBase = config.KHACHLINK_URL;

const ANTAM_TENANT = 'e2d91cdf-d3c4-4399-b96e-e772a7a41630';
const SHOPAI_DOMAIN = 'shopai.timlathay.com';

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VanAn Ecosystem - Instance Feature Gate (NavFlags override)', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting Instance Feature Gate E2E...');
  });

  test('API: feature-settings trả effective flags theo X-KhachLink-Domain', async ({ request }) => {
    // Baseline: không header → tenant flag nguyên bản (An Tâm campaign=true).
    const plain = await request.get(`${apiBase}/api/tenants/${ANTAM_TENANT}/feature-settings`, { timeout: 15000 });
    expect(plain.ok()).toBeTruthy();
    const base = await plain.json();
    expect(base.campaign_Section_Enabled ?? base.Campaign_Section_Enabled).toBeTruthy();

    // Với header shopai (ShowCampaigns=false) → kill-switch → campaign off.
    const gated = await request.get(`${apiBase}/api/tenants/${ANTAM_TENANT}/feature-settings`, {
      headers: { 'X-KhachLink-Domain': SHOPAI_DOMAIN }, timeout: 15000,
    });
    expect(gated.ok()).toBeTruthy();
    const eff = await gated.json();
    expect(eff.campaign_Section_Enabled ?? eff.Campaign_Section_Enabled).toBeFalsy();
    expect(eff.home_CampaignSection_Enabled ?? eff.Home_CampaignSection_Enabled).toBeFalsy();
    // Unmapped flag không đổi.
    expect(eff.vibeShowcase_Section_Enabled ?? eff.VibeShowcase_Section_Enabled).toBeTruthy();
    reporter.log('feature-settings: campaign off under shopai domain, unchanged without header');
  });

  test('API: campaigns by-tenant → rỗng khi portal ShowCampaigns=false', async ({ request }) => {
    const gated = await request.get(`${apiBase}/api/campaigns/by-tenant/${ANTAM_TENANT}`, {
      headers: { 'X-KhachLink-Domain': SHOPAI_DOMAIN }, timeout: 15000,
    });
    expect(gated.ok()).toBeTruthy();
    const list = await gated.json();
    expect(Array.isArray(list) ? list.length : (list.items?.length ?? -1)).toBe(0);
    reporter.log('campaigns by-tenant: empty under shopai domain');
  });

  test('UI: storefront trên portal ShowCampaigns=false không hiện section khuyến mãi', async ({ browser }) => {
    const storeUrl = process.env.GATE_TEST_STORE_URL;
    if (!storeUrl) {
      test.skip(true, 'GATE_TEST_STORE_URL chưa set — cần storefront có campaign section bật (RV setup)');
      return;
    }

    const context = await browser.newContext({
      extraHTTPHeaders: { 'X-KhachLink-Domain': SHOPAI_DOMAIN },
    });
    const page = await context.newPage();
    await page.goto(storeUrl);
    await page.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => {});

    // Section campaigns phải vắng (feature-settings effective=false → không render).
    await expect(page.locator('#campaigns')).toHaveCount(0, { timeout: 20000 });
    // Booking QR cũng ẩn khi ShowQrClaim=false — soft check (chỉ khi booking enabled).
    const bookingQr = page.locator('[data-testid="store-booking-qr"]');
    if (await bookingQr.count() > 0) {
      await expect(bookingQr).toHaveCount(0);
    }
    reporter.log('storefront: campaign section + booking QR hidden under gated domain');
    await context.close();
  });
});
