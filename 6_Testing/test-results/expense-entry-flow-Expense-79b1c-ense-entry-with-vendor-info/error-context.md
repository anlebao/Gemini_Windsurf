# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: expense-entry-flow.spec.ts >> Expense Entry Flow >> should create expense entry with vendor info
- Location: expense-entry-flow.spec.ts:12:7

# Error details

```
Error: page.goto: Protocol error (Page.navigate): Cannot navigate to invalid URL
Call log:
  - navigating to "/login", waiting until "load"

```

# Test source

```ts
  1  | import { test, expect } from '@playwright/test';
  2  | 
  3  | test.describe('Expense Entry Flow', () => {
  4  |   test.beforeEach(async ({ page }) => {
> 5  |     await page.goto('/login');
     |                ^ Error: page.goto: Protocol error (Page.navigate): Cannot navigate to invalid URL
  6  |     await page.fill('#username', 'admin@vanan.vn');
  7  |     await page.fill('#password', 'VanAn@2026');
  8  |     await page.click('button[type="submit"]');
  9  |     await page.waitForURL('/');
  10 |   });
  11 | 
  12 |   test('should create expense entry with vendor info', async ({ page }) => {
  13 |     await page.goto('/accounting/expenses');
  14 |     await page.fill('#date', '2026-05-20');
  15 |     await page.fill('#amount', '500000');
  16 |     await page.selectOption('#account', '621');
  17 |     await page.fill('#vendor', 'Công ty ABC');
  18 |     await page.selectOption('#category', 'materials');
  19 |     await page.fill('#description', 'Mua vật liệu sản xuất');
  20 |     await page.locator('#description').press('Tab');
  21 | 
  22 |     // Debug: inspect page content
  23 |     const pageContent = await page.content();
  24 |     console.log('Page contains form:', pageContent.includes('<form'));
  25 |     console.log('Page contains EditForm:', pageContent.includes('EditForm'));
  26 | 
  27 |     // Debug: inspect all buttons
  28 |     const allButtons = await page.locator('button').all();
  29 |     console.log(`Found ${allButtons.length} buttons`);
  30 |     for (let i = 0; i < allButtons.length; i++) {
  31 |       const text = await allButtons[i].textContent();
  32 |       const type = await allButtons[i].getAttribute('type');
  33 |       console.log(`Button ${i}: text="${text}", type="${type}"`);
  34 |     }
  35 | 
  36 |     // Try to find button by text
  37 |     const submitBtn = page.locator('button:has-text("Lưu Chi Phí")');
  38 |     await expect(submitBtn).toBeVisible();
  39 |     await submitBtn.click();
  40 | 
  41 |     await expect(page.locator('.vanan-alert__message').filter({ hasText: 'Đã lưu chi phí thành công!' })).toBeVisible({ timeout: 15000 });
  42 | 
  43 |     await page.goto('/accounting/history');
  44 |     await expect(page.locator('text=500.000 ₫')).toBeVisible();
  45 |   });
  46 | 
  47 |   test('should require vendor when category is purchase', async ({ page }) => {
  48 |     await page.goto('/accounting/expenses');
  49 |     await page.fill('#date', '2026-05-20');
  50 |     await page.fill('#amount', '500000');
  51 |     await page.selectOption('#account', '621');
  52 |     await page.fill('#description', 'Test expense');
  53 |     await page.locator('#description').press('Tab');
  54 |     await page.selectOption('#category', 'materials');
  55 | 
  56 |     const submitBtn = page.locator('button[type="submit"]');
  57 |     await expect(submitBtn).toBeVisible();
  58 |     await expect(submitBtn).toHaveAttribute('type', 'submit');
  59 |     await submitBtn.click();
  60 | 
  61 |     await expect(page.locator('.vanan-alert__message').filter({ hasText: 'Nhà cung cấp bắt buộc khi loại chi phí là Nguyên vật liệu hoặc Bảo trì & Sửa chữa.' })).toBeVisible({ timeout: 15000 });
  62 |   });
  63 | });
  64 | 
```