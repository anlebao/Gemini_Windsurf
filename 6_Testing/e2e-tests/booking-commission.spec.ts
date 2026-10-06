import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// Booking P6.3 (Gate 4, SRS §17/§35 — AC-Q03/Q04/Q05): commission ledger E2E.
//
// Flow: QR salesman → customer booking (attribution §7.6) → tenant confirm/assign/check-in/start/complete
//       → commission auto-finalize tại COMPLETED (P6 hardening — ledger EARNED + tax snapshot).
//       → pay → PAID + WalletTransaction link.
//
// Chạy qua PUBLIC API (create booking) + TENANT API (transitions/ledger — JWT tenant_id claim).
//
// Prerequisites (RV P7 seed): booking-enabled tenant + staff + offering + schedule + QR GẮN SALESMAN.
//   - BOOKING_TEST_QR_TOKEN: qr_token raw (QR có salesmanId — §7.3)
//   - BOOKING_TEST_TENANT_ID: tenant id (Guid)
//   - BOOKING_TEST_TENANT_TOKEN: JWT chứa claim tenant_id (mint tại RV — impersonate pattern va-iie)
//   - BOOKING_TEST_OFFERING_ID, BOOKING_TEST_STAFF_ID, BOOKING_TEST_DATE, BOOKING_TEST_SLOT
//   - BOOKING_TEST_COMMISSION_RULE: 'true' nếu tenant đã có CommissionRule (global % hoặc offering)
// Self-gating: skip nếu thiếu env hoặc E2E tier tắt.

const config = loadEnvConfig();
const reporter = new TestReporter('Booking Commission E2E');

