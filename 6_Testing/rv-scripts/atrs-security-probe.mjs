// RV probe — ATRS security center: console errors + screenshot + REAL passkey
// registration via CDP virtual authenticator + recovery-code generate step-up.
// Env: SECURITY_CUSTOMER_TOKEN, KHACHLINK_URL (default https://vanan.timlathay.com)
import { chromium } from 'playwright';

const BASE = process.env.KHACHLINK_URL || 'https://vanan.timlathay.com';
const TOKEN = process.env.SECURITY_CUSTOMER_TOKEN || '';
if (!TOKEN) { console.error('SECURITY_CUSTOMER_TOKEN required'); process.exit(2); }

const consoleErrors = [];
const pageErrors = [];
const results = [];
const check = (n, p, d = '') => { results.push({ n, p: !!p }); console.log(`[${p ? 'PASS' : 'FAIL'}] ${n}${d ? ' — ' + String(d).slice(0, 200) : ''}`); };

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ viewport: { width: 375, height: 812 } });
const page = await context.newPage();
page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
page.on('pageerror', e => pageErrors.push(String(e)));
await page.addInitScript(t => { try { localStorage.clear(); localStorage.setItem('customer_token', t); } catch {} }, TOKEN);

// CDP virtual authenticator (resident key + user verification)
const cdp = await context.newCDPSession(page);
await cdp.send('WebAuthn.enable');
const { authenticatorId } = await cdp.send('WebAuthn.addVirtualAuthenticator', {
  options: {
    protocol: 'ctap2', ctap2Version: 'ctap2_1', transport: 'internal',
    hasResidentKey: true, hasUserVerification: true, isUserVerified: true,
    automaticPresenceSimulation: true,
  },
});
console.log('virtual authenticator:', authenticatorId);

await page.goto(`${BASE}/security`, { waitUntil: 'networkidle' });
await page.locator('h3', { hasText: 'Bảo mật tài khoản' }).waitFor({ timeout: 40000 });
check('security page renders', true);
await page.screenshot({ path: 'rv-atrs-security-top.png', fullPage: true });

// ── Passkey registration (real ceremony qua virtual authenticator) ──
const createBtn = page.getByRole('button', { name: /Tạo passkey/ });
check('create passkey button visible', await createBtn.isVisible().catch(() => false));
await createBtn.click();
// chờ server confirm — UI hiện credential hoặc error
try {
  await page.locator('text=/Passkey · tạo|Chưa có passkey/').first().waitFor({ timeout: 30000 });
} catch {}
await page.waitForTimeout(3000);
await page.screenshot({ path: 'rv-atrs-after-register.png', fullPage: true });
const bodyText = await page.locator('body').innerText();
const registered = /Passkey · tạo/.test(bodyText);
const regError = await page.locator('text=/lỗi|thất bại|không hỗ trợ|thử lại/i').allInnerTexts().catch(() => []);
check('passkey registered (server-confirmed)', registered, registered ? 'credential listed' : `errors=${JSON.stringify(regError).slice(0, 150)}`);

// ── Recovery codes: generate cần step-up → page sẽ trigger passkey assert ──
const genBtn = page.getByRole('button', { name: /Tạo mã|mã khôi phục mới|Tạo lại/ }).first();
if (await genBtn.isVisible().catch(() => false)) {
  await genBtn.click();
  await page.waitForTimeout(6000);
  await page.screenshot({ path: 'rv-atrs-after-generate.png', fullPage: true });
  const t2 = await page.locator('body').innerText();
  const codesShown = /Mã khôi phục mới|chỉ hiển thị lần này/.test(t2);
  check('recovery codes generated via step-up', codesShown, codesShown ? 'codes displayed once' : 'codes not shown');
} else {
  check('recovery codes generate button visible', false, 'button not found');
}

check('zero console errors', consoleErrors.length === 0, consoleErrors.slice(0, 3).join(' | '));
check('zero pageerrors', pageErrors.length === 0, pageErrors.slice(0, 3).join(' | '));

await browser.close();
const fails = results.filter(r => !r.p);
console.log(`\n═══ PROBE: ${results.length - fails.length}/${results.length} PASS ═══`);
process.exit(fails.length ? 1 : 0);
