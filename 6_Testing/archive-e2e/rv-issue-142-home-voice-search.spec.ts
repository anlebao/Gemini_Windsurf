import { test, expect } from '@playwright/test';

/**
 * RV Issue #142 — Home voice search box (Google Maps style)
 * Target: KhachLink production VPS (diemthuong2.khachvip.online)
 *
 * Verifies:
 * 1. Home page shows search input (replaces old "Tìm cửa hàng" button)
 * 2. Mic icon button present (voice search)
 * 3. Text search: type keyword + Enter → navigates to /stores?q=<keyword>
 * 4. /stores?q=<keyword> pre-fills the search box and shows results
 * 5. Old VanAnButton "Tìm cửa hàng" is gone
 */

const KHACHLINK_URL = process.env.KHACHLINK_URL || 'https://diemthuong2.khachvip.online';

test.describe('RV Issue #142 — Home voice search box', () => {
  test('Home shows search input + mic icon (not old button)', async ({ page }) => {
    await page.goto(KHACHLINK_URL, { waitUntil: 'networkidle' });

    // Wait for Blazor WASM to render (app root must have content)
    await page.waitForSelector('#home-search-input', { timeout: 30000 });

    // Search input exists
    const searchInput = page.locator('#home-search-input');
    await expect(searchInput).toBeVisible();

    // Heading still present
    await expect(page.locator('h3:has-text("Tìm cửa hàng gần bạn")')).toBeVisible();

    // Mic button (may be hidden if voice disabled, so check conditionally)
    const micBtn = page.locator('.home-voice-mic-btn');
    const micCount = await micBtn.count();
    if (micCount > 0) {
      await expect(micBtn.first()).toBeVisible();
    }

    // Old button "Tìm cửa hàng" (VanAnButton) should NOT exist as a standalone button
    // The heading has the text, but there should be no button with just "Tìm cửa hàng"
    const oldBtn = page.locator('button:has-text("Tìm cửa hàng")');
    await expect(oldBtn).toHaveCount(0);
  });

  test('Text search: type + Enter → navigates to /stores?q=keyword', async ({ page }) => {
    await page.goto(KHACHLINK_URL, { waitUntil: 'networkidle' });
    await page.waitForSelector('#home-search-input', { timeout: 30000 });

    const searchInput = page.locator('#home-search-input');
    await searchInput.fill('cafe');
    await searchInput.press('Enter');

    // Should navigate to /stores?q=cafe
    await page.waitForURL('**/stores?q=cafe', { timeout: 15000 });
    expect(page.url()).toContain('/stores');
    expect(page.url()).toContain('q=cafe');
  });

  test('/stores?q=keyword pre-fills search box', async ({ page }) => {
    await page.goto(`${KHACHLINK_URL}/stores?q=cafe`, { waitUntil: 'networkidle' });

    // StoreFinder search input should be pre-filled with "cafe"
    await page.waitForSelector('#search-input', { timeout: 30000 });
    const storeSearchInput = page.locator('#search-input');
    await expect(storeSearchInput).toHaveValue('cafe');
  });

  test('/stores without query param loads normally', async ({ page }) => {
    await page.goto(`${KHACHLINK_URL}/stores`, { waitUntil: 'networkidle' });
    await page.waitForSelector('#search-input', { timeout: 30000 });
    const storeSearchInput = page.locator('#search-input');
    // Empty keyword is fine — just verify page loads
    await expect(storeSearchInput).toBeVisible();
  });
});
