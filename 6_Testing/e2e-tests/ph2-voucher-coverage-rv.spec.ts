import { test, expect, Page } from '@playwright/test';

// Task PH2 — RV production "như người dùng": menu 3 nơi → mở phiếu vay → tạo phiếu (JE preview
// Nợ 111/Có 341) → success · Smart Entry vay → route đúng · Unknown chặn · Advance alert.
// Config: playwright-rv-ph2.config.ts (app2 + host-resolver). Auth: sysadmin impersonate tenant.

const TENANT = '7c021960-6d6c-4de4-88a1-cf8373184f49'; // Cafe Tân Quy (SME — dùng chung voucher RV)

async function setup(page: Page) {
  for (let i = 1; i <= 3; i++) {
    try {
      await page.goto('/login', { waitUntil: 'domcontentloaded' });
      break;
    } catch (e) {
      if (i === 3 || !String(e).includes('Download is starting')) throw e;
      await page.waitForTimeout(1500);
    }
  }
  await page.waitForLoadState('domcontentloaded');
  await page.evaluate(async (t) => {
    await fetch('/api/platform/login', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ Username: 'sysadmin@vanan.vn', Password: '2026@vanan' }),
      credentials: 'include',
    });
    await fetch(`/api/admin/impersonate/${t}`, { method: 'POST', credentials: 'include' });
  }, TENANT);
}

async function safeGoto(page: Page, path: string) {
  for (let i = 1; i <= 3; i++) {
    try {
      const r = await page.goto(path, { waitUntil: 'domcontentloaded' });
      await page.waitForLoadState('networkidle');
      return r;
    } catch (e) {
      if (i === 3 || !String(e).includes('Download is starting')) throw e;
      await page.waitForTimeout(1500);
    }
  }
  return null;
}

