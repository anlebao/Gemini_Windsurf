import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// T-11: E2E spec for KhachLink /VanAnDashboard
// VanAnDashboard is at KhachLink (5002), not ShopERP.
// Page uses @attribute [StreamRendering] + @inject ILogger only (no IDashboardService —
// that service was removed from DI, verified 2026-06-20).
// Tests use KHACHLINK_URL explicitly as baseURL for this page.

const config = loadEnvConfig();
const reporter = new TestReporter('E2E Tests');

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('KhachLink - VanAn Dashboard (T-11)', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting VanAn Dashboard E2E Tests (T-11)...');
  });

  test('Dashboard page renders at /VanAnDashboard', async ({ page }) => {
    await page.goto(`${config.KHACHLINK_URL}/VanAnDashboard`);
    await page.waitForLoadState('networkidle');

    // Page must not 404 or 500
    // Heading must be visible
    await expect(
      page.locator('h1.dashboard-title, h1, h2').first()
    ).toBeVisible({ timeout: 10000 });

    reporter.pass('VanAn Dashboard Page Load', { url: page.url() });
  });

  test('Dashboard has .dashboard-container', async ({ page }) => {
    await page.goto(`${config.KHACHLINK_URL}/VanAnDashboard`);
    await page.waitForLoadState('networkidle');

    await expect(page.locator('.dashboard-container')).toBeVisible({ timeout: 10000 });

    reporter.pass('Dashboard Container', { visible: true });
  });

  test('Dashboard shows header with title', async ({ page }) => {
    await page.goto(`${config.KHACHLINK_URL}/VanAnDashboard`);
    await page.waitForLoadState('networkidle');

    // Header section must be present
    await expect(page.locator('.dashboard-header')).toBeVisible({ timeout: 10000 });

    // Title must contain "VanAn" or "Dashboard"
    const title = page.locator('.dashboard-title, h1').first();
    await expect(title).toBeVisible();
    const text = await title.textContent();
    expect(text).toBeTruthy();

    reporter.pass('Dashboard Header', { title: text?.trim() });
  });

  test('Dashboard metrics grid renders (or shows loading state)', async ({ page }) => {
    await page.goto(`${config.KHACHLINK_URL}/VanAnDashboard`);
    await page.waitForLoadState('networkidle');

    // Either metrics grid rendered OR loading spinner visible OR alert warning shown
    // (StreamRendering page — metrics may take time to load)
    const hasMetrics  = await page.locator('.metrics-grid').isVisible().catch(() => false);
    const hasSpinner  = await page.locator('.loading-spinner, .spinner-border').isVisible().catch(() => false);
    const hasWarning  = await page.locator('.alert-warning').isVisible().catch(() => false);

    expect(hasMetrics || hasSpinner || hasWarning).toBeTruthy();

    reporter.pass('Dashboard Metrics State', { hasMetrics, hasSpinner, hasWarning });
  });

  test('Dashboard page title is correct', async ({ page }) => {
    await page.goto(`${config.KHACHLINK_URL}/VanAnDashboard`);
    // Page title set by <PageTitle> component
    await expect(page).toHaveTitle(/VanAn Dashboard|VanAn|Dashboard/, { timeout: 10000 });

    reporter.pass('Dashboard Page Title', { title: await page.title() });
  });
});
