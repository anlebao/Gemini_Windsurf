import { test, expect } from '@playwright/test';
import { readFileSync } from 'fs';
import { join, dirname } from 'path';

/**
 * Issue #195 booking-payment RV (production app2).
 * - Fix 243112db: "Thanh toán" = payment thật + single revenue source.
 * - Fix 21505b38: double revenue journal — bridge single source + DepositPaidAmount snapshot.
 * Sau sweep API sạch (booking-payment-rv-sweep.mjs + dbcheck.sh) — UI final:
 *   đơn booking (orderId từ report) bấm "Thanh toán" → Paid + confirmed + nút biến mất.
 *   Đơn deposit (depositOrderId từ report): Detail hiển thị "Đã thu cọc 10.000" + "Còn lại 290.000".
 * Auth: rv-bp-auth.cjs → auth/rv-bp-hkd.json (impersonate Test HKD 6aaf19e4).
 * Chạy: npx playwright test -c playwright-rv-bp.config.ts
 */
const REPORT = JSON.parse(
  readFileSync(join(dirname(__dirname), 'rv-scripts', 'booking-payment-rv-report.json'), 'utf8'),
);
const ORDER_ID = REPORT.orderId;
const DEPOSIT_ORDER_ID = REPORT.depositOrderId;
const DEPOSIT_AMOUNT = REPORT.depositAmount ?? 10000;

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

  test('3. Deposit order — "Đã thu cọc / Còn lại" hiển thị + "Thanh toán" (offset 3387)', async ({ page }) => {
    test.skip(!DEPOSIT_ORDER_ID, 'no deposit order in report (sweep deposit scenario skipped)');
    await page.goto(`/orders/${DEPOSIT_ORDER_ID}`);
    await page.waitForLoadState('networkidle');

    // Booking order: SourceType=booking + DepositPaidAmount>0 → 2 dòng cọc
    await expect(page.getByText('Đã thu cọc')).toBeVisible({ timeout: 30000 });
    await expect(page.getByText('Còn lại cần thu')).toBeVisible();
    // TotalAmount = 300.000 (offering) + VAT 10% 30.000 = 330.000 → Còn lại = 330.000 − cọc
    const TOTAL_AMOUNT = 330000; // Massage 60 phút 300.000 + VAT 10%
    const remaining = TOTAL_AMOUNT - DEPOSIT_AMOUNT;
    // Số còn lại (culture-tolerant: "320.000" hoặc "320,000")
    await expect(page.getByText(new RegExp(`${remaining.toLocaleString('en-US')}|${remaining.toLocaleString('vi-VN')}`))).toBeVisible();
    // "Đã thu cọc" hiển thị đúng số cọc (culture-tolerant)
    await expect(page.getByText(new RegExp(`${DEPOSIT_AMOUNT.toLocaleString('en-US')}|${DEPOSIT_AMOUNT.toLocaleString('vi-VN')}`))).toBeVisible();

    // Thanh toán cuối → Paid + confirmed (deposit offset entry được tạo — dbcheck --deposit verify)
    await expect(page.getByTestId('btn-confirm-detail')).toBeVisible({ timeout: 15000 });
    await page.getByTestId('btn-confirm-detail').click();
    await expect(page.getByTestId('timeline-step-confirmed')).toHaveClass(/current/, { timeout: 20000 });
    await expect(page.getByTestId('btn-confirm-payment')).toHaveCount(0);
  });
});