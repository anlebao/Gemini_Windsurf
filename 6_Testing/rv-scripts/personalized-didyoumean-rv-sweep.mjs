// ─────────────────────────────────────────────────────────────────────────────
// RV SWEEP — Personalized DidYouMean W2 (2026-11-28) — PRODUCTION
// W2 = tenant scope wiring (StoreFinder KL+Dir, Home Dir) + search load hardening
// (A1 CT abort · A2 min-length · A3 client cache · B1 ResponseCache).
// Tenant scope CÓ data prod → full loop kể cả record-path (chip accept → learned).
// Env: KHACHLINK_URL (default commienphi), DIRECTORY_URL (timlathay),
//      API_URL (default api2.khachvip.online)
// ─────────────────────────────────────────────────────────────────────────────
import { chromium } from 'playwright';
import { writeFileSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPORT = join(HERE, '..', '..', '.devin', 'pdm-w2-rv-report.json');
const KHACHLINK = process.env.KHACHLINK_URL || 'https://commienphi.timlathay.com';
const DIRECTORY = process.env.DIRECTORY_URL || 'https://timlathay.com';
const API = process.env.API_URL || 'https://api2.khachvip.online';

// Mirror SearchTextNormalizer.Normalize.
const norm = s => s.toLowerCase().replace(/đ/g, 'd')
    .normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/\s+/g, ' ').trim();

const results = [];
const check = (n, p, d = '') => {
    results.push({ n, p: !!p, d: String(d).slice(0, 300) });
    console.log(`[${p ? 'PASS' : 'FAIL'}] ${n}${d ? ' — ' + String(d).slice(0, 300) : ''}`);
};

// Misspell candidates cho tenant search — lấy cặp đầu tiên trả didYouMean.
const MISSPELLS = [
    'antaam', // verified prod: → "An Tâm" (fuzzy + didYouMean)
    'caphe tan qui', 'caffe tan qui', 'cafe tin qui', 'quan cafe tan',
    'ann tam', 'an tamm', 'cafe tan qui',
];

// ── L1: API-level ──────────────────────────────────────────────────────────
let pair = null, apiOk = false, cacheHeader = null;
for (const m of MISSPELLS) {
    try {
        const r = await fetch(`${API}/api/tenants/search?name=${encodeURIComponent(m)}`);
        if (!r.ok) continue;
        apiOk = true;
        cacheHeader ??= r.headers.get('cache-control');
        const j = await r.json();
        if (j.didYouMean) { pair = { misspell: m, suggestion: j.didYouMean }; break; }
    } catch (e) { console.log('L1 fetch error:', e.message); }
}
check('L1 tenants search API reachable', apiOk);
check('L1 ResponseCache on /api/tenants/search (B1)',
    /max-age=60/i.test(cacheHeader ?? ''), `cache-control="${cacheHeader}"`);
check('L1 DidYouMean tenant pair discovered', !!pair,
    pair ? `"${pair.misspell}" → "${pair.suggestion}"` : `thử ${MISSPELLS.length} misspell`);

// ── L2: WASM markers ───────────────────────────────────────────────────────
try {
    const r = await fetch(`${KHACHLINK}/_framework/VanAn.KhachLink.wasm`);
    const buf = Buffer.from(await r.arrayBuffer());
    check('L2 WASM contains SearchResultCache type', buf.includes('SearchResultCache'),
        `${buf.length} bytes`);
} catch (e) { check('L2 WASM fetch', false, e.message); }
try {
    const r = await fetch(`${KHACHLINK}/_framework/VanAn.UI.Platform.wasm`);
    const buf = Buffer.from(await r.arrayBuffer());
    check('L2 UI.Platform WASM contains SearchCorrectionStore (moved)',
        buf.includes('SearchCorrectionStore'), `${buf.length} bytes`);
} catch (e) { check('L2 UI.Platform WASM fetch', false, e.message); }

// ── L3/L4: KhachLink StoreFinder — full loop + load checks ─────────────────
const consoleErrors = [], pageErrors = [], tenantReqs = [];
const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ bypassCSP: true, ignoreHTTPSErrors: true });
const page = await context.newPage();
page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
page.on('pageerror', e => pageErrors.push(String(e)));
page.on('request', req => {
    if (req.url().includes('/api/tenants/search')) tenantReqs.push(req.url());
});

// Seed pair FRESH (chưa từng search trong sweep) cho rewrite/undo checks —
// tránh false-negative do A3 client cache (URL đã fetch → cache hit → 0 request).
const SEED_MISS = 'xyzq';
const SEED_SUGG = 'Cafe Tân Quy';
await page.addInitScript(s => {
    try { localStorage.setItem('vanan_search_corrections', s); } catch {}
}, JSON.stringify({ v: 1, scopes: { tenant: { [SEED_MISS]: { c: 'cafe tan quy', d: SEED_SUGG, n: 1, t: Date.now(), s: 'typed' } } } }));

