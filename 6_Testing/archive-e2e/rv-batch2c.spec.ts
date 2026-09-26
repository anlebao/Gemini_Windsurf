import { test, expect } from '@playwright/test';

/**
 * Batch 2c RV (2026-09-20, after CD deploy of 3495ac21):
 * - Checkout: chọn "Giao hàng" → map ghim vị trí hiện ra, mặc định = GPS khách,
 *   hidden inputs delivery-lat/lng được ghi toạ độ (submit dùng pin).
 * - Deployed assets chứa fix: realtime.js có pinLocation; WASM NearbyOrders có
 *   "Không giới hạn"; WASM Checkout có checkout-pin-map.
 *
 * Run: npx playwright test e2e-tests/rv-batch2c.spec.ts --config=playwright-rv-batch2.config.ts
 */

const KH = 'https://diemthuong2.khachvip.online';
const FREE_PRODUCT_ID = '00000000-0000-0000-0000-0000000000ff';
const TENANT_ID = '00000000-0000-0000-0000-000000000001';

test.describe('Batch 2c RV — checkout pin map + shipper unlimited radius', () => {

  test('Checkout DELIVERY → map ghim vị trí hiện ra + hidden inputs có toạ độ', async ({ page }) => {
    await page.goto(KH, { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.evaluate(([freeId, tenantId]) => {
      localStorage.setItem('vanan_cart', JSON.stringify({
        Items: [{
          Id: '11111111-1111-1111-1111-111111111111',
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

    await page.goto(`${KH}/checkout`, { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.waitForTimeout(5000);

    // Map pin chưa hiện khi mặc định DINEIN
    const pinSectionBefore = await page.locator('[data-testid="checkout-pin-map-section"]').count();
    console.log(`[RV] pin section before DELIVERY: ${pinSectionBefore}`);
    expect(pinSectionBefore).toBe(0);

    // Chọn "Giao hàng"
    await page.click('[data-testid="order-type-delivery"]');
    await page.waitForTimeout(6000); // render map + tiles

    const pinSection = await page.locator('[data-testid="checkout-pin-map-section"]');
    expect(await pinSection.isVisible()).toBe(true);

    // Leaflet thêm class .leaflet-container vào chính #checkout-pin-map + các leaflet-pane
    const mapInit = await page.evaluate(() => {
      const div = document.getElementById('checkout-pin-map');
      return div
        ? div.classList.contains('leaflet-container') || div.querySelector('.leaflet-map-pane') !== null
        : false;
    });
    console.log(`[RV] map initialized: ${mapInit}`);
    expect(mapInit, 'map phải render (leaflet-container / leaflet-map-pane)').toBe(true);

    // Hidden inputs phải có toạ độ (mặc định GPS khách; fallback trung tâm khi không có GPS)
    const lat = await page.inputValue('#delivery-lat').catch(() => '');
    const lng = await page.inputValue('#delivery-lng').catch(() => '');
    console.log(`[RV] pinned coords: lat=${lat} lng=${lng}`);
    expect(parseFloat(lat), 'lat phải là số != 0').not.toBeNaN();
    expect(parseFloat(lat)).not.toBe(0);
    expect(parseFloat(lng)).not.toBe(0);

    // Nút "Dùng vị trí hiện tại" có mặt
    const useLoc = page.locator('button:has-text("Dùng vị trí hiện tại")');
    expect(await useLoc.isVisible()).toBe(true);

    await page.evaluate(() => localStorage.removeItem('vanan_cart'));
  });

  test('Deployed assets chứa fix (realtime.js pinLocation + WASM markers)', async ({ request }) => {
    // realtime.js (RCL asset) phải chứa pinLocation
    const js = await request.get(`${KH}/_content/VanAn.UI.Platform/js/realtime.js`);
    expect(js.status()).toBe(200);
    const jsBody = await js.text();
    expect(jsBody).toContain('pinLocation');
    expect(jsBody).toContain('draggable');
    console.log(`[RV] realtime.js: pinLocation present (${jsBody.length} bytes)`);

    // WASM KhachLink chứa "Không giới hạn" (NearbyOrders) + "checkout-pin-map" (Checkout)
    // Tên DLL thật lấy từ blazor.boot.json (Blazor WASM dùng tên content-hash)
    const boot = await request.get(`${KH}/_framework/blazor.boot.json`);
    expect(boot.status()).toBe(200);
    const bootJson = await boot.json() as any;
    const resources: Record<string, string> = bootJson.resources?.assembly ?? {};
    const dllName = Object.keys(resources).find(n => n.toLowerCase().includes('vanan.khachlink'));
    console.log(`[RV] KhachLink DLL: ${dllName}`);
    expect(dllName, 'tìm thấy vanan.khachlink.dll trong blazor.boot.json').toBeTruthy();

    const wasm = await request.get(`${KH}/_framework/${dllName}`);
    expect(wasm.status()).toBe(200);
    const buf = await wasm.body();
    // .NET DLL lưu string UTF-16 trong metadata — kiểm tra cả 2 encoding
    const txt16 = buf.toString('utf16le');
    const txt8 = buf.toString('latin1');
    const has = (s: string) => txt16.includes(s) || txt8.includes(s);
    console.log(`[RV] WASM: 'Không giới hạn'=${has('Không giới hạn')} 'checkout-pin-map'=${has('checkout-pin-map')} 'delivery-lat'=${has('delivery-lat')}`);
    expect(has('Không giới hạn'), 'WASM chứa "Không giới hạn" (NearbyOrders)').toBe(true);
    expect(has('checkout-pin-map'), 'WASM chứa "checkout-pin-map" (Checkout)').toBe(true);
    expect(has('delivery-lat'), 'WASM chứa "delivery-lat" (Checkout pin)').toBe(true);
  });
});
