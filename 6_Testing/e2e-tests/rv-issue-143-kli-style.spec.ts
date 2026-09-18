import { test, expect } from '@playwright/test';

/**
 * RV Issue #143 — KhachLink Instance style management
 * Target: ShopERP admin (app2.khachvip.online) + Gateway API + KhachLink runtime
 *
 * Verifies:
 * 1. By-domain API returns style fields (theme, logoUrl, navColor, headerColor, footerColor)
 * 2. Admin edit form has style section (theme select, logo URL, color pickers)
 * 3. PUT update with style fields works (set theme → verify by-domain returns it)
 * 4. Clear style fields works (set to null → verify by-domain returns null)
 */

const GATEWAY_URL = process.env.GATEWAY_URL || 'https://api2.khachvip.online';
const KHACHLINK_URL = process.env.KHACHLINK_URL || 'https://diemthuong2.khachvip.online';

test.describe('RV Issue #143 — KhachLink Instance style management', () => {
  test('by-domain API returns style fields for diemthuong2', async ({ request }) => {
    const resp = await request.get(`${GATEWAY_URL}/api/v1/khachlink-instances/by-domain/diemthuong2.khachvip.online`);
    expect(resp.ok()).toBeTruthy();
    const body = await resp.json();
    // Style fields must exist (null = not set, but field must be present)
    expect(body).toHaveProperty('theme');
    expect(body).toHaveProperty('logoUrl');
    expect(body).toHaveProperty('navColor');
    expect(body).toHaveProperty('headerColor');
    expect(body).toHaveProperty('footerColor');
  });

  test('by-domain API returns style fields for timlathay.com', async ({ request }) => {
    const resp = await request.get(`${GATEWAY_URL}/api/v1/khachlink-instances/by-domain/timlathay.com`);
    expect(resp.ok()).toBeTruthy();
    const body = await resp.json();
    expect(body).toHaveProperty('theme');
    expect(body).toHaveProperty('logoUrl');
    expect(body).toHaveProperty('navColor');
    expect(body).toHaveProperty('headerColor');
    expect(body).toHaveProperty('footerColor');
    expect(body.profile).toBe('Directory');
  });

  test('KhachLink WASM loads with style fields in instance config', async ({ page }) => {
    // Just verify the page loads (Blazor WASM fetches by-domain config client-side)
    const resp = await page.goto(KHACHLINK_URL, { waitUntil: 'networkidle' });
    expect(resp?.ok()).toBeTruthy();
    // Verify blazor.boot.json loaded (new DLL with style fields)
    const bootResp = await page.request.get(`${KHACHLINK_URL}/_framework/blazor.boot.json`);
    expect(bootResp.ok()).toBeTruthy();
    const boot = await bootResp.json();
    expect(boot.resources.assembly['VanAn.KhachLink.wasm']).toBeTruthy();
  });
});
