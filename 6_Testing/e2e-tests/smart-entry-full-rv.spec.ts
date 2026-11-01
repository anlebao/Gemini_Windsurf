import { test, expect, Page } from '@playwright/test';

// RV TOÀN BỘ kịch bản trong docs/guides/smart-entry-user-guide.md (task PG Smart Entry AI).
// Bao phủ: §9.1 Phiếu Thu T1-T10 · §9.2 Phiếu Chi C1-C12 · §9.3 Chuyển tiền X1-X4 ·
// §10 tình huống thất bại F1-F23 (mọi khẳng định đã đối chiếu parser thật qua probe local).
// READ-ONLY: parse + apply + prefill, KHÔNG submit phiếu lên production.
// Auth: platform login + impersonate Cafe Tân Quy (flag SmartEntryEnabled ON).

const PILOT_TENANT = '7c021960-6d6c-4de4-88a1-cf8373184f49';

async function loginAndImpersonate(page: Page, tenantId: string) {
  await page.goto('/login');
  await page.waitForLoadState('domcontentloaded');
  const loginOk = await page.evaluate(async () => {
    const r = await fetch('/api/platform/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ Username: 'sysadmin@vanan.vn', Password: '2026@vanan' }),
      credentials: 'include',
    });
    return r.ok;
  });
  expect(loginOk, 'platform login should succeed').toBeTruthy();
  const impOk = await page.evaluate(async (t) => {
    const r = await fetch(`/api/admin/impersonate/${t}`, { method: 'POST', credentials: 'include' });
    return r.ok;
  }, tenantId);
  expect(impOk, `impersonate ${tenantId} should succeed`).toBeTruthy();
}

async function openAiMode(page: Page, path: string) {
  // Nav flaky trên prod (Download interruption / Blazor circuit chậm) → retry + reload 1 lần
  for (let attempt = 1; attempt <= 2; attempt++) {
    try {
      await page.goto(path, { waitUntil: 'load' });
      break;
    } catch (e) {
      if (attempt === 2 || !String(e).includes('Download is starting')) throw e;
      await page.waitForTimeout(1500);
    }
  }
  await page.waitForLoadState('networkidle');
  const toggle = page.getByTestId('entry-mode-toggle');
  try {
    await toggle.waitFor({ state: 'visible', timeout: 30000 });
  } catch {
    await page.reload({ waitUntil: 'load' });
    await page.waitForLoadState('networkidle');
    await toggle.waitFor({ state: 'visible', timeout: 30000 });
  }
  await toggle.getByText('Nhập bằng mô tả').click();
  await expect(page.getByTestId('smart-entry-input')).toBeVisible({ timeout: 15000 });
}

async function parseText(page: Page, text: string) {
  await page.getByTestId('smart-entry-input').fill(text);
  await page.getByTestId('smart-entry-parse').click();
  await expect(page.getByTestId('smart-entry-result')).toBeVisible({ timeout: 15000 });
}

interface CardExpect {
  intent: string;
  contains?: string[];          // chuỗi con trong card kết quả
  missing?: string[];           // chuỗi con trong dòng "⚠ Thiếu: ..."
  warnings?: string[];          // chuỗi con của warning (không kèm emoji)
  confidence?: 'High' | 'Medium' | 'Low';
  date?: string;                // "Ngày: dd/MM/yyyy" nếu cần chính xác
}

async function expectCard(page: Page, e: CardExpect) {
  const card = page.getByTestId('smart-entry-result');
  await expect(page.getByTestId('smart-entry-intent')).toHaveText(e.intent);
  if (e.confidence) await expect(page.getByTestId('smart-entry-confidence')).toHaveText(e.confidence);
  if (e.date) await expect(card).toContainText(`Ngày: ${e.date}`);
  for (const s of e.contains ?? []) await expect(card).toContainText(s);
  for (const s of e.warnings ?? []) await expect(card).toContainText(s);
  if ((e.missing ?? []).length) {
    await expect(page.getByTestId('smart-entry-missing')).toBeVisible();
    for (const s of e.missing!) await expect(page.getByTestId('smart-entry-missing')).toContainText(s);
  }
}

function relDate(offset: number): string {
  const x = new Date();
  x.setDate(x.getDate() + offset);
  return `${String(x.getDate()).padStart(2, '0')}/${String(x.getMonth() + 1).padStart(2, '0')}/${x.getFullYear()}`;
}

async function applyToForm(page: Page) {
  await page.getByTestId('smart-entry-apply').click();
}

