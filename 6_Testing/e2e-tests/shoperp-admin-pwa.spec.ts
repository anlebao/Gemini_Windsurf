import { test, expect } from '@playwright/test';

/**
 * Admin Mobile PWA (SRS van_an_admin_mobile_pwa_srs_v1 — Gate 4).
 * Verify: manifest installable · SW + offline.html tồn tại · role landing
 * (SystemAdmin → /sitemap) · ops pages render trong standalone-ready app.
 * Playwright không cài PWA thật — assert ở mức manifest/SW/route/DOM.
 * Auth: storageState SystemAdmin (rv-metering-admin.json).
 */
test.describe('Admin Mobile PWA — ShopERP', () => {
  test.use({ storageState: 'auth/rv-metering-admin.json' });

  test('manifest + service worker + offline.html served', async ({ page }) => {
    // Manifest — installable requirement
    const manifest = await page.request.get('/manifest.json');
    expect(manifest.ok()).toBeTruthy();
    const body = await manifest.json();
    expect(body.display).toBe('standalone');
    expect(body.short_name).toBe('ShopERP');
    expect(body.icons.length).toBeGreaterThanOrEqual(8);
    expect(body.icons.some((i: any) => (i.purpose || '').includes('maskable'))).toBeTruthy();

    // SW + offline fallback files phải tồn tại
    expect((await page.request.get('/service-worker.js')).ok()).toBeTruthy();
    expect((await page.request.get('/offline.html')).ok()).toBeTruthy();
    // Icon bắt buộc cho install
    expect((await page.request.get('/icons/icon-192x192.png')).ok()).toBeTruthy();
    expect((await page.request.get('/icons/icon-512x512.png')).ok()).toBeTruthy();
  });

  test('home page links manifest + registers SW', async ({ page }) => {
    await page.goto('/sitemap');
    await page.waitForLoadState('networkidle');

    // <link rel="manifest"> trong head
    await expect(page.locator('link[rel="manifest"]')).toHaveCount(1);
    await expect(page.locator('meta[name="theme-color"]')).toHaveCount(1);

    // SW đã register (pwa.js gọi navigator.serviceWorker.register)
    const swReg = await page.evaluate(async () => {
      const reg = await navigator.serviceWorker.getRegistration('/');
      return reg ? reg.active?.scriptURL || reg.installing?.scriptURL || 'registered' : null;
    });
    expect(swReg).toBeTruthy();
  });

  test('SystemAdmin landing /sitemap + ops page renders', async ({ page }) => {
    // Role landing: "/" → SystemAdmin/Owner → /sitemap
    await page.goto('/');
    await page.waitForURL('**/sitemap', { timeout: 15000 });
    await expect(page).toHaveURL(/\/sitemap/);

    // Ops page mẫu render được (kitchen — Masterchef home dùng chung layout)
    await page.goto('/kitchen');
    await page.waitForLoadState('networkidle');
    expect(page.url()).toContain('/kitchen');
  });
});
