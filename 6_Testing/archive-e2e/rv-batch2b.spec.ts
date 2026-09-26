import { test, expect } from '@playwright/test';

/**
 * Batch 2 follow-up RV (2026-09-20, after CD deploy of 25447f1a).
 *
 * 1) Free/Charity order flow: cart toàn free → "Thanh toán" phải đi qua /checkout
 *    ("Hình thức nhận hàng" + SĐT/địa chỉ) — KHÔNG tạo đơn DINEIN trực tiếp rồi
 *    redirect tracking (lỗi cũ khiến shipper không tìm thấy đơn).
 * 2) Mở rộng vùng hiển thị toàn trang KhachLink: .khachlink-main max-width 1200px —
 *    container trang > 700px trên desktop (trước đây 500-700px, lề 2 bên quá lớn).
 *
 * Run: npx playwright test e2e-tests/rv-batch2b.spec.ts --config=playwright-rv-batch2.config.ts
 */

const KH = 'https://diemthuong2.khachvip.online';
const FREE_PRODUCT_ID = '00000000-0000-0000-0000-0000000000ff';
const TENANT_ID = '00000000-0000-0000-0000-000000000001';

test.describe('Batch 2 follow-up RV — free/charity checkout + wider layout', () => {

  test('1) Free cart → "Thanh toán" đi qua /checkout (không redirect tracking)', async ({ page }) => {
    await page.goto(KH, { waitUntil: 'domcontentloaded', timeout: 30000 });
    // Inject cart toàn free/charity (CartState PascalCase JSON — System.Text.Json case-sensitive)
    await page.evaluate(([freeId, tenantId]) => {
      localStorage.setItem('vanan_cart', JSON.stringify({
        Items: [{
          Id: crypto.randomUUID ? crypto.randomUUID() : '11111111-1111-1111-1111-111111111111',
          ProductId: freeId,
          ProductName: 'Sản phẩm miễn phí RV',
          Description: '',
          Quantity: 1,
          UnitPrice: 0,
          VatRate: 0.1,
          TenantId: tenantId,
          TenantName: 'Vạn An Cafe (HKD Group 1)',
          IsFree: true,
          IsCharity: false,
        }],
        OrderNote: '',
      }));
    }, [FREE_PRODUCT_ID, TENANT_ID]);

    // /cart → nút checkout (AllItemsFree → "Nhận đồ miễn phí")
    await page.goto(`${KH}/cart`, { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.waitForTimeout(4000);
    const cartBody = await page.textContent('body') || '';
    expect(cartBody).toContain('Sản phẩm miễn phí RV');

    const checkoutBtn = page.locator('[data-testid="cart-btn-checkout"]').first();
    await checkoutBtn.waitFor({ state: 'visible', timeout: 15000 });
    await checkoutBtn.click();
    await page.waitForTimeout(3000);

    // PHẢI dừng ở /checkout — không được redirect /order-tracking (fix 1)
    const url = page.url();
    console.log(`[RV1] after checkout click → ${url}`);
    expect(url).toContain('/checkout');
    expect(url).not.toContain('/order-tracking');

    // /checkout hiển thị "Hình thức nhận hàng" + notice miễn phí
    const checkoutBody = await page.textContent('body') || '';
    expect(checkoutBody).toContain('Hình thức nhận hàng');
    expect(checkoutBody).toContain('Đơn hàng miễn phí');

    // Cleanup
    await page.evaluate(() => localStorage.removeItem('vanan_cart'));
  });

  test('2) Vùng hiển thị mở rộng: container > 700px trên desktop (mọi trang)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`${KH}/community/salesman-store`, { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.waitForTimeout(4000);

    const widths = await page.evaluate(() => {
      const main = document.querySelector('.khachlink-main');
      const container = document.querySelector('.khachlink-main .container');
      return {
        main: main ? main.getBoundingClientRect().width : 0,
        container: container ? container.getBoundingClientRect().width : 0,
      };
    });
    console.log(`[RV2] salesman-store main=${widths.main}px container=${widths.container}px`);
    expect(widths.main, 'main display area should be wide (>700px)').toBeGreaterThan(700);
    expect(widths.container, 'page container should use Bootstrap wide widths (>700px)').toBeGreaterThan(700);

    // Một trang nội dung khác (tracking đơn cũ — hoặc wallet khi guest)
    await page.goto(`${KH}/community/wallet`, { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.waitForTimeout(4000);
    const w2 = await page.evaluate(() => {
      const container = document.querySelector('.khachlink-main .container');
      return container ? container.getBoundingClientRect().width : 0;
    });
    console.log(`[RV2] wallet container=${w2}px`);
    expect(w2).toBeGreaterThan(700);

    // Trang gian hàng vẫn render đúng
    const body = await page.textContent('body') || '';
    expect(body.length).toBeGreaterThan(50);
  });
});
