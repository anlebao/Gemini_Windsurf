import { test, expect } from '@playwright/test';

/**
 * Loyalty Points Integrity — Batch 3 RV (2026-09-21, after CD Multi-VPS deploy of 2c12852b).
 *
 * Verifies the deployed ShopERP LoyaltyConfigAdmin page renders the NEW budget UI:
 *   - Budget form: Ngân sách tháng / ngày / giới hạn khách-ngày / trần mỗi đơn (%)
 *   - Runtime counters "Đã dùng tháng này / hôm nay"
 *   - Reset buttons (hôm nay / tháng)
 *   - No Blazor crash errors on page load + tenant select
 *
 * API-level budget persist/validation/reset verified separately via SSH (rv_b3_budget.sh).
 *
 * Run: npx playwright test e2e-tests/rv-batch3.spec.ts --config=playwright-rv-batch3.config.ts
 */

const APP2 = 'https://app2.khachvip.online';
const SYSADMIN_USER = 'sysadmin@vanan.vn';
const SYSADMIN_PASS = '2026@vanan';

test.describe('Batch 3 RV — LoyaltyConfigAdmin budget UI', () => {

  test('Page load: /admin/loyalty-config không crash, hiện budget form sau khi chọn tenant', async ({ page }) => {
    const errors: string[] = [];
    page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
    page.on('pageerror', e => errors.push(String(e)));

    // Login sysadmin qua UI form (cookie auth)
    await page.goto(`${APP2}/Login`, { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.fill('#username', SYSADMIN_USER);
    await page.fill('#password', SYSADMIN_PASS);
    await page.click('button[type="submit"]');
    await page.waitForURL(u => !u.toString().toLowerCase().includes('/login'), { timeout: 20000 });
    console.log(`[B3] login OK — ${page.url()}`);

    // Mở trang config
    const resp = await page.goto(`${APP2}/admin/loyalty-config`, { waitUntil: 'domcontentloaded', timeout: 30000 });
    expect(resp?.status()).toBe(200);
    await page.waitForTimeout(5000); // Blazor Server render

    const crash = errors.filter(e => /Exception|Unable to set property/i.test(e));
    console.log(`[B3] page errors: ${errors.length}, crash-like: ${crash.length}`);
    expect(crash, `crash errors: ${crash.join(' | ')}`).toEqual([]);

    // Global card render
    const body = await page.textContent('body') || '';
    expect(body).toContain('Cấu hình toàn cục');

    // Chọn tenant "Vạn An Test" → budget form hiện ra
    const tenantSelect = page.locator('#tenant-select');
    await tenantSelect.waitFor({ state: 'visible', timeout: 15000 });
    await tenantSelect.selectOption({ label: 'Vạn An Test' });
    await page.waitForTimeout(3500); // LoadTenantConfigAsync

    const body2 = (await page.textContent('body')) || '';
    console.log(`[B3] budget labels present: tháng=${body2.includes('Ngân sách tháng')} ngày=${body2.includes('Ngân sách ngày')} khách-ngày=${body2.includes('Giới hạn mỗi khách')} trần-đơn=${body2.includes('Trần mỗi đơn')}`);

    expect(body2, 'Ngân sách tháng (điểm)').toContain('Ngân sách tháng');
    expect(body2, 'Ngân sách ngày (điểm)').toContain('Ngân sách ngày');
    expect(body2, 'Giới hạn mỗi khách / ngày').toContain('Giới hạn mỗi khách');
    expect(body2, 'Trần mỗi đơn (% giá trị đơn)').toContain('Trần mỗi đơn');

    // Counters display
    expect(body2, 'Đã dùng tháng này').toContain('Đã dùng tháng này');
    expect(body2, 'Đã dùng hôm nay').toContain('Đã dùng hôm nay');

    // Reset buttons
    expect(body2, 'Reset counter hôm nay').toContain('Reset counter hôm nay');
    expect(body2, 'Reset counter tháng').toContain('Reset counter tháng');

    // Vạn An Test chưa có budget → các input rỗng (unlimited)
    const monthly = page.locator('#tenant-monthly-budget');
    if (await monthly.count() > 0) {
      const v = await monthly.inputValue();
      console.log(`[B3] monthly budget input value: '${v}' (rỗng = unlimited)`);
      expect(v).toBe('');
    }

    // Screenshot evidence
    await page.screenshot({ path: 'rv-b3-loyalty-config.png', fullPage: true });
  });
});
