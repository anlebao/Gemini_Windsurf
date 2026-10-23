// metering-rv-sweep.mjs — RV sweep TOÀN DIỆN cho vanan-metering (P1–P7) trên production.
// Pattern: booking-rv-sweep.mjs — chạy 1 lần thu TOÀN BỘ FAIL, phân tích theo cụm sau.
//
// Usage:
//   set METERING_CALLER_KEY=...   (key caller shoperp-a — KHÔNG hardcode)
//   set METERING_ADMIN_KEY=...    (key admin sysadmin  — KHÔNG hardcode)
//   node metering-rv-sweep.mjs [--base https://app2.khachvip.online/admin/metering/api]
//
// Nguyên tắc an toàn production:
//   - Rate-card test dùng serviceCode `rv.sweep.<ts>` — append-only nhưng deactivate cuối run.
//   - Wallet/topup test dùng tenant GUID RANDOM (wallet chấp nhận opaque tenantId) — không đụng tenant thật.
//   - Usage events là append-only facts — dùng tenant random + service rv.* → không ảnh hưởng billing thật.
//   - KHÔNG in giá trị key; chỉ in callerId (đã public trong CallerKeys).

const BASE = process.argv.find(a => a.startsWith('--base'))?.split('=')[1]
  || process.env.METERING_BASE
  || 'https://app2.khachvip.online/admin/metering/api';
const PUBLIC_BASE = process.env.PUBLIC_BASE || 'https://api2.khachvip.online';
const CALLER_ID = process.env.METERING_CALLER_ID || 'shoperp-a';
const ADMIN_ID = process.env.METERING_ADMIN_ID || 'sysadmin';
const CALLER_KEY = process.env.METERING_CALLER_KEY;
const ADMIN_KEY = process.env.METERING_ADMIN_KEY;

if (!CALLER_KEY || !ADMIN_KEY) {
  console.error('Thiếu METERING_CALLER_KEY / METERING_ADMIN_KEY env — extract từ VPS .env.metering trước.');
  process.exit(2);
}

const results = [];
function check(name, pass, detail = '') {
  results.push({ name, pass: !!pass, detail: String(detail).slice(0, 300) });
  console.log(`[${pass ? 'PASS' : 'FAIL'}] ${name}${detail ? ` — ${String(detail).slice(0, 160)}` : ''}`);
}

