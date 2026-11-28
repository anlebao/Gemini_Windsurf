// RV Regime-Lock (2026-10-30) — commit c2c1b052 + migration fix 98914068
// Sweep: HKD guard → HKD elect TT58 qua admin modal → TT58 effective → SME TT133 → HTX TT71 → cleanup revert.
// Write-path test dùng tenant HKD demo "Vạn An Cafe (HKD Group 1)" + cleanup về default sau khi xong.
const { chromium } = require('@playwright/test');

const APP2 = 'https://app2.khachvip.online';
const HKD = '00000000-0000-0000-0000-000000000001'; // Vạn An Cafe (HKD Group 1) — demo HKD tenant
const SME = '7c021960-6d6c-4de4-88a1-cf8373184f49'; // Cafe Tân Quy (Type=3)
const HTX = 'a5b6c7d8-1234-5678-9abc-def012345678'; // Vạn An Trading (Type=5)

const results = [];
function check(name, ok, extra = '') {
  results.push({ name, ok, extra });
  console.log(`${ok ? 'PASS' : 'FAIL'} ${name}${extra ? ' — ' + extra : ''}`);
}

async function newContext(browser, login = false) {
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();
  const consoleErrors = [];
  page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  page.on('pageerror', (e) => consoleErrors.push('PAGEERROR: ' + e.message));
  if (login) {
    const res = await context.request.post(`${APP2}/api/platform/login`, {
      data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
      headers: { 'Content-Type': 'application/json' },
    });
    if (!res.ok()) throw new Error('Login thất bại: ' + res.status());
  }
  return { context, page, consoleErrors };
}

async function impersonate(context, tenantId, label) {
  const imp = await context.request.post(`${APP2}/api/admin/impersonate/${tenantId}`);
  const txt = await imp.text();
  if (!imp.ok() || !txt.includes('"success":true')) {
    throw new Error(`Impersonate ${label} thất bại: ${imp.status()} ${txt.slice(0, 150)}`);
  }
}

async function body(page, url) {
  const resp = await page.goto(`${APP2}${url}`, { waitUntil: 'networkidle', timeout: 60000 });
  if (!resp || resp.status() !== 200) throw new Error(`${url} → HTTP ${resp?.status()}`);
  await page.waitForTimeout(1500);
  return page.locator('body').innerText();
}

