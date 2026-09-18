import { test, expect } from '@playwright/test';
import { injectGpsMock } from './helpers/gps-mock';

/**
 * Realtime Platform P5/P6 (2026-09-18): Shop chat E2E.
 * Customer ↔ shop conversation on /store/by-id/{tenantId} (subject Shop/{tenantId})
 * + the shop-inbox list endpoint. Guest identity = X-Customer-Device-Id (P1 D6).
 *
 * Prerequisites:
 * - Gateway deployed with P5 (RealtimeController Shop surface + MessagingHub/TrackingHub)
 * - KhachLink deployed with P4/P5 (realtime.js RCL asset + store chat section)
 *
 * Tenant: "Cafe Tân Quy" (Vạn An Cafe HKD Group 1 area) from the production directory.
 */
const BASE_URL = 'https://diemthuong2.khachvip.online';
const GATEWAY = 'https://api2.khachvip.online';
const TENANT_ID = '7c021960-6d6c-4de4-88a1-cf8373184f49';
const FRESH_DEVICE = crypto.randomUUID();

test('P5-1: shop conversation history returns 401 without identity', async ({ request }) => {
  const resp = await request.get(`${GATEWAY}/api/realtime/conversations/Shop/${TENANT_ID}`);
  expect(resp.status()).toBe(401);
});

test('P5-2: shop inbox endpoint returns 401 without identity', async ({ request }) => {
  const resp = await request.get(`${GATEWAY}/api/realtime/shop/conversations`);
  expect(resp.status()).toBe(401);
});

test('P5-3: a fresh guest device CREATES the shop conversation (P6 chicken-egg fix)', async ({ request }) => {
  // Before the P6 fix this returned 403 forever — a first-time visitor had no
  // participant row, so the conversation was never created.
  const resp = await request.get(
    `${GATEWAY}/api/realtime/conversations/Shop/${TENANT_ID}`,
    { headers: { 'X-Customer-Device-Id': FRESH_DEVICE } },
  );
  expect(resp.status()).toBe(200);
  const body = await resp.json();
  expect(Array.isArray(body.messages)).toBe(true);
});

test('P5-4: guest sends + reads back a shop message', async ({ request }) => {
  const content = `RV-P6 shop chat ${Date.now()}`;
  const send = await request.post(
    `${GATEWAY}/api/realtime/conversations/messages`,
    {
      headers: { 'X-Customer-Device-Id': FRESH_DEVICE, 'Content-Type': 'application/json' },
      data: { subjectType: 'Shop', subjectId: TENANT_ID, content },
    },
  );
  expect(send.status()).toBe(200);
  const sent = await send.json();
  expect(sent.messageId).toBeTruthy();

  const history = await request.get(
    `${GATEWAY}/api/realtime/conversations/Shop/${TENANT_ID}`,
    { headers: { 'X-Customer-Device-Id': FRESH_DEVICE } },
  );
  expect(history.status()).toBe(200);
  const body = await history.json();
  const found = (body.messages ?? []).some((m: any) => m.content === content);
  expect(found).toBe(true);
});

test('P5-5: a DIFFERENT device never sees the first guest\'s messages (history filter)', async ({ request }) => {
  const otherDevice = crypto.randomUUID();
  const resp = await request.get(
    `${GATEWAY}/api/realtime/conversations/Shop/${TENANT_ID}`,
    { headers: { 'X-Customer-Device-Id': otherDevice } },
  );
  // The shop chat is a public widget — access is open, but the shared thread is filtered
  // per caller (own messages + shop replies only), so the other device sees an empty list.
  expect(resp.status()).toBe(200);
  const body = await resp.json();
  expect(Array.isArray(body.messages)).toBe(true);
  const foreign = (body.messages ?? []).some((m: any) => m.content?.includes('RV-P6 shop chat'));
  expect(foreign).toBe(false);
});

test('P5-6: realtime hubs exist (negotiate not 404)', async ({ request }) => {
  for (const hub of ['/hubs/messaging', '/hubs/tracking']) {
    const resp = await request.post(`${GATEWAY}${hub}/negotiate`, {
      headers: { 'Content-Type': 'application/json' },
      data: {},
    });
    expect(resp.status()).not.toBe(404);
  }
});

test('P5-7: store page renders shop chat + static map (UI)', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true });
  await injectGpsMock(context);
  const page = await context.newPage();
  const errors: string[] = [];
  page.on('pageerror', (err) => errors.push(err.message));
  try {
    await page.goto(`${BASE_URL}/store/by-id/${TENANT_ID}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(4000);

    // Static map (F9 — VanAnMap thay Google iframe); renders when the tenant enables
    // the location section — log the count, don't hard-assert (feature flag per tenant).
    const mapCount = await page.locator('#store-location-map').count();
    console.log(`store-location-map elements: ${mapCount}`);
    // Chat panel (P5) — core assertion of this spec
    await expect(page.locator('.chat-panel')).toHaveCount(1);

    // Guest sends a message from the UI → appears in the panel
    const content = `RV-P6 UI ${Date.now()}`;
    await page.locator('.chat-panel input.vanan-input').fill(content);
    await page.locator('.chat-panel button', { hasText: 'Gửi' }).first().click();
    await page.waitForTimeout(3000);
    await expect(page.locator('.chat-panel').getByText(content)).toHaveCount(1);

    // No console errors (realtime.js + leaflet loaded from _content)
    expect(errors.filter(e => !e.includes('favicon'))).toEqual([]);
  } finally {
    await context.close();
  }
});
