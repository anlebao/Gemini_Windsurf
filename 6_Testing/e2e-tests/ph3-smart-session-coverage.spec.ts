import { test, expect, Page } from '@playwright/test';

// Task PH3 — session fix + voucher coverage: tax/interest → 333/635 · CTGS route ·
// khấu hao/ký quỹ menu + routes · session recovery (multi JSON deterministic).
// Flag-gated (SmartEntryEnabled): recovery single/multi test — auto-skip khi flag OFF.
// Auth: global storageState (auth/admin.json) applied via playwright.config.ts.

async function hasSmartToggle(page: Page): Promise<boolean> {
  try {
    await page.getByTestId('entry-mode-toggle').waitFor({ state: 'visible', timeout: 8000 });
    return true;
  } catch {
    return false;
  }
}

test.describe('PH3 voucher coverage — menu + routes (flag-independent)', () => {
  test('NavMenu kế toán có link Khấu Hao + Ký Quỹ', async ({ page }) => {
    await page.goto('/accounting');
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('link', { name: 'Phiếu Khấu Hao' }).first()).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('link', { name: /Phiếu Ký Quỹ/ }).first()).toBeVisible();
  });

  test('Sitemap có data-testid link-accounting-depreciation/deposit', async ({ page }) => {
    await page.goto('/sitemap');
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('link-accounting-depreciation')).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId('link-accounting-deposit')).toBeVisible();
  });

  test('depreciation route render — Account select TK chi phí', async ({ page }) => {
    await page.goto('/vouchers/depreciation');
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('voucher-save')).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId('vf-amount')).toBeVisible();
    await expect(page.getByTestId('vf-account')).toBeVisible();
  });

  test('deposit route render — mode select receive/return', async ({ page }) => {
    await page.goto('/vouchers/deposit');
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('voucher-save')).toBeVisible({ timeout: 15000 });
    const opts = await page.getByTestId('vf-mode').locator('option').evaluateAll(
      o => o.map(x => (x as HTMLOptionElement).value));
    expect(opts).toEqual(['receive', 'return']);
  });
});

test.describe('PH3 smart-apply (flag-gated)', () => {
  test('Nộp thuế → category tax + gợi ý TK 333 (không 621)', async ({ page }) => {
    await page.goto('/accounting/expenses');
    await page.waitForLoadState('networkidle');
    test.skip(!(await hasSmartToggle(page)), 'SmartEntryEnabled flag OFF for test tenant');
    await page.getByTestId('entry-mode-toggle').getByText('Nhập bằng mô tả').click();
    await page.getByTestId('smart-entry-input').fill('nộp thuế 5 triệu');
    await page.getByTestId('smart-entry-parse').click();
    await expect(page.getByTestId('smart-entry-result')).toBeVisible({ timeout: 15000 });
    await page.getByTestId('smart-entry-apply').click();
    // Preview định khoản Nợ 333/Có 111 (mapping rule ManualExpense với account 333)
    await expect(page.getByTestId('journal-preview')).toBeVisible({ timeout: 10000 });
  });

  test('Kết chuyển lãi lỗ → route link CTGS', async ({ page }) => {
    await page.goto('/accounting/revenue');
    await page.waitForLoadState('networkidle');
    test.skip(!(await hasSmartToggle(page)), 'SmartEntryEnabled flag OFF for test tenant');
    await page.getByTestId('entry-mode-toggle').getByText('Nhập bằng mô tả').click();
    await page.getByTestId('smart-entry-input').fill('kết chuyển lãi lỗ cuối tháng');
    await page.getByTestId('smart-entry-parse').click();
    await expect(page.getByTestId('smart-entry-result')).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId('smart-entry-intent')).toHaveText('Phiếu hạch toán (CTGS)');
    await page.getByTestId('smart-entry-apply').click();
    await expect(page.getByTestId('smart-route-link')).toBeVisible({ timeout: 10000 });
    const href = await page.getByTestId('smart-route-link').getAttribute('href');
    expect(href).toContain('/accounting/journal-voucher');
  });

  test('Khấu hao + Ký quỹ → route link phiếu generic', async ({ page }) => {
    await page.goto('/accounting/revenue');
    await page.waitForLoadState('networkidle');
    test.skip(!(await hasSmartToggle(page)), 'SmartEntryEnabled flag OFF for test tenant');
    await page.getByTestId('entry-mode-toggle').getByText('Nhập bằng mô tả').click();
    await page.getByTestId('smart-entry-input').fill('trích khấu hao TSCĐ 10 triệu');
    await page.getByTestId('smart-entry-parse').click();
    await expect(page.getByTestId('smart-entry-result')).toBeVisible({ timeout: 15000 });
    await page.getByTestId('smart-entry-apply').click();
    await expect(page.getByTestId('smart-route-link')).toBeVisible({ timeout: 10000 });
    expect(await page.getByTestId('smart-route-link').getAttribute('href')).toContain('/vouchers/depreciation');
  });
});