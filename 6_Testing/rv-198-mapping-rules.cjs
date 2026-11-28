// RV #198 L3 — /accounting/mapping-rules production probe (tenant a5b6c7d8 Vạn An Trading TT133)
const { chromium } = require('@playwright/test');
const APP2 = 'https://app2.khachvip.online';
const TENANT = 'a5b6c7d8-1234-5678-9abc-def012345678';

(async () => {
  const browser = await chromium.launch({
    headless: true,
    args: ['--host-resolver-rules=MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, MAP api.khachvip.online 136.85.94.119, MAP khachvip.online 136.85.94.119, MAP www2.khachvip.online 136.85.94.119'],
  });
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const logs = [];
  const page = await context.newPage();
  page.on('console', m => { if (m.type() === 'error' || m.type() === 'warning') logs.push(`[${m.type()}] ${m.text()}`); });
  page.on('pageerror', e => logs.push(`[pageerror] ${e.message}`));

  const login = await context.request.post(`${APP2}/api/platform/login`, {
    data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
    headers: { 'Content-Type': 'application/json' },
  });
  console.log(`login: ${login.status()}`);
  const imp = await context.request.post(`${APP2}/api/admin/impersonate/${TENANT}`);
  console.log(`impersonate: ${imp.status()}`);

  const resp = await page.goto(`${APP2}/accounting/mapping-rules`, { waitUntil: 'networkidle', timeout: 90000 });
  console.log(`page status: ${resp?.status()} | final URL: ${page.url()}`);
  await page.waitForTimeout(6000); // Blazor boot + data load

  const bodyText = await page.locator('body').innerText().catch(() => '');
  console.log(`title/h1 present: ${/mapping|quy tắc|rule/i.test(bodyText)}`);
  const rowCount = await page.locator('table tbody tr, .vanan-grid-row, [data-testid*="row"]').count();
  console.log(`grid rows: ${rowCount}`);
  const gridText = await page.locator('table, .vanan-grid, main').first().innerText().catch(() => '(no grid)');
  const events = ['manual-receipt','manual-expense','order-revenue','order-vat','order-cogs','purchase','purchase-cash','transfer-in','transfer-out','transfer-fee','deposit-offset','receivable','receivable-payment','payable-payment'];
  const found = events.filter(e => gridText.includes(e));
  console.log(`event codes rendered (${found.length}/${events.length}): ${found.join(', ')}`);
  const hasError = /unhandled|error|exception|không tải được|not found/i.test(bodyText);
  console.log(`error text on page: ${hasError}`);
  await page.screenshot({ path: 'rv-198-mapping-rules.png', fullPage: true });
  console.log(`console/page errors: ${logs.length}`);
  logs.slice(0, 10).forEach(l => console.log('  ' + l));
  await browser.close();
  const pass = resp?.status() === 200 && rowCount > 0 && !hasError;
  console.log(`RESULT: ${pass ? 'PASS' : 'FAIL'}`);
  process.exit(pass ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
