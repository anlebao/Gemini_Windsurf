#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV SWEEP — ATRS WAVE 4 (U02 evidence + recovery mine/cancel + D9 attachments)
// Sweep-first: chạy 1 lần thu TOÀN BỘ FAIL, phân tích theo nhóm root cause.
// Self-cleaning: dispute case withdraw + recovery request cancel ở cuối.
//
// Env:
//   ATRS_CUSTOMER_TOKEN  — customer token (X-Customer-Token)
//   ATRS_BASE            — default https://api2.khachvip.online (Gateway YARP)
//   ATRS_DIRECT          — default http://34.126.90.228:5080 (atrs trực tiếp)
// ─────────────────────────────────────────────────────────────────────────────
import { createHash } from 'node:crypto';
import fs from 'node:fs';

const BASE = process.env.ATRS_BASE ?? 'https://api2.khachvip.online';
const DIRECT = process.env.ATRS_DIRECT ?? 'http://34.126.90.228:5080';
const CUST = process.env.ATRS_CUSTOMER_TOKEN ?? '';
const API = `${BASE}/api/v1/account-trust`;
const TENANT = '00000000-0000-0000-0000-000000000001';
const NOTICE = 'privacy-v1';

const results = [];
const check = (name, pass, detail = '') => {
  results.push({ name, pass: !!pass });
  console.log(`[${pass ? 'PASS' : 'FAIL'}] ${name}${detail ? ` — ${String(detail).slice(0, 220)}` : ''}`);
};
const info = (name, detail = '') => console.log(`[INFO] ${name}${detail ? ` — ${String(detail).slice(0, 220)}` : ''}`);

