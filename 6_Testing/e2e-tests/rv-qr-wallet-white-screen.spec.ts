import { test, expect } from '@playwright/test';

const BASE_URL = 'https://diemthuong2.khachvip.online';

// Use valid GUID for sessionId + tenantId (WalletSession.SessionId is Guid type)
const VALID_GUID = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890';
const TENANT_GUID = '00000000-0000-0000-0000-000000000001';

test('QR Wallet — tap vé → QR code hiển thị (không white screen)', async ({ page }) => {
    const session = {
        sessionId: VALID_GUID,
        qrPayload: 'VANAN:GUARD:SESSION:' + VALID_GUID,
        shortCode: null,
        plateNumber: '51F-12345',
        issuedAt: new Date().toISOString(),
        tenantId: TENANT_GUID,
        claimedAt: new Date().toISOString()
    };

    await page.addInitScript((s) => {
        localStorage.setItem('vanan_qr_wallet', JSON.stringify([s]));
    }, session);

    await page.goto(`${BASE_URL}/qr/wallet`, { waitUntil: 'networkidle' });
    await page.waitForTimeout(5000); // Wait for Blazor WASM to fully load

    // Check "Vé của tôi" tab
    const walletTab = page.locator('text=Vé của tôi').first();
    await expect(walletTab).toBeVisible({ timeout: 15000 });

    // Check session card with plate number
    const sessionCard = page.locator('text=51F-12345').first();
    await expect(sessionCard).toBeVisible({ timeout: 10000 });

    // Tap to show fullscreen QR
    await sessionCard.click();
    await page.waitForTimeout(3000);

    // Verify fullscreen overlay
    const overlay = page.locator('.qr-fullscreen-overlay');
    await expect(overlay).toBeVisible({ timeout: 5000 });

    // KEY: QR image displayed (not white screen)
    const qrImg = page.locator('.qr-fullscreen-img');
    await expect(qrImg).toBeVisible({ timeout: 10000 });

    const src = await qrImg.getAttribute('src');
    expect(src).toBeTruthy();
    expect(src).toContain('data:image/png');

    const box = await qrImg.boundingBox();
    expect(box).toBeTruthy();
    expect(box!.width).toBeGreaterThan(50);
    expect(box!.height).toBeGreaterThan(50);

    await page.screenshot({ path: 'test-results/rv-qr-displayed.png' });
    console.log('[RV PASS] QR image:', { srcLen: src!.length, w: box!.width, h: box!.height });
});

test('QR Wallet — ShortCode vé → hiển thị mã 6 số (không "Vé không hợp lệ")', async ({ page }) => {
    const session = {
        sessionId: 'b2c3d4e5-f6a7-8901-bcde-f12345678901',
        qrPayload: null,
        shortCode: 'ABC123',
        plateNumber: '59P1-678.90',
        issuedAt: new Date().toISOString(),
        tenantId: TENANT_GUID,
        claimedAt: new Date().toISOString()
    };

    await page.addInitScript((s) => {
        localStorage.setItem('vanan_qr_wallet', JSON.stringify([s]));
    }, session);

    await page.goto(`${BASE_URL}/qr/wallet`, { waitUntil: 'networkidle' });
    await page.waitForTimeout(5000);

    const walletTab = page.locator('text=Vé của tôi').first();
    await expect(walletTab).toBeVisible({ timeout: 15000 });

    const sessionCard = page.locator('text=59P1-678.90').first();
    await expect(sessionCard).toBeVisible({ timeout: 10000 });
    await sessionCard.click();
    await page.waitForTimeout(2000);

    const overlay = page.locator('.qr-fullscreen-overlay');
    await expect(overlay).toBeVisible({ timeout: 5000 });

    const shortCode = page.locator('.qr-fullscreen-code');
    await expect(shortCode).toBeVisible({ timeout: 5000 });
    await expect(shortCode).toContainText('ABC123');

    await page.screenshot({ path: 'test-results/rv-shortcode-displayed.png' });
    console.log('[RV PASS] Short code displayed');
});
