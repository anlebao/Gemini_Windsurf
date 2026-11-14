import { test, expect } from '@playwright/test';

// Task W2-4 (Gate 4 rules — UI layout change → E2E bắt buộc).
// KhachLink Home retention: new-user hero strip (khi không có _lastTenantId)
// + retention teasers + empty-state CTAs. Regression: user có lastTenant
// không thấy hero (hero chỉ render sau khi lastTenant resolve = none).
// Env: KHACHLINK_URL (default https://diemthuong2.khachvip.online).

const BASE = process.env.KHACHLINK_URL || 'https://diemthuong2.khachvip.online';

test.use({
  baseURL: BASE,
  storageState: { cookies: [], origins: [] }, // guest + localStorage sạch = new user
  viewport: { width: 375, height: 812 },
});

test.describe('KhachLink home retention (W2-4) @mobile', () => {
  test('new-user hero strip + retention teasers render', async ({ page }) => {
    // Đảm bảo không có lastTenant → hero strip hiện
    await page.addInitScript(() => {
      try { localStorage.clear(); } catch { /* noop */ }
    });
    await page.goto('/', { waitUntil: 'networkidle' });

    // Hero strip (chỉ hiện khi lastTenant resolved = none)
    const hero = page.locator('.new-user-hero');
    await expect(hero).toBeVisible({ timeout: 40000 });
    await expect(hero.getByText('Chào mừng đến KhachLink')).toBeVisible();

    // Retention teasers: 4 cards
    const teasers = page.locator('.retention-teasers .vanan-card, .retention-teasers [class*="card"]').first();
    await expect(page.locator('.retention-teasers')).toBeVisible();

    // CTA chính trong hero
    await expect(hero.locator('button', { hasText: 'Quét QR ngay' })).toBeVisible();

    // Không overflow ngang 375px
    const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
    expect(scrollWidth).toBeLessThanOrEqual(376);
  });

  test('teaser "Đăng nhập để tích điểm" điều hướng /login', async ({ page }) => {
    await page.addInitScript(() => { try { localStorage.clear(); } catch { /* noop */ } });
    await page.goto('/', { waitUntil: 'networkidle' });
    await expect(page.locator('.retention-teasers')).toBeVisible({ timeout: 40000 });

    await page.locator('.retention-teasers button', { hasText: 'Đăng nhập để tích điểm' }).click();
    await page.waitForURL('**/login**', { timeout: 15000 });
  });
});