(async () => {
  const browser = await chromium.launch({
    headless: true,
    args: ['--host-resolver-rules=MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, MAP api.khachvip.online 136.85.94.119'],
  });

  // ── Phase 0: sysadmin session cho admin modal (KHÔNG impersonate) ──
  const admin = await newContext(browser, true);
  console.log('Login sysadmin OK');

  // ═══ Phase 1: HKD chưa bầu chọn → guard "không áp dụng" ═══
  {
    const s = await newContext(browser, true);
    await impersonate(s.context, HKD, 'HKD demo');
    const bs = await body(s.page, '/accounting/balance-sheet');
    check('HKD balance-sheet guard "không áp dụng"',
      bs.includes('không áp dụng') || bs.includes('Không áp dụng') || bs.includes('TT 152'));
    check('HKD balance-sheet link Sổ HKD', bs.includes('Sổ HKD'));
    const idx = await body(s.page, '/accounting');
    check('HKD index có Sổ HKD button', idx.includes('Sổ HKD'));
    check('HKD index KHÔNG có BCTC button', !idx.includes('Báo Cáo Tài Chính'));
    const hub = await body(s.page, '/accounting/financial-reports');
    check('HKD hub guard (không B01a-DNN links)',
      !hub.includes('B 01a-DNN') && (hub.includes('không áp dụng') || hub.includes('Không áp dụng') || hub.includes('TT 152')));
    await s.page.screenshot({ path: 'rv-rl-hkd-guard.png', fullPage: true });
    check('HKD console errors', s.consoleErrors.length === 0, s.consoleErrors.slice(0, 2).join(' | '));
    await s.context.close();
  }

  // ═══ Phase 2: Write-path — modal "Chế độ kế toán" elect TT58 cho HKD demo ═══
  {
    const resp = await admin.page.goto(`${APP2}/admin/tenants`, { waitUntil: 'networkidle', timeout: 60000 });
    check('Admin tenants page load', resp?.status() === 200);
    await admin.page.waitForTimeout(2500);
    const row = admin.page.locator('tr', { hasText: 'HKD Group 1' });
    check('HKD demo row found', await row.count() >= 1);
    await row.first().locator('button[title*="Chế độ kế toán"]').click();
    await admin.page.waitForTimeout(1200);
    // Scope select TRONG modal — tránh nhầm filter dropdown của bảng tenants
    const modalSelects = admin.page.locator('[class*="modal"] select.vanan-select');
    check('Modal select rendered', await modalSelects.count() >= 1, `count=${await modalSelects.count()}`);
    await modalSelects.first().selectOption('2'); // TT58_2026
    await admin.page.waitForTimeout(600);
    check('DnsnTaxMethod select appears for TT58', await modalSelects.count() === 2, `count=${await modalSelects.count()}`);
    await admin.page.locator('[class*="modal"] button:has-text("Lưu")').click();
    await admin.page.waitForTimeout(2500);
    const pageText = await admin.page.locator('body').innerText();
    check('Save success alert', pageText.includes('Đã lưu chế độ kế toán'));
    await admin.page.screenshot({ path: 'rv-rl-admin-modal.png' });
  }

  // ── chờ NATS → SQLite mirror ──
  await new Promise(r => setTimeout(r, 4000));

  // ═══ Phase 3: HKD elected TT58 → effective TT58 + badge tự nguyện ═══
  {
    const s = await newContext(browser, true);
    await impersonate(s.context, HKD, 'HKD→TT58');
    const bs = await body(s.page, '/accounting/balance-sheet');
    check('HKD→TT58 balance-sheet hiện TT 58', bs.includes('TT 58/2026') || bs.includes('TT 58'));
    check('HKD→TT58 badge tự nguyện', bs.includes('tự nguyện'));
    // Guard chứa link "Mở Sổ HKD" — effective TT58 thì không còn link này (dùng link thay vì text "không áp dụng" vì guard message tự chứa "TT 58").
    check('HKD→TT58 không còn guard (không link Mở Sổ HKD)', !bs.includes('Mở Sổ HKD'));
    const idx = await body(s.page, '/accounting');
    check('HKD→TT58 index có Sổ DNSN', idx.includes('Sổ DNSN'));
    check('HKD→TT58 index có BCTC', idx.includes('Báo Cáo Tài Chính'));
    const hub = await body(s.page, '/accounting/financial-reports');
    check('HKD→TT58 hub hiện DNSN', hub.includes('DNSN') || hub.includes('TT 58'));
    const dnsn = await body(s.page, '/accounting/dnsn-books');
    check('HKD→TT58 dnsn-books render', !dnsn.includes('Error') && dnsn.length > 100);
    await s.page.screenshot({ path: 'rv-rl-hkd-tt58.png', fullPage: true });
    check('HKD→TT58 console errors', s.consoleErrors.length === 0, s.consoleErrors.slice(0, 2).join(' | '));
    await s.context.close();
  }

  // ═══ Phase 4: SME → chỉ TT133 (nhãn tĩnh, không select) ═══
  {
    const s = await newContext(browser, true);
    await impersonate(s.context, SME, 'Cafe Tân Quy');
    const bs = await body(s.page, '/accounting/balance-sheet');
    check('SME balance-sheet hiện TT 133', bs.includes('TT 133/2016'));
    check('SME balance-sheet KHÔNG hiện chuẩn khác', !bs.includes('TT 99/2025') && !bs.includes('TT 71/2024') && !bs.includes('TT 58/2026'));
    const stdSelect = s.page.locator('label:has-text("Chuẩn kế toán") select');
    check('SME không có select (1 chuẩn → nhãn tĩnh)', await stdSelect.count() === 0);
    const hub = await body(s.page, '/accounting/financial-reports');
    check('SME hub hiện B 01a-DNN', hub.includes('B 01a-DNN'));
    check('SME hub không hiện HTX', !hub.includes('HTX'));
    const idx = await body(s.page, '/accounting');
    check('SME index có BCTC, không Sổ DNSN/HKD', idx.includes('Báo Cáo Tài Chính') && !idx.includes('Sổ DNSN') && !idx.includes('Sổ HKD'));
    check('SME console errors', s.consoleErrors.length === 0, s.consoleErrors.slice(0, 2).join(' | '));
    await s.context.close();
  }

  // ═══ Phase 5: HTX → chỉ TT71 ═══
  {
    const s = await newContext(browser, true);
    await impersonate(s.context, HTX, 'Vạn An Trading');
    const bs = await body(s.page, '/accounting/balance-sheet');
    check('HTX balance-sheet hiện TT 71', bs.includes('TT 71/2024'));
    const hub = await body(s.page, '/accounting/financial-reports');
    check('HTX hub hiện B 01-HTX', hub.includes('B 01-HTX'));
    check('HTX hub không B03', !hub.includes('B 03'));
    const idx = await body(s.page, '/accounting');
    check('HTX index có BCTC, không Sổ DNSN', idx.includes('Báo Cáo Tài Chính') && !idx.includes('Sổ DNSN'));
    check('HTX console errors', s.consoleErrors.length === 0, s.consoleErrors.slice(0, 2).join(' | '));
    await s.context.close();
  }

  // ═══ Phase 6: CLEANUP — revert HKD demo về default ═══
  {
    // Modal Phase 2 không tự đóng sau Lưu → reload trang để clear overlay.
    await admin.page.goto(`${APP2}/admin/tenants`, { waitUntil: 'networkidle', timeout: 60000 });
    await admin.page.waitForTimeout(2500);
    const row = admin.page.locator('tr', { hasText: 'HKD Group 1' });
    await row.first().locator('button[title*="Chế độ kế toán"]').click();
    await admin.page.waitForTimeout(1200);
    const modalSelects = admin.page.locator('[class*="modal"] select.vanan-select');
    await modalSelects.first().selectOption(''); // — Mặc định —
    await admin.page.locator('[class*="modal"] button:has-text("Lưu")').click();
    await admin.page.waitForTimeout(2500);
    const pageText = await admin.page.locator('body').innerText();
    check('Cleanup revert success', pageText.includes('Đã lưu chế độ kế toán'));

    await new Promise(r => setTimeout(r, 4000));
    const s = await newContext(browser, true);
    await impersonate(s.context, HKD, 'HKD cleanup');
    const bs = await body(s.page, '/accounting/balance-sheet');
    check('HKD sau revert → guard trở lại',
      bs.includes('không áp dụng') || bs.includes('Không áp dụng') || bs.includes('TT 152'));
    await s.context.close();
    check('Admin console errors', admin.consoleErrors.length === 0, admin.consoleErrors.slice(0, 2).join(' | '));
  }

  await admin.context.close();
  await browser.close();

  const failed = results.filter(r => !r.ok);
  console.log(`\n=== ${results.length - failed.length}/${results.length} PASS — ${failed.length} FAIL ===`);
  process.exit(failed.length ? 1 : 0);
})().catch((e) => { console.error('FATAL:', e.message); process.exit(1); });
