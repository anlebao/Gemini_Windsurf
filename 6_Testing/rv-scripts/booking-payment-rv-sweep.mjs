#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV SWEEP — Issue #195 booking-payment fix (243112db): "Thanh toán" = payment
// thật + single revenue source. PRODUCTION (app2/api2.khachvip.online).
//
// Sweep-first methodology (runtime-verification.md): quét TOÀN BỘ trong 1 lần,
// thu tất cả FAIL vào report — KHÔNG fix case-by-case.
//
// Chạy:  node rv-scripts/booking-payment-rv-sweep.mjs
// Report: rv-scripts/booking-payment-rv-report.json (bookingId/orderId cho DB-check)
// DB verify: rv-scripts/booking-payment-rv-dbcheck.sh (đọc report)
// UI final: playwright-rv-195b (spec rv-195-followup) + spec booking-payment (sau sweep sạch)
// ─────────────────────────────────────────────────────────────────────────────
import { writeFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPORT_PATH = join(__dirname, 'booking-payment-rv-report.json');

const APP2 = 'https://app2.khachvip.online';   // ShopERP (login + impersonate)
const GW = 'https://api2.khachvip.online';     // Gateway (tenant API)
// Test tenant (booking RV seed): Test HKD — KHÔNG đụng tenant thật.
const TENANT = '6aaf19e4-8b55-48cd-94d7-b61c96a2d66d';
const OFFERING = 'fd69fdfc-fad2-4012-a187-ad24a6901939'; // Massage 60 phút — 300.000
const STAFF = '4e2fb124-adc9-467f-a79f-db2ff87bc84e';    // KTV Lan

const results = [];
const errors = [];
function check(name, ok, detail = '') {
  results.push({ name, ok: !!ok, detail });
  const mark = ok ? 'PASS' : 'FAIL';
  console.log(`  [${mark}] ${name}${detail ? ` — ${String(detail).slice(0, 260)}` : ''}`);
  if (!ok) errors.push({ name, detail });
}
async function api(base, method, path, { token, body, headers = {} } = {}) {
  const h = { 'Content-Type': 'application/json', ...headers };
  if (token) h.Authorization = `Bearer ${token}`;
  const res = await fetch(`${base}${path}`, {
    method, headers: h,
    body: body === undefined ? undefined : JSON.stringify(body),
    redirect: 'manual',
  });
  let data = null;
  try { data = await res.json(); } catch { /* non-json */ }
  return { status: res.status, ok: res.ok, data, headers: res.headers };
}
const f = (s) => (s && typeof s === 'object' ? JSON.stringify(s) : String(s)).slice(0, 300);
const asArray = (d) => (Array.isArray(d) ? d : []);

// ── cookie .VanAn.Jwt từ Set-Cookie ─────────────────────────────────────────
function extractJwt(res) {
  const raw = res.headers.getSetCookie?.() ?? [];
  const line = raw.find((c) => c.startsWith('.VanAn.Jwt='));
  if (!line) return null;
  return line.split(';')[0].slice('.VanAn.Jwt='.length);
}

// ═══════════════════════════════════════════════════════════════════════════
console.log('════════════════════════════════════════════════════════════');
console.log('RV SWEEP — booking-payment single-source (production)');
console.log(`Tenant test: ${TENANT} (Test HKD) | Offering: ${OFFERING} | Staff: ${STAFF}`);
console.log('════════════════════════════════════════════════════════════');

// ── cookie jar (browser-like: impersonate yêu cầu COOKIE auth — Bearer → 500 SignInAsync) ──
const jar = new Map();
function absorbCookies(res) {
  for (const c of (res.headers.getSetCookie?.() ?? [])) {
    const eq = c.indexOf('=');
    if (eq <= 0) continue;
    jar.set(c.slice(0, eq).trim(), c.slice(eq + 1).split(';')[0].trim());
  }
}
const cookieHeader = () => [...jar.entries()].map(([n, v]) => `${n}=${v}`).join('; ');

// ── A. AUTH ────────────────────────────────────────────────────────────────
console.log('\n── A. Auth (sysadmin → impersonate Test HKD) ──');
let TOK = null;
{
  const login = await api(APP2, 'POST', '/api/platform/login', {
    body: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
  });
  absorbCookies(login); // auth cookie cho SignInAsync
  check('A1 Login sysadmin → 200 + auth cookie', login.status === 200 && jar.size > 0,
    f({ status: login.status, cookies: jar.size }));

  // Impersonate phải gọi bằng COOKIE (SignInAsync không hỗ trợ Bearer) — token trả trong body.
  const imp = await api(APP2, 'POST', `/api/admin/impersonate/${TENANT}`, { headers: { Cookie: cookieHeader() } });
  absorbCookies(imp);
  TOK = imp.data?.token ?? jar.get('.VanAn.Jwt') ?? null;
  check('A2 Impersonate Test HKD (cookie) → tenant Owner token', imp.status === 200 && !!TOK,
    f({ status: imp.status, hasToken: !!TOK, cookies: jar.size }));
}

// ── B. PUBLIC: catalog + availability (test data sẵn sàng) ─────────────────
console.log('\n── B. Catalog + availability ──');
const TEST_DATE = new Date(Date.now() + 86400e3).toISOString().slice(0, 10); // ngày MAI (đủ slot)
let slot = null;
{
  const cat = await api(GW, 'GET', `/api/public/booking/tenants/${TENANT}/services`);
  check('B1 Catalog Test HKD → 200 + offering Massage 60 + depositPolicy',
    cat.status === 200 && asArray(cat.data?.offerings).some((o) => o.id === OFFERING) && !!cat.data?.depositPolicy,
    f(cat.data?.offerings?.map((o) => o.id)));

  const avail = await api(GW, 'GET', `/api/public/booking/availability?tenantId=${TENANT}&offeringId=${OFFERING}&date=${TEST_DATE}&staffId=${STAFF}`);
  slot = asArray(avail.data).sort((a, b) => a.startAt.localeCompare(b.startAt))[0]?.startAt;
  check('B2 Availability staff → có slot tương lai', avail.status === 200 && !!slot, f(asArray(avail.data).length));
}

// ═══════════════════════════════════════════════════════════════════════════
// C. BOOKING LIFECYCLE → Order hook (D2) — Test HKD
// ═══════════════════════════════════════════════════════════════════════════
console.log('\n── C. Booking lifecycle → Order ──');
let bookingId = null, orderId = null, code = null;
if (TOK && slot) {
  const idem = `rv195b-pay-${Date.now()}`;
  const create = await api(GW, 'POST', '/api/tenant/booking/bookings', {
    token: TOK,
    body: { OfferingId: OFFERING, StartAt: slot, StaffId: STAFF, CustomerName: 'RV Sweep', CustomerPhone: '0900000000', CustomerNote: 'RV booking-payment sweep' },
    headers: { 'Idempotency-Key': idem },
  });
  bookingId = create.data?.bookingId;
  code = create.data?.publicBookingCode;
  check('C1 Create (tenant API, có staff) → 201 StaffAssigned + code',
    create.status === 201 && create.data?.status === 'StaffAssigned' && !!code, f(create.data));

  if (bookingId) {
    const T = (path, body) => api(GW, 'POST', `/api/tenant/booking/bookings/${bookingId}${path}`, { token: TOK, body });
    const ci = await T('/check-in');
    check('C2 Check-in → CheckedIn', ci.status === 200 && ci.data?.status === 'CheckedIn', f(ci.data));
    const st = await T('/start');
    check('C3 Start → InService', st.status === 200 && st.data?.status === 'InService', f(st.data));
    const co = await T('/complete', { actualTotal: 300000 });
    orderId = co.data?.orderId;
    check('C4 Complete → Completed + OrderId (D2 hook)', co.status === 200 && co.data?.status === 'Completed' && !!orderId, f(co.data));
  } else {
    check('C2-C4 (skip — create fail)', false, 'no bookingId');
  }
} else {
  check('C1-C4 (skip — no token/slot)', false, `token=${!!TOK} slot=${slot}`);
}

// ── D. Order sync đúng tenant (SourceType=booking) — chờ NATS ─────────────
console.log('\n── D. Order sync (chờ NATS ~12s) ──');
if (orderId) {
  await new Promise((r) => setTimeout(r, 12000));
  // Order ở ShopERP SQLite + PG — DB check riêng (dbcheck.sh đọc report).
  check('D1 OrderId có để DB-check', true, orderId);
} else {
  check('D1 (skip — no orderId)', false, 'no order');
}

// ── Report ────────────────────────────────────────────────────────────────
writeFileSync(REPORT_PATH, JSON.stringify({
  runAt: new Date().toISOString(), tenantId: TENANT, bookingId, orderId, code,
  results, errors,
}, null, 2));
console.log(`\nReport: ${REPORT_PATH}`);
console.log(`SUMMARY: ${results.filter((r) => r.ok).length}/${results.length} PASS · ${errors.length} FAIL`);
if (errors.length) {
  console.log('\nFAILS:');
  for (const e of errors) console.log(`  ✗ ${e.name} — ${e.detail}`);
}
process.exit(errors.length ? 1 : 0);