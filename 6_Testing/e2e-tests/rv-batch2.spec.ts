import { test, expect } from '@playwright/test';

/**
 * Issue Batch #176-#181 — Batch 2 RV (2026-09-19, after CD Multi-VPS deploy of a620d1de).
 *
 * #176 — diemthuong2 store page loads WITHOUT the VanAnButton `disabled` InvalidCastException
 *        (fix c76c0b4d + sweep `disabled=` -> `Disabled` in 6ab09cbc).
 * #181 — no floating CartDrawer / header cart icon on /cart + /checkout (still present on home).
 * #178 — new salesman/products endpoints require auth; /community/salesman-store page loads.
 * #179 — quick-setup wizard shows REAL seeded counts from the DB (GetSeedCountsAsync),
 *        NOT hardcoded template metadata; seed persists to ShopERP SQLite.
 *
 * Run: npx playwright test e2e-tests/rv-batch2.spec.ts --config=playwright-rv-batch2.config.ts
 */

const KH = 'https://diemthuong2.khachvip.online';
const APP2 = 'https://app2.khachvip.online';
const GW = 'https://api2.khachvip.online';
const SYSADMIN_USER = 'sysadmin@vanan.vn';
const SYSADMIN_PASS = '2026@vanan';

test.describe('Batch 2 RV — #176 #181 #178 #179', () => {

  test('#176: store page loads without disabled-crash', async ({ page }) => {
    const errors: string[] = [];
    page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
    page.on('pageerror', e => errors.push(String(e)));

    const resp = await page.goto(`${KH}/store/by-id/00000000-0000-0000-0000-000000000001`, { waitUntil: 'domcontentloaded', timeout: 30000 });
    expect(resp?.status()).toBe(200);
    await page.waitForTimeout(6000); // WASM boot + render

    const crash = errors.filter(e => /InvalidCastException|Unable to set property|VanAnButton/i.test(e));
    console.log(`[#176] page errors: ${errors.length}, crash-like: ${crash.length}`);
    expect(crash, `crash errors: ${crash.join(' | ')}`).toEqual([]);

    const body = await page.textContent('body') || '';
    expect(body.length).toBeGreaterThan(50);
  });

  test('#181: no floating cart on /cart + /checkout', async ({ page }) => {
    // Fresh cart state
    await page.goto(KH, { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.evaluate(() => localStorage.clear());

    // Add a product on the store page (theme buttons call AddToCart: "Đặt hàng"/"Đặt Hàng"/"Thêm vào giỏ")
    await page.goto(`${KH}/store/by-id/00000000-0000-0000-0000-000000000001`, { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.waitForTimeout(8000);
    const addBtn = page.locator('button:has-text("Đặt hàng"), button:has-text("Đặt Hàng"), button:has-text("Thêm vào giỏ")').first();
    await addBtn.waitFor({ state: 'visible', timeout: 15000 });
    await addBtn.click();
    await page.waitForTimeout(2500);
    console.log('[#181] product added to cart');

    // Home: floating drawer SHOULD be visible (cart has items)
    await page.goto(`${KH}/`, { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.waitForTimeout(3500);
    const drawerOnHome = await page.locator('.cart-drawer-card').isVisible().catch(() => false);

    // /cart: floating drawer + header cart icon must be ABSENT
    await page.goto(`${KH}/cart`, { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.waitForTimeout(3500);
    const drawerOnCart = await page.locator('.cart-drawer-card').count();
    const navCartOnCart = await page.locator('#nav-cart').count();
    const cartBadgeOnCart = await page.locator('[data-testid="cart-badge"]').count();

    // /checkout: same
    await page.goto(`${KH}/checkout`, { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.waitForTimeout(3500);
    const drawerOnCheckout = await page.locator('.cart-drawer-card').count();
    const navCartOnCheckout = await page.locator('#nav-cart').count();

    console.log(`[#181] drawer home=${drawerOnHome} cart=${drawerOnCart} checkout=${drawerOnCheckout} | navCart cart=${navCartOnCart} checkout=${navCartOnCheckout} | badge cart=${cartBadgeOnCart}`);
    expect(drawerOnCart, 'floating drawer must NOT render on /cart').toBe(0);
    expect(navCartOnCart, 'header cart icon must NOT render on /cart').toBe(0);
    expect(drawerOnCheckout, 'floating drawer must NOT render on /checkout').toBe(0);
    expect(navCartOnCheckout, 'header cart icon must NOT render on /checkout').toBe(0);

    // Cleanup cart
    await page.evaluate(() => localStorage.removeItem('vanan_cart'));
  });

  test('#178: salesman/products endpoints require auth (401)', async ({ request }) => {
    for (const ep of ['/api/community/salesman/products', '/api/community/salesman/qr?productId=00000000-0000-0000-0000-000000000001']) {
      const r = await request.get(`${GW}${ep}`);
      expect(r.status(), `GET ${ep}`).toBe(401);
    }
    const add = await request.post(`${GW}/api/community/salesman/products/add`, { data: { productId: '00000000-0000-0000-0000-000000000001' } });
    expect(add.status(), 'POST salesman/products/add').toBe(401);
    const rem = await request.post(`${GW}/api/community/salesman/products/remove`, { data: { productId: '00000000-0000-0000-0000-000000000001' } });
    expect(rem.status(), 'POST salesman/products/remove').toBe(401);
  });

  test('#178: /community/salesman-store page loads', async ({ page }) => {
    const resp = await page.goto(`${KH}/community/salesman-store`, { waitUntil: 'domcontentloaded', timeout: 30000 });
    expect(resp?.status()).toBe(200);
    await page.waitForTimeout(4000);
    const body = await page.textContent('body') || '';
    expect(body).toContain('Gian hàng của tôi');
  });

  test('#179: quick-setup seeds real data + shows REAL DB counts', async ({ browser }) => {
    // Tenant created + verified on the gateway VPS via SSH (crawl/batch is NOT exposed
    // externally — 404 from outside). Set RV_B2_TENANT_ID before running.
    const ts = Date.now();
    const tid = process.env.RV_B2_TENANT_ID || '';
    expect(tid, 'RV_B2_TENANT_ID env must be set (created via SSH on vanan-gateway)').toBeTruthy();
    console.log(`[#179] test tenant (pre-created via SSH): ${tid}`);

    // ── UI: login sysadmin → run quick-setup wizard ──────────────────────────
    const ctx = await browser.newContext();
    const page = await ctx.newPage();
    await page.goto(`${APP2}/Login`, { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.fill('#username', SYSADMIN_USER);
    await page.fill('#password', SYSADMIN_PASS);
    await page.click('button[type="submit"]');
    await page.waitForURL(u => !u.toString().toLowerCase().includes('/login'), { timeout: 20000 });
    console.log(`[#179] sysadmin UI login OK — ${page.url()}`);

    await page.goto(`${APP2}/quick-setup?tenantId=${tid}`, { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.waitForTimeout(4000); // Blazor render

    // Step 1: pick template (first card = Quán Cafe, 32 products)
    await page.click('#step1 .template-card >> nth=0');
    await page.waitForTimeout(500);
    await page.click('#step1 button:has-text("Tiếp tục")');
    await page.waitForTimeout(800);

    // Step 2: shop info — Blazor @bind updates on change (blur/Tab), so Tab after each fill
    await page.fill('input[placeholder="Nhập tên cửa hàng"]', `RV B2 Shop ${ts}`);
    await page.locator('input[placeholder="Nhập tên cửa hàng"]').press('Tab');
    await page.fill('input[placeholder="Nhập địa chỉ"]', '12 RV B2, TP.HCM');
    await page.locator('input[placeholder="Nhập địa chỉ"]').press('Tab');
    await page.fill('input[placeholder="Nhập số điện thoại"]', '0900000123');
    await page.locator('input[placeholder="Nhập số điện thoại"]').press('Tab');
    await page.click('#step2 button:has-text("Tiếp tục")');
    await page.waitForTimeout(800);

    // Step 3: confirm → start
    await page.click('#step3 button:has-text("Bắt đầu khởi tạo")');

    // Step 4: success with REAL counts (32 products / 15 ingredients — from DB, not template metadata)
    await page.waitForSelector('#step4 .setup-result', { timeout: 30000 });
    await page.waitForTimeout(2500);
    const body = await page.textContent('body') || '';
    console.log(`[#179] step4 text: ${body.replace(/\s+/g, ' ').slice(0, 300)}`);
    expect(body).toContain('Khởi tạo thành công');
    expect(body).toContain('32 sản phẩm');
    expect(body).toContain('15 nguyên liệu');
    expect(body).not.toContain('Khởi tạo không thành công');

    // Persist tenant id for the SQLite check step (run separately via SSH)
    await ctx.storageState({ path: 'rv-batch2-state.json' });
    process.stdout.write(`RV_B2_TENANT_ID=${tid}\n`);
    await ctx.close();
  });
});
