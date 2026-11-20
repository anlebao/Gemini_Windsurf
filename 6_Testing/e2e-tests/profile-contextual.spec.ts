import { test, expect } from '@playwright/test';

// KhachLink Customer Profile — Contextual UX v1 (Gate 4: UI layout change → E2E required).
// Task card: docs/AI/tasks/khachlink_profile_contextual_ux/task_card.md
//
// Verifies the 4-zone layout on /profile:
//   ZONE 1 header (name/tier/points/identity) — unchanged
//   ZONE 2 "Việc cần làm" — max 3 rule-based context cards ([data-testid^="ctx-card-"])
//   ZONE 3 "Truy cập nhanh" — quick-access grid
//   ZONE 4 "Cài đặt & tài khoản" — collapsed by default, expands on toggle
//
// 2 modes:
//  - Guest: no customer_token → login prompt, no context cards.
//  - Authenticated: seed PROFILE_CUSTOMER_TOKEN → cards ≤3, settings toggle works.
// Env: KHACHLINK_URL (default https://shopai.timlathay.com), PROFILE_CUSTOMER_TOKEN.
// If no token → authenticated tests auto-skip (không fail CI).

const BASE = process.env.KHACHLINK_URL || 'https://shopai.timlathay.com';
const CUSTOMER_TOKEN = process.env.PROFILE_CUSTOMER_TOKEN || '';

test.use({
  baseURL: BASE,
  viewport: { width: 375, height: 812 },
});

test.describe('Profile contextual UX v1 @mobile', () => {
  test('guest → login prompt, no context cards, no settings', async ({ page }) => {
    await page.addInitScript(() => { try { localStorage.clear(); } catch { /* noop */ } });
    await page.goto('/profile', { waitUntil: 'networkidle' });

    await expect(page.locator('text=Chưa đăng nhập')).toBeVisible({ timeout: 40000 });
    await expect(page.locator('button', { hasText: 'Đăng nhập ngay' })).toBeVisible();

    // No context cards / quick access / settings for guests
    await expect(page.locator('[data-testid^="ctx-card-"]')).toHaveCount(0);
    await expect(page.locator('[data-testid="settings-toggle"]')).toHaveCount(0);
  });

  test.describe('authenticated', () => {
    test.skip(!CUSTOMER_TOKEN, 'PROFILE_CUSTOMER_TOKEN not set — post-deploy RV only');

    test.beforeEach(async ({ page }) => {
      await page.addInitScript((token) => {
        try {
          localStorage.clear();
          if (token) localStorage.setItem('customer_token', token);
        } catch { /* noop */ }
      }, CUSTOMER_TOKEN);
      await page.goto('/profile', { waitUntil: 'networkidle' });
      // Wait for profile load (header name or context section)
      await expect(page.locator('h5').first()).toBeVisible({ timeout: 40000 });
    });

    test('context cards: max 3, each has title + reason + CTA', async ({ page }) => {
      const cards = page.locator('[data-testid^="ctx-card-"]');
      const count = await cards.count();
      expect(count).toBeGreaterThanOrEqual(1);
      expect(count).toBeLessThanOrEqual(3);

      for (let i = 0; i < count; i++) {
        const card = cards.nth(i);
        await expect(card.locator('.fw-semibold')).toBeVisible();
        await expect(card.locator('.text-muted.small')).toBeVisible();
        // CTA: dedicated testid (v2 added a dismiss × button before the CTA)
        await expect(card.locator('[data-testid^="ctx-cta-"]')).toBeVisible();
        // v2: every card has a dismiss button (server-side persistence, 7d TTL)
        await expect(card.locator('[data-testid^="ctx-dismiss-"]')).toBeVisible();
      }
    });

    test('quick-access grid renders core destinations', async ({ page }) => {
      await expect(page.locator('text=Truy cập nhanh')).toBeVisible();
      // Buttons may be gated by NavFlags — check the always-on ones + at least 4 total
      const buttons = page.locator('button', { hasText: /Lịch sử tích điểm|Đơn hàng|Nhiệm vụ|Cửa hàng|Bảo mật|Khiếu nại/ });
      expect(await buttons.count()).toBeGreaterThanOrEqual(4);
    });

    test('settings collapsed by default, expands on toggle', async ({ page }) => {
      const settings = page.locator('#profile-settings');
      await expect(settings).toHaveCount(0); // not rendered while collapsed

      await page.locator('[data-testid="settings-toggle"]').click();
      await expect(settings).toBeVisible({ timeout: 10000 });

      // Settings contains push toggle + birthday + logout
      await expect(page.locator('#pushToggle')).toBeVisible();
      await expect(page.locator('input[type="date"]')).toBeVisible();
      await expect(page.locator('button', { hasText: 'Đăng xuất' })).toBeVisible();
    });

    test('context card CTA navigates or opens settings', async ({ page }) => {
      const firstCard = page.locator('[data-testid^="ctx-card-"]').first();
      const cta = firstCard.locator('[data-testid^="ctx-cta-"]').first();
      const href = await cta.getAttribute('href');

      await cta.click();
      if (href && href.startsWith('#')) {
        // Anchor card → settings section opens
        await expect(page.locator('#profile-settings')).toBeVisible({ timeout: 10000 });
      } else {
        // Route card → URL changes away from /profile
        await page.waitForURL((url) => !url.pathname.endsWith('/profile'), { timeout: 15000 });
      }
    });

    test('v2: dismiss button hides card locally', async ({ page }) => {
      const cards = page.locator('[data-testid^="ctx-card-"]');
      const before = await cards.count();
      test.skip(before === 0, 'no context cards to dismiss');

      await page.locator('[data-testid^="ctx-dismiss-"]').first().click();
      await expect(cards).toHaveCount(before - 1, { timeout: 10000 });
    });

    test('no horizontal overflow at 375px', async ({ page }) => {
      const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
      expect(scrollWidth).toBeLessThanOrEqual(376);
    });
  });
});