test.describe('RV Full Guide Matrix — Smart Entry AI (production, read-only)', () => {
  test('§9.1 Revenue T1-T10 — parse đúng + apply prefill', async ({ page }) => {
    await loginAndImpersonate(page, PILOT_TENANT);
    await openAiMode(page, '/accounting/revenue');

    await parseText(page, 'thu tiền bán hàng 2 triệu tiền mặt');
    await expectCard(page, { intent: 'Thu doanh thu', confidence: 'High', contains: ['Số tiền: 2,000,000', 'PTTT: cash'] });
    await applyToForm(page);
    await expect(page.getByTestId('voucher-type-select')).toHaveValue('revenue');

    await parseText(page, 'thu doanh thu 15 triệu hôm qua chuyển khoản');
    await expectCard(page, { intent: 'Thu doanh thu', confidence: 'High', contains: ['Số tiền: 15,000,000', 'PTTT: bank'], date: relDate(-1) });

    await parseText(page, 'bán hàng thu 500k qr momo');
    await expectCard(page, { intent: 'Thu doanh thu', confidence: 'High', contains: ['Số tiền: 500,000', 'PTTT: qr'] });

    await parseText(page, 'bán chịu cho khách chị Lan 8 triệu');
    await expectCard(page, { intent: 'Ghi nhận phải thu (bán chịu)', confidence: 'High', contains: ['Số tiền: 8,000,000', 'Đối tượng: chị Lan'] });
    await applyToForm(page);
    await expect(page.getByTestId('voucher-type-select')).toHaveValue('receivable');
    await expect(page.getByTestId('doi-tuong-input')).toHaveValue('chị Lan');

    await parseText(page, 'khách anh Hùng trả nợ 5 triệu bằng chuyển khoản');
    await expectCard(page, { intent: 'Thu tiền khách trả nợ', confidence: 'High', contains: ['Số tiền: 5,000,000', 'Đối tượng: anh Hùng', 'PTTT: bank'] });

    await parseText(page, 'khách công ty TNHH ABC thanh toán nợ 1 tỷ 2 ngày 28/10 ck');
    await expectCard(page, { intent: 'Thu tiền khách trả nợ', confidence: 'High', contains: ['Số tiền: 1,200,000,000', 'Đối tượng: công ty TNHH ABC', 'PTTT: bank'], date: '28/10/2026' });

    await parseText(page, 'thu nợ 2tr5 khách anh Minh trả góp');
    await expectCard(page, { intent: 'Thu tiền khách trả nợ', confidence: 'High', contains: ['Số tiền: 2,500,000', 'Đối tượng: anh Minh'] });

    await parseText(page, 'đặt cọc của khách anh Nam 3 triệu');
    await expectCard(page, { intent: 'Khách ứng trước/cọc (trả trước)', confidence: 'High', contains: ['Số tiền: 3,000,000', 'Đối tượng: anh Nam'], warnings: ['trả trước'] });

    await parseText(page, 'chủ góp vốn thêm 200 triệu bằng tiền mặt');
    await expectCard(page, { intent: 'Góp vốn chủ sở hữu', confidence: 'High', contains: ['Số tiền: 200,000,000', 'PTTT: cash'] });
    await applyToForm(page);
    await expect(page.getByTestId('voucher-type-select')).toHaveValue('capital-contribution');

    await parseText(page, 'nhận tiền 7 triệu dịch vụ tư vấn ngày 30/10 chuyển khoản');
    await expectCard(page, { intent: 'Thu doanh thu', confidence: 'High', contains: ['Số tiền: 7,000,000', 'PTTT: bank'], date: '30/10/2026' });
  });

  test('§9.2 Expense C1-C12 — parse đúng + apply prefill', async ({ page }) => {
    await loginAndImpersonate(page, PILOT_TENANT);
    await openAiMode(page, '/accounting/expenses');

    await parseText(page, 'chi tiền thuê mặt bằng 15 triệu hôm qua');
    await expectCard(page, { intent: 'Chi phí', confidence: 'High', contains: ['Số tiền: 15,000,000', 'Nhóm chi: rent'], date: relDate(-1) });

    await parseText(page, 'chi 2tr5 tiền điện tháng 10');
    await expectCard(page, { intent: 'Chi phí', confidence: 'High', contains: ['Số tiền: 2,500,000', 'Nhóm chi: utilities'] });

    await parseText(page, 'trả lương nhân viên 80 triệu chuyển khoản');
    await expectCard(page, { intent: 'Chi phí', confidence: 'High', contains: ['Số tiền: 80,000,000', 'Nhóm chi: labor', 'PTTT: bank'] });

    await parseText(page, 'chi mua nguyên vật liệu 12 củ');
    await expectCard(page, { intent: 'Chi phí', confidence: 'High', contains: ['Số tiền: 12,000,000', 'Nhóm chi: materials'] });

    await parseText(page, 'chi quảng cáo facebook 3 triệu hôm kia');
    await expectCard(page, { intent: 'Chi phí', confidence: 'High', contains: ['Số tiền: 3,000,000', 'Nhóm chi: marketing'], date: relDate(-2) });

    await parseText(page, 'thanh toán tiền xăng xe 500k');
    await expectCard(page, { intent: 'Chi phí', confidence: 'High', contains: ['Số tiền: 500,000', 'Nhóm chi: maintenance'] });

    await parseText(page, 'mua chịu 12 triệu vật tư của nhà cung cấp ABC');
    await expectCard(page, { intent: 'Ghi nhận phải trả (mua chịu)', confidence: 'High', contains: ['Số tiền: 12,000,000', 'Đối tượng: nhà cung cấp ABC'] });
    await applyToForm(page);
    await expect(page.getByTestId('voucher-type-select')).toHaveValue('payable');
    await expect(page.getByTestId('doi-tuong-input')).toHaveValue('nhà cung cấp ABC');

    await parseText(page, 'trả nợ nhà cung cấp ABC 20 triệu');
    await expectCard(page, { intent: 'Trả tiền người bán', confidence: 'High', contains: ['Số tiền: 20,000,000', 'Đối tượng: nhà cung cấp ABC'] });
    await applyToForm(page);
    await expect(page.getByTestId('voucher-type-select')).toHaveValue('payable-payment');

    await parseText(page, 'trả nợ nhà cung cấp Nam 5tr ck');
    await expectCard(page, { intent: 'Trả tiền người bán', confidence: 'High', contains: ['Số tiền: 5,000,000', 'Đối tượng: nhà cung cấp Nam', 'PTTT: bank'] });

    await parseText(page, 'nộp thuế môn bài 2 triệu ngày 30/01');
    await expectCard(page, { intent: 'Chi phí', confidence: 'High', contains: ['Số tiền: 2,000,000'], date: '30/01/2026' });

    await parseText(page, 'tạm ứng lương cho nhân viên Nam 5 triệu tiền mặt');
    await expectCard(page, { intent: 'Chi phí', confidence: 'High', contains: ['Số tiền: 5,000,000', 'Nhóm chi: labor', 'PTTT: cash'] });

    await parseText(page, 'hoàn tiền khách 1 triệu 5');
    await expectCard(page, { intent: 'Chi phí', confidence: 'High', contains: ['Số tiền: 1,500,000'] });
  });

  test('§9.3 Transfer X1-X4 — hướng + phí + apply prefill', async ({ page }) => {
    await loginAndImpersonate(page, PILOT_TENANT);
    await openAiMode(page, '/accounting/transfers');

    await parseText(page, 'hôm nay rút tiền gửi ngân hàng một trăm triệu nhập vào quỹ tiền mặt');
    await expectCard(page, { intent: 'Chuyển tiền: NH → quỹ', confidence: 'High', contains: ['Số tiền: 100,000,000', /→ 111/] });
    await applyToForm(page);
    await expect(page.getByTestId('transfer-amount')).toHaveValue('100000000');
    const toValues = await page.getByTestId('transfer-to').locator('option').evaluateAll(o => o.map(x => (x as HTMLOptionElement).value));
    if (toValues.includes('111')) await expect(page.getByTestId('transfer-to')).toHaveValue('111');

    await parseText(page, 'nộp 50 triệu tiền mặt vào tài khoản ngân hàng');
    await expectCard(page, { intent: 'Chuyển tiền: quỹ → NH', confidence: 'High', contains: ['Số tiền: 50,000,000', 'Chuyển: 111 →'] });

    await parseText(page, 'rút 50 triệu từ ngân hàng về quỹ phí 30 nghìn');
    await expectCard(page, { intent: 'Chuyển tiền: NH → quỹ', confidence: 'High', contains: ['Số tiền: 50,000,000', 'Phí: 30,000', /→ 111/] });

    await parseText(page, 'gửi 20 triệu từ quỹ vào tài khoản ngân hàng hôm qua');
    await expectCard(page, { intent: 'Chuyển tiền: quỹ → NH', confidence: 'High', contains: ['Số tiền: 20,000,000', 'Chuyển: 111 →'], date: relDate(-1) });
  });

  test('§10.1 F1-F2 + F19-F20 — bẫy số tiền (số trần = đồng, lẻ)', async ({ page }) => {
    await loginAndImpersonate(page, PILOT_TENANT);
    await openAiMode(page, '/accounting/revenue');

    await parseText(page, 'thu 500');
    await expectCard(page, { intent: 'Thu doanh thu', confidence: 'High', contains: ['Số tiền: 500'] });

    await parseText(page, 'thu một trăm');
    await expectCard(page, { intent: 'Thu doanh thu', confidence: 'High', contains: ['Số tiền: 100'] });

    await parseText(page, 'thu khoảng 5 triệu');
    await expectCard(page, { intent: 'Thu doanh thu', confidence: 'High', contains: ['Số tiền: 5,000,000'] });

    await parseText(page, 'thu 5 củ lẻ 2');
    await expectCard(page, { intent: 'Thu doanh thu', confidence: 'High', contains: ['Số tiền: 5,000,002'] });
  });

  test('§10.2 F4-F10 — nhiễu loại nghiệp vụ (tháng 10, thiếu khách, thiếu hướng)', async ({ page }) => {
    await loginAndImpersonate(page, PILOT_TENANT);
    await openAiMode(page, '/accounting/expenses');

    // F4: "tháng 10" nhiễu amount
    await parseText(page, 'trả tiền điện tháng 10 là 2tr5');
    await expectCard(page, { intent: 'Chi phí', confidence: 'High', contains: ['Số tiền: 10', 'Nhóm chi: utilities'], warnings: ['kiểm tra lại đơn vị'] });

    // F9: "mua hàng" không đủ
    await parseText(page, 'mua hàng 30 triệu');
    await expectCard(page, { intent: 'Chưa xác định', confidence: 'Low', contains: ['Số tiền: 30,000,000'], missing: ['intent'] });

    // F10: "trả" trần
    await parseText(page, 'công ty ABC trả 5 triệu hợp đồng tháng 10');
    await expectCard(page, { intent: 'Chưa xác định', confidence: 'Low', contains: ['Số tiền: 5,000,000'], missing: ['intent'] });

    // F11: trả nợ không rõ bên → đối tượng cụt + nghiêng người bán
    await parseText(page, 'trả nợ 5 triệu cho anh Hùng');
    await expectCard(page, { intent: 'Trả tiền người bán', confidence: 'High', contains: ['Đối tượng: Hùng'], warnings: ['chưa rõ khách hàng hay nhà cung cấp'] });

    // F24 (parser quirk xác nhận): "thanh toán nợ ... cho X" làm mất đối tượng — còn "trả nợ ... X" thì OK
    await parseText(page, 'thanh toán nợ 5tr ck cho nhà cung cấp Nam');
    await expectCard(page, { intent: 'Trả tiền người bán', confidence: 'Medium', contains: ['Số tiền: 5,000,000', 'PTTT: bank'], missing: ['đối tượng'] });

    await parseText(page, 'trả nợ nhà cung cấp Nam 5tr ck');
    await expectCard(page, { intent: 'Trả tiền người bán', confidence: 'High', contains: ['Số tiền: 5,000,000', 'Đối tượng: nhà cung cấp Nam', 'PTTT: bank'] });
  });

  test('§10.2 F5 — "thu nợ" thiếu khách → route sang Phiếu Chi', async ({ page }) => {
    await loginAndImpersonate(page, PILOT_TENANT);
    await openAiMode(page, '/accounting/revenue');

    await parseText(page, 'thu nợ 5 triệu');
    await expectCard(page, { intent: 'Trả tiền người bán', confidence: 'Medium', contains: ['Số tiền: 5,000,000'], missing: ['đối tượng'], warnings: ['chưa rõ khách hàng hay nhà cung cấp'] });

    await applyToForm(page);
    const link = page.getByTestId('smart-route-link');
    await expect(link).toBeVisible({ timeout: 15000 });
    await expect(link).toHaveAttribute('href', /\/accounting\/expenses\?desc=/);
  });

  test('§10.2 F21 — nhập hàng → route Phiếu Nhập Mua', async ({ page }) => {
    await loginAndImpersonate(page, PILOT_TENANT);
    await openAiMode(page, '/accounting/expenses');

    await parseText(page, 'mua hàng hóa nhập kho 30 triệu');
    await expectCard(page, { intent: 'Nhập mua hàng hóa', confidence: 'High', contains: ['Số tiền: 30,000,000'] });

    await applyToForm(page);
    const link = page.getByTestId('smart-route-link');
    await expect(link).toBeVisible({ timeout: 15000 });
    await expect(link).toHaveAttribute('href', /\/accounting\/purchase-receipts\/new/);
  });

  test('§10.3 F12-F13 + §10.4 F14-F16 — đối tượng/ngày/PTTT', async ({ page }) => {
    await loginAndImpersonate(page, PILOT_TENANT);
    await openAiMode(page, '/accounting/revenue');

    // F12: khách không tên → thiếu đối tượng
    await parseText(page, 'khách trả nợ 5 triệu');
    await expectCard(page, { intent: 'Thu tiền khách trả nợ', confidence: 'Medium', contains: ['Số tiền: 5,000,000'], missing: ['đối tượng'] });

    // F13: đặt cọc bắt nhầm tên
    await parseText(page, 'khách đặt cọc trước 3 triệu');
    await expectCard(page, { intent: 'Khách ứng trước/cọc (trả trước)', confidence: 'High', contains: ['Số tiền: 3,000,000', 'Đối tượng: đặt cọc trước'], warnings: ['trả trước'] });

    // F14: thiếu chữ "ngày"
    await parseText(page, 'thu 1 triệu 25/10');
    await expectCard(page, { intent: 'Thu doanh thu', confidence: 'High', contains: ['Số tiền: 1,000,000'], date: relDate(0) });

    // F16: "bank" tiếng Anh → cash
    await parseText(page, 'thu 5 triệu qua bank');
    await expectCard(page, { intent: 'Thu doanh thu', confidence: 'High', contains: ['Số tiền: 5,000,000', 'PTTT: cash'] });
  });

  test('§10.5 F3/F17/F18 + F23 — thiếu dữ kiện & tên trùng số', async ({ page }) => {
    await loginAndImpersonate(page, PILOT_TENANT);
    await openAiMode(page, '/accounting/revenue');

    // F3: thiếu amount
    await parseText(page, 'khách anh Hùng trả nợ');
    await expectCard(page, { intent: 'Thu tiền khách trả nợ', confidence: 'Low', contains: ['Đối tượng: anh Hùng'], missing: ['amount'] });

    // F17: chỉ tên
    await parseText(page, 'anh Hùng');
    await expectCard(page, { intent: 'Chưa xác định', confidence: 'Low', missing: ['intent', 'amount'] });

    // F18: câu mơ hồ
    await parseText(page, 'hôm qua có khoản thu');
    await expectCard(page, { intent: 'Chưa xác định', confidence: 'Low', missing: ['amount', 'intent'], date: relDate(-1) });

    // F23: "ông Ba" = số 3 → amount sai + tên cắt cụt (trap) — rồi cách nói đúng
    await parseText(page, 'khách ông Ba trả nợ 2tr5');
    await expectCard(page, { intent: 'Thu tiền khách trả nợ', confidence: 'High', contains: ['Số tiền: 3', 'Đối tượng: ông'], warnings: ['kiểm tra lại đơn vị'] });

    await parseText(page, 'thu nợ 2tr5 khách anh Minh trả góp');
    await expectCard(page, { intent: 'Thu tiền khách trả nợ', confidence: 'High', contains: ['Số tiền: 2,500,000', 'Đối tượng: anh Minh'] });
  });

  test('§10.2 F6/F8/F22 — khách trả tiền & chuyển tiền thiếu hướng', async ({ page }) => {
    await loginAndImpersonate(page, PILOT_TENANT);
    await openAiMode(page, '/accounting/transfers');

    // F8: thiếu hướng → chưa xác định
    await parseText(page, 'chuyển tiền 2 triệu');
    await expectCard(page, { intent: 'Chưa xác định', confidence: 'Low', contains: ['Số tiền: 2,000,000'], missing: ['intent'] });

    // F22: nói đủ hướng → đúng TransferIn
    await parseText(page, 'chuyển từ quỹ sang ngân hàng 2 triệu');
    await expectCard(page, { intent: 'Chuyển tiền: quỹ → NH', confidence: 'High', contains: ['Số tiền: 2,000,000', 'Chuyển: 111 →'] });

    // F6: khách trả tiền = trả nợ (không phải doanh thu)
    await page.goto('/accounting/revenue');
    await page.waitForLoadState('networkidle');
    await page.getByTestId('entry-mode-toggle').getByText('Nhập bằng mô tả').click();
    await expect(page.getByTestId('smart-entry-input')).toBeVisible({ timeout: 15000 });
    await parseText(page, 'khách trả tiền 5 triệu');
    await expectCard(page, { intent: 'Thu tiền khách trả nợ', confidence: 'Medium', contains: ['Số tiền: 5,000,000'], missing: ['đối tượng'] });
  });
});