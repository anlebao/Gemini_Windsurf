import { test, expect } from '@playwright/test';

/**
 * Issue #195 booking-payment RV (2026-10-29, production app2, commit 243112db).
 * Sau sweep API sạch (booking-payment-rv-sweep.mjs + dbcheck.sh) — UI final:
 * đơn booking (order từ sweep: 01a185f0-830e-70ca-8471-178b48338984, code 42FB2135B07C4EE9)
 * bấm "Thanh toán" → PaymentStatus=Paid + status confirmed + nút "Đã thanh toán" biến mất.
 * Auth: rv-bp-auth.cjs → auth/rv-bp-hkd.json (impersonate Test HKD 6aaf19e4).
 * Chạy: npx playwright test -c playwright-rv-bp.config.ts
 */
const ORDER_ID = '01a185f0-830e-70ca-8471-178b48338984';

test.describe('Issue #195 booking-payment RV (production)', () => {
  test.describe.configure({ mode: 'serial' });

  test('1. Booking order detail — "Thanh toán" (confirmed) visible, PaymentStatus Pending', async ({ page }) => {
    await page.goto(`/orders/${ORDER_ID}`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByTestId('status-timeline')).toBeVisible({ timeout: 30000 });
    // Booking flow: pending "Chờ thanh toán" → confirmed "Đã thanh toán" (action Thanh toán)
    await expect(page.getByTestId('timeline-step-pending')).toHaveClass(/current/);
    await expect(page.getByTestId('flow-actions')).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId('btn-confirm-detail')).toBeVisible();
    // PaymentStatus ban đầu = Pending → nút "Đã thanh toán" (ConfirmPaymentAsync) VẪN hiện
    await expect(page.getByTestId('btn-confirm-payment')).toBeVisible();
  });

  test('2. Click "Thanh toán" → PaymentStatus Paid + status confirmed + nút "Đã thanh toán" biến mất', async ({ page }) => {
    await page.goto(`/orders/${ORDER_ID}`);
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('btn-confirm-detail')).toBeVisible({ timeout: 30000 });

    await page.getByTestId('btn-confirm-detail').click();
    // Status chuyển confirmed (timeline current)
    await expect(page.getByTestId('timeline-step-confirmed')).toHaveClass(/current/, { timeout: 20000 });
    // PaymentStatus = Paid → nút "Đã thanh toán" (chỉ hiện khi Pending) phải biến mất
    await expect(page.getByTestId('btn-confirm-payment')).toHaveCount(0);
  });
});