test.describe('RV PH2 — voucher coverage như người dùng', () => {
  test('Menu 3 nơi: NavMenu + Sitemap + AccountingIndex quick actions', async ({ page }) => {
    await setup(page);
    await safeGoto(page, '/accounting');
    await expect(page.getByRole('link', { name: 'Phiếu Vay Nhận Tiền' }).first()).toBeVisible({ timeout: 30000 });
    await expect(page.getByRole('link', { name: 'Phiếu Trả Nợ Vay' }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: /Phiếu Vay Nhận Tiền/ })).toBeVisible();

    await safeGoto(page, '/sitemap');
    await expect(page.getByTestId('link-accounting-loan-in')).toBeVisible({ timeout: 30000 });
    await expect(page.getByTestId('link-accounting-loan-repayment')).toBeVisible();
  });

  test('Tạo phiếu Vay Nhận Tiền: preview Nợ 111/Có 341 → Lưu → success', async ({ page }) => {
    await setup(page);
    await safeGoto(page, '/vouchers/loan-in');
    await expect(page.getByTestId('voucher-save')).toBeVisible({ timeout: 30000 });

    await page.getByTestId('vf-amount').fill('1000000');
    await page.getByTestId('vf-account').selectOption('341');
    await page.getByTestId('vf-party').fill('RV PH2 Bank');
    await page.getByTestId('vf-description').fill('RV PH2 — vay nhận tiền test');

    // JE preview (strategy — parity 100%)
    await expect(page.getByTestId('voucher-preview')).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId('voucher-preview')).toContainText('Nợ 111');
    await expect(page.getByTestId('voucher-preview')).toContainText('Có 341');

    await page.getByTestId('voucher-save').click();
    await expect(page.getByTestId('voucher-success-alert')).toBeVisible({ timeout: 30000 });
    // In phiếu 01-TT sẵn sàng
    await expect(page.getByRole('button', { name: /In phiếu/ })).toBeVisible();
  });

  test('Phiếu Trả Nợ Vay render + Account select (read-only)', async ({ page }) => {
    await setup(page);
    await safeGoto(page, '/vouchers/loan-repayment');
    await expect(page.getByTestId('voucher-save')).toBeVisible({ timeout: 30000 });
    await expect(page.getByTestId('vf-account')).toBeVisible();
    const opts = await page.getByTestId('vf-account').locator('option').evaluateAll(
      o => o.map(x => (x as HTMLOptionElement).value));
    expect(opts.some(v => v === '311' || v === '341')).toBeTruthy();
  });

  test('HostPage guard: /vouchers/revenue → link trang Nhập Doanh Thu', async ({ page }) => {
    await setup(page);
    await safeGoto(page, '/vouchers/revenue');
    await expect(page.getByTestId('hosted-route-link')).toBeVisible({ timeout: 30000 });
    await expect(page.getByTestId('voucher-save')).toHaveCount(0);
  });

  test('Smart Entry: "vay ngân hàng 100 triệu nhập quỹ" → route phiếu vay', async ({ page }) => {
    await setup(page);
    await safeGoto(page, '/accounting/revenue');
    const toggle = page.getByTestId('entry-mode-toggle');
    try {
      await toggle.waitFor({ state: 'visible', timeout: 8000 });
    } catch {
      test.skip(true, 'SmartEntryEnabled flag OFF');
      return;
    }
    await toggle.getByText('Nhập bằng mô tả').click();
    await page.getByTestId('smart-entry-input').fill('vay ngân hàng 100 triệu nhập quỹ tiền mặt');
    await page.getByTestId('smart-entry-parse').click();
    await expect(page.getByTestId('smart-entry-result')).toBeVisible({ timeout: 20000 });
    await expect(page.getByTestId('smart-entry-intent')).toHaveText('Vay nhận tiền');
    await page.getByTestId('smart-entry-apply').click();
    await expect(page.getByTestId('smart-route-link')).toBeVisible({ timeout: 15000 });
    const href = await page.getByTestId('smart-route-link').getAttribute('href');
    expect(href).toContain('/vouchers/loan-in');
  });

  test('Smart Entry: Unknown → chặn cứng (không điền form)', async ({ page }) => {
    await setup(page);
    await safeGoto(page, '/accounting/revenue');
    const toggle = page.getByTestId('entry-mode-toggle');
    try {
      await toggle.waitFor({ state: 'visible', timeout: 8000 });
    } catch {
      test.skip(true, 'SmartEntryEnabled flag OFF');
      return;
    }
    await toggle.getByText('Nhập bằng mô tả').click();
    await page.getByTestId('smart-entry-input').fill('blabla không liên quan gì cả');
    await page.getByTestId('smart-entry-parse').click();
    await expect(page.getByTestId('smart-entry-result')).toBeVisible({ timeout: 20000 });
    await page.getByTestId('smart-entry-apply').click();
    await expect(page.getByTestId('smart-apply-error')).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId('voucher-type-select')).toHaveValue('revenue');
  });

  test('Smart Entry: khách đặt cọc → thu nợ + cảnh báo trả trước', async ({ page }) => {
    await setup(page);
    await safeGoto(page, '/accounting/revenue');
    const toggle = page.getByTestId('entry-mode-toggle');
    try {
      await toggle.waitFor({ state: 'visible', timeout: 8000 });
    } catch {
      test.skip(true, 'SmartEntryEnabled flag OFF');
      return;
    }
    await toggle.getByText('Nhập bằng mô tả').click();
    await page.getByTestId('smart-entry-input').fill('đặt cọc của khách anh Nam 3 triệu');
    await page.getByTestId('smart-entry-parse').click();
    await expect(page.getByTestId('smart-entry-result')).toBeVisible({ timeout: 20000 });
    await page.getByTestId('smart-entry-apply').click();
    await expect(page.getByTestId('voucher-type-select')).toHaveValue('receivable-payment');
    await expect(page.getByTestId('advance-warning')).toBeVisible({ timeout: 15000 });
  });
});