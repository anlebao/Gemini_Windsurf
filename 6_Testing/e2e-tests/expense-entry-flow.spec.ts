import { test, expect } from '@playwright/test';

test.describe('Expense Entry Flow', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('#username', 'admin@vanan.vn');
    await page.fill('#password', 'VanAn@2026');
    await page.click('button[type="submit"]');
    await page.waitForURL('/');
  });

  test('should create expense entry with vendor info', async ({ page }) => {
    await page.goto('/accounting/expenses');

    // 1. Wait for Blazor Form to mark itself as interactive
    const form = page.locator('form');
    await expect(form).toHaveAttribute('data-blazor-interactive', 'true', { timeout: 10000 });

    await page.fill('#date', '2026-05-20');
    await page.fill('#amount', '500000');
    await page.selectOption('#account', '621');
    await page.fill('#vendor', 'Công ty ABC');
    await page.selectOption('#category', 'materials');
    await page.fill('#description', 'Mua vật liệu sản xuất');
    await page.locator('#description').press('Tab');

    // 2. HARDENING STEP: Ép thêm chốt chặn đảm bảo nút bấm đã đồng bộ trạng thái type thành submit dưới DOM
    const submitBtn = page.getByTestId('expense-submit');
    await expect(submitBtn).toHaveAttribute('type', 'submit');
    await expect(submitBtn).not.toHaveAttribute('disabled');

    // 3. Tiến hành click an toàn
    await submitBtn.click();

    // Use data-testid for success alert
    const successAlert = page.getByTestId('expense-success-alert');
    await expect(successAlert).toBeVisible({ timeout: 15000 });

    await page.goto('/accounting/history');
    // Đảm bảo định dạng số tiền đúng chuẩn vi-VN (dấu chấm phân cách hàng nghìn)
    await expect(page.locator('text=500.000 ₫')).toBeVisible();
    // Assertion cụ thể kiểm tra format theo chuẩn vi-VN
    const amountCell = page.locator('[data-testid="datagrid-cell-amount"]').first();
    await expect(amountCell).toHaveText(/500\.000/);
  });

  test('should require vendor when category is purchase', async ({ page }) => {
    await page.goto('/accounting/expenses');

    // Wait for Blazor Form to mark itself as interactive
    const form = page.locator('form');
    await expect(form).toHaveAttribute('data-blazor-interactive', 'true', { timeout: 10000 });

    await page.fill('#date', '2026-05-20');
    await page.fill('#amount', '500000');
    await page.selectOption('#account', '621');
    await page.fill('#description', 'Test expense');
    await page.locator('#description').press('Tab');
    await page.selectOption('#category', 'materials');

    const submitBtn = page.locator('button[type="submit"]');
    await expect(submitBtn).toBeVisible();
    await expect(submitBtn).toHaveAttribute('type', 'submit');
    await submitBtn.click();

    await expect(page.locator('.vanan-alert__message').filter({ hasText: 'Nhà cung cấp bắt buộc khi loại chi phí là Nguyên vật liệu hoặc Bảo trì & Sửa chữa.' })).toBeVisible({ timeout: 15000 });
  });
});
