#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV SWEEP — OCR Tenant Onboarding (S0-S5, 2026-10-25) — PRODUCTION
// Quét API surface + static assets trong 1 lần, thu TOÀN BỘ FAIL (sweep-first).
//
// Chạy:  node rv-scripts/ocr-onboarding-rv-sweep.mjs
// Report: rv-scripts/ocr-onboarding-rv-report.json
//
// Cover:
//   L1 — Gateway health · no-auth 401 · auth 400 (Case A/B validation) ·
//        Case A happy (Company, fake MST) · Case B happy (HKD Group2) · cleanup deactivate
//   L2 — static assets: onboarding-ocr.js / onboarding-wizard.js / vie.traineddata.gz
// ─────────────────────────────────────────────────────────────────────────────
import { writeFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const GW = process.env.GW_URL ?? 'https://api2.khachvip.online';
const APP2 = process.env.APP2_URL ?? 'https://app2.khachvip.online';
const ADMIN_USER = process.env.RV_ADMIN_USER ?? 'sysadmin@vanan.vn';
const ADMIN_PASS = process.env.RV_ADMIN_PASS ?? '2026@vanan';

const results = [];
function check(name, pass, detail = '') {
  results.push({ name, pass: !!pass, detail });
  console.log(`${pass ? '[PASS]' : '[FAIL]'} ${name}${detail ? ` — ${detail}` : ''}`);
}

async function loginAdmin() {
  const resp = await fetch(`${APP2}/api/platform/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ Username: ADMIN_USER, Password: ADMIN_PASS }),
  });
  if (!resp.ok) throw new Error(`Login fail: ${resp.status} ${await resp.text()}`);
  const body = await resp.json();
  const token = body?.token ?? body?.accessToken ?? body?.jwt ?? body?.access_token;
  if (!token) throw new Error(`No token in login response: ${JSON.stringify(body).slice(0, 200)}`);
  return token;
}

async function api(path, token, opts = {}) {
  const headers = { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) };
  const resp = await fetch(`${GW}${path}`, { ...opts, headers });
  let body = null;
  try { body = await resp.json(); } catch { /* non-JSON */ }
  return { status: resp.status, body };
}

// Fake MST 10 chữ số duy nhất mỗi run (tránh collision dedup Case A)
const ts = String(Date.now());
const FAKE_MST = '99' + ts.slice(-8);
const TS_NAME = new Date().toISOString().slice(0, 19).replace(/[:T]/g, '');

(async () => {
  console.log(`=== OCR Onboarding RV Sweep — ${new Date().toISOString()} ===`);

  // ── L1: API ─────────────────────────────────────────────────────────────
  try {
    const health = await fetch(`${GW}/health`);
    check('L1 Gateway /health 200', health.status === 200, `status=${health.status}`);
  } catch (e) {
    check('L1 Gateway /health 200', false, e.message);
  }

  try {
    const r = await fetch(`${GW}/api/v1/ocr-onboarding`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({}),
      redirect: 'manual',   // không follow — auth challenge trả 302/401, không phải 200
    });
    // 302 (redirect login) hoặc 401 đều chứng minh endpoint được bảo vệ
    check('L1 no-auth → 302/401 (endpoint tồn tại + auth enforced)', r.status === 302 || r.status === 401,
      `status=${r.status}`);
  } catch (e) {
    check('L1 no-auth → 302/401', false, e.message);
  }

  let token = null;
  try {
    token = await loginAdmin();
    check('L1 admin login (sysadmin@vanan.vn)', !!token);
  } catch (e) {
    check('L1 admin login', false, e.message);
  }

  if (token) {
    // Case A — MST không hợp lệ → 400
    const rA1 = await api('/api/v1/ocr-onboarding', token, {
      method: 'POST',
      body: JSON.stringify({
        case: 0, name: 'RV Test A1', address: '1 Test St',
        businessType: 1, hkdGroup: null, productLines: [],
        latitude: 10.77, longitude: 106.69,
        gpdkInfo: { mst: 'ABC123', name: 'RV Test A1', address: '1 Test St' },
      }),
    });
    check('L1 Case A invalid MST → 400 + message', rA1.status === 400 && JSON.stringify(rA1.body).includes('Mã số thuế'),
      `status=${rA1.status}`);

    // Case A — thiếu MST → 400
    const rA2 = await api('/api/v1/ocr-onboarding', token, {
      method: 'POST',
      body: JSON.stringify({
        case: 0, name: 'RV Test A2', address: '1 Test St',
        businessType: 1, hkdGroup: null, productLines: [],
        latitude: 10.77, longitude: 106.69,
        gpdkInfo: { mst: null, name: 'RV Test A2' },
      }),
    });
    check('L1 Case A missing MST → 400', rA2.status === 400, `status=${rA2.status}`);

    // Case B — thiếu HKDGroup → 400
    const rB1 = await api('/api/v1/ocr-onboarding', token, {
      method: 'POST',
      body: JSON.stringify({
        case: 1, name: 'RV Test B1', address: '1 Test St',
        businessType: 2, hkdGroup: null, productLines: [],
        latitude: 10.77, longitude: 106.69,
      }),
    });
    check('L1 Case B missing HKDGroup → 400', rB1.status === 400 && JSON.stringify(rB1.body).includes('HKDGroup'),
      `status=${rB1.status}`);

    // ── Case B HAPPY PATH (tạo tenant HKD thật + cleanup) ────────────────
    const nameB = `RV OCR Test ${TS_NAME}`;
    const rB2 = await api('/api/v1/ocr-onboarding', token, {
      method: 'POST',
      body: JSON.stringify({
        case: 1, name: nameB, address: '12 Nguyễn Huệ, Q1, HCM',
        businessType: 2, hkdGroup: 2,
        productLines: [{ name: 'Trà đào', price: 30000, lineNo: 1 }],
        latitude: 10.7705, longitude: 106.7031,
        signName: nameB,
      }),
    });
    const createdB = rB2.status === 200 && rB2.body?.tenantId && rB2.body?.ownerUsername && rB2.body?.ownerPassword;
    check('L1 Case B happy → 200 + credentials (1 lần)', createdB, `status=${rB2.status}`);
    const tenantBId = rB2.body?.tenantId;
    if (tenantBId) {
      const listB = await api('/api/v1/tenants', token);
      check('L1 tenant B xuất hiện trong danh sách', listB.status === 200 &&
        JSON.stringify(listB.body).includes(tenantBId));
      const deactB = await api(`/api/v1/tenants/${tenantBId}/deactivate`, token,
        { method: 'POST', body: JSON.stringify({ reason: 'RV cleanup' }) });
      check('L1 cleanup B: deactivate → 200', deactB.status === 200, `status=${deactB.status}`);
    }

    // ── Case A HAPPY PATH (Công ty, fake MST + cleanup) ───────────────────
    const nameA = `RV OCR TestA ${TS_NAME}`;
    const rA3 = await api('/api/v1/ocr-onboarding', token, {
      method: 'POST',
      body: JSON.stringify({
        case: 0, name: nameA, address: '12 Nguyễn Huệ, Q1, HCM',
        businessType: 1, hkdGroup: null,
        productLines: [],
        latitude: 10.7705, longitude: 106.7031,
        gpdkInfo: { mst: FAKE_MST, name: nameA, address: '12 Nguyễn Huệ', legalForm: 'TNHH' },
        signName: nameA,
      }),
    });
    const createdA = rA3.status === 200 && rA3.body?.tenantId;
    check('L1 Case A happy → 200 (fake MST)', createdA, `status=${rA3.status} ${rA3.body?.error ?? ''}`);
    const tenantAId = rA3.body?.tenantId;
    if (tenantAId) {
      const deactA = await api(`/api/v1/tenants/${tenantAId}/deactivate`, token,
        { method: 'POST', body: JSON.stringify({ reason: 'RV cleanup' }) });
      check('L1 cleanup A: deactivate → 200', deactA.status === 200, `status=${deactA.status}`);
    }

    // Dedup Case A: tạo lại cùng MST → chặn (ExistingDuplicateTenantId)
    const rA4 = await api('/api/v1/ocr-onboarding', token, {
      method: 'POST',
      body: JSON.stringify({
        case: 0, name: nameA + ' dup', address: '12 Nguyễn Huệ, Q1, HCM',
        businessType: 1, hkdGroup: null, productLines: [],
        latitude: 10.7705, longitude: 106.7031,
        gpdkInfo: { mst: FAKE_MST, name: nameA },
      }),
    });
    check('L1 Case A dedup MST → ExistingDuplicateTenantId (chặn tạo trùng)',
      rA4.status === 200 && rA4.body?.existingDuplicateTenantId === tenantAId,
      `status=${rA4.status}`);
  }

  // ── L2: Static assets ──────────────────────────────────────────────────
  try {
    const js = await fetch(`${APP2}/js/onboarding-ocr.js`);
    const txt = await js.text();
    check('L2 onboarding-ocr.js 200 + marker', js.status === 200 && txt.includes('vananOnboardingOcr'),
      `status=${js.status}, len=${txt.length}`);
  } catch (e) {
    check('L2 onboarding-ocr.js', false, e.message);
  }

  try {
    const js = await fetch(`${APP2}/js/onboarding-wizard.js`);
    const txt = await js.text();
    check('L2 onboarding-wizard.js 200 + marker', js.status === 200 && txt.includes('vananOnboardingWizard'),
      `status=${js.status}, len=${txt.length}`);
    // 2026-10-26 user directive: camera scan mode (openCamera/captureAndOcr/captureMenuShot)
    check('L2 wizard JS có camera scan functions', txt.includes('openCamera') && txt.includes('captureAndOcr') && txt.includes('captureMenuShot'));
  } catch (e) {
    check('L2 onboarding-wizard.js', false, e.message);
  }

  try {
    const gz = await fetch(`${APP2}/js/lib/ocr/vie.traineddata.gz`);
    check('L2 vie.traineddata.gz 200 (OCR tiếng Việt)', gz.status === 200, `status=${gz.status}`);
  } catch (e) {
    check('L2 vie.traineddata.gz', false, e.message);
  }

  // ── Report ─────────────────────────────────────────────────────────────
  const failed = results.filter((r) => !r.pass).length;
  const report = { ts: new Date().toISOString(), total: results.length, failed, results };
  writeFileSync(join(__dirname, 'ocr-onboarding-rv-report.json'), JSON.stringify(report, null, 2));
  console.log(`\n=== SWEEP DONE: ${results.length - failed}/${results.length} PASS, ${failed} FAIL ===`);
  process.exit(failed > 0 ? 1 : 0);
})();