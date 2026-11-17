#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV SWEEP — ATRS WAVE 5 W5b (A20 audit explorer + A22 roles/grants) — PRODUCTION
//
// Env:
//   ATRS_ADMIN_JWT  — staff JWT role=SystemAdmin (break-glass)
//   ATRS_STAFF_JWT  — staff JWT không grant (RBAC negative) → sau đó được grant audit.read
//   ATRS_BASE       — default https://api2.khachvip.online (Gateway YARP)
//   ATRS_DIRECT     — default http://34.126.90.228:5080 (atrs trực tiếp)
// ─────────────────────────────────────────────────────────────────────────────
import fs from 'node:fs';

const BASE = process.env.ATRS_BASE ?? 'https://api2.khachvip.online';
const DIRECT = process.env.ATRS_DIRECT ?? 'http://34.126.90.228:5080';
const ADMIN = process.env.ATRS_ADMIN_JWT ?? '';
const STAFF = process.env.ATRS_STAFF_JWT ?? '';
const API = `${BASE}/api/v1/account-trust`;
const STAFF_B = 'bbbbbbbb-0000-4000-8000-0000000000b2';
const RV_SUBJECT = '43094916-d1a1-42f3-a443-af3130823eb2'; // customer RV W5a — có events

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
  console.log(`ATRS Wave-5 W5b sweep → ${API}`);
  console.log(`admin=${ADMIN ? 'set' : 'MISSING'} staff=${STAFF ? 'set' : 'MISSING'}`);
  if (!ADMIN || !STAFF) { console.error('ATRS_ADMIN_JWT + ATRS_STAFF_JWT required'); process.exit(2); }

  // ── L1: Health + auth negatives ──────────────────────────────────────────
  try {
    const h = await fetch(`${DIRECT}/health`);
    check('L1 health direct :5080', h.status === 200, `status=${h.status}`);
  } catch (e) { check('L1 health direct :5080', false, e.message); }

  for (const [label, method, path, body] of [
    ['audit-list', 'GET', '/admin/security-events', undefined],
    ['permissions-list', 'GET', '/admin/permissions', undefined],
    ['grant', 'POST', '/admin/permissions', { demoUserId: STAFF_B, permission: 'audit.read' }],
  ]) {
    const noTok = await call(method, path, { body });
    check(`auth-neg ${label} no-token=401`, noTok.status === 401, `got ${noTok.status}`);
    const badTok = await call(method, path, { bearer: 'invalid-token-xyz', body });
    check(`auth-neg ${label} bad-token=401`, badTok.status === 401, `got ${badTok.status}`);
  }

  // ── L2: RBAC negative — STAFF không grant → 403 ─────────────────────────
  const staffDeny = [
    ['audit', 'GET', '/admin/security-events'],
    ['permissions', 'GET', '/admin/permissions'],
    ['grant', 'POST', '/admin/permissions', { demoUserId: STAFF_B, permission: 'audit.read' }],
    ['revoke', 'POST', '/admin/permissions/00000000-0000-0000-0000-000000000001/revoke'],
  ];
  for (const [label, method, path, body] of staffDeny) {
    const r = await staff(method, path, STAFF, body);
    check(`rbac staff-no-grant ${label}=403`, r.status === 403, `got ${r.status}`);
  }

  // ── L3: A20 audit explorer — ADMIN ──────────────────────────────────────
  const page0 = await staff('GET', '/admin/security-events?skip=0&take=50', ADMIN);
  const p0 = page0.json ?? {};
  const shapeOk = page0.status === 200 && Array.isArray(p0.items) && typeof p0.total === 'number'
    && p0.items.every(i => typeof i.eventType === 'string' && typeof i.outcome === 'string' && typeof i.occurredAt === 'string');
  check('L3 audit page shape (items/total + fields)', shapeOk, `status=${page0.status} total=${p0.total}`);
  const p0str = JSON.stringify(p0);
  check('L3 audit SANITIZED (không payload JSON)', !p0str.includes('minimalMetadataJson') && !p0str.includes('evidenceReferencesJson'), 'payload-leak-check');

  const byEvent = await staff('GET', '/admin/security-events?eventType=recovery', ADMIN);
  check('L3 filter eventType=recovery → total>0', byEvent.status === 200 && byEvent.json?.total > 0, `total=${byEvent.json?.total}`);
  check('L3 filter eventType all match', (byEvent.json?.items ?? []).every(i => i.eventType.includes('recovery')), 'match-check');

  const byOutcome = await staff('GET', '/admin/security-events?outcome=Failure', ADMIN);
  check('L3 filter outcome=Failure → 200', byOutcome.status === 200, `total=${byOutcome.json?.total}`);

  const byActor = await staff('GET', '/admin/security-events?actorType=Staff', ADMIN);
  check('L3 filter actorType=Staff → total>0', byActor.status === 200 && byActor.json?.total > 0, `total=${byActor.json?.total}`);

  const bySubject = await staff('GET', `/admin/security-events?subjectId=${RV_SUBJECT}`, ADMIN);
  check('L3 filter subjectId (customer RV) → 200', bySubject.status === 200, `total=${bySubject.json?.total}`);
  check('L3 filter subjectId all match', (bySubject.json?.items ?? []).every(i => i.trustSubjectId === RV_SUBJECT), 'match-check');

  const now = new Date();
  const from = new Date(now.getTime() - 48 * 3600 * 1000).toISOString();
  const to = now.toISOString();
  const byDate = await staff('GET', `/admin/security-events?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`, ADMIN);
  check('L3 filter date range 48h → 200 + total>0', byDate.status === 200 && byDate.json?.total > 0, `total=${byDate.json?.total}`);

  const page1 = await staff('GET', '/admin/security-events?skip=0&take=5', ADMIN);
  const page2 = await staff('GET', '/admin/security-events?skip=5&take=5', ADMIN);
  check('L3 paging skip/take + total nhất quán', page1.status === 200 && page2.status === 200
    && page1.json?.items?.length === 5 && page1.json?.total === page2.json?.total, `t1=${page1.json?.total} t2=${page2.json?.total}`);

  // ── L4: read-only — không update/delete ─────────────────────────────────
  const put = await call('PUT', '/admin/security-events', { bearer: ADMIN });
  const del = await call('DELETE', '/admin/security-events/00000000-0000-0000-0000-000000000001', { bearer: ADMIN });
  // PUT không match action → 405; DELETE có segment id → route không tồn tại → 404.
  check('L4 audit read-only (PUT 405 / DELETE 404-405)', put.status === 405 && [404, 405].includes(del.status), `put=${put.status} del=${del.status}`);

  // ── L5: A22 roles/grants — ADMIN ────────────────────────────────────────
  const list0 = await staff('GET', '/admin/permissions', ADMIN);
  const l0 = list0.json ?? [];
  check('L5 permissions list → 200 array + fields', list0.status === 200 && Array.isArray(l0)
    && l0.every(g => typeof g.id === 'string' && typeof g.demoUserId === 'string' && typeof g.permission === 'string'), `count=${l0.length}`);

  const badGrant = await staff('POST', '/admin/permissions', ADMIN, { demoUserId: STAFF_B, permission: 'not.a.permission' });
  check('L5 grant invalid permission → 400', badGrant.status === 400, `got ${badGrant.status}`);

  for (const p of ['audit.read', 'dispute.view']) {
    const g = await staff('POST', '/admin/permissions', ADMIN, { demoUserId: STAFF_B, permission: p });
    check(`L5 grant ${p} → 200`, g.status === 200, `got ${g.status}`);
  }
  const dup = await staff('POST', '/admin/permissions', ADMIN, { demoUserId: STAFF_B, permission: 'audit.read' });
  check('L5 grant duplicate → 200 idempotent (existing)', dup.status === 200, `got ${dup.status}`);

  const staffAudit = await staff('GET', '/admin/security-events', STAFF);
  check('L6 staff-granted audit.read → 200', staffAudit.status === 200, `got ${staffAudit.status}`);
  const staffPermList = await staff('GET', '/admin/permissions', STAFF);
  check('L6 staff permissions list vẫn 403 (grant mgmt SystemAdmin-only)', staffPermList.status === 403, `got ${staffPermList.status}`);

  // Cleanup grants (self-cleaning).
  const grants = await staff('GET', `/admin/permissions?demoUserId=${STAFF_B}`, ADMIN);
  for (const g of (grants.json ?? [])) {
    if (g.isRevoked) continue;
    const rv = await staff('POST', `/admin/permissions/${g.id}/revoke`, ADMIN);
    check(`L5 revoke ${g.permission} → 200`, rv.status === 200, `got ${rv.status}`);
  }
  const afterRevoke = await staff('GET', '/admin/security-events', STAFF);
  check('L6 staff audit sau revoke → 403', afterRevoke.status === 403, `got ${afterRevoke.status}`);

  // ── Summary ──────────────────────────────────────────────────────────────
  const fails = results.filter(r => !r.pass);
  console.log(`\n==== SWEEP ${fails.length === 0 ? 'ALL PASS' : `${fails.length} FAIL`} (${results.length - fails.length}/${results.length}) ====`);
  fs.writeFileSync('atrs-wave5b-sweep-report.json', JSON.stringify({ results, generatedAt: new Date().toISOString() }, null, 2));
  process.exit(fails.length === 0 ? 0 : 1);
}

main().catch(e => { console.error(e); process.exit(3); });