import { test, expect, TestInfo } from '@playwright/test';

// RV W2 (2026-10-17, accounting-legal-compliance-master-plan):
//   P0.2-Đ2 — Sổ Chi Tiết TK (/accounting/sub-ledger — Mẫu S38-DN): filter tháng + TK + đối tượng,
//             cột PS Nợ/PS Có + Dư Nợ/Dư Có, Xuất Excel.
//   P0.4    — Chuẩn dữ liệu HĐĐT NĐ 254/2026 + TT 91/2026 (DTO mapping + guard ngừng HĐĐT — không có UI mới,
//             verify qua L1 markers + compliance test ở build/RV L1-L2).
// Chạy production qua app2.khachvip.online (playwright-rv-w2.config.ts).
// - Project w2-books (Vạn An Trading a5b6c7d8 — DN TT133, JournalEntries TK 111 có data T10/2026):
//   Sub-ledger render với dữ liệu thật + đối tượng dropdown + Excel export.

const runFor = (testInfo: TestInfo, project: string) =>
  test.skip(testInfo.project.name !== project, `chạy ở project ${project} only`);

test.describe('RV W2 — Sổ Chi Tiết TK (P0.2-Đ2, Mẫu S38-DN)', () => {
  test('Sổ Chi Tiết render header + bộ lọc TK/đối tượng + totals', async ({ page }, testInfo) => {
    runFor(testInfo, 'w2-books');

    await page.goto('/accounting/sub-ledger');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1:has-text("Sổ Chi Tiết")')).toBeVisible({ timeout: 30000 });

    // Bộ lọc: Năm/Tháng/TK/Đối tượng + nút Áp dụng/Xuất Excel.
    await expect(page.getByText('Đối tượng:', { exact: false })).toBeVisible({ timeout: 20000 });
    await expect(page.getByRole('button', { name: 'Áp dụng' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Xuất Excel' })).toBeVisible();

    // Totals row hiển thị (TK mặc định đầu tiên của tenant).
    await expect(page.getByText('Dư đầu kỳ:', { exact: false })).toBeVisible({ timeout: 20000 });
    await expect(page.getByText('PS Nợ:', { exact: false })).toBeVisible();
    await expect(page.getByText('PS Có:', { exact: false })).toBeVisible();
    await expect(page.getByText('Dư cuối kỳ:', { exact: false })).toBeVisible();
    // Không có lỗi tải.
    await expect(page.locator('.vanan-alert--error, .alert-danger')).toHaveCount(0);
  });

  test('Sổ Chi Tiết TK 111 hiển thị dữ liệu + cột Dư Nợ/Dư Có', async ({ page }, testInfo) => {
    runFor(testInfo, 'w2-books');

    await page.goto('/accounting/sub-ledger?tk=111');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('text=Sổ chi tiết tài khoản 111').first()).toBeVisible({ timeout: 30000 });
    // Cột đúng mẫu S38-DN: PS Nợ/PS Có + Dư Nợ/Dư Có + Đối tượng + TK ĐƯ.
    for (const col of ['Đối tượng', 'PS Nợ', 'PS Có', 'Dư Nợ', 'Dư Có']) {
      await expect(page.getByText(col, { exact: false }).first()).toBeVisible({ timeout: 15000 });
    }
    await expect(page.locator('.vanan-alert--error, .alert-danger')).toHaveCount(0);
  });

  test('Xuất Excel sổ chi tiết tải file .xlsx (S38-DN)', async ({ page }, testInfo) => {
    runFor(testInfo, 'w2-books');

    await page.goto('/accounting/sub-ledger?tk=111');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('text=Sổ chi tiết tài khoản 111').first()).toBeVisible({ timeout: 30000 });

    const downloadPromise = page.waitForEvent('download', { timeout: 30000 });
    await page.getByRole('button', { name: 'Xuất Excel' }).click();
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toMatch(/^so-chi-tiet-111_\d{6}\.xlsx$/);
  });
});
