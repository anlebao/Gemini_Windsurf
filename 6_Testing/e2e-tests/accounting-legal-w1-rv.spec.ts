import { test, expect, TestInfo } from '@playwright/test';

// RV W1 (2026-10-16, accounting-legal-compliance-master-plan):
//   P0.2-Đ1 — Sổ Nhật ký chung (/accounting/journal) + Sổ Cái per-TK (/accounting/ledger)
//   P0.1   — Kiểm soát thanh toán tiền mặt ≥ 20 triệu (POS modal cảnh báo + lý do bắt buộc)
//            + Báo cáo rà soát (/accounting/cash-payment-review) hiển thị đơn.
// Chạy production qua app2.khachvip.online (playwright-rv-w1.config.ts).
// - Project w1-books (Vạn An Trading a5b6c7d8 — DN TT133, 55 JournalEntries, TK 111 có data T10/2026):
//   Journal + Ledger render với dữ liệu thật.
// - Project w1-pos (Hoàng Nguyên 8d26fec2 — sản phẩm 70.000.000 đ):
//   POS tạo đơn ≥ 20tr → modal cảnh báo → nhập lý do → xác nhận → báo cáo rà soát hiển thị đơn.

const runFor = (testInfo: TestInfo, project: string) =>
  test.skip(testInfo.project.name !== project, `chạy ở project ${project} only`);

test.describe('RV W1 — Sổ Nhật ký chung + Sổ Cái (P0.2-Đ1)', () => {
  test('Sổ Nhật ký chung render dữ liệu tháng hiện tại + tổng Nợ/Có', async ({ page }, testInfo) => {
    runFor(testInfo, 'w1-books');

    await page.goto('/accounting/journal');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1:has-text("Sổ Nhật Ký Chung")')).toBeVisible({ timeout: 30000 });

    // Tháng hiện tại (10/2026) có 6 bút toán — totals row phải hiện.
    await expect(page.getByText('Tổng Nợ:', { exact: false }).first()).toBeVisible({ timeout: 20000 });
    await expect(page.getByText('Tổng Có:', { exact: false }).first()).toBeVisible();
    // Không có lỗi tải.
    await expect(page.locator('.vanan-alert--error, .alert-danger')).toHaveCount(0);
  });

  test('Sổ Cái TK 111 hiển thị Dư đầu kỳ / PS Nợ / PS Có / Dư cuối kỳ', async ({ page }, testInfo) => {
    runFor(testInfo, 'w1-books');

    await page.goto('/accounting/ledger');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1:has-text("Sổ Cái")')).toBeVisible({ timeout: 30000 });

    // Chọn TK 111 (có phát sinh tháng 10/2026).
    const accountSelect = page.locator('select:has(option[value="111"])');
    await expect(accountSelect).toBeVisible({ timeout: 20000 });
    await accountSelect.selectOption('111');
    await page.getByRole('button', { name: 'Áp dụng' }).click();

    await expect(page.locator('text=Sổ cái tài khoản 111').first()).toBeVisible({ timeout: 20000 });
    await expect(page.getByText('Dư đầu kỳ:', { exact: false })).toBeVisible();
    await expect(page.getByText('PS Nợ:', { exact: false })).toBeVisible();
    await expect(page.getByText('PS Có:', { exact: false })).toBeVisible();
    await expect(page.getByText('Dư cuối kỳ:', { exact: false })).toBeVisible();
    // Không có lỗi tải.
    await expect(page.locator('.vanan-alert--error, .alert-danger')).toHaveCount(0);
  });
});

test.describe('RV W1 — Kiểm soát tiền mặt ≥ 20 triệu (P0.1)', () => {
  test('POS đơn ≥ 20tr chọn tiền mặt → modal cảnh báo → lý do bắt buộc → xác nhận', async ({ page }, testInfo) => {
    runFor(testInfo, 'w1-pos');

    await page.goto('/pos');
    await page.waitForLoadState('networkidle');

    // Sản phẩm 70.000.000 đ — 1 click Thêm là vượt ngưỡng.
    const addBtn = page.locator('[data-testid^="pos-add-"]').first();
    await expect(addBtn).toBeVisible({ timeout: 30000 });
    await addBtn.click();

    // Tổng cộng ≥ 20 triệu (70.000.000 hoặc 77.000.000 nếu VAT 10% — cart dùng "N0" invariant: dấu phẩy).
    await expect(page.locator('.pos-cart-total')).toContainText(/70[.,]000[.,]000|77[.,]000[.,]000/, { timeout: 15000 });

    await page.getByTestId('pos-btn-create').click();
    await page.waitForURL(/\/pos\/payment\//, { timeout: 30000 });
    await page.waitForLoadState('networkidle');

    // Chọn tiền mặt (mặc định) → xác nhận → modal cảnh báo phải hiện.
    await page.getByTestId('pos-btn-cash-confirm').click();
    const warning = page.getByTestId('pos-cash-warning');
    await expect(warning).toBeVisible({ timeout: 15000 });

    // Lý do bắt buộc: nút xác nhận disabled khi trống.
    const confirmBtn = page.getByTestId('pos-btn-cash-warning-confirm');
    await expect(confirmBtn).toBeDisabled();

    // Nhập lý do → enabled → xác nhận → về kitchen.
    // Blazor @bind cần event đủ (input+change+blur) — dùng pressSequentially + blur, không fill().
    const reason = page.getByTestId('pos-cash-warning-reason');
    await reason.click();
    await reason.pressSequentially(`RV W1 tiền mặt ≥20tr ${Date.now()}`, { delay: 5 });
    await reason.blur();
    await expect(confirmBtn).toBeEnabled({ timeout: 15000 });
    await confirmBtn.click();
    await page.waitForURL(/\/kitchen/, { timeout: 30000 });
  });

  test('Báo cáo rà soát hiển thị đơn tiền mặt ≥ 20 triệu đã thanh toán', async ({ page }, testInfo) => {
    runFor(testInfo, 'w1-pos');

    await page.goto('/accounting/cash-payment-review');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1:has-text("Rà Soát Thanh Toán Tiền Mặt")')).toBeVisible({ timeout: 30000 });
    await expect(page.getByText('Đơn hàng ≥ 20 triệu thanh toán tiền mặt')).toBeVisible({ timeout: 20000 });

    // Đơn POS 70tr vừa thanh toán (project w1-pos) phải nằm trong báo cáo.
    await expect(page.locator('.vanan-data-grid, table').getByText(/70\.000\.000|77\.000\.000/).first()).toBeVisible({ timeout: 20000 });
  });
});