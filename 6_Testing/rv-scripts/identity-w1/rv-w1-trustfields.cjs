// RV W1 — trust-fields E2E production: submit → verify-signup → (PG check bên ngoài) → deactivate → downgrade
// Password sysadmin qua env SYSADMIN_PW (KHÔNG hardcode — SEC-2).
const { chromium } = require('@playwright/test');
const API2 = 'https://api2.khachvip.online';
const APP2 = 'https://app2.khachvip.online';
const IP = '136.85.94.119';

const PW = process.env.SYSADMIN_PW;
if (!PW) { console.error('missing SYSADMIN_PW env'); process.exit(1); }

const results = [];
const check = (name, ok, detail = '') => { results.push({ name, ok }); console.log(`${ok ? '[PASS]' : '[FAIL]'} ${name}${detail ? ' — ' + detail : ''}`); };

(async () => {
  const ts = Date.now();
  const phone = `090${String(ts).slice(-7)}`;
  const username = `rvw1${String(ts).slice(-6)}`;
  const shopName = `RV W1 Trust ${ts}`;
  const payload = {
    case: 1, name: shopName, address: '123 Duong RV, Phuong 1, Q1, TP.HCM',
    businessType: 2, hkdGroup: 1, gpdkInfo: null, signName: null,
    latitude: null, longitude: null, photoUrls: null,
    productLines: [{ name: 'Ca phe sua da', price: 25000, lineNo: 1 }],
    contactName: 'RV Tester', contactPhone: phone, contactEmail: null,
    ownerUsername: username, ownerPassword: 'matkhauRV123',
    turnstileToken: null, honeypotWebsite: null, src: 'direct',
  };

  const browser = await chromium.launch({ headless: true, args: [`--host-resolver-rules=MAP app2.khachvip.online ${IP}, MAP api2.khachvip.online ${IP}`] });
  const ctx = await browser.newContext({ ignoreHTTPSErrors: true });

  // 1. sysadmin login — lấy JWT (cookie app2 không share sang api2 domain → Bearer)
  const login = await ctx.request.post(`${APP2}/api/platform/login`, {
    data: { Username: 'sysadmin@vanan.vn', Password: PW },
    headers: { 'Content-Type': 'application/json' },
  });
  const loginBody = login.ok() ? await login.json() : null;
  check('sysadmin login', login.ok() && !!loginBody?.token, `status=${login.status()}`);
  const auth = { Authorization: `Bearer ${loginBody?.token}` };

  // 2. submit self-serve (Case B)
  const submit = await ctx.request.post(`${API2}/api/v1/merchant-signups`, { data: payload });
  const submitBody = submit.ok() ? await submit.json() : null;
  check('submit signup → Pending', submit.ok() && submitBody?.signupId, `status=${submit.status()} signupId=${submitBody?.signupId}`);
  const tenantId = submitBody?.signupId;

  // 3. verify-signup (sysadmin — Bearer)
  const verify = await ctx.request.post(`${API2}/api/v1/tenants/${tenantId}/verify-signup`, { data: {}, headers: auth });
  const verifyText = await verify.text();
  const verifyBody = verify.ok() ? JSON.parse(verifyText) : null;
  check('verify-signup → Active + owner', verify.ok() && verifyBody?.ownerUsername, `status=${verify.status()} body=${verifyText.slice(0, 200)}`);

  // 4. deactivate → downgrade hook (TenantManagementService.DeactivateAsync)
  const deact = await ctx.request.post(`${API2}/api/v1/tenants/${tenantId}/deactivate`, {
    data: { reason: 'rv-w1-trustfields-cleanup' },
    headers: auth,
  });
  check('deactivate tenant', [200, 204].includes(deact.status()), `status=${deact.status()}`);

  console.log(`\nRESULT tenantId=${tenantId} username=${username} phone=${phone} shop=${shopName}`);

  await browser.close();
  const failed = results.filter((r) => !r.ok).length;
  console.log(`=== RV W1 trust-fields API: ${results.length - failed}/${results.length} PASS ===`);
  process.exit(failed ? 1 : 0);
})().catch((e) => { console.error(e); process.exit(1); });