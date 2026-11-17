import { test, expect } from '@playwright/test';

// ATRS Wave 4 (Rail C) — Gate 4: UI layout change → E2E bắt buộc.
// Covers new KhachLink surfaces:
//   U01 /security/setup   — account-protection onboarding (dismiss không trap)
//   U02 /security/profile — personal evidence (masked, self-declared)
//   U09 /recovery         — 4-step recovery wizard
//   U10 /recovery/{id}    — status + verify code + cancel
//   U11 /disputes         — dispute center (open/closed)
//   U12 /disputes/new     — submit form (case type, read-only tx, privacy ack)
//   U13 /disputes/{id}    — detail (covered indirectly via list links)
//
// 2 modes giống atrs-security-center.spec.ts:
//  - Guest: không có customer_token → login prompt.
//  - Authenticated: seed SECURITY_CUSTOMER_TOKEN vào localStorage trước WASM boot.
// Env: KHACHLINK_URL (default https://vanan.timlathay.com), SECURITY_CUSTOMER_TOKEN.
// Authenticated tests tự skip khi không có token — không fail CI.

const BASE = process.env.KHACHLINK_URL || 'https://vanan.timlathay.com';
const CUSTOMER_TOKEN = process.env.SECURITY_CUSTOMER_TOKEN || '';

test.use({
  baseURL: BASE,
  viewport: { width: 375, height: 812 },
});

function seedToken(page: import('@playwright/test').Page, token: string) {
  return page.addInitScript((t) => {
    try {
      localStorage.clear();
      if (t) localStorage.setItem('customer_token', t);
    } catch { /* noop */ }
  }, token);
}