async function req(method, path, { auth = 'caller', body, headers = {}, raw } = {}) {
  const h = { ...headers };
  if (auth === 'caller') { h['X-Caller-Id'] = CALLER_ID; h['X-Metering-Key'] = CALLER_KEY; }
  if (auth === 'admin') { h['X-Caller-Id'] = ADMIN_ID; h['X-Metering-Key'] = ADMIN_KEY; }
  if (body !== undefined) h['Content-Type'] = 'application/json';
  const res = await fetch(`${BASE}${path}`, {
    method, headers: h,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  if (raw) return res;
  let json = null;
  try { json = await res.json(); } catch { /* non-json */ }
  return { status: res.status, json };
}

const idem = () => `rv-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
const nowIso = () => new Date().toISOString();
const SVC = `rv.sweep.${Date.now()}`;
const TENANT = crypto.randomUUID();
const TENANT2 = crypto.randomUUID();

// ============ A. HEALTH + AUTH ============
{
  const r = await fetch(`${BASE}/v1/health`);
  check('A1 health 200 (no auth)', r.status === 200, `status=${r.status}`);
}
{
  const r = await fetch(`${BASE}/v1/rate-cards?includeInactive=false`);
  check('A2 no-auth → 401', r.status === 401, `status=${r.status}`);
}
{
  const r = await fetch(`${BASE}/v1/rate-cards?includeInactive=false`, {
    headers: { 'X-Caller-Id': CALLER_ID, 'X-Metering-Key': 'wrong-key-xyz' },
  });
  check('A3 bad-key → 401', r.status === 401, `status=${r.status}`);
}
{
  const r = await req('GET', '/v1/auth/check', { auth: 'caller' });
  check('A4 caller auth → callerId', r.status === 200 && r.json?.callerId === CALLER_ID, `status=${r.status} caller=${r.json?.callerId}`);
}
{
  const r = await req('GET', '/v1/auth/check', { auth: 'admin' });
  check('A5 admin auth → callerId', r.status === 200 && r.json?.callerId === ADMIN_ID, `status=${r.status} caller=${r.json?.callerId}`);
}

// ============ B. NON-ADMIN 403 ============
{
  const r = await req('POST', '/v1/rate-cards', { auth: 'caller', body: { serviceCode: SVC, meterUnit: 'call', creditRate: 1 } });
  check('B1 non-admin POST rate-cards → 403', r.status === 403, `status=${r.status}`);
}
{
  const r = await req('POST', `/v1/wallets/${TENANT}/grants`, { auth: 'caller', body: { credits: 1, reason: 'rv' } });
  check('B2 non-admin POST grants → 403', r.status === 403, `status=${r.status}`);
}
{
  const r = await req('PUT', '/v1/topup-config', { auth: 'caller', body: { bankCode: 'x', bankName: 'x', accountNumber: 'x', accountName: 'x' } });
  check('B3 non-admin PUT topup-config → 403', r.status === 403, `status=${r.status}`);
}
{
  const r = await req('POST', `/v1/topups/${crypto.randomUUID()}/confirm`, { auth: 'caller', body: { credits: 1 } });
  check('B4 non-admin POST confirm → 403', r.status === 403, `status=${r.status}`);
}

// ============ C. RATE CARD (admin) ============
let cardId, cardV2;
{
  const r = await req('POST', '/v1/rate-cards', {
    auth: 'admin',
    body: { serviceCode: SVC, meterUnit: 'call', creditRate: 2, enforcement: 'Deny', effectiveFrom: new Date(Date.now() - 60000).toISOString(), label: 'RV sweep', auditNote: 'rv-sweep' },
  });
  cardId = r.json?.id;
  check('C1 admin POST rate-card → 201', r.status === 201 && !!cardId, `status=${r.status} id=${cardId}`);
}
{
  const r = await req('GET', `/v1/rate-cards/resolve?serviceCode=${SVC}`, { auth: 'caller' });
  check('C2 resolve → rate=2 Deny', r.status === 200 && r.json?.creditRate === 2 && r.json?.enforcement === 'Deny', `status=${r.status} ${JSON.stringify(r.json)}`);
}
{
  const r = await req('PATCH', `/v1/rate-cards/${cardId}`, {
    auth: 'admin',
    body: { creditRate: 5, effectiveFrom: new Date(Date.now() - 30000).toISOString(), auditNote: 'rv-sweep patch' },
  });
  cardV2 = r.json?.id;
  check('C3 PATCH → new version id', (r.status === 200 || r.status === 201) && cardV2 && cardV2 !== cardId, `status=${r.status} v2=${cardV2}`);
}
{
  const r = await req('GET', `/v1/rate-cards/resolve?serviceCode=${SVC}`, { auth: 'caller' });
  check('C4 resolve head → rate=5', r.status === 200 && r.json?.creditRate === 5, `status=${r.status} rate=${r.json?.creditRate}`);
}
{
  const r = await req('GET', '/v1/rate-cards?includeInactive=true', { auth: 'caller' });
  const versions = (r.json ?? []).filter(v => v.serviceCode === SVC);
  check('C5 history append-only ≥2 versions', r.status === 200 && versions.length >= 2, `status=${r.status} versions=${versions.length}`);
}
{
  const r = await req('GET', `/v1/rate-cards/resolve?serviceCode=${SVC}&atUtc=${encodeURIComponent(new Date(Date.now() - 45000).toISOString())}`, { auth: 'caller' });
  check('C6 resolve historical → rate=2', r.status === 200 && r.json?.creditRate === 2, `status=${r.status} rate=${r.json?.creditRate}`);
}
{
  const r = await req('GET', `/v1/rate-cards/resolve?serviceCode=rv.unpriced.${Date.now()}`, { auth: 'caller' });
  check('C7 resolve missing → resolved:false', r.status === 200 && r.json?.resolved === false, `status=${r.status} ${JSON.stringify(r.json)}`);
}

// ============ D. WALLET + DENY + ALERTS + LEDGER ============
{
  const r = await req('GET', `/v1/wallets/${TENANT}`, { auth: 'caller' });
  check('D1 wallet missing → 0-balance view', r.status === 200 && r.json?.balance === 0, `status=${r.status} ${JSON.stringify(r.json)}`);
}
{
  const r = await req('POST', `/v1/wallets/${TENANT}/grants`, { auth: 'admin', body: { credits: 100, reason: 'rv-sweep grant' } });
  check('D2 admin grant 100 → 200/201', r.status === 200 || r.status === 201, `status=${r.status}`);
}
{
  const r = await req('GET', `/v1/wallets/${TENANT}`, { auth: 'caller' });
  check('D3 wallet balance=100 granted=100', r.status === 200 && r.json?.balance === 100 && r.json?.grantedTotal === 100, `bal=${r.json?.balance} granted=${r.json?.grantedTotal} alert=${r.json?.alertLevel}`);
}
{
  const r = await req('GET', `/v1/wallets/${TENANT}/check?serviceCode=${SVC}&estimate=20`, { auth: 'caller' });
  check('D4 check estimate=20 → required=100 deny=false', r.status === 200 && r.json?.required === 100 && r.json?.deny === false, `status=${r.status} ${JSON.stringify(r.json)}`);
}
{
  const r = await req('GET', `/v1/wallets/${TENANT}/check?serviceCode=${SVC}&estimate=21`, { auth: 'caller' });
  check('D5 check estimate=21 → deny=true', r.status === 200 && r.json?.deny === true, `status=${r.status} deny=${r.json?.deny}`);
}
// Ingest priced events: rate 5/call. qty10→50, qty8→40, qty2→10 → balance 0.
const evKey1 = idem(), evKey2 = idem(), evKey3 = idem(), evKey4 = idem();
let ev1Id;
{
  const r = await req('POST', '/v1/usage/events', {
    auth: 'caller',
    headers: { 'Idempotency-Key': evKey1 },
    body: { tenantId: TENANT, serviceCode: SVC, quantity: 10, unit: 'call', occurredAtUtc: nowIso() },
  });
  ev1Id = r.json?.eventId;
  check('D6 ingest qty10 → 201', r.status === 201 && !!ev1Id, `status=${r.status} ev=${ev1Id}`);
}
{
  const r = await req('GET', `/v1/wallets/${TENANT}`, { auth: 'caller' });
  check('D7 balance=50 + warn50', r.status === 200 && r.json?.balance === 50 && r.json?.alertLevel === 'warn50', `bal=${r.json?.balance} alert=${r.json?.alertLevel}`);
}
{
  const r = await req('POST', '/v1/usage/events', {
    auth: 'caller', headers: { 'Idempotency-Key': evKey2 },
    body: { tenantId: TENANT, serviceCode: SVC, quantity: 8, unit: 'call', occurredAtUtc: nowIso() },
  });
  check('D8 ingest qty8 → 201', r.status === 201, `status=${r.status}`);
}
{
  const r = await req('GET', `/v1/wallets/${TENANT}`, { auth: 'caller' });
  check('D9 balance=10 + warn90', r.status === 200 && r.json?.balance === 10 && r.json?.alertLevel === 'warn90', `bal=${r.json?.balance} alert=${r.json?.alertLevel}`);
}
{
  const r = await req('POST', '/v1/usage/events', {
    auth: 'caller', headers: { 'Idempotency-Key': evKey3 },
    body: { tenantId: TENANT, serviceCode: SVC, quantity: 2, unit: 'call', occurredAtUtc: nowIso() },
  });
  check('D10 ingest qty2 → 201', r.status === 201, `status=${r.status}`);
}
{
  const r = await req('GET', `/v1/wallets/${TENANT}`, { auth: 'caller' });
  check('D11 balance=0 + depleted', r.status === 200 && r.json?.balance === 0 && r.json?.alertLevel === 'depleted', `bal=${r.json?.balance} alert=${r.json?.alertLevel}`);
}
{
  const r = await req('POST', '/v1/usage/events', {
    auth: 'caller', headers: { 'Idempotency-Key': evKey4 },
    body: { tenantId: TENANT, serviceCode: SVC, quantity: 1, unit: 'call', occurredAtUtc: nowIso() },
  });
  check('D12 over-balance ingest → 201 (fact-only, unpaid)', r.status === 201, `status=${r.status}`);
}
{
  const r = await req('GET', `/v1/wallets/${TENANT}`, { auth: 'caller' });
  check('D13 balance stays 0 (no negative)', r.status === 200 && r.json?.balance === 0, `bal=${r.json?.balance}`);
}
{
  const r = await req('GET', `/v1/wallets/${TENANT}/check?serviceCode=${SVC}&estimate=1`, { auth: 'caller' });
  check('D14 check estimate=1 at 0 → deny=true', r.status === 200 && r.json?.deny === true, `deny=${r.json?.deny}`);
}
// Idempotent replay — same key must return same eventId, no double deduct.
{
  const r = await req('POST', '/v1/usage/events', {
    auth: 'caller', headers: { 'Idempotency-Key': evKey1 },
    body: { tenantId: TENANT, serviceCode: SVC, quantity: 10, unit: 'call', occurredAtUtc: nowIso() },
  });
  check('D15 replay → same eventId', r.status === 200 && r.json?.eventId === ev1Id && r.json?.idempotentReplay === true, `status=${r.status} ev=${r.json?.eventId}`);
}
{
  const r = await req('GET', `/v1/wallets/${TENANT}`, { auth: 'caller' });
  check('D16 replay no double-deduct (bal=0)', r.status === 200 && r.json?.balance === 0, `bal=${r.json?.balance}`);
}
// Grant → cycle reset, alerts refire.
{
  const r = await req('POST', `/v1/wallets/${TENANT}/grants`, { auth: 'admin', body: { credits: 10, reason: 'rv cycle reset' } });
  check('D17 grant → 200/201', r.status === 200 || r.status === 201, `status=${r.status}`);
}
{
  const r = await req('GET', `/v1/wallets/${TENANT}`, { auth: 'caller' });
  // GrantedTotal=110, balance=10 → 9% → warn90 (new cycle refires).
  check('D18 grant resets cycle → warn90 refires', r.status === 200 && r.json?.balance === 10 && r.json?.alertLevel === 'warn90', `bal=${r.json?.balance} alert=${r.json?.alertLevel}`);
}
{
  const r = await req('GET', `/v1/wallets/${TENANT}/ledger?pageSize=50`, { auth: 'caller' });
  const items = r.json?.items ?? [];
  const grants = items.filter(i => i.type === 'Grant').length;
  const deducts = items.filter(i => i.type === 'Deduct').length;
  const neg = items.some(i => i.balanceAfter < 0);
  check('D19 ledger append-only: 2 grants + 3 deducts, no negative', r.status === 200 && grants === 2 && deducts === 3 && !neg, `grants=${grants} deducts=${deducts} neg=${neg}`);
}

// ============ E. UNPRICED + METERONLY ============
{
  const UNSVC = `rv.unpriced.${Date.now()}`;
  const r = await req('POST', '/v1/usage/events', {
    auth: 'caller', headers: { 'Idempotency-Key': idem() },
    body: { tenantId: TENANT2, serviceCode: UNSVC, quantity: 5, unit: 'x', occurredAtUtc: nowIso() },
  });
  check('E1 unpriced ingest → 201', r.status === 201, `status=${r.status}`);
}
{
  const r = await req('GET', `/v1/wallets/${TENANT2}/check?serviceCode=${SVC}&estimate=1000`, { auth: 'caller' });
  check('E2 zero-balance check → deny (priced deny card)', r.status === 200 && r.json?.deny === true, `deny=${r.json?.deny}`);
}
{
  // MeterOnly card → never deny even at 0 balance.
  const MOSVC = `rv.meteronly.${Date.now()}`;
  await req('POST', '/v1/rate-cards', {
    auth: 'admin',
    body: { serviceCode: MOSVC, meterUnit: 'x', creditRate: 99, enforcement: 'MeterOnly', effectiveFrom: new Date(Date.now() - 60000).toISOString() },
  });
  const r = await req('GET', `/v1/wallets/${TENANT2}/check?serviceCode=${MOSVC}&estimate=100`, { auth: 'caller' });
  check('E3 MeterOnly card → deny=false at 0 balance', r.status === 200 && r.json?.deny === false, `deny=${r.json?.deny}`);
}

// ============ F. BATCH ============
{
  const k1 = idem(), k2 = idem();
  const r = await req('POST', '/v1/usage/events:batch', {
    auth: 'caller',
    body: [
      { idempotencyKey: k1, tenantId: TENANT2, serviceCode: 'rv.batch', quantity: 1, unit: 'x', occurredAtUtc: nowIso() },
      { idempotencyKey: k1, tenantId: TENANT2, serviceCode: 'rv.batch', quantity: 1, unit: 'x', occurredAtUtc: nowIso() }, // dup in batch
      { idempotencyKey: k2, tenantId: TENANT2, serviceCode: 'rv.batch', quantity: 0, unit: 'x', occurredAtUtc: nowIso() },  // qty=0 rejected
    ],
  });
  const rs = r.json?.results ?? [];
  check('F1 batch → accepted+replayed+rejected', r.status === 200 && r.json?.accepted === 1 && r.json?.replayed === 1 && r.json?.rejected === 1, `status=${r.status} ${JSON.stringify(r.json).slice(0, 200)}`);
}
{
  const r = await req('POST', '/v1/usage/events:batch', { auth: 'caller', body: [] });
  check('F2 empty batch → 400', r.status === 400, `status=${r.status}`);
}

// ============ G. TOPUP ============
let topupId, topupRef;
{
  // Keep existing config if set; PUT is singleton upsert — re-save same values to not clobber prod config.
  const cur = await req('GET', '/v1/topup-config', { auth: 'admin' });
  if (cur.status === 200) {
    check('G1 topup-config already set (skip PUT)', true, `bank=${cur.json?.bankCode}`);
  } else {
    const r = await req('PUT', '/v1/topup-config', {
      auth: 'admin',
      body: { bankCode: 'MB', bankName: 'MB Bank', accountNumber: '0000000000', accountName: 'VAN AN RV' },
    });
    check('G1 PUT topup-config → 200', r.status === 200, `status=${r.status}`);
  }
}
{
  const key = idem();
  const r = await req('POST', '/v1/topups', {
    auth: 'caller', headers: { 'Idempotency-Key': key },
    body: { tenantId: TENANT2, amountVnd: 50000 },
  });
  topupId = r.json?.id; topupRef = r.json?.referenceCode;
  check('G2 create topup → 201 + TOPUP- ref', r.status === 201 && /^TOPUP-[A-Z0-9]{8}$/.test(topupRef ?? ''), `status=${r.status} ref=${topupRef}`);
  check('G3 qrPayload = vietqr.io URL', typeof r.json?.qrPayload === 'string' && r.json.qrPayload.includes('img.vietqr.io/image/') && r.json.qrPayload.includes(topupRef), `${r.json?.qrPayload?.slice(0, 120)}`);
  check('G4 status=pending + expiresAt', r.json?.status === 'pending' && !!r.json?.expiresAtUtc, `status=${r.json?.status}`);
  // Replay dedupe
  const r2 = await req('POST', '/v1/topups', {
    auth: 'caller', headers: { 'Idempotency-Key': key },
    body: { tenantId: TENANT2, amountVnd: 99999 },
  });
  check('G5 idem replay → same id', (r2.status === 200 || r2.status === 201) && r2.json?.id === topupId, `status=${r2.status} id=${r2.json?.id}`);
}
{
  const r = await req('POST', '/v1/topups', { auth: 'caller', body: { tenantId: TENANT2, amountVnd: 1000 } });
  check('G6 missing Idempotency-Key → 400', r.status === 400, `status=${r.status}`);
}
{
  const balBefore = (await req('GET', `/v1/wallets/${TENANT2}`, { auth: 'caller' })).json?.balance ?? -1;
  const r = await req('POST', `/v1/topups/${topupId}/confirm`, { auth: 'admin', body: { credits: 7, note: 'rv confirm' } });
  check('G7 admin confirm → confirmed', r.status === 200 && r.json?.status === 'confirmed', `status=${r.status} ${JSON.stringify(r.json)}`);
  const balAfter = (await req('GET', `/v1/wallets/${TENANT2}`, { auth: 'caller' })).json?.balance ?? -1;
  check('G8 wallet +7 credits', balBefore === 0 && balAfter === 7, `before=${balBefore} after=${balAfter}`);
  const r2 = await req('POST', `/v1/topups/${topupId}/confirm`, { auth: 'admin', body: { credits: 7 } });
  const balAfter2 = (await req('GET', `/v1/wallets/${TENANT2}`, { auth: 'caller' })).json?.balance ?? -1;
  check('G9 confirm replay idempotent (no double grant)', r2.status === 200 && balAfter2 === 7, `status=${r2.status} bal=${balAfter2}`);
}
{
  // Reject flow on second topup.
  const r1 = await req('POST', '/v1/topups', {
    auth: 'caller', headers: { 'Idempotency-Key': idem() },
    body: { tenantId: TENANT2, amountVnd: 20000 },
  });
  const id2 = r1.json?.id;
  const rr = await req('POST', `/v1/topups/${id2}/reject`, { auth: 'admin', body: { reason: 'rv reject' } });
  check('G10 reject → rejected', rr.status === 200 && rr.json?.status === 'rejected', `status=${rr.status} ${JSON.stringify(rr.json)}`);
  const rc = await req('POST', `/v1/topups/${id2}/confirm`, { auth: 'admin', body: { credits: 5 } });
  check('G11 confirm rejected topup → 409', rc.status === 409, `status=${rc.status}`);
}
{
  const r = await req('GET', `/v1/topups?tenantId=${TENANT2}`, { auth: 'caller' });
  const items = r.json?.items ?? [];
  check('G12 list topups → 2 items for tenant', r.status === 200 && items.length === 2, `status=${r.status} n=${items.length}`);
}
{
  const r = await req('GET', `/v1/wallets/${TENANT2}/ledger?pageSize=50`, { auth: 'caller' });
  const topupGrant = (r.json?.items ?? []).find(i => i.topupRequestId === topupId);
  check('G13 ledger links grant→topup', r.status === 200 && !!topupGrant && topupGrant.credits === 7, `${JSON.stringify(topupGrant)}`);
}

// ============ H. EVENTS LIST + SUMMARY ============
{
  const r = await req('GET', `/v1/usage/events?tenantId=${TENANT}&pageSize=10`, { auth: 'caller' });
  const items = r.json?.items ?? [];
  const sorted = items.every((v, i, a) => i === 0 || a[i - 1].occurredAtUtc >= v.occurredAtUtc);
  check('H1 events list desc', r.status === 200 && items.length >= 4 && sorted, `status=${r.status} n=${items.length}`);
}
{
  const period = nowIso().slice(0, 7);
  const r = await req('GET', `/v1/usage/summary?period=${period}&tenantId=${TENANT}`, { auth: 'caller' });
  const row = (r.json?.items ?? []).find(i => i.serviceCode === SVC);
  check('H2 summary credits = qty×rate', r.status === 200 && row && row.quantity === 21 && row.credits === 105, `status=${r.status} ${JSON.stringify(row)}`);
}
{
  const r = await req('GET', '/v1/usage/summary?period=bad-period', { auth: 'caller' });
  check('H3 bad period → 400', r.status === 400, `status=${r.status}`);
}

// ============ I. PUBLIC ENDPOINT (gateway) ============
let pubTenant = crypto.randomUUID();
{
  const r = await fetch(`${PUBLIC_BASE}/api/public/metering/event`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', 'User-Agent': 'rv-sweep-browser/1.0' },
    body: JSON.stringify({ tenantId: pubTenant, serviceCode: 'store.visit', quantity: 1, unit: 'visit', occurredAtUtc: nowIso(), idempotencyKey: `rvpub-${Date.now()}` }),
  });
  check('I1 public store.visit → 200 accepted', r.status === 200 && (await r.json())?.accepted === true, `status=${r.status}`);
}
{
  const r = await fetch(`${PUBLIC_BASE}/api/public/metering/event`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', 'User-Agent': 'curl/8.0 rv-probe' },
    body: JSON.stringify({ tenantId: crypto.randomUUID(), serviceCode: 'store.visit', quantity: 1, occurredAtUtc: nowIso() }),
  });
  check('I2 bot UA → 200 not forwarded', r.status === 200 && (await r.json())?.accepted === false, `status=${r.status}`);
}
{
  const r = await fetch(`${PUBLIC_BASE}/api/public/metering/event`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', 'User-Agent': 'rv-browser' },
    body: JSON.stringify({ tenantId: crypto.randomUUID(), serviceCode: 'chat.message', quantity: 1, occurredAtUtc: nowIso() }),
  });
  check('I3 non-whitelist serviceCode → 400', r.status === 400, `status=${r.status}`);
}
{
  const r = await fetch(`${PUBLIC_BASE}/api/public/metering/event`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', 'User-Agent': 'rv-browser' },
    body: JSON.stringify({ tenantId: crypto.randomUUID(), serviceCode: 'store.visit', quantity: 5, occurredAtUtc: nowIso() }),
  });
  check('I4 quantity!=1 → 400', r.status === 400, `status=${r.status}`);
}
// I5: verify public event actually landed in metering (caller=gateway).
{
  await new Promise(r => setTimeout(r, 4000)); // fire-and-forget forward
  const r = await req('GET', `/v1/usage/events?tenantId=${pubTenant}&serviceCode=store.visit&pageSize=5`, { auth: 'admin' });
  const items = r.json?.items ?? [];
  check('I5 public event landed (caller=gateway)', r.status === 200 && items.some(i => i.callerId === 'gateway'), `status=${r.status} n=${items.length} callers=${[...new Set(items.map(i => i.callerId))].join(',')}`);
}

// ============ J. RATE-CARD CLEANUP + NEGATIVE ============
{
  const r = await req('DELETE', `/v1/rate-cards/${cardV2 ?? cardId}`, { auth: 'admin' });
  check('J1 deactivate rate-card → 204', r.status === 204 || r.status === 200, `status=${r.status}`);
  const r2 = await req('GET', `/v1/rate-cards/resolve?serviceCode=${SVC}`, { auth: 'caller' });
  check('J2 resolve after deactivate → resolved:false', r2.status === 200 && r2.json?.resolved === false, `status=${r2.status} ${JSON.stringify(r2.json)}`);
}
{
  const r = await req('POST', '/v1/usage/events', {
    auth: 'caller', headers: { 'Idempotency-Key': idem() },
    body: { tenantId: TENANT, serviceCode: SVC, quantity: -5, unit: 'call', occurredAtUtc: nowIso() },
  });
  check('J3 negative quantity → 400', r.status === 400, `status=${r.status}`);
}
{
  const r = await req('POST', '/v1/usage/events', {
    auth: 'caller', // no Idempotency-Key
    body: { tenantId: TENANT, serviceCode: 'x', quantity: 1, unit: 'x', occurredAtUtc: nowIso() },
  });
  check('J4 missing Idempotency-Key → 400', r.status === 400, `status=${r.status}`);
}

// ============ REPORT ============
const fails = results.filter(r => !r.pass);
console.log(`\n================ SWEEP RESULT: ${results.length - fails.length}/${results.length} PASS — ${fails.length} FAIL ================`);
if (fails.length) {
  console.log('FAILURES:');
  for (const f of fails) console.log(`  ✗ ${f.name} — ${f.detail}`);
}
try {
  const { writeFileSync } = await import('fs');
  writeFileSync(new URL('./metering-rv-report.json', import.meta.url), JSON.stringify({ at: new Date().toISOString(), base: BASE, total: results.length, fails: fails.length, results }, null, 2));
} catch { /* report optional */ }
process.exit(fails.length ? 1 : 0);
