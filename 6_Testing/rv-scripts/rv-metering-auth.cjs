// RV metering auth — tạo 2 storageState trên production:
// - auth/rv-metering-admin.json:  SystemAdmin (trang /admin/metering/*)
// - auth/rv-metering-tenant.json: impersonate tenant (trang /wallet/phi-nen-tang)
// Pattern: rv-w1-auth.cjs (login thật /api/platform/login → impersonate → save state).
// Lý do: DevLoginController #if DEBUG — KHÔNG tồn tại trong Release build production.
const { chromium } = require('@playwright/test');

const APP2 = 'https://app2.khachvip.online';
const TENANT_ID = 'a5b6c7d8-1234-5678-9abc-def012345678'; // Vạn An Trading (DN TT133)

(async () => {
  const browser = await chromium.launch({
    headless: true,
    args: [
      '--host-resolver-rules=' +
        'MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, ' +
        'MAP api.khachvip.online 136.85.94.119, MAP khachvip.online 136.85.94.119, MAP www2.khachvip.online 136.85.94.119',
    ],
  });

  // 1. SystemAdmin state.
  {
    const context = await browser.newContext({ ignoreHTTPSErrors: true });
    const login = await context.request.post(`${APP2}/api/platform/login`, {
      data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
      headers: { 'Content-Type': 'application/json' },
    });
    if (!login.ok()) { console.error(`Login sysadmin fail: ${login.status()}`); process.exit(1); }
    const page = await context.newPage();
    const resp = await page.goto(`${APP2}/admin/metering/rate-cards`, { waitUntil: 'networkidle', timeout: 60000 });
    console.log(`admin /admin/metering/rate-cards: ${resp?.status()}`);
    await context.storageState({ path: 'auth/rv-metering-admin.json' });
    console.log('saved auth/rv-metering-admin.json');
    await context.close();
  }

  // 2. Tenant state (sysadmin → impersonate Vạn An Trading).
  {
    const context = await browser.newContext({ ignoreHTTPSErrors: true });
    const login = await context.request.post(`${APP2}/api/platform/login`, {
      data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
      headers: { 'Content-Type': 'application/json' },
    });
    if (!login.ok()) { console.error(`Login sysadmin fail: ${login.status()}`); process.exit(1); }
    const imp = await context.request.post(`${APP2}/api/admin/impersonate/${TENANT_ID}`);
    if (!imp.ok()) { console.error(`Impersonate fail: ${imp.status()} ${await imp.text()}`); process.exit(1); }
    const page = await context.newPage();
    const resp = await page.goto(`${APP2}/wallet/phi-nen-tang`, { waitUntil: 'networkidle', timeout: 60000 });
    console.log(`tenant /wallet/phi-nen-tang: ${resp?.status()}`);
    await context.storageState({ path: 'auth/rv-metering-tenant.json' });
    console.log('saved auth/rv-metering-tenant.json');
    await context.close();
  }

  // 3. Real tenant Owner state (NON-SystemAdmin — dùng để test guard /admin/*).
  //    Impersonate GIỮ role SystemAdmin nên không test được guard; phải login user thật.
  //    adminvanan1 login qua UI /Login form (API login không chấp nhận username non-email).
  {
    const context = await browser.newContext({ ignoreHTTPSErrors: true });
    const page = await context.newPage();
    await page.goto(`${APP2}/Login`, { waitUntil: 'networkidle', timeout: 60000 });
    await page.locator('#username').fill('adminvanan1');
    await page.locator('#password').fill('2026@vanan');
    await page.locator('button[type="submit"]').click();
    await page.waitForURL('**/dashboard**', { timeout: 60000 }).catch(async () => {
      console.log(`owner login redirect: ${page.url()}`);
    });
    const resp = await page.goto(`${APP2}/dashboard`, { waitUntil: 'networkidle', timeout: 60000 });
    console.log(`owner /dashboard: ${resp?.status()} url=${page.url()}`);
    await context.storageState({ path: 'auth/rv-metering-owner.json' });
    console.log('saved auth/rv-metering-owner.json');
    await context.close();
  }

  await browser.close();
})().catch((e) => { console.error(e); process.exit(1); });