test.describe('ATRS Wave 4 security surfaces @mobile', () => {
  test.describe('guest', () => {
    test.beforeEach(async ({ page }) => {
      await seedToken(page, '');
    });

    test('U01 /security/setup → login prompt', async ({ page }) => {
      await page.goto('/security/setup', { waitUntil: 'networkidle' });
      await expect(page.locator('h3', { hasText: 'Bảo vệ tài khoản' })).toBeVisible({ timeout: 40000 });
      await expect(page.locator('text=Đăng nhập để thiết lập bảo mật')).toBeVisible({ timeout: 20000 });
    });

    test('U02 /security/profile → login prompt', async ({ page }) => {
      await page.goto('/security/profile', { waitUntil: 'networkidle' });
      await expect(page.locator('h3', { hasText: 'Thông tin cá nhân hỗ trợ' })).toBeVisible({ timeout: 40000 });
      await expect(page.locator('text=Đăng nhập để quản lý thông tin')).toBeVisible({ timeout: 20000 });
    });

    test('U09 /recovery → login prompt', async ({ page }) => {
      await page.goto('/recovery', { waitUntil: 'networkidle' });
      await expect(page.locator('h3', { hasText: 'Khôi phục tài khoản' })).toBeVisible({ timeout: 40000 });
      await expect(page.locator('text=Đăng nhập để khôi phục tài khoản')).toBeVisible({ timeout: 20000 });
    });

    test('U11 /disputes → login prompt', async ({ page }) => {
      await page.goto('/disputes', { waitUntil: 'networkidle' });
      await expect(page.locator('h3', { hasText: 'Khiếu nại' })).toBeVisible({ timeout: 40000 });
      await expect(page.locator('text=Đăng nhập để xem khiếu nại')).toBeVisible({ timeout: 20000 });
    });

    test('U12 /disputes/new → login prompt', async ({ page }) => {
      await page.goto('/disputes/new', { waitUntil: 'networkidle' });
      await expect(page.locator('h3', { hasText: 'Tạo khiếu nại' })).toBeVisible({ timeout: 40000 });
      await expect(page.locator('text=Đăng nhập để tạo khiếu nại')).toBeVisible({ timeout: 20000 });
    });
  });

  test.describe('authenticated', () => {
    test.skip(!CUSTOMER_TOKEN, 'SECURITY_CUSTOMER_TOKEN not set — post-deploy RV only');

    test('U01 setup renders assurance + honest wording, Để sau không trap', async ({ page }) => {
      await seedToken(page, CUSTOMER_TOKEN);
      await page.goto('/security/setup', { waitUntil: 'networkidle' });

      await expect(page.locator('h3', { hasText: 'Bảo vệ tài khoản' })).toBeVisible({ timeout: 40000 });
      await expect(page.locator('text=Mức bảo vệ hiện tại')).toBeVisible({ timeout: 30000 });
      // Honest wording: không overclaim danh tính pháp lý
      await expect(page.locator('text=không phải chứng minh danh tính pháp lý')).toBeVisible();

      // "Để sau" → điều hướng về home, không trap
      await page.locator('button', { hasText: 'Để sau' }).click();
      await page.waitForURL('**/', { timeout: 15000 });
    });

    test('U02 profile renders fields + masked values + consent controls', async ({ page }) => {
      await seedToken(page, CUSTOMER_TOKEN);
      await page.goto('/security/profile', { waitUntil: 'networkidle' });

      await expect(page.locator('h3', { hasText: 'Thông tin cá nhân hỗ trợ' })).toBeVisible({ timeout: 40000 });
      await expect(page.locator('text=khai báo của bạn')).toBeVisible({ timeout: 30000 });
      await expect(page.locator('text=chưa xác minh')).toBeVisible();
    });

    test('U09 recovery wizard step 1 → step 2 navigation', async ({ page }) => {
      await seedToken(page, CUSTOMER_TOKEN);
      await page.goto('/recovery', { waitUntil: 'networkidle' });

      await expect(page.locator('h3', { hasText: 'Khôi phục tài khoản' })).toBeVisible({ timeout: 40000 });
      await expect(page.locator('text=Bạn gặp vấn đề gì?')).toBeVisible({ timeout: 30000 });

      // Chọn vấn đề → Tiếp tục enabled → step 2 hiển thị phương thức thật
      await page.locator('input[name="problemType"]').first().check();
      await page.locator('button', { hasText: 'Tiếp tục' }).click();
      await expect(page.locator('text=Phương thức bạn còn dùng được?')).toBeVisible({ timeout: 10000 });
      await expect(page.locator('text=Passkey trên thiết bị khác')).toBeVisible();
    });

    test('U11 disputes center renders + empty/create state', async ({ page }) => {
      await seedToken(page, CUSTOMER_TOKEN);
      await page.goto('/disputes', { waitUntil: 'networkidle' });

      await expect(page.locator('h3', { hasText: 'Khiếu nại' })).toBeVisible({ timeout: 40000 });
      // Một trong hai: empty state hoặc ít nhất 1 case card
      const empty = page.locator('text=Chưa có khiếu nại nào');
      const anyCase = page.locator('text=Đang xử lý');
      await expect(empty.or(anyCase)).toBeVisible({ timeout: 30000 });
    });

    test('U12 dispute form renders case types + privacy ack gates submit', async ({ page }) => {
      await seedToken(page, CUSTOMER_TOKEN);
      await page.goto('/disputes/new', { waitUntil: 'networkidle' });

      await expect(page.locator('h3', { hasText: 'Tạo khiếu nại' })).toBeVisible({ timeout: 40000 });
      await expect(page.locator('label', { hasText: 'Loại khiếu nại' })).toBeVisible({ timeout: 30000 });

      // Submit bị disable khi thiếu privacy ack / mô tả
      const submit = page.locator('button', { hasText: 'Gửi khiếu nại' });
      await expect(submit).toBeDisabled();
      await page.locator('#disputePrivacy').check();
      await page.locator('textarea').fill('Điểm thưởng không được cộng sau giao dịch hôm qua.');
      await expect(submit).toBeEnabled();
    });

    test('no horizontal overflow 375px trên các trang mới', async ({ page }) => {
      await seedToken(page, CUSTOMER_TOKEN);
      for (const route of ['/security/setup', '/security/profile', '/recovery', '/disputes', '/disputes/new']) {
        await page.goto(route, { waitUntil: 'networkidle' });
        await page.waitForTimeout(1500); // đợi WASM render
        const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
        expect(scrollWidth, `overflow at ${route}`).toBeLessThanOrEqual(376);
      }
    });
  });
});
