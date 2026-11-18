import { test, expect } from '@playwright/test';

// Storefront metering deny gate (Gate 4 — UI behavior change → E2E bắt buộc):
// Store.razor gọi GET /api/public/metering/check?serviceCode=store.visit trước khi render.
//  - allowed:false (ví cạn + rate card Deny) → "Trang web này tạm ngưng phục vụ", KHÔNG render store.
//  - allowed:true / endpoint lỗi → store render bình thường (fail-open G9).
// Search results (Directory/timlathay portal) KHÔNG bị ảnh hưởng — chỉ trang storefront bị gate.
//
// Env: STORE_GATE_TEST_STORE_URL — full storefront URL (vd https://<host>/store/<slug>).
// Spec skip khi env chưa set (RV-gated, giống ai-chat-widget.spec.ts).

const STORE_URL = process.env.STORE_GATE_TEST_STORE_URL ?? '';

async function mockCheck(page: any, allowed: boolean | 'error') {
  await page.route('**/api/public/metering/check**', async (route: any) => {
    if (allowed === 'error') {
      await route.abort('failed');
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ allowed }),
    });
  });
}

test.describe('Storefront metering deny gate', () => {
  test.beforeEach(async ({}, testInfo) => {
    if (!STORE_URL) testInfo.skip(true, 'STORE_GATE_TEST_STORE_URL chưa set — RV-gated');
  });

  test('allowed=false → trang tạm ngưng phục vụ, không render storefront', async ({ page }) => {
    await mockCheck(page, false);
    await page.goto(STORE_URL, { waitUntil: 'domcontentloaded' });

    await expect(page.getByText('tạm ngưng phục vụ')).toBeVisible({ timeout: 30000 });
    // Storefront content must NOT render
    await expect(page.locator('.store-page')).toHaveCount(0);
    await expect(page.locator('.hero-title')).toHaveCount(0);
    // store.visit beacon must NOT fire for a suspended storefront
    const visitCalls: string[] = [];
    page.on('request', (req) => { if (req.url().includes('/api/public/metering/event')) visitCalls.push(req.url()); });
    await page.waitForTimeout(3000);
    expect(visitCalls.length).toBe(0);
  });

  test('allowed=true → storefront render bình thường', async ({ page }) => {
    await mockCheck(page, true);
    await page.goto(STORE_URL, { waitUntil: 'domcontentloaded' });

    await expect(page.locator('.store-page, .store-not-found, .store-loading').first())
      .toBeVisible({ timeout: 30000 });
    await expect(page.getByText('tạm ngưng phục vụ')).toHaveCount(0);
  });

  test('check endpoint lỗi → fail-open, storefront vẫn render', async ({ page }) => {
    await mockCheck(page, 'error');
    await page.goto(STORE_URL, { waitUntil: 'domcontentloaded' });

    await expect(page.getByText('tạm ngưng phục vụ')).toHaveCount(0, { timeout: 30000 });
    // Give WASM time to finish initial load, then assert store or not-found rendered
    await expect(page.locator('.store-page, .store-not-found').first())
      .toBeVisible({ timeout: 30000 });
  });
});
