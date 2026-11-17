#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV SWEEP — ATRS WAVE 3 (dispute vertical + staff RBAC) — PRODUCTION
// Sweep-first: chạy 1 lần thu TOÀN BỘ FAIL, phân tích theo nhóm root cause.
//
// Env:
//   ATRS_CUSTOMER_TOKEN  — customer token (X-Customer-Token)
//   ATRS_ADMIN_JWT       — staff JWT role=SystemAdmin (break-glass)
//   ATRS_STAFF_JWT       — staff JWT không grant (RBAC negative) → sau đó được grant
//   ATRS_BASE            — default https://api2.khachvip.online (Gateway YARP)
//   ATRS_DIRECT          — default http://34.126.90.228:5080 (atrs trực tiếp)
// ─────────────────────────────────────────────────────────────────────────────
import { createHash } from 'node:crypto';
import fs from 'node:fs';

const BASE = process.env.ATRS_BASE ?? 'https://api2.khachvip.online';
const DIRECT = process.env.ATRS_DIRECT ?? 'http://34.126.90.228:5080';
const CUST = process.env.ATRS_CUSTOMER_TOKEN ?? '';
const ADMIN = process.env.ATRS_ADMIN_JWT ?? '';
const STAFF = process.env.ATRS_STAFF_JWT ?? '';
const API = `${BASE}/api/v1/account-trust`;
const TENANT = '00000000-0000-0000-0000-000000000001';
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
  console.log(`ATRS Wave-3 sweep → ${API}`);
  console.log(`cust=${CUST ? 'set' : 'MISSING'} admin=${ADMIN ? 'set' : 'MISSING'} staff=${STAFF ? 'set' : 'MISSING'}`);
  if (!CUST || !ADMIN || !STAFF) { console.error('ATRS_CUSTOMER_TOKEN + ATRS_ADMIN_JWT + ATRS_STAFF_JWT required'); process.exit(2); }

  // ── L1: Health + auth negatives ──────────────────────────────────────────
  try {
    const h = await fetch(`${DIRECT}/health`);
    check('L1 health direct :5080', h.status === 200, `status=${h.status}`);
  } catch (e) { check('L1 health direct :5080', false, e.message); }

  for (const [label, method, path, body] of [
    ['disputes-list', 'GET', '/disputes', undefined],
    ['disputes-submit', 'POST', '/disputes', { caseType: 'POINTS_MISSING', claimedIssue: 'auth-neg probe', idempotencyKey: `rv-authneg-${Date.now()}` }],
    ['me', 'GET', '/me', undefined],
    ['transactions', 'GET', `/transactions?tenantId=${TENANT}`, undefined],
  ]) {
    const noTok = await call(method, path, { token: '', body });
    check(`auth-neg ${label} no-token=401`, noTok.status === 401, `got ${noTok.status}`);
    const badTok = await call(method, path, { token: 'invalid-token-xyz', body });
    check(`auth-neg ${label} bad-token=401`, badTok.status === 401, `got ${badTok.status}`);
  }

  const me = await call('GET', '/me');
  check('GET /me 200', me.status === 200, `status=${me.status}`);
  const subjectId = me.json?.trustSubjectId;
  info('trustSubjectId', subjectId);

  // ── L2: Customer dispute lifecycle ───────────────────────────────────────
  const idemKey = `rv-wave3-${Date.now()}`;
  const submitBody = {
    caseType: 'POINTS_MISSING',
    claimedIssue: 'RV Wave 3 — điểm cộng thiếu sau đơn hàng test',
    tenantId: TENANT,
    relatedTransactionIds: ['rv-tx-001'],
    idempotencyKey: idemKey,
  };
  const sub = await call('POST', '/disputes', { body: submitBody });
  check('submit dispute → 200', sub.status === 200, `got ${sub.status} ${JSON.stringify(sub.json ?? '').slice(0, 160)}`);
  const caseId = sub.json?.case?.id ?? sub.json?.id;
  check('submit returns case.id', !!caseId, `id=${caseId}`);

  const subReplay = await call('POST', '/disputes', { body: submitBody });
  const replayExisting = subReplay.json?.existing === true || subReplay.json?.case?.id === caseId;
  check('idempotent replay → existing/same-id', subReplay.status === 200 && replayExisting, `got ${subReplay.status} existing=${subReplay.json?.existing}`);

  const list = await call('GET', '/disputes');
  check('GET /disputes 200 array', list.status === 200 && Array.isArray(list.json), `status=${list.status}`);
  const inList = Array.isArray(list.json) && list.json.some(c => (c.id ?? c.caseId) === caseId);
  check('list contains submitted case', inList, `count=${Array.isArray(list.json) ? list.json.length : '?'}`);

  if (caseId) {
    const detail = await call('GET', `/disputes/${caseId}`);
    check('GET dispute detail 200', detail.status === 200, `got ${detail.status}`);
    const dJson = JSON.stringify(detail.json ?? {});
    check('user detail user-safe (no internalRationale/accessClassification)',
      !dJson.includes('internalRationale') && !dJson.includes('accessClassification') && !dJson.includes('reliabilityClass'),
      'leak-check');

    const fakeGet = await call('GET', '/disputes/00000000-0000-0000-0000-0000000000ff');
    check('GET fake dispute → 404 (anti-enum)', fakeGet.status === 404, `got ${fakeGet.status}`);

    const hash = createHash('sha256').update(`rv-evidence-${Date.now()}`).digest('hex');
    const ev = await call('POST', `/disputes/${caseId}/evidence`, {
      body: { evidenceType: 'SCREENSHOT', sourceSystem: 'rv-sweep', sourceReference: 'rv-001', integrityHash: hash, description: 'RV evidence', accessClassification: 'USER_VISIBLE' },
    });
    check('add evidence → 200', ev.status === 200, `got ${ev.status} ${JSON.stringify(ev.json ?? '').slice(0, 140)}`);

    const evBadHash = await call('POST', `/disputes/${caseId}/evidence`, {
      body: { evidenceType: 'SCREENSHOT', sourceSystem: 'rv', integrityHash: 'not-a-hash' },
    });
    check('evidence bad hash → 400 not 500', [400, 422].includes(evBadHash.status), `got ${evBadHash.status}`);
  }

  const txs = await call('GET', `/transactions?tenantId=${TENANT}`);
  check('GET /transactions 200 array (fail-open rỗng OK)', txs.status === 200 && Array.isArray(txs.json), `status=${txs.status} n=${Array.isArray(txs.json) ? txs.json.length : '?'}`);
  const txsNoTenant = await call('GET', '/transactions');
  check('GET /transactions missing tenantId → 400', txsNoTenant.status === 400, `got ${txsNoTenant.status}`);

  // Withdraw flow — case thứ 2 (không đụng case chính)
  const sub2 = await call('POST', '/disputes', {
    body: { caseType: 'OWNERSHIP_DISPUTE', claimedIssue: 'RV wave3 withdraw test', tenantId: TENANT, idempotencyKey: `rv-wave3-w-${Date.now()}` },
  });
  const case2 = sub2.json?.case?.id ?? sub2.json?.id;
  if (case2) {
    const wd = await call('POST', `/disputes/${case2}/withdraw`);
    check('withdraw own case → 200', wd.status === 200, `got ${wd.status}`);
    const wdReplay = await call('POST', `/disputes/${case2}/withdraw`);
    check('withdraw replay → 404/200 (idempotent-ish)', [200, 404].includes(wdReplay.status), `got ${wdReplay.status}`);
  } else {
    check('withdraw own case → 200', false, `submit2 failed status=${sub2.status}`);
  }

  // ── L3: Staff RBAC negatives (STAFF chưa có grant) ───────────────────────
  const staffQueueDeny = await staff('GET', '/admin/disputes', STAFF);
  check('staff no-grant queue → 403', staffQueueDeny.status === 403, `got ${staffQueueDeny.status}`);
  const staffPermListDeny = await staff('GET', '/admin/permissions', STAFF);
  check('staff no-grant permissions list → 403', staffPermListDeny.status === 403, `got ${staffPermListDeny.status}`);
  const staffGrantDeny = await staff('POST', '/admin/permissions', STAFF, { demoUserId: STAFF_B, permission: 'dispute.view' });
  check('staff grant attempt → 403 (SystemAdmin only)', staffGrantDeny.status === 403, `got ${staffGrantDeny.status}`);
  const staffProposeDeny = caseId ? await staff('POST', `/admin/disputes/${caseId}/decisions`, STAFF, {
    decision: 'NO_ACTION', decisionReason: 'test', policyVersion: 'atrs-dispute-v1',
  }) : { status: 0 };
  check('staff no-grant propose → 403', staffProposeDeny.status === 403, `got ${staffProposeDeny.status}`);

  // ── L4: Grant management (SystemAdmin) ───────────────────────────────────
  const permList = await staff('GET', '/admin/permissions', ADMIN);
  check('admin permissions list → 200', permList.status === 200 && Array.isArray(permList.json), `status=${permList.status}`);

  const grants = ['dispute.view', 'dispute.investigate', 'dispute.propose_adjustment'];
  const grantIds = [];
  for (const p of grants) {
    const g = await staff('POST', '/admin/permissions', ADMIN, { demoUserId: STAFF_B, permission: p });
    check(`grant ${p} → 200`, g.status === 200, `got ${g.status} ${JSON.stringify(g.json ?? '').slice(0, 120)}`);
    if (g.json?.id) grantIds.push({ id: g.json.id, permission: p });
  }
  const badGrant = await staff('POST', '/admin/permissions', ADMIN, { demoUserId: STAFF_B, permission: 'not.a.permission' });
  check('grant invalid permission → 400', badGrant.status === 400, `got ${badGrant.status}`);

  // ── L5: Staff queue + investigation (STAFF giờ có grant) ─────────────────
  const staffQueue = await staff('GET', '/admin/disputes', STAFF);
  check('staff granted queue → 200', staffQueue.status === 200 && Array.isArray(staffQueue.json), `status=${staffQueue.status}`);

  if (caseId) {
    const staffDetail = await staff('GET', `/admin/disputes/${caseId}`, STAFF);
    check('staff detail → 200', staffDetail.status === 200, `got ${staffDetail.status}`);

    const assign = await staff('POST', `/admin/disputes/${caseId}/assign`, ADMIN, { staffUserId: STAFF_B });
    check('assign case → 200', assign.status === 200, `got ${assign.status} ${JSON.stringify(assign.json ?? '').slice(0, 140)}`);

    const transition = await staff('POST', `/admin/disputes/${caseId}/transition`, STAFF, { status: 'UNDER_REVIEW', note: 'RV wave3 investigate' });
    check('transition UNDER_REVIEW → 200', transition.status === 200, `got ${transition.status} ${JSON.stringify(transition.json ?? '').slice(0, 140)}`);

    // Maker-checker self-approval: ADMIN propose → ADMIN approve phải 403
    const adminPropose = await staff('POST', `/admin/disputes/${caseId}/decisions`, ADMIN, {
      decision: 'NO_ACTION', decisionReason: 'RV self-approval probe', userFacingExplanation: 'probe',
      policyVersion: 'atrs-dispute-v1',
    });
    info('admin propose NO_ACTION', `status=${adminPropose.status} id=${adminPropose.json?.id ?? adminPropose.json?.decisionId ?? '?'}`);
    const d0 = adminPropose.json?.id ?? adminPropose.json?.decisionId;
    if (d0) {
      const selfApprove = await staff('POST', `/admin/disputes/decisions/${d0}/approve`, ADMIN);
      check('self-approval (proposer==approver) → 403', selfApprove.status === 403, `got ${selfApprove.status} ${JSON.stringify(selfApprove.json ?? '').slice(0, 120)}`);
      const rejectD0 = await staff('POST', `/admin/disputes/decisions/${d0}/reject`, ADMIN, { rejectionReason: 'RV probe — thay bằng proposal của investigator' });
      info('reject probe decision', `status=${rejectD0.status}`);
    }

    // Investigator (STAFF) propose adjustment decision
    const propose = await staff('POST', `/admin/disputes/${caseId}/decisions`, STAFF, {
      decision: 'APPROVED',
      decisionReason: 'RV wave3 — cộng bù điểm thiếu',
      userFacingExplanation: 'Chúng tôi xác nhận thiếu điểm và sẽ cộng bù.',
      internalRationale: 'RV internal',
      policyVersion: 'atrs-dispute-v1',
      proposedAdjustmentPoints: 100,
      proposedAdjustmentTenantId: TENANT,
      proposedAdjustmentReason: 'RV wave3 adjustment',
    });
    check('staff propose adjustment → 200', propose.status === 200, `got ${propose.status} ${JSON.stringify(propose.json ?? '').slice(0, 200)}`);
    const decisionId = propose.json?.id ?? propose.json?.decisionId;

    if (decisionId) {
      // STAFF không có approve_adjustment → approve phải 403
      const staffApproveDeny = await staff('POST', `/admin/disputes/decisions/${decisionId}/approve`, STAFF);
      check('staff no-approve-perm approve → 403', staffApproveDeny.status === 403, `got ${staffApproveDeny.status}`);

      // ADMIN approve → ledger adjust → EXECUTED + RESOLVED (hoặc FAILED fail-closed)
      const approve = await staff('POST', `/admin/disputes/decisions/${decisionId}/approve`, ADMIN);
      check('admin approve → 200', approve.status === 200, `got ${approve.status} ${JSON.stringify(approve.json ?? '').slice(0, 240)}`);
      const decStatus = approve.json?.status ?? approve.json?.decisionStatus;
      const resolutionRef = approve.json?.resolutionReference ?? approve.json?.ledgerReference;
      info('decision outcome', `status=${decStatus} resolutionRef=${resolutionRef}`);

      const finalDetail = await staff('GET', `/admin/disputes/${caseId}`, ADMIN);
      const caseStatus = finalDetail.json?.status ?? finalDetail.json?.case?.status;
      info('final case status', `status=${caseStatus}`);
      check('case RESOLVED sau ledger confirm (hoặc FAILED→UNDER_REVIEW documented)',
        ['RESOLVED', 'UNDER_REVIEW', 'DECIDED'].includes(String(caseStatus)), `status=${caseStatus}`);
      if (String(caseStatus) === 'RESOLVED') {
        check('adjustment executed có resolutionReference', !!resolutionRef || JSON.stringify(finalDetail.json).includes('resolutionReference') || JSON.stringify(finalDetail.json).includes('executed'), 'ref-check');
      }
    }
  }

  // ── L6: Recovery queue (ATRS-10 admin surface regression) ────────────────
  const recQueue = await staff('GET', '/admin/recovery-requests', ADMIN);
  check('admin recovery queue → 200', [200].includes(recQueue.status), `status=${recQueue.status}`);

  // ── L7: Revoke grants (self-cleaning → sweep re-runnable) ────────────────
  for (const { id, permission } of grantIds) {
    const rv = await staff('POST', `/admin/permissions/${id}/revoke`, ADMIN);
    check(`revoke ${permission} → 200`, rv.status === 200, `got ${rv.status}`);
  }
  const staffQueueAfterRevoke = await staff('GET', '/admin/disputes', STAFF);
  check('staff queue sau revoke → 403 lại', staffQueueAfterRevoke.status === 403, `got ${staffQueueAfterRevoke.status}`);

  // ── Summary ──────────────────────────────────────────────────────────────
  const pass = results.filter(r => r.pass).length;
  const fail = results.filter(r => !r.pass);
  console.log(`\n═══ RESULT: ${pass}/${results.length} PASS ═══`);
  if (fail.length) {
    console.log('FAILURES:');
    fail.forEach(f => console.log(`  - ${f.name}`));
  }
  const report = { ts: new Date().toISOString(), base: API, total: results.length, pass, fail: fail.map(f => f.name), results };
  fs.writeFileSync('atrs-wave3-sweep-report.json', JSON.stringify(report, null, 2));
  console.log('report → atrs-wave3-sweep-report.json');
  process.exit(fail.length ? 1 : 0);
}

main().catch(e => { console.error('FATAL', e); process.exit(2); });
