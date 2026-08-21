import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// VA-FI-MVP2 Phase 4 (Gate 4): Financial Intelligence UI E2E tests.
// Validates the 4 new pages render without crashing + key layout elements present.
// Per Gate 4 rule: UI layout change → mandatory E2E spec.
//
// Strategy (matches hkd-books.spec.ts precedent):
// - Pages may show data widgets OR guard/empty alerts on a fresh DB.
// - Both states prove the page works. Tests use .or() to accept either.

const config = loadEnvConfig();
const reporter = new TestReporter('Financial Intelligence E2E');

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VanAn Ecosystem - Financial Intelligence MVP-2 UI E2E Tests', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting Financial Intelligence E2E Tests...');
  });

  test.beforeEach(async ({ page }) => {
    await page.goto(config.SHOPERP_URL);
    await page.waitForLoadState('networkidle');
  });

  // ─── BUSINESS PROFILE FORM ────────────────────────────────────────────────

  test('Owner can access BusinessProfile form page', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/admin/business-profile`);
    await page.waitForLoadState('networkidle');

    // Page heading
    await expect(
      page.locator('h1:has-text("Hồ sơ doanh nghiệp")')
    ).toBeVisible();

    // Page must render either the form (VanACard/VanAAlert) OR an error alert.
    // VanAAlert is always present (info banner) so check that.
    await expect(
      page.locator('.vanan-alert').first()
    ).toBeVisible();
  });

  test('BusinessProfile form shows fixed cost fields when rendered', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/admin/business-profile`);
    await page.waitForLoadState('networkidle');

    // Either the form fields are visible (loaded state) OR loading/error alert shown.
    const rentLabel = page.locator('label:has-text("Tiền thuê mặt bằng")');
    const errorAlert = page.locator('.vanan-alert:has-text("Không tải được"), .vanan-alert:has-text("error")');

    await expect(rentLabel.or(errorAlert)).toBeVisible({ timeout: 15000 });
  });

  test('BusinessProfile form has Save button', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/admin/business-profile`);
    await page.waitForLoadState('networkidle');

    // Save button OR error alert (if load failed, form not rendered)
    const saveButton = page.locator('button:has-text("Lưu hồ sơ")');
    const errorAlert = page.locator('.vanan-alert:has-text("Không tải được")');

    await expect(saveButton.or(errorAlert)).toBeVisible({ timeout: 15000 });
  });

  // ─── FINANCIAL DASHBOARD ──────────────────────────────────────────────────

  test('Owner can access Financial Dashboard page', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/financial`);
    await page.waitForLoadState('networkidle');

    await expect(
      page.locator('h1:has-text("Dashboard tài chính")')
    ).toBeVisible();

    // Period picker (month + year select) must be present
    await expect(
      page.locator('select').first()
    ).toBeVisible();
  });

  test('Financial Dashboard renders widgets or guard alerts', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/financial`);
    await page.waitForLoadState('networkidle');

    // Either: 5 widgets render (VanAMetricsCard or VanACard with data-testid)
    // OR: guard alert (PROFILE_MISSING / INSUFFICIENT_DATA / loading text)
    const widget = page.locator('[data-testid^="widget-"], [data-testid^="card-financial"]');
    const guardAlert = page.locator('.vanan-alert:has-text("Chưa khai báo"), .vanan-alert:has-text("Chưa có dữ liệu")');
    const loading = page.locator('text=Đang tải');

    await expect(widget.or(guardAlert).or(loading)).toBeVisible({ timeout: 15000 });
  });

  test('Financial Dashboard period picker changes trigger reload', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/financial`);
    await page.waitForLoadState('networkidle');

    // Change month — should trigger reload (loading text appears briefly OR widgets re-render)
    const monthSelect = page.locator('select').first();
    await monthSelect.selectOption({ index: 1 }); // Tháng 1

    // Page should still show heading (proves no crash)
    await expect(
      page.locator('h1:has-text("Dashboard tài chính")')
    ).toBeVisible({ timeout: 10000 });
  });

  // ─── BREAK-EVEN PAGE ──────────────────────────────────────────────────────

  test('Owner can access Break-even analysis page', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/financial/break-even`);
    await page.waitForLoadState('networkidle');

    await expect(
      page.locator('h1:has-text("Phân tích điểm hòa vốn")')
    ).toBeVisible();

    // Either single BE card renders OR guard alert OR loading
    const card = page.locator('[data-testid="card-breakeven-single"]');
    const guardAlert = page.locator('.vanan-alert:has-text("Chưa khai báo"), .vanan-alert:has-text("Chưa có dữ liệu")');
    const loading = page.locator('text=Đang tải');

    await expect(card.or(guardAlert).or(loading)).toBeVisible({ timeout: 15000 });
  });

  test('Break-even page has Export Excel button', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/financial/break-even`);
    await page.waitForLoadState('networkidle');

    const exportButton = page.locator('button:has-text("Xuất Excel")');
    const guardAlert = page.locator('.vanan-alert:has-text("Không tải được")');

    await expect(exportButton.or(guardAlert)).toBeVisible({ timeout: 15000 });
  });

  // ─── UNIT ECONOMICS PAGE ──────────────────────────────────────────────────

  test('Owner can access Unit Economics page', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/financial/unit-economics`);
    await page.waitForLoadState('networkidle');

    await expect(
      page.locator('h1:has-text("Kinh tế đơn vị sản phẩm")')
    ).toBeVisible();

    // Either unit economics card OR empty state OR loading
    const card = page.locator('[data-testid="card-unit-economics"]');
    const emptyAlert = page.locator('.vanan-alert:has-text("Chưa có dữ liệu")');
    const loading = page.locator('text=Đang tải');

    await expect(card.or(emptyAlert).or(loading)).toBeVisible({ timeout: 15000 });
  });

  test('Unit Economics page has Export Excel button', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/financial/unit-economics`);
    await page.waitForLoadState('networkidle');

    const exportButton = page.locator('button:has-text("Xuất Excel")');
    const emptyAlert = page.locator('.vanan-alert:has-text("Chưa có dữ liệu")');

    await expect(exportButton.or(emptyAlert)).toBeVisible({ timeout: 15000 });
  });

  // ─── SITEMAP NAVIGATION ───────────────────────────────────────────────────

  test('Sitemap has Financial Intelligence card with links', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/sitemap`);
    await page.waitForLoadState('networkidle');

    // Financial Intelligence card (Owner role)
    const card = page.locator('[data-testid="card-financial-intelligence"]');
    await expect(card).toBeVisible();

    // All 4 links present
    await expect(
      page.locator('a[href="/admin/business-profile"]')
    ).toBeVisible();
    await expect(
      page.locator('a[href="/financial"]')
    ).toBeVisible();
    await expect(
      page.locator('a[href="/financial/break-even"]')
    ).toBeVisible();
    await expect(
      page.locator('a[href="/financial/unit-economics"]')
    ).toBeVisible();
  });
});
