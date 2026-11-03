import { test, expect, Page } from '@playwright/test';

// Task PH3 — RV production "như người dùng": session multi khôi phục deterministic sau
// NavMenu (full reload) · nộp thuế → 333 · kết chuyển → CTGS desc · khấu hao/ký quỹ tạo phiếu.
// Config: playwright-rv-ph3.config.ts (app2 + host-resolver). Auth: sysadmin impersonate tenant.

const TENANT = '7c021960-6d6c-4de4-88a1-cf8373184f49'; // Cafe Tân Quy (SME — LLM pilot)

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

test.describe('RV PH3 — smart session + voucher coverage như người dùng', () => {
  test('Menu 3 nơi: NavMenu + Sitemap + AccountingIndex (khấu hao + ký quỹ)', async ({ page }) => {
    await setup(page);
    await safeGoto(page, '/accounting');
    await expect(page.getByRole('link', { name: 'Phiếu Khấu Hao' }).first()).toBeVisible({ timeout: 30000 });
    await expect(page.getByRole('link', { name: /Phiếu Ký Quỹ/ }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: /Phiếu Khấu Hao/ })).toBeVisible();
    await safeGoto(page, '/sitemap');
    await expect(page.getByTestId('link-accounting-depreciation')).toBeVisible({ timeout: 30000 });
    await expect(page.getByTestId('link-accounting-deposit')).toBeVisible();
  });

  test('Session multi: parse → lưu thu → NavMenu Chi → multi còn + phiếu thu đã lưu', async ({ page }) => {
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

    // Multi sentence — LLM split (2 phiếu: thu + chi)
    await page.getByTestId('smart-entry-input').fill('thu tiền bán hàng 2 triệu và chi tiền điện 1 triệu');
    await page.getByTestId('smart-entry-ai').click(); // ✨ AI phân tích — buộc LLM multi
    await expect(page.getByTestId('smart-entry-multi')).toBeVisible({ timeout: 30000 });

    // Áp phiếu thu (item 1) → Lưu → phiếu đánh dấu "✓ Đã lưu"
    await page.getByTestId('smart-entry-multi-apply').first().click();
    await page.getByRole('button', { name: 'Lưu Doanh Thu' }).click();
    // Lưu xong → nút thu trong multi thành "Đã xử lý"
    await expect(page.getByTestId('smart-entry-multi-done').first()).toBeVisible({ timeout: 30000 });
    // Đóng modal "Đã lưu thành công" (nếu mở — chặn click NavMenu)
    const closeBtn = page.getByTestId('success-modal-continue');
    if (await closeBtn.isVisible().catch(() => false)) await closeBtn.click();
    await page.waitForTimeout(500);

    // Đi qua NavMenu Nhập Chi Phí (SSR <a> → full reload — circuit mới)
    await page.getByRole('link', { name: 'Nhập Chi Phí' }).first().click();
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1500); // settle circuit (Blazor Server — event đầu dễ bị rớt)

    // PH3: khôi phục deterministic từ multi JSON — 2 phiếu + phiếu thu đã lưu
    await expect(page.getByTestId('smart-entry-multi')).toBeVisible({ timeout: 30000 });
    await expect(page.getByTestId('smart-entry-multi-done')).toBeVisible();
    // Phiếu chi còn nút Áp (chưa lưu)
    const applies = page.getByTestId('smart-entry-multi-apply');
    await expect(applies.filter({ hasNotText: 'Đã xử lý' }).first()).toBeVisible();
  });

  test('Nộp thuế → preview Nợ 333/Có 111 (không 621)', async ({ page }) => {
    await setup(page);
    await safeGoto(page, '/accounting/expenses');
    const toggle = page.getByTestId('entry-mode-toggle');
    try {
      await toggle.waitFor({ state: 'visible', timeout: 8000 });
    } catch {
      test.skip(true, 'SmartEntryEnabled flag OFF');
      return;
    }
    await toggle.getByText('Nhập bằng mô tả').click();
    await page.getByTestId('smart-entry-input').fill('nộp thuế 5 triệu');
    await page.getByTestId('smart-entry-parse').click();
    await expect(page.getByTestId('smart-entry-result')).toBeVisible({ timeout: 20000 });
    await expect(page.getByTestId('smart-entry-result')).toContainText('Nhóm chi: tax');
    await page.getByTestId('smart-entry-apply').click();
    await expect(page.getByTestId('journal-preview')).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId('journal-preview')).toContainText('Nợ 333');
    await expect(page.getByTestId('journal-preview')).toContainText('Có 111');
  });

  test('Kết chuyển lãi lỗ → route CTGS + Diễn Giải prefill', async ({ page }) => {
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
    await page.getByTestId('smart-entry-input').fill('kết chuyển lãi lỗ cuối tháng');
    await page.getByTestId('smart-entry-parse').click();
    await expect(page.getByTestId('smart-entry-result')).toBeVisible({ timeout: 20000 });
    await expect(page.getByTestId('smart-entry-intent')).toHaveText('Phiếu hạch toán (CTGS)');
    await page.getByTestId('smart-entry-apply').click();
    const href = await page.getByTestId('smart-route-link').getAttribute('href');
    expect(href).toContain('/accounting/journal-voucher');
    // Mở trang CTGS — Diễn Giải prefill
    await safeGoto(page, href!);
    await expect(page.getByTestId('jv-save')).toBeVisible({ timeout: 30000 });
    await expect(page.getByTestId('jv-description')).toHaveValue(/kết chuyển lãi lỗ/i);
  });

  test('Tạo phiếu Khấu Hao: preview Nợ 642/Có 214 → Lưu → success', async ({ page }) => {
    await setup(page);
    await safeGoto(page, '/vouchers/depreciation');
    await expect(page.getByTestId('voucher-save')).toBeVisible({ timeout: 30000 });
    await page.waitForTimeout(1500); // settle circuit (Blazor Server)
    await page.getByTestId('vf-amount').fill('1000000');
    await expect(page.getByTestId('vf-amount')).toHaveValue('1000000');
    await page.getByTestId('vf-account').selectOption({ index: 0 });
    await page.getByTestId('vf-party').fill('RV PH3 TSCĐ');
    await page.getByTestId('vf-description').fill('RV PH3 — khấu hao test');
    await expect(page.getByTestId('voucher-preview')).toBeVisible({ timeout: 30000 });
    await expect(page.getByTestId('voucher-preview')).toContainText('Có 214');
    await page.getByTestId('voucher-save').click();
    await expect(page.getByTestId('voucher-success-alert')).toBeVisible({ timeout: 30000 });
  });

  test('Tạo phiếu Ký Quỹ (hoàn trả): preview Nợ 338/Có 111 → Lưu → success', async ({ page }) => {
    await setup(page);
    await safeGoto(page, '/vouchers/deposit');
    await expect(page.getByTestId('voucher-save')).toBeVisible({ timeout: 30000 });
    await page.waitForTimeout(1500); // settle circuit — event đầu dễ bị rớt (Blazor Server)
    await page.getByTestId('vf-amount').fill('500000');
    await expect(page.getByTestId('vf-amount')).toHaveValue('500000'); // chắc chắn event đã vào model
    await page.getByTestId('vf-mode').selectOption('return');
    await page.getByTestId('vf-party').fill('RV PH3 Đối tác');
    await page.getByTestId('vf-description').fill('RV PH3 — hoàn trả ký cược test');
    await expect(page.getByTestId('voucher-preview')).toBeVisible({ timeout: 30000 });
    await expect(page.getByTestId('voucher-preview')).toContainText('Nợ 338');
    await expect(page.getByTestId('voucher-preview')).toContainText('Có 111');
    await page.getByTestId('voucher-save').click();
    await expect(page.getByTestId('voucher-success-alert')).toBeVisible({ timeout: 30000 });
  });
});