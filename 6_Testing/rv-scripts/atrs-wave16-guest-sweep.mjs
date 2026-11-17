#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV SWEEP — ATRS-16 guest device identity (buyer không email/SĐT) — PRODUCTION
// Guest: X-Customer-Device-Id (KHÔNG token) → TrustSubject DeviceCluster lazy-create.
// Dùng device id DUY NHẤT mỗi run (subject mới → cap 3/ngày không dính).
// Env: ATRS_ADMIN_JWT, ATRS_BASE, ATRS_DIRECT
// ─────────────────────────────────────────────────────────────────────────────
import fs from 'node:fs';

const BASE = process.env.ATRS_BASE ?? 'https://api2.khachvip.online';
const DIRECT = process.env.ATRS_DIRECT ?? 'http://34.126.90.228:5080';
const ADMIN = process.env.ATRS_ADMIN_JWT ?? '';
const API = `${BASE}/api/v1/account-trust`;
const DEVICE = `rv-guest-${Date.now()}-${Math.floor(Math.random() * 1e6)}`;

const results = [];
const check = (name, pass, detail = '') => {
  results.push({ name, pass: !!pass });
  console.log(`[${pass ? 'PASS' : 'FAIL'}] ${name}${detail ? ` — ${String(detail).slice(0, 220)}` : ''}`);
};
const info = (name, detail = '') => console.log(`[INFO] ${name}${detail ? ` — ${String(detail).slice(0, 220)}` : ''}`);

