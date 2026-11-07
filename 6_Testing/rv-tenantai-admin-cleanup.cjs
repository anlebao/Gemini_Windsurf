// Task AI-P10 RV CLEANUP (2026-11-07) — dọn dữ liệu test trên production An Tâm:
// 1) doc pending "E2E FAQ ..." mồ côi (spec run fail trước approve) → xóa qua docs tab.
// 2) profile published bị spec publish "Bé E2E" → rollback về bản published gần nhất
//    không phải tên test (E2E/RVP10).
const { chromium } = require('@playwright/test');

const APP2 = 'https://app2.khachvip.online';
const TENANT = 'e2d91cdf-d3c4-4399-b96e-e772a7a41630';
const MAP = '--host-resolver-rules=MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119';
const TEST_NAME = /E2E FAQ|RV-P10|Bé RVP10|Bé E2E/i;

(async () => {
  const browser = await chromium.launch({ headless: true, args: [MAP] });
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const log = m => console.log(m);

  const login = await context.request.post(`${APP2}/api/platform/login`, {
    data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
    headers: { 'Content-Type': 'application/json' },
  });
  if (!login.ok()) { console.error('login fail'); process.exit(1); }
  const imp = await context.request.post(`${APP2}/api/admin/impersonate/${TENANT}`);
  if (!imp.ok()) { console.error('impersonate fail'); process.exit(1); }

  const page = await context.newPage();

  // ---- 1) docs cleanup ----
  await page.goto(`${APP2}/admin/tenant-ai/knowledge`, { waitUntil: 'networkidle', timeout: 60000 });
  for (const status of ['pending_review', 'approved', 'rejected', '']) {
    await page.getByTestId('tab-docs').click();
    await page.getByTestId('select-status-filter').selectOption(status);
    await page.waitForTimeout(1500);
    for (let guard = 0; guard < 10; guard++) {
      const item = page.getByTestId('doc-item').filter({ hasText: TEST_NAME }).first();
      if (!(await item.isVisible({ timeout: 3000 }).catch(() => false))) break;
      await item.getByTestId('btn-delete-doc').click();
      await item.getByTestId('btn-confirm-delete').click();
      await page.waitForTimeout(2000);
      log(`deleted doc [${status || 'all'}] match`);
    }
  }

  // ---- 2) profile restore ----
  await page.goto(`${APP2}/admin/tenant-ai/profile`, { waitUntil: 'networkidle', timeout: 60000 });
  const pub = (await page.getByTestId('published-summary')
    .textContent({ timeout: 10000 }).catch(() => '')) || '';
  log(`published now: ${pub.trim().slice(0, 90)}`);
  if (TEST_NAME.test(pub)) {
    const sel = page.getByTestId('select-rollback-version');
    const options = await sel.locator('option')
      .evaluateAll(os => os.map(o => ({ v: o.value, t: o.textContent.trim() })));
    // chọn bản non-test mới nhất (list đã sort desc) — dùng value attr (label
    // "v@v.Version" đang bug email-literal trên bản prod hiện tại).
    const pick = options.find(o => o.v !== '0' && !TEST_NAME.test(o.t) && !o.t.includes('(draft)'));
    if (!pick) { log('KHÔNG tìm được bản rollback sạch — cần xử tay.'); }
    else {
      log(`rollback về: ${pick.t} (value=${pick.v})`);
      await sel.selectOption(pick.v);
      await page.getByTestId('btn-rollback').click();
      await page.getByTestId('btn-confirm-rollback').click();
      await page.waitForTimeout(3000);
      const now = (await page.getByTestId('published-summary')
        .textContent({ timeout: 15000 }).catch(() => '')) || '';
      log(`published sau rollback: ${now.trim().slice(0, 90)}`);
    }
  } else log('published đã là bản thật — không cần rollback.');

  await browser.close();
})().catch(e => { console.error('FATAL', e); process.exit(2); });
