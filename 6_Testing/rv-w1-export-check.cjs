// RV 2026-10-17 — kiểm tra xuất Excel Sổ Nhật ký + Sổ Cái trên production (app2.khachvip.online).
// Dùng storageState auth/rv-w1-books.json (Vạn An Trading — Journal T10/2026 + TK 111 có data).
// Pattern: host-resolver map app2 → gateway IP (như playwright-rv-w1.config.ts).
const { chromium } = require('@playwright/test');

const APP2 = 'https://app2.khachvip.online';

(async () => {
  const browser = await chromium.launch({
    headless: true,
    args: [
      '--host-resolver-rules=' +
        'MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, ' +
        'MAP api.khachvip.online 136.85.94.119, MAP khachvip.online 136.85.94.119, MAP www2.khachvip.online 136.85.94.119',
    ],
  });
  const context = await browser.newContext({
    ignoreHTTPSErrors: true,
    storageState: 'auth/rv-w1-books.json',
  });
  const page = await context.newPage();

  // ── 1. Sổ Nhật ký chung → Xuất Excel ──
  await page.goto(`${APP2}/accounting/journal`, { waitUntil: 'networkidle', timeout: 60000 });
  await page.locator('h1:has-text("Sổ Nhật Ký Chung")').waitFor({ timeout: 30000 });
  await page.waitForTimeout(8000); // circuit connect

  const dl1 = page.waitForEvent('download', { timeout: 30000 });
  await page.getByRole('button', { name: /Xuất Excel/ }).click();
  const d1 = await dl1;
  const p1 = await d1.path(); require('fs').copyFileSync(p1, 'rv-journal.xlsx');
  console.log(`[Journal] download: ${d1.suggestedFilename()} | size: ${require('fs').statSync(p1).size} bytes`);

  // ── 2. Sổ Cái TK 111 → Xuất Excel ──
  await page.goto(`${APP2}/accounting/ledger`, { waitUntil: 'networkidle', timeout: 60000 });
  await page.locator('h1:has-text("Sổ Cái")').waitFor({ timeout: 30000 });
  await page.waitForTimeout(8000);
  const accSel = page.locator('select:has(option[value="111"])');
  await accSel.selectOption('111');
  await page.getByRole('button', { name: 'Áp dụng' }).click();
  await page.locator('text=Sổ cái tài khoản 111').first().waitFor({ timeout: 20000 });

  const dl2 = page.waitForEvent('download', { timeout: 30000 });
  await page.getByRole('button', { name: /Xuất Excel/ }).click();
  const d2 = await dl2;
  const p2 = await d2.path(); require('fs').copyFileSync(p2, 'rv-ledger.xlsx');
  console.log(`[Sổ Cái] download: ${d2.suggestedFilename()} | size: ${require('fs').statSync(p2).size} bytes`);

  await browser.close();
  console.log('RV EXPORT CHECK DONE');
})().catch((e) => {
  console.error('RV EXPORT CHECK FAILED:', e.message);
  process.exit(1);
});