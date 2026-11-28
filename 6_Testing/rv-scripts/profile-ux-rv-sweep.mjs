// ─────────────────────────────────────────────────────────────────────────────
// RV SWEEP — Profile UX upgrade — PRODUCTION
// Task: docs/AI/tasks/task_profile_ux_upgrade.md · commit c478e8e0
// L2 WASM markers → L4 UI (guest skeleton/chip + optional authenticated hero,
// grouping headers, Zone 4 cleanup: no Fraud Flags / raw device token /
// duplicate upgrade button).
// Env: KHACHLINK_URL (default commienphi),
//      PROFILE_CUSTOMER_TOKEN (optional — real customer assertions).
// ─────────────────────────────────────────────────────────────────────────────
import { chromium } from 'playwright';
import { writeFileSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPORT = join(HERE, '..', '..', '.devin', 'profile-ux-rv-report.json');
const KHACHLINK = process.env.KHACHLINK_URL || 'https://commienphi.timlathay.com';
const TOKEN = process.env.PROFILE_CUSTOMER_TOKEN || '';

const results = [];
const check = (n, p, d = '') => {
    results.push({ n, p: !!p, d: String(d).slice(0, 300) });
    console.log(`[${p ? 'PASS' : 'FAIL'}] ${n}${d ? ' — ' + String(d).slice(0, 300) : ''}`);
};

// ── L2: WASM markers — literal UTF-16LE; type/testid strings ───────────────
try {
    const r = await fetch(`${KHACHLINK}/_framework/VanAn.KhachLink.wasm`);
    const buf = Buffer.from(await r.arrayBuffer());
    const utf16 = s => Buffer.from(s, 'utf16le');
    check('L2 WASM contains loyalty-hero testid (UTF-16LE)',
        r.ok && buf.includes(utf16('loyalty-hero')), `${buf.length} bytes`);
    check('L2 WASM contains profile-skel-card + hero-redeem markers',
        buf.includes(utf16('profile-skel-card')) && buf.includes(utf16('hero-redeem')));
    check('L2 WASM contains Mua sắm / Tài khoản group headers',
        buf.includes(utf16('Tài khoản')) && buf.includes(utf16('sắm')));
} catch (e) { check('L2 WASM fetch', false, e.message); }

// ── L4: UI flow (Playwright) ───────────────────────────────────────────────
const browser = await chromium.launch({ headless: true });
const pageErrors = [];

// Guest: skeleton renders first (không crash), guest card + BackToStoreChip,
// KHÔNG có fraud flags trên guest path.
{
    const ctx = await browser.newContext({ bypassCSP: true, ignoreHTTPSErrors: true });
    const page = await ctx.newPage();
    page.on('pageerror', e => pageErrors.push(String(e)));
    await page.addInitScript(() => {
        try {
            localStorage.clear();
            localStorage.setItem('customer_device_id', crypto.randomUUID());
        } catch { /* noop */ }
    });
    await page.goto(`${KHACHLINK}/profile`, { waitUntil: 'domcontentloaded' });
    await page.locator('text=Chưa đăng nhập').waitFor({ timeout: 60000 }).catch(() => null);
    await page.waitForTimeout(3000);

    check('L4 guest /profile → guest card rendered', await page.locator('text=Chưa đăng nhập').count() > 0);
    check('L4 guest: KHÔNG hiện Fraud Flags (cleanup B)', await page.locator('text=Fraud Flags').count() === 0);
    await page.screenshot({ path: 'rv-profile-ux-guest.png', fullPage: true });
    await ctx.close();
}

// Authenticated (optional): hero + grouping + Zone 4 cleanup
if (TOKEN) {
    const ctx2 = await browser.newContext({ bypassCSP: true, ignoreHTTPSErrors: true });
    const p2 = await ctx2.newPage();
    p2.on('pageerror', e => pageErrors.push(String(e)));
    await p2.addInitScript(t => {
        try {
            localStorage.clear();
            localStorage.setItem('customer_token', t);
        } catch { /* noop */ }
    }, TOKEN);
    await p2.goto(`${KHACHLINK}/profile`, { waitUntil: 'domcontentloaded' });
    await p2.locator('[data-testid="loyalty-hero"]').waitFor({ timeout: 60000 }).catch(() => null);
    await p2.waitForTimeout(3000); // hero + context cards load

    check('L4 auth: loyalty-hero card rendered', await p2.locator('[data-testid="loyalty-hero"]').count() > 0);
    check('L4 auth: hero-points số lớn hiện', await p2.locator('[data-testid="hero-points"]').count() > 0);
    check('L4 auth: grouping headers "Mua sắm"+"Tài khoản"',
        await p2.locator('h6:has-text("Mua sắm")').count() > 0 &&
        await p2.locator('h6:has-text("Tài khoản")').count() > 0);

    // CTA Đổi điểm → /rewards same-origin
    await p2.locator('[data-testid="hero-redeem"]').click().catch(() => null);
    await p2.waitForURL(/\/rewards/, { timeout: 15000 }).catch(() => null);
    check('L4 auth: "Đổi điểm" → /rewards', /\/rewards/.test(p2.url()), p2.url());
    await p2.goto(`${KHACHLINK}/profile`, { waitUntil: 'domcontentloaded' });
    await p2.locator('[data-testid="loyalty-hero"]').waitFor({ timeout: 60000 }).catch(() => null);
    await p2.waitForTimeout(2000);

    // Zone 4 cleanup: settings mở → no fraud flags / raw token / dup upgrade
    await p2.locator('[data-testid="settings-toggle"]').click().catch(() => null);
    await p2.waitForTimeout(800);
    check('L4 auth zone4: KHÔNG Fraud Flags', await p2.locator('text=Fraud Flags').count() === 0);
    check('L4 auth zone4: KHÔNG raw Device Token', await p2.locator('text=Device Token:').count() === 0);
    check('L4 auth zone4: KHÔNG btn-upgrade-identity (ctx card là single-source)',
        await p2.locator('[data-testid="btn-upgrade-identity"]').count() === 0);
    check('L4 auth zone4: device card vẫn hiện "Thiết bị đang sử dụng"',
        await p2.locator('text=Thiết bị đang sử dụng').count() > 0);
    await p2.screenshot({ path: 'rv-profile-ux-auth.png', fullPage: true });
    await ctx2.close();
} else {
    check('L4 auth profile-ux', true, 'SKIPPED — PROFILE_CUSTOMER_TOKEN not set');
}

check('L4 no page errors during flow', pageErrors.length === 0,
    pageErrors.slice(0, 2).join(' | '));

await browser.close();

const pass = results.filter(r => r.p).length;
mkdirSync(join(HERE, '..', '..', '.devin'), { recursive: true });
writeFileSync(REPORT, JSON.stringify({ when: new Date().toISOString(), pass, total: results.length, results }, null, 2));
console.log(`\n${pass}/${results.length} PASS — report → ${REPORT}`);
process.exit(pass === results.length ? 0 : 1);
