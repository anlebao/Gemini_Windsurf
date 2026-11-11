// VOICE SERVICE C3 (2026-11-11, Gate 4): /admin/voice — SystemAdmin bật/tắt voice per-tenant
// (kill-switch VoiceFeatureEnabled + VoiceOpenCalling) qua Gateway /api/admin/voice-config.
// Chạy với ShopERP production/local + SystemAdmin session:
//   npx playwright test --config=playwright-rv-voice-admin.config.ts
// (spec skip nếu VOICE_ADMIN_TEST_URL chưa set — convention voice-chat.spec.ts).
import { test, expect } from '@playwright/test';

const URL = process.env.VOICE_ADMIN_TEST_URL || '';

test('admin voice page renders tenant list with toggles and wallet link', async ({ page }) => {
    if (!URL) test.skip(true, 'Set VOICE_ADMIN_TEST_URL (ShopERP /admin/voice as SystemAdmin) to run.');

    await page.goto(URL);
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: /Cấu hình gọi thoại/ })).toBeVisible();
    const rows = page.locator('[data-testid="voice-config-row"]');
    await expect(rows.first()).toBeVisible();
    await expect(page.locator('a[href="/wallet/phi-nen-tang"]').first()).toBeVisible();
});

test('admin voice page saves config and shows success alert', async ({ page }) => {
    if (!URL) test.skip(true, 'Set VOICE_ADMIN_TEST_URL to run.');

    await page.goto(URL);
    await page.waitForLoadState('networkidle');

    // Lưu giữ nguyên giá trị hiện tại (idempotent — không đổi cấu hình thật).
    const row = page.locator('[data-testid="voice-config-row"]').first();
    await row.locator('.vanan-button').first().click();
    await expect(page.locator('.vanan-alert, .alert-success, [class*="success"]').first()).toBeVisible({ timeout: 10000 });
});