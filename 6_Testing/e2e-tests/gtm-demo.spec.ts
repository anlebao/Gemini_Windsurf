import { test, expect } from '@playwright/test';

/**
 * GTM Drill Machine W2 — Interactive Demo (KhachLink /demo)
 * Task card: docs/AI/tasks/gtm_drill_mvp/task_card_w2_interactive_demo.md (Task 2.3, Gate 4)
 *
 * Target: KhachLink app (configurable via KHACHLINK_URL, defaults to localhost:5002).
 *
 * Verifies:
 * 1. /demo renders setup form (shop name + industry inputs)
 * 2. Entering shop name + industry → storefront mock renders with 3-5 sample products
 * 3. Editing product name + price → render updates
 * 4. Changing theme → visual changes (theme class on wrapper)
 * 5. CTA "Đưa cửa hàng lên TimLaThay" → navigates to /claim?name=... with prefilled name
 *
 * Run (per .devin/rules/playwright.rules.md — only after build passes + implementation complete):
 *   KHACHLINK_URL=http://localhost:5002 npx playwright test gtm-demo
 */

const KHACHLINK_URL = process.env.KHACHLINK_URL || 'http://localhost:5002';
const DEMO_ROUTE = `${KHACHLINK_URL}/demo`;

test.describe('GTM W2 — Interactive Demo', () => {
  test('Demo setup form renders with shop name + industry inputs', async ({ page }) => {
    await page.goto(DEMO_ROUTE, { waitUntil: 'networkidle' });

    // Setup title
    await expect(page.locator('h2:has-text("Dựng cửa hàng demo")')).toBeVisible();

    // Shop name input
    await expect(page.locator('input[placeholder="Quán Cà Phê Nhất Nghệ"]')).toBeVisible();

    // Industry input
    await expect(page.locator('input[placeholder*="cà phê"]')).toBeVisible();
  });

  test('Entering shop name + industry renders storefront mock with sample products', async ({ page }) => {
    await page.goto(DEMO_ROUTE, { waitUntil: 'networkidle' });

    // Fill setup form
    await page.locator('input[placeholder="Quán Cà Phê Nhất Nghệ"]').fill('Quán Test');
    await page.locator('input[placeholder*="cà phê"]').fill('cà phê');

    // Submit form
    await page.locator('button:has-text("Xem trước storefront")').click();

    // Hero section renders with shop name
    await expect(page.locator('h1:has-text("Quán Test")')).toBeVisible({ timeout: 10000 });

    // Products section renders with sample products (cà phê industry → 5 products)
    await expect(page.locator('h2:has-text("Sản phẩm")')).toBeVisible();
    await expect(page.locator('input[placeholder="Tên sản phẩm"]')).toHaveCount(5, { timeout: 10000 });

    // Sample product names from cà phê industry seed
    await expect(page.locator('input[value="Cà phê sữa đá"]')).toBeVisible();
    await expect(page.locator('input[value="Trà sữa trân châu"]')).toBeVisible();
  });

  test('Editing product name + price updates render', async ({ page }) => {
    await page.goto(DEMO_ROUTE, { waitUntil: 'networkidle' });

    // Setup demo
    await page.locator('input[placeholder="Quán Cà Phê Nhất Nghệ"]').fill('Quán Edit Test');
    await page.locator('input[placeholder*="cà phê"]').fill('phở');
    await page.locator('button:has-text("Xem trước storefront")').click();

    // Wait for products to render (phở industry → 4 products)
    await expect(page.locator('input[placeholder="Tên sản phẩm"]')).toHaveCount(4, { timeout: 10000 });

    // Edit first product name
    const firstProductName = page.locator('input[placeholder="Tên sản phẩm"]').first();
    await firstProductName.fill('Phở bò tái đặc biệt');

    // Edit first product price
    const firstProductPrice = page.locator('input[placeholder="Giá (VND)"]').first();
    await firstProductPrice.fill('75000');

    // Verify edits applied
    await expect(firstProductName).toHaveValue('Phở bò tái đặc biệt');
    await expect(firstProductPrice).toHaveValue('75000');
  });

  test('Adding and deleting products updates product count', async ({ page }) => {
    await page.goto(DEMO_ROUTE, { waitUntil: 'networkidle' });

    // Setup demo with generic industry (3 products)
    await page.locator('input[placeholder="Quán Cà Phê Nhất Nghệ"]').fill('Quán Add/Delete');
    await page.locator('input[placeholder*="cà phê"]').fill('ngành không xác định');
    await page.locator('button:has-text("Xem trước storefront")').click();

    // Wait for 3 generic products
    await expect(page.locator('input[placeholder="Tên sản phẩm"]')).toHaveCount(3, { timeout: 10000 });

    // Add a product
    await page.locator('button:has-text("Thêm sản phẩm")').click();
    await expect(page.locator('input[placeholder="Tên sản phẩm"]')).toHaveCount(4);

    // Delete the last product
    await page.locator('button:has-text("")').locator('i.bi-trash').last().click();
    await expect(page.locator('input[placeholder="Tên sản phẩm"]')).toHaveCount(3);
  });

  test('Changing theme updates wrapper class', async ({ page }) => {
    await page.goto(DEMO_ROUTE, { waitUntil: 'networkidle' });

    // Setup demo
    await page.locator('input[placeholder="Quán Cà Phê Nhất Nghệ"]').fill('Quán Theme Test');
    await page.locator('button:has-text("Xem trước storefront")').click();

    // Default theme = classic
    const storePage = page.locator('.store-page');
    await expect(storePage).toHaveClass(/theme-classic/);

    // Change to modern theme
    await page.locator('select').selectOption('1'); // ThemeType.Modern = 1
    await expect(storePage).toHaveClass(/theme-modern/);

    // Change to premium theme
    await page.locator('select').selectOption('4'); // ThemeType.Premium = 4
    await expect(storePage).toHaveClass(/theme-premium/);
  });

  test('CTA navigates to /claim with prefilled shop name', async ({ page }) => {
    await page.goto(DEMO_ROUTE, { waitUntil: 'networkidle' });

    // Setup demo
    const shopName = 'Quán Navigation Test';
    await page.locator('input[placeholder="Quán Cà Phê Nhất Nghệ"]').fill(shopName);
    await page.locator('input[placeholder*="cà phê"]').fill('cà phê');
    await page.locator('button:has-text("Xem trước storefront")').click();

    // Wait for hero to render
    await expect(page.locator('h1:has-text("' + shopName + '")')).toBeVisible({ timeout: 10000 });

    // Click CTA
    await page.locator('button:has-text("Đưa cửa hàng lên TimLaThay")').click();

    // Verify navigation to /claim with name query param
    await page.waitForURL(new RegExp('/claim\\?name=' + encodeURIComponent(shopName).replace(/[.*+?^${}()|[\]\\]/g, '\\$&')), { timeout: 10000 });

    // Verify Register form rendered with prefilled shop name
    await expect(page.locator('h3:has-text("Đăng ký cửa hàng")')).toBeVisible();
    await expect(page.locator('input[placeholder="Quán Cà Phê Nhất Nghệ"]')).toHaveValue(shopName);
  });
});
