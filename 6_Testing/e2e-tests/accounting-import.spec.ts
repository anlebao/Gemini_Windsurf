import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// NHẬP LIỆU & SỔ SÁCH P5 (P4 #1 Import Excel, Gate 4, 2026-10-08): Import Excel E2E tests.
// Validates: /accounting/import render + tải mẫu · upload CSV → dry-run bảng lỗi theo dòng (Q4) →
// lưu khi 0 lỗi → báo cáo công nợ thấy số liệu → cleanup bằng đảo bút toán (G10).
//
// Strategy (matches cong-no + va-iie-forecast precedent):
// - Self-gating: skip khi ENABLE_E2E=false (CI build không chạy E2E).
// - Flow dùng CHỈ phiếu công nợ 131 (ghi nhận — Tang>0) → có nút Đảo trên sổ chi tiết
//   (CongNoLedger) → cleanup được (revenue/expense KHÔNG có UI reversal — tránh data dư).
// - JE thu/chi + [G6] công nợ không JE: đã verify trong Core.Tests (ImportServiceTests).

const config = loadEnvConfig();
const reporter = new TestReporter('ImportExcel E2E');

const shopErpBase = process.env.IMPORT_SHOPERP_URL || config.SHOPERP_URL;

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VanAn Ecosystem - Import Excel (P4 #1) E2E Tests', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting Import Excel E2E Tests...');
  });

  test.beforeEach(async ({ page, request }) => {
    // Dev login endpoint CHỈ tồn tại trong DEBUG build (#if DEBUG — DevLoginController).
    // - Local dev E2E: POST /dev/login → cookie session Owner.
    // - Production RV: dev login 404 → bỏ qua, session từ storageState (login thật).
    const devLoginUrl = `${shopErpBase}/dev/login`;
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

    await page.goto(`${shopErpBase}/dashboard`);
    await page.waitForLoadState('networkidle');
  });

  function today(): string {
    const d = new Date();
    const dd = String(d.getDate()).padStart(2, '0');
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    return `${dd}/${mm}/${d.getFullYear()}`;
  }

  function csvBuffer(rows: string[]): Buffer {
    const header = 'Ngày,Loại phiếu,Tài khoản,Số tiền,Đối tượng,MST,Diễn giải,Số chứng từ';
    return Buffer.from('\uFEFF' + [header, ...rows].join('\n'), 'utf8');
  }

  // ─── RENDER (Gate 4) ─────────────────────────────────────────────────────

  test('Import Excel page renders: tải mẫu + upload + 8 cột', async ({ page }) => {
    await page.goto(`${shopErpBase}/accounting/import`);
    await page.waitForLoadState('networkidle');

    await expect(page.locator('.import-entries-page h1')).toHaveText('Import Dữ Liệu Từ Excel', { timeout: 15000 });
    await expect(page.getByRole('button', { name: /Tải mẫu Excel/ })).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('button', { name: /Tải mẫu CSV/ })).toBeVisible({ timeout: 15000 });
    await expect(page.locator('input[data-testid="import-file-input"]')).toBeVisible({ timeout: 15000 });
    // 8 cột mô tả
    await expect(page.getByText('Số chứng từ', { exact: false }).first()).toBeVisible({ timeout: 15000 });
  });

  test('Sitemap shows Import Excel link for Owner', async ({ page }) => {
    await page.goto(`${shopErpBase}/sitemap`);
    await page.waitForLoadState('networkidle');

    await expect(page.locator('[data-testid="link-accounting-import"]')).toBeVisible({ timeout: 15000 });
  });

  // ─── LỖI DÒNG (Q4 — dry-run, 0 lỗi mới lưu) ─────────────────────────────

  test('Upload CSV lỗi: bảng lỗi theo dòng + KHÔNG có nút Lưu', async ({ page }) => {
    await page.goto(`${shopErpBase}/accounting/import`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000); // chờ Blazor circuit connect (SSR → interactive)

    const ts = Date.now();
    await page.locator('input[data-testid="import-file-input"]').setInputFiles({
      name: 'import-loi.csv',
      mimeType: 'text/csv',
      buffer: csvBuffer([
        `${today()},ghi-nhan-phai-thu,131,2000000,,,Thiếu đối tượng,HD-x-${ts}`,       // lỗi: thiếu đối tượng
        `${today()},thu-doanh-thu,642,1000000,,,TK sai cho thu,PT-x-${ts}`,             // lỗi: TK 642 không hợp lệ cho thu
        `${today()},thu-doanh-thu,511,1000000,,,Hợp lệ,PT-ok-${ts}`,                    // hợp lệ
      ]),
    });

    await expect(page.locator('[data-testid="import-error-row"]').first()).toBeVisible({ timeout: 20000 });
    await expect(page.locator('[data-testid="import-error-row"]')).toHaveCount(2);
    await expect(page.getByText(/2 dòng lỗi/).first()).toBeVisible({ timeout: 15000 });
    // Q4: có lỗi → KHÔNG có nút Lưu
    await expect(page.getByRole('button', { name: /Lưu \d+ Dòng/ })).toHaveCount(0);
  });

  // ─── FULL FLOW: upload → preview 0 lỗi → lưu → báo cáo → đảo (cleanup) ────

  test('Flow: import 2 phiếu công nợ → lưu → báo cáo công nợ → đảo bút toán', async ({ page }) => {
    const ts = Date.now();
    const khachA = `E2E Import Khách A ${ts}`;
    const khachB = `E2E Import Khách B ${ts}`;
    const amountA = '2000000';
    const amountAVn = '2.000.000';
    const amountBVn = '3.000.000';

    // 1) Upload CSV (2 phiếu ghi nhận phải thu — bán chịu)
    await page.goto(`${shopErpBase}/accounting/import`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000); // chờ circuit

    await page.locator('input[data-testid="import-file-input"]').setInputFiles({
      name: 'import-ok.csv',
      mimeType: 'text/csv',
      buffer: csvBuffer([
        `${today()},ghi-nhan-phai-thu,131,${amountA},${khachA},0312345678,Bán chịu import,HD-IMP-${ts}`,
        `${today()},ghi-nhan-phai-thu,131,3000000,${khachB},0312345678,Bán chịu import,HD-IMP2-${ts}`,
      ]),
    });

    // 2) Preview 0 lỗi → nút Lưu 2 Dòng
    await expect(page.getByText(/2 dòng hợp lệ/).first()).toBeVisible({ timeout: 20000 });
    await expect(page.getByRole('button', { name: /Lưu 2 Dòng/ })).toBeVisible({ timeout: 15000 });

    // 3) Lưu → success
    await page.getByRole('button', { name: /Lưu 2 Dòng/ }).click();
    await expect(page.getByText(/Đã lưu 2\/2/).first()).toBeVisible({ timeout: 20000 });

    // 4) Báo cáo công nợ → cả 2 đối tượng với số dư
    await page.goto(`${shopErpBase}/accounting/cong-no`);
    await page.waitForLoadState('networkidle');

    const rowA = page.locator(`[data-testid="congno-row"]:has-text("${khachA}")`);
    await expect(rowA).toBeVisible({ timeout: 15000 });
    await expect(rowA).toContainText(amountAVn);

    const rowB = page.locator(`[data-testid="congno-row"]:has-text("${khachB}")`);
    await expect(rowB).toBeVisible({ timeout: 15000 });
    await expect(rowB).toContainText(amountBVn);

    // 5) Cleanup: đảo bút toán cả 2 (G10 — immutable)
    for (const khach of [khachA, khachB]) {
      await page.goto(`${shopErpBase}/accounting/cong-no`);
      await page.waitForLoadState('networkidle');
      const row = page.locator(`[data-testid="congno-row"]:has-text("${khach}")`);
      await expect(row).toBeVisible({ timeout: 15000 });
      await row.locator('a[data-testid="congno-row-link"]').click();
      await page.waitForLoadState('networkidle');

      const ledgerLine = page.locator(`[data-testid="congno-ledger-line"]:has-text("${khach}")`);
      await expect(ledgerLine).toBeVisible({ timeout: 15000 });
      await ledgerLine.locator('button:has-text("Đảo")').click();
      await expect(page.locator('.vanan-modal:has-text("Đảo Bút Toán")')).toBeVisible({ timeout: 10000 });
      await page.locator('.vanan-modal input').fill('E2E import cleanup');
      await page.locator('.vanan-modal button:has-text("Xác Nhận Đảo")').click();

      await expect(
        page.locator('[data-testid="congno-ledger-line"]:has-text("Reversal of:")')
      ).toBeVisible({ timeout: 15000 });
    }
  });
});
