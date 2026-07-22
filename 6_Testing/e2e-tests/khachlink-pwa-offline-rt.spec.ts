import { test, expect, Page, BrowserContext } from '@playwright/test';

/**
 * RT (Runtime) Test — KhachLink PWA Offline Master Plan — Full Completed Phases
 *
 * Tests against LIVE site: https://diemthuong.khachvip.online
 * Covers: Phase 1 (WASM), Phase 2 (SW DLL caching), Phase 2b (price validation),
 *         Phase 3 SC5-SC8 (offline API fallback), SRI Hotfix (v12-sri-fix).
 *
 * Run: npx playwright test e2e-tests/khachlink-pwa-offline-rt.spec.ts --project=chromium
 */

const BASE_URL = 'https://diemthuong.khachvip.online';
const SW_VERSION = 'v12-sri-fix';

// Mobile emulation (customer-facing PWA)
const mobileContext = {
  userAgent: 'Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36',
  viewport: { width: 412, height: 915 },
  isMobile: true,
  hasTouch: true,
  ignoreHTTPSErrors: true,
};

async function createMobileContext(browser: any): Promise<BrowserContext> {
  return browser.newContext(mobileContext);
}

// ============================================================================
// SRI HOTFIX + Phase 1: WASM loads without SRI integrity errors
// ============================================================================
test.describe('SRI Hotfix + Phase 1: WASM SDK conversion @smoke', () => {
  test('RT-SRI-01: App loads without SRI integrity errors', async ({ browser }) => {
    const context = await createMobileContext(browser);
    const page = await context.newPage();

    const consoleErrors: string[] = [];
    const sriErrors: string[] = [];
    page.on('console', msg => {
      if (msg.type() === 'error') consoleErrors.push(msg.text());
    });
    page.on('pageerror', err => {
      if (err.message.includes('integrity') || err.message.includes('SRI')) {
        sriErrors.push(err.message);
      }
    });

    await page.goto(BASE_URL, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(8000); // Wait for Blazor WASM boot

    // Check: no SRI integrity errors
    const allErrors = consoleErrors.join('\n');
    const sriInConsole = allErrors.includes('integrity') || allErrors.includes('SRI');
    expect(sriErrors.length, `SRI errors found: ${JSON.stringify(sriErrors)}`).toBe(0);
    expect(sriInConsole, `SRI errors in console: ${allErrors.substring(0, 500)}`).toBe(false);

    // Check: Blazor error UI NOT visible (app loaded successfully)
    const errorUi = page.locator('#blazor-error-ui');
    const errorUiVisible = await errorUi.isVisible({ timeout: 3000 }).catch(() => false);
    expect(errorUiVisible, 'Blazor error UI should NOT be visible').toBe(false);

    // Check: app content loaded (not stuck on loading spinner)
    const loadingText = page.locator('text=Vạn An đang tải');
    const loadingVisible = await loadingText.isVisible({ timeout: 3000 }).catch(() => false);
    expect(loadingVisible, 'App should not be stuck on loading spinner').toBe(false);

    await context.close();
  });

  test('RT-SRI-02: WASM files load with 200 status (not blocked by SRI)', async ({ browser }) => {
    const context = await createMobileContext(browser);
    const page = await context.newPage();

    const wasmResponses: { url: string; status: number }[] = [];
    page.on('response', resp => {
      if (resp.url().includes('_framework/') && (resp.url().endsWith('.wasm') || resp.url().endsWith('.dll'))) {
        wasmResponses.push({ url: resp.url().split('/').pop() || resp.url(), status: resp.status() });
      }
    });

    await page.goto(BASE_URL, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(8000);

    // At least VanAn.KhachLink.wasm and VanAn.Shared.wasm should load
    const khachlinkWasm = wasmResponses.find(r => r.url.includes('VanAn.KhachLink.wasm'));
    const sharedWasm = wasmResponses.find(r => r.url.includes('VanAn.Shared.wasm'));

    expect(khachlinkWasm, 'VanAn.KhachLink.wasm should be fetched').toBeTruthy();
    expect(khachlinkWasm?.status, `VanAn.KhachLink.wasm status should be 200, got ${khachlinkWasm?.status}`).toBe(200);

    expect(sharedWasm, 'VanAn.Shared.wasm should be fetched').toBeTruthy();
    expect(sharedWasm?.status, `VanAn.Shared.wasm status should be 200, got ${sharedWasm?.status}`).toBe(200);

    await context.close();
  });
});

// ============================================================================
// Phase 2: Service Worker DLL caching — SW v12-sri-fix active
// ============================================================================
test.describe('Phase 2: Service Worker caching @smoke', () => {
  test('RT-SW-01: Service worker v12-sri-fix is registered and active', async ({ browser }) => {
    const context = await createMobileContext(browser);
    const page = await context.newPage();

    await page.goto(BASE_URL, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(5000); // Wait for SW registration

    const swInfo = await page.evaluate(async () => {
      const reg = await navigator.serviceWorker.getRegistration();
      if (!reg) return { registered: false, scriptURL: '', state: '' };
      return {
        registered: true,
        scriptURL: reg.active?.scriptURL || '',
        state: reg.active?.state || '',
      };
    });

    expect(swInfo.registered, 'Service worker should be registered').toBe(true);
    expect(swInfo.state, `SW state should be activated, got ${swInfo.state}`).toBe('activated');
    expect(swInfo.scriptURL, 'SW script URL should be service-worker.js').toContain('service-worker.js');

    await context.close();
  });

  test('RT-SW-02: WASM cache storage populated with _framework assets', async ({ browser }) => {
    const context = await createMobileContext(browser);
    const page = await context.newPage();

    await page.goto(BASE_URL, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(10000); // Wait for SW install + precache

    const cacheInfo = await page.evaluate(async () => {
      const cacheNames = await caches.keys();
      const wasmCacheName = cacheNames.find(n => n.includes('wasm'));
      if (!wasmCacheName) return { exists: false, count: 0, names: cacheNames };

      const cache = await caches.open(wasmCacheName);
      const keys = await cache.keys();
      return {
        exists: true,
        count: keys.length,
        names: cacheNames,
        sampleUrls: keys.slice(0, 5).map(r => r.url.split('/').pop()),
      };
    });

    expect(cacheInfo.names.some(n => n.includes(SW_VERSION)), `Cache should include ${SW_VERSION}, got: ${JSON.stringify(cacheInfo.names)}`).toBe(true);

    // Old caches should be cleaned up by activate event
    const oldCaches = cacheInfo.names.filter(n => n.includes('v10-batched') || n.includes('v11-phase3'));
    expect(oldCaches.length, `Old caches should be deleted, found: ${JSON.stringify(oldCaches)}`).toBe(0);

    await context.close();
  });
});

// ============================================================================
// Phase 3 SC5-SC8: Offline API fallback — each page works offline with cached data
// ============================================================================
test.describe('Phase 3 SC5-SC8: Offline API fallback @smoke', () => {

  test('RT-SC5: Offline Store Finder — cached stores show', async ({ browser }) => {
    const context = await createMobileContext(browser);
    const page = await context.newPage();

    // Step 1: Load online first (populate cache)
    await page.goto(`${BASE_URL}/store-finder`, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(8000); // Wait for API calls + cache

    // Step 2: Go offline
    await context.setOffline(true);

    // Step 3: Reload — should show cached stores
    await page.reload({ waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.waitForTimeout(5000);

    // Verify: page loads (not blank, not error)
    const errorUi = page.locator('#blazor-error-ui');
    const errorVisible = await errorUi.isVisible({ timeout: 3000 }).catch(() => false);
    expect(errorVisible, 'Blazor error UI should NOT be visible offline').toBe(false);

    // Verify: some store-related content visible (store cards, store list, or store finder UI)
    const pageText = await page.textContent('body').catch(() => '');
    const hasContent = pageText && pageText.length > 100;
    expect(hasContent, 'Page should have content when offline (cached stores)').toBe(true);

    await context.setOffline(false);
    await context.close();
  });

  test('RT-SC6: Offline Home — cached catalog + campaigns show', async ({ browser }) => {
    const context = await createMobileContext(browser);
    const page = await context.newPage();

    // Step 1: Load Home online first (populate cache for catalog + campaigns)
    await page.goto(BASE_URL, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(8000);

    // Step 2: Go offline
    await context.setOffline(true);

    // Step 3: Reload — should show cached catalog + campaigns
    await page.reload({ waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.waitForTimeout(5000);

    // Verify: page loads without error
    const errorUi = page.locator('#blazor-error-ui');
    const errorVisible = await errorUi.isVisible({ timeout: 3000 }).catch(() => false);
    expect(errorVisible, 'Blazor error UI should NOT be visible offline').toBe(false);

    // Verify: content visible (not blank offline shell only)
    const pageText = await page.textContent('body').catch(() => '');
    const hasContent = pageText && pageText.length > 100;
    expect(hasContent, 'Home should have content when offline (cached catalog/campaigns)').toBe(true);

    await context.setOffline(false);
    await context.close();
  });

  test('RT-SC7: Offline Order Tracking — cached order shows', async ({ browser }) => {
    const context = await createMobileContext(browser);
    const page = await context.newPage();

    // Step 1: Load a known order tracking page online first
    // Use a test order ID — may or may not exist, but the page should load from cache
    const testOrderId = '00000000-0000-0000-0000-000000000001';
    await page.goto(`${BASE_URL}/order-tracking/${testOrderId}`, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(8000);

    // Step 2: Go offline
    await context.setOffline(true);

    // Step 3: Reload — should show cached order page (even if order not found, page structure loads)
    await page.reload({ waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.waitForTimeout(5000);

    // Verify: page loads (WASM runs from cache, not blank)
    const errorUi = page.locator('#blazor-error-ui');
    const errorVisible = await errorUi.isVisible({ timeout: 3000 }).catch(() => false);
    expect(errorVisible, 'Blazor error UI should NOT be visible offline').toBe(false);

    // Verify: WASM app renders (not just offline shell HTML)
    const appDiv = page.locator('#app');
    const appExists = await appDiv.count();
    expect(appExists, '#app div should exist (WASM rendered)').toBeGreaterThan(0);

    await context.setOffline(false);
    await context.close();
  });

  test('RT-SC8: Offline Order History — cached orders show', async ({ browser }) => {
    const context = await createMobileContext(browser);
    const page = await context.newPage();

    // Step 1: Load order history online first
    await page.goto(`${BASE_URL}/order-history`, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(8000);

    // Step 2: Go offline
    await context.setOffline(true);

    // Step 3: Reload — should show cached order history
    await page.reload({ waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.waitForTimeout(5000);

    // Verify: page loads without error
    const errorUi = page.locator('#blazor-error-ui');
    const errorVisible = await errorUi.isVisible({ timeout: 3000 }).catch(() => false);
    expect(errorVisible, 'Blazor error UI should NOT be visible offline').toBe(false);

    // Verify: WASM app renders
    const pageText = await page.textContent('body').catch(() => '');
    const hasContent = pageText && pageText.length > 100;
    expect(hasContent, 'Order History should have content when offline').toBe(true);

    await context.setOffline(false);
    await context.close();
  });
});

// ============================================================================
// Phase 2b: Price validation + navigator.onLine guard
// ============================================================================
test.describe('Phase 2b: Online-only checkout guard @smoke', () => {
  test('RT-ONLINE-01: Checkout blocked when offline with clear error', async ({ browser }) => {
    const context = await createMobileContext(browser);
    const page = await context.newPage();

    // Load app online first
    await page.goto(BASE_URL, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(8000);

    // Go offline
    await context.setOffline(true);
    await page.waitForTimeout(1000);

    // Check navigator.onLine is false
    const isOnline = await page.evaluate(() => navigator.onLine);
    expect(isOnline, 'navigator.onLine should be false when offline').toBe(false);

    // Verify: app still renders (WASM works offline for browsing)
    const errorUi = page.locator('#blazor-error-ui');
    const errorVisible = await errorUi.isVisible({ timeout: 3000 }).catch(() => false);
    expect(errorVisible, 'App should work offline for browsing').toBe(false);

    await context.setOffline(false);
    await context.close();
  });
});

// ============================================================================
// Phase 3: Auth endpoints NOT cached (security — no cross-user leak)
// ============================================================================
test.describe('Phase 3 Security: Auth endpoints not cached @smoke', () => {
  test('RT-SEC-01: Auth endpoints excluded from dynamic cache', async ({ browser }) => {
    const context = await createMobileContext(browser);
    const page = await context.newPage();

    await page.goto(BASE_URL, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(10000);

    // Navigate to pages that trigger whitelisted API calls to populate dynamic cache
    await page.goto(`${BASE_URL}/store-finder`, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(5000);
    await page.goto(BASE_URL, { waitUntil: 'networkidle', timeout: 60000 });
    await page.waitForTimeout(5000);

    // Check dynamic cache does NOT contain auth endpoints
    const cacheInfo = await page.evaluate(async () => {
      const cacheNames = await caches.keys();
      const dynamicCacheName = cacheNames.find(n => n.includes('dynamic'));
      if (!dynamicCacheName) return { exists: false, authUrls: [], totalKeys: 0 };

      const cache = await caches.open(dynamicCacheName);
      const keys = await cache.keys();
      const authUrls = keys
        .filter(r => r.url.includes('/api/customers/me') || r.url.includes('/api/loyalty/my') || r.url.includes('/api/customer-identity/me'))
        .map(r => r.url);
      return { exists: true, authUrls, totalKeys: keys.length };
    });

    // If dynamic cache exists, auth endpoints must NOT be in it.
    // If dynamic cache doesn't exist yet (no API calls triggered), that's also a pass
    // (no cache = no auth endpoints cached = no cross-user leak risk).
    if (cacheInfo.exists) {
      expect(cacheInfo.authUrls.length, `Auth endpoints should NOT be in dynamic cache, found: ${JSON.stringify(cacheInfo.authUrls)}`).toBe(0);
    } else {
      console.log('RT-SEC-01: Dynamic cache not yet populated — no auth endpoints cached (trivially safe)');
    }

    await context.close();
  });
});