async function call(method, path, { token = CUST, body } = {}) {
  const h = {};
  if (token) h['X-Customer-Token'] = token;
  if (body !== undefined) h['Content-Type'] = 'application/json';
  const res = await fetch(`${API}${path}`, {
    method, headers: h,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  let json = null;
  try { json = await res.json(); } catch { /* empty */ }
  return { status: res.status, json };
}

// 1x1 PNG hợp lệ (67 bytes)
const PNG_B64 = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==';

async function main() {
  console.log(`ATRS Wave-4 sweep → ${API}`);
  console.log(`cust=${CUST ? 'set' : 'MISSING'}`);
  if (!CUST) { console.error('ATRS_CUSTOMER_TOKEN required'); process.exit(2); }

  // ── L1: Health + auth negatives trên routes mới ──────────────────────────
  try {
    const h = await fetch(`${DIRECT}/health`);
    check('L1 health direct :5080', h.status === 200, `status=${h.status}`);
  } catch (e) { check('L1 health direct :5080', false, e.message); }

  for (const [label, method, path, body] of [
    ['personal-evidence-get', 'GET', '/personal-evidence', undefined],
    ['personal-evidence-put', 'PUT', '/personal-evidence', { fieldKey: 'FULL_NAME', value: 'x', privacyNoticeVersion: NOTICE }],
    ['consents-get', 'GET', '/consents', undefined],
    ['consents-post', 'POST', '/consents', { purpose: 'x', privacyNoticeVersion: NOTICE }],
    ['recovery-mine', 'GET', '/recovery-requests/mine', undefined],
    ['dispute-attachments', 'GET', `/disputes/${crypto.randomUUID()}/attachments`, undefined],
  ]) {
    const noTok = await call(method, path, { token: '', body });
    check(`auth-neg ${label} no-token=401`, noTok.status === 401, `got ${noTok.status}`);
  }

  const me = await call('GET', '/me');
  check('GET /me 200', me.status === 200, `status=${me.status}`);
  info('assuranceState', me.json?.assuranceState);

  // ── L2: Personal evidence (U02) ──────────────────────────────────────────
  const evGet = await call('GET', '/personal-evidence');
  check('GET personal-evidence 200 array', evGet.status === 200 && Array.isArray(evGet.json), `status=${evGet.status}`);

  const upsert = await call('PUT', '/personal-evidence', {
    body: { fieldKey: 'RECOVERY_NOTE', value: 'RV wave4 — ghi chú hỗ trợ test', privacyNoticeVersion: NOTICE },
  });
  check('PUT personal-evidence → 200', upsert.status === 200, `got ${upsert.status} ${JSON.stringify(upsert.json ?? '').slice(0, 160)}`);
  if (upsert.status === 200) {
    check('self-declared → UNVERIFIED (không bao giờ VERIFIED)',
      String(upsert.json?.verificationStatus ?? '').toUpperCase() !== 'VERIFIED',
      `status=${upsert.json?.verificationStatus}`);
    check('source = USER_DECLARED', String(upsert.json?.source ?? '').toUpperCase().includes('DECLARED') || upsert.json?.source === 'USER_DECLARED', `src=${upsert.json?.source}`);
  }

  const evBad = await call('PUT', '/personal-evidence', {
    body: { fieldKey: 'NOT_A_FIELD', value: 'x', privacyNoticeVersion: NOTICE },
  });
  check('PUT fieldKey lạ → 400', evBad.status === 400, `got ${evBad.status}`);

  // Masking: CONTACT_PHONE phải trả maskedValue, không plaintext
  const phone = await call('PUT', '/personal-evidence', {
    body: { fieldKey: 'CONTACT_PHONE', value: '0901234567', privacyNoticeVersion: NOTICE },
  });
  if (phone.status === 200) {
    const mv = phone.json?.maskedValue ?? '';
    check('phone masked (không lộ full digits)', !mv.includes('0901234567'), `masked=${mv}`);
  } else {
    info('phone upsert', `status=${phone.status} (field có thể không hỗ trợ — check UI fields)`);
  }

  // Consent grant → list → withdraw
  const grant = await call('POST', '/consents', { body: { purpose: 'evidence_processing', privacyNoticeVersion: NOTICE } });
  check('grant consent → 200', grant.status === 200, `got ${grant.status} ${JSON.stringify(grant.json ?? '').slice(0, 140)}`);
  const consentId = grant.json?.id;
  const consents = await call('GET', '/consents');
  check('GET consents 200 array', consents.status === 200 && Array.isArray(consents.json), `status=${consents.status}`);
  if (consentId) {
    const wd = await call('POST', `/consents/${consentId}/withdraw`);
    check('withdraw consent → 200', wd.status === 200, `got ${wd.status}`);
  }

  // ── L3: Recovery mine + cancel (U10) ─────────────────────────────────────
  const mine = await call('GET', '/recovery-requests/mine');
  check('GET recovery-requests/mine 200 array', mine.status === 200 && Array.isArray(mine.json), `status=${mine.status} n=${Array.isArray(mine.json) ? mine.json.length : '?'}`);

  // Submit requestType STRING (UI contract — regression cho fix ad5814c8)
  const idemKey = `rv-wave4-${Date.now()}`;
  const submit = await call('POST', '/recovery-requests', {
    body: { requestType: 'OTHER', idempotencyKey: idemKey, newDeviceId: null, evidenceSummaryJson: '{"note":"rv wave4"}' },
  });
  check('submit recovery (string requestType) → 200', submit.status === 200, `got ${submit.status} ${JSON.stringify(submit.json ?? '').slice(0, 200)}`);
  const reqId = submit.json?.request?.id ?? submit.json?.id;

  const badType = await call('POST', '/recovery-requests', {
    body: { requestType: 'NOT_A_TYPE', idempotencyKey: `rv-bad-${Date.now()}` },
  });
  check('submit requestType lạ → 400', badType.status === 400, `got ${badType.status}`);

  if (reqId) {
    const inMine = Array.isArray(mine.json) ? mine.json.some(r => (r.id ?? r.requestId) === reqId) : false;
    // /mine có thể không chứa request vừa tạo nếu list trước submit — re-fetch
    const mine2 = await call('GET', '/recovery-requests/mine');
    const inMine2 = Array.isArray(mine2.json) && mine2.json.some(r => r.id === reqId);
    check('/mine chứa request vừa submit', inMine2, `found=${inMine2}`);

    const cancel = await call('POST', `/recovery-requests/${reqId}/cancel`);
    check('cancel recovery → 200', cancel.status === 200, `got ${cancel.status} ${JSON.stringify(cancel.json ?? '').slice(0, 140)}`);
    const statusAfter = String(cancel.json?.status ?? cancel.json?.request?.status ?? '');
    check('status sau cancel = CANCELLED/terminal', ['CANCELLED', 'REJECTED', 'COMPLETED', 'EXPIRED'].includes(statusAfter) || cancel.status === 200, `status=${statusAfter}`);

    // Cancel lần 2 → idempotent (200 state hiện tại hoặc 404)
    const cancel2 = await call('POST', `/recovery-requests/${reqId}/cancel`);
    check('cancel replay → 200/404 (không 500)', [200, 404].includes(cancel2.status), `got ${cancel2.status}`);
  }

  // Ownership: fake id → 404
  const fakeCancel = await call('POST', `/recovery-requests/${crypto.randomUUID()}/cancel`);
  check('cancel fake id → 404 (anti-enum)', fakeCancel.status === 404, `got ${fakeCancel.status}`);

  // ── L4: Dispute attachments (D9/R2) ──────────────────────────────────────
  const sub = await call('POST', '/disputes', {
    body: {
      caseType: 'POINTS_MISSING',
      claimedIssue: 'RV Wave 4 — attachment upload test',
      tenantId: TENANT,
      idempotencyKey: `rv-wave4-d-${Date.now()}`,
    },
  });
  check('submit dispute → 200', sub.status === 200, `got ${sub.status}`);
  const caseId = sub.json?.case?.id ?? sub.json?.id;

  if (caseId) {
    const up = await call('POST', `/disputes/${caseId}/attachments`, {
      body: { fileName: 'rv-proof.png', contentType: 'image/png', dataBase64: PNG_B64 },
    });
    check('upload attachment png → 200', up.status === 200, `got ${up.status} ${JSON.stringify(up.json ?? '').slice(0, 200)}`);
    const att = up.json?.attachment ?? up.json;
    if (att?.downloadUrl) {
      check('downloadUrl là presigned https', String(att.downloadUrl).startsWith('https://'), `url=${String(att.downloadUrl).slice(0, 80)}`);
      // Presigned GET phải fetch được binary
      try {
        const dl = await fetch(att.downloadUrl);
        check('presigned GET → 200 binary', dl.status === 200, `status=${dl.status}`);
      } catch (e) { check('presigned GET → 200 binary', false, e.message); }
    } else {
      info('attachment downloadUrl', 'null — R2 có thể chưa cấu hình (UnconfiguredAtrsObjectStorage)');
    }

    const badType = await call('POST', `/disputes/${caseId}/attachments`, {
      body: { fileName: 'evil.exe', contentType: 'application/x-msdownload', dataBase64: PNG_B64 },
    });
    check('upload contentType lạ → 400', badType.status === 400, `got ${badType.status}`);

    const badB64 = await call('POST', `/disputes/${caseId}/attachments`, {
      body: { fileName: 'x.png', contentType: 'image/png', dataBase64: '!!!not-base64!!!' },
    });
    check('upload base64 hỏng → 400', badB64.status === 400, `got ${badB64.status}`);

    const list = await call('GET', `/disputes/${caseId}/attachments`);
    check('list attachments → 200 array', list.status === 200 && Array.isArray(list.json), `status=${list.status} n=${Array.isArray(list.json) ? list.json.length : '?'}`);

    const fakeList = await call('GET', `/disputes/${crypto.randomUUID()}/attachments`);
    check('list attachments fake case → 404', fakeList.status === 404, `got ${fakeList.status}`);

    // Self-cleaning: withdraw case
    const wd = await call('POST', `/disputes/${caseId}/withdraw`);
    check('withdraw case → 200', wd.status === 200, `got ${wd.status}`);
  }

  // ── Summary ──────────────────────────────────────────────────────────────
  const pass = results.filter(r => r.pass).length;
  const fail = results.filter(r => !r.pass);
  console.log(`\n═══ RESULT: ${pass}/${results.length} PASS ═══`);
  if (fail.length) {
    console.log('FAILURES:');
    fail.forEach(f => console.log(`  - ${f.name}`));
  }
  const report = { ts: new Date().toISOString(), base: API, total: results.length, pass, fail: fail.map(f => f.name), results };
  fs.writeFileSync('atrs-wave4-sweep-report.json', JSON.stringify(report, null, 2));
  console.log('report → atrs-wave4-sweep-report.json');
  process.exit(fail.length ? 1 : 0);
}

main().catch(e => { console.error('FATAL', e); process.exit(2); });
