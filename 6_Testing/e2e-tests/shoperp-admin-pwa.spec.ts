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

  test('mobile nav: tap group keeps drawer open, tap leaf navigates', async ({ page }) => {
    // Regression: tap <summary> group trên mobile từng đóng luôn drawer
    // (nav-scrollable onclick bắn toggler cho MỌI click, kể cả group header).
    // Fix: chỉ auto-close khi click trúng <a> leaf link.
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/sitemap');
    await page.waitForLoadState('networkidle');

    const toggler = page.locator('input.navbar-toggler');
    // Mở drawer trên mobile (hamburger = checkbox)
    await toggler.check({ force: true });
    await expect(page.locator('.nav-scrollable')).toBeVisible();

    // Bấm group header → drawer PHẢI còn mở (toggler vẫn checked)
    const summary = page.locator('.vanan-navigation__summary').first();
    await summary.click();
    await page.waitForTimeout(300);
    await expect(toggler).toBeChecked();
    await expect(page.locator('.nav-scrollable')).toBeVisible();

    // Bấm leaf link trong group vừa bung → navigate + drawer đóng
    const leaf = summary.locator('..').locator('.vanan-navigation__submenu a').first();
    if (await leaf.count() > 0 && await leaf.isVisible()) {
      await leaf.click();
      await page.waitForTimeout(500);
      await expect(toggler).not.toBeChecked();
    }
  });
});
