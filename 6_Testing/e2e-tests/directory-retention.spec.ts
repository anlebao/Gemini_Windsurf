import { test, expect } from '@playwright/test';

// Task W2-3 (Gate 4 rules — UI layout change → E2E bắt buộc).
// timlathay.com retention: below-fold (category chips + recent + featured)
// thay initial state rỗng; "Dùng vị trí" giữ traffic on-domain (/stores nội bộ).
// Env: DIRECTORY_URL (default https://timlathay.com).

const BASE = process.env.DIRECTORY_URL || 'https://timlathay.com';

test.use({
  baseURL: BASE,
  storageState: { cookies: [], origins: [] }, // guest — directory là public surface
  viewport: { width: 375, height: 812 },
});

test.describe('Directory home retention (W2-3) @mobile', () => {
  test('initial load có below-fold: chips ngành + featured (không rỗng)', async ({ page }) => {
    await page.goto('/', { waitUntil: 'networkidle' });

    const belowFold = page.locator('.below-fold');
    await expect(belowFold).toBeVisible({ timeout: 30000 });

    // Category chips
    await expect(belowFold.locator('button', { hasText: 'Quán ăn' })).toBeVisible();
    await expect(belowFold.locator('button', { hasText: 'Cà phê' })).toBeVisible();

    // Featured section label (dù có hay không có data — section phải render)
    await expect(belowFold.getByText('Cửa hàng nổi bật')).toBeVisible();

    // Không overflow ngang ở 375px
    const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
    expect(scrollWidth).toBeLessThanOrEqual(376);
  });

  test('click category chip → chạy search store-mode', async ({ page }) => {
    await page.goto('/', { waitUntil: 'networkidle' });
    const belowFold = page.locator('.below-fold');
    await expect(belowFold).toBeVisible({ timeout: 30000 });

    await belowFold.locator('button', { hasText: 'Quán ăn' }).click();

    // Sau search: _hasSearched=true → below-fold biến mất, results/loading hiện
    await expect(page.locator('.below-fold')).toHaveCount(0, { timeout: 15000 });
    // Input giữ keyword chip
    await expect(page.locator('input[aria-label="Tìm cửa hàng"]')).toHaveValue('Quán ăn');
  });

  test('"Dùng vị trí của tôi" điều hướng on-domain /stores', async ({ page }) => {
    await page.goto('/', { waitUntil: 'networkidle' });

    // Click nút → phải navigate nội bộ (không forceLoad sang domain khác)
    await page.locator('button', { hasText: 'Dùng vị trí của tôi' }).click();
    await page.waitForURL('**/stores**', { timeout: 15000 });

    const url = new URL(page.url());
    expect(url.hostname).toBe(new URL(BASE).hostname); // vẫn timlathay.com, không phải khachvip.online
    // StoreFinder page render (search box nội bộ)
    await expect(page.locator('input[aria-label="Tìm cửa hàng"]')).toBeVisible({ timeout: 30000 });
  });
});
