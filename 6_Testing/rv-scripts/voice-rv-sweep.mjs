#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV SWEEP — VOICE SERVICE P7 (2026-11-11) — PRODUCTION (voice.khachvip.online)
// Sweep-first: thu TOÀN BỘ FAIL. Lớp browser (2-tab SDK call flow, chat UI, deny/
// kill-switch E2E) nằm ở 6_Testing/e2e-tests (voice-p2p/voice-chat/voice-admin) — C6.
//
// Chạy:  node rv-scripts/voice-rv-sweep.mjs
//   env:  VOICE_JWT_SECRET (Jwt__Secret từ /opt/vanan-voice/.env.voice — KHÔNG hardcode;
//         thiếu → bỏ qua các check cần auth với WARN)
//         GW_URL (api2.khachvip.online — tùy chọn, cho kill-switch config toggle qua
//         /api/admin/voice-config với SystemAdmin JWT mint cùng secret)
// Report: rv-scripts/voice-rv-report.json
//
// Cover:
//   L1 — /v1/health 200 · /v1/metrics/summary 200 + shape
//   L2 — TURN /v1/turn/credentials: no-auth 401 · bad-token 401 · valid 200 + shape
//        missed-calls: no-auth 401 · cross-user 403 · own 200
//   L2b— voice-config GET public 200 + shape (C3 — PG-backed read)
//   L5 — kill-switch config: toggle VoiceFeatureEnabled OFF→ON qua Gateway admin API
//        (TEST tenant aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee, restore cuối run)
// ─────────────────────────────────────────────────────────────────────────────
import { writeFileSync } from 'fs';
import { createHmac } from 'node:crypto';

const VOICE = process.env.VOICE_BASE ?? 'https://voice.khachvip.online';
const GW = process.env.GW_URL ?? 'https://api2.khachvip.online';
const SECRET = process.env.VOICE_JWT_SECRET ?? '';
const TEST_TENANT = 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee';

const results = [];
function check(name, pass, detail = '') {
  results.push({ name, pass: !!pass, detail: String(detail).slice(0, 300) });
  console.log(`[${pass ? 'PASS' : 'FAIL'}] ${name}${detail ? ` — ${String(detail).slice(0, 160)}` : ''}`);
}
function warn(name, detail = '') {
  results.push({ name, pass: null, detail: 'SKIP: ' + String(detail).slice(0, 200) });
  console.log(`[WARN] ${name} — SKIP: ${String(detail).slice(0, 140)}`);
}

function mintJwt(userId, tenantId, role = null) {
  const now = Math.floor(Date.now() / 1000);
  const b64 = (o) => Buffer.from(JSON.stringify(o)).toString('base64url');
  const payload = { sub: userId, tenant_id: tenantId, nbf: now, exp: now + 3600, iat: now, iss: 'VanAnShopERP', aud: 'VanAnApi' };
  if (role) payload.role = role;
  const signingInput = `${b64({ alg: 'HS256', typ: 'JWT' })}.${b64(payload)}`;
  return `${signingInput}.${createHmac('sha256', SECRET).update(signingInput).digest('base64url')}`;
}

async function get(path, token = null) {
  const headers = { Accept: 'application/json' };
  if (token) headers.Authorization = `Bearer ${token}`;
  const res = await fetch(`${VOICE}${path}`, { headers });
  let json = null;
  try { json = await res.json(); } catch { /* non-json */ }
  return { status: res.status, json };
}

// ============ L1. HEALTH + METRICS ============
{
  const r = await get('/v1/health');
  check('L1 health 200', r.status === 200 && r.json?.service === 'vanan-voice', `status=${r.status} body=${JSON.stringify(r.json)}`);
}
{
  const r = await get('/v1/metrics/summary');
  const shape = r.json && typeof r.json.total === 'number' && typeof r.json.today === 'number'
    && typeof r.json.connected === 'number' && typeof r.json.failed === 'number';
  check('L1 metrics/summary 200 + shape', r.status === 200 && shape, `status=${r.status} keys=${r.json ? Object.keys(r.json).join(',') : 'null'}`);
}

