import { test, expect, Page } from '@playwright/test';

// RV Issue #198 (commits 7a13ef99 + cd82dc02):
//   - COA-driven account dropdown (tenant chart thay static list)
//   - Preview định khoản read-only qua account-map
//   - VAT đầu vào (DN/HTX — "trong đó VAT" → entry 133 + manual-vat-in)
//   - "Ghi nhận phải trả" → hint/link Phiếu Nhập Mua
//   - manual-vat-in hiện trên /accounting/mapping-rules
// Tenant DN TT133: a5b6c7d8 (Vạn An Trading) — VAT field phải hiện.
// READ-ONLY spec — mở form, verify render; KHÔNG submit phiếu production.
// Auth qua page.evaluate(fetch) — apiRequestContext không đi qua host-resolver-rules.

const DN_TENANT = 'a5b6c7d8-1234-5678-9abc-def012345678';

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

test.describe('RV #198 — Expense form (DN TT133)', () => {
  test('VAT field + preview + COA options render; payable hint links PurchaseReceipt', async ({ page }) => {
    await loginAndImpersonate(page, DN_TENANT);
    await page.goto('/accounting/expenses');
    await page.waitForLoadState('networkidle');

    // 1) VAT đầu vào hiện với DN (ẩn với HKD/DNSN)
    await expect(page.getByTestId('vat-amount-input')).toBeVisible({ timeout: 30000 });

    // 2) Preview định khoản render
    await expect(page.getByTestId('journal-preview')).toBeVisible({ timeout: 30000 });

    // 3) COA-driven options: có option format "{code} - {name}" từ chart
    //    (<option> không "visible" theo Playwright — chỉ đếm/text, không assert visible).
    const optTexts = await page.locator('select option').allTextContents();
    const coaLike = optTexts.filter(t => /^\d{3,}\s*-\s*\S+/.test(t.trim()));
    expect(coaLike.length, 'expected COA-style options (code - name)').toBeGreaterThanOrEqual(3);

    // 4) Voucher "payable" → hint + link → /accounting/purchase-receipts/new
    await page.getByTestId('voucher-type-select').selectOption('payable');
    await expect(page.getByTestId('link-purchase-receipt')).toBeVisible({ timeout: 15000 });
    const href = await page.getByTestId('link-purchase-receipt').getAttribute('href');
    expect(href).toBe('/accounting/purchase-receipts/new');

    // 5) "Trả tiền người bán" → KHÔNG có VAT field (công nợ không VAT)
    await page.getByTestId('voucher-type-select').selectOption('payable-payment');
    await expect(page.getByTestId('vat-amount-input')).toHaveCount(0);
  });

  test('Preview block notes no-rule/skip on payable voucher', async ({ page }) => {
    await loginAndImpersonate(page, DN_TENANT);
    await page.goto('/accounting/expenses');
    await page.waitForLoadState('networkidle');

    await page.getByTestId('voucher-type-select').selectOption('payable');
    const preview = page.getByTestId('journal-preview');
    await expect(preview).toBeVisible({ timeout: 15000 });
    const text = await preview.textContent();
    expect(text).toMatch(/Bút toán sẽ ghi|bỏ qua|cash-basis|Chọn tài khoản/);
  });
});

test.describe('RV #198 — Revenue form', () => {
  test('Preview định khoản renders on revenue form (DN tenant)', async ({ page }) => {
    await loginAndImpersonate(page, DN_TENANT);
    await page.goto('/accounting/revenue');
    await page.waitForLoadState('networkidle');

    await expect(page.getByTestId('journal-preview')).toBeVisible({ timeout: 30000 });
    await expect(page.getByTestId('vat-amount-input')).toHaveCount(0);
  });
});

test.describe('RV #198 — Mapping rules page', () => {
  test('manual-vat-in + purchase events appear in effective rules (DN tenant)', async ({ page }) => {
    await loginAndImpersonate(page, DN_TENANT);
    await page.goto('/accounting/mapping-rules');
    await page.waitForLoadState('networkidle');

    // Grid render event code trong <strong>; dropdown option là "code — label" → exact chỉ khớp grid.
    await expect(page.getByText('manual-vat-in', { exact: true })).toBeVisible({ timeout: 30000 });
    // S2: purchase/purchase-cash (W6 events) cũng phải liệt kê để tenant override được
    await expect(page.getByText('purchase-cash', { exact: true })).toBeVisible({ timeout: 15000 });
    await expect(page.getByText('payable', { exact: true })).toBeVisible({ timeout: 15000 });
  });

  test('account-map alias route renders same page', async ({ page }) => {
    await loginAndImpersonate(page, DN_TENANT);
    await page.goto('/accounting/account-map');
    await page.waitForLoadState('networkidle');

    await expect(page.getByText('manual-receipt', { exact: true })).toBeVisible({ timeout: 30000 });
  });
});

// Task card 2026-11-01: P1 in phiếu mọi tenant (gỡ gate HTX) + P2 phiếu hạch toán CTGS.
// READ-ONLY — chỉ render + mở modal in từ entry có sẵn; KHÔNG submit phiếu production.
test.describe('RV 2026-11-01 — Phiếu hạch toán CTGS', () => {
  test('/accounting/journal-voucher renders grid + save disabled when unbalanced', async ({ page }) => {
    await loginAndImpersonate(page, DN_TENANT);
    await page.goto('/accounting/journal-voucher');
    await page.waitForLoadState('networkidle');

    await expect(page.getByTestId('jv-lines')).toBeVisible({ timeout: 30000 });
    await expect(page.getByTestId('jv-line')).toHaveCount(2);
    await expect(page.getByTestId('jv-save')).toBeDisabled();
    await expect(page.getByTestId('jv-balance-badge')).toContainText('Lệch');
  });
});

test.describe('RV 2026-11-01 — In phiếu từ lịch sử (non-HTX regression)', () => {
  test('revenue entry detail modal shows In phiếu (01-TT) on DN tenant', async ({ page }) => {
    await loginAndImpersonate(page, DN_TENANT);
    await page.goto('/accounting/history');
    await page.waitForLoadState('networkidle');

    // Mở rộng khoảng ngày (default = tháng hiện tại có thể trống) + lọc Revenue.
    await page.locator('#filter-button').click();
    await page.getByTestId('filter-from-date').fill('2026-01-01');
    await page.locator('.filter-panel select.form-select').selectOption('Revenue');
    await page.getByRole('button', { name: 'Áp Dụng' }).click();
    await page.waitForLoadState('networkidle');

    // Mở detail của entry Revenue đầu tiên → nút In phiếu phải hiện (trước chỉ HTX có).
    const detailButtons = page.getByRole('button', { name: 'Chi Tiết' });
    await expect(detailButtons.first(), 'expected at least one Revenue entry in history').toBeVisible({ timeout: 30000 });
    await detailButtons.first().click();
    await page.getByTestId('print-voucher-btn').click();

    // Modal phiếu thu render mẫu 01-TT.
    await expect(page.getByText('PHIẾU THU', { exact: true })).toBeVisible({ timeout: 15000 });
    await expect(page.getByText('Mẫu số 01 - TT', { exact: true })).toBeVisible();
  });
});
