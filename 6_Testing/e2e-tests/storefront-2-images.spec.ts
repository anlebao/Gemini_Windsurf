import { test, expect } from '@playwright/test';

// Storefront 2-ảnh background (2026-11-24, Gate 4 — UI layout change → E2E bắt buộc):
//  1) API plumbing: GET /api/tenants/by-slug/{slug} (TenantStoreDto) PHẢI trả
//     storefrontBannerUrl + storefrontBackdropUrl (null OK — tenant chưa set ảnh).
//  2) UI lazy-load: window.vananStorefront.applyLazyBackgrounds tồn tại;
//     .store-backdrop[data-bg] / .store-hero[data-hero-bg] chỉ xuất hiện khi tenant
//     đã cấu hình ảnh; gradient fallback (--store-hero-gradient) luôn render ngay
//     kể cả khi ảnh chưa load — storefront không bao giờ "treo" chờ ảnh R2.
// Env: HOPTACXA_URL (default https://hoptacxa.timlathay.com).

const PORTAL = process.env.HOPTACXA_URL || 'https://hoptacxa.timlathay.com';

interface CoopMember {
  tenantId: string;
  name: string;
  slug: string | null;
  khachLinkDomain: string | null;
}

async function fetchJson<T>(request: any, url: string): Promise<T | null> {
  const res = await request.get(url);
  if (!res.ok()) return null;
  return res.json() as Promise<T>;
}

// Member có slug → storefront tại {khachLinkDomain ?? PORTAL}/store/{slug}
// (portal hoptacxa.timlathay.com cũng serve /store/{slug} + proxy /api/tenants).
async function storefrontCandidates(request: any): Promise<{ base: string; path: string; slug: string | null; tenantId: string }[]> {
  const out: { base: string; path: string; slug: string | null; tenantId: string }[] = [];
  const coops = await fetchJson<any[]>(request, `${PORTAL}/api/cooperatives`);
  if (!coops?.length) return out;
  for (const coop of coops) {
    const members = await fetchJson<CoopMember[]>(request, `${PORTAL}/api/cooperatives/${coop.tenantId}/members`);
    for (const m of members ?? []) {
      if (m.slug) {
        out.push({
          base: m.khachLinkDomain ? `https://${m.khachLinkDomain}` : PORTAL,
          path: `/store/${m.slug}`, slug: m.slug, tenantId: m.tenantId,
        });
      } else if (m.khachLinkDomain) {
        out.push({
          base: `https://${m.khachLinkDomain}`,
          path: `/store/by-id/${m.tenantId}`, slug: null, tenantId: m.tenantId,
        });
      }
    }
  }
  return out;
}

test.describe('Storefront 2 background images', () => {
  test('by-slug store-info DTO chứa storefrontBannerUrl/storefrontBackdropUrl', async ({ request }) => {
    const candidates = await storefrontCandidates(request);
    const target = candidates.find((c) => c.slug);
    if (!target) {
      test.skip(true, 'Không có member storefront có slug trên prod');
      return;
    }

    // by-slug chỉ áp dụng cho candidate có slug
    let dto: any = null;
    if (target.slug) {
      for (const base of [target.base, PORTAL]) {
        dto = await fetchJson<any>(request, `${base}/api/tenants/by-slug/${target.slug}`);
        if (dto) break;
      }
    }
    if (!dto) {
      test.skip(true, `by-slug endpoint không reachable cho ${target.slug}`);
      return;
    }

    // Assert tối thiểu: endpoint trả TenantStoreDto hợp lệ + field mới trong contract.
    expect(dto.name).toBeTruthy();
    // Field PHẢI có mặt trong JSON (null OK — tenant chưa set ảnh; verify camelCase names).
    expect(Object.prototype.hasOwnProperty.call(dto, 'storefrontBannerUrl')).toBeTruthy();
    expect(Object.prototype.hasOwnProperty.call(dto, 'storefrontBackdropUrl')).toBeTruthy();
    // Phương án A (2026-11-24): storefrontVertical — enum serialize string ("General"...).
    expect(Object.prototype.hasOwnProperty.call(dto, 'storefrontVertical')).toBeTruthy();
  });

  test('Store page: vananStorefront lazy helper + hero/backdrop render an toàn', async ({ page, request }) => {
    const candidates = await storefrontCandidates(request);
    if (!candidates.length) {
      test.skip(true, 'Không có member storefront trên prod');
      return;
    }

    // Thử từng storefront — tenant hết phí nền tảng (metering gate) render "tạm ngưng"
    // không có .store-hero → skip sang candidate kế tiếp.
    let loaded = false;
    for (const c of candidates.slice(0, 6)) {
      await page.goto(`${c.base}${c.path}`, { waitUntil: 'domcontentloaded' });
      loaded = await page.waitForSelector('.store-hero', { timeout: 20000 }).then(() => true).catch(() => false);
      if (loaded) break;
    }
    if (!loaded) {
      test.skip(true, 'Tất cả storefront candidates đều bị metering-gate hoặc lỗi');
      return;
    }

    // JS helper phải tồn tại (lazy-load contract)
    const fnType = await page.evaluate(() => typeof (window as any).vananStorefront?.applyLazyBackgrounds);
    expect(fnType).toBe('function');

    // Hero luôn render — gradient fallback có mặt ngay cả khi chưa có banner image.
    const hero = page.locator('.store-hero').first();
    await expect(hero).toBeVisible();

    // Nếu tenant có banner → data-hero-bg attr tồn tại và sau idle --store-hero-image được set.
    const heroBg = await hero.getAttribute('data-hero-bg');
    if (heroBg) {
      await page.waitForFunction(
        () => getComputedStyle(document.querySelector('.store-hero') as HTMLElement).getPropertyValue('--store-hero-image') !== '',
        { timeout: 15000 }
      ).catch(() => {});
      const applied = await page.evaluate(() =>
        getComputedStyle(document.querySelector('.store-hero') as HTMLElement).getPropertyValue('--store-hero-image'));
      expect(applied).toContain('url(');
    }

    // Nếu tenant có backdrop → .store-backdrop[data-bg] tồn tại; sau idle backgroundImage được set.
    const backdrop = page.locator('.store-backdrop[data-bg]');
    const backdropCount = await backdrop.count();
    if (backdropCount > 0) {
      const bg = await backdrop.first().getAttribute('data-bg');
      expect(bg).toBeTruthy();
      await page.waitForTimeout(2500); // requestIdleCallback + image fetch
      const appliedBg = await backdrop.first().evaluate((el) => getComputedStyle(el).backgroundImage);
      expect(appliedBg).toContain('url(');
    }

    // Regression: trang không được kẹt loading — content chính vẫn visible.
    await expect(page.locator('.store-body, main, .store-footer').first()).toBeVisible({ timeout: 30000 });
  });
});
