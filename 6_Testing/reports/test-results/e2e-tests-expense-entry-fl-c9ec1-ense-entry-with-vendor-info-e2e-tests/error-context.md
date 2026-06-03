# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: e2e-tests\expense-entry-flow.spec.ts >> Expense Entry Flow >> should create expense entry with vendor info
- Location: e2e-tests\expense-entry-flow.spec.ts:12:7

# Error details

```
Error: expect(locator).toBeVisible() failed

Locator: getByTestId('expense-success-alert')
Expected: visible
Timeout: 15000ms
Error: element(s) not found

Call log:
  - Expect "toBeVisible" with timeout 15000ms
  - waiting for getByTestId('expense-success-alert')

```

```yaml
- navigation:
  - list:
    - listitem:
      - link "dashboardDashboard":
        - /url: /accounting
    - listitem:
      - link "plus-circleNhập Doanh Thu":
        - /url: /accounting/revenue
    - listitem:
      - link "minus-circleNhập Chi Phí":
        - /url: /accounting/expenses
    - listitem:
      - link "historyLịch Sử Giao Dịch":
        - /url: /accounting/history
    - listitem:
      - link "account-balanceSố Dư Tài Khoản":
        - /url: /accounting/balance
- main:
  - heading "Nhập Chi Phí" [level=1]
  - button "← Quay Lại" [disabled]
  - button "Dismiss": ×
  - text: Mã tài khoản không hợp lệ. Chi phí phải thuộc nhóm 6xx.
  - heading "Thông Tin Chi Phí" [level=5]
  - text: Ngày
  - textbox "Ngày": 2026-05-20
  - text: Ngày phát sinh chi phí Số Tiền (VNĐ)
  - spinbutton "Số Tiền (VNĐ)": "500000"
  - text: Nhập số tiền chi phí Tài Khoản
  - combobox "Tài Khoản":
    - option "621 - Chi phí nguyên vật liệu" [selected]
    - option "622 - Chi phí nhân công"
    - option "627 - Chi phí bán hàng"
    - option "641 - Chi phí quản lý doanh nghiệp"
    - option "642 - Chi phí tài chính"
  - text: Chọn tài khoản kế toán Nhà Cung Cấp
  - textbox "Nhà Cung Cấp":
    - /placeholder: Tên nhà cung cấp
    - text: Công ty ABC
  - text: Nhà cung cấp hàng hóa/dịch vụ Loại Chi Phí
  - combobox "Loại Chi Phí":
    - option "Nguyên vật liệu" [selected]
    - option "Nhân công"
    - option "Điện nước"
    - option "Tiền thuê mặt bằng"
    - option "Marketing & Quảng cáo"
    - option "Bảo trì & Sửa chữa"
    - option "Khác"
  - text: Phân loại chi phí Diễn Giải
  - textbox "Diễn Giải":
    - /placeholder: Nhập diễn giải giao dịch
    - text: Mua vật liệu sản xuất
  - text: Mô tả chi tiết về chi phí Số Chứng Từ
  - textbox "Số Chứng Từ":
    - /placeholder: "VD: HĐ-001"
  - text: Số hóa đơn hoặc chứng từ liên quan
  - button "Lưu Chi Phí"
```

# Test source

```ts
  1  | import { test, expect } from '@playwright/test';
  2  | 
  3  | test.describe('Expense Entry Flow', () => {
  4  |   test.beforeEach(async ({ page }) => {
  5  |     await page.goto('/login');
  6  |     await page.fill('#username', 'admin@vanan.vn');
  7  |     await page.fill('#password', 'VanAn@2026');
  8  |     await page.click('button[type="submit"]');
  9  |     await page.waitForURL('/');
  10 |   });
  11 | 
  12 |   test('should create expense entry with vendor info', async ({ page }) => {
  13 |     await page.goto('/accounting/expenses');
  14 | 
  15 |     // 1. Wait for Blazor Form to mark itself as interactive
  16 |     const form = page.locator('form');
  17 |     await expect(form).toHaveAttribute('data-blazor-interactive', 'true', { timeout: 10000 });
  18 | 
  19 |     await page.fill('#date', '2026-05-20');
  20 |     await page.fill('#amount', '500000');
  21 |     await page.selectOption('#account', '621');
  22 |     await page.fill('#vendor', 'Công ty ABC');
  23 |     await page.selectOption('#category', 'materials');
  24 |     await page.fill('#description', 'Mua vật liệu sản xuất');
  25 |     await page.locator('#description').press('Tab');
  26 | 
  27 |     // 2. HARDENING STEP: Ép thêm chốt chặn đảm bảo nút bấm đã đồng bộ trạng thái type thành submit dưới DOM
  28 |     const submitBtn = page.getByTestId('expense-submit');
  29 |     await expect(submitBtn).toHaveAttribute('type', 'submit');
  30 |     await expect(submitBtn).not.toHaveAttribute('disabled');
  31 | 
  32 |     // 3. Tiến hành click an toàn
  33 |     await submitBtn.click();
  34 | 
  35 |     // Use data-testid for success alert
  36 |     const successAlert = page.getByTestId('expense-success-alert');
> 37 |     await expect(successAlert).toBeVisible({ timeout: 15000 });
     |                                ^ Error: expect(locator).toBeVisible() failed
  38 | 
  39 |     await page.goto('/accounting/history');
  40 |     // Đảm bảo định dạng số tiền đúng chuẩn vi-VN (dấu chấm phân cách hàng nghìn)
  41 |     await expect(page.locator('text=500.000 ₫')).toBeVisible();
  42 |     // Assertion cụ thể kiểm tra format theo chuẩn vi-VN
  43 |     const amountCell = page.locator('[data-testid="datagrid-cell-amount"]').first();
  44 |     await expect(amountCell).toHaveText(/500\.000/);
  45 |   });
  46 | 
  47 |   test('should require vendor when category is purchase', async ({ page }) => {
  48 |     await page.goto('/accounting/expenses');
  49 | 
  50 |     // Wait for Blazor Form to mark itself as interactive
  51 |     const form = page.locator('form');
  52 |     await expect(form).toHaveAttribute('data-blazor-interactive', 'true', { timeout: 10000 });
  53 | 
  54 |     await page.fill('#date', '2026-05-20');
  55 |     await page.fill('#amount', '500000');
  56 |     await page.selectOption('#account', '621');
  57 |     await page.fill('#description', 'Test expense');
  58 |     await page.locator('#description').press('Tab');
  59 |     await page.selectOption('#category', 'materials');
  60 | 
  61 |     const submitBtn = page.locator('button[type="submit"]');
  62 |     await expect(submitBtn).toBeVisible();
  63 |     await expect(submitBtn).toHaveAttribute('type', 'submit');
  64 |     await submitBtn.click();
  65 | 
  66 |     await expect(page.locator('.vanan-alert__message').filter({ hasText: 'Nhà cung cấp bắt buộc khi loại chi phí là Nguyên vật liệu hoặc Bảo trì & Sửa chữa.' })).toBeVisible({ timeout: 15000 });
  67 |   });
  68 | });
  69 | 
```