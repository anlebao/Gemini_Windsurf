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
        const emit = (txt, isFinal) => this.onresult && this.onresult({ results: [
            Object.assign([{ transcript: txt }], { isFinal })
        ]});
        this._timers.push(setTimeout(() => emit('an', false), 300));
        this._timers.push(setTimeout(() => emit('an ta', false), 700));
        this._timers.push(setTimeout(() => emit('an tam', false), 1100));
        this._timers.push(setTimeout(() => emit('an tam', true), 1500));
    }
    stop() {
        this._timers.forEach(clearTimeout);
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
{
    const { ctx, page } = await newPageWithSR('speech');
    await page.goto(`${DIRECTORY}/stores`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(4000); // SSR + interactive boot
    const mic = await micButton(page);
    check('Dir StoreFinder mic render (SSR)', await mic.count() > 0);
    if (await mic.count() > 0) {
        await mic.click();
        let earlyFill = false, found = false;
        for (let i = 0; i < 20; i++) {
            await page.waitForTimeout(500);
            const inp = await page.locator('.vanan-search-pill input, input').first()
                .inputValue().catch(() => '');
            // V2b: interim fill qua DOM trực tiếp — input có transcript TRƯỚC khi
            // silence 2.5s + search + SSR render xong (poll đầu ≤2.5s sau click)
            if (inp.includes('an tam') && i <= 4) earlyFill = true;
            if (inp.includes('an tam')) { found = true; break; }
            const body = await page.locator('body').innerText().catch(() => '');
            if (body.includes('An Tâm')) { found = true; break; }
        }
        check('Dir interim → input DOM-fill sớm, 0 roundtrip (V2b)', earlyFill);
        check('Dir voice submit → transcript/search chạy (SSR)', found);
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

check('no pageerror', pageErrors.length === 0, pageErrors[0]);
await browser.close();

const pass = results.filter(r => r.p).length;
const fail = results.filter(r => !r.p);
console.log(`\n═══ VOICE V1 SWEEP: ${pass}/${results.length} PASS, ${fail.length} FAIL ═══`);
mkdirSync(dirname(REPORT), { recursive: true });
writeFileSync(REPORT, JSON.stringify({ when: new Date().toISOString(), results }, null, 2));
process.exit(fail.length ? 1 : 0);
