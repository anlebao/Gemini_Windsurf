import { test, expect } from '@playwright/test';

// W3-2d rollout (Gate 4 — UI layout change → E2E bắt buộc):
//  Pattern A+B search components (VanAnPublicSearchBar + VanAnDidYouMeanChip)
//  được port sang KhachLink surfaces:
//   1) StoreFinder (/stores) trên KhachLink storefront instances — dùng chung
//      /api/tenants/search (ITenantSearchService, Pattern B fuzzy + DidYouMean).
//   2) HoptacxaPortal — client-side filter HTX/member trên data đã load.
// Env: KHACHLINK_URL (default https://commienphi.timlathay.com),
//      HOPTACXA_URL (default https://hoptacxa.timlathay.com).

// WASM boot chậm trên prod — cần timeout rộng hơn default 30s
test.describe.configure({ timeout: 150_000 });

const KHACHLINK = process.env.KHACHLINK_URL || 'https://commienphi.timlathay.com';
const PORTAL = process.env.HOPTACXA_URL || 'https://hoptacxa.timlathay.com';

test.describe('W3-2d KhachLink search rollout — StoreFinder', () => {
  test('Pattern A: compact input-group, enterkeyhint=search, button trong group @375px', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto(`${KHACHLINK}/stores`, { waitUntil: 'domcontentloaded' });
    // WASM boot chậm — chờ search input render
    const input = page.locator('#search-input');
    await expect(input).toBeVisible({ timeout: 90000 });

    // enterkeyhint + font-size ≤16px (chống iOS zoom)
    await expect(input).toHaveAttribute('enterkeyhint', 'search');
    const fontSize = await input.evaluate((el) => parseFloat(getComputedStyle(el).fontSize));
    expect(fontSize).toBeLessThanOrEqual(16);

    // nút search nằm TRONG .input-group (không bị keyboard che)
    const group = page.locator('.input-group', { has: input });
    await expect(group).toBeVisible();
    const searchBtn = group.locator('button', { has: page.locator('i.bi-search') }).last();
    await expect(searchBtn).toBeVisible();

    // Enter submit → gọi /api/tenants/search (đợi kết quả hoặc empty state)
    await input.fill('tra sua');
    await input.press('Enter');
    await page.waitForSelector('.store-card-hover, .empty-state-card', { timeout: 60000 });
  });

  test('Pattern B: fuzzy query → DidYouMean chip "Ý bạn là" + 1-tap re-search', async ({ page }) => {
    // Query deterministic: API prod trả didYouMean="Samho" cho "samo" (verified W3-2c)
    const apiRes = await page.request.get(
      `${KHACHLINK}/api/tenants/search?name=samo`
    );
    test.skip(!apiRes.ok(), 'tenant search API không khả dụng');
    const dto = await apiRes.json();
    test.skip(!dto?.didYouMean, `API không trả didYouMean cho "samo" (strategy=${dto?.matchStrategy})`);

    await page.goto(`${KHACHLINK}/stores`, { waitUntil: 'domcontentloaded' });
    const input = page.locator('#search-input');
    await expect(input).toBeVisible({ timeout: 90000 });
    await input.fill('samo');
    await input.press('Enter');

    // Chip render + click → search lại với suggestion.
    // Markup: VanAnAlert chứa text "Ý bạn là:" + <button>Samho</button> — button chỉ chứa suggestion.
    const chip = page.locator('.alert', { hasText: 'Ý bạn là' })
      .locator('button', { hasText: dto.didYouMean });
    await expect(chip).toBeVisible({ timeout: 60000 });
    await chip.click();
    await expect(input).toHaveValue(dto.didYouMean, { timeout: 30000 });
  });
});

test.describe('W3-2d KhachLink search rollout — HoptacxaPortal', () => {
  test('portal search filter HTX/member client-side', async ({ page }) => {
    await page.goto(PORTAL, { waitUntil: 'domcontentloaded' });
    const input = page.locator('#htx-portal-search');
    await expect(input).toBeVisible({ timeout: 90000 });

    // Pattern A basics
    await expect(input).toHaveAttribute('enterkeyhint', 'search');
    const fontSize = await input.evaluate((el) => parseFloat(getComputedStyle(el).fontSize));
    expect(fontSize).toBeLessThanOrEqual(16);

    // Gõ keyword khớp member thật → member cards bị filter + result count hiện
    const firstCard = page.locator('.htx-member-card').first();
    await expect(firstCard).toBeVisible({ timeout: 90000 });
    const memberName: string = (await firstCard.locator('.htx-member-name').innerText()).trim();
    const keyword = memberName.split(/\s+/)[0]; // từ đầu tên — đủ unique-ish

    await input.fill(keyword);
    await page.waitForSelector('.htx-member-card', { timeout: 30000 });
    await expect(page.locator('text=Kết quả:')).toBeVisible({ timeout: 15000 });
    // Ít nhất 1 card còn lại và tất cả chứa keyword (case-insensitive)
    const cards = page.locator('.htx-member-card');
    const count = await cards.count();
    expect(count).toBeGreaterThan(0);
    for (let i = 0; i < count; i++) {
      await expect(cards.nth(i)).toContainText(new RegExp(keyword, 'i'));
    }

    // Keyword không tồn tại → empty state
    await input.fill('zzzz-khong-ton-tai-999');
    await expect(page.locator('.htx-member-card')).toHaveCount(0, { timeout: 15000 });
  });
});
