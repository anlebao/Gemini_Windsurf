import { test, expect } from '@playwright/test';

// Directory (timlathay.com) PWA install entry points:
//  - Header icon "Cài đặt ứng dụng" (DirectoryLayout — always visible, iOS-safe)
//  - Floating #vanan-dir-install-btn (Android only, after ~2.5s)
// Both delegate to window.vananDirInstall / directory-pwa.js install flow.
const BASE_URL = process.env.DIRECTORY_URL ?? 'https://timlathay.com';

test.describe('directory pwa install', () => {
  test.use({
    baseURL: BASE_URL,
    userAgent:
      'Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36',
    viewport: { width: 412, height: 915 },
    isMobile: true,
    hasTouch: true,
  });

  test('header install button is visible and clickable', async ({ page }) => {
    const pageErrors: string[] = [];
    page.on('pageerror', (e) => pageErrors.push(String(e)));

    await page.goto('/', { waitUntil: 'domcontentloaded' });

    const headerBtn = page.locator('button[aria-label="Cài đặt ứng dụng"]').first();
    await expect(headerBtn).toBeVisible({ timeout: 30000 });

    // Tap → directory-pwa.js triggerInstall: toast, iOS hint, or native prompt.
    // We only assert no uncaught page error — outcome varies by browser state.
    await headerBtn.click();
    await page.waitForTimeout(1500);
    expect(pageErrors).toEqual([]);
  });

  test('floating install button appears on Android within 10s', async ({ page }) => {
    await page.goto('/', { waitUntil: 'domcontentloaded' });
    // Floating button is created 2.5s after DOMContentLoaded (non-iOS).
    await expect(page.locator('#vanan-dir-install-btn')).toBeVisible({ timeout: 10000 });
  });
});
