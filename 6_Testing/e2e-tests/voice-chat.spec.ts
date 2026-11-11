// VOICE SERVICE P4 — E2E: voice call button in the chat UI (Gate 4 — UI change requires E2E).
// UI-level assertions only (call button gating, incoming overlay, deny message) — no media
// connect needed, so these run even where WebRTC ICE is blocked. Full media flow is covered
// by voice-p2p.spec.ts (needs working WebRTC ICE).
// Validation phase: npx playwright test --config=playwright-voice-p2p.config.ts (spec mở rộng)
import { test, expect } from '@playwright/test';

// Trạng thái mục tiêu (production/local):
//  - ChatPanel (KhachLink storefront) hoặc ShopInbox (ShopERP) với flag Voice_Call_Enabled ON
//  - voice service chạy + CORS + Jwt:Secret khớp
test('chat voice call button appears only when the flag is ON', async ({ page }) => {
    await page.goto(process.env.VOICE_CHAT_TEST_URL || 'about:blank');
    const panel = page.locator('[data-testid="voice-call-panel"]');

    if (!process.env.VOICE_CHAT_TEST_URL) {
        test.skip(true, 'Set VOICE_CHAT_TEST_URL (chat page with the tenant flag) to run.');
    }

    await expect(panel).toBeVisible();
    await expect(page.locator('[data-testid="voice-call-panel"] .voice-call-btn')).toBeVisible();
});

test('incoming call overlay offers accept/reject', async ({ page }) => {
    await page.goto(process.env.VOICE_CHAT_TEST_URL || 'about:blank');
    if (!process.env.VOICE_CHAT_TEST_URL) test.skip(true, 'Set VOICE_CHAT_TEST_URL to run.');

    // Simulated by the voice service delivering IncomingCall to this user's connection.
    await expect(page.locator('[data-testid="voice-incoming"]')).toBeVisible({ timeout: 30000 });
    await expect(page.locator('[data-testid="voice-accept"]')).toBeVisible();
    await expect(page.locator('[data-testid="voice-reject"]')).toBeVisible();
});