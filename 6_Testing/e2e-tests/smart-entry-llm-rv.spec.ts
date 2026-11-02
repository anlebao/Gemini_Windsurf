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
  const card = page.getByTestId('smart-entry-result');
  // Blazor Server đôi khi rớt click khi circuit busy → retry: card phải ẩn (parse reset) rồi hiện lại
  for (let i = 0; i < 3; i++) {
    await page.getByTestId('smart-entry-parse').click();
    try {
      await expect(card).toBeHidden({ timeout: 3000 });
      await expect(card).toBeVisible({ timeout: 30000 });
      return;
    } catch {
      await page.waitForTimeout(500);
    }
  }
  throw new Error('parse click dropped 3×');
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

  test('Không bịa dữ kiện: "hôm qua có khoản thu" → LLM điền intent, KHÔNG tự đặt amount', async ({ page }) => {
    await loginAndImpersonate(page, PILOT_TENANT);
    await openAiMode(page, '/accounting/revenue');

    await parseText(page, 'hôm qua có khoản thu');
    // Intent được điền (Thu doanh thu) + warning AI — đúng contract "không đoán"
    await expectLlmMerged(page, []);
    // amount KHÔNG được bịa (câu không có số tiền) → vẫn "⚠ Thiếu: amount"
    await expect(page.getByTestId('smart-entry-missing')).toContainText('amount');
  });

  test('Không bịa hướng: "chuyển tiền 2 triệu" thiếu hướng → giữ Chưa xác định + warning AI', async ({ page }) => {
    await loginAndImpersonate(page, PILOT_TENANT);
    await openAiMode(page, '/accounting/transfers');

    await parseText(page, 'chuyển tiền 2 triệu');
    // LLM đã được gọi + merge attempt (warning AI) nhưng không bịa hướng 111/112
    await expect(page.getByTestId('smart-entry-result')).toContainText('Đề xuất từ AI');
    await expect(page.getByTestId('smart-entry-intent')).toHaveText('Chưa xác định');
  });

  test('Nút "AI phân tích": force LLM trên câu rule-based High → warning AI', async ({ page }) => {
    await loginAndImpersonate(page, PILOT_TENANT);
    await openAiMode(page, '/accounting/expenses');

    // Phân tích thường: High, không warning AI
    await parseText(page, 'chi tiền thuê mặt bằng 15 triệu hôm qua');
    await expect(page.getByTestId('smart-entry-result')).not.toContainText('Đề xuất từ AI');
    await expect(page.getByTestId('smart-entry-intent')).toHaveText('Chi phí');

    // Bấm "AI phân tích" → buộc gọi LLM → warning AI xuất hiện
    await page.getByTestId('smart-entry-ai').click();
    await expect(page.getByTestId('smart-entry-result')).toBeVisible({ timeout: 30000 });
    await expect(page.getByTestId('smart-entry-result')).toContainText('Đề xuất từ AI');
    await expect(page.getByTestId('smart-entry-intent')).toHaveText('Chi phí');
  });
});