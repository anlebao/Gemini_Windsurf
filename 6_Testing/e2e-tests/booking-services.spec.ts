import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// Fix 2026-10-09 (bug 2 — Gate 4): Quản lý dịch vụ đặt lịch /booking/services.
// Validates: render + Sitemap + tạo dịch vụ (offering) → hiển thị → tạm ẩn (cleanup isActive=false).
// Self-gating (cong-no pattern): skip khi ENABLE_E2E=false; dev login tolerant (production RV storageState).

const config = loadEnvConfig();
const reporter = new TestReporter('Booking Services E2E');
const shopErpBase = process.env.BOOKING_SHOPERP_URL || config.SHOPERP_URL;

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VanAn Ecosystem - Dịch vụ đặt lịch (Booking Services)', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting Booking Services E2E...');
  });

  test.beforeEach(async ({ page, request }) => {
    try {
      const response = await request.post(`${shopErpBase}/dev/login`, { timeout: 8000 });
      if (response.ok()) {
        const body = await response.json();
        console.log(`Dev login successful: tenantId=${body.tenantId}, role=${body.role}`);
      } else {
        console.log(`Dev login unavailable (${response.status()}) — dùng storageState (production RV)`);
      }
    } catch {
      console.log('Dev login unavailable — dùng storageState (production RV)');
    }
    await page.goto(`${shopErpBase}/dashboard`);
    await page.waitForLoadState('networkidle');
  });

  test('Dịch vụ đặt lịch render + Sitemap link', async ({ page }) => {
    await page.goto(`${shopErpBase}/booking/services`);
    await page.waitForLoadState('networkidle');

    await expect(page.locator('h1')).toContainText('Dịch vụ đặt lịch', { timeout: 15000 });
    await expect(page.locator('[data-testid="offering-add"]')).toBeVisible({ timeout: 15000 });

    await page.goto(`${shopErpBase}/sitemap`);
    await page.waitForLoadState('networkidle');
    await expect(page.locator('[data-testid="link-booking-services"]')).toBeVisible({ timeout: 15000 });
  });

  test('Tạo dịch vụ → hiển thị → tạm ẩn (cleanup)', async ({ page }) => {
    const name = `RV Dịch vụ ${Date.now()}`;

    await page.goto(`${shopErpBase}/booking/services`);
    await page.waitForLoadState('networkidle');
    await expect(page.locator('h1')).toContainText('Dịch vụ đặt lịch', { timeout: 15000 });

    // Tạo dịch vụ.
    await page.locator('[data-testid="offering-add"]').click();
    await expect(page.locator('.modal')).toBeVisible();
    await page.locator('[data-testid="offering-name"]').fill(name);
    await page.locator('[data-testid="offering-duration"]').fill('45');
    await page.locator('[data-testid="offering-price"]').fill('250000');
    await page.locator('.modal button', { hasText: 'Confirm' }).click();

    await expect(page.locator('[data-testid="services-success"]')).toBeVisible({ timeout: 20000 });
    await expect(page.locator(`tr:has-text("${name}")`).first()).toBeVisible({ timeout: 15000 });

    // Cleanup: tạm ẩn (isActive=false) — không xóa được (không có DELETE endpoint).
    const row = page.locator(`tr:has-text("${name}")`).first();
    const toggle = row.locator('button', { hasText: 'Tạm ẩn' });
    await expect(toggle).toBeVisible({ timeout: 10000 });
    await toggle.click();
    await expect(row.locator('button', { hasText: 'Kích hoạt' })).toBeVisible({ timeout: 15000 });
    console.log(`Offering created + deactivated: ${name}`);
  });
});
