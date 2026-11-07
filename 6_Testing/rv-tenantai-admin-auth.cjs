// Task AI-P10 RV (2026-11-07) — storageState cho admin UI /admin/tenant-ai/* trên production.
// Pattern rv-booking-auth.cjs: login sysadmin → impersonate tenant An Tâm (pilot, TenantAiEnabled=ON).
const { chromium } = require('@playwright/test');

const APP2 = 'https://app2.khachvip.online';
const TENANT_ID = 'e2d91cdf-d3c4-4399-b96e-e772a7a41630'; // An Tâm pilot (TenantAiEnabled)
const OUT = 'auth/rv-tenantai-admin.json';

(async () => {
  const browser = await chromium.launch({
    headless: true,
    args: [
      '--host-resolver-rules=' +
        'MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, ' +
        'MAP api.khachvip.online 136.85.94.119, MAP khachvip.online 136.85.94.119, MAP www2.khachvip.online 136.85.94.119',
    ],
  });
  const context = await browser.newContext({ ignoreHTTPSErrors: true });

  // 1. Login sysadmin (API — set cookie trong context).
  const login = await context.request.post(`${APP2}/api/platform/login`, {
    data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
    headers: { 'Content-Type': 'application/json' },
  });
  if (!login.ok()) {
    console.error(`Login thất bại: ${login.status()}`);
    process.exit(1);
  }
  console.log('Login sysadmin OK');

  // 2. Impersonate tenant An Tâm (Owner quyền — policy StaffOrAbove đủ dùng).
  const imp = await context.request.post(`${APP2}/api/admin/impersonate/${TENANT_ID}`);
  if (!imp.ok()) {
    console.error(`Impersonate thất bại: ${imp.status()} ${await imp.text()}`);
    process.exit(1);
  }
  console.log(`Impersonate ${TENANT_ID} OK`);

  // 3. Truy cập dashboard để hoàn tất session Blazor.
  const page = await context.newPage();
  const resp = await page.goto(`${APP2}/dashboard`, { waitUntil: 'networkidle', timeout: 60000 });
  console.log(`Dashboard: ${resp?.status()}`);

  await context.storageState({ path: OUT });
  console.log(`StorageState saved: ${OUT}`);
  await browser.close();
})().catch((e) => {
  console.error(e);
  process.exit(1);
});