// ============ L2. AUTH — TURN + MISSED-CALLS ============
if (!SECRET) {
  warn('L2 TURN/missed-calls auth checks', 'VOICE_JWT_SECRET chưa set — bỏ qua (chỉ chạy phần anonymous)');
} else {
  const userA = '11111111-1111-4111-8111-111111111111';
  const userB = '22222222-2222-4222-8222-222222222222';
  const tokenA = mintJwt(userA, TEST_TENANT);

  {
    const r = await get('/v1/turn/credentials');
    check('L2 TURN no-auth → 401', r.status === 401, `status=${r.status}`);
  }
  {
    const r = await get('/v1/turn/credentials', 'bad.token.here');
    check('L2 TURN bad-token → 401', r.status === 401, `status=${r.status}`);
  }
  {
    const r = await get('/v1/turn/credentials', tokenA);
    const shape = r.json && Array.isArray(r.json.urls) && r.json.urls.length > 0
      && !!r.json.username && !!r.json.credential && r.json.ttl > 0;
    check('L2 TURN valid → 200 + shape', r.status === 200 && shape,
      `status=${r.status} urls=${r.json ? JSON.stringify(r.json.urls) : 'null'}`);
  }
  {
    const r = await get(`/v1/missed-calls/${userA}`);
    check('L2 missed-calls no-auth → 401', r.status === 401, `status=${r.status}`);
  }
  {
    const r = await get(`/v1/missed-calls/${userB}`, tokenA); // userA đọc của userB → 403
    check('L2 missed-calls cross-user → 403', r.status === 403, `status=${r.status}`);
  }
  {
    const r = await get(`/v1/missed-calls/${userA}`, tokenA);
    check('L2 missed-calls own → 200 (array)', r.status === 200 && Array.isArray(r.json), `status=${r.status} type=${Array.isArray(r.json) ? 'array' : typeof r.json}`);
  }
}

// ============ L2b. VOICE-CONFIG GET (C3 — PG-backed read) ============
{
  const r = await get(`/v1/tenants/${TEST_TENANT}/voice-config`);
  const shape = r.json && typeof r.json.voiceFeatureEnabled === 'boolean' && typeof r.json.voiceOpenCalling === 'boolean';
  check('L2b voice-config GET 200 + shape', r.status === 200 && shape,
    `status=${r.status} body=${r.json ? JSON.stringify(r.json) : 'null'}`);
}

// ============ L5. KILL-SWITCH CONFIG (Gateway admin API — restore cuối run) ============
if (SECRET && GW) {
  const adminToken = mintJwt('11111111-1111-4111-8111-000000000001', '00000000-0000-0000-0000-000000000000', 'SystemAdmin');
  async function putConfig(feature, open) {
    const res = await fetch(`${GW}/api/admin/voice-config/${TEST_TENANT}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${adminToken}` },
      body: JSON.stringify({ voiceFeatureEnabled: feature, voiceOpenCalling: open })
    });
    let json = null;
    try { json = await res.json(); } catch { /* noop */ }
    return { status: res.status, json };
  }
  try {
    const off = await putConfig(false, false);
    check('L5 kill-switch OFF qua admin API → 200', off.status === 200 && off.json?.voiceFeatureEnabled === false,
      `status=${off.status} feature=${off.json?.voiceFeatureEnabled}`);
    const readOff = await get(`/v1/tenants/${TEST_TENANT}/voice-config`);
    check('L5 voice service đọc OFF (PG-backed)', readOff.status === 200 && readOff.json?.voiceFeatureEnabled === false,
      `status=${readOff.status} feature=${readOff.json?.voiceFeatureEnabled}`);
  } finally {
    const on = await putConfig(true, false);
    check('L5 restore ON (cleanup)', on.status === 200 && on.json?.voiceFeatureEnabled === true,
      `status=${on.status} feature=${on.json?.voiceFeatureEnabled}`);
  }
} else {
  warn('L5 kill-switch config toggle', 'cần VOICE_JWT_SECRET + GW_URL (SystemAdmin mint) — bỏ qua');
}

// ============ REPORT ============
const summary = {
  ts: new Date().toISOString(),
  service: 'vanan-voice',
  total: results.length,
  pass: results.filter(r => r.pass === true).length,
  fail: results.filter(r => r.pass === false).length,
  skipped: results.filter(r => r.pass === null).length,
  results
};
writeFileSync(new URL('./voice-rv-report.json', import.meta.url), JSON.stringify(summary, null, 2));
console.log(`\n=== RV VOICE: ${summary.pass}/${summary.total} PASS · ${summary.fail} FAIL · ${summary.skipped} SKIP ===`);
process.exit(summary.fail > 0 ? 1 : 0);