import { test, expect, Page } from '@playwright/test';

/**
 * TT 71/2024 (HTX) — Phases 4a/5/7 UI E2E (Gate 4).
 *
 * Covers deployed UI surfaces cho tenant HTX (Chế độ kế toán HTX):
 *   - /accounting/revenue  — account select chỉ 511/512/558 (KHÔNG 515/711)
 *   - /accounting/expenses — account select chỉ 642/658 (KHÔNG 621/622/627/641)
 *   - /accounting/trial-balance — auto-map TT 71 (option selected = TT71_2024)
 *   - /accounting/cash-flow-statement — chặn B03 (alert "KHÔNG yêu cầu LCTT")
 *   - /accounting/financial-reports — B 01-HTX/B 02-HTX/B 09-HTX, ẨN B 03-DN
 *
 * SELF-GATING: spec tự skip nếu tenant hiện tại (storageState auth/admin.json)
 * KHÔNG phải HTX — an toàn cho tier-full CI (DN admin fixture) và chạy được
 * ngay khi có session tenant HTX thật.
 * Lưu ý: flow tạo phiếu thu/chi + in 01-TT/02-TT KHÔNG nằm ở đây (tạo entry thật
 * làm bẩn production) — voucher render đã cover bởi bUnit Tt71VoucherTests; flow
 * in đầy đủ xác nhận ở L5 manual (user).
 *
 * Run: npx playwright test e2e-tests/tt71-htx.spec.ts
 */

const SHOPERP = process.env.SHOPERP_URL ?? 'https://app2.khachvip.online';

async function isHtxTenant(page: Page): Promise<boolean> {
    // Load revenue page và kiểm tra option "558 - Thu nhập khác" (chỉ HTX có)
    await page.goto(`${SHOPERP}/accounting/revenue`, { waitUntil: 'domcontentloaded' });
    await page.locator('form').waitFor({ state: 'attached', timeout: 30000 });
    await page.waitForTimeout(3000); // Blazor interactive + tenant type async load
    return (await page.locator('#account option', { hasText: '558 - Thu nhập khác' }).count()) > 0;
}

test.describe('TT 71/2024 (HTX) — UI theo chuẩn tenant', () => {
    test('Phiếu thu: account select chỉ 511/512/558', async ({ page }) => {
        test.skip(!(await isHtxTenant(page)), 'Cần session tenant HTX (storageState) — skip');

        await page.goto(`${SHOPERP}/accounting/revenue`, { waitUntil: 'domcontentloaded' });
        await page.locator('#account').waitFor({ state: 'attached', timeout: 30000 });

        await expect(page.locator('#account option', { hasText: '558 - Thu nhập khác' })).toHaveCount(1);
        await expect(page.locator('#account option', { hasText: '511 - Doanh thu giao dịch bên ngoài' })).toHaveCount(1);
        await expect(page.locator('#account option', { hasText: '512 - Doanh thu giao dịch nội bộ' })).toHaveCount(1);
        await expect(page.locator('#account option[value="515"]')).toHaveCount(0);
        await expect(page.locator('#account option[value="711"]')).toHaveCount(0);
    });

    test('Phiếu chi: account select chỉ 642/658', async ({ page }) => {
        test.skip(!(await isHtxTenant(page)), 'Cần session tenant HTX (storageState) — skip');

        await page.goto(`${SHOPERP}/accounting/expenses`, { waitUntil: 'domcontentloaded' });
        await page.locator('#account').waitFor({ state: 'attached', timeout: 30000 });

        await expect(page.locator('#account option', { hasText: '658 - Chi phí khác' })).toHaveCount(1);
        await expect(page.locator('#account option', { hasText: '642 - Chi phí quản lý kinh doanh' })).toHaveCount(1);
        await expect(page.locator('#account option[value="621"]')).toHaveCount(0);
        await expect(page.locator('#account option[value="622"]')).toHaveCount(0);
        await expect(page.locator('#account option[value="627"]')).toHaveCount(0);
        await expect(page.locator('#account option[value="641"]')).toHaveCount(0);
    });

    test('Trial balance: auto-map TT 71 (option selected)', async ({ page }) => {
        test.skip(!(await isHtxTenant(page)), 'Cần session tenant HTX (storageState) — skip');

        await page.goto(`${SHOPERP}/accounting/trial-balance`, { waitUntil: 'domcontentloaded' });
        await expect(page.locator('select option', { hasText: 'TT 71/2024 (HTX)' })).toHaveCount(1, { timeout: 30000 });
        await expect(page.locator('select option[value="TT71_2024"][selected]')).toHaveCount(1, { timeout: 30000 });
    });

    test('Cash flow: chặn B03 cho HTX (alert thay vì báo cáo)', async ({ page }) => {
        test.skip(!(await isHtxTenant(page)), 'Cần session tenant HTX (storageState) — skip');

        await page.goto(`${SHOPERP}/accounting/cash-flow-statement`, { waitUntil: 'domcontentloaded' });
        await expect(page.locator('text=KHÔNG yêu cầu Báo cáo lưu chuyển tiền tệ')).toBeVisible({ timeout: 30000 });
        await expect(page.locator('text=Báo Cáo Lưu Chuyển Tiền Tệ')).toHaveCount(0);
    });

    test('Financial reports hub: B01/B02/B09-HTX + ẩn B03', async ({ page }) => {
        test.skip(!(await isHtxTenant(page)), 'Cần session tenant HTX (storageState) — skip');

        await page.goto(`${SHOPERP}/accounting/financial-reports`, { waitUntil: 'domcontentloaded' });
        await expect(page.locator('a:has-text("B 01-HTX")')).toBeVisible({ timeout: 30000 });
        await expect(page.locator('a:has-text("B 02-HTX")')).toBeVisible();
        await expect(page.locator('a:has-text("B 09-HTX")')).toBeVisible();
        await expect(page.locator('a:has-text("B 03-DN")')).toHaveCount(0);
    });
});
