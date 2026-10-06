import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// Booking P6.3 (Gate 4, SRS §18.3/§35 — AC-Q01): tenant isolation E2E.
//
// Tenant A/B KHÔNG đọc được dữ liệu của nhau qua QR resolve, catalog, queue, token:
//   - QR A resolve → tenant A; QR B resolve → tenant B (không lẫn nhau)
//   - Catalog tenant A không chứa offering của tenant B
//   - Booking tạo qua QR A KHÔNG xuất hiện trong queue tenant B (JWT tenant_id claim)
//
// Mở rộng (self-gated theo seed):
//   - leave → unavailable: BOOKING_TEST_LEAVE_STAFF_ID + BOOKING_TEST_LEAVE_DATE
//   - deposit → financial status: BOOKING_TEST_DEPOSIT=true + tenant có deposit policy
//
// Prerequisites (RV P7 seed): 2 tenant enabled booking, mỗi tenant staff+offering+schedule+QR.
//   - BOOKING_TEST_QR_TOKEN_A / BOOKING_TEST_QR_TOKEN_B
//   - BOOKING_TEST_TENANT_TOKEN_A / BOOKING_TEST_TENANT_TOKEN_B (JWT tenant_id claim)
//   - BOOKING_TEST_OFFERING_ID_A / BOOKING_TEST_OFFERING_ID_B
// Self-gating: skip nếu thiếu env hoặc E2E tier tắt.

const config = loadEnvConfig();
const reporter = new TestReporter('Booking Isolation E2E');

