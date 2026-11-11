// Voice Service P2 — E2E WebRTC thật qua demo SDK (SRS: "do not use fake successful WebRTC
// connections in tests" — đây là P2P thật với fake MEDIA DEVICE, không fake kết nối).
// Chạy: npx playwright test --config=playwright-voice-p2p.config.ts (validation phase).
import { test, expect } from '@playwright/test';
import { createHmac } from 'node:crypto';

const SECRET = process.env.VOICE_E2E_SECRET || 'voice-e2e-secret-0123456789-abcdef-123456';
const TENANT = 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee';
const USER_A = '11111111-1111-4111-8111-111111111111';
const USER_B = '22222222-2222-4222-8222-222222222222';
const HUB = 'http://127.0.0.1:5070/hubs/voice';
const API = 'http://127.0.0.1:5070';
const DEMO = 'http://127.0.0.1:5080/demo/index.html';

function mintJwt(userId) {
    const now = Math.floor(Date.now() / 1000);
    const b64 = (obj) => Buffer.from(JSON.stringify(obj)).toString('base64url');
    const signingInput = `${b64({ alg: 'HS256', typ: 'JWT' })}.${b64({
        sub: userId, tenant_id: TENANT, nbf: now, exp: now + 3600, iat: now,
        iss: 'VanAnShopERP', aud: 'VanAnApi'
    })}`;
    return `${signingInput}.${createHmac('sha256', SECRET).update(signingInput).digest('base64url')}`;
}

async function openDemo(page, role, userId) {
    const url = `${DEMO}?role=${role}&token=${mintJwt(userId)}&target=${USER_B}&hub=${HUB}`;
    await page.goto(url);
    await expect(page.locator('#state')).toHaveText('idle');
}

// Enable open calling for the E2E tenant via the admin endpoint (§14.4) — context-less calls.
test.beforeAll(async ({ request }) => {
    const response = await request.put(`${API}/v1/tenants/${TENANT}/voice-config`, {
        data: { voiceFeatureEnabled: true, voiceOpenCalling: true }
    });
    expect(response.ok()).toBe(true);
});

test('P2P call between two browser tabs (real WebRTC, fake media)', async ({ browser }) => {
    const context = await browser.newContext();
    const caller = await context.newPage();
    const callee = await context.newPage();

    await openDemo(caller, 'caller', USER_A);
    await openDemo(callee, 'callee', USER_B);

    // Caller dials → callee rings.
    await caller.click('#btn-call');
    await expect(callee.locator('#state')).toHaveText('ringing');

    // Callee accepts → both negotiate → connected (media flows with fake devices).
    await callee.click('#btn-accept');
    await expect(caller.locator('#state')).toHaveText('connected');
    await expect(callee.locator('#state')).toHaveText('connected');

    // Both sides attached real media streams (fake device stream).
    const callerHasRemote = await caller.evaluate(
        () => !!document.getElementById('remote-audio').srcObject);
    const calleeHasRemote = await callee.evaluate(
        () => !!document.getElementById('remote-audio').srcObject);
    expect(callerHasRemote).toBe(true);
    expect(calleeHasRemote).toBe(true);

    // Caller ends → both terminal.
    await caller.click('#btn-end');
    await expect(caller.locator('#state')).toHaveText('ended');
    await expect(callee.locator('#state')).toHaveText('ended');

    await context.close();
});

test('mic is NOT requested before user action (demo shows idle with no stream)', async ({ browser }) => {
    const context = await browser.newContext();
    const page = await context.newPage();
    await openDemo(page, 'caller', USER_A);

    // No local stream attached while idle (SDK requests mic only on accept/start-answered).
    const hasLocal = await page.evaluate(() => !!document.getElementById('local-audio').srcObject);
    expect(hasLocal).toBe(false);

    await context.close();
});