import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// Profile UX upgrade (task_profile_ux_upgrade, Gate 4 — UI layout change):
//  A) Loyalty Hero — big points + progress bar to next tier + tier ring + CTAs
//     "Đổi điểm"→/rewards, "Lịch sử"→/my-loyalty (data /api/loyalty/my fail-open).
//  B) Zone 4 cleanup — fraud flags table + raw device token REMOVED (security
//     leak), duplicate upgrade CTA removed (single source = ctx identity-upgrade).
//  C) Quick-access grouped "Mua sắm"/"Tài khoản", referral card (SalesmanCode),
//     guest BackToStoreChip, skeleton loading (profile-skel-card shimmer).
//
// Env: PROFILE_CUSTOMER_TOKEN (optional) — authenticated assertions auto-skip.

const config = loadEnvConfig();
const reporter = new TestReporter('Profile UX Upgrade E2E');
const khachLinkBase = config.KHACHLINK_URL;
const CUSTOMER_TOKEN = process.env.PROFILE_CUSTOMER_TOKEN || '';

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VanAn Ecosystem - Profile UX Upgrade', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting Profile UX E2E...');
  });

  test('UI guest: skeleton → guest card + guest path KHÔNG hiện fraud flags', async ({ browser }) => {
    const context = await browser.newContext();
    const page = await context.newPage();
    await page.addInitScript(() => {
      try {
        localStorage.clear();
        localStorage.setItem('customer_device_id', crypto.randomUUID());
      } catch { /* noop */ }
    });
    await page.goto(`${khachLinkBase}/profile`, { waitUntil: 'networkidle' });

    // Skeleton flash (fail-open — nếu quá nhanh thì chỉ assert guest card)
    await expect(page.locator('text=Chưa đăng nhập')).toBeVisible({ timeout: 40000 });
    await expect(page.locator('text=Fraud Flags')).toHaveCount(0);
    await context.close();
  });

  test.describe('authenticated (PROFILE_CUSTOMER_TOKEN)', () => {
    test.skip(!CUSTOMER_TOKEN, 'PROFILE_CUSTOMER_TOKEN not set — post-deploy RV only');

    async function openProfile(browser: import('@playwright/test').Browser) {
      const context = await browser.newContext();
      const page = await context.newPage();
      await page.addInitScript((token) => {
        try {
          localStorage.clear();
          localStorage.setItem('customer_token', token);
        } catch { /* noop */ }
      }, CUSTOMER_TOKEN);
      await page.goto(`${khachLinkBase}/profile`, { waitUntil: 'networkidle' });
      await page.waitForTimeout(2500); // hero + context cards load sau profile
      return { context, page };
    }

    test('Loyalty hero: points lớn + CTAs "Đổi điểm"→/rewards', async ({ browser }) => {
      const { context, page } = await openProfile(browser);
      const hero = page.locator('[data-testid="loyalty-hero"]');
      await expect(hero).toBeVisible({ timeout: 40000 });
      await expect(page.locator('[data-testid="hero-points"]')).toBeVisible();
      // CTA Đổi điểm → /rewards same-origin
      await page.locator('[data-testid="hero-redeem"]').click();
      await page.waitForURL(/\/rewards/, { timeout: 15000 });
      reporter.log(`hero: redeem → ${page.url()}`);
      await context.close();
    });

    test('Zone 3 grouping: headers "Mua sắm" + "Tài khoản" hiện', async ({ browser }) => {
      const { context, page } = await openProfile(browser);
      await expect(page.locator('[data-testid="loyalty-hero"]')).toBeVisible({ timeout: 40000 });
      await expect(page.locator('h6:has-text("Mua sắm")')).toBeVisible();
      await expect(page.locator('h6:has-text("Tài khoản")')).toBeVisible();
      await context.close();
    });

    test('Zone 4 cleanup: KHÔNG fraud flags, KHÔNG raw device token, KHÔNG duplicate upgrade', async ({ browser }) => {
      const { context, page } = await openProfile(browser);
      await page.locator('[data-testid="settings-toggle"]').click();
      await expect(page.locator('text=Fraud Flags')).toHaveCount(0);
      await expect(page.locator('text=Device Token:')).toHaveCount(0);
      await expect(page.locator('[data-testid="btn-upgrade-identity"]')).toHaveCount(0);
      // Device card vẫn hiện nhưng chỉ "đã đăng ký" (không lộ token)
      await expect(page.locator('text=Thiết bị đang sử dụng')).toBeVisible();
      reporter.log('zone4: fraud flags + raw token + dup upgrade removed');
      await context.close();
    });
  });
});
