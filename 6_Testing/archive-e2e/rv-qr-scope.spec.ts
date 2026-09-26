import { test } from '@playwright/test';

test('Debug qrcode.js scope', async ({ page }) => {
    await page.goto('https://diemthuong2.khachvip.online/qr/wallet', { waitUntil: 'networkidle' });
    await page.waitForTimeout(3000);

    // Check if qrcode function works at all
    const result = await page.evaluate(() => {
        try {
            const qr = (window as any).qrcode(5, 'M');
            qr.addData('test');
            qr.make();
            return 'qrcode() works, moduleCount=' + qr.getModuleCount();
        } catch (e: any) {
            return 'qrcode() FAILED: ' + e.message;
        }
    });
    console.log('[DEBUG]', result);

    // Check QRErrorCorrectionLevel scope
    const scopeCheck = await page.evaluate(() => {
        try {
            // Try to access QRErrorCorrectionLevel — should be undefined (it's inside IIFE)
            return 'QRErrorCorrectionLevel in global: ' + typeof (window as any).QRErrorCorrectionLevel;
        } catch (e: any) {
            return 'scope check error: ' + e.message;
        }
    });
    console.log('[DEBUG]', scopeCheck);

    // Try vananQR.generate on a test canvas
    const genTest = await page.evaluate(() => {
        try {
            // Create a canvas
            const canvas = document.createElement('canvas');
            canvas.id = 'test-qr-canvas';
            document.body.appendChild(canvas);

            const v = (window as any).vananQR;
            if (!v) return 'vananQR not defined';
            if (!v.generate) return 'vananQR.generate not defined';

            v.generate('test-qr-canvas', 'test123', 200, 200);

            const ctx = canvas.getContext('2d');
            const imageData = ctx!.getImageData(0, 0, canvas.width, canvas.height);
            const nonWhitePixels = Array.from(imageData.data).filter((v, i) => i % 4 === 0 && v < 128).length;

            return 'generate() works, canvas=' + canvas.width + 'x' + canvas.height + ', darkPixels=' + nonWhitePixels;
        } catch (e: any) {
            return 'generate() FAILED: ' + e.message;
        }
    });
    console.log('[DEBUG]', genTest);
});
