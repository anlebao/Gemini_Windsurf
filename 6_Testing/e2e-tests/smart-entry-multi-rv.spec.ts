import { test, expect, Page } from '@playwright/test';

// RV G4 multi-voucher (production, Cafe Tân Quy — LLM pilot): 1 câu → list phiếu.
const TENANT = '7c021960-6d6c-4de4-88a1-cf8373184f49';

async function setup(page: Page) {
  await page.goto('/login');
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

async function openAi(page: Page) {
  await page.goto('/accounting/expenses');
  await page.waitForLoadState('networkidle');
  await page.getByTestId('entry-mode-toggle').waitFor({ state: 'visible', timeout: 30000 });
  await page.getByTestId('entry-mode-toggle').getByText('Nhập bằng mô tả').click();
  await page.getByTestId('smart-entry-input').waitFor({ state: 'visible', timeout: 15000 });
}

async function parse(page: Page, text: string) {
  await page.getByTestId('smart-entry-input').fill(text);
  const card = page.getByTestId('smart-entry-result');
  for (let i = 0; i < 3; i++) {
    await page.getByTestId('smart-entry-parse').click();
    try {
      await expect(page.getByTestId('smart-entry-multi')).toBeVisible({ timeout: 30000 });
      return;
    } catch {
      try { await expect(card).toHaveAttribute('data-desc', text, { timeout: 30000 }); return; }
      catch { await page.waitForTimeout(500); }
    }
  }
  throw new Error('parse failed 3×');
}

test('G4 multi: 2 nghiệp vụ → 2 phiếu', async ({ page }) => {
  await setup(page);
  await openAi(page);
  await parse(page, 'thu 5 triệu và chi 3 triệu tiền điện');
  const items = page.getByTestId('smart-entry-multi-item');
  const count = await items.count();
  console.log('MULTI COUNT:', count);
  expect(count).toBeGreaterThanOrEqual(2);
  const text = (await page.getByTestId('smart-entry-multi').textContent()) ?? '';
  console.log('MULTI TEXT:', text.replace(/\s+/g, ' ').slice(0, 200));
});

test('G4 multi: thanh lý TSCĐ 100tr + gửi NH 50tr', async ({ page }) => {
  await setup(page);
  await openAi(page);
  await parse(page, 'thanh lý tài sản cố định được 100 triệu, mang 50 triệu gửi ngân hàng còn lại nhập quỹ tiền mặt');
  const items = page.getByTestId('smart-entry-multi-item');
  const count = await items.count();
  console.log('TSCD MULTI COUNT:', count);
  expect(count).toBeGreaterThanOrEqual(2);
  const text = (await page.getByTestId('smart-entry-multi').textContent()) ?? '';
  console.log('TSCD TEXT:', text.replace(/\s+/g, ' ').slice(0, 260));
});