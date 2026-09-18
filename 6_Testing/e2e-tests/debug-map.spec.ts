import { test, expect } from '@playwright/test';
import { injectGpsMock } from './helpers/gps-mock';

const BASE_URL = 'https://diemthuong2.khachvip.online';
const TENANT_ID = '7c021960-6d6c-4de4-88a1-cf8373184f49';

test('DEBUG-1: store page map — tiles load via provider fallback (P6 manual-test fix)', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true });
  await injectGpsMock(context);
  const page = await context.newPage();
  const failed: string[] = [];
  page.on('requestfailed', (req) => {
    if (req.url().includes('tile.openstreetmap')) failed.push('osm');
    if (req.url().includes('cartocdn')) failed.push('carto');
  });
  await page.goto(`${BASE_URL}/store/by-id/${TENANT_ID}`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(6000);

  const info = await page.evaluate(() => {
    const el = document.getElementById('store-location-map');
    const tiles = Array.from(document.querySelectorAll('#store-location-map img.leaflet-tile'));
    const loaded = tiles.filter((t) => (t as HTMLImageElement).complete && (t as HTMLImageElement).naturalWidth > 0).length;
    const src = tiles[0] ? (tiles[0] as HTMLImageElement).src : null;
    return { divExists: !!el, leafletInit: !!el && el.classList.contains('leaflet-container'), tiles: tiles.length, tilesLoaded: loaded, tileHost: src ? new URL(src).host : null };
  });
  console.log(`[MAP] ${JSON.stringify(info)}`);
  console.log(`[TILE FAILURES] ${failed.join(', ') || 'none'}`);
  // The map must render tiles — the fallback kicks in after OSM errors, so poll (not a fixed sleep).
  await expect.poll(async () => {
    return page.evaluate(() =>
      Array.from(document.querySelectorAll('#store-location-map img.leaflet-tile'))
        .filter((t) => (t as HTMLImageElement).complete && (t as HTMLImageElement).naturalWidth > 0).length);
  }, { timeout: 20000 }).toBeGreaterThan(0);
  await context.close();
});

test('DEBUG-2: chat panel — refresh button present + mobile layout (input + Gửi stacked)', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true, viewport: { width: 375, height: 700 } });
  await injectGpsMock(context);
  const page = await context.newPage();
  await page.goto(`${BASE_URL}/store/by-id/${TENANT_ID}`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(4000);

  await expect(page.locator('.chat-panel')).toHaveCount(1);

  // Refresh button (bi-arrow-clockwise) in the panel header. NOTE: VanAnButton's attribute
  // merging drops a passed aria-label, so select by the icon inside the header button.
  const refreshBtn = page.locator('.chat-panel button .bi-arrow-clockwise');
  await expect(refreshBtn).toHaveCount(1);

  // Mobile: input + send button stacked (flex-column) — button on its own line, full width.
  const layout = await page.evaluate(() => {
    const row = document.querySelector('.chat-input-row');
    const input = document.querySelector('.chat-input-row .vanan-input') as HTMLElement | null;
    const btn = document.querySelector('.chat-input-row .chat-send-btn') as HTMLElement | null;
    if (!row || !input || !btn) return null;
    const rowStyle = getComputedStyle(row);
    const inputRect = input.getBoundingClientRect();
    const btnRect = btn.getBoundingClientRect();
    return {
      flexDirection: rowStyle.flexDirection,
      btnBelowInput: btnRect.top >= inputRect.bottom - 1,
      btnWidthPct: Math.round((btnRect.width / row.getBoundingClientRect().width) * 100),
      noHorizontalOverflow: document.documentElement.scrollWidth <= window.innerWidth,
    };
  });
  console.log(`[MOBILE LAYOUT] ${JSON.stringify(layout)}`);
  expect(layout).not.toBeNull();
  expect(layout!.flexDirection).toBe('column');
  expect(layout!.btnBelowInput).toBe(true);
  expect(layout!.noHorizontalOverflow).toBe(true);
  await context.close();
});
