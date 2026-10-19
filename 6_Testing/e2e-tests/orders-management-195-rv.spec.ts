import { test, expect } from '@playwright/test';

/**
 * Issue #195 — PRODUCTION RV spec (app2.khachvip.online).
 * Variant của orders-management-195.spec.ts — thay /dev/login (dev-only)
 * bằng storageState auth (rv-195-auth.cjs):
 *   - default: auth/rv-195-owner.json (impersonate Test HKD — Owner role)
 *   - admin test: auth/rv-195-admin.json (sysadmin — SystemAdmin policy)
 * Chạy: npx playwright test -c playwright-rv-195.config.ts
 */

test.describe('Issue #195 RV — Order Management (production)', () => {
  test.describe.configure({ mode: 'serial' });

  test('Orders list defaults to pending + today', async ({ page }) => {
    await page.goto('/orders');
    await page.waitForLoadState('networkidle');

    const statusFilter = page.getByTestId('filter-status');
    await expect(statusFilter).toBeVisible({ timeout: 30000 });
    await expect(statusFilter).toHaveValue('pending');

    const dateFilter = page.getByTestId('filter-date');
    await expect(dateFilter).toBeVisible();
    await expect(dateFilter).toHaveValue('today');
  });

  test('Orders list shows bulk bar + pager + customer column', async ({ page }) => {
    await page.goto('/orders');
    await page.waitForLoadState('networkidle');

    await page.getByTestId('filter-status').selectOption('ALL');
    await page.getByTestId('filter-date').selectOption('all');
    // Blazor Server update qua websocket — networkidle không chờ được; poll link-order-detail
    const firstLink = page.getByTestId('link-order-detail').first();
    await firstLink.waitFor({ state: 'visible', timeout: 30000 }).catch(() => {});
    const hasOrders = (await page.getByTestId('link-order-detail').count()) > 0;
    if (!hasOrders) {
      test.skip(true, 'Không có đơn nào trên tenant — chỉ verify trang render');
    }

    const bulkBar = page.getByTestId('bulk-bar');

    await expect(bulkBar).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId('chk-select-all')).toBeVisible();
    await expect(page.getByTestId('pager')).toBeVisible();
    await expect(page.getByTestId('pager-info')).toBeVisible();

    await page.getByTestId('chk-select-all').check();
    await expect(page.getByTestId('bulk-count')).toBeVisible({ timeout: 5000 });
    await expect(page.getByTestId('bulk-action-select')).toBeVisible();
    await expect(page.getByTestId('btn-bulk-apply')).toBeVisible();
  });

  test('Order detail shows flow timeline + grouped info', async ({ page }) => {
    await page.goto('/orders');
    await page.waitForLoadState('networkidle');

    await page.getByTestId('filter-status').selectOption('ALL');
    await page.getByTestId('filter-date').selectOption('all');
    const detailLink = page.getByTestId('link-order-detail').first();
    await detailLink.waitFor({ state: 'visible', timeout: 30000 }).catch(() => {});
    if ((await detailLink.count()) === 0) {
      test.skip(true, 'Không có đơn nào để mở detail');
    }
    await detailLink.click();
    await page.waitForLoadState('networkidle');

    await expect(page.getByTestId('status-timeline')).toBeVisible({ timeout: 20000 });
    await expect(page.getByTestId('timeline-step-pending')).toBeVisible();

    // flow-actions chỉ render khi đơn còn next step — đơn terminal (completed/cancelled) hợp lệ vắng
    const hasNext = (await page.getByTestId('next-statuses').count()) > 0;
    if (hasNext) {
      await expect(page.getByTestId('flow-actions')).toBeVisible();
    }
  });

  test.describe('SystemAdmin', () => {
    test.use({ storageState: 'auth/rv-195-admin.json' });

    test('Order flow config page renders editor', async ({ page }) => {
      await page.goto('/admin/order-flow');
      await page.waitForLoadState('networkidle');

      await expect(page.getByTestId('flow-steps-table')).toBeVisible({ timeout: 30000 });
      await expect(page.getByTestId('btn-add-step')).toBeVisible();
      await expect(page.getByTestId('btn-save-flow')).toBeVisible();

      const rowsBefore = await page.getByTestId('flow-step-row').count();
      await page.getByTestId('btn-add-step').click();
      await expect(page.getByTestId('flow-step-row')).toHaveCount(rowsBefore + 1);
    });
  });
});
