#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV SWEEP — Onboarding P6 (2026-11-11) — PRODUCTION
// Cloud OCR assist (Gemini) + upload kind/thumb + self-serve submit E2E + static
// assets (markers P6). Sweep-first: thu TOÀN BỘ FAIL.
//
// Chạy:  node rv-scripts/ocr-p6-assist-rv-sweep.mjs
//   env:  SYSADMIN_PW (bắt buộc cho phần cleanup admin — SEC-2, password rotate 2026-11-10)
//         RV_SIGN_IMG / RV_MENU_IMG (mặc định rv-fixtures/rv-sign.png, rv-menu.png)
// Report: rv-scripts/ocr-p6-assist-rv-report.json
//
// Cover:
//   L1 — /health · ocr-assist (sign+menu qua base64 inline + URL host allowlist) ·
//        validation 400 (kind sai, thiếu ảnh) · upload kind/thumb + pattern URL ·
//        upload validation 400 (kind sai, folder sai, thumb thiếu kind)
//   L1b — merchant-signups: config public · submit Case B (không OTP — requireOtp=false) ·
//         status Pending · cleanup reject-pending (cần SYSADMIN_PW) → status Rejected
//   L2 — static assets: onboarding-ocr.js / onboarding-wizard.js (ShopERP app2 +
//        KhachLink api2) chứa markers P6 (VN_PHONE_RE/_extractPhone/_extractAddress/
//        makeThumbnailBytes) · vie.traineddata.gz
// ─────────────────────────────────────────────────────────────────────────────
import { writeFileSync, readFileSync, existsSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const GW = process.env.GW_URL ?? 'https://api2.khachvip.online';
const APP2 = process.env.APP2_URL ?? 'https://app2.khachvip.online';
const SYSADMIN_PW = process.env.SYSADMIN_PW ?? '';
const SIGN_IMG = process.env.RV_SIGN_IMG ?? join(__dirname, '..', 'rv-fixtures', 'rv-sign.png');
const MENU_IMG = process.env.RV_MENU_IMG ?? join(__dirname, '..', 'rv-fixtures', 'rv-menu.png');

const results = [];
function check(name, pass, detail = '') {
  results.push({ name, pass: !!pass, detail });
  console.log(`${pass ? '[PASS]' : '[FAIL]'} ${name}${detail ? ` — ${detail}` : ''}`);
}
const toB64 = (p) => existsSync(p) ? readFileSync(p).toString('base64') : null;

async function upload(fileBytes, filename, qs, folder = 'onboarding') {
  const form = new FormData();
  form.append('file', new Blob([fileBytes], { type: 'image/png' }), filename);
  const resp = await fetch(`${GW}/api/v1/images/upload?folder=${folder}${qs}`, { method: 'POST', body: form });
  let body = null;
  try { body = await resp.json(); } catch { /* noop */ }
  return { status: resp.status, body };
}

async function api(path, opts = {}, token = null) {
  const headers = { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) };
  const resp = await fetch(`${GW}${path}`, { ...opts, headers });
  let body = null;
  try { body = await resp.json(); } catch { /* noop */ }
  return { status: resp.status, body };
}

const ts = String(Date.now());
const RV_NAME = `RV P6 ${new Date().toISOString().slice(0, 19).replace(/[:T]/g, '')}`;
const RV_PHONE = '0912345678';

