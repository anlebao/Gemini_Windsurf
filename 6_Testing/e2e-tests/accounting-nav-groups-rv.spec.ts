import { test, expect, TestInfo } from '@playwright/test';

// RV — Nav menu "Kế Toán" nhóm con (2026-10-17, user directive):
//   Kế Toán → 4 nhóm: Sổ Sách (NKC + Sổ Cái + Sổ Chi Tiết) · Báo Cáo Tài Chính ·
//             Báo Cáo Quản Trị (Công Nợ) · Sổ HKD (chỉ tenant HKD).
// Chạy production qua app2.khachvip.online (playwright-rv-nav.config.ts).
// - Project nav-dn (Vạn An Trading a5b6c7d8 — DN TT133):
//   thấy Sổ Sách + Báo Cáo Quản Trị + Báo Cáo Tài Chính, KHÔNG thấy Sổ HKD.
// LƯU Ý: nhóm chứa URL hiện tại TỰ MỞ — không click toggle (click sẽ đóng lại).

const runFor = (testInfo: TestInfo, project: string) =>
  test.skip(testInfo.project.name !== project, `chạy ở project ${project} only`);

test.describe('RV — Nav Kế Toán nhóm con', () => {
  test('Kế Toán hiển thị nhóm Sổ Sách / Báo Cáo Quản Trị / Báo Cáo Tài Chính (DN)', async ({ page }, testInfo) => {
    runFor(testInfo, 'nav-dn');

    // /accounting nằm trong nhóm "Kế Toán" → nhóm tự mở, không cần click.
    await page.goto('/accounting');
    await page.waitForLoadState('networkidle');

    const nav = page.locator('.vanan-navigation');

    // 3 nhóm con hiển thị cho tenant DN.
    await expect(nav.locator('summary', { hasText: 'Sổ Sách' })).toBeVisible();
    await expect(nav.locator('summary', { hasText: 'Báo Cáo Quản Trị' })).toBeVisible();
    await expect(nav.locator('summary', { hasText: 'Báo Cáo Tài Chính' })).toBeVisible();

    // Tenant DN → KHÔNG có nhóm "Sổ HKD".
    await expect(nav.locator('summary', { hasText: 'Sổ HKD' })).toHaveCount(0);
  });

  test('Nhóm Sổ Sách bung ra 3 sổ + link Sổ Cái điều hướng được', async ({ page }, testInfo) => {
    runFor(testInfo, 'nav-dn');

    await page.goto('/accounting');
    await page.waitForLoadState('networkidle');

    const nav = page.locator('.vanan-navigation');
    // Sub-group "Sổ Sách" đang đóng (URL /accounting không thuộc nhóm) → click mở.
    await nav.locator('summary', { hasText: 'Sổ Sách' }).click();

    // 3 link sổ trong nhóm.
    const soNkc = nav.locator('a[href="/accounting/journal"]');
    const soCai = nav.locator('a[href="/accounting/ledger"]');
    const soChiTiet = nav.locator('a[href="/accounting/sub-ledger"]');
    await expect(soNkc).toBeVisible();
    await expect(soCai).toBeVisible();
    await expect(soChiTiet).toBeVisible();

    // Click Sổ Cái → điều hướng đúng trang.
    await soCai.click();
    await page.waitForLoadState('networkidle');
    await expect(page).toHaveURL(/\/accounting\/ledger/);
    await expect(page.locator('h1', { hasText: 'Sổ Cái' })).toBeVisible({ timeout: 30000 });
  });

  test('Nhóm Báo Cáo Quản Trị chứa Công Nợ + trang công nợ render', async ({ page }, testInfo) => {
    runFor(testInfo, 'nav-dn');

    await page.goto('/accounting');
    await page.waitForLoadState('networkidle');

    const nav = page.locator('.vanan-navigation');
    await nav.locator('summary', { hasText: 'Báo Cáo Quản Trị' }).click();

    const congNo = nav.locator('a[href="/accounting/cong-no"]');
    await expect(congNo).toBeVisible();

    await congNo.click();
    await page.waitForLoadState('networkidle');
    await expect(page).toHaveURL(/\/accounting\/cong-no/);
    await expect(page.locator('h1', { hasText: 'Báo Cáo Công Nợ' })).toBeVisible({ timeout: 30000 });
  });
});
