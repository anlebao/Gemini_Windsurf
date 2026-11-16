import { test, expect } from '@playwright/test';

// ATRS-11 subset (Wave 2) — Gate 4: UI layout change → E2E bắt buộc.
// Security center /security (U06 status + U03 passkey + U05 recovery codes + U07/U08 devices).
// 2 modes:
//  - Guest: không có customer_token → login prompt.
//  - Authenticated: seed SECURITY_CUSTOMER_TOKEN vào localStorage trước WASM boot →
//    status card + sections render từ vanan-atrs (qua Gateway YARP /api/v1/account-trust/*).
// Env: KHACHLINK_URL (default https://vanan.timlathay.com), SECURITY_CUSTOMER_TOKEN.
// Nếu không có token → chỉ chạy guest test (authenticated tests tự skip — không fail CI).

const BASE = process.env.KHACHLINK_URL || 'https://vanan.timlathay.com';
const CUSTOMER_TOKEN = process.env.SECURITY_CUSTOMER_TOKEN || '';

test.use({
  baseURL: BASE,
  viewport: { width: 375, height: 812 },
});

test.describe('Security center (ATRS-11 subset) @mobile', () => {
  test('guest → login prompt', async ({ page }) => {
    await page.addInitScript(() => { try { localStorage.clear(); } catch { /* noop */ } });
    await page.goto('/security', { waitUntil: 'networkidle' });

    await expect(page.locator('h3', { hasText: 'Bảo mật tài khoản' })).toBeVisible({ timeout: 40000 });
    // Guest: prompt đăng nhập
    const loginCard = page.locator('text=Đăng nhập để xem bảo mật tài khoản');
    await expect(loginCard).toBeVisible({ timeout: 20000 });
  });

  test.describe('authenticated', () => {
    test.skip(!CUSTOMER_TOKEN, 'SECURITY_CUSTOMER_TOKEN not set — post-deploy RV only');

    test('status card + passkey + recovery codes + devices render', async ({ page }) => {
      await page.addInitScript((token) => {
        try {
          localStorage.clear();
          if (token) localStorage.setItem('customer_token', token);
        } catch { /* noop */ }
      }, CUSTOMER_TOKEN);

      await page.goto('/security', { waitUntil: 'networkidle' });

      // U06 status card
      await expect(page.locator('h3', { hasText: 'Bảo mật tài khoản' })).toBeVisible({ timeout: 40000 });
      await expect(page.locator('text=Trạng thái bảo vệ')).toBeVisible({ timeout: 30000 });

      // Passkey section (U03)
      await expect(page.locator('text=Passkey')).toBeVisible();
      // Recovery codes section (U05)
      await expect(page.locator('text=Mã khôi phục')).toBeVisible();
      // Devices section (U07)
      await expect(page.locator('text=Thiết bị')).toBeVisible();

      // Không overflow ngang 375px
      const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
      expect(scrollWidth).toBeLessThanOrEqual(376);
    });

    test('profile → security entry điều hướng /security', async ({ page }) => {
      await page.addInitScript((token) => {
        try {
          localStorage.clear();
          if (token) localStorage.setItem('customer_token', token);
        } catch { /* noop */ }
      }, CUSTOMER_TOKEN);

      await page.goto('/profile', { waitUntil: 'networkidle' });
      const entry = page.locator('a', { hasText: 'Bảo mật tài khoản' }).first();
      await expect(entry).toBeVisible({ timeout: 40000 });
      await entry.click();
      await page.waitForURL('**/security', { timeout: 15000 });
    });
  });
});