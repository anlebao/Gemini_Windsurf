import { test, expect } from '@playwright/test';

/**
 * Realtime Platform P4/P6 (2026-09-18): generic live-location surface E2E.
 * /api/realtime/location/* + /hubs/tracking + the migrated order-tracking page.
 *
 * Prerequisites:
 * - Gateway deployed with P3/P4 (RealtimeController + TrackingHub + OrderRealtimeAuthorizer)
 * - KhachLink deployed with P4 (VanAnMap via _content/VanAn.UI.Platform realtime.js)
 *
 * A real-order happy path needs a real order + its matching device id — covered by
 * unit tests + the existing community-delivery-flow spec; here we verify the
 * generic surface's auth/validation contracts and the migrated UI.
 */
const BASE_URL = 'https://diemthuong2.khachvip.online';
const GATEWAY = 'https://api2.khachvip.online';
const FAKE_ORDER = '00000000-0000-0000-0000-000000000099';
const DEVICE = crypto.randomUUID();

test('P4-1: latest location returns 401 without identity', async ({ request }) => {
  const resp = await request.get(`${GATEWAY}/api/realtime/location/Order/${FAKE_ORDER}/latest`);
  expect(resp.status()).toBe(401);
});

test('P4-2: a stranger cannot read an order location (403, default deny)', async ({ request }) => {
  const resp = await request.get(
    `${GATEWAY}/api/realtime/location/Order/${FAKE_ORDER}/latest`,
    { headers: { 'X-Customer-Device-Id': DEVICE } },
  );
  expect(resp.status()).toBe(403);
});

test('P4-3: (0,0) ping is rejected before access checks', async ({ request }) => {
  const resp = await request.post(
    `${GATEWAY}/api/realtime/location/ping`,
    {
      headers: { 'X-Customer-Device-Id': DEVICE, 'Content-Type': 'application/json' },
      data: { subjectType: 'Order', subjectId: FAKE_ORDER, lat: 0, lng: 0 },
    },
  );
  expect(resp.status()).toBe(400);
});

test('P4-4: invalid subject type is rejected (400)', async ({ request }) => {
  const resp = await request.post(
    `${GATEWAY}/api/realtime/location/ping`,
    {
      headers: { 'X-Customer-Device-Id': DEVICE, 'Content-Type': 'application/json' },
      data: { subjectType: 'NotASubject', subjectId: FAKE_ORDER, lat: 10.8, lng: 106.7 },
    },
  );
  expect(resp.status()).toBe(400);
});

test('P4-5: order tracking page loads with the migrated map (UI)', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true });
  const page = await context.newPage();
  const errors: string[] = [];
  page.on('pageerror', (err) => errors.push(err.message));
  try {
    await page.goto(`${BASE_URL}/order-tracking/${FAKE_ORDER}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);
    expect(page.url()).toContain('/order-tracking/');
    // Page renders (order not found state) without crashing on the migrated components.
    expect(errors.filter(e => !e.includes('favicon'))).toEqual([]);
    console.log('PASS: order-tracking page renders with P4 components, 0 page errors');
  } finally {
    await context.close();
  }
});