await page.goto(`${KHACHLINK}/stores`, { waitUntil: 'domcontentloaded' });
const input = page.locator('#search-input');
try { await input.waitFor({ timeout: 45000 }); } catch {}
await page.waitForTimeout(3000); // WASM boot + initial list-all load
check('L3 stores page + search input render', await input.count() > 0);

if (await input.count() > 0) {
    const readCorr = () => page.evaluate(() => {
        try { return JSON.parse(localStorage.getItem('vanan_search_corrections') || 'null'); }
        catch { return 'PARSE_ERR'; }
    });
    const nameCalls = () => tenantReqs.filter(u => u.includes('name='));

    // ── Load checks (A1/A2/A3) — chạy trước khi localStorage có correction ──
    // A2: 1 ký tự → 0 request name=
    tenantReqs.length = 0;
    await input.fill('c');
    await page.waitForTimeout(800);
    check('L4 A2 min-length: 1 char → no request', nameCalls().length === 0,
        `${nameCalls().length} requests`);

    // A1+A3: typing burst → ít request; repeat query → cache hit
    await input.fill('cafe');
    await page.waitForTimeout(900);          // debounce 300 → request "cafe"
    const afterCafe = nameCalls().length;
    check('L4 debounce: fill "cafe" → 1 request', afterCafe === 1,
        `${afterCafe} requests: ${JSON.stringify(nameCalls())}`);
    // gõ thêm ký tự nhanh (<300ms giữa 2 lần) → coalesce
    await input.fill('cafe t');
    await page.waitForTimeout(120);
    await input.fill('cafe ta');
    await page.waitForTimeout(900);
    const burstCount = nameCalls().length;
    check('L4 typing burst coalesced (≤2 extra requests)',
        burstCount - afterCafe <= 1,
        `${burstCount} total name= reqs: ${JSON.stringify(nameCalls())}`);
    // A3: quay lại "cafe" → cache hit, không request mới
    const beforeCache = nameCalls().length;
    await input.fill('cafe');
    await page.waitForTimeout(900);
    check('L4 A3 client cache: repeat "cafe" → 0 new request',
        nameCalls().length === beforeCache,
        `before=${beforeCache} after=${nameCalls().length}`);

    // ── Full loop tenant scope (nếu pair có) ──
    if (pair) {
        const normKey = norm(pair.misspell);
        // Step 1: search misspell → DidYouMean chip
        await input.fill(pair.misspell);
        await input.press('Enter');
        const chip = page.locator('button.btn-primary', { hasText: pair.suggestion });
        try { await chip.first().waitFor({ timeout: 20000 }); } catch {}
        check('L4 DidYouMean chip shows tenant suggestion', await chip.count() > 0,
            `chip "${pair.suggestion}"`);

        // Step 2: accept → correction ghi vào scopes.tenant
        if (await chip.count() > 0) {
            await chip.first().click();
            await page.waitForTimeout(3500);
            const corr = await readCorr();
            const entry = corr?.scopes?.tenant?.[normKey];
            check('L4 record-path: correction in scopes.tenant',
                entry?.d === pair.suggestion, `key="${normKey}" entry=${JSON.stringify(entry)}`);
        }

        // Step 3: nhập lại misspell → auto-resolve note (recorded entry resolve OK;
        // request có thể cache-hit nên chỉ assert note, không assert network ở đây)
        tenantReqs.length = 0;
        await input.fill('');
        await page.waitForTimeout(600);
        await input.fill(pair.misspell);
        await page.waitForTimeout(1200); // debounce → resolve → search
        const note = page.locator('.search-learned-note');
        check('L4 learned note on repeat input', await note.count() > 0,
            await note.count() ? (await note.innerText()).slice(0, 200) : 'không thấy note');

        // Step 3b: seeded pair FRESH → request rewrite ra network (không trúng cache)
        tenantReqs.length = 0;
        await input.fill('');
        await page.waitForTimeout(600);
        await input.fill(SEED_MISS);
        await page.waitForTimeout(1200);
        check('L4 learned note for seeded pair',
            (await note.innerText().catch(() => '')).includes(SEED_MISS),
            await note.count() ? (await note.innerText()).slice(0, 200) : 'không thấy note');
        const rewritten = nameCalls().some(u =>
            u.includes(`name=${encodeURIComponent(SEED_SUGG)}`));
        check('L4 request rewritten with corrected name', rewritten,
            nameCalls()[nameCalls().length - 1] || 'no request captured');
        await page.screenshot({ path: 'rv-w2-learned.png', fullPage: true });

        // Step 4: Hoàn tác seeded pair → forget + raw re-search (fresh URL → ra network)
        const undo = page.locator('.search-learned-undo');
        if (await undo.count() > 0) {
            tenantReqs.length = 0;
            await undo.first().click();
            await page.waitForTimeout(3000);
            const after = await readCorr();
            check('L4 undo removes correction entry',
                !after?.scopes?.tenant?.[SEED_MISS]);
            check('L4 note hidden after undo', await note.count() === 0);
            const rawResent = nameCalls().some(u =>
                u.includes(`name=${encodeURIComponent(SEED_MISS)}`));
            check('L4 undo re-searches raw query', rawResent,
                nameCalls()[nameCalls().length - 1]);
            await page.screenshot({ path: 'rv-w2-undo.png', fullPage: true });
        } else {
            check('L4 undo button present', false, 'note không có → undo thiếu');
        }
    }
}

