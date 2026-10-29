// RV 2026-10-29 (issue #195 booking-payment) — storageState impersonate Test HKD:
//   auth/rv-bp-hkd.json
// Pattern: rv-195b-auth.cjs — host-resolver map app2 → gateway IP.
const { chromium } = require('@playwright/test');

const APP2 = 'https://app2.khachvip.online';
const TENANT_HKD = '6aaf19e4-8b55-48cd-94d7-b61c96a2d66d'; // Test HKD
const RESOLVER =
  '--host-resolver-rules=' +
  'MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, ' +
  'MAP api.khachvip.online 136.85.94.119, MAP khachvip.online 136.85.94.119, MAP www2.khachvip.online 136.85.94.119';

(async () => {
  const browser = await chromium.launch({ headless: true, args: [RESOLVER] });
  const ctx = await browser.newContext({ ignoreHTTPSErrors: true });

  const login = await ctx.request.post(`${APP2}/api/platform/login`, {
    data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
    headers: { 'Content-Type': 'application/json' },
  });
  if (!login.ok()) { console.error(`Login thất bại: ${login.status()}`); process.exit(1); }
  console.log('Login sysadmin OK');

  const imp = await ctx.request.post(`${APP2}/api/admin/impersonate/${TENANT_HKD}`);
  if (!imp.ok()) { console.error(`Impersonate thất bại: ${imp.status()} ${await imp.text()}`); process.exit(1); }
  console.log(`Impersonate ${TENANT_HKD} OK`);

  const p = await ctx.newPage();
  await p.goto(`${APP2}/dashboard`, { waitUntil: 'networkidle', timeout: 60000 });
  await ctx.storageState({ path: 'auth/rv-bp-hkd.json' });
  console.log('StorageState saved: auth/rv-bp-hkd.json');
  await ctx.close();
  await browser.close();
})().catch((e) => { console.error(e); process.exit(1); });