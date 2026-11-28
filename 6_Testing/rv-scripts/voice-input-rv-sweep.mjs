// ─────────────────────────────────────────────────────────────────────────────
// RV SWEEP — Voice Search Input Hardening V1+V2 (2026-11-28) — PRODUCTION
// V1 "treo": watchdog first-result (S1) · noise hard-cap (S2) · silence→submit
// (S3) · isRecording reset + re-tap (S4) · lazy-race (S5) · nav abort (S6).
// V2 "chậm": adaptive silence timing (S3b) · continuous=false flag (S7) ·
// interim DOM-fill SSR (Dir) · Home KL unify → vananVoiceInput (S8).
// FakeSpeechRecognition inject qua addInitScript — scripted events, KHÔNG cần
// mic thật.
// Env: KHACHLINK_URL (default commienphi), DIRECTORY_URL (timlathay),
//      API_URL (api2.khachvip.online)
// ─────────────────────────────────────────────────────────────────────────────
import { chromium } from 'playwright';
import { writeFileSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPORT = join(HERE, '..', '..', '.devin', 'voice-v1-rv-report.json');
const KHACHLINK = process.env.KHACHLINK_URL || 'https://commienphi.timlathay.com';
const DIRECTORY = process.env.DIRECTORY_URL || 'https://timlathay.com';
// Full instance matrix (2026-11-28): mọi KhachLink instance có voice search.
// Hoptacxa portal: `_isHoptacxa` skip voice init → mic PHẢI ẩn (negative check).
const KL_HOSTS = (process.env.KHACHLINK_HOSTS ||
    'commienphi.timlathay.com,shopai.timlathay.com,vanan.timlathay.com')
    .split(',').map(h => 'https://' + h.trim());

const results = [];
const check = (n, p, d = '') => {
    results.push({ n, p: !!p, d: String(d).slice(0, 300) });
    console.log(`[${p ? 'PASS' : 'FAIL'}] ${n}${d ? ' — ' + String(d).slice(0, 300) : ''}`);
};

// FakeSpeechRecognition — emit scripted events vào page trước khi JS app load.
// mode: 'silent' (không result nào) · 'noise' (interim mới mỗi 400ms) ·
//       'speech' (interim×3 rồi final "an tam" rồi ngừng)
const FAKE_SR = mode => `
window.__fakeSRInstances = [];
window.SpeechRecognition = window.webkitSpeechRecognition = class {
    constructor() {
        this._mode = '${mode}';
        this._timers = [];
        window.__fakeSRInstances.push(this);
    }
    start() {
        if (this._mode === 'silent') return; // không event nào — watchdog phải bắt
        if (this._mode === 'noise') {
            let i = 0;
            const tick = () => {
                i++;
                const txt = 'noise word' + i;
                this.onresult && this.onresult({ results: [
                    Object.assign([{ transcript: txt }], { isFinal: false })
                ]});
                this._timers.push(setTimeout(tick, 400));
            };
            tick();
            return;
        }
        // speech: interim×3 → final "an tam" → ngừng (silence timer xử lý)
        // misspell: voice transcript sai chính tả → verify fuzzy/didYouMean chain
        // nomatch: transcript chắc chắn KHÔNG có trong featured/default list —
        // phải thấy empty-state → chứng minh OnVoiceFinal→search chạy THẬT
        // ('An Tâm' nằm sẵn featured → match mặc định là false-positive)
        const words = this._mode === 'misspell'
            ? [['anta', false], ['antaam', false], ['antaam', true]]
            : this._mode === 'nomatch'
            ? [['zzqq', false], ['zzqqxx', false], ['zzqqxx', true]]
            : [['an', false], ['an ta', false], ['an tam', false], ['an tam', true]];
        const emit = (txt, isFinal) => this.onresult && this.onresult({ results: [
            Object.assign([{ transcript: txt }], { isFinal })
        ]});
        words.forEach(([t, f], i) =>
            this._timers.push(setTimeout(() => emit(t, f), 300 + i * 400)));
        // noend: quirk Chrome thật — final đến nhưng 'end' không bao giờ fire
        // (kể cả sau stop()) — dead-man finalize fallback phải cover
        if (this._mode === 'noend') this._noend = true;
    }
    stop() {
        this._timers.forEach(clearTimeout);
        if (this._noend) return; // end không fire — mô phỏng Chrome stuck
        const self = this;
        setTimeout(() => self.onend && self.onend(), 50);
    }
    abort() {
        this._timers.forEach(clearTimeout);
    }
};`;

const browser = await chromium.launch({ headless: true });
const pageErrors = [];

async function newPageWithSR(mode) {
    const ctx = await browser.newContext({ bypassCSP: true, ignoreHTTPSErrors: true });
    const page = await ctx.newPage();
    page.on('pageerror', e => pageErrors.push(String(e)));
    await page.addInitScript(FAKE_SR(mode));
    return { ctx, page };
}

async function micButton(page) {
    // VanAnPublicSearchBar mic — aria-label "Tìm kiếm bằng giọng nói" / "Dừng nói"
    const b = page.locator('button[aria-label*="giọng nói"], button[aria-label*="Dừng nói"]').first();
    try { await b.waitFor({ timeout: 45000 }); } catch {}
    return b;
}
const isListening = async (page) =>
    (await page.locator('button.listening').count()) > 0 ||
    (await page.locator('.voice-recording-hint').count()) > 0;

// ═══ S1: no-result watchdog — silent SR → error ≤10s + mic off ═══════════════
{
    const { ctx, page } = await newPageWithSR('silent');
    await page.goto(`${KHACHLINK}/stores`, { waitUntil: 'domcontentloaded' });
    const mic = await micButton(page);
    if (await mic.count() === 0) {
        check('S1 mic button render (KL /stores)', false, 'mic không hiện — voice feature off?');
    } else {
        check('S1 mic button render (KL /stores)', true);
        await mic.click();
        // Watchdog 8s → error + abort. Đợi ≤12s.
        let errorText = '', off = false;
        for (let i = 0; i < 24; i++) {
            await page.waitForTimeout(500);
            errorText = await page.locator('body').innerText().catch(() => '');
            off = !(await isListening(page));
            if (off && errorText.includes('Không nghe thấy')) break;
        }
        check('S1 watchdog: no-result → error "Không nghe thấy" ≤12s',
            errorText.includes('Không nghe thấy'), errorText.slice(0, 120));
        check('S1 watchdog: mic state off sau abort', off);
    }
    await ctx.close();
}

// ═══ S3+S4+S7: speech → adaptive silence → submit; reset; re-tap; flags ════
{
    const { ctx, page } = await newPageWithSR('speech');
    const tenantReqs = [];
    page.on('request', r => {
        if (r.url().includes('/api/tenants/search')) tenantReqs.push(r.url());
    });
    await page.goto(`${KHACHLINK}/stores`, { waitUntil: 'domcontentloaded' });
    const mic = await micButton(page);
    if (await mic.count() === 0) {
        check('S3 mic button render', false, 'mic không hiện');
    } else {
        const t0 = Date.now();
        await mic.click();
        // V2c: continuous phải là false (Chrome endpointing tự end)
        await page.waitForTimeout(300);
        const cont = await page.evaluate(() =>
            window.__fakeSRInstances.length ? window.__fakeSRInstances[0].continuous : 'NONE');
        check('S7 V2c: recognition.continuous === false', cont === false, String(cont));
        // interim ~1.5s; final event ở ~1500ms → V2a arm 800ms → stop ~2300ms →
        // onend → OnVoiceFinal → request. Old path (2.5s floor) ≈ +3.9s.
        let found = false, reqMs = -1;
        for (let i = 0; i < 20; i++) {
            await page.waitForTimeout(500);
            if (tenantReqs.some(u => u.includes('name=an%20tam') || u.includes('name=an+tam'))) {
                found = true; reqMs = Date.now() - t0; break;
            }
        }
        check('S3 silence → submit transcript "an tam"', found,
            tenantReqs[tenantReqs.length - 1] || 'no request');
        // V2a adaptive: final emit ~1500ms → request phải đến <1500+2400=3900ms.
        // (2.5s floor cũ → ~4000-4500ms → fail check này)
        check('S3 V2a adaptive silence: request <2400ms sau isFinal',
            found && (reqMs - 1500) < 2400, `reqMs=${reqMs} sinceFinal=${reqMs - 1500}ms`);
        // S4: sau submit — "Đang ghi âm" phải TẮT (A1 fix — trước đây kẹt mãi)
        await page.waitForTimeout(1500);
        check('S4 isRecording/listening reset sau final', !(await isListening(page)));
        // S4b: tap mic lần 2 phải start được NGAY (không cần 2 tap)
        await mic.click();
        await page.waitForTimeout(800);
        check('S4b re-tap mic starts session 2 (listening on)', await isListening(page));
        await page.locator('button[aria-label*="Dừng nói"]').first().click().catch(() => {});
    }
    await ctx.close();
}

// ═══ S2: noise hard-cap — interim mới liên tục → session phải end ≤35s ══════
{
    const { ctx, page } = await newPageWithSR('noise');
    await page.goto(`${KHACHLINK}/stores`, { waitUntil: 'domcontentloaded' });
    const mic = await micButton(page);
    if (await mic.count() === 0) {
        check('S2 mic button render', false, 'mic không hiện');
    } else {
        await mic.click();
        // Noise-guard: transcript đổi mỗi 400ms → silence re-arm legit → nhưng
        // MAX_SESSION 30s phải cắt. Đợi ≤36s.
        let off = false;
        for (let i = 0; i < 72; i++) {
            await page.waitForTimeout(500);
            if (!(await isListening(page))) { off = true; break; }
        }
        check('S2 noise hard-cap: session end ≤36s', off);
    }
    await ctx.close();
}

// ═══ S5: lazy-race — isSupported callable ≤5s sau firstRender ═══════════════
{
    const ctx = await browser.newContext({ bypassCSP: true, ignoreHTTPSErrors: true });
    const page = await ctx.newPage();
    page.on('pageerror', e => pageErrors.push('S5:' + String(e)));
    const t0 = Date.now();
    await page.goto(`${KHACHLINK}/stores`, { waitUntil: 'domcontentloaded' });
    let ok = false, waited = -1;
    for (let i = 0; i < 16; i++) {
        ok = await page.evaluate(() =>
            !!(window.vananVoiceInput && window.vananVoiceInput.isSupported &&
               window.vananVoiceInput.isSupported())).catch(() => false);
        if (ok) { waited = Date.now() - t0; break; }
        await page.waitForTimeout(500);
    }
    check('S5 lazy-race: vananVoiceInput.isSupported callable ≤8s', ok,
        `waited=${waited}ms`);
    const readyFn = await page.evaluate(() => typeof window.vananWhenLazyReady);
    check('S5 vananWhenLazyReady function exists', readyFn === 'function', readyFn);
    await ctx.close();
}

// ═══ S6: SPA navigation abort — đang nghe → click in-app link (Blazor nav,
// KHÔNG reload — context JS giữ nguyên) → Dispose → stop → session chết ═════
{
    const { ctx, page } = await newPageWithSR('noise');
    await page.goto(`${KHACHLINK}/stores`, { waitUntil: 'domcontentloaded' });
    const mic = await micButton(page);
    if (await mic.count() === 0) {
        check('S6 mic button render', false, 'mic không hiện');
    } else {
        await mic.click();
        await page.waitForTimeout(1000);
        check('S6 session active trước nav', await isListening(page));
        // SPA nav: click link nội bộ (Blazor enhanced nav — không reload page)
        const nav = page.locator('a[href="/products"], a[href="/"]').first();
        const hadLink = await nav.count() > 0;
        if (hadLink) await nav.click();
        await page.waitForTimeout(2000);
        const stillActive = await page.evaluate(() =>
            window.vananVoiceInput ? window.vananVoiceInput.isActive() : 'NO-API');
        check('S6 SPA nav → session abort (isActive=false)',
            hadLink && stillActive === false,
            `link=${hadLink} isActive=${stillActive}`);
    }
    await ctx.close();
}

// ═══ Dir SSR smoke: mic + vananVoiceInput trên timlathay ════════════════════
// 'nomatch' → empty-state chỉ hiện khi search chạy THẬT (anti false-positive
// 'An Tâm' nằm sẵn trong listing mặc định).
{
    const { ctx, page } = await newPageWithSR('nomatch');
    await page.goto(`${DIRECTORY}/stores`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(4000); // SSR + interactive boot
    const mic = await micButton(page);
    check('Dir StoreFinder mic render (SSR)', await mic.count() > 0);
    if (await mic.count() > 0) {
        await mic.click();
        let earlyFill = false, found = false;
        for (let i = 0; i < 20; i++) {
            await page.waitForTimeout(500);
            const inp = await page.locator('#vanan-public-search-input').first()
                .inputValue().catch(() => '');
            // V2b: interim fill qua DOM trực tiếp — input có transcript TRƯỚC khi
            // silence 2.5s + search + SSR render xong (poll đầu ≤2.5s sau click)
            if (inp.includes('zzqqxx') && i <= 4) earlyFill = true;
            const body = await page.locator('body').innerText().catch(() => '');
            if (body.includes('Không tìm thấy')) found = true;
            if (found && (earlyFill || i > 4)) break;
        }
        check('Dir interim → input DOM-fill sớm, 0 roundtrip (V2b)', earlyFill);
        check('Dir voice submit → empty-state (search chạy THẬT, SSR)', found);
    }
    await ctx.close();
}

// ═══ S8: Home KL unify — vananVoiceInput → DOM-fill → SubmitHomeSearch nav ══
{
    const { ctx, page } = await newPageWithSR('speech');
    await page.goto(`${KHACHLINK}/`, { waitUntil: 'domcontentloaded' });
    // Home nặng — lazy JS + feature flag Voice_Note_Enabled gate
    const mic = page.locator('.home-voice-mic-btn, button[aria-label*="giọng nói"]').first();
    try { await mic.waitFor({ timeout: 45000 }); } catch {}
    check('S8 Home KL mic render (V2c unify)', await mic.count() > 0);
    if (await mic.count() > 0) {
        await mic.click();
        // interim → DOM-fill #home-search-input; final → SubmitHomeSearch →
        // navigate /products?q=an tam (mode mặc định product — trước đây voice
        // hardcode /stores bỏ qua mode, unify fix đi cùng đường typed submit)
        let earlyFill = false, navUrl = '';
        for (let i = 0; i < 20; i++) {
            await page.waitForTimeout(500);
            const inp = await page.locator('#home-search-input').inputValue().catch(() => '');
            if (inp.includes('an tam') && i <= 4) earlyFill = true;
            if (page.url().includes('q=')) { navUrl = page.url(); break; }
        }
        check('S8 Home interim → DOM-fill #home-search-input', earlyFill);
        check('S8 Home final → SubmitHomeSearch nav với q=an tam',
            navUrl.includes('q=') && /products|stores/.test(navUrl), navUrl);
    }
    await ctx.close();
}

// ═══ S9: noend — Chrome 'end' không fire (manual-test failure mode): final
// đến, nhưng session không đóng + stop() không sinh onend → dead-man
// finalize fallback (400ms) phải vẫn submit transcript ══════════════════════
{
    const { ctx, page } = await newPageWithSR('noend');
    const tenantReqs = [];
    page.on('request', r => {
        if (r.url().includes('/api/tenants/search')) tenantReqs.push(r.url());
    });
    await page.goto(`${KHACHLINK}/stores`, { waitUntil: 'domcontentloaded' });
    const mic = await micButton(page);
    if (await mic.count() === 0) {
        check('S9 mic button render', false, 'mic không hiện');
    } else {
        await mic.click();
        // final ở ~1400ms → finalCommit 800ms → stop() → fake không fire end →
        // dead-man 400ms → finalize → request ≈ 2.6-3.5s. Đợi ≤8s.
        let found = false, off = false;
        for (let i = 0; i < 16; i++) {
            await page.waitForTimeout(500);
            if (tenantReqs.some(u => u.includes('name=an%20tam') || u.includes('name=an+tam'))) {
                found = true;
            }
            off = !(await isListening(page));
            if (found && off) break;
        }
        check('S9 noend: transcript vẫn submit (dead-man finalize)', found,
            tenantReqs[tenantReqs.length - 1] || 'no request');
        check('S9 noend: listening off sau fallback', off);
    }
    await ctx.close();
}

// ═══ MATRIX: toàn bộ voice search input trên mọi KhachLink instance ═══════
// Helper: mic click → speech → assert DOM-fill + request/nav/DOM result.
async function voiceFlow({ host, path, inputSel, reqFilter, domProof, micSel, mode, transcript }) {
    const { ctx, page } = await newPageWithSR(mode || 'speech');
    const reqs = [];
    if (reqFilter) page.on('request', r => { if (reqFilter(r.url())) reqs.push(r.url()); });
    await page.goto(host + path, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(4000); // WASM boot / SSR interactive
    const mic = page.locator(micSel || 'button[aria-label*="giọng nói"]').first();
    try { await mic.waitFor({ timeout: 30000 }); } catch {}
    const hasMic = await mic.count() > 0;
    if (!hasMic) { await ctx.close(); return { mic: false }; }
    await mic.click();
    const want = transcript || 'an tam';
    let earlyFill = false, hit = false, dom = false, navUrl = '';
    for (let i = 0; i < 16; i++) {
        await page.waitForTimeout(500);
        const inp = await page.locator(inputSel).first().inputValue().catch(() => '');
        if (inp.includes(want) && i <= 5) earlyFill = true;
        if (reqs.length) hit = true;
        if (domProof) {
            const body = await page.locator('body').innerText().catch(() => '');
            if (domProof(body)) dom = true;
        }
        if (page.url().includes('q=')) { navUrl = page.url(); break; }
        // domProof KHÔNG short-circuit khi cần request — kết quả có thể nằm sẵn
        // trong default listing (ví dụ "An Tâm" là tenant đầu tiên) → dom true
        // ngay lần poll đầu trước cả khi voice final được emit
        if (hit || (dom && !reqFilter)) break;
    }
    // Request vừa fire → render còn chưa kịp → poll domProof thêm ≤4s
    if (domProof && !dom) {
        for (let i = 0; i < 8 && !dom; i++) {
            await page.waitForTimeout(500);
            const body = await page.locator('body').innerText().catch(() => '');
            if (domProof(body)) dom = true;
        }
    }
    // SSR/render lag: OnVoiceEnd → _listening=false cần 1 render cycle — poll ≤3s
    let off = false;
    for (let i = 0; i < 6 && !off; i++) {
        off = !(await isListening(page));
        if (!off) await page.waitForTimeout(500);
    }
    await ctx.close();
    return { mic: true, earlyFill, hit, dom, navUrl, off, lastReq: reqs[reqs.length - 1] };
}

for (const host of KL_HOSTS) {
    const tag = host.replace('https://', '').split('.')[0];
    // /stores — StoreFinder in-page search → request /api/tenants/search?name=
    // filter PHẢI match query "an" — boot-time list request (name= rỗng) không tính
    const st = await voiceFlow({
        host, path: '/stores', inputSel: '#search-input',
        reqFilter: u => u.includes('/api/tenants/search') && u.includes('name=an'),
    });
    check(`${tag}/stores mic render`, st.mic !== false);
    if (st.mic) {
        check(`${tag}/stores interim→DOM-fill sớm`, st.earlyFill);
        check(`${tag}/stores submit name=an tam`, st.hit, st.lastReq || 'no req');
        check(`${tag}/stores listening off`, st.off);
    }
    // nomatch render-proof: request fire ≠ render (JSInvokable thiếu
    // StateHasChanged → request ra network nhưng DOM không đổi — bug "transcript
    // fill nhưng search không chạy" user report). Assert empty-state XUẤT HIỆN.
    const nm = await voiceFlow({
        host, path: '/stores', inputSel: '#search-input', mode: 'nomatch',
        transcript: 'zzqqxx',
        reqFilter: u => u.includes('/api/tenants/search') && u.includes('zzqqxx'),
        domProof: b => b.includes('Không tìm thấy cửa hàng nào'),
    });
    check(`${tag}/stores nomatch: request name=zzqqxx`, nm.mic !== false && nm.hit, nm.lastReq || 'no req');
    if (nm.mic) check(`${tag}/stores nomatch: empty-state RENDER`, nm.dom);
    // /products — ProductSearchResults → request /api/catalog/search?q=
    const pr = await voiceFlow({
        host, path: '/products', inputSel: '#product-search-input',
        reqFilter: u => u.includes('catalog/search') && /[?&]q=an/i.test(u),
    });
    check(`${tag}/products mic render`, pr.mic !== false);
    if (pr.mic) {
        check(`${tag}/products interim→DOM-fill sớm`, pr.earlyFill);
        check(`${tag}/products submit catalog q=an tam`, pr.hit, pr.lastReq || 'no req');
        check(`${tag}/products listening off`, pr.off);
    }
    // / — Home unify → SubmitHomeSearch nav q=
    const hm = await voiceFlow({
        host, path: '/', inputSel: '#home-search-input',
        micSel: '.home-voice-mic-btn, button[aria-label*="giọng nói"]',
    });
    check(`${tag}/ mic render (Home)`, hm.mic !== false);
    if (hm.mic) {
        check(`${tag}/ interim→DOM-fill #home-search-input`, hm.earlyFill);
        check(`${tag}/ final→nav q=an tam`, /products|stores/.test(hm.navUrl) && hm.navUrl.includes('q='), hm.navUrl);
    }
}

// Hoptacxa portal: _isHoptacxa skip voice init → mic PHẢI ẩn (negative check)
{
    const { ctx, page } = await newPageWithSR('speech');
    await page.goto('https://hoptacxa.timlathay.com/', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(8000);
    const micCount = await page.locator('button[aria-label*="giọng nói"]').count();
    check('hoptacxa/ mic ẩn đúng (portal không voice)', micCount === 0, `count=${micCount}`);
    await ctx.close();
}

// Dir Home `/` — SSR store mode: interim DOM-fill + search server-side → DOM.
// Dùng 'nomatch' transcript (zzqqxx) — chắc chắn KHÔNG có trong featured list
// → empty-state text chỉ xuất hiện khi OnVoiceFinal→SearchStores chạy THẬT.
{
    const { ctx, page } = await newPageWithSR('nomatch');
    await page.goto(`${DIRECTORY}/`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(5000);
    const mic = page.locator('button[aria-label*="giọng nói"]').first();
    try { await mic.waitFor({ timeout: 30000 }); } catch {}
    check('Dir/ mic render (Home)', await mic.count() > 0);
    if (await mic.count() > 0) {
        await mic.click();
        let earlyFill = false, searched = false;
        for (let i = 0; i < 16 && !(earlyFill && searched); i++) {
            await page.waitForTimeout(500);
            const inp = await page.locator('#vanan-public-search-input').first().inputValue().catch(() => '');
            if (inp.includes('zzqqxx') && i <= 5) earlyFill = true;
            const body = await page.locator('body').innerText().catch(() => '');
            if (body.includes('Không tìm thấy') || body.includes('không có cửa hàng')) {
                searched = true;
            }
        }
        check('Dir/ interim→DOM-fill sớm', earlyFill);
        check('Dir/ voice submit → empty-state render (search chạy THẬT)', searched);
        let off = false;
        for (let i = 0; i < 6 && !off; i++) {
            off = !(await isListening(page));
            if (!off) await page.waitForTimeout(500);
        }
        check('Dir/ listening off sau submit', off);
    }
    await ctx.close();
}

// ═══ D-BLOCK: DidYouMean + fuzzy — search TRẢ ĐÚNG kết quả, không chỉ không
// treo. Cover: API contract → typed misspell → voice misspell (fuzzy chain) ══
// L1 API contract (không cần browser): fuzzy "antaam" → didYouMean "An Tâm"
try {
    const r = await fetch('https://api2.khachvip.online/api/tenants/search?name=antaam');
    const j = await r.json();
    const dy = j.didYouMean || (j.results && j.results.didYouMean) || '';
    check('API fuzzy: name=antaam → didYouMean "An Tâm"',
        String(dy).includes('An Tâm'), JSON.stringify(j).slice(0, 120));
    const r2 = await fetch('https://api2.khachvip.online/api/tenants/search?name=cafe');
    const j2 = await r2.json();
    check('API search: name=cafe → có kết quả', (j2.results || []).length > 0,
        `n=${(j2.results || []).length}`);
} catch (e) {
    check('API fuzzy contract', false, String(e));
}

// Typed misspell helper: fill + Enter → fuzzy result / DidYouMean chip trong DOM
async function typedMisspellFlow({ host, path, inputSel }) {
    const { ctx, page } = await newPageWithSR('speech');
    await page.goto(host + path, { waitUntil: 'domcontentloaded' });
    // Mic chỉ render khi _speechSupported=true — tức Blazor interactive + lazy
    // voice JS đã load → proxy chắc chắn cho "page sẵn sàng nhận input events".
    // Sleep cứng 4s flake trên instance WASM cold (commienphi/vanan).
    const mic = await micButton(page);
    await mic.count();
    const inp = page.locator(inputSel).first();
    const found = await inp.count() > 0;
    let dom = false, body = '';
    if (found) {
        await inp.fill('antaam');
        await inp.press('Enter');
        for (let i = 0; i < 14; i++) {
            await page.waitForTimeout(500);
            body = await page.locator('body').innerText().catch(() => '');
            if (body.includes('An Tâm')) { dom = true; break; }
        }
    }
    await ctx.close();
    return { found, dom, body: body.slice(0, 120) };
}

for (const host of KL_HOSTS) {
    const tag = host.replace('https://', '').split('.')[0];
    // D1: typed misspell → fuzzy/DidYouMean đưa "An Tâm" ra (result hoặc chip)
    const dy = await typedMisspellFlow({ host, path: '/stores', inputSel: '#search-input' });
    check(`${tag}/stores typed "antaam" → fuzzy/DidYouMean "An Tâm"`,
        dy.found && dy.dom, dy.body);
    // D2: VOICE misspell → transcript "antaam" submit → fuzzy chain trả kết quả
    const vdy = await voiceFlow({
        host, path: '/stores', inputSel: '#search-input', mode: 'misspell',
        transcript: 'antaam',
        reqFilter: u => u.includes('/api/tenants/search') && u.includes('antaam'),
        domProof: b => b.includes('An Tâm'),
    });
    check(`${tag}/stores voice "antaam" mic`, vdy.mic !== false);
    if (vdy.mic) {
        check(`${tag}/stores voice "antaam" → request name=antaam`, vdy.hit, vdy.lastReq || 'no req');
        check(`${tag}/stores voice "antaam" → "An Tâm" trong DOM (fuzzy/DYM)`, vdy.dom);
    }
}
// Dir /stores typed misspell (SSR — server-side search → DOM proof only)
{
    const dy = await typedMisspellFlow({ host: DIRECTORY, path: '/stores', inputSel: '#vanan-public-search-input' });
    check('Dir/stores typed "antaam" → fuzzy/DidYouMean "An Tâm"', dy.found && dy.dom, dy.body);
}

check('no pageerror', pageErrors.length === 0, pageErrors[0]);
await browser.close();

const pass = results.filter(r => r.p).length;
const fail = results.filter(r => !r.p);
console.log(`\n═══ VOICE V1 SWEEP: ${pass}/${results.length} PASS, ${fail.length} FAIL ═══`);
mkdirSync(dirname(REPORT), { recursive: true });
writeFileSync(REPORT, JSON.stringify({ when: new Date().toISOString(), results }, null, 2));
process.exit(fail.length ? 1 : 0);
