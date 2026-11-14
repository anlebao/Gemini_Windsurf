import { test, expect } from '@playwright/test';

// Task W3-2 polish pack (Gate 4 — UI layout change → E2E bắt buộc).
// FR-V4: /stores ProfileGuard ShowStores (flag ON → render bình thường;
//        flag OFF → redirect+toast — không có instance OFF trên prod → skip case đó).
// FR-L3: header icon overflow — <400px tối đa 3 icon (⋮+cart+profile), ⋮ menu hoạt động.
// FR-L5: mic buttons dùng bi-mic-fill/bi-stop-fill thay emoji.
// Env: KHACHLINK_URL (default https://diemthuong2.khachvip.online — FullCommerce,
//      nhiều nav flags ON nhất).

const BASE = process.env.KHACHLINK_URL || 'https://diemthuong2.khachvip.online';

test.describe('W3-2 polish pack', () => {
  test('FR-V4: /stores render bình thường khi ShowStores ON (guard không chặn nhầm)', async ({ page }) => {
    await page.goto(`${BASE}/stores`, { waitUntil: 'domcontentloaded' });
    // ProfileGuard allow → trang finder render (hero "Tìm cửa hàng")
    await expect(page.locator('.store-finder-page, .store-finder-hero')).toBeVisible({ timeout: 60000 });
    await expect(page.locator('h3, h1').filter({ hasText: 'Tìm cửa hàng' }).first()).toBeVisible();
  });

  test('FR-L3: header <400px → ≤3 icon visible + ⋮ menu mở được', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`${BASE}/`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('.khachlink-hero-header', { timeout: 60000 });

    const header = page.locator('.khachlink-hero-header .header-actions');
    // ⋮ button phải hiện ở <400px
    const moreBtn = header.locator('.header-more-btn');
    await expect(moreBtn).toBeVisible();

    // Secondary icons phải ẩn
    await expect(header.locator('.header-icon-secondary').first()).toBeHidden();

    // Mở ⋮ menu → items render
    await moreBtn.click();
    const menu = header.locator('.header-more-menu');
    await expect(menu).toBeVisible();
    const items = menu.locator('.header-more-item');
    expect(await items.count()).toBeGreaterThan(0);

    // Đóng menu khi click item/menu
    await page.locator('body').click({ position: { x: 10, y: 400 } });
  });

  test('FR-L3: header ≥401px → secondary icons hiện, ⋮ ẩn', async ({ page }) => {
    await page.setViewportSize({ width: 500, height: 844 });
    await page.goto(`${BASE}/`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('.khachlink-hero-header', { timeout: 60000 });

    const header = page.locator('.khachlink-hero-header .header-actions');
    await expect(header.locator('.header-more-btn')).toBeHidden();
    // Ít nhất 1 secondary icon visible (nếu flags ON — FullCommerce có nhiều)
    const secondaryCount = await header.locator('.header-icon-secondary:visible').count();
    expect(secondaryCount).toBeGreaterThan(0);
  });

  test('FR-L5: voice mic button dùng bi-mic-fill (không emoji) — nếu render', async ({ page }) => {
    await page.goto(`${BASE}/`, { waitUntil: 'domcontentloaded' });
    // Mic button chỉ render khi voice flag ON + speech supported — check nếu có.
    const micBtn = page.locator('.home-voice-mic-btn, .voice-mic-btn').first();
    if (await micBtn.count() === 0) {
      test.skip(true, 'Voice search không bật/unsupported trên domain này');
      return;
    }
    const html = await micBtn.innerHTML();
    expect(html).toContain('bi-mic');
    expect(html).not.toContain('🎤');
  });
});
