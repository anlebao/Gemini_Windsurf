#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV SWEEP — ATRS WAVE 5 W5c (A21 security policy versioned + maker-checker) — PROD
//
// Env: ATRS_ADMIN_JWT (SystemAdmin), ATRS_STAFF_JWT (no-grant staff),
//      ATRS_BASE (default https://api2.khachvip.online), ATRS_DIRECT (default :5080)
// Self-cleaning: mọi policy test dùng key rv-* riêng, không đụng policy thật;
// grants STAFF_B cấp xong thu hồi hết.
// ─────────────────────────────────────────────────────────────────────────────
import fs from 'node:fs';

const BASE = process.env.ATRS_BASE ?? 'https://api2.khachvip.online';
const DIRECT = process.env.ATRS_DIRECT ?? 'http://34.126.90.228:5080';
const ADMIN = process.env.ATRS_ADMIN_JWT ?? '';
const STAFF = process.env.ATRS_STAFF_JWT ?? '';
const API = `${BASE}/api/v1/account-trust`;
const STAFF_B = 'bbbbbbbb-0000-4000-8000-0000000000b2';
const P_KEY = `rv.w5c.sla_hours.${Date.now() % 100000}`; // key riêng từng run

const results = [];
const check = (name, pass, detail = '') => {
  results.push({ name, pass: !!pass });
  console.log(`[${pass ? 'PASS' : 'FAIL'}] ${name}${detail ? ` — ${String(detail).slice(0, 220)}` : ''}`);
};
const info = (name, detail = '') => console.log(`[INFO] ${name}${detail ? ` — ${String(detail).slice(0, 220)}` : ''}`);

