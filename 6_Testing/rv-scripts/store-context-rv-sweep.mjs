// ─────────────────────────────────────────────────────────────────────────────
// RV SWEEP — Store Context (back-to-store + "Cửa Hàng Của Bạn") — PRODUCTION
// Task: docs/AI/tasks/task_store_context_back_to_store.md
// Sweep-first: thu TOÀN BỘ FAIL 1 lần. L1 API → L2 WASM markers → L4 UI flow.
// Env: KHACHLINK_URL (default timlathay), API_URL (default api2.khachvip.online),
//      STORE_TENANT_ID (default An Tâm), STORE_CONTEXT_ORDER_ID (optional).
// ─────────────────────────────────────────────────────────────────────────────
import { chromium } from 'playwright';
import { writeFileSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPORT = join(HERE, '..', '..', '.devin', 'store-context-rv-report.json');
const KHACHLINK = process.env.KHACHLINK_URL || 'https://commienphi.timlathay.com';
const API = process.env.API_URL || 'https://api2.khachvip.online';
const TENANT = (process.env.STORE_TENANT_ID || 'e2d91cdf-d3c4-4399-b96e-e772a7a41630').toLowerCase();
const GATED_DOMAIN = process.env.GATED_DOMAIN || 'shopai.timlathay.com'; // ShowCampaigns=false
const ORDER_ID = process.env.STORE_CONTEXT_ORDER_ID || '';

const results = [];
const check = (n, p, d = '') => {
    results.push({ n, p: !!p, d: String(d).slice(0, 300) });
    console.log(`[${p ? 'PASS' : 'FAIL'}] ${n}${d ? ' — ' + String(d).slice(0, 300) : ''}`);
};

// ── L1: API-level ──────────────────────────────────────────────────────────
try {
    const plain = await fetch(`${API}/api/platform/khachlink-home-settings`);
    const base = await plain.json();
    check('L1 home-settings GET anonymous 200 + campaign on', plain.ok &&
        (base.home_CampaignSection_Enabled ?? base.Home_CampaignSection_Enabled) === true);

    const gated = await fetch(`${API}/api/platform/khachlink-home-settings`,
        { headers: { 'X-KhachLink-Domain': GATED_DOMAIN } });
    const eff = await gated.json();
    check('L1 home-settings gated: home campaign OFF under gated domain', gated.ok &&
        (eff.home_CampaignSection_Enabled ?? eff.Home_CampaignSection_Enabled) === false,
        `domain=${GATED_DOMAIN} resp=${JSON.stringify(eff)}`);
    // Untouched fields must equal base row (gate chỉ AND campaign+store).
    check('L1 home-settings gated: featured/social/nav unchanged vs base', gated.ok &&
        (eff.home_FeaturedSection_Enabled ?? eff.Home_FeaturedSection_Enabled) ===
            (base.home_FeaturedSection_Enabled ?? base.Home_FeaturedSection_Enabled) &&
        (eff.home_SocialHub_Enabled ?? eff.Home_SocialHub_Enabled) ===
            (base.home_SocialHub_Enabled ?? base.Home_SocialHub_Enabled) &&
        (eff.showNavMenu ?? eff.ShowNavMenu) === (base.showNavMenu ?? base.ShowNavMenu));
} catch (e) { check('L1 home-settings fetch', false, e.message); }

// ── L2: WASM markers — string literals trong .NET asm là UTF-16LE; type names là ASCII
try {
    const r = await fetch(`${KHACHLINK}/_framework/VanAn.KhachLink.wasm`);
    const buf = Buffer.from(await r.arrayBuffer());
    const utf16 = s => Buffer.from(s, 'utf16le');
    check('L2 WASM contains khachlink_last_store marker (UTF-16LE)',
        r.ok && buf.includes(utf16('khachlink_last_store')), `${buf.length} bytes`);
    check('L2 WASM contains BackToStoreChip type marker',
        buf.includes('BackToStoreChip') || buf.includes(utf16('Quay l')));
} catch (e) { check('L2 WASM fetch', false, e.message); }

// ── L4: UI flow (Playwright) ───────────────────────────────────────────────
const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ bypassCSP: true, ignoreHTTPSErrors: true });
const page = await context.newPage();
const pageErrors = [];
page.on('pageerror', e => pageErrors.push(String(e)));

