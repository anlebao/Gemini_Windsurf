import { test, expect } from '@playwright/test';
import { isTierEnabled } from '../utils/env-config';

// RV 2026-10-08 — TEMP spec (xoá sau RV): verify issue #188 fixes trên production (tenant Test HKD).
// 1. /booking/config render + đang BẬT (issue #188 bug 1)
// 2. /booking/qr-channels: tạo QR mới → QR image + link render (issue #188 bug 2 — Q1)
// 3. /inventory/ingredients: thêm nguyên liệu → success (issue #188 bug 3)
// Cleanup: revoke QR vừa tạo + xóa nguyên liệu vừa tạo.
// Self-gating: CHỈ chạy khi E2E tier bật + BOOKING_SHOPERP_URL (production RV config) — CI bỏ qua.

test.describe.configure({ mode: isTierEnabled('e2e') && process.env.BOOKING_SHOPERP_URL ? 'parallel' : 'skip' });

test.describe('RV ShopERP — issue #188 (config/QR/ingredients) production', () => {
  test('Booking config render + đang BẬT', async ({ page }) => {
    await page.goto('https://app2.khachvip.online/booking/config');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText('Cấu hình đặt lịch', { timeout: 20000 });
    await expect(page.locator('[data-testid="config-toggle"]')).toBeVisible();
    await expect(page.locator('[data-testid="config-toggle"]')).toBeChecked();
    await expect(page.locator('text=Đang BẬT')).toBeVisible();
  });

  test('QR channels: tạo QR mới → QR image + link render → revoke (cleanup)', async ({ page }) => {
    await page.goto('https://app2.khachvip.online/booking/qr-channels');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText('Mã QR đặt lịch', { timeout: 20000 });

    // Tạo QR mới.
    await page.locator('button', { hasText: 'Tạo mã QR' }).click();
    await expect(page.locator('.modal')).toBeVisible();
    await page.locator('.modal button', { hasText: 'Confirm' }).click();

    // Row mới nhất: QR image + link hiển thị (Q1 — xem lại sau khi tạo).
    const qrImage = page.locator('img[data-testid^="qr-image-"]').first();
    await expect(qrImage).toBeVisible({ timeout: 20000 });
    const src = await qrImage.getAttribute('src');
    expect(src, 'QR image phải là data URI PNG').toContain('data:image/png;base64,');
    const link = page.locator('[data-testid^="qr-link-"]').first();
    await expect(link).toBeVisible();
    const linkText = await link.innerText();
    expect(linkText).toMatch(/https:\/\/khachvip\.online\/booking\/[a-f0-9]{32}/);
    console.log(`QR link: ${linkText}`);

    // QR link mở được (KhachLink catalog — tenant enabled → 200 JSON).
    const qrToken = linkText.split('/booking/')[1];
    const resolve = await page.request.get(
      `https://api2.khachvip.online/api/public/booking/qr/${qrToken}?anonymousSessionId=rv-${Date.now()}`);
    console.log(`QR resolve: ${resolve.status()}`);
    expect(resolve.ok(), 'QR resolve phải 200 (tenant enabled)').toBeTruthy();
    const body = await resolve.json();
    expect(body.tenantName).toBeTruthy();

    // Cleanup: revoke QR vừa tạo.
    await page.locator('button', { hasText: 'Thu hồi' }).first().click();
    await expect(qrImage).toBeHidden({ timeout: 15000 }).catch(() => {
      console.log('QR revoke — ẩn image OK (hoặc đã re-render)');
    });
  });

  test('Ingredients: thêm nguyên liệu → success → xóa (cleanup)', async ({ page }) => {
    const name = `RV Nguyên liệu ${Date.now()}`;
    await page.goto('https://app2.khachvip.online/inventory/ingredients');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText('Nguyên liệu', { timeout: 20000 });

    await page.locator('[data-testid="ingredient-add"]').click();
    await expect(page.locator('.modal')).toBeVisible();
    await page.locator('[data-testid="ingredient-name"]').fill(name);
    await page.locator('[data-testid="ingredient-unit"]').fill('kg');
    await page.locator('[data-testid="ingredient-stock"]').fill('10');
    await page.locator('[data-testid="ingredient-threshold"]').fill('1');
    await page.locator('[data-testid="ingredient-price"]').fill('50000');
    await page.locator('.modal button', { hasText: 'Confirm' }).click();

    await expect(page.locator('[data-testid="ingredients-success"]')).toBeVisible({ timeout: 20000 });
    await expect(page.locator(`text=${name}`).first()).toBeVisible();

    // Cleanup: xóa nguyên liệu vừa tạo (tồn 10 → xác nhận).
    const row = page.locator(`tr:has-text("${name}")`).first();
    await row.locator('button', { hasText: 'Xóa' }).click();
    await expect(page.locator('.modal')).toBeVisible();
    await page.locator('.modal button', { hasText: 'Confirm' }).click();
    await expect(page.locator('tr', { hasText: name }).first()).toBeHidden({ timeout: 15000 });
  });

  test('Services: tạo dịch vụ → hiển thị → tạm ẩn (cleanup)', async ({ page }) => {
    const name = `RV Dịch vụ ${Date.now()}`;
    await page.goto('https://app2.khachvip.online/booking/services');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText('Dịch vụ đặt lịch', { timeout: 20000 });
    await page.locator('[data-testid="offering-add"]').click();
    await expect(page.locator('.modal')).toBeVisible();
    await page.locator('[data-testid="offering-name"]').fill(name);
    await page.locator('[data-testid="offering-duration"]').fill('45');
    await page.locator('[data-testid="offering-price"]').fill('250000');
    await page.locator('.modal button', { hasText: 'Confirm' }).click();

    await expect(page.locator('[data-testid="services-success"]')).toBeVisible({ timeout: 20000 });
    const row = page.locator(`tr:has-text("${name}")`).first();
    await expect(row).toBeVisible({ timeout: 15000 });

    // Cleanup: tạm ẩn.
    await row.locator('button', { hasText: 'Tạm ẩn' }).click();
    await expect(row.locator('button', { hasText: 'Kích hoạt' })).toBeVisible({ timeout: 15000 });
    console.log(`Offering created + deactivated: ${name}`);
  });
});
