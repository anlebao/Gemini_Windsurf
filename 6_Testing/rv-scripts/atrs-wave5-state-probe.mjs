#!/usr/bin/env node
// Probe: A16 escalate → return-to-review happy path live (prod) + notes audit trail.
// Reuses customer token to create fresh request → drive state machine qua staff APIs.
const API = 'https://api2.khachvip.online/api/v1/account-trust';
const CUST = process.env.ATRS_CUSTOMER_TOKEN ?? '';
const ADMIN = process.env.ATRS_ADMIN_JWT ?? '';
const results = [];
const check = (name, pass, detail = '') => { results.push({ name, pass: !!pass }); console.log(`[${pass ? 'PASS' : 'FAIL'}] ${name}${detail ? ` — ${String(detail).slice(0, 180)}` : ''}`); };
const info = (m) => console.log(`[INFO] ${m}`);
const call = async (method, path, { token = '', bearer = '', body } = {}) => {
  const h = {};
  if (token) h['X-Customer-Token'] = token;
  if (bearer) h['Authorization'] = `Bearer ${bearer}`;
  if (body !== undefined) h['Content-Type'] = 'application/json';
  const res = await fetch(`${API}${path}`, { method, headers: h, body: body === undefined ? undefined : JSON.stringify(body) });
  let json = null; try { json = await res.json(); } catch { }
  return { status: res.status, json };
};

(async () => {
  if (!CUST || !ADMIN) { console.error('tokens missing'); process.exit(2); }
  const idem = `rv-wave5b-${Date.now()}`;
  const sub = await call('POST', '/recovery-requests', { token: CUST, body: { requestType: 'COMPROMISED', idempotencyKey: idem } });
  const rid = sub.json?.request?.id;
  info(`created ${rid} status=${sub.json?.request?.status}`);
  check('submit fresh → 200', sub.status === 200 && !!rid, `got ${sub.status}`);

  // bad code → AUTOMATED_CHECK
  const vc = await call('POST', `/recovery-requests/${rid}/verify-code`, { token: CUST, body: { code: 'WRONG-CODE' } });
  check('verify bad code → 400 (AUTOMATED_CHECK)', vc.status === 400, `got ${vc.status}`);

  // request-info → PENDING_USER_ACTION (staff)
  const ri = await call('POST', `/admin/recovery-requests/${rid}/request-info`, { bearer: ADMIN });
  check('request-info → PENDING_USER_ACTION', ri.status === 200 && ri.json?.status === 'PENDING_USER_ACTION', `got ${ri.status} ${ri.json?.status}`);

  // note + recommend (A16)
  const n1 = await call('POST', `/admin/recovery-requests/${rid}/notes`, { bearer: ADMIN, body: { body: 'RV probe — note nội bộ', noteType: 'INTERNAL_NOTE' } });
  check('add note → 200', n1.status === 200, `got ${n1.status}`);
  const n2 = await call('POST', `/admin/recovery-requests/${rid}/notes`, { bearer: ADMIN, body: { body: 'RV probe — khuyến nghị duyệt', noteType: 'RECOMMEND_APPROVE' } });
  check('recommend approve → 200', n2.status === 200 && n2.json?.noteType === 'RECOMMEND_APPROVE', `got ${n2.status}`);

  // escalate → ESCALATED
  const esc = await call('POST', `/admin/recovery-requests/${rid}/escalate`, { bearer: ADMIN });
  check('escalate → ESCALATED', esc.status === 200 && esc.json?.status === 'ESCALATED', `got ${esc.status} ${esc.json?.status}`);

  // return-to-review → PENDING_REVIEW
  const ret = await call('POST', `/admin/recovery-requests/${rid}/return-to-review`, { bearer: ADMIN });
  check('return-to-review → PENDING_REVIEW', ret.status === 200 && ret.json?.status === 'PENDING_REVIEW', `got ${ret.status} ${ret.json?.status}`);

  // reject (terminal cleanup) → REJECTED
  const rej = await call('POST', `/admin/recovery-requests/${rid}/reject`, { bearer: ADMIN, body: { reasonCode: 'rv-wave5b-terminal' } });
  check('reject → REJECTED (cleanup)', rej.status === 200 && rej.json?.status === 'REJECTED', `got ${rej.status} ${rej.json?.status}`);

  // detail: notes + events timeline có staff audit
  const det = await call('GET', `/admin/recovery-requests/${rid}`, { bearer: ADMIN });
  const dj = det.json ?? {};
  const noteTypes = (dj.notes ?? []).map(n => n.noteType);
  const eventTypes = (dj.recentEvents ?? []).map(e => e.eventType);
  check('detail notes ≥1', noteTypes.length >= 1, `notes=${noteTypes.join(',')}`);
  check('audit timeline có staff events', ['recovery.staff_view', 'recovery.info_requested', 'recovery.escalated', 'recovery.deescalated', 'recovery.rejected'].some(t => eventTypes.includes(t)), `events=${eventTypes.join(',')}`);

  const fails = results.filter(r => !r.pass);
  console.log(`\n==== PROBE ${fails.length === 0 ? 'ALL PASS' : `${fails.length} FAIL`} (${results.length - fails.length}/${results.length}) ====`);
  process.exit(fails.length ? 1 : 0);
})().catch(e => { console.error(e); process.exit(3); });