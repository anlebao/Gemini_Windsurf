import { test, expect } from '@playwright/test';

// W3-3 Option A (Gate 4 — UI layout change → E2E bắt buộc):
//  Desktop sidebar storefront-grade:
//   1) Brand = tenant ShopName + logo (không còn "Vạn An KhachLink" hardcode)
//   2) Nav group chứa trang active tự expand (details[open])
//   3) Active link có accent bar (--shop-primary)
//   4) Icon-rail 768–1199px: sidebar ~76px, labels ẩn, icons còn
//   5) Mobile <768px: sidebar ẩn, bottom nav hiển thị (không regression)
// Env: KHACHLINK_URL (default https://commienphi.timlathay.com).

const BASE = process.env.KHACHLINK_URL || 'https://commienphi.timlathay.com';
test.describe.configure({ timeout: 150_000 });

test('desktop ≥1200px: brand tenant + active group auto-expand + accent bar', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`${BASE}/stores`, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('.nav-desktop .nav-brand-name', { timeout: 90000 });

  // 1) Brand hiển thị tenant name — KHÔNG phải "Vạn An KhachLink" hardcode
  const brandName = page.locator('.nav-desktop .nav-brand-name');
  await expect(brandName).toBeVisible();
  const text = (await brandName.innerText()).trim();
  expect(text.length).toBeGreaterThan(0);
  expect(text).not.toBe('Vạn An KhachLink');

  // 2) /stores thuộc group "Mua sắm" → details phải có open attribute
  const shoppingGroup = page.locator('.nav-desktop details.nav-group', { hasText: 'Mua sắm' });
  await expect(shoppingGroup).toHaveAttribute('open', /.*/);
  // Link Cửa hàng active
  await expect(page.locator('.nav-desktop a.nav-link.active', { hasText: 'Cửa hàng' })).toBeVisible();

  // 3) Accent bar trên active link (box-shadow inset 3px)
  const shadow = await page.locator('.nav-desktop a.nav-link.active').first()
    .evaluate((el) => getComputedStyle(el).boxShadow);
  expect(shadow).toContain('inset');
});

test('icon-rail 900px: sidebar thu nhỏ, labels ẩn, icons còn', async ({ page }) => {
  await page.setViewportSize({ width: 900, height: 800 });
  await page.goto(`${BASE}/`, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('.nav-desktop', { timeout: 90000 });

  // Sidebar width ~76px (không phải 240px)
  const width = await page.locator('.khachlink-sidebar').evaluate((el) => el.getBoundingClientRect().width);
  expect(width).toBeLessThan(100);
  expect(width).toBeGreaterThan(60);

  // Brand name ẩn, icon/nav icons vẫn hiển thị
  await expect(page.locator('.nav-desktop .nav-brand-name')).toBeHidden();
  const homeIcon = page.locator('.nav-desktop a.nav-link .bi').first();
  await expect(homeIcon).toBeVisible();
});

test('mobile 375px: sidebar ẩn hoàn toàn, bottom nav hiển thị', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await page.goto(`${BASE}/`, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('.mobile-bottom-nav', { timeout: 90000 });

  await expect(page.locator('.nav-desktop')).toBeHidden();
  await expect(page.locator('.mobile-bottom-nav')).toBeVisible();
});