// ── Directory (timlathay) — seeded correction → resolve/note/undo ──────────
const dirPage = await context.newPage();
dirPage.on('pageerror', e => pageErrors.push('dir:' + String(e)));
const dirReqs = [];
dirPage.on('request', req => {
    if (req.url().includes('/api/tenants/search')) dirReqs.push(req.url());
});
if (pair) {
    const normKey = norm(pair.misspell);
    const seed = { v: 1, scopes: { tenant: { [normKey]: { c: norm(pair.suggestion), d: pair.suggestion, n: 1, t: Date.now(), s: 'typed' } } } };
    await dirPage.addInitScript(s => {
        try { localStorage.setItem('vanan_search_corrections', s); } catch {}
    }, JSON.stringify(seed));
    await dirPage.goto(`${DIRECTORY}/stores`, { waitUntil: 'networkidle' });
    const dirInput = dirPage.locator('input').first();
    try {
        await dirPage.locator('.vanan-search-pill input, input[aria-label*="cửa hàng" i]').first().waitFor({ timeout: 30000 });
    } catch {}
    const dIn = dirPage.locator('.vanan-search-pill input, input[aria-label*="cửa hàng" i]').first();
    if (await dIn.count() > 0) {
        dirReqs.length = 0;
        await dIn.fill(pair.misspell);
        await dIn.press('Enter');
        await dirPage.waitForTimeout(4000);
        const dNote = dirPage.locator('.search-learned-note');
        check('L4 Dir StoreFinder learned note', await dNote.count() > 0,
            await dNote.count() ? (await dNote.innerText()).slice(0, 200) : 'không thấy note');
        // SSR: API call chạy server-side → không capture được request trên browser.
        // Assert rewrite qua DOM: result card render tenant được rewrite tới.
        const dCard = dirPage.locator('.store-card', { hasText: pair.suggestion });
        try { await dCard.first().waitFor({ timeout: 10000 }); } catch {}
        check('L4 Dir results rewritten to corrected tenant', await dCard.count() > 0,
            `${await dCard.count()} cards với "${pair.suggestion}"`);
        const dUndo = dirPage.locator('.search-learned-undo');
        if (await dUndo.count() > 0) {
            await dUndo.first().click();
            await dirPage.waitForTimeout(3000);
            check('L4 Dir note hidden after undo', await dNote.count() === 0);
            // Raw re-search server-side → fuzzy didYouMean chip quay lại cho misspell
            const dChip = dirPage.locator('button.btn-primary', { hasText: pair.suggestion });
            try { await dChip.first().waitFor({ timeout: 10000 }); } catch {}
            check('L4 Dir undo re-searches raw (chip returns)', await dChip.count() > 0,
                'didYouMean chip sau undo');
        }
        await dirPage.screenshot({ path: 'rv-w2-dir.png', fullPage: true });
    } else {
        check('L4 Dir StoreFinder input render', false, 'input không tìm thấy');
    }
}

check('L3 no pageerror', pageErrors.length === 0, pageErrors[0]);
const realErrs = consoleErrors.filter(e => !/favicon|net::|Failed to load resource/i.test(e));
check('L3 no console errors', realErrs.length === 0, realErrs[0]);
await browser.close();

const pass = results.filter(r => r.p).length;
const fail = results.filter(r => !r.p);
console.log(`\n═══ W2 SWEEP: ${pass}/${results.length} PASS, ${fail.length} FAIL ═══`);
mkdirSync(dirname(REPORT), { recursive: true });
writeFileSync(REPORT, JSON.stringify({ when: new Date().toISOString(), results }, null, 2));
process.exit(fail.length ? 1 : 0);
