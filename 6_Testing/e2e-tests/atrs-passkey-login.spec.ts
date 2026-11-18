import { test, expect } from '@playwright/test';

// ATRS-21 (Gate 4): Login.razor thêm passkey login/register buttons.
// 2 tests:
//  1. /login render — nút passkey hiện khi browser hỗ trợ WebAuthn (headless Chromium có API).
//  2. Full ceremony (RV-only, cần backend thật): guest register passkey qua CDP virtual
//     authenticator → auto-login → customer_token trong localStorage → /profile.
//     Skip khi không set PASSKEY_LOGIN_RV=1 (post-deploy RV).
// Env: KHACHLINK_URL (default https://vanan.timlathay.com).

const BASE = process.env.KHACHLINK_URL || 'https://vanan.timlathay.com';
const RV = process.env.PASSKEY_LOGIN_RV === '1';

test.use({
  baseURL: BASE,
  viewport: { width: 375, height: 812 },
});

test.describe('Passkey login (ATRS-21) @mobile', () => {
  test('login page shows passkey button when WebAuthn supported', async ({ page }) => {
    await page.addInitScript(() => { try { localStorage.clear(); } catch { /* noop */ } });
    await page.goto('/login', { waitUntil: 'networkidle' });

    await expect(page.locator('h3', { hasText: 'Đăng nhập' })).toBeVisible({ timeout: 40000 });
    // Headless Chromium expose window.PublicKeyCredential → isSupported() = true.
    const supported = await page.evaluate(() => !!(window as any).PublicKeyCredential && !!navigator.credentials);
    if (supported) {
      await expect(page.getByTestId('btn-passkey-login')).toBeVisible({ timeout: 20000 });
      await expect(page.getByTestId('btn-passkey-register')).toBeVisible();
    } else {
      // Browser không hỗ trợ → button phải ẩn (không fail — capability-dependent).
      await expect(page.getByTestId('btn-passkey-login')).toHaveCount(0);
    }
  });

  test.describe('full ceremony (RV)', () => {
    test.skip(!RV, 'PASSKEY_LOGIN_RV=1 required — post-deploy runtime verification only');

    test('guest registers passkey → auto-login → customer_token stored', async ({ page, context }) => {
      const consoleErrors: string[] = [];
      page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
      page.on('pageerror', e => consoleErrors.push(String(e)));

      // CDP virtual authenticator — resident key + user verification (pattern atrs-security-probe.mjs)
      const cdp = await context.newCDPSession(page);
      await cdp.send('WebAuthn.enable');
      await cdp.send('WebAuthn.addVirtualAuthenticator', {
        options: {
          protocol: 'ctap2', ctap2Version: 'ctap2_1', transport: 'internal',
          hasResidentKey: true, hasUserVerification: true, isUserVerified: true,
          automaticPresenceSimulation: true,
        },
      });

      await page.addInitScript(() => { try { localStorage.clear(); } catch { /* noop */ } });
      await page.goto('/login', { waitUntil: 'networkidle' });
      await expect(page.getByTestId('btn-passkey-register')).toBeVisible({ timeout: 40000 });

      // Guest registration → auto login → /profile
      await page.getByTestId('btn-passkey-register').click();

      // Chờ redirect /profile hoặc token xuất hiện (ceremony + 2 API calls có thể ~10-20s)
      await page.waitForURL(/\/profile/, { timeout: 60000 });
      const token = await page.evaluate(() => localStorage.getItem('customer_token'));
      expect(token, 'customer_token phải được lưu sau passkey login').toBeTruthy();
      const provider = await page.evaluate(() => localStorage.getItem('login_provider'));
      expect(provider).toBe('passkey');
    });
  });
});
