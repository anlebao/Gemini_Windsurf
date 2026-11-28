import { test, Page } from '@playwright/test';

// PROBE: reproduce thao tác user trên production — CẢ 2 form revenue + expense.
const TENANT = 'a5b6c7d8-1234-5678-9abc-def012345678'; // không bật LLM — flow thuần

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

async function applyFlow(page: Page, path: string, sentence: string, amountExpected: string) {
  await page.goto(path);
  await page.waitForLoadState('networkidle');
  await page.getByTestId('entry-mode-toggle').waitFor({ state: 'visible', timeout: 30000 });
  await page.getByTestId('entry-mode-toggle').getByText('Nhập bằng mô tả').click();
  await page.getByTestId('smart-entry-input').waitFor({ state: 'visible', timeout: 15000 });
  await page.getByTestId('smart-entry-input').fill(sentence);
  await page.getByTestId('smart-entry-parse').click();
  await page.getByTestId('smart-entry-result').waitFor({ state: 'visible', timeout: 15000 });
  await page.getByTestId('smart-entry-apply').click();
  await page.waitForTimeout(600);
  const state = await page.evaluate(() => {
    const g = (id: string) => (document.getElementById(id) as HTMLInputElement | null)?.value ?? 'N';
    return { amount: g('amount'), date: g('date'), description: g('description'), hasFn: typeof (window as any).vananSetElementValue };
  });
  console.log(path, '=>', JSON.stringify(state));
  return state.amount === amountExpected;
}

test('probe apply user-flow both forms', async ({ page }) => {
  await setup(page);
  const rev = await applyFlow(page, '/accounting/revenue', 'thu tiền bán hàng 2 triệu tiền mặt', '2.000.000');
  const exp = await applyFlow(page, '/accounting/expenses', 'chi tiền thuê mặt bằng 15 triệu hôm qua', '15.000.000');
  console.log('REVENUE OK:', rev, '| EXPENSE OK:', exp);
});