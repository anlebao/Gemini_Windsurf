// RV W1 (2026-10-16) — tạo storageState cho 2 tenant trên production:
// - auth/rv-w1-books.json: Vạn An Trading Co. (DN TT133 — a5b6c7d8) — Sổ Nhật ký chung + Sổ Cái
// - auth/rv-w1-pos.json:   Công Ty TNHH Xây Dựng Hoàng Nguyên (8d26fec2 — sản phẩm 70tr) — POS ≥20tr
// Pattern: rv-booking-auth.cjs (login sysadmin → impersonate tenant) — host-resolver map app2 → gateway IP.
const { chromium } = require('@playwright/test');
const fs = require('fs');

const APP2 = 'https://app2.khachvip.online';
const TENANTS = [
  { id: 'a5b6c7d8-1234-5678-9abc-def012345678', out: 'auth/rv-w1-books.json' }, // Vạn An Trading (DN TT133)
  { id: '8d26fec2-21a7-4b83-8370-4ae1012b5d66', out: 'auth/rv-w1-pos.json' },   // Xây Dựng Hoàng Nguyên (70tr product)
];

(async () => {
  const browser = await chromium.launch({
    headless: true,
    args: [
      '--host-resolver-rules=' +
        'MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, ' +
        'MAP api.khachvip.online 136.85.94.119, MAP khachvip.online 136.85.94.119, MAP www2.khachvip.online 136.85.94.119',
    ],
  });

  for (const t of TENANTS) {
    const context = await browser.newContext({ ignoreHTTPSErrors: true });

    // 1. Login sysadmin (API — set cookie trong context).
    const login = await context.request.post(`${APP2}/api/platform/login`, {
      data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
      headers: { 'Content-Type': 'application/json' },
    });
    if (!login.ok()) {
      console.error(`Login thất bại: ${login.status()} ${await login.text()}`);
      process.exit(1);
    }
    console.log(`Login sysadmin OK (${t.id.slice(0, 8)})`);

    // 2. Impersonate tenant.
    const imp = await context.request.post(`${APP2}/api/admin/impersonate/${t.id}`);
    if (!imp.ok()) {
      console.error(`Impersonate thất bại: ${imp.status()} ${await imp.text()}`);
      process.exit(1);
    }
    console.log(`Impersonate ${t.id.slice(0, 8)} OK`);

    // 3. Truy cập dashboard để hoàn tất session Blazor.
    const page = await context.newPage();
    const resp = await page.goto(`${APP2}/dashboard`, { waitUntil: 'networkidle', timeout: 60000 });
    console.log(`Dashboard ${t.id.slice(0, 8)}: ${resp?.status()}`);

    await context.storageState({ path: t.out });
    console.log(`StorageState saved: ${t.out}`);
    await context.close();
  }

  await browser.close();
})().catch((e) => {
  console.error(e);
  process.exit(1);
});