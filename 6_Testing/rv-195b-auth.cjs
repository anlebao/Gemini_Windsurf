// RV 2026-10-29 (issue #195 follow-up) — tạo 2 storageState trên production:
//   auth/rv-195b-admin.json : sysadmin thuần (test /admin/order-flow help + guard)
//   auth/rv-195b-quy.json   : sysadmin impersonate Cafe Tân Quy (test bill + flow-aware + transition)
// Pattern: rv-195-auth.cjs — host-resolver map app2 → gateway IP.
const { chromium } = require('@playwright/test');

const APP2 = 'https://app2.khachvip.online';
const TENANT_QUY = '7c021960-6d6c-4de4-88a1-cf8373184f49'; // Cafe Tân Quy
const RESOLVER =
  '--host-resolver-rules=' +
  'MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, ' +
  'MAP api.khachvip.online 136.85.94.119, MAP khachvip.online 136.85.94.119, MAP www2.khachvip.online 136.85.94.119';

async function loginSysadmin(context) {
  const login = await context.request.post(`${APP2}/api/platform/login`, {
    data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
    headers: { 'Content-Type': 'application/json' },
  });
  if (!login.ok()) {
    console.error(`Login thất bại: ${login.status()}`);
    process.exit(1);
  }
  console.log('Login sysadmin OK');
}

(async () => {
  const browser = await chromium.launch({ headless: true, args: [RESOLVER] });

  // 1. Admin state (no impersonation)
  const ctxAdmin = await browser.newContext({ ignoreHTTPSErrors: true });
  await loginSysadmin(ctxAdmin);
  const pAdmin = await ctxAdmin.newPage();
  await pAdmin.goto(`${APP2}/dashboard`, { waitUntil: 'networkidle', timeout: 60000 });
  await ctxAdmin.storageState({ path: 'auth/rv-195b-admin.json' });
  console.log('StorageState saved: auth/rv-195b-admin.json');
  await ctxAdmin.close();

  // 2. Cafe Tân Quy state (impersonate)
  const ctxQuy = await browser.newContext({ ignoreHTTPSErrors: true });
  await loginSysadmin(ctxQuy);
  const imp = await ctxQuy.request.post(`${APP2}/api/admin/impersonate/${TENANT_QUY}`);
  if (!imp.ok()) {
    console.error(`Impersonate thất bại: ${imp.status()} ${await imp.text()}`);
    process.exit(1);
  }
  console.log(`Impersonate ${TENANT_QUY} OK`);
  const pQuy = await ctxQuy.newPage();
  await pQuy.goto(`${APP2}/dashboard`, { waitUntil: 'networkidle', timeout: 60000 });
  await ctxQuy.storageState({ path: 'auth/rv-195b-quy.json' });
  console.log('StorageState saved: auth/rv-195b-quy.json');
  await ctxQuy.close();

  await browser.close();
})().catch((e) => {
  console.error(e);
  process.exit(1);
});