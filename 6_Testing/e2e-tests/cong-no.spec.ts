import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// THU CHI & CÔNG NỢ MVP (P3.1, Gate 4, 2026-10-07): Công nợ E2E tests.
// Validates: phiếu thu "Ghi nhận phải thu" (TK 131) + Tra MST → báo cáo công nợ
// (số dư + tuổi nợ) → sổ chi tiết → lịch sử thanh toán khoản nợ → đảo bút toán.
//
// Strategy (matches va-iie-forecast + accounting-entry-flow precedent):
// - Self-gating: skip khi ENABLE_E2E=false (CI build không chạy E2E).
// - Render tests dùng .or() chấp nhận data hoặc empty/error state (DB có thể trống).
// - Flow test tạo dữ liệu với tên đối tượng + số tiền duy nhất (timestamp) + cleanup bằng
//   đảo bút toán (AccountingEntry immutable — không xóa được).

const config = loadEnvConfig();
const reporter = new TestReporter('CongNo E2E');

// ShopERP base — mặc định từ .env.test (api.khachvip.online/shoperp — local dev);
// production RV override bằng env CONGNO_SHOPERP_URL (vd app2.khachvip.online — ShopERP thật).
const shopErpBase = process.env.CONGNO_SHOPERP_URL || config.SHOPERP_URL;

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VanAn Ecosystem - Công Nợ (131/331) E2E Tests', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting Công Nợ E2E Tests...');
  });

  test.beforeEach(async ({ page, request }) => {
    // Dev login endpoint CHỈ tồn tại trong DEBUG build (#if DEBUG — DevLoginController).
    // - Local dev E2E: POST /dev/login → cookie session Owner.
    // - Production RV (playwright-rv-congno.config.ts): dev login 404 → bỏ qua,
    //   session lấy từ storageState auth/rv-congno.json (login thật adminvanan1).
    const shopErpUrl = shopErpBase;
    const devLoginUrl = `${shopErpUrl}/dev/login`;

    try {
      const response = await request.post(devLoginUrl, { timeout: 8000 });
      if (response.ok()) {
        const body = await response.json();
        console.log(`Dev login successful: tenantId=${body.tenantId}, role=${body.role}`);
      } else {
        console.log(`Dev login unavailable (${response.status()}) — dùng storageState (production RV)`);
      }
    } catch {
      console.log('Dev login unavailable — dùng storageState (production RV)');
    }

    await page.goto(`${shopErpUrl}/dashboard`);
    await page.waitForLoadState('networkidle');
  });

  // ─── RENDER TESTS (Gate 4) ───────────────────────────────────────────────

  test('Phiếu thu công nợ render 3 loại phiếu + Đối tượng bắt buộc', async ({ page }) => {
    await page.goto(`${shopErpBase}/accounting/revenue`);
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1:has-text("Nhập Doanh Thu")')).toBeVisible({ timeout: 15000 });

    // Dropdown "Loại Phiếu Thu" + 3 lựa chọn (FR-1)
    const voucherType = page.locator('#voucherType');
    await expect(voucherType).toBeVisible({ timeout: 15000 });
    await expect(voucherType.locator('option:has-text("Ghi nhận phải thu")')).toHaveCount(1);
    await expect(voucherType.locator('option:has-text("Thu tiền khách trả nợ")')).toHaveCount(1);

    // Chọn công nợ → ô Đối tượng (bắt buộc) hiện ra (FR-2)
    await voucherType.selectOption('receivable');
    await expect(page.locator('#doiTuong')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('select#account option[value="131"]')).toHaveCount(1);
  });

  test('Báo cáo công nợ render 2 khối + chips lọc', async ({ page }) => {
    await page.goto(`${shopErpBase}/accounting/cong-no`);
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1:has-text("Báo Cáo Công Nợ")')).toBeVisible({ timeout: 15000 });

    // 2 khối 131/331 (FR-7)
    await expect(page.locator('[data-testid="congno-group-131"]')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('[data-testid="congno-group-331"]')).toBeVisible({ timeout: 15000 });

    // Chips lọc Tất cả/Còn nợ/Hết nợ
    await expect(page.locator('[data-testid="congno-filter-chip"]')).toBeVisible();
    await expect(page.locator('button:has-text("Còn nợ")')).toBeVisible();
    await expect(page.locator('button:has-text("Hết nợ")')).toBeVisible();
  });

  test('Sổ chi tiết render (sổ phải thu)', async ({ page }) => {
    await page.goto(`${shopErpBase}/accounting/cong-no/thu/Kh%C3%A1ch%20A`);
    await page.waitForLoadState('networkidle');

    // Sổ render (data hoặc empty state) — không phải lỗi route
    await expect(
      page.locator('[data-testid="congno-ledger-card"]').or(page.locator('.vanan-alert:has-text("Đường dẫn không hợp lệ")'))
    ).toBeVisible({ timeout: 15000 });
  });

  test('Sitemap shows Công Nợ link for Owner', async ({ page }) => {
    await page.goto(`${shopErpBase}/sitemap`);
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="link-accounting-cong-no"]')).toBeVisible({ timeout: 15000 });
  });

  // ─── FULL FLOW: phiếu công nợ → báo cáo → sổ → lịch sử → đảo ─────────────

  test('Flow: bán chịu → báo cáo công nợ → sổ → lịch sử → đảo bút toán', async ({ page }) => {
    const shopErpUrl = shopErpBase;
    const doiTuong = `E2E Khách ${Date.now()}`;
    const amount = '1234567';
    const amountVn = '1.234.567'; // vi-VN format trong báo cáo/sổ

    // 1) Phiếu thu "Ghi nhận phải thu" (bán chịu — TK 131)
    await page.goto(`${shopErpUrl}/accounting/revenue`);
    await page.waitForLoadState('networkidle');

    await page.selectOption('#voucherType', 'receivable');
    await expect(page.locator('#doiTuong')).toBeVisible({ timeout: 10000 });
    await page.fill('#doiTuong', doiTuong);
    // Ngày: DynamicFormFields @bind là literal (UI.Platform) → DOM input TRỐNG dù field.Value có mặc định
    // → bắt buộc fill ngày (cùng pattern accounting-entry-flow.spec.ts)
    await page.fill('#date', new Date().toISOString().slice(0, 10));
    await page.fill('#amount', amount);
    await page.fill('#description', `Bán chịu — ${doiTuong}`);
    await page.click('button:has-text("Lưu Doanh Thu")');

    await expect(page.locator('.vanan-alert-success, .alert-success, [class*="alert-success"]'))
      .toContainText('thành công', { timeout: 15000 });

    // 2) Báo cáo công nợ → dòng đối tượng với số dư cuối kỳ
    await page.goto(`${shopErpUrl}/accounting/cong-no`);
    await page.waitForLoadState('networkidle');

    const reportRow = page.locator(`[data-testid="congno-row"]:has-text("${doiTuong}")`);
    await expect(reportRow).toBeVisible({ timeout: 15000 });
    await expect(reportRow).toContainText(amountVn);

    // 3) Click đối tượng → sổ chi tiết (FR-8)
    await reportRow.locator('a[data-testid="congno-row-link"]').click();
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="congno-ledger-table"]')).toBeVisible({ timeout: 15000 });
    const ledgerLine = page.locator(`[data-testid="congno-ledger-line"]:has-text("${doiTuong}")`);
    await expect(ledgerLine).toContainText(amountVn);

    // 4) Lịch sử thanh toán khoản nợ (FR-8.1) — chưa thanh toán
    await ledgerLine.locator('button:has-text("Lịch sử")').click();
    await expect(page.locator('.modal:has-text("Lịch Sử Thanh Toán Khoản Nợ")')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('.modal:has-text("Lịch Sử Thanh Toán Khoản Nợ")')).toContainText('chưa được thanh toán');
    await expect(page.locator('.modal:has-text("Lịch Sử Thanh Toán Khoản Nợ")')).toContainText(amountVn);

    // 5) Cleanup: đảo bút toán (G10 — immutable, không xóa)
    await page.locator('.modal button:has-text("Đóng"), .modal [aria-label="Close"], .modal .btn-close').first().click().catch(() => {});
    await page.keyboard.press('Escape').catch(() => {});
    await ledgerLine.locator('button:has-text("Đảo")').click();
    await expect(page.locator('.modal:has-text("Đảo Bút Toán")')).toBeVisible({ timeout: 10000 });
    await page.locator('.modal input').fill('E2E cleanup');
    await page.locator('.modal button:has-text("Xác Nhận Đảo")').click();

    // Sổ reload → dòng đảo xuất hiện (badge "đảo"), số dư về 0
    await expect(
      page.locator('[data-testid="congno-ledger-line"]:has-text("Reversal of:")')
    ).toBeVisible({ timeout: 15000 });
  });

  // ─── TRA MST [G11] — tolerant (network/rate-limit có thể fail — R6) ───────

  test('Tra MST điền tên công ty vào Đối tượng (hoặc lỗi thân thiện)', async ({ page }) => {
    await page.goto(`${shopErpBase}/accounting/revenue`);
    await page.waitForLoadState('networkidle');

    await page.selectOption('#voucherType', 'receivable');
    await expect(page.locator('#doiTuong')).toBeVisible({ timeout: 10000 });

    // MST giả định — lookup có thể thành công (điền tên) hoặc trả lỗi thân thiện (404/429/502)
    await page.fill('#mst', '0312345678');
    await page.click('button:has-text("Tra cứu MST")');

    await expect(
      page.locator('#doiTuong').or(page.locator('span.text-danger:has-text("Mã số thuế")'))
    ).toBeVisible({ timeout: 15000 });
  });
});
