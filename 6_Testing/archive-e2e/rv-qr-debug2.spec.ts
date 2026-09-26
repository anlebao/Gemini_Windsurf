import { test, expect } from '@playwright/test';

const BASE_URL = 'https://diemthuong2.khachvip.online';
const VALID_GUID = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890';
const TENANT_GUID = '00000000-0000-0000-0000-000000000001';

test('QR Wallet — debug fullscreen QR', async ({ page }) => {
    // Capture console logs
    page.on('console', msg => console.log(`[BROWSER ${msg.type()}]`, msg.text()));

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
    await page.waitForTimeout(5000);

    // Click tab
    await page.locator('text=Vé của tôi').first().click();
    await page.waitForTimeout(1000);

    // Screenshot before tap
    await page.screenshot({ path: 'test-results/rv-before-tap.png' });

    // Tap session
    await page.locator('text=51F-12345').first().click();
    await page.waitForTimeout(5000);

    // Screenshot after tap
    await page.screenshot({ path: 'test-results/rv-after-tap.png' });

    // Check overlay content
    const overlayHtml = await page.locator('.qr-fullscreen-overlay').innerHTML().catch(() => 'NO OVERLAY');
    console.log('[DEBUG] Overlay HTML:', overlayHtml.substring(0, 1000));

    // Check if "Đang tạo mã QR..." is visible (means JS didn't return data URL)
    const loadingCount = await page.locator('text=Đang tạo mã QR').count();
    console.log('[DEBUG] "Đang tạo mã QR..." count:', loadingCount);

    // Check if "Vé không hợp lệ" is visible
    const invalidCount = await page.locator('text=Vé không hợp lệ').count();
    console.log('[DEBUG] "Vé không hợp lệ" count:', invalidCount);

    // Check if canvas exists (old code)
    const canvasCount = await page.locator('#qr-fullscreen-canvas').count();
    console.log('[DEBUG] canvas count:', canvasCount);

    // Check if img exists
    const imgCount = await page.locator('.qr-fullscreen-img').count();
    console.log('[DEBUG] .qr-fullscreen-img count:', imgCount);

    // Check if vananQR is defined
    const vananQRDefined = await page.evaluate(() => typeof (window as any).vananQR);
    console.log('[DEBUG] vananQR type:', vananQRDefined);

    // Check if vananQrWallet is defined
    const walletDefined = await page.evaluate(() => typeof (window as any).vananQrWallet);
    console.log('[DEBUG] vananQrWallet type:', walletDefined);

    // Try calling generateQrDataUrl directly
    const result = await page.evaluate(() => {
        const w = (window as any).vananQrWallet;
        if (!w) return 'vananQrWallet not defined';
        if (!w.generateQrDataUrl) return 'generateQrDataUrl not defined';
        return 'generateQrDataUrl exists';
    });
    console.log('[DEBUG] generateQrDataUrl:', result);
});
