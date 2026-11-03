import { test, expect, Page } from '@playwright/test';

// RV sweep-first: toàn bộ phiếu Voucher Platform (registry-driven) — route + render form.
const TENANT = '7c021960-6d6c-4de4-88a1-cf8373184f49';

const VOUCHERS = [
  { path: '/vouchers/advance', name: 'Phiếu Tạm Ứng', fields: ['vf-amount', 'vf-date', 'vf-party'] },
  { path: '/vouchers/stock-issue', name: 'Phiếu Xuất Kho', fields: ['vf-amount', 'vf-date', 'vf-mode'] },
  { path: '/vouchers/asset-disposal', name: 'Thanh Lý TSCĐ', fields: ['vf-assetName', 'vf-originalCost', 'vf-accumulatedDepreciation', 'vf-liquidationPrice', 'vf-date'] },
];

async function setup(page: Page) {
  for (let i = 1; i <= 3; i++) {
    try {
      await page.goto('/login', { waitUntil: 'domcontentloaded' });
      break;
    } catch (e) {
      if (i === 3 || !String(e).includes('Download is starting')) throw e;
      await page.waitForTimeout(1500);
    }
  }
  await page.waitForLoadState('domcontentloaded');
  await page.evaluate(async (t) => {
    await fetch('/api/platform/login', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ Username: 'sysadmin@vanan.vn', Password: '2026@vanan' }),
      credentials: 'include',
    });
    await fetch(`/api/admin/impersonate/${t}`, { method: 'POST', credentials: 'include' });
  }, TENANT);
}

async function safeGoto(page: Page, path: string) {
  for (let i = 1; i <= 3; i++) {
    try {
      const r = await page.goto(path, { waitUntil: 'domcontentloaded' });
      await page.waitForLoadState('networkidle');
      return r;
    } catch (e) {
      if (i === 3 || !String(e).includes('Download is starting')) throw e;
      await page.waitForTimeout(1500);
    }
  }
  return null;
}

test.describe('RV Voucher Platform sweep', () => {
  for (const v of VOUCHERS) {
    test(`${v.name} — route render + schema fields (read-only)`, async ({ page }) => {
      await setup(page);
      const resp = await safeGoto(page, v.path);
      expect(resp?.status() ?? 0, `${v.path} status`).toBeLessThan(400);
      await expect(page.getByTestId('voucher-save')).toBeVisible({ timeout: 30000 });
      for (const f of v.fields) {
        await expect(page.getByTestId(f)).toBeVisible({ timeout: 15000 });
      }
      const h1 = await page.locator('h1').textContent();
      console.log(`${v.path} → OK, h1: ${h1?.trim()}`);
    });
  }

  test('AccountingIndex — nhóm Chứng Từ / Phiếu đủ 8 phiếu', async ({ page }) => {
    await setup(page);
    await safeGoto(page, '/accounting');
    await expect(page.getByText('Chứng Từ / Phiếu — theo nghiệp vụ')).toBeVisible({ timeout: 30000 });
    for (const label of ['Phiếu Thu', 'Phiếu Chi', 'Chuyển Tiền Nội Bộ', 'Tạm Ứng / Hoàn Ứng', 'Phiếu Xuất Kho', 'Thanh Lý TSCĐ', 'Phiếu Nhập Mua', 'Phiếu Hạch Toán CTGS']) {
      await expect(page.getByText(label, { exact: false }).first()).toBeVisible();
    }
    console.log('AccountingIndex voucher group OK — 8 phiếu');
  });
});