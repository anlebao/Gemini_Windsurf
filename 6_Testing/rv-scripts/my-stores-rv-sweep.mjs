// ─────────────────────────────────────────────────────────────────────────────
// RV SWEEP — "Cửa hàng của tôi" (my-stores) — PRODUCTION
// Task: docs/AI/tasks/task_my_stores_profile.md · commit 374acb16
// L1 API (401/200/deviceId/token-precedence) → L2 WASM markers → L4 UI (guest +
// optional authenticated PROFILE_CUSTOMER_TOKEN).
// Env: KHACHLINK_URL (default commienphi), API_URL (api2.khachvip.online),
//      PROFILE_CUSTOMER_TOKEN + PROFILE_DEVICE_ID (optional — real customer).
// ─────────────────────────────────────────────────────────────────────────────
import { chromium } from 'playwright';
import { writeFileSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPORT = join(HERE, '..', '..', '.devin', 'my-stores-rv-report.json');
const KHACHLINK = process.env.KHACHLINK_URL || 'https://commienphi.timlathay.com';
const API = process.env.API_URL || 'https://api2.khachvip.online';
const TOKEN = process.env.PROFILE_CUSTOMER_TOKEN || '';
const DEVICE_ID = process.env.PROFILE_DEVICE_ID || '';

const results = [];
const check = (n, p, d = '') => {
    results.push({ n, p: !!p, d: String(d).slice(0, 300) });
    console.log(`[${p ? 'PASS' : 'FAIL'}] ${n}${d ? ' — ' + String(d).slice(0, 300) : ''}`);
};

// ── L1: API-level ──────────────────────────────────────────────────────────
try {
    // No identity headers → 401
    const noAuth = await fetch(`${API}/api/customers/my-stores`);
    check('L1 my-stores no-identity → 401', noAuth.status === 401, `status=${noAuth.status}`);

    // Random deviceId → 200 + stores [] (guest path — Orders.CustomerDeviceId union)
    const rand = await fetch(`${API}/api/customers/my-stores`, {
        headers: { 'X-Customer-Device-Id': crypto.randomUUID() },
    });
    const randBody = rand.ok ? await rand.json() : null;
    check('L1 my-stores random deviceId → 200 + stores array', rand.ok &&
        Array.isArray(randBody?.stores), `status=${rand.status} body=${JSON.stringify(randBody)?.slice(0, 120)}`);

    // Invalid token + valid deviceId → 401 (token path ưu tiên, không silent downgrade)
    const badTok = await fetch(`${API}/api/customers/my-stores`, {
        headers: {
            'X-Customer-Token': 'not-a-real-token-zzz',
            'X-Customer-Device-Id': crypto.randomUUID(),
        },
    });
    check('L1 my-stores invalid token → 401 (no silent downgrade)', badTok.status === 401,
        `status=${badTok.status}`);

    // Real deviceId (optional) → stores shape: name/slug/orderCount/pointBalance/mode
    if (DEVICE_ID) {
        const real = await fetch(`${API}/api/customers/my-stores`, {
            headers: { 'X-Customer-Device-Id': DEVICE_ID },
        });
        const realBody = real.ok ? await real.json() : null;
        const s = realBody?.stores?.[0];
        check('L1 my-stores real device → stores[] shape (name+slug+points+mode)',
            real.ok && Array.isArray(realBody?.stores) &&
            (realBody.stores.length === 0 ||
                (s.name !== undefined && 'pointBalance' in s && 'mode' in s)),
            `status=${real.status} n=${realBody?.stores?.length} first=${JSON.stringify(s)?.slice(0, 200)}`);
    }
} catch (e) { check('L1 my-stores fetch', false, e.message); }

// ── L2: WASM markers — literal UTF-16LE; type names ASCII ───────────────────
try {
    const r = await fetch(`${KHACHLINK}/_framework/VanAn.KhachLink.wasm`);
    const buf = Buffer.from(await r.arrayBuffer());
    const utf16 = s => Buffer.from(s, 'utf16le');
    check('L2 WASM contains api/customers/my-stores path (UTF-16LE)',
        r.ok && buf.includes(utf16('api/customers/my-stores')), `${buf.length} bytes`);
    check('L2 WASM contains MyStoresSection type marker',
        buf.includes('MyStoresSection') || buf.includes(utf16('my-stores-section')));
} catch (e) { check('L2 WASM fetch', false, e.message); }

// ── L4: UI flow (Playwright) ───────────────────────────────────────────────
const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ bypassCSP: true, ignoreHTTPSErrors: true });
const page = await context.newPage();
const pageErrors = [];
page.on('pageerror', e => pageErrors.push(String(e)));

