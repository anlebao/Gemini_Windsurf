import { test, expect } from '@playwright/test';

const BASE_URL = 'https://diemthuong2.khachvip.online';

test('QR Wallet — debug: screenshot sau khi load', async ({ page }) => {
    // Inject session
    await page.addInitScript(() => {
        const session = {
            sessionId: 'rv-test-001',
            qrPayload: 'VANAN:GUARD:SESSION:rv-test-001',
            shortCode: null,
            plateNumber: '51F-12345',
            issuedAt: new Date().toISOString(),
            tenantId: '00000000-0000-0000-0000-000000000001',
            claimedAt: new Date().toISOString()
        };
        localStorage.setItem('vanan_qr_wallet', JSON.stringify([session]));
    });

    await page.goto(`${BASE_URL}/qr/wallet`, { waitUntil: 'networkidle' });
    await page.waitForTimeout(5000); // Wait longer for Blazor WASM

    // Screenshot full page
    await page.screenshot({ path: 'test-results/rv-qr-wallet-loaded.png', fullPage: true });

    // Dump all visible text
    const bodyText = await page.locator('body').innerText();
    console.log('[DEBUG] Page text:', bodyText.substring(0, 2000));

    // Check localStorage content
    const lsContent = await page.evaluate(() => localStorage.getItem('vanan_qr_wallet'));
    console.log('[DEBUG] localStorage:', lsContent);

    // Check if "Vé của tôi" tab exists
    const tabCount = await page.locator('text=Vé của tôi').count();
    console.log('[DEBUG] "Vé của tôi" count:', tabCount);

    // Check if plate number text exists anywhere
    const plateCount = await page.locator('text=51F-12345').count();
    console.log('[DEBUG] "51F-12345" count:', plateCount);

    // Check for any error messages
    const errorCount = await page.locator('text=/[Ll]ỗi|[Ee]rror|exception/i').count();
    console.log('[DEBUG] Error text count:', errorCount);
});
