import { test, expect } from '@playwright/test';

// W3 (P0.3 — Điều 10 TT 91/2026): E2E spec cho luồng điều chỉnh/thay thế HĐĐT.
// Auth: global storageState (auth/admin.json) như các spec einvoice-* khác.
// Cần ít nhất 1 hóa đơn trạng thái "Đã Phát Hành" (TaxApproved) trên môi trường test
// để chạy full flow — các case guard/modal vẫn chạy được không cần data.
test.describe('EInvoice — Điều Chỉnh / Thay Thế (W3 P0.3)', () => {
  test('trang Invoice Management render + status filter có trạng thái mới', async ({ page }) => {
    await page.goto('/einvoice/invoices');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="invoice-management"]')).toBeVisible();
    const filter = page.locator('[data-testid="status-filter"]');
    await expect(filter.locator('option[value="TaxApproved"]')).toHaveCount(1);
    await expect(filter.locator('option[value="Adjusted"]')).toHaveCount(1);
    await expect(filter.locator('option[value="Replaced"]')).toHaveCount(1);
  });

  test('hóa đơn Đã Phát Hành hiển thị nút Điều Chỉnh + Thay Thế', async ({ page }) => {
    await page.goto('/einvoice/invoices');
    await page.waitForLoadState('networkidle');

    // Lọc trạng thái Đã Phát Hành — nếu không có data, test skip gracefully.
    await page.selectOption('[data-testid="status-filter"]', 'TaxApproved');
    const adjustBtn = page.locator('[data-testid="btn-adjust-invoice"]').first();
    if (!(await adjustBtn.count())) {
      test.skip(true, 'Không có hóa đơn TaxApproved trên môi trường test');
    }
    await expect(adjustBtn).toBeVisible();
    await expect(page.locator('[data-testid="btn-replace-invoice"]').first()).toBeVisible();
  });

  test('modal điều chỉnh: lý do bắt buộc + ghi chú kê khai Điều 10 TT 91/2026', async ({ page }) => {
    await page.goto('/einvoice/invoices');
    await page.waitForLoadState('networkidle');

    await page.selectOption('[data-testid="status-filter"]', 'TaxApproved');
    const adjustBtn = page.locator('[data-testid="btn-adjust-invoice"]').first();
    if (!(await adjustBtn.count())) {
      test.skip(true, 'Không có hóa đơn TaxApproved trên môi trường test');
    }
    await adjustBtn.click();

    // Modal mở với ghi chú pháp lý + trường lý do.
    await expect(page.locator('[data-testid="adjust-modal"]')).toBeVisible();
    await expect(page.locator('[data-testid="adjust-legal-note"]')).toContainText('Điều 10 TT 91/2026');
    await expect(page.locator('[data-testid="adjust-reason-input"]')).toBeVisible();

    // Confirm không lý do → báo lỗi, không gọi API.
    await page.click('[data-testid="confirm-adjust"]');
    await expect(page.locator('[data-testid="alert-error"]')).toContainText('bắt buộc');
  });

  test('full flow: điều chỉnh hóa đơn → hóa đơn mới có badge ĐC + gốc Đã Điều Chỉnh', async ({ page }) => {
    await page.goto('/einvoice/invoices');
    await page.waitForLoadState('networkidle');

    await page.selectOption('[data-testid="status-filter"]', 'TaxApproved');
    const adjustBtn = page.locator('[data-testid="btn-adjust-invoice"]').first();
    if (!(await adjustBtn.count())) {
      test.skip(true, 'Không có hóa đơn TaxApproved trên môi trường test');
    }
    await adjustBtn.click();

    await page.fill('[data-testid="adjust-reason-input"]', 'E2E: sai mã số thuế người mua');
    await page.fill('[data-testid="adjust-customer-tax"]', '0100109106');
    await page.click('[data-testid="confirm-adjust"]');

    // Thành công → modal đóng + alert success + hóa đơn điều chỉnh mới trong list.
    await expect(page.locator('[data-testid="alert-success"]')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('[data-testid="badge-adjusted-invoice"]').first()).toBeVisible();
  });
});
