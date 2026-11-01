import { test, expect, Page } from '@playwright/test';

// Task PG — Smart Entry AI: 2 mode Manual/AI trên Revenue / Expense / Transfers.
// Flag-gated: SmartEntryEnabled (SystemSetting "ShopFeature:{tenant}:SmartEntryEnabled",
// PG + SQLite mirror — precedent AccountMappingEnabled). Toggle chỉ render khi flag ON
// → flag-dependent tests tự skip nếu flag OFF cho test tenant.
// Auth: global storageState (auth/admin.json) applied via playwright.config.ts.

async function hasSmartToggle(page: Page): Promise<boolean> {
  try {
    await page.getByTestId('entry-mode-toggle').waitFor({ state: 'visible', timeout: 8000 });
    return true;
  } catch {
    return false;
  }
}

async function enableAiMode(page: Page) {
  await page.getByTestId('entry-mode-toggle').getByText('Nhập bằng mô tả').click();
  await expect(page.getByTestId('smart-entry-input')).toBeVisible({ timeout: 10000 });
}

async function parseDescription(page: Page, text: string) {
  await page.getByTestId('smart-entry-input').fill(text);
  await page.getByTestId('smart-entry-parse').click();
  await expect(page.getByTestId('smart-entry-result')).toBeVisible({ timeout: 15000 });
}

test.describe('Smart Entry AI (Task PG)', () => {
  test('transfers page renders manual form — flag-independent', async ({ page }) => {
    await page.goto('/accounting/transfers');
    await page.waitForLoadState('networkidle');

    await expect(page.getByTestId('transfer-from')).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId('transfer-to')).toBeVisible();
    await expect(page.getByTestId('transfer-amount')).toBeVisible();
    await expect(page.getByTestId('transfer-date')).toBeVisible();
    await expect(page.getByTestId('transfer-save')).toBeVisible();
  });

  test('manual submit without accounts shows validation error — no writes', async ({ page }) => {
    await page.goto('/accounting/transfers');
    await page.waitForLoadState('networkidle');

    await page.getByTestId('transfer-amount').fill('100000');
    await page.getByTestId('transfer-save').click();
    await expect(page.getByText('Chọn đủ TK nguồn và TK đích.')).toBeVisible({ timeout: 15000 });
  });

  test('AI mode: transfer intent parses → apply → prefill amount/accounts', async ({ page }) => {
    await page.goto('/accounting/transfers');
    await page.waitForLoadState('networkidle');
    test.skip(!(await hasSmartToggle(page)), 'SmartEntryEnabled flag OFF for test tenant');

    await enableAiMode(page);
    await parseDescription(page, 'Hôm nay rút tiền gửi ngân hàng một trăm triệu nhập vào quỹ tiền mặt');

    // Kết quả parse: intent = chuyển tiền NH → quỹ, amount 100 triệu
    // (format ambient culture — vi-VN "100.000.000", invariant "100,000,000")
    await expect(page.getByTestId('smart-entry-intent')).toHaveText('Chuyển tiền: NH → quỹ');
    await expect(page.getByTestId('smart-entry-result')).toContainText(/100[.,]000[.,]000/);

    await page.getByTestId('smart-entry-apply').click();

    // Amount prefill vào form (input number → raw value)
    await expect(page.getByTestId('transfer-amount')).toHaveValue('100000000');

    // TK đích 111 (quỹ TM) prefill nếu tenant có account 111 trong list
    const toValues = await page.getByTestId('transfer-to').locator('option').evaluateAll(
      opts => opts.map(o => (o as HTMLOptionElement).value));
    if (toValues.includes('111')) {
      await expect(page.getByTestId('transfer-to')).toHaveValue('111');
    }
    // TK nguồn 112x prefill nếu tenant có bank account nào
    const fromValues = await page.getByTestId('transfer-from').locator('option').evaluateAll(
      opts => opts.map(o => (o as HTMLOptionElement).value));
    const bankOpt = fromValues.find(v => v.startsWith('112'));
    if (bankOpt) {
      await expect(page.getByTestId('transfer-from')).toHaveValue(bankOpt);
    }
  });

  test('expense page: transfer intent → route link to /accounting/transfers?desc=', async ({ page }) => {
    await page.goto('/accounting/expenses');
    await page.waitForLoadState('networkidle');
    test.skip(!(await hasSmartToggle(page)), 'SmartEntryEnabled flag OFF for test tenant');

    await enableAiMode(page);
    await parseDescription(page, 'rút tiền gửi ngân hàng 50 triệu nhập vào quỹ tiền mặt');

    await page.getByTestId('smart-entry-apply').click();

    const routeLink = page.getByTestId('smart-route-link');
    await expect(routeLink).toBeVisible({ timeout: 10000 });
    await expect(routeLink).toHaveAttribute('href', /\/accounting\/transfers\?desc=/);

    // Click → mở TransferEntry với deep-link prefill
    await routeLink.click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('transfer-amount')).toHaveValue('50000000', { timeout: 15000 });
  });

  test('deep-link ?desc= parses and prefills on load', async ({ page }) => {
    await page.goto('/accounting/transfers');
    await page.waitForLoadState('networkidle');
    test.skip(!(await hasSmartToggle(page)), 'SmartEntryEnabled flag OFF for test tenant');

    const desc = encodeURIComponent('rút tiền gửi ngân hàng hai trăm triệu nhập vào quỹ tiền mặt');
    await page.goto(`/accounting/transfers?desc=${desc}`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByTestId('transfer-amount')).toHaveValue('200000000', { timeout: 15000 });
  });
});