async function call(method, path, { device = '', bearer = '', body } = {}) {
  const h = {};
  if (device) h['X-Customer-Device-Id'] = device;
  if (bearer) h['Authorization'] = `Bearer ${bearer}`;
  if (body !== undefined) h['Content-Type'] = 'application/json';
  const res = await fetch(`${API}${path}`, {
    method, headers: h,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  let json = null;
  try { json = await res.json(); } catch { /* empty */ }
  return { status: res.status, json };
}

async function main() {
  console.log(`ATRS-16 guest sweep → ${API} (device ${DEVICE.slice(0, 24)}…)`);
  if (!ADMIN) { console.error('ATRS_ADMIN_JWT required'); process.exit(2); }

  try { const h = await fetch(`${DIRECT}/health`); check('L1 health :5080', h.status === 200, `status=${h.status}`); }
  catch (e) { check('L1 health :5080', false, e.message); }

  // ── Regression: không token, không device → 401 ──────────────────────────
  const noAuth = await call('POST', '/recovery-requests', { body: { requestType: 'LOST_DEVICE', idempotencyKey: `x-${Date.now()}` } });
  check('L2 không token + không device → 401', noAuth.status === 401, `got ${noAuth.status}`);

  const badDevice = await call('POST', '/recovery-requests', { device: 'bad id with spaces!!', body: { requestType: 'LOST_DEVICE', idempotencyKey: `x-${Date.now()}` } });
  check('L2 device id format sai → 401', badDevice.status === 401, `got ${badDevice.status}`);

  // ── Guest lifecycle: submit → notification → approve (admin) ─────────────
  const idem = `rv-guest-${Date.now()}`;
  const sub = await call('POST', '/recovery-requests', { device: DEVICE, body: { requestType: 'LOST_DEVICE', idempotencyKey: idem } });
  const rid = sub.json?.request?.id;
  check('L3 guest submit recovery → 200 (không email/SĐT)', sub.status === 200 && !!rid, `got ${sub.status}`);

  const notify = await call('GET', '/notifications?skip=0&take=50', { device: DEVICE });
  const n = notify.json ?? {};
  check('L3 guest notifications → 200 + recovery.initiated', notify.status === 200 && (n.items ?? []).some(x => x.eventType === 'recovery.initiated'),
    `status=${notify.status} total=${n.total}`);

  const mine = await call('GET', '/recovery-requests/mine', { device: DEVICE });
  check('L3 guest recovery mine → 200 + có request', mine.status === 200 && Array.isArray(mine.json) && mine.json.length >= 1, `status=${mine.status}`);

  // Guest không đọc được request của subject khác (anti-enum).
  const otherGet = await call('GET', '/recovery-requests/00000000-0000-0000-0000-0000000000ff', { device: DEVICE });
  check('L3 guest anti-enum request khác → 404', otherGet.status === 404, `got ${otherGet.status}`);

  // Admin approve → guest nhận notification approved.
  const appr = await call('POST', `/admin/recovery-requests/${rid}/approve`, { bearer: ADMIN });
  check('L4 admin approve guest request → 200', appr.status === 200, `got ${appr.status}`);
  const notify2 = await call('GET', '/notifications?skip=0&take=50', { device: DEVICE });
  check('L4 guest nhận notification recovery.approved', (notify2.json?.items ?? []).some(x => x.eventType === 'recovery.approved'),
    `events=${(notify2.json?.items ?? []).map(x => x.eventType).join(',')}`);

  // ── Daily cap: submit tiếp (cap 3/ngày). Anti-duplicate chặn khi còn active → reject giữa các submit. ──
  const cap2 = await call('POST', '/recovery-requests', { device: DEVICE, body: { requestType: 'NO_CREDENTIAL', idempotencyKey: `${idem}-c2` } });
  const cap2Id = cap2.json?.request?.id;
  check('L5 cap: submit 2 → 200', cap2.status === 200 && !!cap2Id, `got ${cap2.status}`);
  if (cap2Id) await call('POST', `/admin/recovery-requests/${cap2Id}/reject`, { bearer: ADMIN, body: { reasonCode: 'rv-cap-clear' } });

  const cap3 = await call('POST', '/recovery-requests', { device: DEVICE, body: { requestType: 'COMPROMISED', idempotencyKey: `${idem}-c3` } });
  const cap3Id = cap3.json?.request?.id;
  check('L5 cap: submit 3 → 200', cap3.status === 200 && !!cap3Id, `got ${cap3.status}`);
  if (cap3Id) await call('POST', `/admin/recovery-requests/${cap3Id}/reject`, { bearer: ADMIN, body: { reasonCode: 'rv-cap-clear' } });

  const cap4 = await call('POST', '/recovery-requests', { device: DEVICE, body: { requestType: 'OTHER', idempotencyKey: `${idem}-c4` } });
  check('L5 cap: submit 4 → 429 (rate-limited)', cap4.status === 429, `got ${cap4.status} ${JSON.stringify(cap4.json ?? '').slice(0, 80)}`);

  // ── Metrics guest ────────────────────────────────────────────────────────
  try {
    const m = await fetch(`${DIRECT}/metrics`);
    const txt = await m.text();
    check('L6 metrics vẫn hợp lệ sau guest flow', m.status === 200 && txt.includes('atrs_events_total{event_type="recovery.created"}'), 'metrics-ok');
  } catch (e) { check('L6 metrics', false, e.message); }

  // ── Cleanup: terminalize các request (reject 2 request còn active) ───────
  for (const id of [cap2.json?.request?.id, cap3.json?.request?.id]) {
    if (id) await call('POST', `/admin/recovery-requests/${id}/reject`, { bearer: ADMIN, body: { reasonCode: 'rv-guest-cleanup' } });
  }
  const after = await call('GET', '/recovery-requests/mine', { device: DEVICE });
  info('cleanup trạng thái', (after.json ?? []).map(r => r.status).join(','));

  const fails = results.filter(r => !r.pass);
  console.log(`\n==== SWEEP ${fails.length === 0 ? 'ALL PASS' : `${fails.length} FAIL`} (${results.length - fails.length}/${results.length}) ====`);
  fs.writeFileSync('atrs-wave16-guest-sweep-report.json', JSON.stringify({ results, device: DEVICE, generatedAt: new Date().toISOString() }, null, 2));
  process.exit(fails.length === 0 ? 0 : 1);
}

main().catch(e => { console.error(e); process.exit(3); });