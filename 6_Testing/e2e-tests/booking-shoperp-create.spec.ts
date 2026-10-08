import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// Feature 3 (Gate 4, 2026-10-08): Luồng đặt lịch từ ShopERP — Owner/Staff tạo thay khách.
// Validates: /booking/create (POS-style) → chọn dịch vụ → slot → khách → tạo → success + mã
// → xuất hiện trong /booking/queue → cleanup bằng hủy (Confirmed → CANCELLED §9.3).
//
// Strategy (matches cong-no.spec.ts):
// - Self-gating: skip khi ENABLE_E2E=false.
// - Render tests dùng .or() chấp nhận data hoặc empty/error state (tenant chưa có catalog).
// - Flow test chỉ chạy khi tenant booking đã enable + có catalog (production RV seed);
//   cleanup UI-driven (không cần token env).

const config = loadEnvConfig();
const reporter = new TestReporter('Booking ShopERP Create E2E');

const shopErpBase = process.env.BOOKING_SHOPERP_URL || config.SHOPERP_URL;

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VanAn Ecosystem - Đặt lịch nhanh từ ShopERP (Feature 3)', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting Booking ShopERP Create E2E...');
  });

  test.beforeEach(async ({ page, request }) => {
    // Dev login endpoint CHỈ tồn tại trong DEBUG build; production RV dùng storageState.
    try {
      const response = await request.post(`${shopErpBase}/dev/login`, { timeout: 8000 });
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

  // ─── RENDER TESTS (Gate 4) ───────────────────────────────────────────────

  test('Đặt lịch nhanh render + Sitemap link', async ({ page }) => {
    await page.goto(`${shopErpBase}/booking/create`);
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText('Đặt lịch nhanh', { timeout: 15000 });
    // Catalog: có dịch vụ hoặc empty-state hợp lệ (tenant chưa cấu hình).
    const catalog = page.locator('text=Chọn dịch vụ');
    await expect(catalog.or(page.locator('text=Tính năng đặt lịch đang TẮT'))).toBeVisible({ timeout: 15000 });
  });

  // ─── FLOW TEST (production RV — tenant booking đã seed) ─────────────────

  test('Tạo lịch hẹn từ ShopERP → queue → hủy (cleanup)', async ({ page }) => {
    const customerName = `KH E2E ${Date.now()}`;

    await page.goto(`${shopErpBase}/booking/create`);
    await page.waitForLoadState('networkidle');

    // Bỏ qua nếu tenant chưa bật/có catalog.
    const disabledWarning = page.locator('text=Tính năng đặt lịch đang TẮT');
    if (await disabledWarning.isVisible({ timeout: 15000 }).catch(() => false)) {
      test.skip(true, 'Tenant chưa bật đặt lịch — RV flow cần bật config trước');
      return;
    }

    await expect(page.locator('h1')).toContainText('Đặt lịch nhanh', { timeout: 15000 });

    // Chọn dịch vụ đầu tiên.
    const chooseButton = page.locator('button', { hasText: 'Chọn' }).first();
    await expect(chooseButton).toBeVisible({ timeout: 15000 });
    await chooseButton.click();

    // Slot đầu tiên (chỉ available — server filter) → click.
    const slot = page.locator('[data-testid^="slot-"]').first();
    await expect(slot).toBeVisible({ timeout: 15000 });
    await slot.click();

    // Điền khách + ghi chú.
    await page.locator('[data-testid="booking-customer-name"]').fill(customerName);
    await page.locator('[data-testid="booking-note"]').fill('E2E tạo từ ShopERP');

    // Submit.
    await page.locator('[data-testid="booking-create-submit"]').click();

    // Success + mã booking.
    const success = page.locator('[data-testid="booking-create-success"]');
    await expect(success).toBeVisible({ timeout: 20000 });
    const successText = await success.innerText();
    const codeMatch = successText.match(/BK-[A-Z0-9-]+/);
    expect(codeMatch, 'Phải có mã booking BK-...').toBeTruthy();
    const bookingCode = codeMatch![0];
    console.log(`Booking created: ${bookingCode}`);

    // Queue hiển thị booking (Confirmed — auto-confirm Q3).
    await page.goto(`${shopErpBase}/booking/queue`);
    await page.waitForLoadState('networkidle');
    await expect(page.locator(`text=${bookingCode}`).first()).toBeVisible({ timeout: 20000 });

    // Cleanup: hủy qua queue (Confirmed → CANCELLED §9.3) — đảm bảo re-runnable.
    const card = page.locator(`.vanan-card:has-text("${bookingCode}")`).first();
    await expect(card).toBeVisible({ timeout: 20000 });
    await card.locator('button', { hasText: 'Hủy' }).click();
    await expect(page.locator(`text=${bookingCode}`).first()).toBeHidden({ timeout: 20000 }).catch(() => {
      console.log(`Cleanup: booking ${bookingCode} có thể đã hết hạn filter — bỏ qua`);
    });
  });
});
