import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// Booking P6.3 (Gate 4, SRS §12/§35 — AC-C04): double-booking prevention E2E.
//
// 2 khách hàng race cùng 1 staff + khung giờ → TỐI ĐA 1 booking thành công,
// request còn lại nhận 409 (business conflict, message thân thiện §6.5).
//
// Chạy qua PUBLIC API (anonymous — §25) — KHÔNG cần UI (deterministic hơn race UI).
//
// Prerequisites (RV P7 seed): booking-enabled tenant + staff + offering + schedule + QR.
//   - BOOKING_TEST_QR_TOKEN: qr_token raw (resolve → tenant + attribution session)
//   - BOOKING_TEST_OFFERING_ID: offering id (Guid)
//   - BOOKING_TEST_STAFF_ID: staff id (Guid — cùng skill offering, có schedule)
//   - BOOKING_TEST_DATE: ngày test (yyyy-MM-dd — weekday có schedule)
//   - BOOKING_TEST_SLOT: khung giờ test (HH:mm — nằm trong working interval)
// Self-gating: skip nếu thiếu env hoặc E2E tier tắt (playwright.rules).

const config = loadEnvConfig();
const reporter = new TestReporter('Booking Concurrency E2E');

const QR_TOKEN = process.env.BOOKING_TEST_QR_TOKEN ?? '';
const OFFERING_ID = process.env.BOOKING_TEST_OFFERING_ID ?? '';
const STAFF_ID = process.env.BOOKING_TEST_STAFF_ID ?? '';
const TEST_DATE = process.env.BOOKING_TEST_DATE ?? '';
const TEST_SLOT = process.env.BOOKING_TEST_SLOT ?? '';

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VanAn Ecosystem - Booking Double-Booking Race (AC-C04)', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    if (!QR_TOKEN || !OFFERING_ID || !STAFF_ID || !TEST_DATE || !TEST_SLOT) {
      reporter.setArchitectDecision('Thiếu BOOKING_TEST_* env (QR/offering/staff/date/slot) — skip (RV P7 seed)');
      test.skip();
    }
    reporter.log('Starting Booking Concurrency E2E (AC-C04)');
  });

  test('2 khách race cùng staff+slot → 1 thành công + 1 conflict 409 (AC-C04)', async ({ request }) => {
    // Resolve QR → tenantId + attribution session (P3.4 — booking gắn snapshot §7.6).
    const qrRes = await request.get(
      `${config.GATEWAY_PUBLIC_URL}/api/public/booking/qr/${QR_TOKEN}?anonymousSessionId=race-${Date.now()}`
    );
    expect(qrRes.ok(), `QR resolve thất bại: ${qrRes.status()}`).toBeTruthy();
    const qrBody = await qrRes.json();
    const tenantId = qrBody.tenantId;
    const attributionId = qrBody.attributionSessionId;
    expect(tenantId).toBeTruthy();

    // Availability: xác nhận slot trống TRƯỚC khi race (fresh-DB tolerance).
    const availRes = await request.get(
      `${config.GATEWAY_PUBLIC_URL}/api/public/booking/availability` +
      `?tenantId=${tenantId}&offeringId=${OFFERING_ID}&date=${TEST_DATE}&staffId=${STAFF_ID}`
    );
    if (!availRes.ok()) {
      test.skip(true, `Availability API lỗi ${availRes.status()} — cần seed schedule (RV P7)`);
      return;
    }
    const slots = await availRes.json() as Array<{ startAt: string }>;
    const targetSlot = slots.find((s) => s.startAt.includes(TEST_SLOT));
    if (!targetSlot) {
      test.skip(true, `Slot ${TEST_SLOT} không available — cần seed schedule/ngày (RV P7)`);
      return;
    }

    // ── RACE: 2 request song song cùng staff+slot, Idempotency-Key khác nhau ──
    const startAt = targetSlot.startAt;
    const [r1, r2] = await Promise.all([
      request.post(`${config.GATEWAY_PUBLIC_URL}/api/public/booking/bookings`, {
        headers: { 'Idempotency-Key': `race-1-${Date.now()}` },
        data: {
          tenantId,
          offeringId: OFFERING_ID,
          startAt,
          staffId: STAFF_ID,
          customerDeviceId: `race-device-${Date.now()}`,
          customerNote: 'AC-C04 race',
          addOns: [],
          attributionId,
        },
      }),
      request.post(`${config.GATEWAY_PUBLIC_URL}/api/public/booking/bookings`, {
        headers: { 'Idempotency-Key': `race-2-${Date.now()}` },
        data: {
          tenantId,
          offeringId: OFFERING_ID,
          startAt,
          staffId: STAFF_ID,
          customerDeviceId: `race-device-${Date.now()}`,
          customerNote: 'AC-C04 race',
          addOns: [],
          attributionId,
        },
      }),
    ]);

    const statuses = [r1.status(), r2.status()].sort();
    // Tối đa 1 thành công (200/201) — request còn lại conflict 409 (§12, AC-C04).
    const successCount = statuses.filter((s) => s === 200 || s === 201).length;
    const conflictCount = statuses.filter((s) => s === 409).length;

    reporter.log(`Race result: success=${successCount} conflict=${conflictCount} (${statuses.join(',')})`);
    expect(successCount + conflictCount, 'Phải có đúng 1 thành công + 1 conflict (hoặc 2 thành công nếu slot khác nhau)').toBeGreaterThanOrEqual(2);

    // Nếu cả 2 đều thành công → slot bị đặt 2 lần = FAIL double-booking.
    // (Chấp nhận khi DB reset giữa 2 request? Không — server lock là authoritative.)
    if (successCount === 2) {
      // Double-check: 2 booking cùng staff+slot là vi phạm AC-C04.
      reporter.log('⚠️ Cả 2 request thành công — kiểm tra lại server conflict lock');
    }

    // Conflict response phải có message thân thiện (§6.5).
    const conflictRes = r1.status() === 409 ? r1 : r2;
    if (conflictRes.status() === 409) {
      const body = await conflictRes.json();
      const msg = body?.message ?? '';
      expect(msg.length).toBeGreaterThan(0);
      reporter.log(`Conflict message: ${msg}`);
    }

    // Không có booking trùng → tối đa 1 booking cho staff+slot (verify bằng status).
    expect(successCount).toBeLessThanOrEqual(1);
  });

  test('Retry cùng Idempotency-Key → không tạo booking trùng (§21.1)', async ({ request }) => {
    const qrRes = await request.get(
      `${config.GATEWAY_PUBLIC_URL}/api/public/booking/qr/${QR_TOKEN}?anonymousSessionId=idem-${Date.now()}`
    );
    if (!qrRes.ok()) {
      test.skip(true, 'QR resolve thất bại — cần QR active (RV P7)');
      return;
    }
    const qrBody = await qrRes.json();
    const tenantId = qrBody.tenantId;
    const attributionId = qrBody.attributionSessionId;

    const availRes = await request.get(
      `${config.GATEWAY_PUBLIC_URL}/api/public/booking/availability` +
      `?tenantId=${tenantId}&offeringId=${OFFERING_ID}&date=${TEST_DATE}&staffId=${STAFF_ID}`
    );
    if (!availRes.ok()) return;
    const slots = await availRes.json() as Array<{ startAt: string }>;
    const targetSlot = slots.find((s) => s.startAt.includes(TEST_SLOT));
    if (!targetSlot) {
      test.skip(true, `Slot ${TEST_SLOT} không available (RV P7)`);
      return;
    }

    const idemKey = `idem-${Date.now()}`;
    const payload = {
      tenantId,
      offeringId: OFFERING_ID,
      startAt: targetSlot.startAt,
      staffId: STAFF_ID,
      customerDeviceId: `idem-device-${Date.now()}`,
      customerNote: 'AC §21.1 idempotency',
      addOns: [],
      attributionId,
    };

    const first = await request.post(`${config.GATEWAY_PUBLIC_URL}/api/public/booking/bookings`, {
      headers: { 'Idempotency-Key': idemKey },
      data: payload,
    });
    if (first.status() === 409) {
      test.skip(true, 'Slot bị trùng bởi test khác — skip (RV P7)');
      return;
    }
    expect(first.ok()).toBeTruthy();
    const code1 = (await first.json()).publicBookingCode;

    // Retry cùng key → trả về booking GỐC (idempotent), KHÔNG tạo booking mới.
    const retry = await request.post(`${config.GATEWAY_PUBLIC_URL}/api/public/booking/bookings`, {
      headers: { 'Idempotency-Key': idemKey },
      data: payload,
    });
    expect(retry.ok()).toBeTruthy();
    const code2 = (await retry.json()).publicBookingCode;
    expect(code2).toBe(code1);

    reporter.log(`Idempotency verified: code=${code1}`);
  });
});
