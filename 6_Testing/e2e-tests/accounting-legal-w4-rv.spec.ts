import { test, expect, TestInfo } from '@playwright/test';

// RV W4 (2026-10-18, accounting-legal-compliance-master-plan):
//   P1.1 — Audit trail capture IP/UserAgent (cột "IP / UA" + modal chi tiết)
//   P1.2 — Reopen kỳ: modal cảnh báo đỏ + gõ "MỞ KHÓA" + role gate (Owner)
//   P1.3 — Cảnh báo âm quỹ/âm kho: hook Sổ Cái render không lỗi
// Chạy production qua app2.khachvip.online (playwright-rv-w4.config.ts).
// - Project w4-audit: login sysadmin TRONG test (không impersonate) — /admin/audit-trail.
// - Project w4-books: storageState auth/rv-w1-books.json (Owner Vạn An Trading a5b6c7d8).
// LƯU Ý: test reopen CHỈ mở modal + verify gate rồi HUỶ — KHÔNG bấm xác nhận (đảo bút toán production).

const runFor = (testInfo: TestInfo, project: string) =>
  test.skip(testInfo.project.name !== project, `chạy ở project ${project} only`);

test.describe('RV W4 — Audit IP/UserAgent (P1.1)', () => {
  test('Nhật ký audit hiển thị cột IP / UA + modal chi tiết có IP Address', async ({ page, context }, testInfo) => {
    runFor(testInfo, 'w4-audit');

    // Login sysadmin (SystemAdmin policy — không impersonate).
    const login = await context.request.post('/api/platform/login', {
      data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
      headers: { 'Content-Type': 'application/json' },
    });
    expect(login.ok()).toBeTruthy();

    await page.goto('/admin/audit-trail');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1:has-text("Nhật Ký Audit")')).toBeVisible({ timeout: 30000 });
    // W4: cột IP / UA phải tồn tại trong header bảng.
    await expect(page.locator('th:has-text("IP / UA")')).toBeVisible({ timeout: 20000 });
    // Có ít nhất 1 dòng log với cell ip-ua.
    await expect(page.locator('td.ip-ua').first()).toBeVisible();

    // Mở modal chi tiết dòng đầu — render không lỗi.
    await page.getByRole('button', { name: '👁️ Chi tiết' }).first().click();
    await expect(page.locator('text=Chi tiết Audit Log')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('label:has-text("Entity Type:")')).toBeVisible();
    // Không có lỗi render.
    await expect(page.locator('.vanan-alert--error, .alert-danger')).toHaveCount(0);
  });
});

test.describe('RV W4 — Reopen phê duyệt (P1.2) + Ledger (P1.3)', () => {
  test('Period Closing: nếu kỳ đã đóng → nút Mở Lại hiện cho Owner → modal cảnh báo + gate gõ MỞ KHÓA (KHÔNG xác nhận)', async ({ page }, testInfo) => {
    runFor(testInfo, 'w4-books');

    await page.goto('/accounting/period-closing');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1, h2').filter({ hasText: /Đóng Sổ|Kỳ Kế Toán|Period/ }).first()).toBeVisible({ timeout: 30000 });

    const reopenBtn = page.getByRole('button', { name: /Mở Lại Kỳ Này/ });
    if (!(await reopenBtn.isVisible().catch(() => false))) {
      test.info().annotations.push({ type: 'note', description: 'Kỳ hiện tại chưa đóng — chỉ verify trang render' });
      await expect(page.locator('.vanan-alert--error, .alert-danger')).toHaveCount(0);
      return;
    }

    await reopenBtn.click();
    // Modal cảnh báo đỏ phải hiện.
    await expect(page.locator('text=CẢNH BÁO: Mở Lại Kỳ Kế Toán Đã Đóng')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('text=HÀNH ĐỘNG NGUY HIỂM')).toBeVisible();
    await expect(page.locator('input[placeholder="MỞ KHÓA"]')).toBeVisible();

    const confirmBtn = page.getByRole('button', { name: 'Xác Nhận Mở Lại' });
    await expect(confirmBtn).toBeDisabled();

    // Chỉ nhập lý do → vẫn disabled.
    const reason = page.locator('input[placeholder*="lý do mở lại"]');
    await reason.click();
    await reason.pressSequentially('RV W4 kiểm tra gate', { delay: 5 });
    await reason.blur();
    await expect(confirmBtn).toBeDisabled();

    // Gõ đúng MỞ KHÓA → enabled. KHÔNG bấm — huỷ ngay.
    const confirm = page.locator('input[placeholder="MỞ KHÓA"]');
    await confirm.click();
    await confirm.pressSequentially('MỞ KHÓA', { delay: 5 });
    await confirm.blur();
    await expect(confirmBtn).toBeEnabled({ timeout: 15000 });

    await page.getByRole('button', { name: 'Huỷ' }).last().click();
    await expect(page.locator('text=CẢNH BÁO: Mở Lại Kỳ Kế Toán Đã Đóng')).toHaveCount(0, { timeout: 10000 });
  });

  test('Sổ Cái render không lỗi (P1.3 balance alert hook)', async ({ page }, testInfo) => {
    runFor(testInfo, 'w4-books');

    await page.goto('/accounting/ledger');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1:has-text("Sổ Cái")')).toBeVisible({ timeout: 30000 });
    const accountSelect = page.locator('select:has(option[value="111"])');
    await expect(accountSelect).toBeVisible({ timeout: 20000 });
    await accountSelect.selectOption('111');
    await page.getByRole('button', { name: 'Áp dụng' }).click();
    await expect(page.locator('text=Sổ cái tài khoản 111').first()).toBeVisible({ timeout: 20000 });
    await expect(page.locator('.vanan-alert--error, .alert-danger')).toHaveCount(0);
  });
});