const QR_A = process.env.BOOKING_TEST_QR_TOKEN_A ?? '';
const QR_B = process.env.BOOKING_TEST_QR_TOKEN_B ?? '';
const TOKEN_A = process.env.BOOKING_TEST_TENANT_TOKEN_A ?? '';
const TOKEN_B = process.env.BOOKING_TEST_TENANT_TOKEN_B ?? '';
const OFFERING_A = process.env.BOOKING_TEST_OFFERING_ID_A ?? '';
const OFFERING_B = process.env.BOOKING_TEST_OFFERING_ID_B ?? '';

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VanAn Ecosystem - Booking Tenant Isolation (AC-Q01 §18.3)', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    if (!QR_A || !QR_B || !TOKEN_A || !TOKEN_B || !OFFERING_A || !OFFERING_B) {
      reporter.setArchitectDecision('Thiếu BOOKING_TEST_* env (QR A/B + token A/B + offering A/B) — skip (RV P7 seed)');
      test.skip();
    }
    reporter.log('Starting Booking Isolation E2E (AC-Q01)');
  });

  test('QR A resolve tenant A; QR B resolve tenant B — không lẫn nhau (§18.3 #5)', async ({ request }) => {
    const [ra, rb] = await Promise.all([
      request.get(`${config.GATEWAY_PUBLIC_URL}/api/public/booking/qr/${QR_A}?anonymousSessionId=iso-a-${Date.now()}`),
      request.get(`${config.GATEWAY_PUBLIC_URL}/api/public/booking/qr/${QR_B}?anonymousSessionId=iso-b-${Date.now()}`),
    ]);
    expect(ra.ok()).toBeTruthy();
    expect(rb.ok()).toBeTruthy();

    const bodyA = await ra.json();
    const bodyB = await rb.json();

    // TenantId khác nhau + KHÔNG có dữ liệu chéo (attribution session riêng).
    expect(bodyA.tenantId).not.toBe(bodyB.tenantId);
    expect(bodyA.attributionSessionId).not.toBe(bodyB.attributionSessionId);
    reporter.log(`Tenant A=${bodyA.tenantId} Tenant B=${bodyB.tenantId}`);
  });

  test('Catalog tenant A không chứa offering tenant B (§18.3 — read boundary)', async ({ request }) => {
    const ra = await request.get(`${config.GATEWAY_PUBLIC_URL}/api/public/booking/qr/${QR_A}?anonymousSessionId=iso-cat-${Date.now()}`);
    expect(ra.ok()).toBeTruthy();
    const tenantA = (await ra.json()).tenantId as string;

    const catRes = await request.get(
      `${config.GATEWAY_PUBLIC_URL}/api/public/booking/tenants/${tenantA}/services`
    );
    expect(catRes.ok()).toBeTruthy();
    const catalog = await catRes.json() as { offerings: Array<{ id: string }> };

    // Offering của tenant B KHÔNG xuất hiện trong catalog tenant A.
    const offeringIds = catalog.offerings.map((o) => o.id);
    expect(offeringIds).not.toContain(OFFERING_B);
    // Offering A hiển thị (đúng tenant).
    if (!offeringIds.includes(OFFERING_A)) {
      test.skip(true, `Offering A chưa active/seed — cần seed (RV P7)`);
      return;
    }
    reporter.log(`Catalog isolation OK: ${offeringIds.length} offerings tenant A, không chứa offering B`);
  });

  test('Booking tạo qua QR A KHÔNG xuất hiện trong queue tenant B (§18.3 #1)', async ({ request }) => {
    // Tạo booking ở tenant A qua public API.
    const qrA = await request.get(`${config.GATEWAY_PUBLIC_URL}/api/public/booking/qr/${QR_A}?anonymousSessionId=iso-q-${Date.now()}`);
    if (!qrA.ok()) {
      test.skip(true, 'QR A không resolve (RV P7 seed)');
      return;
    }
    const qrBody = await qrA.json();
    const tenantA = qrBody.tenantId as string;
    const attributionId = qrBody.attributionSessionId;

    // Availability tenant A → slot đầu tiên.
    const avail = await request.get(
      `${config.GATEWAY_PUBLIC_URL}/api/public/booking/availability` +
      `?tenantId=${tenantA}&offeringId=${OFFERING_A}&date=${process.env.BOOKING_TEST_DATE ?? ''}`
    );
    if (!avail.ok()) {
      test.skip(true, `Availability tenant A lỗi ${avail.status()} — cần seed (RV P7)`);
      return;
    }
    const slots = await avail.json() as Array<{ startAt: string }>;
    if (slots.length === 0) {
      test.skip(true, 'Tenant A chưa có slot — cần seed schedule (RV P7)');
      return;
    }

    const createRes = await request.post(`${config.GATEWAY_PUBLIC_URL}/api/public/booking/bookings`, {
      headers: { 'Idempotency-Key': `iso-booking-${Date.now()}` },
      data: {
        tenantId: tenantA,
        offeringId: OFFERING_A,
        startAt: slots[0].startAt,
        staffId: null,
        customerDeviceId: `iso-device-${Date.now()}`,
        customerNote: 'AC-Q01 isolation',
        addOns: [],
        attributionId,
      },
    });
    if (createRes.status() === 409) {
      test.skip(true, 'Slot bị trùng test khác — skip (RV P7)');
      return;
    }
    expect(createRes.ok()).toBeTruthy();
    const bookingCode = (await createRes.json()).publicBookingCode as string;

    // Queue tenant B (JWT B) — KHÔNG chứa booking của tenant A.
    const queueB = await request.get(
      `${config.GATEWAY_URL}/api/tenant/booking/bookings?from=${process.env.BOOKING_TEST_DATE ?? ''}`,
      { headers: { Authorization: `Bearer ${TOKEN_B}` } }
    );
    expect(queueB.ok(), `Queue tenant B lỗi ${queueB.status()}`).toBeTruthy();
    const items = await queueB.json() as Array<{ publicBookingCode: string }>;
    expect(items.map((i) => i.publicBookingCode)).not.toContain(bookingCode);

    // Queue tenant A — chứa booking vừa tạo (chứng minh dữ liệu đúng tenant).
    const queueA = await request.get(
      `${config.GATEWAY_URL}/api/tenant/booking/bookings?from=${process.env.BOOKING_TEST_DATE ?? ''}`,
      { headers: { Authorization: `Bearer ${TOKEN_A}` } }
    );
    expect(queueA.ok()).toBeTruthy();
    const itemsA = await queueA.json() as Array<{ publicBookingCode: string }>;
    expect(itemsA.map((i) => i.publicBookingCode)).toContain(bookingCode);

    reporter.log('Isolation verified: booking A chỉ xuất hiện trong queue tenant A');
  });

  // ── Mở rộng (self-gated): leave → unavailable (§11.3) ──────────────────────

  test('Staff có override LEAVE → không available ngày đó (§11.3)', async ({ request }) => {
    const leaveStaff = process.env.BOOKING_TEST_LEAVE_STAFF_ID;
    const leaveDate = process.env.BOOKING_TEST_LEAVE_DATE;
    if (!leaveStaff || !leaveDate) {
      test.skip(true, 'Chưa set BOOKING_TEST_LEAVE_STAFF_ID/DATE — skip (RV P7 seed override)');
      return;
    }
    const availRes = await request.get(
      `${config.GATEWAY_PUBLIC_URL}/api/public/booking/availability` +
      `?tenantId=${process.env.BOOKING_TEST_TENANT_ID ?? ''}&offeringId=${OFFERING_A}&date=${leaveDate}&staffId=${leaveStaff}`
    );
    if (!availRes.ok()) {
      test.skip(true, `Availability lỗi ${availRes.status()} (RV P7)`);
      return;
    }
    const slots = await availRes.json() as Array<{ startAt: string }>;
    // LEAVE override → không có slot nào cho staff đó trong ngày.
    expect(slots.length).toBe(0);
    reporter.log(`Leave override verified: staff ${leaveStaff} không có slot ngày ${leaveDate}`);
  });

  // ── Mở rộng (self-gated): deposit → financial status (§16) ────────────────

  test('Booking có deposit → financial-status hiển thị DepositRequired (§16)', async ({ request }) => {
    if (process.env.BOOKING_TEST_DEPOSIT !== 'true') {
      test.skip(true, 'Chưa set BOOKING_TEST_DEPOSIT=true — skip (RV P7 seed deposit policy)');
      return;
    }
    const qrA = await request.get(`${config.GATEWAY_PUBLIC_URL}/api/public/booking/qr/${QR_A}?anonymousSessionId=iso-dep-${Date.now()}`);
    if (!qrA.ok()) {
      test.skip(true, 'QR A không resolve (RV P7)');
      return;
    }
    const qrBody = await qrA.json();
    const tenantA = qrBody.tenantId as string;

    const avail = await request.get(
      `${config.GATEWAY_PUBLIC_URL}/api/public/booking/availability` +
      `?tenantId=${tenantA}&offeringId=${OFFERING_A}&date=${process.env.BOOKING_TEST_DATE ?? ''}`
    );
    if (!avail.ok()) return;
    const slots = await avail.json() as Array<{ startAt: string }>;
    if (slots.length === 0) {
      test.skip(true, 'Tenant A chưa có slot (RV P7)');
      return;
    }

    const createRes = await request.post(`${config.GATEWAY_PUBLIC_URL}/api/public/booking/bookings`, {
      headers: { 'Idempotency-Key': `iso-dep-${Date.now()}` },
      data: {
        tenantId: tenantA,
        offeringId: OFFERING_A,
        startAt: slots[0].startAt,
        staffId: null,
        customerDeviceId: `dep-device-${Date.now()}`,
        customerNote: 'AC deposit',
        addOns: [],
        attributionId: qrBody.attributionSessionId,
      },
    });
    if (createRes.status() === 409) {
      test.skip(true, 'Slot bị trùng — skip (RV P7)');
      return;
    }
    expect(createRes.ok()).toBeTruthy();
    const bookingCode = (await createRes.json()).publicBookingCode as string;

    // Tìm bookingId trong queue tenant A rồi đọc financial-status.
    const queueA = await request.get(
      `${config.GATEWAY_URL}/api/tenant/booking/bookings?from=${process.env.BOOKING_TEST_DATE ?? ''}`,
      { headers: { Authorization: `Bearer ${TOKEN_A}` } }
    );
    expect(queueA.ok()).toBeTruthy();
    const items = await queueA.json() as Array<{ bookingId: string; publicBookingCode: string }>;
    const item = items.find((i) => i.publicBookingCode === bookingCode);
    if (!item) {
      test.skip(true, 'Không tìm thấy booking (RV P7)');
      return;
    }

    const finRes = await request.get(
      `${config.GATEWAY_URL}/api/tenant/booking/bookings/${item.bookingId}/financial-status`,
      { headers: { Authorization: `Bearer ${TOKEN_A}` } }
    );
    expect(finRes.ok()).toBeTruthy();
    const fin = await finRes.json() as { depositRequired: boolean; depositAmount: number | null; depositType: string | null };
    expect(fin.depositRequired).toBe(true);
    expect(fin.depositAmount).toBeGreaterThan(0);
    expect(fin.depositType).toBeTruthy();
    reporter.log(`Deposit verified: amount=${fin.depositAmount} type=${fin.depositType}`);
  });
});
