// RV probe — ATRS-21 customer passkey login end-to-end (production).
// Flow: guest → register passkey (virtual authenticator CDP) → auto-login →
// customer_token → /profile. Sau đó logout → discoverable login lại → token mới.
// Env: KHACHLINK_URL (default https://vanan.timlathay.com)
import { chromium } from 'playwright';

const BASE = process.env.KHACHLINK_URL || 'https://vanan.timlathay.com';
const consoleErrors = [];
const pageErrors = [];
const results = [];
const check = (n, p, d = '') => { results.push({ n, p: !!p }); console.log(`[${p ? 'PASS' : 'FAIL'}] ${n}${d ? ' — ' + String(d).slice(0, 200) : ''}`); };

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ viewport: { width: 375, height: 812 } });
const page = await context.newPage();
page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
page.on('pageerror', e => pageErrors.push(String(e)));

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

await page.addInitScript(() => { try { localStorage.clear(); } catch {} });
await page.goto(`${BASE}/login`, { waitUntil: 'domcontentloaded' });
await page.locator('h3', { hasText: 'Đăng nhập' }).waitFor({ timeout: 40000 });
check('login page renders', true);

const regBtn = page.getByTestId('btn-passkey-register');
check('guest passkey register button visible', await regBtn.isVisible().catch(() => false));

// ── 1. Guest register → auto login ──
await regBtn.click();
try {
  await page.waitForURL(/\/profile/, { timeout: 60000 });
} catch {
  await page.screenshot({ path: 'rv-passkey-login-fail.png', fullPage: true });
}
const token1 = await page.evaluate(() => localStorage.getItem('customer_token'));
const provider1 = await page.evaluate(() => localStorage.getItem('login_provider'));
const customerId1 = await page.evaluate(() => localStorage.getItem('customer_id'));
check('register→auto-login → /profile', /\/profile/.test(page.url()), page.url());
check('customer_token stored', !!token1);
check('login_provider=passkey', provider1 === 'passkey', provider1);
check('customer_id stored', !!customerId1, customerId1);

// Token round-trip: /api/customer-identity/me qua gateway
if (token1) {
  const me = await page.evaluate(async (t) => {
    const r = await fetch('/api/customer-identity/me', { headers: { 'X-Customer-Token': t } });
    const text = await r.text();
    return { status: r.status, body: text.slice(0, 300) };
  }, token1);
  check('customer-identity/me 200', me.status === 200, `status=${me.status} body=${me.body}`);
}
await page.screenshot({ path: 'rv-passkey-after-login.png', fullPage: true });

// ── 2. Logout → discoverable login lại (passkey đã resident trên authenticator) ──
await page.evaluate(() => { localStorage.removeItem('customer_token'); localStorage.removeItem('login_provider'); localStorage.removeItem('customer_id'); });
const deviceId1 = await page.evaluate(() => localStorage.getItem('customer_device_id'));
await page.goto(`${BASE}/login`, { waitUntil: 'domcontentloaded' });
await page.evaluate((id) => { if (id) localStorage.setItem('customer_device_id', id); }, deviceId1);
await page.locator('h3', { hasText: 'Đăng nhập' }).waitFor({ timeout: 40000 });
// chờ passkey UI render xong (isSupported check async sau WASM boot)
await page.getByTestId('btn-passkey-register').waitFor({ state: 'visible', timeout: 30000 }).catch(() => {});
const loginBtn = page.getByRole('button', { name: /vân tay|khuôn mặt/ });
check('passkey login button visible', await loginBtn.isVisible().catch(() => false));
await loginBtn.click();
try {
  await page.waitForURL(/\/profile/, { timeout: 60000 });
} catch {
  await page.screenshot({ path: 'rv-passkey-relogin-fail.png', fullPage: true });
}
const token2 = await page.evaluate(() => localStorage.getItem('customer_token'));
check('discoverable re-login → /profile + token', /\/profile/.test(page.url()) && !!token2, page.url());
check('same customer_id', (await page.evaluate(() => localStorage.getItem('customer_id'))) === customerId1);

// ── 3. Negative: finish với challenge rác → generic error, không oracle ──
// Probe này cố tình gây HTTP 400 → browser log "Failed to load resource" là expected noise,
// loại khỏi console-error check (chỉ đếm lỗi phát sinh TRƯỚC probe).
const consoleErrBeforeNeg = consoleErrors.length;
const neg = await page.evaluate(async () => {
  const r = await fetch('/api/v1/account-trust/passkey/login/finish', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ challenge: 'bogus', origin: location.origin, rpId: location.hostname.split('.').slice(-2).join('.'), assertionResponse: {}, deviceId: 'x' })
  });
  return { status: r.status, body: await r.json().catch(() => null) };
});
check('fake finish → generic 400 (anti-enumeration)', neg.status === 400 && neg.body?.success === false, JSON.stringify(neg.body).slice(0, 120));
consoleErrors.length = consoleErrBeforeNeg; // discard expected 400 resource-load noise từ probe

check('zero console errors', consoleErrors.length === 0, consoleErrors.slice(0, 3).join(' | '));
check('zero pageerrors', pageErrors.length === 0, pageErrors.slice(0, 3).join(' | '));

await browser.close();
const fails = results.filter(r => !r.p);
console.log(`\n═══ PASSKEY LOGIN RV: ${results.length - fails.length}/${results.length} PASS ═══`);
process.exit(fails.length ? 1 : 0);
