#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV SWEEP 2026-10-14 — Booking: Fix 1 (order routing) + Fix 2b (doanh thu kế toán)
// + BS-1 (edit lịch sau check-in) + BS-3 (entry point công khai)
// SWEEP-FIRST: chạy 1 lần thu TOÀN BỘ fail → phân tích theo cụm → fix nhóm → re-sweep.
//
// Chạy:  node rv-scripts/booking-rv-20261014.mjs
// Env:   .devin/rv-token-20261014.txt (tenant JWT — mint bằng rv_mint_token.sh)
//        .devin/rv-booking-20261014.env (tenant/offering/staff ids)
// ─────────────────────────────────────────────────────────────────────────────
import { readFileSync, writeFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO = join(__dirname, '..', '..');

function loadEnvFile(path) {
  try {
    const txt = readFileSync(path, 'utf-8');
    const out = {};
    for (const line of txt.split('\n')) {
      const t = line.trim();
      if (t && !t.startsWith('#')) {
        const i = t.indexOf('=');
        if (i > 0) out[t.slice(0, i).trim()] = t.slice(i + 1).trim();
      }
    }
    return out;
  } catch { return {}; }
}
const env = loadEnvFile(join(REPO, '.devin', 'rv-booking-20261014.env'));
const GW = 'https://api2.khachvip.online';
const TOKEN = (readFileSync(join(REPO, '.devin', 'rv-token-20261014.txt'), 'utf-8') || '').trim();

const V = {
  TID: env.BOOKING_TENANT_ID ?? '6aaf19e4-8b55-48cd-94d7-b61c96a2d66d',
  OFF_60: env.BOOKING_OFFERING_60 ?? 'fd69fdfc-fad2-4012-a187-ad24a6901939',
  OFF_90: env.BOOKING_OFFERING_90 ?? '78721ad0-ccf2-4239-85da-4e8de4a15049',
  STAFF_LAN: env.BOOKING_STAFF_LAN ?? '4e2fb124-adc9-467f-a79f-db2ff87bc84e', // skill cả 2 dịch vụ
  STAFF_HOA: env.BOOKING_STAFF_HOA ?? '96236f9d-30b9-47fe-bb76-e40530585e9d',
  SHOP_INSTANCE: env.BOOKING_SHOP_INSTANCE ?? '9e94f876-27bd-4a16-a85b-b5f42620bc6e',
};

const results = [];
const errors = [];
function check(name, ok, detail = '') {
  results.push({ name, ok: !!ok, detail });
  const mark = ok ? 'PASS' : 'FAIL';
  console.log(`  [${mark}] ${name}${detail ? ` — ${String(detail).slice(0, 200)}` : ''}`);
  if (!ok) errors.push({ name, detail });
}
async function api(method, path, { token, body, headers = {} } = {}) {
  const h = { 'Content-Type': 'application/json', ...headers };
  if (token) h.Authorization = `Bearer ${token}`;
  const res = await fetch(`${GW}${path}`, {
    method, headers: h,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  let data = null;
  try { data = await res.json(); } catch { /* non-json */ }
  return { status: res.status, ok: res.ok, data };
}
const f = (s) => (s && typeof s === 'object' ? JSON.stringify(s) : String(s)).slice(0, 250);
const asArray = (d) => (Array.isArray(d) ? d : []);
// So sánh thời điểm — response reload từ DB có thể thiếu hậu tố Z (DateTime Kind Unspecified
// khi materialize từ PG timestamp); API availability/create trả UTC có Z. Hiển thị UI đúng cả 2
// (ToVietnamTime coi Unspecified = UTC). Chuẩn hóa về UTC trước khi so sánh.
const toUtc = (s) => new Date(/Z$|[+-]\d\d:?\d\d$/.test(s) ? s : `${s}Z`).getTime();
const sameTime = (a, b) => !!a && !!b && toUtc(a) === toUtc(b);

// Ngày MAI (slot động — re-run không trùng, tránh self-conflict).
const TEST_DATE = new Date(Date.now() + 86400e3).toISOString().slice(0, 10);
const usedSlots = new Set();
async function getFutureSlot(offeringId, staffId, skip = 0) {
  const r = await api('GET', `/api/public/booking/availability?tenantId=${V.TID}&offeringId=${offeringId}&date=${TEST_DATE}${staffId ? `&staffId=${staffId}` : ''}`);
  if (!r.ok) return null;
  const slots = asArray(r.data)
    .filter((s) => !usedSlots.has(s.startAt))
    .sort((a, b) => a.startAt.localeCompare(b.startAt));
  const chosen = slots[skip]?.startAt;
  if (!chosen) return null;
  usedSlots.add(chosen);
  return chosen;
}
const T = (bookingId, path, body) => api('POST', `/api/tenant/booking/bookings/${bookingId}${path}`, { token: TOKEN, body });
const tput = (bookingId, body) => api('PUT', `/api/tenant/booking/bookings/${bookingId}`, { token: TOKEN, body });

// ── cleanup sweep bookings từ run trước (prefix 'rv14-') ──
async function cleanup() {
  const q = await api('GET', `/api/tenant/booking/bookings?from=${new Date().toISOString().slice(0, 10)}`, { token: TOKEN });
  let n = 0;
  for (const b of asArray(q.data).filter((x) => (x.customerDeviceId ?? '').startsWith('rv14-'))) {
    try {
      if (['PendingConfirmation', 'Confirmed', 'StaffAssigned'].includes(b.status))
        await T(b.bookingId, '/cancel', { reason: 'RV cleanup' });
      else if (['CheckedIn', 'InService'].includes(b.status)) {
        await T(b.bookingId, '/check-in', undefined).catch(() => {});
        await T(b.bookingId, '/start', undefined).catch(() => {});
        await T(b.bookingId, '/complete', { actualTotal: 450000 }).catch(() => {});
      }
      n++;
    } catch { /* best-effort */ }
  }
  return n;
}

console.log('════════════════════════════════════════════════════════════');
console.log('RV SWEEP 2026-10-14 — Fix1 + Fix2b + BS-1 + BS-3 (production)');
console.log(`Gateway: ${GW} | date: ${TEST_DATE}`);
console.log('════════════════════════════════════════════════════════════');
console.log(`[cleanup] bookings rv14- còn active: ${await cleanup()}`);

// ═══════════════════════════════════════════════════════════════════════
// A. BS-3 — entry point CÔNG KHAI (endpoint mới)
// ═══════════════════════════════════════════════════════════════════════
console.log('\n── A. Public entry point (BS-3) ──');
{
  const r = await api('GET', `/api/public/booking/entry?tenantId=${V.TID}`);
  check('A1 Entry Test HKD → 200 + enabled + link chứa /booking/ + QR PNG',
    r.status === 200 && r.data?.enabled === true
    && typeof r.data?.bookingLink === 'string' && r.data.bookingLink.includes('/booking/')
    && typeof r.data?.qrCodePngBase64 === 'string' && r.data.qrCodePngBase64.startsWith('data:image/png'),
    f(r.data));

  const r2 = await api('GET', '/api/public/booking/entry?tenantId=00000000-0000-0000-0000-00000000b0b0');
  check('A2 Entry tenant bogus → 200 + enabled=false (ẩn card, không 500)',
    r2.status === 200 && r2.data?.enabled === false, f(r2.data));

  const r3 = await api('GET', '/api/public/booking/entry');
  check('A3 Entry thiếu tenantId → 400', r3.status === 400, f(r3.data));
}

// ═══════════════════════════════════════════════════════════════════════
// B. BS-1 — PUT /bookings/{id} (edit nội dung, kể cả sau check-in)
// ═══════════════════════════════════════════════════════════════════════
console.log('\n── B. BS-1: edit booking (PUT) ──');
const slotA = await getFutureSlot(V.OFF_60, V.STAFF_LAN);
check('B0 Có slot tương lai cho staff KTV Lan', !!slotA, `slot=${slotA}`);

let main = null;   // booking chính (đi qua toàn bộ flow)
let extra = null;  // booking phụ (conflict test)
let code = null;
if (slotA) {
  const created = await api('POST', '/api/tenant/booking/bookings', {
    token: TOKEN,
    body: {
      offeringId: V.OFF_60, addOnIds: [], startAt: slotA, staffId: V.STAFF_LAN,
      customerName: 'RV BS-1', customerPhone: '0900000001', customerNote: 'Khởi tạo',
    },
    headers: { 'Idempotency-Key': `rv14-main-${Date.now()}` },
  });
  check('B1 Create (tenant, staff Lan) → 201 StaffAssigned + OfferingId DTO',
    created.status === 201 && created.data?.status === 'StaffAssigned' && !!created.data?.offeringId,
    f(created.data));
  main = created.data;
  code = main?.publicBookingCode;

  // B2: đổi note
  const u1 = await tput(main.bookingId, { customerNote: 'Ghi chú đã sửa' });
  check('B2 PUT note → 200 + note cập nhật (không đổi dịch vụ/giờ/staff)',
    u1.status === 200 && u1.data?.customerNote === 'Ghi chú đã sửa'
    && u1.data?.offeringId === V.OFF_60 && sameTime(u1.data?.startAt, slotA),
    f(u1.data));

  // B3: đổi dịch vụ 60 → 90 (KTV Lan có skill cả 2) → snapshot + tổng tiền
  const u2 = await tput(main.bookingId, { offeringId: V.OFF_90 });
  check('B3 PUT offering → Massage 90 (450k) + snapshot + EstimatedTotal 450000',
    u2.status === 200 && u2.data?.offeringId === V.OFF_90
    && u2.data?.offeringNameSnapshot === 'Massage 90 phút' && u2.data?.estimatedTotal === 450000,
    f(u2.data));

  // B4: đổi giờ sang slot khác (cùng staff)
  const slotB = await getFutureSlot(V.OFF_90, V.STAFF_LAN, 1);
  if (slotB) {
    const u3 = await tput(main.bookingId, { startAt: slotB });
    check('B4 PUT startAt → slot mới (re-check availability)',
      u3.status === 200 && sameTime(u3.data?.startAt, slotB), f(u3.data));
  } else check('B4 (skip)', false, 'no second slot');

  // B5: conflict — booking phụ cùng staff trùng slotB → PUT main về slotB → 409
  const slotC = await getFutureSlot(V.OFF_60, V.STAFF_LAN, 0);
  if (slotC && slotC !== slotB) {
    const extraR = await api('POST', '/api/tenant/booking/bookings', {
      token: TOKEN,
      body: { offeringId: V.OFF_60, addOnIds: [], startAt: slotC, staffId: V.STAFF_LAN,
        customerName: 'RV conflict', customerPhone: '0900000002', customerNote: 'conflict' },
      headers: { 'Idempotency-Key': `rv14-extra-${Date.now()}` },
    });
    extra = extraR.data;
    check('B5a Booking phụ tạo OK (cùng staff, slot riêng)', extraR.status === 201, f(extraR.data));
    if (extra) {
      const u4 = await tput(main.bookingId, { startAt: slotC });
      check('B5b PUT về slot đã có booking phụ → 409 conflict (exclude self vẫn chặn booking khác)',
        u4.status === 409, f(u4.data));
      // khôi phục giờ main (không để conflict)
      const u5 = await tput(main.bookingId, { startAt: slotB });
      check('B5c Khôi phục giờ main về slotB', u5.status === 200 && sameTime(u5.data?.startAt, slotB), f(u5.data));
    }
  } else check('B5 (skip)', false, 'no third slot');

  // B6: check-in → vẫn sửa được (yêu cầu user: "kể cả khi khách đã check in")
  const ci = await T(main.bookingId, '/check-in');
  check('B6a Check-in → CheckedIn', ci.status === 200 && ci.data?.status === 'CheckedIn', f(ci.data));
  const u6 = await tput(main.bookingId, { customerNote: 'Sửa sau check-in' });
  check('B6b PUT note SAU check-in → 200 (yêu cầu BS-1)',
    u6.status === 200 && u6.data?.customerNote === 'Sửa sau check-in', f(u6.data));
}

// ═══════════════════════════════════════════════════════════════════════
// C+D. F1 order hook + F2b doanh thu (deposit → 3387; complete → 511/3331)
// ═══════════════════════════════════════════════════════════════════════
console.log('\n── C. F2b deposit + F1 order hook ──');
if (main) {
  const dep = await T(main.bookingId, '/deposit/received', { amount: 100000 });
  check('C1 Deposit 100k → Paid (trigger 3387)', dep.status === 200 && dep.data?.depositStatus === 'Paid', f(dep.data));

  const st = await T(main.bookingId, '/start');
  check('C2 Start → InService', st.status === 200 && st.data?.status === 'InService', f(st.data));

  const co = await T(main.bookingId, '/complete', { actualTotal: 450000 });
  check('C3 Complete → Completed + OrderId (Fix 1 hook)', co.status === 200 && co.data?.status === 'Completed' && !!co.data?.orderId, f(co.data));
  const orderId = co.data?.orderId;
  const oidShort = orderId ? String(orderId).slice(0, 8) : '';
  check('C4 BookingQueueItemDto có OfferingId (BS-1 DTO)', !!co.data?.offeringId, f(co.data));

  // ── verification 2 lớp (PG + ShopERP SQLite) — chạy riêng qua SSH, key = booking code
  console.log(`\n  → bookingCode=${code} bookingId=${main.bookingId} orderId=${orderId} (${oidShort})`);
  console.log(`  → PG: SELECT * FROM "AccountingEntries" WHERE "Reference" LIKE 'Booking:${main.bookingId}%';`);
  console.log(`  → PG: SELECT "RoutingKey" FROM "OutboxMessages" WHERE "EventType"='OrderCreated' AND "CorrelationId"='${orderId}';`);
  console.log(`  → shop-a SQLite: SELECT "Id","TrackingCode" FROM "Orders" WHERE "TrackingCode"='${code}';`);
  writeFileSync(join(REPO, '.devin', 'rv-sweep-20261014-key.txt'),
    `bookingCode=${code}\nbookingId=${main.bookingId}\norderId=${orderId}\ntestDate=${TEST_DATE}\n`);
}

// ── cleanup booking phụ ──
if (extra && ['StaffAssigned', 'Confirmed', 'PendingConfirmation'].includes(extra.status)) {
  const cx = await T(extra.bookingId, '/cancel', { reason: 'RV cleanup' });
  check('Z1 Cleanup booking phụ → cancelled', cx.status === 200, f(cx.data));
}

// ═══════════════════════════════════════════════════════════════════════
console.log('\n════════════════════════════════════════════════════════════');
const passCount = results.filter((r) => r.ok).length;
console.log(`SUMMARY: ${passCount}/${results.length} PASS`);
if (errors.length) {
  console.log('FAILS:');
  for (const e of errors) console.log(`  - ${e.name}: ${e.detail}`);
  writeFileSync(join(REPO, '.devin', 'booking-rv-20261014-report.json'), JSON.stringify(results, null, 2));
  process.exit(1);
}
writeFileSync(join(REPO, '.devin', 'booking-rv-20261014-report.json'), JSON.stringify(results, null, 2));
process.exit(0);
