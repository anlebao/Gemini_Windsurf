#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV SWEEP — ATRS WAVE 5 W5d (ATRS-13 in-app notifications + metrics) — PRODUCTION
// Env: ATRS_CUSTOMER_TOKEN (X-Customer-Token), ATRS_ADMIN_JWT (SystemAdmin),
//      ATRS_BASE (default https://api2.khachvip.online), ATRS_DIRECT (default :5080)
// Self-cleaning: tạo recovery request → approve → REJECTED cleanup (như waves trước).
// ─────────────────────────────────────────────────────────────────────────────
import fs from 'node:fs';

const BASE = process.env.ATRS_BASE ?? 'https://api2.khachvip.online';
const DIRECT = process.env.ATRS_DIRECT ?? 'http://34.126.90.228:5080';
const CUST = process.env.ATRS_CUSTOMER_TOKEN ?? '';
const ADMIN = process.env.ATRS_ADMIN_JWT ?? '';
const API = `${BASE}/api/v1/account-trust`;

const results = [];
const check = (name, pass, detail = '') => {
  results.push({ name, pass: !!pass });
  console.log(`[${pass ? 'PASS' : 'FAIL'}] ${name}${detail ? ` — ${String(detail).slice(0, 220)}` : ''}`);
};
const info = (name, detail = '') => console.log(`[INFO] ${name}${detail ? ` — ${String(detail).slice(0, 220)}` : ''}`);

