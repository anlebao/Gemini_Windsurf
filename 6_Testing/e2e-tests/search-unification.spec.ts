import { test, expect } from '@playwright/test';

// Search Unification v2 (Gate 4 — UI layout change → E2E bắt buộc):
//  A) Pill style storefront → VanAnPublicSearchBar (Pill=true) trên mọi public
//     search surface: Directory Home/StoreFinder, KhachLink Home/StoreFinder/
//     ProductSearchResults, HoptacxaPortal, StoreSearchBar (accent vars).
//  B) Smart product search: /api/catalog/search port Pattern B — normalize bỏ
//     dấu + token + fuzzy + DidYouMean chip (FeaturedProductSearchService).
// Env: KHACHLINK_URL (default commienphi), DIRECTORY_URL (default timlathay.com),
//      HOPTACXA_URL.

test.describe.configure({ timeout: 150_000 });

const KHACHLINK = process.env.KHACHLINK_URL || 'https://commienphi.timlathay.com';
const DIRECTORY = process.env.DIRECTORY_URL || 'https://timlathay.com';
const PORTAL = process.env.HOPTACXA_URL || 'https://hoptacxa.timlathay.com';

/** Strip dấu tiếng Việt (đ→d riêng vì NFD không strip) — mirror DiacriticFold. */
function stripVi(s: string): string {
  return s.normalize('NFD').replace(/[̀-ͯ]/g, '')
    .replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase().trim();
}

test.describe('Search Unification v2 — pill style on public surfaces', () => {
  test('KhachLink /stores search input nằm trong .vanan-search-pill', async ({ page }) => {
    await page.goto(`${KHACHLINK}/stores`, { waitUntil: 'domcontentloaded' });
    const input = page.locator('#search-input');
    await expect(input).toBeVisible({ timeout: 90000 });
    await expect(page.locator('.input-group.vanan-search-pill', { has: input })).toBeVisible();
    // nút search pill (gradient) vẫn trong group — Pattern A
    const group = page.locator('.input-group.vanan-search-pill', { has: input });
    await expect(group.locator('button.btn-primary').last()).toBeVisible();
  });

  test('KhachLink /products refactor → shared pill bar (không còn input-group-lg)', async ({ page }) => {
    await page.goto(`${KHACHLINK}/products`, { waitUntil: 'domcontentloaded' });
    const input = page.locator('#product-search-input');
    await expect(input).toBeVisible({ timeout: 90000 });
    await expect(page.locator('.input-group.vanan-search-pill', { has: input })).toBeVisible();
    await expect(input).toHaveAttribute('enterkeyhint', 'search');
    // raw markup cũ bị thay — không còn input-group-lg quanh input này
    await expect(page.locator('.input-group-lg', { has: input })).toHaveCount(0);
  });

  test('Directory Home search input trong pill shell', async ({ page }) => {
    await page.goto(DIRECTORY, { waitUntil: 'domcontentloaded' });
    const input = page.locator('.dir-search-input');
    await expect(input).toBeVisible({ timeout: 60000 });
    await expect(page.locator('.input-group.vanan-search-pill', { has: input })).toBeVisible();
  });

  test('HoptacxaPortal search input trong pill shell', async ({ page }) => {
    await page.goto(PORTAL, { waitUntil: 'domcontentloaded' });
    const input = page.locator('#htx-portal-search');
    await expect(input).toBeVisible({ timeout: 90000 });
    await expect(page.locator('.input-group.vanan-search-pill', { has: input })).toBeVisible();
  });

  test('Storefront semantic search bar giữ pill + tenant accent', async ({ page }) => {
    // Lấy slug thật từ tenant search (deterministic, không hardcode)
    const res = await page.request.get(`${KHACHLINK}/api/tenants/search?name=samho`);
    test.skip(!res.ok(), 'tenant search API không khả dụng');
    const dto = await res.json();
    const store = dto?.results?.[0];
    test.skip(!store?.slug, 'không có tenant slug để test storefront');
    await page.goto(`${KHACHLINK}/store/${store.slug}`, { waitUntil: 'domcontentloaded' });
    const input = page.locator('.store-search-input');
    await expect(input).toBeVisible({ timeout: 90000 });
    await expect(page.locator('.input-group.vanan-search-pill', { has: input })).toBeVisible();
  });
});

test.describe('Search Unification v2 — smart product search', () => {
  test('API /api/catalog/search: query bỏ dấu vẫn match (normalize levels)', async ({ page }) => {
    // Lấy product thật → strip dấu → query lại phải match (contains-norm/token/fuzzy).
    const all = await page.request.get(`${KHACHLINK}/api/catalog/search?pageSize=20`);
    test.skip(!all.ok(), 'catalog search API không khả dụng');
    const allDto = await all.json();
    const names: string[] = (allDto?.products ?? []).map((p: any) => p.displayName).filter(Boolean);
    test.skip(names.length === 0, 'không có featured product để test');

    // Chọn tên có dấu nếu được (test normalize), fallback tên đầu
    const target = names.find((n) => n !== stripVi(n)) ?? names[0];
    const stripped = stripVi(target).split(/\s+/).slice(0, 2).join(' ');
    test.skip(stripped.length < 3, 'tên quá ngắn để tạo query không dấu');

    const res = await page.request.get(
      `${KHACHLINK}/api/catalog/search?q=${encodeURIComponent(stripped)}`
    );
    expect(res.ok()).toBeTruthy();
    const dto = await res.json();
    expect(['contains', 'token', 'fuzzy']).toContain(dto.matchStrategy);
    const hit = (dto.products ?? []).some(
      (p: any) => stripVi(p.displayName ?? '') === stripVi(target)
    );
    expect(hit).toBeTruthy();
  });

  test('UI /products: fuzzy → DidYouMean chip "Ý bạn là" + 1-tap re-search', async ({ page }) => {
    // Deterministic probe: tìm 1 typo query mà API trả didYouMean (mirror w3-2d spec).
    const all = await page.request.get(`${KHACHLINK}/api/catalog/search?pageSize=20`);
    test.skip(!all.ok(), 'catalog search API không khả dụng');
    const allDto = await all.json();
    const names: string[] = (allDto?.products ?? []).map((p: any) => p.displayName).filter(Boolean);
    test.skip(names.length === 0, 'không có featured product để test');

    // Thử typo variants của token đầu các product cho đến khi có didYouMean.
    let typo: string | null = null;
    let suggestion: string | null = null;
    for (const n of names.slice(0, 10)) {
      const tok = stripVi(n).split(/\s+/)[0];
      if (!tok || tok.length < 3) continue;
      for (const t of [`${tok}o`, tok.slice(0, -1)]) {
        const r = await page.request.get(`${KHACHLINK}/api/catalog/search?q=${encodeURIComponent(t)}`);
        if (!r.ok()) continue;
        const d = await r.json();
        if (d?.didYouMean) { typo = t; suggestion = d.didYouMean; break; }
      }
      if (typo) break;
    }
    test.skip(!typo, 'không tìm được typo query nào trả didYouMean trên dữ liệu prod');

    await page.goto(`${KHACHLINK}/products`, { waitUntil: 'domcontentloaded' });
    const input = page.locator('#product-search-input');
    await expect(input).toBeVisible({ timeout: 90000 });
    await input.fill(typo!);
    await input.press('Enter');

    const chip = page.locator('.alert', { hasText: 'Ý bạn là' })
      .locator('button', { hasText: suggestion! });
    await expect(chip).toBeVisible({ timeout: 60000 });
    await chip.click();
    await expect(input).toHaveValue(suggestion!, { timeout: 30000 });
  });
});
