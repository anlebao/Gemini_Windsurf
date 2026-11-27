import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// Direct-sell offering (2026-11-26, Gate 4): AppointmentOffering.IsSellable.
// Admin: /booking/services modal có checkbox "Bán trực tiếp" + badge trên grid.
// Storefront: offering IsSellable hiện nút "Đặt mua" cạnh "Đặt lịch" → thêm vào giỏ.
//
// Storefront leg cần tenant booking-enabled + offering sellable — tolerant skip
// khi không có data (pattern booking-customer.spec.ts).

const config = loadEnvConfig();
const reporter = new TestReporter('Service Direct Sell E2E');
const shopErpBase = process.env.BOOKING_SHOPERP_URL || config.SHOPERP_URL;
const khachLinkBase = config.KHACHLINK_URL;

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VanAn Ecosystem - Dịch vụ bán trực tiếp (IsSellable)', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting Service Direct Sell E2E...');
  });

  test.beforeEach(async ({ page, request }) => {
    try {
      const response = await request.post(`${shopErpBase}/dev/login`, { timeout: 8000 });
      if (response.ok()) {
        const body = await response.json();
        console.log(`Dev login successful: tenantId=${body.tenantId}, role=${body.role}`);
      }
    } catch {
      console.log('Dev login unavailable — dùng storageState (production RV)');
    }
  });

  test('Admin: modal dịch vụ có toggle Bán trực tiếp + badge trên grid', async ({ page }) => {
    await page.goto(`${shopErpBase}/dashboard`);
    await page.waitForLoadState('networkidle');
    await page.goto(`${shopErpBase}/booking/services`);
    await page.waitForLoadState('networkidle');
    await expect(page.locator('h1')).toContainText('Dịch vụ đặt lịch', { timeout: 15000 });

    // Mở modal tạo → checkbox sellable phải hiện.
    await page.locator('[data-testid="offering-add"]').click();
    await expect(page.locator('.modal')).toBeVisible();
    const sellableToggle = page.locator('[data-testid="offering-sellable"]');
    await expect(sellableToggle).toBeVisible({ timeout: 10000 });
    await expect(sellableToggle).not.toBeChecked(); // default off — không phá vỡ flow cũ

    // Tạo offering sellable → badge "Bán trực tiếp" xuất hiện trên grid.
    const name = `RV Bán trực tiếp ${Date.now()}`;
    await page.locator('[data-testid="offering-name"]').fill(name);
    await page.locator('[data-testid="offering-duration"]').fill('30');
    await page.locator('[data-testid="offering-price"]').fill('100000');
    await sellableToggle.check();
    await page.locator('.modal button', { hasText: 'Confirm' }).click();

    await expect(page.locator('[data-testid="services-success"]')).toBeVisible({ timeout: 20000 });
    const row = page.locator(`tr:has-text("${name}")`).first();
    await expect(row).toBeVisible({ timeout: 15000 });
    await expect(row.locator('.badge', { hasText: 'Bán trực tiếp' })).toBeVisible({ timeout: 10000 });
    reporter.log(`Offering sellable created: ${name}`);
  });

  test('Storefront: offering sellable hiện nút Đặt mua → thêm vào giỏ', async ({ page }) => {
    // Cần tenant storefront có booking enabled + offering IsSellable — env-gated.
    const storeUrl = process.env.BOOKING_TEST_STORE_URL;
    if (!storeUrl) {
      test.skip(true, 'BOOKING_TEST_STORE_URL chưa set — cần storefront tenant có offering sellable (RV setup)');
      return;
    }

    await page.goto(storeUrl);
    await page.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => {});

    const buyBtn = page.locator('[data-testid^="store-offering-buy-"]').first();
    if (!(await buyBtn.isVisible({ timeout: 20000 }).catch(() => false))) {
      test.skip(true, 'Storefront không có offering IsSellable — cần seed (RV setup)');
      return;
    }

    await buyBtn.click();
    // Toast xác nhận thêm vào giỏ (vananShowCartToast).
    await expect(
      page.locator('.vanan-toast, [class*="toast"]').filter({ hasText: 'giỏ hàng' }).first()
        .or(page.locator('text=/Đã thêm .* vào giỏ hàng/').first())
    ).toBeVisible({ timeout: 10000 });
    reporter.log('Storefront Đặt mua → cart toast visible');
  });
});