async function call(method, path, { token = '', bearer = '', body } = {}) {
  const h = {};
  if (token) h['X-Customer-Token'] = token;
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
  console.log(`ATRS Wave-5 W5d sweep → ${API}`);
  console.log(`cust=${CUST ? 'set' : 'MISSING'} admin=${ADMIN ? 'set' : 'MISSING'}`);
  if (!CUST || !ADMIN) { console.error('ATRS_CUSTOMER_TOKEN + ATRS_ADMIN_JWT required'); process.exit(2); }

  // ── L1: health + metrics (direct :5080) ──────────────────────────────────
  try { const h = await fetch(`${DIRECT}/health`); check('L1 health :5080', h.status === 200, `status=${h.status}`); }
  catch (e) { check('L1 health :5080', false, e.message); }

  try {
    const m = await fetch(`${DIRECT}/metrics`);
    const txt = await m.text();
    const hasCounters = txt.includes('atrs_events_total{event_type="');
    const hasGauges = txt.includes('atrs_recovery_pending ') && txt.includes('atrs_dispute_open ') && txt.includes('atrs_sla_overdue ');
    const numsOk = /atrs_recovery_pending \d+/.test(txt) && /atrs_dispute_open \d+/.test(txt) && /atrs_sla_overdue \d+/.test(txt);
    check('L1 /metrics Prometheus format (counters + gauges)', m.status === 200 && hasCounters && hasGauges && numsOk,
      `status=${m.status} counters=${hasCounters} gauges=${hasGauges} nums=${numsOk}`);
  } catch (e) { check('L1 /metrics', false, e.message); }

  // ── L2: auth-neg notifications ───────────────────────────────────────────
  const noTok = await call('GET', '/notifications', { token: '' });
  check('auth-neg notifications no-token=401', noTok.status === 401, `got ${noTok.status}`);
  const badTok = await call('GET', '/notifications', { token: 'invalid-token-xyz' });
  check('auth-neg notifications bad-token=401', badTok.status === 401, `got ${badTok.status}`);
  const noTokRead = await call('POST', '/notifications/00000000-0000-0000-0000-000000000001/read', { token: '' });
  check('auth-neg notifications/read no-token=401', noTokRead.status === 401, `got ${noTokRead.status}`);

  // ── L3: tạo recovery request → notification "recovery.initiated" ─────────
  const idem = `rv-wave5d-${Date.now()}`;
  const sub = await call('POST', '/recovery-requests', { token: CUST, body: { requestType: 'LOST_DEVICE', idempotencyKey: idem } });
  const rid = sub.json?.request?.id;
  check('customer submit recovery → 200', sub.status === 200 && !!rid, `got ${sub.status}`);

  const list1 = await call('GET', '/notifications?skip=0&take=50', { token: CUST });
  const l1 = list1.json ?? {};
  const initiated = (l1.items ?? []).some(n => n.eventType === 'recovery.initiated');
  check('L3 notification recovery.initiated xuất hiện', list1.status === 200 && initiated,
    `status=${list1.status} total=${l1.total} unread=${l1.unread}`);
  check('L3 notification KHÔNG chứa secret/code', (l1.items ?? []).every(n => !/XXXXX|secret|evidenceSummary/.test(n.body + n.title)),
    'no-secret-check');

  // ── L4: approve → notification "recovery.approved" ───────────────────────
  const appr = await call('POST', `/admin/recovery-requests/${rid}/approve`, { bearer: ADMIN });
  check('L4 admin approve recovery → 200', appr.status === 200, `got ${appr.status}`);

  const list2 = await call('GET', '/notifications?skip=0&take=50', { token: CUST });
  const l2 = list2.json ?? {};
  const approved = (l2.items ?? []).some(n => n.eventType === 'recovery.approved');
  check('L4 notification recovery.approved xuất hiện (acceptance: notify khi recovery đổi trạng thái)',
    list2.status === 200 && approved, `status=${list2.status} events=${(l2.items ?? []).map(n => n.eventType).join(',')}`);

  // ── L5: mark-read + anti-enumeration ─────────────────────────────────────
  const firstUnread = (l2.items ?? []).find(n => !n.isRead);
  if (firstUnread) {
    const read = await call('POST', `/notifications/${firstUnread.id}/read`, { token: CUST });
    check('L5 mark-read → 200', read.status === 200, `got ${read.status}`);
    const after = await call('GET', '/notifications?skip=0&take=50', { token: CUST });
    const stillUnread = (after.json?.items ?? []).find(n => n.id === firstUnread.id)?.isRead ?? false;
    check('L5 sau read → isRead=true + unread giảm', stillUnread && (after.json?.unread ?? 0) < l2.unread,
      `unread ${l2.unread}→${after.json?.unread}`);
  } else {
    info('không có notification unread — skip mark-read', '');
  }

  const otherRead = await call('POST', '/notifications/00000000-0000-0000-0000-0000000000ff/read', { token: CUST });
  check('L5 mark-read id không tồn tại → 404 (anti-enum)', otherRead.status === 404, `got ${otherRead.status}`);

  // ── L6: metrics counters sau triggers ────────────────────────────────────
  try {
    const m = await fetch(`${DIRECT}/metrics`);
    const txt = await m.text();
    const created = /atrs_events_total\{event_type="recovery\.created"\} (\d+)/.exec(txt);
    const approved = /atrs_events_total\{event_type="recovery\.approved"\} (\d+)/.exec(txt);
    check('L6 counter recovery.created ≥1', created !== null && parseInt(created[1]) >= 1, `value=${created?.[1]}`);
    check('L6 counter recovery.approved ≥1', approved !== null && parseInt(approved[1]) >= 1, `value=${approved?.[1]}`);
  } catch (e) { check('L6 metrics counters', false, e.message); }

  // ── Cleanup: reject request MỚI (request approved không reject được — 409 đúng) ──
  const cleanupSub = await call('POST', '/recovery-requests', { token: CUST, body: { requestType: 'OTHER', idempotencyKey: `${idem}-cleanup` } });
  const cleanupId = cleanupSub.json?.request?.id;
  const rej = await call('POST', `/admin/recovery-requests/${cleanupId}/reject`, { bearer: ADMIN, body: { reasonCode: 'rv-wave5d-terminal' } });
  check('cleanup reject → REJECTED', rej.status === 200 && rej.json?.status === 'REJECTED', `got ${rej.status} ${rej.json?.status}`);
  // Reject request APPROVED → 409 (không 500 — guard invalid state).
  const rejApproved = await call('POST', `/admin/recovery-requests/${rid}/reject`, { bearer: ADMIN, body: { reasonCode: 'x' } });
  check('reject request APPROVED → 409 (guard, không 500)', rejApproved.status === 409, `got ${rejApproved.status}`);

  const fails = results.filter(r => !r.pass);
  console.log(`\n==== SWEEP ${fails.length === 0 ? 'ALL PASS' : `${fails.length} FAIL`} (${results.length - fails.length}/${results.length}) ====`);
  fs.writeFileSync('atrs-wave5d-sweep-report.json', JSON.stringify({ results, generatedAt: new Date().toISOString() }, null, 2));
  process.exit(fails.length === 0 ? 0 : 1);
}

main().catch(e => { console.error(e); process.exit(3); });