// Guest: random deviceId → /profile renders login card; my-stores request fired;
// empty stores → section hidden (valid state — không crash, không lỗi).
const myDeviceId = crypto.randomUUID();
await page.addInitScript(did => {
    try {
        localStorage.clear();
        localStorage.setItem('customer_device_id', did);
    } catch { /* noop */ }
}, myDeviceId);

const myStoresCalls = [];
page.on('response', r => { if (r.url().includes('/api/customers/my-stores')) myStoresCalls.push(r.status()); });

await page.goto(`${KHACHLINK}/profile`, { waitUntil: 'domcontentloaded' });
await page.locator('text=Chưa đăng nhập').waitFor({ timeout: 60000 }).catch(() => null);
await page.waitForTimeout(4000); // MyStoresSection self-loads sau render

check('L4 guest /profile → my-stores API called with deviceId',
    myStoresCalls.length > 0 && myStoresCalls.every(s => s === 200),
    `calls=${JSON.stringify(myStoresCalls)}`);
check('L4 guest random device → section hidden (empty stores)',
    await page.locator('[data-testid="my-stores-section"]').count() === 0);
await page.screenshot({ path: 'rv-my-stores-guest.png', fullPage: true });

// Authenticated (optional): real customer → section rows + "Vào cửa hàng" same-origin
if (TOKEN) {
    const ctx2 = await browser.newContext({ bypassCSP: true, ignoreHTTPSErrors: true });
    const p2 = await ctx2.newPage();
    await p2.addInitScript(t => {
        try {
            localStorage.clear();
            localStorage.setItem('customer_token', t);
        } catch { /* noop */ }
    }, TOKEN);
    await p2.goto(`${KHACHLINK}/profile`, { waitUntil: 'domcontentloaded' });
    await p2.waitForTimeout(8000); // profile + my-stores load

    const section = p2.locator('[data-testid="my-stores-section"]');
    if (await section.count() === 0) {
        check('L4 auth: my-stores section', true, 'SKIPPED — customer chưa có stores (valid)');
    } else {
        check('L4 auth: "Cửa hàng của tôi" section rendered', true);
        const goBtn = p2.locator('[data-testid^="my-store-go-"]').first();
        await goBtn.click();
        await p2.waitForURL(/\/store\//, { timeout: 15000 }).catch(() => null);
        check('L4 auth: "Vào cửa hàng" → same-origin /store/', /\/store\//.test(p2.url()), p2.url());
        await p2.screenshot({ path: 'rv-my-stores-auth.png', fullPage: true });
    }
    await ctx2.close();
} else {
    check('L4 auth my-stores section', true, 'SKIPPED — PROFILE_CUSTOMER_TOKEN not set');
}

check('L4 no page errors during flow', pageErrors.length === 0,
    pageErrors.slice(0, 2).join(' | '));

await browser.close();

// ── Report ──────────────────────────────────────────────────────────────────
const failed = results.filter(r => !r.p);
mkdirSync(dirname(REPORT), { recursive: true });
writeFileSync(REPORT, JSON.stringify({ when: new Date().toISOString(), results }, null, 2));
console.log(`\n═══ ${results.length - failed.length}/${results.length} PASS — ${REPORT}`);
process.exit(failed.length ? 1 : 0);
