#!/usr/bin/env node
// ─────────────────────────────────────────────────────────────────────────────
// RV SWEEP — ATRS WAVE 2 (WebAuthn/step-up/security center) — PRODUCTION
// Sweep-first: chạy 1 lần thu TOÀN BỘ FAIL, phân tích theo nhóm root cause.
//
// Chạy:  ATRS_CUSTOMER_TOKEN=<token> node atrs-wave2-sweep.mjs
// Env:   ATRS_BASE (default https://api2.khachvip.online), ATRS_DIRECT (default http://34.126.90.228:5080)
//        ATRS_ORIGIN (default https://vanan.timlathay.com), ATRS_RPID (default timlathay.com)
// ─────────────────────────────────────────────────────────────────────────────
const BASE = process.env.ATRS_BASE ?? 'https://api2.khachvip.online';
const DIRECT = process.env.ATRS_DIRECT ?? 'http://34.126.90.228:5080';
const TOKEN = process.env.ATRS_CUSTOMER_TOKEN ?? '';
const ORIGIN = process.env.ATRS_ORIGIN ?? 'https://vanan.timlathay.com';
const RPID = process.env.ATRS_RPID ?? 'timlathay.com';
const API = `${BASE}/api/v1/account-trust`;

const results = [];
const check = (name, pass, detail = '') => {
  results.push({ name, pass: !!pass });
  console.log(`[${pass ? 'PASS' : 'FAIL'}] ${name}${detail ? ` — ${String(detail).slice(0, 200)}` : ''}`);
};
const sleep = (ms) => new Promise(r => setTimeout(r, ms));

