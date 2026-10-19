// RV 2026-10-19 (issue #195) — tạo 2 storageState trên production:
//   auth/rv-195-admin.json : sysadmin thuần (test /admin/order-flow)
//   auth/rv-195-owner.json : sysadmin impersonate Test HKD (test /orders, /orders/{id})
// Pattern: rv-booking-auth.cjs — host-resolver map app2 → gateway IP.
const { chromium } = require('@playwright/test');

const APP2 = 'https://app2.khachvip.online';
const TENANT_ID = 'a5b6c7d8-1234-5678-9abc-def012345678'; // tenant có 60 orders trên app2 replica
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
  await ctxAdmin.storageState({ path: 'auth/rv-195-admin.json' });
  console.log('StorageState saved: auth/rv-195-admin.json');
  await ctxAdmin.close();

  // 2. Owner state (impersonate Test HKD)
  const ctxOwner = await browser.newContext({ ignoreHTTPSErrors: true });
  await loginSysadmin(ctxOwner);
  const imp = await ctxOwner.request.post(`${APP2}/api/admin/impersonate/${TENANT_ID}`);
  if (!imp.ok()) {
    console.error(`Impersonate thất bại: ${imp.status()} ${await imp.text()}`);
    process.exit(1);
  }
  console.log(`Impersonate ${TENANT_ID} OK`);
  const pOwner = await ctxOwner.newPage();
  await pOwner.goto(`${APP2}/dashboard`, { waitUntil: 'networkidle', timeout: 60000 });
  await ctxOwner.storageState({ path: 'auth/rv-195-owner.json' });
  console.log('StorageState saved: auth/rv-195-owner.json');
  await ctxOwner.close();

  await browser.close();
})().catch((e) => {
  console.error(e);
  process.exit(1);
});
