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

    await page.goto(BASE, { waitUntil: 'networkidle' });
    await page.waitForSelector('.dir-search-input', { timeout: 60000 });
    await page.waitForFunction(() => (window as any).Blazor !== undefined, null, { timeout: 30000 });
    await page.waitForTimeout(2500);
    await page.fill('.dir-search-input', typo);
    await page.keyboard.press('Enter'); // Enter submit — Pattern A giữ hành vi cũ

    // Chấp nhận: results hiển thị HOẶC DidYouMean chip xuất hiện
    const didYouMean = page.locator('text=Ý bạn là');
    const storeCards = page.locator('.store-card');
    await expect(didYouMean.or(storeCards.first())).toBeVisible({ timeout: 30000 });
  });

  test('Pattern B: DidYouMean chip "Ý bạn là?" render + 1-tap re-search', async ({ page, request }) => {
    // Tìm typo query chắc chắn trả didYouMean từ API trước (tránh flaky do data đổi).
    const listRes = await request.get(`${BASE}/api/tenants/search?name=`);
    if (!listRes.ok()) { test.skip(true, 'Search API không khả dụng'); return; }
    const listData = await listRes.json();
    const names: string[] = (listData.results ?? []).map((s: any) => s.name).filter(Boolean);
    if (names.length === 0) { test.skip(true, 'Chưa có tenant data'); return; }

    // Thử vài biến thể typo cho tới khi API trả didYouMean non-null.
    let typo = '';
    let suggestion = '';
    for (const n of names.slice(0, 8)) {
      const compact = n.replace(/\s+/g, '').toLowerCase();
      const candidates = [
        compact.slice(0, Math.max(3, compact.length - 2)),           // cắt 2 ký tự cuối
        compact.split('').reverse().join(''),                        // đảo chữ
        compact.slice(0, 3) + 'x' + compact.slice(3),                 // chèn ký tự lạ
      ];
      for (const c of candidates) {
        if (c.length < 3 || c === compact) continue;
        const r = await request.get(`${BASE}/api/tenants/search?name=${encodeURIComponent(c)}`);
        if (!r.ok()) continue;
        const d = await r.json();
        if (d.matchStrategy === 'fuzzy' && d.didYouMean) {
          typo = c; suggestion = d.didYouMean;
          break;
        }
      }
      if (typo) break;
    }
    if (!typo) { test.skip(true, 'Không tìm được fuzzy case có didYouMean trên prod data'); return; }

    await page.goto(BASE, { waitUntil: 'networkidle' });
    await page.waitForSelector('.dir-search-input', { timeout: 60000 });
    // Blazor Server: chờ circuit sẵn sàng — Enter trước khi connect sẽ bị drop
    // (event mất → search không chạy → flake).
    await page.waitForFunction(() => (window as any).Blazor !== undefined, null, { timeout: 30000 });
    await page.waitForTimeout(2500);
    await page.fill('.dir-search-input', typo);
    await page.keyboard.press('Enter');

    // Chip "Ý bạn là: <suggestion>?" phải hiển thị
    const chip = page.locator('text=Ý bạn là');
    await expect(chip).toBeVisible({ timeout: 30000 });
    await expect(page.locator(`button:has-text("${suggestion}")`).first()).toBeVisible();

    // 1-tap → re-search với suggestion → store card của suggestion xuất hiện
    await page.locator(`button:has-text("${suggestion}")`).first().click();
    await expect(page.locator('.store-card').first()).toBeVisible({ timeout: 30000 });
    await expect(page.locator(`text=${suggestion}`).first()).toBeVisible();
  });
});
