import { test, expect } from '@playwright/test';

/**
 * Issue #195 — FOLLOW-UP RV (2026-10-29, app2.khachvip.online, commit d98fd639).
 * 3 comment user 2026-10-28:
 *   1. In bill ưu tiên tên tenant (Cafe Tân Quy), không còn hardcode "VẠN AN"
 *   2. Đơn POS Tân Quy (01a18139...) xử lý tiếp được — flow-aware list + transition pending→completed
 *   3. /admin/order-flow có hướng dẫn + chặn profile step không terminal rỗng chuyển tiếp
 * Auth: rv-195b-auth.cjs → auth/rv-195b-quy.json (impersonate Cafe Tân Quy) + auth/rv-195b-admin.json
 * Chạy: npx playwright test -c playwright-rv-195b.config.ts
 */
const ORDER_ID = '01a18139-e2d0-7089-985d-21272d5c8b7e';

test.describe('Issue #195 follow-up RV (production)', () => {
  test.describe.configure({ mode: 'serial' });

  test('1. Print bill shows tenant name (Cafe Tân Quy) — not VẠN AN', async ({ page }) => {
    await page.goto(`/orders/${ORDER_ID}/print`);
    await page.waitForLoadState('networkidle');

    const storeName = page.locator('.bill-store-name');
    await expect(storeName).toBeVisible({ timeout: 30000 });
    await expect(storeName).toContainText('Cafe Tân Quy');
    await expect(storeName).not.toContainText('VẠN AN');
  });

  test('2. Orders list is flow-aware — never shows Xác nhận for Tân Quy', async ({ page }) => {
    await page.goto('/orders');
    await page.waitForLoadState('networkidle');

    await page.getByTestId('filter-status').selectOption('ALL');
    await page.getByTestId('filter-date').selectOption('all');

    // Row của đơn Tân Quy — locate qua link detail href
    const rowLink = page.locator(`a[href="/orders/${ORDER_ID}"]`);
    await rowLink.first().waitFor({ state: 'visible', timeout: 30000 });
    const row = rowLink.first().locator('xpath=ancestor::tr').first();

    // Flow "Nhanh - gon - 1 bước": KHÔNG bao giờ hiện "Xác nhận" (confirmed không có trong flow)
    await expect(row.getByTestId('btn-confirm-order')).toHaveCount(0);

    // Idempotent: pending → chỉ hiện "Hoàn tất"; đã completed (RV lần trước) → không còn nút action
    const isPending = (await row.locator('.status-badge.status-pending').count()) > 0;
    if (isPending) {
      await expect(row.getByTestId('btn-complete-order').first()).toBeVisible({ timeout: 15000 });
    } else {
      await expect(row.getByTestId('btn-complete-order')).toHaveCount(0);
    }
  });

  test('3. Detail page flow-aware + transition pending → completed works', async ({ page }) => {
    await page.goto(`/orders/${ORDER_ID}`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByTestId('status-timeline')).toBeVisible({ timeout: 30000 });
    // Flow cho phép completed → nút Hoàn tất; KHÔNG có Xác nhận
    await expect(page.getByTestId('btn-confirm-detail')).toHaveCount(0);

    const currentStep = page.locator('.timeline-step.current');
    await expect(currentStep).toHaveCount(1);
    const currentTestId = await currentStep.getAttribute('data-testid');

    if (currentTestId === 'timeline-step-pending') {
      // Đơn còn pending → thực hiện transition pending → completed (đơn Paid — user muốn xử lý tiếp)
      await expect(page.getByTestId('flow-actions')).toBeVisible({ timeout: 15000 });
      await expect(page.getByTestId('btn-complete')).toBeVisible();
      await page.getByTestId('btn-complete').click();
      await expect(page.getByTestId('timeline-step-completed')).toHaveClass(/current/, { timeout: 20000 });
    } else {
      // Đã completed (RV lần trước) — xác nhận trạng thái cuối
      await expect(page.getByTestId('timeline-step-completed')).toHaveClass(/current/);
    }
  });

  test.describe('SystemAdmin', () => {
    test.use({ storageState: 'auth/rv-195b-admin.json' });

    test('4. Order-flow admin shows guidance + blocks stuck-step save', async ({ page }) => {
      await page.goto('/admin/order-flow');
      await page.waitForLoadState('networkidle');

      // Hướng dẫn hiển thị
      await expect(page.getByTestId('order-flow-help')).toBeVisible({ timeout: 30000 });
      await expect(page.getByTestId('order-flow-help')).toContainText('Hướng dẫn thiết lập luồng xử lý');
      await expect(page.getByTestId('order-flow-help')).toContainText('kẹt vĩnh viễn');

      // Client mirror guard: step không terminal + rỗng "Chuyển được sang" → chặn lưu
      await expect(page.getByTestId('btn-save-flow')).toBeVisible();
      // Form mới: nhập tên luồng trước (validation name chạy trước guard step)
      // VanAInput render id="flow-name" (không data-testid)
      await page.locator('#flow-name').fill('RV test flow');
      const firstNextInput = page.getByTestId('step-next').first();
      await firstNextInput.fill('');
      await page.getByTestId('btn-save-flow').click();
      await expect(page.getByText(/đơn sẽ bị kẹt/)).toBeVisible({ timeout: 10000 });
    });
  });
});