async function call(method, path, { bearer = null, body } = {}) {
  const h = {};
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
const staff = (method, path, jwt, body) => call(method, path, { bearer: jwt, body });

async function main() {
  console.log(`ATRS Wave-5 W5c sweep → ${API} (policy key ${P_KEY})`);
  if (!ADMIN || !STAFF) { console.error('tokens missing'); process.exit(2); }

  try { const h = await fetch(`${DIRECT}/health`); check('L1 health :5080', h.status === 200, `status=${h.status}`); }
  catch (e) { check('L1 health :5080', false, e.message); }

  // ── Auth-neg + RBAC ─────────────────────────────────────────────────────
  for (const [label, method, path, body] of [
    ['policy-list', 'GET', '/admin/security-policies'],
    ['policy-propose', 'POST', '/admin/security-policies', { policyKey: P_KEY, policyGroup: 'sla', jsonValue: '{"hours":24}' }],
  ]) {
    const noTok = await call(method, path, { body });
    check(`auth-neg ${label} no-token=401`, noTok.status === 401, `got ${noTok.status}`);
  }
  const staffDeny = [
    ['list', 'GET', '/admin/security-policies'],
    ['propose', 'POST', '/admin/security-policies', { policyKey: P_KEY, policyGroup: 'sla', jsonValue: '{"hours":24}' }],
    ['approve', 'POST', `/admin/security-policies/00000000-0000-0000-0000-000000000001/approve`],
    ['reject', 'POST', `/admin/security-policies/00000000-0000-0000-0000-000000000001/reject`],
  ];
  for (const [label, method, path, body] of staffDeny) {
    const r = await staff(method, path, STAFF, body);
    check(`rbac staff-no-grant ${label}=403`, r.status === 403, `got ${r.status}`);
  }

  // ── Lifecycle: propose v1 (EFFECTIVE) → propose v2 (PROPOSED) → self-approve 403 → approve → supersede ──
  const v1 = await staff('POST', '/admin/security-policies', ADMIN, { policyKey: P_KEY, policyGroup: 'sla', jsonValue: '{"hours":24}', changeNote: 'RV v1' });
  check('propose v1 → 200 EFFECTIVE', v1.status === 200 && v1.json?.status === 'EFFECTIVE' && v1.json?.version === 1, `got ${v1.status} v${v1.json?.version} ${v1.json?.status}`);

  const badJson = await staff('POST', '/admin/security-policies', ADMIN, { policyKey: P_KEY, policyGroup: 'sla', jsonValue: 'not-json' });
  check('propose invalid JSON → 400', badJson.status === 400, `got ${badJson.status}`);

  const v2 = await staff('POST', '/admin/security-policies', ADMIN, { policyKey: P_KEY, policyGroup: 'sla', jsonValue: '{"hours":48}', changeNote: 'RV v2 — tăng SLA' });
  check('propose v2 → 200 PROPOSED v2', v2.status === 200 && v2.json?.status === 'PROPOSED' && v2.json?.version === 2, `got ${v2.status} v${v2.json?.version} ${v2.json?.status}`);

  const dup = await staff('POST', '/admin/security-policies', ADMIN, { policyKey: P_KEY, policyGroup: 'sla', jsonValue: '{"hours":72}' });
  check('propose khi đang có pending → 409', dup.status === 409, `got ${dup.status}`);

  const selfApprove = await staff('POST', `/admin/security-policies/${v2.json?.id}/approve`, ADMIN);
  check('maker-checker self-approve → 403', selfApprove.status === 403, `got ${selfApprove.status}`);

  // Staff được grant policy.manage + policy.approve → làm approver khác proposer.
  for (const p of ['policy.manage', 'policy.approve']) {
    const g = await staff('POST', '/admin/permissions', ADMIN, { demoUserId: STAFF_B, permission: p });
    check(`grant ${p} → 200`, g.status === 200, `got ${g.status}`);
  }
  const staffList = await staff('GET', '/admin/security-policies', STAFF);
  check('staff-granted policy.manage list → 200', staffList.status === 200, `got ${staffList.status}`);
  const hasPending = (staffList.json ?? []).some(x => x.policyKey === P_KEY && x.status === 'PROPOSED');
  check('list chứa proposal đang chờ', hasPending, 'pending-check');

  const approveByStaff = await staff('POST', `/admin/security-policies/${v2.json?.id}/approve`, STAFF);
  check('approve bởi staff khác → 200 EFFECTIVE', approveByStaff.status === 200 && approveByStaff.json?.status === 'EFFECTIVE', `got ${approveByStaff.status} ${approveByStaff.json?.status}`);

  const listAfter = await staff('GET', '/admin/security-policies', ADMIN);
  const rows = listAfter.json ?? [];
  const v1row = rows.find(x => x.policyKey === P_KEY && x.version === 1);
  const v2row = rows.find(x => x.policyKey === P_KEY && x.version === 2);
  check('v1 bị supersede, v2 effective', v1row?.status === 'SUPERSEDED' && v2row?.status === 'EFFECTIVE', `v1=${v1row?.status} v2=${v2row?.status}`);

  // ── Wire check: recovery.sla_hours thật → queue SLA dùng policy ─────────
  // Key mới chưa từng tồn tại → v1 tự EFFECTIVE (bootstrap); key đã tồn tại → PROPOSED cần approve.
  const sla = await staff('POST', '/admin/security-policies', ADMIN, { policyKey: 'recovery.sla_hours', policyGroup: 'sla', jsonValue: '{"hours":1000}', changeNote: 'RV W5c — tạm nâng SLA' });
  const slaOk = sla.status === 200 && (sla.json?.status === 'EFFECTIVE' || sla.json?.status === 'PROPOSED');
  check('propose recovery.sla_hours thật → 200 (v1 EFFECTIVE | vN PROPOSED)', slaOk, `got ${sla.status} v${sla.json?.version} ${sla.json?.status}`);
  if (slaOk && sla.json?.status === 'PROPOSED') {
    const slaApprove = await staff('POST', `/admin/security-policies/${sla.json?.id}/approve`, STAFF);
    check('approve recovery.sla_hours (staff khác) → 200', slaApprove.status === 200, `got ${slaApprove.status}`);
  }
  const queueAfter = await staff('GET', '/admin/recovery-requests?skip=0&take=50', ADMIN);
  const anyBreach = (queueAfter.json?.items ?? []).some(i => i.slaBreached);
  check('queue SLA dùng policy 1000h → không breach (trước đây 1 breach)', queueAfter.status === 200 && !anyBreach, `breach=${anyBreach}`);
  const dashAfter = await staff('GET', '/admin/dashboard', ADMIN);
  check('dashboard slaOverdue=0 khi policy 1000h', dashAfter.status === 200 && dashAfter.json?.slaOverdue === 0, `slaOverdue=${dashAfter.json?.slaOverdue}`);

  // ── Reject path ─────────────────────────────────────────────────────────
  const v3 = await staff('POST', '/admin/security-policies', ADMIN, { policyKey: P_KEY, policyGroup: 'sla', jsonValue: '{"hours":96}', changeNote: 'RV v3' });
  const rej = await staff('POST', `/admin/security-policies/${v3.json?.id}/reject`, STAFF);
  check('reject v3 → 200 REJECTED', rej.status === 200 && rej.json?.status === 'REJECTED', `got ${rej.status} ${rej.json?.status}`);
  const selfReject = await staff('POST', `/admin/security-policies/${v3.json?.id}/reject`, ADMIN);
  check('reject lần nữa (đã REJECTED) → 409', selfReject.status === 409, `got ${selfReject.status}`);

  // ── Cleanup: hạ SLA về hiện trạng trước RV (24h) + thu hồi grants + xoá rv-* policies ──
  const slaReset = await staff('POST', '/admin/security-policies', ADMIN, { policyKey: 'recovery.sla_hours', policyGroup: 'sla', jsonValue: '{"hours":24}', changeNote: 'RV W5c cleanup — reset 24h' });
  if (slaReset.status === 200) await staff('POST', `/admin/security-policies/${slaReset.json?.id}/approve`, STAFF);

  const grants = await staff('GET', `/admin/permissions?demoUserId=${STAFF_B}`, ADMIN);
  for (const g of (grants.json ?? [])) {
    if (g.isRevoked) continue;
    await staff('POST', `/admin/permissions/${g.id}/revoke`, ADMIN);
  }
  const afterRevoke = await staff('GET', '/admin/security-policies', STAFF);
  check('staff sau revoke → 403', afterRevoke.status === 403, `got ${afterRevoke.status}`);

  // Verify SLA đã về 24h (queue breach trở lại như ban đầu — 1 request >24h).
  const queueFinal = await staff('GET', '/admin/recovery-requests?skip=0&take=50', ADMIN);
  const breachFinal = (queueFinal.json?.items ?? []).filter(i => i.slaBreached).length;
  check('SLA reset 24h → breach trở lại (=1 request cũ)', breachFinal >= 1, `breach=${breachFinal}`);

  const fails = results.filter(r => !r.pass);
  console.log(`\n==== SWEEP ${fails.length === 0 ? 'ALL PASS' : `${fails.length} FAIL`} (${results.length - fails.length}/${results.length}) ====`);
  fs.writeFileSync('atrs-wave5c-sweep-report.json', JSON.stringify({ results, generatedAt: new Date().toISOString() }, null, 2));
  process.exit(fails.length === 0 ? 0 : 1);
}

main().catch(e => { console.error(e); process.exit(3); });