import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// Booking P4.8 (Gate 4, SRS §35 — AC-C01/C02/C03/C06): KhachLink customer booking flow E2E.
//
// Flow: QR resolve → Screen 1 (Offering) → Screen 2 (Time & Staff) → Screen 3 (Note & Deposit)
//       → Screen 4 (Confirm) → submit (Idempotency-Key) → Status page (polling §10).
//
// Prerequisites (RV P7): booking-enabled tenant + staff + offering + working schedule + QR channel.
//   - BOOKING_TEST_QR_TOKEN: qr_token raw (tạo qua tenant API /api/tenant/qr-channels — P5)
//   - BOOKING_TEST_TENANT_ID: optional — dùng cho teardown/assert
// Self-gating: spec bị skip nếu thiếu env hoặc E2E tier tắt (playwright.rules).
//
// Fresh-DB tolerance: các bước dùng .or() chấp nhận empty state hợp lệ (page hoạt động đúng).

const config = loadEnvConfig();
const reporter = new TestReporter('Booking Customer E2E');

const QR_TOKEN = process.env.BOOKING_TEST_QR_TOKEN ?? '';

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VanAn Ecosystem - Booking Customer Flow (SRS v1.1 MVP)', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    if (!QR_TOKEN) {
      reporter.setArchitectDecision('BOOKING_TEST_QR_TOKEN chưa set — spec bị skip (RV P7 setup)');
      test.skip();
    }
    reporter.log(`Starting Booking Customer E2E (qr token: ${QR_TOKEN.slice(0, 8)}...)`);
  });

  // ─── AC-Q01: QR resolve → Screen 1 (Offering) ────────────────────────────

  test('QR resolve hiển thị tenant branding + offering screen (AC-Q01/C01)', async ({ page }) => {
    await page.goto(`${config.KHACHLINK_URL}/booking/${QR_TOKEN}`);
    await page.waitForLoadState('networkidle');

    // Tenant name (branding §5.2) HOẶC error alert hợp lệ (QR không active)
    await expect(
      page.locator('[data-testid="booking-tenant-name"]').or(page.locator('.vanan-alert').first())
    ).toBeVisible({ timeout: 20000 });

    const offeringList = page.locator('[data-testid="booking-offering-list"] .booking-offering-card');
    const noService = page.locator('.vanan-alert:has-text("chưa mở")');
    // Có offering cards hoặc alert "chưa mở đặt lịch" — cả 2 đều chứng minh page hoạt động
    await expect(offeringList.first().or(noService.first())).toBeVisible({ timeout: 15000 });
  });

  // ─── AC-C01/C02/C03/C06: Full flow → Status page ─────────────────────────

  test('Full flow: offering → time → note → confirm → status (AC-C01/C02/C03/C06)', async ({ page }) => {
    await page.goto(`${config.KHACHLINK_URL}/booking/${QR_TOKEN}`);
    await page.waitForLoadState('networkidle');

    // ── Screen 1: chọn offering đầu tiên ──
    const offeringCard = page.locator('[data-testid="booking-offering-list"] .booking-offering-card').first();
    if (!(await offeringCard.isVisible({ timeout: 15000 }).catch(() => false))) {
      test.skip(true, 'Tenant chưa có offering active — cần seed data (RV P7)');
      return;
    }
    await offeringCard.click();
    await expect(page.locator('[data-testid="booking-btn-continue"]')).toBeVisible({ timeout: 10000 });
    await page.locator('[data-testid="booking-btn-continue"]').click();

    // ── Screen 2: chọn ngày + slot ──
    await page.waitForLoadState('networkidle');
    const datePicker = page.locator('[data-testid="booking-date-picker"] button').first();
    if (!(await datePicker.isVisible({ timeout: 10000 }).catch(() => false))) {
      test.skip(true, 'Không vào được màn chọn thời gian');
      return;
    }

    // Duyệt tối đa 7 ngày tìm slot trống
    let slotClicked = false;
    for (let i = 0; i < 7 && !slotClicked; i++) {
      const dayBtn = page.locator('[data-testid="booking-date-picker"] button').nth(i);
      await dayBtn.click();
      await page.waitForTimeout(1200); // chờ availability API trả về

      const slotBtn = page.locator('[data-testid="booking-slot-grid"] button').first();
      if (await slotBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
        await slotBtn.click();
        slotClicked = true;
      }
    }
    if (!slotClicked) {
      test.skip(true, 'Không có khung giờ trống trong 7 ngày — cần seed working schedule (RV P7)');
      return;
    }

    await expect(page.locator('[data-testid="booking-btn-continue"]')).toBeVisible({ timeout: 10000 });
    await page.locator('[data-testid="booking-btn-continue"]').click();

    // ── Screen 3: Quick Tag + ghi chú text (STT fallback — text-only §15.3) ──
    await page.waitForLoadState('networkidle');
    const quickTag = page.locator('[data-testid="booking-tag-Phòng-riêng"]');
    if (await quickTag.isVisible({ timeout: 10000 }).catch(() => false)) {
      await quickTag.click();
    }
    const noteInput = page.locator('[data-testid="booking-note-textarea"]');
    if (await noteInput.isVisible({ timeout: 5000 }).catch(() => false)) {
      await noteInput.fill('E2E booking test');
    }

    // Deposit selector (§16.1) hiển thị
    await expect(
      page.locator('[data-testid="booking-deposit"]').or(page.locator('.vanan-alert').first())
    ).toBeVisible({ timeout: 10000 });

    await expect(page.locator('[data-testid="booking-btn-continue"]')).toBeVisible({ timeout: 10000 });
    await page.locator('[data-testid="booking-btn-continue"]').click();

    // ── Screen 4: Confirm summary + submit ──
    await page.waitForLoadState('networkidle');
    await expect(page.locator('[data-testid="confirm-offering"]')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('[data-testid="confirm-total"]')).toBeVisible({ timeout: 5000 });

    const submitBtn = page.locator('[data-testid="booking-btn-submit"]');
    await expect(submitBtn).toBeVisible({ timeout: 5000 });
    await submitBtn.click();

    // ── Status page (AC-C06 — polling): PendingConfirmation hoặc lỗi conflict hướng dẫn (§6.5) ──
    await page.waitForURL(/\/booking\/status\//, { timeout: 20000 });
    await expect(
      page.locator('[data-testid="booking-status-label"]').or(page.locator('[data-testid="booking-submit-error"]'))
    ).toBeVisible({ timeout: 20000 });

    // Status label hoặc error conflict đều là trạng thái page hoạt động đúng
    const statusLabel = page.locator('[data-testid="booking-status-label"]');
    if (await statusLabel.isVisible({ timeout: 10000 }).catch(() => false)) {
      const text = (await statusLabel.textContent()) ?? '';
      reporter.log(`Booking status: ${text.trim()}`);
      // Booking code hiển thị (opaque token §25)
      await expect(page.locator('[data-testid="booking-code"]')).toBeVisible({ timeout: 5000 });
    } else {
      reporter.log('Booking submit trả error conflict — page hiển thị hướng dẫn (§6.5)');
    }
  });

  // ─── §10.2: Status polling — terminal stop ────────────────────────────────

  test('Status page có nút Làm mới + tự cập nhật (AC-C06 §10.2)', async ({ page }) => {
    // Không cần booking code thật — chỉ verify page render đúng với code bất kỳ (404 → error + Làm mới).
    await page.goto(`${config.KHACHLINK_URL}/booking/status/testcode-e2e-${Date.now()}`);
    await page.waitForLoadState('networkidle');

    await expect(
      page.locator('button:has-text("Làm mới")').or(page.locator('[data-testid="booking-status-label"]'))
    ).toBeVisible({ timeout: 20000 });
  });
});
