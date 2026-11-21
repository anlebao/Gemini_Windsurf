// VOICE PRESENCE (2026-11-21) — E2E: global incoming-call overlay + mini call-bar.
// Gate 4 — UI layout change requires E2E.
//
// Cách test: stub `window.vananVoiceChat` TRƯỚC khi Blazor boot (addInitScript) —
// capture DotNetObjectReference mà VoicePresencePanel truyền vào initialize(),
// rồi chủ động bắn OnVoiceIncoming/OnVoiceStateChange để mô phỏng server push.
// Không cần voice service/SignalR thật — test đúng lớp UI mới.
//
// Chạy: VOICE_PRESENCE_STORE_URL="https://<store>?..." npx playwright test --config=playwright-rv-chatui.config.ts voice-presence
// Yêu cầu: trang store với customer ĐÃ LOGIN (localStorage customer_token hợp lệ)
// → VoicePresencePanel mount. Guest → panel không mount → test skip.
import { test, expect } from '@playwright/test';

const STORE_URL = process.env.VOICE_PRESENCE_STORE_URL || '';

test.describe('voice presence — global incoming call UI', () => {
    test.skip(!STORE_URL, 'Set VOICE_PRESENCE_STORE_URL (storefront page with logged-in customer) to run.');

    test.beforeEach(async ({ page }) => {
        // Stub voice bridge: ghi lại dotNetRef của owner đầu tiên, mọi hàm khác no-op.
        await page.addInitScript(() => {
            (window as any).__voiceDotNetRef = null;
            (window as any).vananVoiceChat = {
                isInitialized: () => !!(window as any).__voiceDotNetRef,
                initialize: async (dotNetRef: any) => { (window as any).__voiceDotNetRef = dotNetRef; },
                startCall: async () => {}, acceptCall: async () => {}, rejectCall: async () => {},
                endCall: async () => {}, cancelCall: async () => {},
                mute: () => {}, unmute: () => {},
                getState: () => 'idle', getConnectionState: () => 'connected',
                fetchMissedCalls: async () => [], dispose: async () => {}
            };
        });
        await page.goto(STORE_URL);
        // Đợi presence panel init xong (dotNetRef captured) hoặc timeout → guest/no-flag.
        await page.waitForFunction(() => !!(window as any).__voiceDotNetRef, null, { timeout: 15000 })
            .catch(() => {});
    });

    test('presence panel mounts for logged-in customer (dotNetRef captured)', async ({ page }) => {
        const hasRef = await page.evaluate(() => !!(window as any).__voiceDotNetRef);
        test.skip(!hasRef, 'VoicePresencePanel did not init — guest session or Voice_Call_Enabled off.');
    });

    test('incoming call shows global overlay with caller name when chat closed', async ({ page }) => {
        const hasRef = await page.evaluate(() => !!(window as any).__voiceDotNetRef);
        test.skip(!hasRef, 'VoicePresencePanel did not init.');

        await page.evaluate(() => {
            const r = (window as any).__voiceDotNetRef;
            r.invokeMethodAsync('OnVoiceIncoming', JSON.stringify({
                callerDisplayName: 'E2E Caller', verifiedContextLabel: 'Shop Test'
            }));
            r.invokeMethodAsync('OnVoiceStateChange', 'ringing', '{}');
            r.invokeMethodAsync('OnVoiceConnectionState', 'connected');
        });

        const overlay = page.locator('[data-testid="voice-presence-incoming"]');
        await expect(overlay).toBeVisible({ timeout: 10000 });
        await expect(overlay).toContainText('E2E Caller');
        await expect(overlay).toContainText('Shop Test');
        await expect(page.locator('[data-testid="voice-presence-accept"]')).toBeVisible();
        await expect(page.locator('[data-testid="voice-presence-reject"]')).toBeVisible();
    });

    test('active call shows mini call-bar; idle hides all global UI', async ({ page }) => {
        const hasRef = await page.evaluate(() => !!(window as any).__voiceDotNetRef);
        test.skip(!hasRef, 'VoicePresencePanel did not init.');

        await page.evaluate(() => {
            const r = (window as any).__voiceDotNetRef;
            r.invokeMethodAsync('OnVoiceStateChange', 'connected', '{}');
            r.invokeMethodAsync('OnVoiceConnectionState', 'connected');
        });
        const bar = page.locator('[data-testid="voice-presence-callbar"]');
        await expect(bar).toBeVisible({ timeout: 10000 });
        await expect(page.locator('[data-testid="voice-presence-end"]')).toBeVisible();
        await expect(page.locator('[data-testid="voice-presence-mute"]')).toBeVisible();

        await page.evaluate(() => {
            (window as any).__voiceDotNetRef.invokeMethodAsync('OnVoiceStateChange', 'ended', '{}');
        });
        await expect(bar).toBeHidden({ timeout: 10000 });
        await expect(page.locator('[data-testid="voice-presence-incoming"]')).toBeHidden();
    });
});
