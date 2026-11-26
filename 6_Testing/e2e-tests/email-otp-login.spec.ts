import { test, expect } from '@playwright/test';

// 2026-11-26 (Gate 4): Login.razor thêm "Đăng nhập bằng email" — OTP 6 số qua Brevo (0đ).
// Flow: /login → btn-email-otp-login → nhập email → btn-send-otp → màn nhập mã
// (input-otp-code) + nút gửi lại/đổi email.
// Backend: POST /api/customer-identity/otp/send + /otp/verify (ShopERP, anonymous).
// Env: KHACHLINK_URL (default https://vanan.timlathay.com).

const BASE = process.env.KHACHLINK_URL || 'https://vanan.timlathay.com';
const RV = process.env.EMAIL_OTP_RV === '1';

test.use({
  baseURL: BASE,
  viewport: { width: 375, height: 812 },
});

test.describe('Email OTP login @mobile', () => {
  test('login page shows email OTP entry points', async ({ page }) => {
    await page.addInitScript(() => { try { localStorage.clear(); } catch { /* noop */ } });
    await page.goto('/login', { waitUntil: 'networkidle' });

    await expect(page.locator('h3', { hasText: 'Đăng nhập' })).toBeVisible({ timeout: 40000 });
    await expect(page.getByTestId('btn-email-otp-login')).toBeVisible({ timeout: 20000 });
    await expect(page.getByTestId('btn-guest-login')).toBeVisible();
  });

  test('email entry step renders and validates input', async ({ page }) => {
    await page.addInitScript(() => { try { localStorage.clear(); } catch { /* noop */ } });
    await page.goto('/login', { waitUntil: 'networkidle' });
    await expect(page.getByTestId('btn-email-otp-login')).toBeVisible({ timeout: 40000 });

    await page.getByTestId('btn-email-otp-login').click();
    await expect(page.getByTestId('input-otp-email')).toBeVisible();
    await expect(page.getByTestId('btn-send-otp')).toBeVisible();

    // Email không hợp lệ → không gọi API, hiện lỗi inline (không rời trang)
    await page.getByTestId('input-otp-email').fill('not-an-email');
    await page.getByTestId('btn-send-otp').click();
    await expect(page.getByTestId('input-otp-email')).toBeVisible();
  });

  test.describe('send OTP (RV)', () => {
    test.skip(!RV, 'EMAIL_OTP_RV=1 required — post-deploy runtime verification only');

    test('valid email → chuyển sang màn nhập mã', async ({ page }) => {
      const email = process.env.EMAIL_OTP_TEST_EMAIL;
      test.skip(!email, 'EMAIL_OTP_TEST_EMAIL env required');

      await page.addInitScript(() => { try { localStorage.clear(); } catch { /* noop */ } });
      await page.goto('/login', { waitUntil: 'networkidle' });
      await expect(page.getByTestId('btn-email-otp-login')).toBeVisible({ timeout: 40000 });

      await page.getByTestId('btn-email-otp-login').click();
      await page.getByTestId('input-otp-email').fill(email!);
      await page.getByTestId('btn-send-otp').click();

      // Server trả channel + contactSuffix → UI chuyển sang OtpEntry (không assert mã thật —
      // mã gửi qua email thật, không đọc được trong test).
      await expect(page.getByTestId('input-otp-code')).toBeVisible({ timeout: 30000 });
      await expect(page.getByTestId('btn-verify-otp')).toBeVisible();
      await expect(page.getByTestId('btn-resend-otp')).toBeVisible();
    });
  });
});
