// RV W5/F (2026-10-31) — verify "Tạo Bút Toán Kết Chuyển" on /accounting/period-closing
// production (app2): login sysadmin → impersonate Test Minimal (Type=3 TT133)
// → click btn-gen-closing → read closing-gen-message. JEs verified via psql separately.
const { chromium } = require('@playwright/test');

const APP2 = 'https://app2.khachvip.online';
const TENANT = '4345dff8-b4b7-411f-b8f1-faf36f242d70'; // Test Minimal (Type 3 — DN TT133)

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
  try {
    // 1. Login sysadmin (cookie trong context).
    const login = await context.request.post(`${APP2}/api/platform/login`, {
      data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
      headers: { 'Content-Type': 'application/json' },
    });
    if (!login.ok()) { console.error(`LOGIN_FAIL ${login.status()}`); process.exit(1); }
    console.log('login OK');

    // 2. Impersonate Test Minimal.
    const imp = await context.request.post(`${APP2}/api/admin/impersonate/${TENANT}`);
    if (!imp.ok()) { console.error(`IMPERSONATE_FAIL ${imp.status()} ${await imp.text()}`); process.exit(1); }
    console.log('impersonate OK');

    // 3. Vào trang đóng kỳ — chờ circuit connect.
    const page = await context.newPage();
    const resp = await page.goto(`${APP2}/accounting/period-closing`, { waitUntil: 'networkidle', timeout: 60000 });
    console.log(`page status: ${resp?.status()}`);
    await page.waitForTimeout(9000); // Pattern #29 — chờ Blazor circuit connect

    // 4. Chờ status card + nút kết chuyển (period Open → nút hiện).
    const btn = page.locator("[data-testid='btn-gen-closing']");
    await btn.waitFor({ state: 'visible', timeout: 30000 });
    console.log('btn-gen-closing visible');

    await btn.click();
    const msg = page.locator("[data-testid='closing-gen-message']");
    await msg.waitFor({ state: 'visible', timeout: 30000 });
    console.log(`RESULT: ${(await msg.innerText()).trim()}`);
  } finally {
    await context.close();
    await browser.close();
  }
})().catch((e) => { console.error('ERROR:', e.message); process.exit(1); });
