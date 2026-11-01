import { test, expect, Page } from '@playwright/test';

// Task G2 — RV LLM THẬT trên production (key Google AI Studio đã cấu hình shop-a,
// flag SmartEntryLlmEnabled ON pilot Cafe Tân Quy). READ-ONLY: parse + apply, không submit.
// Chứng minh: (1) cuộc gọi Gemini thật xảy ra + merge thành công → warning "Đề xuất từ AI";
// (2) câu parser bỏ sót (Unknown/Low) được LLM điền intent/amount.
// Assertions dung thứ (LLM không deterministic): warning AI + intent != "Chưa xác định".

const PILOT_TENANT = '7c021960-6d6c-4de4-88a1-cf8373184f49';

async function loginAndImpersonate(page: Page, tenantId: string) {
  for (let attempt = 1; attempt <= 2; attempt++) {
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
    const impOk = await page.evaluate(async (t) => {
      const r = await fetch(`/api/admin/impersonate/${t}`, { method: 'POST', credentials: 'include' });
      return r.ok;
    }, tenantId);
    if (loginOk && impOk) return;
    await page.waitForTimeout(1500);
  }
  throw new Error(`login/impersonate ${tenantId} failed after retries`);
}

async function openAiMode(page: Page, path: string) {
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
  await toggle.waitFor({ state: 'visible', timeout: 30000 });
  await toggle.getByText('Nhập bằng mô tả').click();
  try {
    await expect(page.getByTestId('smart-entry-input')).toBeVisible({ timeout: 15000 });
  } catch {
    await toggle.getByText('Nhập bằng mô tả').click();
    await expect(page.getByTestId('smart-entry-input')).toBeVisible({ timeout: 15000 });
  }
}

async function parseText(page: Page, text: string) {
  await page.getByTestId('smart-entry-input').fill(text);
  await page.getByTestId('smart-entry-parse').click();
  // LLM call ngoài → chờ lâu hơn (30s)
  await expect(page.getByTestId('smart-entry-result')).toBeVisible({ timeout: 30000 });
}

async function expectLlmMerged(page: Page, contains: string[]) {
  const card = page.getByTestId('smart-entry-result');
  // Bằng chứng LLM thật đã chạy + merge: warning "Đề xuất từ AI" (khác "LLM tạm lỗi")
  await expect(card).toContainText('Đề xuất từ AI');
  // Intent đã được LLM điền — không còn "Chưa xác định"
  await expect(page.getByTestId('smart-entry-intent')).not.toHaveText('Chưa xác định');
  for (const s of contains) await expect(card).toContainText(s);
}

test.describe('RV Task G2 — Smart Entry LLM thật (production, read-only)', () => {
  test('Unknown intent "mua hàng 30 triệu" → LLM điền intent + giữ amount', async ({ page }) => {
    await loginAndImpersonate(page, PILOT_TENANT);
    await openAiMode(page, '/accounting/expenses');

    await parseText(page, 'mua hàng 30 triệu');
    await expectLlmMerged(page, ['Số tiền: 30,000,000']);
  });

  test('Missing amount+intent "hôm qua có khoản thu" → LLM điền cả hai', async ({ page }) => {
    await loginAndImpersonate(page, PILOT_TENANT);
    await openAiMode(page, '/accounting/revenue');

    await parseText(page, 'hôm qua có khoản thu');
    await expectLlmMerged(page, []);
    // amount phải được điền bởi LLM (card hiện "Số tiền: ...")
    await expect(page.getByTestId('smart-entry-result')).toContainText('Số tiền:');
  });

  test('Unknown intent "chuyển tiền 2 triệu" → LLM xác định hướng', async ({ page }) => {
    await loginAndImpersonate(page, PILOT_TENANT);
    await openAiMode(page, '/accounting/transfers');

    await parseText(page, 'chuyển tiền 2 triệu');
    await expectLlmMerged(page, ['Số tiền: 2,000,000']);
  });
});