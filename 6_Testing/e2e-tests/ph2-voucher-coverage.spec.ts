import { test, expect, Page } from '@playwright/test';

// Task PH2 — Entry Form Voucher Coverage & Smart-Apply Fix.
// - Flag-independent: menu 3 nơi (NavMenu/Sitemap/AccountingIndex) + routes loan + HostPage guard.
// - Flag-gated (SmartEntryEnabled): Unknown chặn cứng, Advance alert, loan route link.
// Auth: global storageState (auth/admin.json) applied via playwright.config.ts.

async function hasSmartToggle(page: Page): Promise<boolean> {
  try {
    await page.getByTestId('entry-mode-toggle').waitFor({ state: 'visible', timeout: 8000 });
    return true;
  } catch {
    return false;
  }
}

test.describe('PH2 voucher coverage — menu + routes (flag-independent)', () => {
  test('NavMenu kế toán có link 2 phiếu vay', async ({ page }) => {
    await page.goto('/accounting');
    await page.waitForLoadState('networkidle');
    // NavMenu (ShopErpMenuService) — nhóm Kế Toán
    await expect(page.getByRole('link', { name: 'Phiếu Vay Nhận Tiền' }).first()).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('link', { name: 'Phiếu Trả Nợ Vay' }).first()).toBeVisible();
  });

  test('Sitemap có data-testid link-accounting-loan-*', async ({ page }) => {
    await page.goto('/sitemap');
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('link-accounting-loan-in')).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId('link-accounting-loan-repayment')).toBeVisible();
  });

  test('AccountingIndex quick actions có 2 nút phiếu vay', async ({ page }) => {
    await page.goto('/accounting');
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('button', { name: /Phiếu Vay Nhận Tiền/ })).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('button', { name: /Phiếu Trả Nợ Vay/ })).toBeVisible();
  });

  test('loan-in route render — schema form + Account select', async ({ page }) => {
    await page.goto('/vouchers/loan-in');
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('voucher-save')).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId('vf-amount')).toBeVisible();
    await expect(page.getByTestId('vf-date')).toBeVisible();
    await expect(page.getByTestId('vf-account')).toBeVisible();
    const opts = await page.getByTestId('vf-account').locator('option').evaluateAll(
      o => o.map(x => (x as HTMLOptionElement).value));
    expect(opts.length).toBeGreaterThan(0);
    expect(opts.some(v => v === '311' || v === '341')).toBeTruthy();
  });

  test('loan-repayment route render', async ({ page }) => {
    await page.goto('/vouchers/loan-repayment');
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('voucher-save')).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId('vf-account')).toBeVisible();
  });

  test('HostPage guard — /vouchers/revenue hiển thị link trang host', async ({ page }) => {
    await page.goto('/vouchers/revenue');
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('hosted-route-link')).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId('voucher-save')).toHaveCount(0);
  });

  test('dropdown loại phiếu thu sinh từ registry (4 options, thứ tự cũ)', async ({ page }) => {
    await page.goto('/accounting/revenue');
    await page.waitForLoadState('networkidle');
    const labels = await page.getByTestId('voucher-type-select').locator('option').evaluateAll(
      o => o.map(x => (x as HTMLOptionElement).textContent?.trim()));
    expect(labels).toEqual(['Thu doanh thu', 'Ghi nhận phải thu', 'Thu tiền khách trả nợ', 'Góp vốn chủ sở hữu']);
  });

  test('dropdown loại phiếu chi sinh từ registry (3 options, thứ tự cũ)', async ({ page }) => {
    await page.goto('/accounting/expenses');
    await page.waitForLoadState('networkidle');
    const labels = await page.getByTestId('voucher-type-select').locator('option').evaluateAll(
      o => o.map(x => (x as HTMLOptionElement).textContent?.trim()));
    expect(labels).toEqual(['Chi phí', 'Ghi nhận phải trả', 'Trả tiền người bán']);
  });
});

test.describe('PH2 smart-apply (flag-gated)', () => {
  test('Unknown intent → chặn cứng (smart-apply-error), form không điền', async ({ page }) => {
    await page.goto('/accounting/revenue');
    await page.waitForLoadState('networkidle');
    test.skip(!(await hasSmartToggle(page)), 'SmartEntryEnabled flag OFF for test tenant');
    await page.getByTestId('entry-mode-toggle').getByText('Nhập bằng mô tả').click();
    await page.getByTestId('smart-entry-input').fill('blabla không liên quan gì cả');
    await page.getByTestId('smart-entry-parse').click();
    await expect(page.getByTestId('smart-entry-result')).toBeVisible({ timeout: 15000 });
    await page.getByTestId('smart-entry-apply').click();
    await expect(page.getByTestId('smart-apply-error')).toBeVisible({ timeout: 10000 });
    // Form không bị điền loại phiếu sai
    await expect(page.getByTestId('voucher-type-select')).toHaveValue('revenue');
  });

  test('Advance (khách đặt cọc) → giữ thu nợ + cảnh báo trả trước', async ({ page }) => {
    await page.goto('/accounting/revenue');
    await page.waitForLoadState('networkidle');
    test.skip(!(await hasSmartToggle(page)), 'SmartEntryEnabled flag OFF for test tenant');
    await page.getByTestId('entry-mode-toggle').getByText('Nhập bằng mô tả').click();
    await page.getByTestId('smart-entry-input').fill('đặt cọc của khách anh Nam 3 triệu');
    await page.getByTestId('smart-entry-parse').click();
    await expect(page.getByTestId('smart-entry-result')).toBeVisible({ timeout: 15000 });
    await page.getByTestId('smart-entry-apply').click();
    await expect(page.getByTestId('voucher-type-select')).toHaveValue('receivable-payment');
    await expect(page.getByTestId('advance-warning')).toBeVisible({ timeout: 10000 });
  });

  test('Vay → route link phiếu vay từ trang Doanh Thu', async ({ page }) => {
    await page.goto('/accounting/revenue');
    await page.waitForLoadState('networkidle');
    test.skip(!(await hasSmartToggle(page)), 'SmartEntryEnabled flag OFF for test tenant');
    await page.getByTestId('entry-mode-toggle').getByText('Nhập bằng mô tả').click();
    await page.getByTestId('smart-entry-input').fill('vay ngân hàng 100 triệu nhập quỹ tiền mặt');
    await page.getByTestId('smart-entry-parse').click();
    await expect(page.getByTestId('smart-entry-result')).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId('smart-entry-intent')).toHaveText('Vay nhận tiền');
    await page.getByTestId('smart-entry-apply').click();
    await expect(page.getByTestId('smart-route-link')).toBeVisible({ timeout: 10000 });
    const href = await page.getByTestId('smart-route-link').getAttribute('href');
    expect(href).toContain('/vouchers/loan-in');
  });
});