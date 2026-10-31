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
  test('manual-vat-in appears in effective rules (DN tenant)', async ({ page }) => {
    await loginAndImpersonate(page, DN_TENANT);
    await page.goto('/accounting/mapping-rules');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('text=manual-vat-in')).toBeVisible({ timeout: 30000 });
  });
});
