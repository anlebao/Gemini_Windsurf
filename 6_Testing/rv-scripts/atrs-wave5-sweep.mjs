#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV SWEEP — ATRS WAVE 5 W5a (recovery staff vertical A14/A15/A16) — PRODUCTION
// Sweep-first: 1 run thu TOÀN BỘ FAIL → phân tích theo nhóm root cause.
//
// Env:
//   ATRS_CUSTOMER_TOKEN  — customer token (X-Customer-Token) — OPTIONAL (tạo request mới)
//   ATRS_ADMIN_JWT       — staff JWT role=SystemAdmin (break-glass)
//   ATRS_STAFF_JWT       — staff JWT không grant (RBAC negative) → sau đó được grant
//   ATRS_BASE            — default https://api2.khachvip.online (Gateway YARP)
//   ATRS_DIRECT          — default http://34.126.90.228:5080 (atrs trực tiếp)
// ─────────────────────────────────────────────────────────────────────────────
import fs from 'node:fs';

const BASE = process.env.ATRS_BASE ?? 'https://api2.khachvip.online';
const DIRECT = process.env.ATRS_DIRECT ?? 'http://34.126.90.228:5080';
const CUST = process.env.ATRS_CUSTOMER_TOKEN ?? '';
const ADMIN = process.env.ATRS_ADMIN_JWT ?? '';
const STAFF = process.env.ATRS_STAFF_JWT ?? '';
const API = `${BASE}/api/v1/account-trust`;
const STAFF_B = 'bbbbbbbb-0000-4000-8000-0000000000b2';

const results = [];
const check = (name, pass, detail = '') => {
  results.push({ name, pass: !!pass });
  console.log(`[${pass ? 'PASS' : 'FAIL'}] ${name}${detail ? ` — ${String(detail).slice(0, 220)}` : ''}`);
};
const info = (name, detail = '') => console.log(`[INFO] ${name}${detail ? ` — ${String(detail).slice(0, 220)}` : ''}`);

