import { test, expect } from '@playwright/test';

// W3-2c Pattern A+B (Gate 4 — layout change): timlathay.com Directory home.
// A: compact hero, input size thường, nút Tìm kiếm TRONG input-group (không bị
//    keyboard che), enterkeyhint="search".
// B: fuzzy search — query không dấu/sai chính tả vẫn match (matchStrategy=fuzzy
//    hoặc DidYouMean chip hiển thị).
// Env: DIRECTORY_URL (default https://timlathay.com).

const BASE = process.env.DIRECTORY_URL || 'https://timlathay.com';

test.describe('W3-2c search UX pattern', () => {
  test('Pattern A: hero compact + search button trong input-group + enterkeyhint', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto(BASE, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('.dir-search-input', { timeout: 60000 });

    // Input: font-size ≤16px (tránh iOS zoom), enterkeyhint="search"
    const input = page.locator('.dir-search-input');
    const fontSize = await input.evaluate((el) => parseFloat(getComputedStyle(el).fontSize));
    expect(fontSize).toBeLessThanOrEqual(16);
    await expect(input).toHaveAttribute('enterkeyhint', 'search');

    // Nút Tìm kiếm nằm TRONG .input-group (Pattern A rule)
    const group = page.locator('.input-group').first();
    await expect(group.locator('button:has-text("Tìm kiếm"), button:has(i.bi-search)').last()).toBeVisible();

    // Hero icon nhỏ hơn trước (2.5rem → 1.25rem)
    const iconSize = await page.locator('.dir-hero-icon').evaluate((el) => parseFloat(getComputedStyle(el).fontSize));
    expect(iconSize).toBeLessThanOrEqual(22);
  });

  test('Pattern B: gõ không dấu/sai chính tả vẫn có kết quả hoặc DidYouMean', async ({ page, request }) => {
    // Lấy 1 tên store thật từ API public để tạo typo query có kiểm soát.
    const res = await request.get(`${BASE}/api/tenants/search?name=`);
    if (!res.ok()) {
      test.skip(true, 'Search API không khả dụng');
      return;
    }
    const data = await res.json();
    const names: string[] = (data.results ?? []).map((s: any) => s.name).filter(Boolean);
    if (names.length === 0) {
      test.skip(true, 'Chưa có tenant data trên prod');
      return;
    }
    const realName = names[0] as string;

    // Typo query: lấy tên, cắt bớt ký tự giữa (giả lập gõ sai/thiếu dấu)
    const typo = realName.length > 4
      ? realName.slice(0, Math.ceil(realName.length / 2)) + realName.slice(-2)
      : realName + 'x';

    await page.goto(BASE, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('.dir-search-input', { timeout: 60000 });
    await page.fill('.dir-search-input', typo);
    await page.keyboard.press('Enter'); // Enter submit — Pattern A giữ hành vi cũ

    // Chấp nhận: results hiển thị HOẶC DidYouMean chip xuất hiện
    const didYouMean = page.locator('text=Ý bạn là');
    const storeCards = page.locator('.store-card');
    await expect(didYouMean.or(storeCards.first())).toBeVisible({ timeout: 30000 });
  });
});
