import { test, expect } from '@playwright/test';
import { loadEnvConfig } from '../utils/env-config';

// Task W2-2 (Gate 4 rules — UI layout change → E2E bắt buộc).
// Hoptacxa portal landing (Profile=Hoptacxa): hero + module cards +
// "Các hợp tác xã" + "Cửa hàng thành viên" sections, mobile 375px.
// Env: HOPTACXA_URL (default https://hoptacxa.timlathay.com).

const config = loadEnvConfig();
const BASE = process.env.HOPTACXA_URL || 'https://hoptacxa.timlathay.com';

test.use({
  baseURL: BASE,
  storageState: { cookies: [], origins: [] }, // guest — portal là public surface
  viewport: { width: 375, height: 812 },
});

test.describe('Hoptacxa portal landing (W2-2) @mobile', () => {
  test('hero + module cards + sections render trên 375px', async ({ page }) => {
    await page.goto('/', { waitUntil: 'networkidle' });

    // Portal landing thay commerce home
    await expect(page.locator('.hoptacxa-portal')).toBeVisible({ timeout: 30000 });
    await expect(page.locator('.htx-hero-title')).toContainText('Hợp tác xã');

    // Module cards: 4 ô (Cơm từ thiện, Cộng tác viên, Mua chung, Sàn việc)
    const modules = page.locator('.htx-module-card');
    await expect(modules).toHaveCount(4);
    await expect(page.locator('.htx-module-card', { hasText: 'Cơm từ thiện' })).toBeVisible();
    await expect(page.locator('.htx-module-card', { hasText: 'Sắp ra mắt' }).first()).toBeVisible();

    // Sections
    await expect(page.locator('#htx-coops')).toBeVisible();
    await expect(page.locator('.htx-section-title', { hasText: 'Các hợp tác xã' })).toBeVisible();

    // Không overflow ngang ở 375px
    const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
    expect(scrollWidth).toBeLessThanOrEqual(376);
  });

  test('member cards (nếu có data) mang data-tenant-id cho W3 AI attach', async ({ page }) => {
    await page.goto('/', { waitUntil: 'networkidle' });
    await expect(page.locator('.hoptacxa-portal')).toBeVisible({ timeout: 30000 });

    // Đợi cooperatives API resolve (hoặc empty state)
    await page.waitForResponse(
      (r) => r.url().includes('/api/cooperatives/'),
      { timeout: 30000 }
    ).catch(() => null);

    const memberCards = page.locator('.htx-member-card');
    const count = await memberCards.count();
    if (count === 0) {
      test.skip(true, 'Chưa có HTX/member data trên prod — empty state hợp lệ');
    }
    // Mỗi member card phải có tenant id (W3 contextual attach contract)
    for (let i = 0; i < count; i++) {
      const tid = await memberCards.nth(i).getAttribute('data-tenant-id');
      expect(tid).toBeTruthy();
    }
  });
});