async function call(method, path, { token = TOKEN, headers = {}, body } = {}) {
  const h = { ...headers };
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

async function main() {
  console.log(`ATRS Wave-2 sweep → ${API}`);
  console.log(`origin=${ORIGIN} rpid=${RPID} token=${TOKEN ? 'set' : 'MISSING'}`);
  if (!TOKEN) { console.error('ATRS_CUSTOMER_TOKEN required'); process.exit(2); }

  // ── L1a: Health ──────────────────────────────────────────────────────────
  try {
    const h = await fetch(`${DIRECT}/health`);
    check('L1 health direct :5080', h.status === 200, `status=${h.status}`);
  } catch (e) { check('L1 health direct :5080', false, e.message); }

  // ── L1b: Auth negatives ──────────────────────────────────────────────────
  for (const [label, path] of [
    ['me', '/me'], ['devices', '/devices'], ['credentials', '/webauthn/credentials'],
    ['rc-status', '/recovery-codes/status'],
  ]) {
    const noTok = await call('GET', path, { token: '' });
    check(`auth-neg ${label} no-token=401`, noTok.status === 401, `got ${noTok.status}`);
    const badTok = await call('GET', path, { token: 'invalid-token-xyz' });
    check(`auth-neg ${label} bad-token=401`, badTok.status === 401, `got ${badTok.status}`);
  }

  // ── L1c: Security center summary (/me = U06) ─────────────────────────────
  const me = await call('GET', '/me');
  check('GET /me 200', me.status === 200, `status=${me.status}`);
  if (me.status === 200) {
    const j = me.json ?? {};
    for (const f of ['trustSubjectId', 'status', 'assuranceState', 'activeDeviceCount', 'activePasskeyCount', 'hasRecoveryCodes'])
      check(`/me field ${f}`, j[f] !== undefined, `value=${JSON.stringify(j[f])}`);
  }

  // ── L1d: Device registry (U07/U08) ───────────────────────────────────────
  const devs = await call('GET', '/devices');
  check('GET /devices 200 array', devs.status === 200 && Array.isArray(devs.json), `status=${devs.status}`);

  const fakeId = '00000000-0000-0000-0000-0000000000ff';
  const devGet = await call('GET', `/devices/${fakeId}`);
  check('GET /devices/{fake} 404', devGet.status === 404, `got ${devGet.status}`);

  const renameNoStep = await call('PATCH', `/devices/${fakeId}/rename`, { body: { friendlyName: 'x' } });
  check('rename device no-stepup → 401/404 (not 500)', [401, 404, 403].includes(renameNoStep.status), `got ${renameNoStep.status}`);

  const revokeNoStep = await call('POST', `/devices/${fakeId}/revoke`, { body: {} });
  check('revoke device no-stepup → 401', revokeNoStep.status === 401, `got ${revokeNoStep.status}`);

  const lostNoStep = await call('POST', `/devices/${fakeId}/report-lost`);
  check('report-lost no-stepup → 401', lostNoStep.status === 401, `got ${lostNoStep.status}`);

  const revokeBadStep = await call('POST', `/devices/${fakeId}/revoke`, {
    headers: { 'X-Step-Up-Token': 'garbage.step.up' }, body: {},
  });
  check('revoke device bad-stepup → 401', revokeBadStep.status === 401, `got ${revokeBadStep.status}`);

  // ── L1e: WebAuthn ceremonies (API-level; real authenticator = L3/L5) ──────
  const creds = await call('GET', '/webauthn/credentials');
  check('GET /webauthn/credentials 200 array', creds.status === 200 && Array.isArray(creds.json), `status=${creds.status}`);

  const regBegin = await call('POST', '/webauthn/registration/begin', { body: { origin: ORIGIN, rpId: RPID } });
  check('registration/begin valid origin+rpid → 200', regBegin.status === 200, `status=${regBegin.status} ${JSON.stringify(regBegin.json ?? '').slice(0, 120)}`);
  const regChallenge = regBegin.json?.challenge;

  const regBeginBadOrigin = await call('POST', '/webauthn/registration/begin', {
    body: { origin: 'https://evil.example.com', rpId: RPID },
  });
  check('registration/begin evil origin → 400', regBeginBadOrigin.status === 400, `got ${regBeginBadOrigin.status}`);

  const regBeginBadRp = await call('POST', '/webauthn/registration/begin', {
    body: { origin: ORIGIN, rpId: 'evil.example.com' },
  });
  check('registration/begin mismatched rpId → 400', regBeginBadRp.status === 400, `got ${regBeginBadRp.status}`);

  const authBegin = await call('POST', '/webauthn/authentication/begin', { body: { origin: ORIGIN, rpId: RPID } });
  check('authentication/begin → 200|400 (no creds)', [200, 400].includes(authBegin.status), `got ${authBegin.status}`);

  // finish với attestation rác → phải reject (không 500)
  const regFinishBad = await call('POST', '/webauthn/registration/finish', {
    body: { challenge: regChallenge ?? 'x', origin: ORIGIN, rpId: RPID, attestationResponse: {}, deviceRecordId: null },
  });
  check('registration/finish garbage → 400 not 500', regFinishBad.status === 400, `got ${regFinishBad.status}`);

  // challenge replay: dùng lại challenge đã consume → reject
  if (regChallenge) {
    const regFinishReplay = await call('POST', '/webauthn/registration/finish', {
      body: { challenge: regChallenge, origin: ORIGIN, rpId: RPID, attestationResponse: {}, deviceRecordId: null },
    });
    check('registration/finish replayed challenge → 400', regFinishReplay.status === 400, `got ${regFinishReplay.status}`);
  }

  const credRevokeBadStep = await call('POST', `/webauthn/credentials/${fakeId}/revoke`);
  check('credential revoke no-stepup → 401', credRevokeBadStep.status === 401, `got ${credRevokeBadStep.status}`);

  // ── L1f: Recovery codes + step-up gate ───────────────────────────────────
  const rcStatus = await call('GET', '/recovery-codes/status');
  check('GET recovery-codes/status 200', rcStatus.status === 200, `status=${rcStatus.status}`);

  const genNoStep = await call('POST', '/recovery-codes/generate');
  check('generate no-stepup → 401', genNoStep.status === 401, `got ${genNoStep.status}`);

  const genBadStep = await call('POST', '/recovery-codes/generate', {
    headers: { 'X-Step-Up-Token': 'garbage.token.here' },
  });
  check('generate bad-stepup → 401', genBadStep.status === 401, `got ${genBadStep.status}`);

  const verifyWrong = await call('POST', '/recovery-codes/verify', { body: { code: 'XXXXX-XXXXX' } });
  check('verify wrong code → 400', verifyWrong.status === 400, `got ${verifyWrong.status}`);

  // ── L1g: Recovery request (Wave 1 surface regression) ────────────────────
  const idemKey = `rv-sweep-${Date.now()}`;
  const rrSubmit = await call('POST', '/recovery-requests', {
    body: { requestType: 0, idempotencyKey: idemKey },
  });
  check('recovery-requests submit → 200', rrSubmit.status === 200, `got ${rrSubmit.status} ${JSON.stringify(rrSubmit.json ?? '').slice(0, 120)}`);
  const rrId = rrSubmit.json?.request?.id;
  check('submit returns request.id', !!rrId, `id=${rrId}`);
  if (rrId) {
    const rrGet = await call('GET', `/recovery-requests/${rrId}`);
    check('recovery-requests get by id → 200', rrGet.status === 200, `got ${rrGet.status}`);
    const rrGetFake = await call('GET', `/recovery-requests/00000000-0000-0000-0000-0000000000ff`);
    check('recovery-requests get fake → 404 (anti-enum)', rrGetFake.status === 404, `got ${rrGetFake.status}`);
    const rrDup = await call('POST', '/recovery-requests', {
      body: { requestType: 0, idempotencyKey: idemKey },
    });
    const isIdempotent = rrDup.status === 200 && (rrDup.json?.existing === true || rrDup.json?.request?.id === rrId);
    check('recovery-requests idempotent replay → existing/same-id', isIdempotent, `got ${rrDup.status} existing=${rrDup.json?.existing}`);
  }
  const rrNoIdem = await call('POST', '/recovery-requests', { body: { requestType: 0 } });
  check('recovery-requests missing idempotencyKey → 400', rrNoIdem.status === 400, `got ${rrNoIdem.status}`);

  // ── Summary ──────────────────────────────────────────────────────────────
  const pass = results.filter(r => r.pass).length;
  const fail = results.filter(r => !r.pass);
  console.log(`\n═══ RESULT: ${pass}/${results.length} PASS ═══`);
  if (fail.length) {
    console.log('FAILURES:');
    fail.forEach(f => console.log(`  - ${f.name}`));
  }
  const report = { ts: new Date().toISOString(), base: API, total: results.length, pass, fail: fail.map(f => f.name), results };
  const fs = await import('node:fs');
  fs.writeFileSync('atrs-wave2-sweep-report.json', JSON.stringify(report, null, 2));
  console.log('report → atrs-wave2-sweep-report.json');
  process.exit(fail.length ? 1 : 0);
}

main().catch(e => { console.error('FATAL', e); process.exit(2); });
