// ─────────────────────────────────────────────────────────────────────────────
// RV SWEEP — Personalized DidYouMean W1 (2026-11-28) — PRODUCTION
// SRS: docs/specs/SRS_Search_Personalized_DidYouMean_v1.0.md (v1.1 client-side)
// Sweep-first: thu TOÀN BỘ FAIL 1 lần. L1 API → L2 WASM markers → L3/L4 UI flow.
// NOTE: featured catalog prod trống (recommended+search = 0) → DidYouMean chip
// product-scope KHÔNG thể fire → record-path seed localStorage trực tiếp,
// vẫn verify đủ: resolve-before-search → note "đã học" → request rewrite →
// undo → forget → re-search raw. Record-on-accept được bUnit cover (5/5 pass).
// Env: KHACHLINK_URL (default commienphi), API_URL (default api2.khachvip.online)
// ─────────────────────────────────────────────────────────────────────────────
import { chromium } from 'playwright';
import { writeFileSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPORT = join(HERE, '..', '..', '.devin', 'pdm-rv-report.json');
const KHACHLINK = process.env.KHACHLINK_URL || 'https://commienphi.timlathay.com';
const API = process.env.API_URL || 'https://api2.khachvip.online';

// Mirror SearchTextNormalizer.Normalize: lowercase → fold dấu → collapse ws.
const norm = s => s.toLowerCase().replace(/đ/g, 'd')
    .normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/\s+/g, ' ').trim();

const results = [];
const check = (n, p, d = '') => {
    results.push({ n, p: !!p, d: String(d).slice(0, 300) });
    console.log(`[${p ? 'PASS' : 'FAIL'}] ${n}${d ? ' — ' + String(d).slice(0, 300) : ''}`);
};

// ── L1: API-level ──────────────────────────────────────────────────────────
let apiOk = false, didYouMeanFieldPresent = false, catalogEmpty = false;
try {
    const r = await fetch(`${API}/api/catalog/search?q=tra&page=1&pageSize=5`);
    const j = await r.json();
    apiOk = r.ok;
    didYouMeanFieldPresent = Object.hasOwn(j, 'didYouMean');
    catalogEmpty = (j.totalCount ?? -1) === 0;
} catch (e) { console.log('L1 fetch error:', e.message); }
check('L1 catalog search API reachable', apiOk);
check('L1 response exposes didYouMean field (contract intact)', didYouMeanFieldPresent);
check('L1 featured catalog populated', !catalogEmpty,
    'totalCount=0 → DidYouMean chip không thể fire trên prod (data limitation, không phải code bug)');

// ── L2: WASM markers ───────────────────────────────────────────────────────
try {
    const r = await fetch(`${KHACHLINK}/_framework/VanAn.KhachLink.wasm`);
    const buf = Buffer.from(await r.arrayBuffer());
    check('L2 WASM contains SearchCorrectionStore type', buf.includes('SearchCorrectionStore'),
        `${buf.length} bytes`);
} catch (e) { check('L2 WASM fetch', false, e.message); }

// ── L3/L4: UI flow — seed correction → resolve/note/rewrite/undo ───────────
const RAW = 'ta sua';             // câu người dùng gõ (speech-impaired scenario)
const LEARNED = 'trà sữa';        // suggestion đã accept trước đó
const normKey = norm(RAW);        // "ta sua"

const consoleErrors = [], pageErrors = [], searchUrls = [];
const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ bypassCSP: true, ignoreHTTPSErrors: true });
const page = await context.newPage();
page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
page.on('pageerror', e => pageErrors.push(String(e)));
page.on('request', req => { if (req.url().includes('/api/catalog/search')) searchUrls.push(req.url()); });

// Seed correction giống hệt ApplyDidYouMean → RecordAsync ghi:
// { v:1, scopes:{ product:{ "<norm>":{ c,d,n,t,s } } } }
const seed = {
    v: 1,
    scopes: {
        product: {
            [normKey]: {
                c: norm(LEARNED), d: LEARNED, n: 1,
                t: Date.now(), s: 'voice',
            },
        },
    },
};
await page.addInitScript(seedJson => {
    try { localStorage.setItem('vanan_search_corrections', seedJson); } catch {}
}, JSON.stringify(seed));

