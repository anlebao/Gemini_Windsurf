import { test, expect } from '@playwright/test';

/**
 * Loyalty Points Integrity — Batch 4 RV (2026-09-21, after CD Multi-VPS deploy of 5e9ada23).
 *
 * Verifies the deployed KhachLink UI against the TWO new Batch-4 surfaces:
 *   - T4.3 (checkout estimate): the loyalty estimate banner on /checkout now shows the
 *     SERVER-computed estimate (GET /api/loyalty/estimate → LoyaltyPointsCalculator, D1
 *     net revenue) — the client no longer replicates the formula.
 *   - T4.2 (banner thực tế): the order-tracking page shows the REAL awarded points read
 *     from the PG ledger (LoyaltyIssuanceRecord), not a recompute.
 *
 * API-level checks (estimate formula + real-points banner) verified separately via curl:
 *   - /api/loyalty/estimate?tenantId=0dfab177…&subTotal=100000&discountAmount=10000 → {points:9000, netRevenue:90000}
 *   - /api/public/orders/01a0becd-… → {pointsAwarded:30} (ledger thật — old recompute gave 5500)
 *
 * Run: npx playwright test e2e-tests/rv-batch4.spec.ts --config=playwright-rv-batch4.config.ts
 */

const KHACHLINK = 'https://diemthuong2.khachvip.online';
// Tenant có sản phẩm thật trên ShopERP ("Com Trua RV" 50.000đ) — dùng cho luồng add-to-cart → /checkout.
const STORE_ID = 'a5b6c7d8-1234-5678-9abc-def012345678';
// Đơn thật đã completed + đã award 30 điểm qua ledger (PG LoyaltyIssuanceRecord) — banner phải đọc số THẬT.
const COMPLETED_ORDER = '01a0becd-066f-7513-8e1c-e3e4ca9128c5';

test.describe('Batch 4 RV — checkout estimate server + banner điểm thực tế', () => {

  test('Checkout: loyalty estimate banner hiển thị điểm SERVER-computed (T4.3)', async ({ page }) => {
    const errors: string[] = [];
    page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
    page.on('pageerror', e => errors.push(String(e)));

    // Mở store tenant có products → thêm "Com Trua RV" vào giỏ (cart lưu localStorage → reload an toàn)
    await page.goto(`${KHACHLINK}/store/by-id/${STORE_ID}`, { waitUntil: 'domcontentloaded', timeout: 45000 });
    await page.waitForTimeout(4000); // Blazor WASM boot + products load

    // VibeShowcase render nhiều kiểu card — nút có thể là "Thêm vào giỏ" hoặc "Đặt hàng".
    const addBtn = page.locator('button:has-text("Thêm vào giỏ")').first();
    const orderBtn = page.locator('button:has-text("Đặt hàng")').first();
    await (await Promise.any([
      addBtn.waitFor({ state: 'visible', timeout: 20000 }).then(() => addBtn),
      orderBtn.waitFor({ state: 'visible', timeout: 20000 }).then(() => orderBtn),
    ])).click();
    await page.waitForTimeout(1500);
    console.log('[B4] added product to cart');

    // /checkout → banner estimate (server endpoint)
    await page.goto(`${KHACHLINK}/checkout`, { waitUntil: 'domcontentloaded', timeout: 45000 });

    const banner = page.locator('[data-testid="loyalty-estimate-banner"]');
    await banner.waitFor({ state: 'visible', timeout: 30000 });

    const text = (await banner.textContent()) || '';
    console.log(`[B4] estimate banner text: "${text.trim()}"`);
    expect(text, 'banner phải có từ "điểm"').toContain('điểm');

    const m = text.match(/([\d.,]+)\s*điểm/);
    expect(m, `parse estimate points từ: ${text}`).not.toBeNull();
    const points = parseInt((m![1] || '').replace(/\./g, ''), 10);
    console.log(`[B4] estimated points = ${points}`);
    expect(points, 'estimate phải > 0 (server formula)').toBeGreaterThan(0);

    const crash = errors.filter(e => /Exception|Unable to set property/i.test(e));
    expect(crash, `crash errors: ${crash.join(' | ')}`).toEqual([]);

    await page.screenshot({ path: 'rv-b4-checkout-estimate.png', fullPage: true });
  });

  test('Tracking: banner hiển thị điểm THỰC TẾ từ ledger — 30, không recompute (T4.2)', async ({ page }) => {
    const errors: string[] = [];
    page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
    page.on('pageerror', e => errors.push(String(e)));

    await page.goto(`${KHACHLINK}/order-tracking/${COMPLETED_ORDER}`, { waitUntil: 'domcontentloaded', timeout: 45000 });

    const banner = page.locator('[data-testid="loyalty-points-banner"]');
    await banner.waitFor({ state: 'visible', timeout: 30000 });

    const text = (await banner.textContent()) || '';
    console.log(`[B4] tracking banner text: "${text.trim()}"`);
    // Đơn 01a0becd thực tế được award 30 điểm (PG ledger). Recompute cũ theo TotalAmount 55.000 × 10%
    // sẽ ra 5.500 — banner phải hiện 30 (số THẬT từ LoyaltyIssuanceRecord).
    expect(text, 'banner phải hiện 30 điểm (số thật từ ledger)').toContain('30 điểm');

    const crash = errors.filter(e => /Exception|Unable to set property/i.test(e));
    expect(crash, `crash errors: ${crash.join(' | ')}`).toEqual([]);

    await page.screenshot({ path: 'rv-b4-tracking-banner.png', fullPage: true });
  });
});
