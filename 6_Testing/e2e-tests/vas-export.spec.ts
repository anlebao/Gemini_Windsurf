import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';

// Bug 2B — VAS Financial Reports Export E2E (Phase 2)
// Verifies that the 4 VAS report pages render export buttons (DOCX + XLSX).
// Does NOT verify actual file download (requires browser download handling) —
// only verifies buttons are present and clickable (Gate 4 UI layout compliance).

const config = loadEnvConfig();

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('Bug 2B — VAS Financial Reports Export Buttons', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto(config.SHOPERP_URL);
    await page.waitForLoadState('networkidle');
  });

  // ── Balance Sheet ─────────────────────────────────────────────────────────

  test('Balance Sheet page has DOCX + XLSX export buttons', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/accounting/balance-sheet`);
    await page.waitForLoadState('networkidle');

    // Page heading
    await expect(page.getByRole('heading', { name: /Bảng Cân Đối Kế Toán/i })).toBeVisible();

    // Export buttons
    await expect(page.getByRole('button', { name: /Xuất DOCX/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Xuất XLSX/i })).toBeVisible();
  });

  // ── Income Statement ──────────────────────────────────────────────────────

  test('Income Statement page has DOCX + XLSX export buttons', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/accounting/income-statement`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: /Báo Cáo Kết Quả Hoạt Động Kinh Doanh/i })).toBeVisible();

    await expect(page.getByRole('button', { name: /Xuất DOCX/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Xuất XLSX/i })).toBeVisible();
  });

  // ── Cash Flow Statement ───────────────────────────────────────────────────

  test('Cash Flow Statement page has DOCX + XLSX export buttons', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/accounting/cash-flow-statement`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: /Báo Cáo Lưu Chuyển Tiền Tệ/i })).toBeVisible();

    await expect(page.getByRole('button', { name: /Xuất DOCX/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Xuất XLSX/i })).toBeVisible();
  });

  // ── Trial Balance ─────────────────────────────────────────────────────────

  test('Trial Balance page has DOCX + XLSX export buttons', async ({ page }) => {
    await page.goto(`${config.SHOPERP_URL}/accounting/trial-balance`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: /Bảng Cân Đối Số Phát Sinh/i })).toBeVisible();

    await expect(page.getByRole('button', { name: /Xuất DOCX/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Xuất XLSX/i })).toBeVisible();
  });
});
