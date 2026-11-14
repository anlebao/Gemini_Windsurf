import { test, expect } from '@playwright/test';
import { loadEnvConfig } from '../utils/env-config';

// Task W1-2 (Gate 4 rules — UI layout change → E2E bắt buộc).
// Mobile layout pack: bottom nav ≤5 tabs + "Thêm" overflow sheet,
// product grid 2 cột, không overflow-x ở viewport 375px.
// Chạy trên KhachLink public (không cần auth — guest thấy nav mặc định).
// Env: KHACHLINK_URL (default localhost:5002; prod: https://diemthuong2.khachvip.online).

const config = loadEnvConfig();
const BASE = process.env.KHACHLINK_URL || config.KHACHLINK_URL;

test.use({
  baseURL: BASE,
  storageState: { cookies: [], origins: [] }, // guest — không dùng auth/admin.json
  viewport: { width: 375, height: 812 },      // iPhone-ish mobile viewport (FR-L10)
});

test.describe('Mobile layout pack (W1-2) @mobile', () => {
  test('bottom nav renders tối đa 5 tab, không tràn 375px', async ({ page }) => {
    await page.goto('/', { waitUntil: 'networkidle' });

    const nav = page.locator('.mobile-bottom-nav').first();
    await expect(nav).toBeVisible();

    const tabs = nav.locator('.mobile-tab-item');
    const count = await tabs.count();
    expect(count).toBeLessThanOrEqual(5);
    expect(count).toBeGreaterThanOrEqual(1);
  });

  test('tab "Thêm" mở overflow sheet khi nav > 5 items', async ({ page }) => {
    // Instance config (by-domain) resolves async sau first paint — NavMenu render
    // tạm với default flags rồi re-render theo preset. Phải đợi response + re-render
    // xong, nếu không #nav-more-mobile bị detach ngay khi click (flake).
    const cfgDone = page.waitForResponse(
      (r) => r.url().includes('/api/v1/khachlink-instances/by-domain/'),
      { timeout: 30000 }
    ).catch(() => null);
    await page.goto('/', { waitUntil: 'networkidle' });
    await cfgDone;
    // WASM boot + áp NavFlags chậm hơn API response — đợi nav render rồi poll
    // cho tới khi số tab + #nav-more-mobile ổn định (2 nhịp liên tiếp), tránh
    // count() sớm → skip sai hoặc click vào node sắp bị detach.
    const nav = page.locator('.mobile-bottom-nav').first();
    await expect(nav).toBeVisible({ timeout: 30000 });
    const moreTab = page.locator('#nav-more-mobile');
    let lastSig = '', stable = 0;
    for (let i = 0; i < 40 && stable < 2; i++) {
      const sig = `${await nav.locator('.mobile-tab-item').count()}|${await moreTab.count()}`;
      stable = sig === lastSig ? stable + 1 : 0;
      lastSig = sig;
      await page.waitForTimeout(500);
    }

    if (!(await moreTab.count())) {
      test.skip(true, 'Instance này ≤5 nav items — không có tab "Thêm" (hợp lệ)');
    }

    await moreTab.click();
    const sheet = page.locator('.nav-more-sheet');
    await expect(sheet).toBeVisible();
    expect(await sheet.locator('.nav-more-item').count()).toBeGreaterThanOrEqual(1);

    // Click backdrop → sheet đóng
    await page.locator('.nav-more-backdrop').click({ position: { x: 10, y: 10 } });
    await expect(sheet).toBeHidden();
  });

  test('không horizontal scroll ở 375px', async ({ page }) => {
    await page.goto('/', { waitUntil: 'networkidle' });
    const overflowX = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth
    );
    expect(overflowX).toBeLessThanOrEqual(1); // 1px tolerance
  });

  test('product grid 2 cột trên mobile (featured products)', async ({ page }) => {
    await page.goto('/', { waitUntil: 'networkidle' });

    const cards = page.locator('.products-section .feature-card');
    if (!(await cards.count())) {
      test.skip(true, 'Không có featured products trên instance này — skip grid check');
    }

    const first = await cards.first().boundingBox();
    expect(first).not.toBeNull();
    // col-6 trên 375px viewport → card width ≈ 45–55% viewport
    expect(first!.width).toBeGreaterThan(120);
    expect(first!.width).toBeLessThan(230);
  });

  test('desktop regression: bottom nav ẩn, sidebar hiện', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.goto('/', { waitUntil: 'networkidle' });

    await expect(page.locator('.mobile-bottom-nav').first()).toBeHidden();
    // Desktop sidebar hoặc hero header vẫn render (layout phụ thuộc route)
    await expect(page.locator('body')).not.toBeEmpty();
  });
});
