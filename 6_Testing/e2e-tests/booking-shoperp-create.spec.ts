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
    // Catalog: có dịch vụ (card render) hoặc tenant chưa bật (warning).
    await expect(page.locator('.vanan-card__title', { hasText: 'Chọn dịch vụ' })
      .or(page.locator('text=Tính năng đặt lịch đang TẮT'))).toBeVisible({ timeout: 15000 });
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

    // Slot đầu tiên (chỉ available — server filter). Nếu ngày mặc định hết slot (clock server khác
    // clock máy chạy test) → dò 7 ngày tới bằng cách đổi input date (press Tab để fire change — lesson #29).
    const dateInput = page.locator('[data-testid="booking-date"]');
    let slot = page.locator('[data-testid^="slot-"]').first();
    for (let d = 0; d < 7 && !(await slot.isVisible({ timeout: 1500 }).catch(() => false)); d++) {
      const dt = new Date();
      dt.setDate(dt.getDate() + d + 1);
      await dateInput.fill(dt.toISOString().slice(0, 10));
      await dateInput.press('Tab');
      await page.waitForTimeout(2500);
    }
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
    const codeMatch = successText.match(/[0-9A-F]{16}/);
    expect(codeMatch, 'Phải có mã booking (16 hex)').toBeTruthy();
    const bookingCode = codeMatch![0];
    console.log(`Booking created: ${bookingCode}`);

    // Queue hiển thị booking (Confirmed — auto-confirm Q3). Blazor Server cần circuit connect
    // (lesson #29) — retry reload tối đa 3 lần nếu chưa kịp render.
    await page.goto(`${shopErpBase}/booking/queue`);
    await page.waitForLoadState('networkidle');
    await expect(page.locator('h1')).toContainText('Hàng đợi đặt lịch', { timeout: 20000 });
    const card = page.locator(`div.mb-2:has-text("${bookingCode}")`).first();
    for (let i = 0; i < 3 && !(await card.isVisible().catch(() => false)); i++) {
      await page.reload({ waitUntil: 'networkidle' });
      await page.waitForTimeout(4000);
    }
    await expect(card).toBeVisible({ timeout: 30000 });

    // Cleanup: hủy qua queue (Confirmed → CANCELLED §9.3) — đảm bảo re-runnable.
    await card.locator('button', { hasText: 'Hủy' }).click();
    await page.waitForTimeout(3000);
    await page.reload({ waitUntil: 'networkidle' });
    await expect(page.locator(`div.mb-2:has-text("${bookingCode}")`).first()).toBeHidden({ timeout: 20000 }).catch(() => {
      console.log(`Cleanup: booking ${bookingCode} có thể đã hết hạn filter — bỏ qua`);
    });
  });
});
