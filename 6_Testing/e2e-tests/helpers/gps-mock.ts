import type { BrowserContext, Page } from '@playwright/test';

/**
 * CC-S3 (Issue #3): GPS mock for Playwright headless browser.
 *
 * Playwright headless browser has no real GPS. Blazor WASM calls
 * `vananPWA.getCurrentPosition()` JS interop which returns null in headless
 * mode → NearbyProducts, NearbyOrders, DeliveryTracking, OrderTracking pages
 * show "Không lấy được vị trí GPS" and don't load data.
 *
 * This helper injects a mock `vananPWA.getCurrentPosition` via `addInitScript`
 * that runs before any page script. The mock returns coordinates near the
 * Vạn An Cafe HKD Group 1 tenant (10.966, 106.594) so nearby-* APIs return data.
 *
 * The mock returns ALL property name variants (lat/Lat/Latitude) because
 * different Blazor pages deserialize to different C# types:
 * - GeoPosition { Lat, Lng }       — DeliveryTracking, OrderTracking, LocationTrackingService
 * - GpsPosition { Lat, Lng }       — NearbyProducts
 * - GeolocationResult { Latitude, Longitude } — NearbyOrders, StoreFinder
 *
 * System.Text.Json case-insensitive deserialization maps `lat`→`Lat` but
 * NOT `lat`→`Latitude` (different names). Returning all variants ensures
 * every C# type gets populated correctly.
 *
 * Usage:
 *   import { injectGpsMock, GPS_MOCK_COORDS } from './helpers/gps-mock';
 *   const context = await browser.newContext({ ... });
 *   await injectGpsMock(context);   // before context.newPage()
 *   const page = await context.newPage();
 *
 * No production code change — only test tooling.
 */

/** Mock GPS coordinates — near Vạn An Cafe HKD Group 1 tenant. */
export const GPS_MOCK_COORDS = {
  lat: 10.966,
  lng: 106.594,
  Lat: 10.966,
  Lng: 106.594,
  Latitude: 10.966,
  Longitude: 106.594,
};

/**
 * Injects mock `vananPWA.getCurrentPosition` into all pages created from
 * this context. Must be called BEFORE `context.newPage()` — `addInitScript`
 * runs on every new page navigation.
 */
export async function injectGpsMock(context: BrowserContext): Promise<void> {
  await context.addInitScript((coords) => {
    (window as any).vananPWA = (window as any).vananPWA || {};
    (window as any).vananPWA.getCurrentPosition = () =>
      Promise.resolve({
        lat: coords.lat,
        lng: coords.lng,
        Lat: coords.Lat,
        Lng: coords.Lng,
        Latitude: coords.Latitude,
        Longitude: coords.Longitude,
      });
  }, GPS_MOCK_COORDS);
}

/**
 * Injects GPS mock into a single page via `page.addInitScript`.
 * Use when you already have a page and need to add GPS mock before navigation.
 * For new contexts, prefer `injectGpsMock(context)`.
 */
export async function injectGpsMockPage(page: Page): Promise<void> {
  await page.addInitScript((coords) => {
    (window as any).vananPWA = (window as any).vananPWA || {};
    (window as any).vananPWA.getCurrentPosition = () =>
      Promise.resolve({
        lat: coords.lat,
        lng: coords.lng,
        Lat: coords.Lat,
        Lng: coords.Lng,
        Latitude: coords.Latitude,
        Longitude: coords.Longitude,
      });
  }, GPS_MOCK_COORDS);
}
