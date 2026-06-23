import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';

// Wave 4 — RBAC Enforcement E2E Tests [W4-T6]
// Verifies that role policies are enforced at the Blazor UI layer:
//   - Staff cannot access Owner-only pages → redirected to /access-denied
//   - Owner can access all pages
//   - Guard is redirected to Guard/Scan after login

const config = loadEnvConfig();

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('RBAC Enforcement — Blazor UI Layer', () => {

  // ── Helper: log in as a given user ────────────────────────────────────────
  async function loginAs(page: any, username: string, password = 'VanAn@2026') {
    await page.goto(`${config.SHOPERP_URL}/Login`);
    await page.waitForLoadState('networkidle');
    await page.fill('#Username', username);
    await page.fill('#Password', password);
    await page.click('button[type="submit"]');
    await page.waitForLoadState('networkidle');
  }

  // ── Test 1: Staff cannot access Accounting page ───────────────────────────
  test('Staff login → redirected to KDS, cannot access /accounting', async ({ page }) => {
    await loginAs(page, 'staff@vanan.vn');

    // Staff should land on Kitchen page, not Index
    expect(page.url()).toContain('/Kitchen');

    // Attempt to navigate to accounting page
    await page.goto(`${config.SHOPERP_URL}/accounting`);
    await page.waitForLoadState('networkidle');

    // Should be redirected to /access-denied
    expect(page.url()).toContain('/access-denied');

    // Verify the 403 page message is displayed
    const heading = page.getByRole('heading', { name: /403/i });
    await expect(heading).toBeVisible();
  });

  // ── Test 2: Owner can access Accounting page ──────────────────────────────
  test('Owner login → can access /accounting dashboard', async ({ page }) => {
    await loginAs(page, 'owner@vanan.vn');

    await page.goto(`${config.SHOPERP_URL}/accounting`);
    await page.waitForLoadState('networkidle');

    // Should remain on accounting page (not redirected)
    expect(page.url()).toContain('/accounting');
    expect(page.url()).not.toContain('/access-denied');

    // Verify accounting dashboard content is visible
    const heading = page.getByRole('heading', { name: /Kế Toán Dashboard/i });
    await expect(heading).toBeVisible();
  });

  // ── Test 3: Staff cannot access Period Closing ────────────────────────────
  test('Staff cannot access /accounting/period-closing', async ({ page }) => {
    await loginAs(page, 'staff@vanan.vn');

    await page.goto(`${config.SHOPERP_URL}/accounting/period-closing`);
    await page.waitForLoadState('networkidle');

    expect(page.url()).toContain('/access-denied');
  });

  // ── Test 4: StoreKeeper can access EInvoice dashboard ────────────────────
  test('StoreKeeper can access /einvoice dashboard', async ({ page }) => {
    await loginAs(page, 'storekeeper@vanan.vn');

    await page.goto(`${config.SHOPERP_URL}/einvoice`);
    await page.waitForLoadState('networkidle');

    expect(page.url()).toContain('/einvoice');
    expect(page.url()).not.toContain('/access-denied');
  });

  // ── Test 5: StoreKeeper cannot access Provider Configuration ─────────────
  test('StoreKeeper cannot access /einvoice/configuration (Owner only)', async ({ page }) => {
    await loginAs(page, 'storekeeper@vanan.vn');

    await page.goto(`${config.SHOPERP_URL}/einvoice/configuration`);
    await page.waitForLoadState('networkidle');

    expect(page.url()).toContain('/access-denied');
  });

  // ── Test 6: Guard login → redirected to Guard/Scan ────────────────────────
  test('Guard login → redirected to /guard/scan', async ({ page }) => {
    await loginAs(page, 'guard@vanan.vn');

    expect(page.url()).toContain('/Guard/Scan');
  });

  // ── Test 7: NavMenu hides accounting items for Staff ─────────────────────
  test('NavMenu does not show Accounting links for Staff role', async ({ page }) => {
    await loginAs(page, 'staff@vanan.vn');
    await page.goto(`${config.SHOPERP_URL}/`);
    await page.waitForLoadState('networkidle');

    // Accounting nav link should NOT be visible
    const accountingLink = page.locator('nav a[href="accounting"]');
    await expect(accountingLink).not.toBeVisible();
  });

  // ── Test 8: NavMenu shows accounting items for Owner ─────────────────────
  test('NavMenu shows Accounting links for Owner role', async ({ page }) => {
    await loginAs(page, 'owner@vanan.vn');
    await page.goto(`${config.SHOPERP_URL}/`);
    await page.waitForLoadState('networkidle');

    // Accounting nav link SHOULD be visible
    const accountingLink = page.locator('nav a[href="accounting"]');
    await expect(accountingLink).toBeVisible();
  });

  // ── Test 9: Unauthenticated user → redirected to Login ───────────────────
  test('Unauthenticated access to /accounting → redirect to Login', async ({ page }) => {
    // Navigate without logging in
    await page.goto(`${config.SHOPERP_URL}/accounting`);
    await page.waitForLoadState('networkidle');

    // Should be redirected to Login page
    expect(page.url()).toContain('/Login');
  });

});