async function call(method, path, { token = CUST, bearer = null, body } = {}) {
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
const staff = (method, path, jwt, body) => call(method, path, { token: '', bearer: jwt, body });

async function main() {
  console.log(`ATRS Wave-5 W5a sweep → ${API}`);
  console.log(`cust=${CUST ? 'set' : 'MISSING'} admin=${ADMIN ? 'set' : 'MISSING'} staff=${STAFF ? 'set' : 'MISSING'}`);
  if (!ADMIN || !STAFF) { console.error('ATRS_ADMIN_JWT + ATRS_STAFF_JWT required'); process.exit(2); }

  // ── L1: Health + auth negatives ──────────────────────────────────────────
  try {
    const h = await fetch(`${DIRECT}/health`);
    check('L1 health direct :5080', h.status === 200, `status=${h.status}`);
  } catch (e) { check('L1 health direct :5080', false, e.message); }

  for (const [label, method, path, body] of [
    ['recovery-queue', 'GET', '/admin/recovery-requests', undefined],
    ['recovery-detail', 'GET', '/admin/recovery-requests/00000000-0000-0000-0000-000000000001', undefined],
    ['dashboard', 'GET', '/admin/dashboard', undefined],
    ['disputes-list', 'GET', '/admin/disputes', undefined],
  ]) {
    const noTok = await call(method, path, { token: '', body });
    check(`auth-neg ${label} no-token=401`, noTok.status === 401, `got ${noTok.status}`);
    const badTok = await call(method, path, { token: 'invalid-token-xyz', body });
    check(`auth-neg ${label} bad-token=401`, badTok.status === 401, `got ${badTok.status}`);
  }

  // ── L2: RBAC negative — STAFF không grant → 403 ─────────────────────────
  const staffDeny = [
    ['recovery-queue', 'GET', '/admin/recovery-requests'],
    ['recovery-detail', 'GET', '/admin/recovery-requests/00000000-0000-0000-0000-000000000001'],
    ['dashboard', 'GET', '/admin/dashboard'],
    ['assign', 'POST', '/admin/recovery-requests/00000000-0000-0000-0000-000000000001/assign', { staffUserId: STAFF_B }],
    ['note', 'POST', '/admin/recovery-requests/00000000-0000-0000-0000-000000000001/notes', { body: 'x', noteType: 'INTERNAL_NOTE' }],
    ['approve', 'POST', '/admin/recovery-requests/00000000-0000-0000-0000-000000000001/approve'],
  ];
  for (const [label, method, path, body] of staffDeny) {
    const r = await staff(method, path, STAFF, body);
    check(`rbac staff-no-grant ${label}=403`, r.status === 403, `got ${r.status}`);
  }

  // ── L3: ADMIN dashboard (A14) ────────────────────────────────────────────
  const dash = await staff('GET', '/admin/dashboard', ADMIN);
  const d = dash.json ?? {};
  const dashOk = dash.status === 200
    && typeof d.newRecovery24h === 'number'
    && typeof d.awaitingReview === 'number'
    && typeof d.escalated === 'number'
    && typeof d.needsInfo === 'number'
    && typeof d.openDisputes === 'number'
    && typeof d.slaOverdue === 'number'
    && typeof d.recentSecurityIncidents24h === 'number';
  check('L3 dashboard 200 + 7 widgets numeric', dashOk, `status=${dash.status} ${JSON.stringify(d)}`.slice(0, 240));

  // ── L4: Recovery queue paged shape (A15) ─────────────────────────────────
  const q = await staff('GET', '/admin/recovery-requests?skip=0&take=50', ADMIN);
  const qj = q.json ?? {};
  const qOk = q.status === 200 && Array.isArray(qj.items) && typeof qj.total === 'number'
    && typeof qj.skip === 'number' && typeof qj.take === 'number';
  check('L4 queue page shape (items/total/skip/take)', qOk, `status=${q.status} total=${qj.total}`);
  check('L4 queue item SLA fields (ageHours/slaBreached)',
    qOk && qj.items.every(i => typeof i.ageHours === 'number' && typeof i.slaBreached === 'boolean'),
    `items=${qj.items?.length}`);

  // ── L5: Recovery detail + actions (A16) ──────────────────────────────────
  let targetId = null;
  if (CUST) {
    // Tạo request MỚI qua customer path (dead-write-path lesson) — deterministic.
    const sub = await call('POST', '/recovery-requests', {
      body: { requestType: 'LOST_DEVICE', idempotencyKey: `rv-wave5-${Date.now()}`, newDeviceId: null, evidenceSummaryJson: '{"note":"RV W5a sweep"}' },
    });
    check('customer submit recovery → 200', sub.status === 200, `got ${sub.status} ${JSON.stringify(sub.json ?? '').slice(0, 140)}`);
    targetId = sub.json?.request?.id ?? sub.json?.id;
  }
  if (!targetId) {
    // Fallback: request mở đầu tiên trong queue (nếu có).
    const first = (qj.items ?? []).find(i => !['APPROVED', 'REJECTED', 'CANCELLED', 'EXPIRED', 'COMPLETED'].includes(i.status));
    if (first) { targetId = first.id; info('dùng request có sẵn', targetId); }
  }

  if (targetId) {
    const det = await staff('GET', `/admin/recovery-requests/${targetId}`, ADMIN);
    const dj = det.json ?? {};
    const detOk = det.status === 200
      && Array.isArray(dj.notes) && Array.isArray(dj.devices) && Array.isArray(dj.subjectLinks) && Array.isArray(dj.recentEvents);
    check('L5 staff detail 200 + sections arrays', detOk, `status=${det.status}`);
    const detStr = JSON.stringify(dj);
    check('L5 detail KHÔNG leak evidence payload (EvidenceSummaryJson)',
      detOk && !detStr.includes('EvidenceSummaryJson') && !detStr.toLowerCase().includes('secretanswer'),
      'leak-check');
    if (dj.risk) {
      check('L5 detail risk policy version present', !!dj.risk.policyVersion, `version=${dj.risk.policyVersion}`);
      check('L5 detail risk reasonCodes array', Array.isArray(dj.risk.reasonCodes), `codes=${JSON.stringify(dj.risk.reasonCodes)}`);
    } else {
      info('L5 detail risk null (chưa verify code) — skip policy check', targetId);
    }

    // Actions theo state hiện tại — re-runnable, skip hợp lệ ghi INFO.
    const assign = await staff('POST', `/admin/recovery-requests/${targetId}/assign`, ADMIN, { staffUserId: STAFF_B });
    check('L5 assign → 200 + assignedReviewerId', assign.status === 200 && assign.json?.assignedReviewerId === STAFF_B, `got ${assign.status}`);

    const note = await staff('POST', `/admin/recovery-requests/${targetId}/notes`, ADMIN, { body: 'RV W5a — ghi chú nội bộ', noteType: 'INTERNAL_NOTE' });
    check('L5 add note → 200 + noteType', note.status === 200 && note.json?.noteType === 'INTERNAL_NOTE', `got ${note.status}`);
    const rec = await staff('POST', `/admin/recovery-requests/${targetId}/notes`, ADMIN, { body: 'RV W5a — khuyến nghị duyệt', noteType: 'RECOMMEND_APPROVE' });
    check('L5 recommend approve → 200', rec.status === 200 && rec.json?.noteType === 'RECOMMEND_APPROVE', `got ${rec.status}`);

    const st = assign.json?.status ?? (await staff('GET', `/admin/recovery-requests/${targetId}`, ADMIN)).json?.status;
    info('status before escalate/review actions', st);

    const infoReq = await staff('POST', `/admin/recovery-requests/${targetId}/request-info`, ADMIN);
    check('L5 request-info → 200/409 (state-dependent)', [200, 409].includes(infoReq.status), `got ${infoReq.status}`);

    const esc = await staff('POST', `/admin/recovery-requests/${targetId}/escalate`, ADMIN);
    check('L5 escalate → 200/409 (state-dependent)', [200, 409].includes(esc.status), `got ${esc.status}${esc.status === 200 ? ' → ' + esc.json?.status : ''}`);

    const ret = await staff('POST', `/admin/recovery-requests/${targetId}/return-to-review`, ADMIN);
    check('L5 return-to-review → 200/409 (state-dependent)', [200, 409].includes(ret.status), `got ${ret.status}`);

    const rej = await staff('POST', `/admin/recovery-requests/${targetId}/reject`, ADMIN, { reasonCode: 'rv-sweep-terminal' });
    check('L5 reject → 200 (terminal cleanup)', rej.status === 200 && rej.json?.status === 'REJECTED', `got ${rej.status} ${rej.json?.status}`);
    check('L5 reject thiếu reasonCode → 400', (await staff('POST', `/admin/recovery-requests/${targetId}/reject`, ADMIN, {})).status === 400, 'bad-request-check');
  } else {
    info('Không có request để test actions — cần ATRS_CUSTOMER_TOKEN', 'skip L5 actions');
  }

  // ── L6: Grant flow — STAFF được grant recovery.view → 200, revoke → 403 ──
  for (const p of ['recovery.view', 'recovery.review', 'recovery.approve', 'dispute.view']) {
    const g = await staff('POST', '/admin/permissions', ADMIN, { demoUserId: STAFF_B, permission: p });
    check(`grant ${p} → 200`, g.status === 200, `got ${g.status}`);
  }
  const badGrant = await staff('POST', '/admin/permissions', ADMIN, { demoUserId: STAFF_B, permission: 'not.a.permission' });
  check('grant invalid permission → 400', badGrant.status === 400, `got ${badGrant.status}`);

  const staffQueue = await staff('GET', '/admin/recovery-requests', STAFF);
  check('L6 staff-granted queue → 200', staffQueue.status === 200, `got ${staffQueue.status}`);
  const staffDash = await staff('GET', '/admin/dashboard', STAFF);
  check('L6 staff-granted dashboard → 200', staffDash.status === 200, `got ${staffDash.status}`);

  // Cleanup grants (self-cleaning — revoke hết).
  const grants = await staff('GET', '/admin/permissions?demoUserId=' + STAFF_B, ADMIN);
  for (const g of (grants.json ?? [])) {
    if (g.isRevoked) continue;
    const rv = await staff('POST', `/admin/permissions/${g.id}/revoke`, ADMIN);
    check(`revoke grant ${g.permission} → 200`, rv.status === 200, `got ${rv.status}`);
  }
  const afterRevoke = await staff('GET', '/admin/recovery-requests', STAFF);
  check('L6 staff queue sau revoke → 403', afterRevoke.status === 403, `got ${afterRevoke.status}`);

  // ── Summary ──────────────────────────────────────────────────────────────
  const fails = results.filter(r => !r.pass);
  console.log(`\n==== SWEEP ${fails.length === 0 ? 'ALL PASS' : `${fails.length} FAIL`} (${results.length - fails.length}/${results.length}) ====`);
  fs.writeFileSync('atrs-wave5-sweep-report.json', JSON.stringify({ results, generatedAt: new Date().toISOString() }, null, 2));
  process.exit(fails.length === 0 ? 0 : 1);
}

main().catch(e => { console.error(e); process.exit(3); });