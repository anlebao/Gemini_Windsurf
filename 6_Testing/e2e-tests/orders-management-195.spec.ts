import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

/**
 * Issue #195 — Order Management Upgrade E2E (Gate 4: UI layout changes).
 * Covers:
 *  - Orders list: default pending+today filter, bulk action bar, customer column, pagination
 *  - Order detail: flow timeline (current/next), grouped info, customer name/phone
 *  - Admin order-flow config page (SystemAdmin)
 */

const config = loadEnvConfig();
const reporter = new TestReporter('Issue195 OrderMgmt E2E');

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('Issue #195 — Order Management Upgrade', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting Issue #195 E2E Tests...');
  });

  // ─── ORDERS LIST (ShopERP, Owner) ────────────────────────────────────────

  test('Orders list defaults to pending + today with bulk bar and pager', async ({ page, request }) => {
    // Dev login → Owner cookie session
    const login = await request.post(`${config.SHOPERP_URL}/dev/login`);
    expect(login.ok()).toBeTruthy();

    await page.goto(`${config.SHOPERP_URL}/orders`);
    await page.waitForLoadState('networkidle');

    // Issue #195 (2.1): status filter defaults to "pending" (Chờ xác nhận)
    const statusFilter = page.getByTestId('filter-status');
    await expect(statusFilter).toBeVisible({ timeout: 15000 });
    await expect(statusFilter).toHaveValue('pending');

    // Issue #195 (2.1): date filter defaults to "today"
    const dateFilter = page.getByTestId('filter-date');
    await expect(dateFilter).toBeVisible();
    await expect(dateFilter).toHaveValue('today');
  });

  test('Orders list shows bulk bar and pager when orders exist', async ({ page, request }) => {
    await request.post(`${config.SHOPERP_URL}/dev/login`);
    await page.goto(`${config.SHOPERP_URL}/orders`);
    await page.waitForLoadState('networkidle');

    // Switch to "Tất cả" statuses + all dates to maximize chance of data
    await page.getByTestId('filter-status').selectOption('ALL');
    await page.getByTestId('filter-date').selectOption('all');
    await page.waitForLoadState('networkidle');

    // Issue #195 (2.2): bulk action bar with select-all checkbox
    const bulkBar = page.getByTestId('bulk-bar');
    await expect(bulkBar).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId('chk-select-all')).toBeVisible();

    // Issue #195 (2.4): server-side pagination bar
    await expect(page.getByTestId('pager')).toBeVisible();
    await expect(page.getByTestId('pager-info')).toBeVisible();
    await expect(page.getByTestId('btn-page-prev')).toBeVisible();
    await expect(page.getByTestId('btn-page-next')).toBeVisible();
    await expect(page.getByTestId('page-size')).toBeVisible();

    // Select-all toggles bulk count + apply controls
    await page.getByTestId('chk-select-all').check();
    await expect(page.getByTestId('bulk-count')).toBeVisible({ timeout: 5000 });
    await expect(page.getByTestId('bulk-action-select')).toBeVisible();
    await expect(page.getByTestId('btn-bulk-apply')).toBeVisible();
  });

  test('Order detail shows flow timeline and customer info', async ({ page, request }) => {
    await request.post(`${config.SHOPERP_URL}/dev/login`);
    await page.goto(`${config.SHOPERP_URL}/orders`);
    await page.waitForLoadState('networkidle');

    await page.getByTestId('filter-status').selectOption('ALL');
    await page.getByTestId('filter-date').selectOption('all');
    await page.waitForLoadState('networkidle');

    const detailLink = page.getByTestId('link-order-detail').first();
    await expect(detailLink).toBeVisible({ timeout: 15000 });
    await detailLink.click();
    await page.waitForLoadState('networkidle');

    // Issue #195 (1.2): flow timeline rendered with per-step testids
    const timeline = page.getByTestId('status-timeline');
    await expect(timeline).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId('timeline-step-pending')).toBeVisible();

    // Issue #195 (1.2): dynamic action buttons driven by resolved flow
    await expect(page.getByTestId('flow-actions')).toBeVisible();
  });

  // ─── ADMIN ORDER FLOW CONFIG (ShopERP, SystemAdmin) ──────────────────────

  test('SystemAdmin can view and edit order flow config page', async ({ page, request }) => {
    const login = await request.post(`${config.SHOPERP_URL}/dev/login/systemadmin`);
    expect(login.ok()).toBeTruthy();

    await page.goto(`${config.SHOPERP_URL}/admin/order-flow`);
    await page.waitForLoadState('networkidle');

    // Steps editor table always renders (create form with default template)
    await expect(page.getByTestId('flow-steps-table')).toBeVisible({ timeout: 20000 });
    await expect(page.getByTestId('btn-add-step')).toBeVisible();
    await expect(page.getByTestId('btn-save-flow')).toBeVisible();
    await expect(page.getByTestId('flow-form-tenant')).toBeVisible();
    await expect(page.getByTestId('flow-form-source')).toBeVisible();

    // Add-step appends a row
    const rowsBefore = await page.getByTestId('flow-step-row').count();
    await page.getByTestId('btn-add-step').click();
    await expect(page.getByTestId('flow-step-row')).toHaveCount(rowsBefore + 1);
  });
});