await page.goto(`${KHACHLINK}/products`, { waitUntil: 'domcontentloaded' });
const input = page.locator('#product-search-input');
try { await input.waitFor({ timeout: 45000 }); } catch {}
check('L3 products page + search input render', await input.count() > 0);

if (await input.count() > 0) {
    const readCorrections = () => page.evaluate(() => {
        try { return JSON.parse(localStorage.getItem('vanan_search_corrections') || 'null'); }
        catch { return 'PARSE_ERR'; }
    });
    const seedOk = (await readCorrections())?.scopes?.product?.[normKey]?.d === LEARNED;
    check('L4 correction seed persisted in localStorage', seedOk);

    // Step 1: nhập câu đã học → auto-resolve (note + request rewrite)
    searchUrls.length = 0;
    await input.fill(RAW);
    await input.press('Enter');
    const note = page.locator('.search-learned-note');
    try { await note.waitFor({ timeout: 25000 }); } catch {}
    check('L4 learned note shows on learned input', await note.count() > 0,
        await note.count() ? (await note.innerText()).slice(0, 200) : 'note không xuất hiện');
    const rewritten = searchUrls.some(u => u.includes(`q=${encodeURIComponent(LEARNED)}`));
    check('L4 request rewritten with corrected q', rewritten,
        searchUrls[searchUrls.length - 1] || 'no /api/catalog/search request captured');
    const inputVal = await input.inputValue();
    check('L4 input shows corrected keyword', inputVal.trim() === LEARNED, `input="${inputVal}"`);
    await page.screenshot({ path: 'rv-pdm-learned.png', fullPage: true });

    // Step 2: Hoàn tác → forget entry + re-search raw
    const undo = page.locator('.search-learned-undo');
    check('L4 undo button present', await undo.count() > 0);
    if (await undo.count() > 0) {
        searchUrls.length = 0;
        await undo.first().click();
        await page.waitForTimeout(5000);
        const after = await readCorrections();
        check('L4 undo removes correction entry',
            !after?.scopes?.product?.[normKey],
            `after undo: ${JSON.stringify(after?.scopes?.product?.[normKey])}`);
        check('L4 learned note hidden after undo', await note.count() === 0);
        const rawResent = searchUrls.some(u => u.includes(`q=${encodeURIComponent(RAW)}`));
        check('L4 undo re-searches raw query', rawResent,
            searchUrls[searchUrls.length - 1] || 'no request captured');
        await page.screenshot({ path: 'rv-pdm-undo.png', fullPage: true });
    }

    // Step 3: câu chưa học → KHÔNG rewrite, không note
    searchUrls.length = 0;
    await input.fill('mot hai ba bon nam');
    await input.press('Enter');
    await page.waitForTimeout(5000);
    check('L4 unlearned query does NOT rewrite', 
        searchUrls.some(u => u.includes(encodeURIComponent('mot hai ba bon nam'))) &&
        !searchUrls.some(u => u.includes(`q=${encodeURIComponent(LEARNED)}`)),
        searchUrls[searchUrls.length - 1]);
    check('L4 no learned note on unlearned query', await note.count() === 0);

    check('L3 no pageerror', pageErrors.length === 0, pageErrors[0]);
    const realErrs = consoleErrors.filter(e =>
        !/favicon|net::|Failed to load resource/i.test(e));
    check('L3 no console errors', realErrs.length === 0, realErrs[0]);
}
await browser.close();

const pass = results.filter(r => r.p).length;
const fail = results.filter(r => !r.p);
console.log(`\n═══ SWEEP: ${pass}/${results.length} PASS, ${fail.length} FAIL ═══`);
mkdirSync(dirname(REPORT), { recursive: true });
writeFileSync(REPORT, JSON.stringify({ when: new Date().toISOString(), results }, null, 2));
process.exit(fail.length ? 1 : 0);