const QR_TOKEN = process.env.BOOKING_TEST_QR_TOKEN ?? '';
const TENANT_ID = process.env.BOOKING_TEST_TENANT_ID ?? '';
const TENANT_TOKEN = process.env.BOOKING_TEST_TENANT_TOKEN ?? '';
const OFFERING_ID = process.env.BOOKING_TEST_OFFERING_ID ?? '';
const STAFF_ID = process.env.BOOKING_TEST_STAFF_ID ?? '';
const TEST_DATE = process.env.BOOKING_TEST_DATE ?? '';
const TEST_SLOT = process.env.BOOKING_TEST_SLOT ?? '';
const COMMISSION_RULE = process.env.BOOKING_TEST_COMMISSION_RULE === 'true';

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VanAn Ecosystem - Booking Commission Ledger (AC-Q03/Q04/Q05)', () => {
  let createdBookingId: string | undefined;

  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    if (!QR_TOKEN || !TENANT_ID || !TENANT_TOKEN || !OFFERING_ID || !STAFF_ID || !TEST_DATE || !TEST_SLOT) {
      reporter.setArchitectDecision('Thiếu BOOKING_TEST_* env (QR/tenant/token/offering/staff/date/slot) — skip (RV P7 seed)');
      test.skip();
    }
    reporter.log('Starting Booking Commission E2E (AC-Q03/Q04/Q05)');
  });

  test('QR salesman → completed → commission EARNED + tax snapshot → PAID (AC-Q03/Q04/Q05)', async ({ request }) => {
    // 1. QR resolve → tenant + attribution + salesman (§7.3).
    const qrRes = await request.get(
      `${config.GATEWAY_PUBLIC_URL}/api/public/booking/qr/${QR_TOKEN}?anonymousSessionId=comm-${Date.now()}`
    );
    expect(qrRes.ok(), `QR resolve thất bại: ${qrRes.status()}`).toBeTruthy();
    const qrBody = await qrRes.json();
    expect(qrBody.tenantId).toBe(TENANT_ID);
    const attributionId = qrBody.attributionSessionId;
    const salesmanId = qrBody.salesmanId;
    expect(salesmanId, 'QR phải gắn salesman (booking commission)').toBeTruthy();

    // 2. Availability → slot.
    const availRes = await request.get(
      `${config.GATEWAY_PUBLIC_URL}/api/public/booking/availability` +
      `?tenantId=${TENANT_ID}&offeringId=${OFFERING_ID}&date=${TEST_DATE}&staffId=${STAFF_ID}`
    );
    if (!availRes.ok()) {
      test.skip(true, `Availability API lỗi ${availRes.status()} — cần seed schedule (RV P7)`);
      return;
    }
    const slots = (await availRes.json() as Array<{ startAt: string }>)
      .filter((s) => new Date(s.startAt).getTime() > Date.now()); // chỉ slot tương lai (RV hardening)
    const targetSlot = slots.find((s) => s.startAt.includes(TEST_SLOT));
    if (!targetSlot) {
      test.skip(true, `Slot ${TEST_SLOT} không available (RV P7)`);
      return;
    }

    // 3. Customer tạo booking qua public API (attribution snapshot §7.6).
    const createRes = await request.post(`${config.GATEWAY_PUBLIC_URL}/api/public/booking/bookings`, {
      headers: { 'Idempotency-Key': `comm-${Date.now()}` },
      data: {
        tenantId: TENANT_ID,
        offeringId: OFFERING_ID,
        startAt: targetSlot.startAt,
        staffId: null, // "Bất kỳ ai" → tenant assign (P5 queue flow)
        customerDeviceId: `comm-device-${Date.now()}`,
        customerNote: 'AC-Q03 commission flow',
        addOns: [],
        attributionId,
      },
    });
    if (createRes.status() === 409) {
      test.skip(true, 'Slot bị trùng bởi test khác — skip (RV P7)');
      return;
    }
    expect(createRes.ok(), `Create booking thất bại: ${createRes.status()}`).toBeTruthy();
    const bookingCode = (await createRes.json()).publicBookingCode as string;
    reporter.log(`Booking created: ${bookingCode}`);

    // 4. Tenant transitions → COMPLETED (queue tìm bookingId theo public code).
    const tenantHeaders = { Authorization: `Bearer ${TENANT_TOKEN}` };
    const queueRes = await request.get(`${config.GATEWAY_URL}/api/tenant/booking/bookings?from=${TEST_DATE}`, { headers: tenantHeaders });
    expect(queueRes.ok()).toBeTruthy();
    const queue = await queueRes.json() as Array<{ bookingId: string; publicBookingCode: string }>;
    const item = queue.find((b) => b.publicBookingCode === bookingCode);
    if (!item) {
      test.fail(true, `Không tìm thấy booking ${bookingCode} trong queue tenant`);
      return;
    }
    createdBookingId = item.bookingId;

    const transitions: Array<[string, object?]> = [
      [`/api/tenant/booking/bookings/${createdBookingId}/confirm`, undefined],
      [`/api/tenant/booking/bookings/${createdBookingId}/assign-staff`, { staffId: STAFF_ID }],
      [`/api/tenant/booking/bookings/${createdBookingId}/check-in`, undefined],
      [`/api/tenant/booking/bookings/${createdBookingId}/start`, undefined],
      [`/api/tenant/booking/bookings/${createdBookingId}/complete`, { actualTotal: 300000 }],
    ];
    for (const [url, body] of transitions) {
      const res = await request.post(`${config.GATEWAY_URL}${url}`, {
        headers: tenantHeaders,
        ...(body ? { data: body } : {}),
      });
      expect(res.ok(), `Transition ${url} thất bại: ${res.status()} ${await res.text()}`).toBeTruthy();
    }
    reporter.log('Booking COMPLETED via tenant transitions');

    // 5. Commission ledger — entry EARNED (auto-finalize tại COMPLETED — P6 hardening) + tax snapshot.
    const ledgerRes = await request.get(
      `${config.GATEWAY_URL}/api/tenant/booking/commission/ledger?bookingId=${createdBookingId}`,
      { headers: tenantHeaders }
    );
    expect(ledgerRes.ok()).toBeTruthy();
    const ledger = await ledgerRes.json() as Array<{
      entryId: string; bookingId: string; salesmanId: string; state: string;
      grossCommissionAmount: number; taxWithheldAmount: number; netCommissionAmount: number;
      taxRuleVersion?: string; walletTransactionId?: string;
    }>;
    const entry = ledger.find((e) => e.bookingId === createdBookingId);
    if (!entry) {
      // Tenant chưa có CommissionRule → KHÔNG có entry (qualification cần rule — §17.1) — báo + skip.
      if (!COMMISSION_RULE) {
        reporter.setArchitectDecision('Tenant chưa cấu hình CommissionRule — không có ledger entry (kỳ vọng đúng §17.1)');
        test.skip(true, 'Chưa có CommissionRule — set BOOKING_TEST_COMMISSION_RULE=true sau khi seed rule (RV P7)');
        return;
      }
      test.fail(true, 'Booking COMPLETED nhưng không có commission ledger entry');
      return;
    }

    expect(entry.salesmanId).toBe(salesmanId);
    expect(entry.state).toBe('EARNED');
    expect(entry.grossCommissionAmount).toBeGreaterThan(0);
    expect(entry.netCommissionAmount).toBeGreaterThan(0);
    // Tax snapshot version (NĐ 253/2026 adapter — §17.3): luôn có version khi tạo entry.
    expect(entry.taxRuleVersion).toBeTruthy();
    reporter.log(`Commission EARNED: gross=${entry.grossCommissionAmount} tax=${entry.taxWithheldAmount} net=${entry.netCommissionAmount}`);

    // 6. Pay → PAID + WalletTransaction link (§17.4).
    const payRes = await request.post(
      `${config.GATEWAY_URL}/api/tenant/booking/commission/ledger/${entry.entryId}/pay`,
      { headers: tenantHeaders }
    );
    expect(payRes.ok(), `Pay commission thất bại: ${payRes.status()}`).toBeTruthy();
    const paid = await payRes.json() as { state?: string; walletTransactionId?: string };
    expect(paid.state).toBe('PAID');
    expect(paid.walletTransactionId).toBeTruthy();
    reporter.log('Commission PAID + WalletTransaction link verified');
  });
});
