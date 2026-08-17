const { chromium } = require('playwright');

(async () => {
    const browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ bypassCSP: true, ignoreHTTPSErrors: true });
    const page = await context.newPage();

    const logs = [];
    page.on('console', msg => logs.push(`[${msg.type()}] ${msg.text()}`));
    page.on('pageerror', err => logs.push(`[pageerror] ${err.message}`));

    const APP = 'https://app2.khachvip.online';
    const PASSWORD = '2026@vanan';

    console.log('=== RV #130: Direct QR generation test ===');
    console.log('');

    // Login
    await page.goto(`${APP}/Login`, { waitUntil: 'networkidle', timeout: 30000 });
    await page.waitForTimeout(2000);
    await page.locator('#username').fill('baove@vanan.vn');
    await page.locator('#password').fill(PASSWORD);
    await Promise.all([
        page.waitForNavigation({ waitUntil: 'networkidle', timeout: 15000 }).catch(() => {}),
        page.locator('button[type="submit"]').click()
    ]);
    await page.waitForTimeout(3000);

    // Navigate to /guard/scan
    console.log('1. Navigate to /guard/scan...');
    await page.goto(`${APP}/guard/scan`, { waitUntil: 'networkidle', timeout: 30000 });
    await page.waitForTimeout(3000);

    // Test 1: Direct call generateQrImage
    console.log('');
    console.log('2. Call vananGuardCamera.generateQrImage directly...');
    const qrResult = await page.evaluate(async () => {
        try {
            // Check if vananGuardCamera exists
            if (!window.vananGuardCamera) {
                return { error: 'vananGuardCamera not found' };
            }
            // Call generateQrImage
            const base64 = await window.vananGuardCamera.generateQrImage('{"sc":"ABC123","sid":"00000000-0000-0000-0000-000000000001"}', 300);
            if (!base64) {
                return { error: 'generateQrImage returned null/empty' };
            }
            return {
                success: true,
                length: base64.length,
                prefix: base64.substring(0, 50),
                isPng: base64.startsWith('data:image/png')
            };
        } catch (e) {
            return { error: e.message, stack: e.stack?.substring(0, 300) };
        }
    });
    console.log(`   Result: ${JSON.stringify(qrResult)}`);

    // Test 2: Check vananQR after generateQrImage call
    console.log('');
    console.log('3. Check vananQR after call...');
    const qrCheck = await page.evaluate(() => ({
        vananQR: typeof window.vananQR,
        vananQRKeys: window.vananQR ? Object.keys(window.vananQR) : [],
    }));
    console.log(`   ${JSON.stringify(qrCheck)}`);

    // Test 3: Test generateQrToCanvas
    console.log('');
    console.log('4. Test generateQrToCanvas...');
    const canvasResult = await page.evaluate(async () => {
        try {
            // Create canvas element
            const canvas = document.createElement('canvas');
            canvas.id = 'test-qr-canvas';
            document.body.appendChild(canvas);

            const ok = await window.vananGuardCamera.generateQrToCanvas('test-qr-canvas', '{"sc":"TEST456","sid":"00000000-0000-0000-0000-000000000002"}', 200);
            const canvasEl = document.getElementById('test-qr-canvas');
            const ctx = canvasEl.getContext('2d');
            const imageData = ctx.getImageData(0, 0, canvasEl.width, canvasEl.height);
            // Count non-zero pixels (QR code has black pixels)
            let nonZero = 0;
            for (let i = 0; i < imageData.data.length; i += 4) {
                if (imageData.data[i] < 128) nonZero++; // dark pixels
            }
            return {
                success: ok,
                canvasWidth: canvasEl.width,
                canvasHeight: canvasEl.height,
                darkPixels: nonZero,
                hasContent: nonZero > 100
            };
        } catch (e) {
            return { error: e.message };
        }
    });
    console.log(`   ${JSON.stringify(canvasResult)}`);

    // Test 4: Check Scan page UI state
    console.log('');
    console.log('5. Check Scan page UI...');
    const uiCheck = await page.evaluate(() => {
        const inputs = Array.from(document.querySelectorAll('input')).map(i => ({
            name: i.name, type: i.type, placeholder: i.placeholder, value: i.value
        }));
        const buttons = Array.from(document.querySelectorAll('button')).map(b => ({
            text: b.textContent?.trim(), disabled: b.disabled, onclick: b.onclick?.toString()?.substring(0, 50)
        }));
        return { inputs, buttons: buttons.slice(0, 10) };
    });
    console.log(`   Inputs: ${JSON.stringify(uiCheck.inputs)}`);
    console.log(`   Buttons: ${JSON.stringify(uiCheck.buttons)}`);

    // Print errors
    const errors = logs.filter(l => l.includes('[error]') || l.includes('[pageerror]'));
    if (errors.length > 0) {
        console.log('');
        console.log('=== Errors ===');
        errors.slice(0, 10).forEach(l => console.log(l));
    }

    // Summary
    console.log('');
    console.log('=== SUMMARY ===');
    const qrPass = qrResult.success && qrResult.isPng;
    const canvasPass = canvasResult.success && canvasResult.hasContent;
    console.log(`QR image generation: ${qrPass ? 'PASS ✅' : 'FAIL ❌'}`);
    console.log(`QR canvas generation: ${canvasPass ? 'PASS ✅' : 'FAIL ❌'}`);

    await page.screenshot({ path: 'rv-guard130-qr.png', fullPage: true });
    await browser.close();
    process.exit(qrPass && canvasPass ? 0 : 1);
})();
