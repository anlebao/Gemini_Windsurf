import { test, expect } from '@playwright/test';

/**
 * Realtime Platform RV — P1/P4/P6 on production: the REAL-order UI flow that the P4 Razor
 * string-param bug had silently broken (chat input stayed disabled → 401 literal token).
 *
 * Uses the production order 01a0afac-… (DELIVERY, completed) + its CustomerDeviceId
 * 81f43d82-… (same pair as the P1 RV). The device guid is injected into localStorage
 * before the page loads, exactly like a returning guest browser.
 */
const BASE_URL = 'https://diemthuong2.khachvip.online';
const ORDER = '01a0afac-f513-71d4-8719-796708cac905';
const DEVICE = '81f43d82-2486-4566-85cb-f439075c86c2';

test('RV-1: order-tracking page — chat panel ENABLED for the guest device (P6 fix)', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true });
  await context.addInitScript(([order, device]) => {
    localStorage.setItem('customer_device_id', device);
  }, [ORDER, DEVICE]);
  const page = await context.newPage();
  const errors: string[] = [];
  page.on('pageerror', (err) => errors.push(err.message));
  try {
    await page.goto(`${BASE_URL}/order-tracking/${ORDER}`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(4000);

    // Chat panel renders AND the input is enabled (was disabled since P4 — the literal
    // "_customerToken"/missing device header bug fixed in P6).
    await expect(page.locator('.chat-panel')).toHaveCount(1);
    await expect(page.locator('.chat-panel input.vanan-input')).toBeEnabled();

    // Guest sends a message through the UI → appears in the panel.
    const content = `RV-UI ${Date.now()}`;
    await page.locator('.chat-panel input.vanan-input').fill(content);
    await page.locator('.chat-panel button', { hasText: 'Gửi' }).first().click();
    await page.waitForTimeout(3000);
    await expect(page.locator('.chat-panel').getByText(content)).toHaveCount(1);
    console.log(`PASS: chat send via UI — "${content}"`);

    // Map renders for this DELIVERY order (leaflet tiles + markers).
    const mapCount = await page.locator('.leaflet-container').count();
    console.log(`PASS: map containers: ${mapCount}`);
    expect(mapCount).toBeGreaterThan(0);

    expect(errors.filter(e => !e.includes('favicon'))).toEqual([]);
    console.log('PASS: 0 page errors');
  } finally {
    await context.close();
  }
});