(async () => {
  console.log(`=== Onboarding P6 RV Sweep — ${new Date().toISOString()} ===`);

  // ── L1: API ─────────────────────────────────────────────────────────────
  try {
    const health = await fetch(`${GW}/health`);
    check('L1 /health 200', health.status === 200, `status=${health.status}`);
  } catch (e) { check('L1 /health 200', false, e.message); }

  const signB64 = toB64(SIGN_IMG);
  const menuB64 = toB64(MENU_IMG);

  // ocr-assist — sign (base64 inline, anonymous)
  if (signB64) {
    try {
      const r = await api('/api/v1/ocr-assist', {
        method: 'POST',
        body: JSON.stringify({ kind: 'sign', imageBase64: signB64, imageContentType: 'image/png' }),
      });
      const ok = r.status === 200 && r.body?.provider === 'gemini' &&
        (r.body?.signName || r.body?.SignName);
      check('L1 ocr-assist sign → 200 gemini + signName', ok,
        `status=${r.status} provider=${r.body?.provider} sign=${r.body?.signName}`);
      if (r.body?.address || r.body?.phone) {
        check('L1 ocr-assist sign → address/phone extract', !!(r.body?.address && r.body?.phone),
          `address="${r.body?.address}" phone="${r.body?.phone}"`);
      }
    } catch (e) { check('L1 ocr-assist sign', false, e.message); }
  } else {
    check('L1 ocr-assist sign (skip — thiếu ảnh test)', false, `RV_SIGN_IMG=${SIGN_IMG}`);
  }

  // ocr-assist — menu (base64)
  if (menuB64) {
    try {
      const r = await api('/api/v1/ocr-assist', {
        method: 'POST',
        body: JSON.stringify({ kind: 'menu', imageBase64: menuB64, imageContentType: 'image/png' }),
      });
      const items = r.body?.productLines || r.body?.ProductLines;
      check('L1 ocr-assist menu → 200 gemini + items', r.status === 200 && Array.isArray(items) && items.length >= 3,
        `status=${r.status} items=${Array.isArray(items) ? items.length : 0}`);
      if (Array.isArray(items) && items.length) {
        const priced = items.filter((i) => i.price).length;
        check('L1 ocr-assist menu → có price', priced >= 3, `priced=${priced}`);
      }
    } catch (e) { check('L1 ocr-assist menu', false, e.message); }
  } else {
    check('L1 ocr-assist menu (skip — thiếu ảnh test)', false, `RV_MENU_IMG=${MENU_IMG}`);
  }

  // ocr-assist — validation
  try {
    const r = await api('/api/v1/ocr-assist', { method: 'POST', body: JSON.stringify({ kind: 'xyz', imageBase64: signB64 }) });
    check('L1 ocr-assist kind sai → 400', r.status === 400, `status=${r.status}`);
  } catch (e) { check('L1 ocr-assist kind sai', false, e.message); }
  try {
    const r = await api('/api/v1/ocr-assist', { method: 'POST', body: JSON.stringify({ kind: 'sign' }) });
    check('L1 ocr-assist thiếu ảnh → 400', r.status === 400, `status=${r.status}`);
  } catch (e) { check('L1 ocr-assist thiếu ảnh', false, e.message); }

  // Upload — kind + thumb + validation
  const signBytes = signB64 ? Buffer.from(signB64, 'base64') : Buffer.from('RV', 'utf8');
  try {
    const u = await upload(signBytes, 'rv-sign.png', '&kind=sign');
    check('L1 upload kind=sign → 200 + /onboarding/sign/', u.status === 200 && /\/onboarding\/sign\//.test(u.body?.url ?? ''),
      `status=${u.status} url=${u.body?.url?.slice(0, 90) ?? ''}`);
    if (u.status === 200 && u.body?.url) {
      // ocr-assist qua ImageUrl (host allowlist R2)
      try {
        const r = await api('/api/v1/ocr-assist', {
          method: 'POST',
          body: JSON.stringify({ kind: 'sign', imageUrl: u.body.url }),
        });
        check('L1 ocr-assist qua ImageUrl (allowlist) → 200', r.status === 200 && r.body?.provider === 'gemini',
          `status=${r.status} provider=${r.body?.provider}`);
      } catch (e) { check('L1 ocr-assist qua ImageUrl', false, e.message); }
    }
  } catch (e) { check('L1 upload kind=sign', false, e.message); }

  try {
    const u = await upload(signBytes, 'thumb.jpg', '&kind=sign&thumb=true');
    check('L1 upload thumb=true → 200 + /onboarding-thumbs/sign/ + .jpg',
      u.status === 200 && /\/onboarding-thumbs\/sign\/.+\.jpg/.test(u.body?.url ?? ''),
      `status=${u.status} url=${u.body?.url?.slice(0, 100) ?? ''}`);
  } catch (e) { check('L1 upload thumb=true', false, e.message); }

  try {
    const u = await upload(signBytes, 'rv-sign.png', '&kind=hack');
    check('L1 upload kind sai → 400', u.status === 400, `status=${u.status}`);
  } catch (e) { check('L1 upload kind sai', false, e.message); }
  try {
    const u = await upload(signBytes, 'rv-sign.png', '&kind=sign', 'gpkd-claims'); // kind + folder không onboarding → 400
    check('L1 upload kind + folder không onboarding → 400', u.status === 400, `status=${u.status}`);
  } catch (e) { check('L1 upload kind + folder sai', false, e.message); }
  try {
    const u = await upload(signBytes, 'thumb.jpg', '&thumb=true'); // thiếu kind
    check('L1 upload thumb thiếu kind → 400', u.status === 400, `status=${u.status}`);
  } catch (e) { check('L1 upload thumb thiếu kind', false, e.message); }

  // ── L1b: self-serve submit E2E (public, requireOtp=false) ───────────────
  try {
    const cfg = await api('/api/v1/merchant-signups/config');
    check('L1b merchant-signups/config 200 public', cfg.status === 200, `status=${cfg.status}`);
    if (cfg.body?.requireOtp === true) {
      check('L1b submit E2E (skip — requireOtp=true cần OTP)', false, 'requireOtp=true');
    } else {
      const submit = await api('/api/v1/merchant-signups', {
        method: 'POST',
        body: JSON.stringify({
          case: 1, name: RV_NAME, address: '12 Nguyễn Huệ, Q1, HCM',
          businessType: 2, hkdGroup: 2, signName: RV_NAME,
          contactName: 'RV Sweep', contactPhone: RV_PHONE,
          ownerUsername: `rv.p6.${ts.slice(-8)}`, ownerPassword: 'matkhauRV123',
          photoUrls: [], productLines: [{ name: 'Trà đào', price: 30000, lineNo: 1 }],
          src: 'direct',
        }),
      });
      const signupId = submit.body?.signupId;
      check('L1b submit Case B → 200 + signupId', submit.status === 200 && !!signupId,
        `status=${submit.status} signupId=${signupId ?? ''}`);

      if (signupId) {
        const st = await api(`/api/v1/merchant-signups/status?signupId=${signupId}&phone=${RV_PHONE}`);
        check('L1b status → Pending', st.status === 200 && st.body?.status === 'Pending',
          `status=${st.status} body=${JSON.stringify(st.body)}`);

        if (SYSADMIN_PW) {
          try {
            const login = await fetch(`${APP2}/api/platform/login`, {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ Username: 'sysadmin@vanan.vn', Password: SYSADMIN_PW }),
            });
            const loginBody = login.ok ? await login.json() : null;
            const token = loginBody?.token;
            check('L1b admin login', login.ok && !!token, `status=${login.status}`);
            if (token) {
              const pending = await api('/api/v1/tenants/pending', {}, token);
              const match = (pending.body ?? []).find((t) => (t.name ?? '').includes('RV P6'));
              check('L1b pending list → tìm tenant sweep', pending.status === 200 && !!match?.id,
                `status=${pending.status} found=${!!match}`);
              if (match?.id) {
                const rej = await api(`/api/v1/tenants/${match.id}/reject-pending`, {
                  method: 'POST',
                  body: JSON.stringify({ reason: 'RV sweep cleanup' }),
                }, token);
                check('L1b cleanup reject-pending → 204', rej.status === 204, `status=${rej.status}`);
                const st2 = await api(`/api/v1/merchant-signups/status?signupId=${signupId}&phone=${RV_PHONE}`);
                check('L1b status sau reject → Rejected', st2.status === 200 && st2.body?.status === 'Rejected',
                  `status=${st2.status} body=${JSON.stringify(st2.body)}`);
              }
            }
          } catch (e) {
            check('L1b admin cleanup', false, e.message);
          }
        } else {
          console.warn(`[WARN] SYSADMIN_PW chưa set — bỏ qua cleanup. Test residue: signupId=${signupId} name="${RV_NAME}" (reject qua UI /admin/tenants hoặc set SYSADMIN_PW rồi chạy lại)`);
        }
      }
    }
  } catch (e) { check('L1b submit E2E', false, e.message); }

  // ── L2: static assets (markers P6) ─────────────────────────────────────
  const assets = [
    { label: 'ShopERP app2 wizard JS', url: `${APP2}/js/onboarding-wizard.js`, markers: ['makeThumbnailBytes'] },
    { label: 'ShopERP app2 ocr JS', url: `${APP2}/js/onboarding-ocr.js`, markers: ['VN_PHONE_RE', '_extractAddress', '_extractPhone'] },
    { label: 'KhachLink api2 wizard JS', url: `${GW}/js/onboarding-wizard.js`, markers: ['makeThumbnailBytes'] },
    { label: 'KhachLink api2 ocr JS', url: `${GW}/js/onboarding-ocr.js`, markers: ['VN_PHONE_RE', '_extractAddress', '_extractPhone'] },
    { label: 'L2 vie.traineddata.gz', url: `${GW}/js/lib/ocr/vie.traineddata.gz`, markers: [] },
  ];
  for (const a of assets) {
    try {
      const resp = await fetch(a.url);
      if (a.markers.length === 0) {
        check(`L2 ${a.label} 200`, resp.status === 200, `status=${resp.status}`);
        continue;
      }
      const txt = await resp.text();
      const missing = a.markers.filter((m) => !txt.includes(m));
      check(`L2 ${a.label} 200 + markers`, resp.status === 200 && missing.length === 0,
        `status=${resp.status} len=${txt.length}${missing.length ? ` thiếu: ${missing.join(',')}` : ''}`);
    } catch (e) { check(`L2 ${a.label}`, false, e.message); }
  }

  // ── Report ─────────────────────────────────────────────────────────────
  const failed = results.filter((r) => !r.pass).length;
  const report = { ts: new Date().toISOString(), total: results.length, failed, results };
  writeFileSync(join(__dirname, 'ocr-p6-assist-rv-report.json'), JSON.stringify(report, null, 2));
  console.log(`\n=== SWEEP DONE: ${results.length - failed}/${results.length} PASS, ${failed} FAIL ===`);
  console.log(`Report: rv-scripts/ocr-p6-assist-rv-report.json`);
  process.exit(failed > 0 ? 1 : 0);
})();