// Step 1: visit storefront → context recorded (both keys)
await page.goto(`${KHACHLINK}/store/by-id/${TENANT}`, { waitUntil: 'domcontentloaded' });
await page.waitForFunction(() => localStorage.getItem('khachlink_last_store') != null,
    { timeout: 60000 }).catch(() => null);
const visit = await page.evaluate(() => {
    try { return JSON.parse(localStorage.getItem('khachlink_last_store') || 'null'); }
    catch { return 'PARSE_ERR'; }
});
const legacy = await page.evaluate(() => localStorage.getItem('khachlink_last_tenant_id'));
check('L4 store visit → khachlink_last_store JSON recorded',
    visit?.t?.toLowerCase() === TENANT, JSON.stringify(visit));
check('L4 store visit → legacy khachlink_last_tenant_id still written',
    legacy?.toLowerCase() === TENANT, legacy);

// Step 2: home → "Cửa Hàng Của Bạn" + "Vào cửa hàng" deep-link
await page.goto(`${KHACHLINK}/`, { waitUntil: 'domcontentloaded' });
const section = page.locator('.store-section');
try { await section.waitFor({ timeout: 60000 }); } catch {}
check('L4 home shows "Cửa Hàng Của Bạn" after store visit', await section.count() > 0);
// Chờ loading store-info xong — nút chỉ render sau khi _storeLoading=false
const enterBtn = section.locator('text=Vào cửa hàng');
try { await enterBtn.first().waitFor({ timeout: 60000 }); } catch {}
check('L4 "Vào cửa hàng" button present', await enterBtn.count() > 0);
if (await enterBtn.count() > 0) {
    await enterBtn.first().click();
    await page.waitForURL(/\/store\//, { timeout: 15000 }).catch(() => null);
    check('L4 "Vào cửa hàng" navigates to storefront', /\/store\//.test(page.url()), page.url());
}
await page.screenshot({ path: 'rv-store-context-home.png', fullPage: true });

// Step 3: /my-loyalty → back chip → storefront
await page.goto(`${KHACHLINK}/my-loyalty`, { waitUntil: 'domcontentloaded' });
const chip = page.locator('[data-testid="back-to-store"]');
try { await chip.waitFor({ timeout: 60000 }); } catch {}
check('L4 /my-loyalty shows back-to-store chip', await chip.count() > 0);
if (await chip.count() > 0) {
    await chip.locator('button').first().click();
    await page.waitForURL(/\/store\//, { timeout: 15000 }).catch(() => null);
    check('L4 back-chip navigates to storefront', /\/store\//.test(page.url()), page.url());
}

// Step 4: /order-tracking → "Tiếp tục mua" (optional — cần ORDER_ID thật)
if (ORDER_ID) {
    await page.goto(`${KHACHLINK}/order-tracking/${ORDER_ID}`, { waitUntil: 'domcontentloaded' });
    const btn = page.locator('[data-testid="continue-shopping"]');
    try { await btn.waitFor({ timeout: 60000 }); } catch {}
    check('L4 order-tracking shows "Tiếp tục mua"', await btn.count() > 0);
    if (await btn.count() > 0) {
        await btn.locator('button').first().click();
        await page.waitForURL(/\/store\//, { timeout: 15000 }).catch(() => null);
        check('L4 "Tiếp tục mua" navigates to storefront', /\/store\//.test(page.url()), page.url());
    }
} else {
    check('L4 order-tracking "Tiếp tục mua"', true, 'SKIPPED — STORE_CONTEXT_ORDER_ID not set');
}

check('L4 no page errors during flow', pageErrors.length === 0,
    pageErrors.slice(0, 2).join(' | '));

await browser.close();

// ── Report ─────────────────────────────────────────────────────────────────
const pass = results.filter(r => r.p).length;
console.log(`\n=== STORE CONTEXT RV: ${pass}/${results.length} PASS ===`);
mkdirSync(dirname(REPORT), { recursive: true });
writeFileSync(REPORT, JSON.stringify({ when: new Date().toISOString(), khachlink: KHACHLINK, api: API, results }, null, 2));
process.exit(pass === results.length ? 0 : 1);
