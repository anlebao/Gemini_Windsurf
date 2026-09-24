import { test, expect, request } from '@playwright/test';

/**
 * Issue #185 — loyalty point error (app2.khachvip.online) — Batch 1 UI E2E.
 *
 * Covers the deployed UI surfaces changed by cards 03/05 (+ API contract for 04):
 *   - Card 05: checkout shows address/GPS only for DELIVERY; phone marked required
 *     for DELIVERY (and transfer). PICKUP-type orders hide the address field.
 *   - Card 03 Phase A: loyalty estimate endpoint still returns loyaltyEnabled
 *     (regression — checkout/tracking now gate their loyalty UI on this flag).
 *   - Card 04: GET /api/internal/loyalty-budget/caps returns the tenant caps
 *     snapshot (owner read-only view data source). Requires INTERNAL_API_KEY env.
 *
 * NOT covered here (needs a loyalty-disabled tenant fixture): negative case where
 * the estimate returns loyaltyEnabled=false → checkout hides banner/signup modal
 * and tracking hides the points banner. Verified manually via RV window.
 *
 * Run: npx playwright test e2e-tests/issue-185.spec.ts
 */

const KHACHLINK = process.env.KHACHLINK_URL ?? 'https://diemthuong2.khachvip.online';
const GATEWAY = process.env.GATEWAY_PUBLIC_URL ?? 'https://api2.khachvip.online';
// Tenant có sản phẩm thật trên ShopERP ("Com Trua RV" 50.000đ) — same fixture as rv-batch4.
const STORE_ID = 'a5b6c7d8-1234-5678-9abc-def012345678';
const TENANT_ID = '0dfab177-0000-4000-8000-000000000000'; // placeholder — replaced by store tenant below if needed

test.describe('Issue #185 — delivery-only fields + loyalty gating', () => {

  test('Checkout: DELIVERY shows address + required phone; DINEIN hides address', async ({ page }) => {
    const errors: string[] = [];
    page.on('pageerror', e => errors.push(String(e)));

    // Add a product to cart (same flow as rv-batch4) so /checkout renders the form.
    await page.goto(`${KHACHLINK}/store/by-id/${STORE_ID}`, { waitUntil: 'domcontentloaded', timeout: 45000 });
    await page.waitForTimeout(4000); // Blazor WASM boot + products load

    const addBtn = page.locator('button:has-text("Thêm vào giỏ")').first();
    const orderBtn = page.locator('button:has-text("Đặt hàng")').first();
    await (await Promise.any([
      addBtn.waitFor({ state: 'visible', timeout: 20000 }).then(() => addBtn),
      orderBtn.waitFor({ state: 'visible', timeout: 20000 }).then(() => orderBtn),
    ])).click();
    await page.waitForTimeout(1500);

    await page.goto(`${KHACHLINK}/checkout`, { waitUntil: 'domcontentloaded', timeout: 45000 });
    await page.locator('[data-testid="order-type-selector"]').waitFor({ state: 'visible', timeout: 30000 });

    // Default DINEIN → address hidden
    await page.locator('[data-testid="order-type-dinein"]').check();
    await expect(page.locator('[data-testid="checkout-input-address"]')).toHaveCount(0);

    // DELIVERY → address visible, phone marked required
    await page.locator('[data-testid="order-type-delivery"]').check();
    await expect(page.locator('[data-testid="checkout-input-address"]')).toBeVisible();

    // Issue #185 comment (2026-09-24): the delivery pin map must appear so the buyer can
    // drop the delivery location. Leaflet renders a .leaflet-container with tile <img>s.
    const pinMap = page.locator('#checkout-pin-map');
    await expect(pinMap).toBeVisible({ timeout: 15000 });
    await expect(pinMap.locator('.leaflet-container')).toHaveCount(1, { timeout: 15000 });
    await expect(pinMap.locator('img.leaflet-tile').first()).toBeVisible({ timeout: 20000 });
    await expect(page.locator('input#delivery-lat')).toHaveValue(/\d/);

    const phoneLabel = page.locator('label[for="guest-phone"]');
    await expect(phoneLabel).toContainText('*');

    // DELIVERY + no phone → submit must show the delivery-phone validation error
    await page.locator('[data-testid="checkout-input-name"]').fill('E2E Test');
    await page.locator('[data-testid="checkout-input-address"]').fill('123 Test Street');
    await page.locator('[data-testid="checkout-btn-place-order"]').click();
    await expect(page.locator('text=giao hàng').first()).toBeVisible({ timeout: 10000 });

    const crash = errors.filter(e => /Exception/i.test(e));
    expect(crash, `page errors: ${crash.join(' | ')}`).toEqual([]);
  });

  test('Estimate API: response carries loyaltyEnabled + points (Card 03 gate source)', async ({ request: req }) => {
    const resp = await req.get(
      `${GATEWAY}/api/loyalty/estimate?tenantId=${TENANT_ID}&subTotal=100000&discountAmount=10000`);
    expect(resp.ok(), `estimate endpoint status ${resp.status()}`).toBeTruthy();
    const body = await resp.json();
    expect(body).toHaveProperty('loyaltyEnabled');
    expect(body).toHaveProperty('points');
    expect(body).toHaveProperty('netRevenue');
    // Disabled → points MUST be 0 (server-side gate mirrors award gate)
    if (body.loyaltyEnabled === false) {
      expect(body.points).toBe(0);
    }
  });

  test('Budget caps API: GET /api/internal/loyalty-budget/caps returns snapshot (Card 04)', async ({ request: req }) => {
    const apiKey = process.env.INTERNAL_API_KEY;
    test.skip(!apiKey, 'INTERNAL_API_KEY env not set — internal endpoint is API-key protected');

    const resp = await req.get(`${GATEWAY}/api/internal/loyalty-budget/caps?tenantId=${TENANT_ID}`, {
      headers: { 'X-Internal-Api-Key': apiKey! },
    });
    expect(resp.ok(), `caps endpoint status ${resp.status()}`).toBeTruthy();
    const body = await resp.json();
    expect(body.tenantId?.toLowerCase()).toBe(TENANT_ID.toLowerCase());
    expect(body).toHaveProperty('hasConfig');
    expect(body).toHaveProperty('pointsIssuedThisMonth');
    expect(body).toHaveProperty('pointsIssuedToday');
  });

  test('Collaborators API (Card 06): requires Owner JWT — anonymous → 401', async ({ request: req }) => {
    // OwnerPanel default list data source — tenant comes from JWT (IDOR safe).
    const resp = await req.get(`${GATEWAY}/api/v1/tenant-community/collaborators?page=1&pageSize=20`);
    expect([401, 403]).toContain(resp.status());
  });
});
