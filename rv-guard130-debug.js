const { chromium } = require('playwright');

(async () => {
    const browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ bypassCSP: true, ignoreHTTPSErrors: true });
    const page = await context.newPage();

    const requests = [];
    page.on('requestfailed', req => requests.push(`FAIL: ${req.url()} ${req.failure()?.errorText}`));
    page.on('response', resp => {
        if (resp.url().includes('qrcode') || resp.url().includes('guard-camera') || resp.url().includes('.js')) {
            requests.push(`${resp.status()} ${resp.url()}`);
        }
    });

    const APP = 'https://app2.khachvip.online';
    const PASSWORD = '2026@vanan';

    console.log('=== RV #130: Debug QR lib loading ===');
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
    await page.waitForTimeout(5000);

    // Check all script tags
    console.log('');
    console.log('2. Script tags on page...');
    const scripts = await page.evaluate(() => {
        return Array.from(document.querySelectorAll('script')).map(s => ({
            src: s.src || '(inline)',
            type: s.type,
            async: s.async,
            defer: s.defer
        }));
    });
    scripts.forEach((s, i) => console.log(`   ${i}: ${s.src} (async=${s.async}, defer=${s.defer})`));

    // Check if qrcode.js was requested
    console.log('');
    console.log('3. Network requests for JS files...');
    requests.forEach(r => console.log(`   ${r}`));

    // Check vananQR
    console.log('');
    console.log('4. Check vananQR global...');
    const qrCheck = await page.evaluate(() => {
        return {
            vananQR: typeof window.vananQR,
            QRCode: typeof window.QRCode,
            qrcode: typeof window.qrcode,
            generateQrImage: typeof window.generateQrImage,
            _drawToCanvas: typeof window._drawToCanvas,
        };
    });
    console.log(`   ${JSON.stringify(qrCheck)}`);

    // Try to manually load qrcode.js
    console.log('');
    console.log('5. Manually fetch /js/lib/qrcode.js...');
    const qrFetch = await page.evaluate(async () => {
        try {
            const r = await fetch('/js/lib/qrcode.js');
            return { status: r.status, length: (await r.text()).length };
        } catch (e) { return { error: e.message }; }
    });
    console.log(`   ${JSON.stringify(qrFetch)}`);

    // Check page source for script references
    console.log('');
    console.log('6. Page HTML (script-related)...');
    const html = await page.content();
    const scriptMatches = html.match(/<script[^>]*src=["'][^"']*["'][^>]*>/g) || [];
    scriptMatches.forEach(s => console.log(`   ${s}`));

    // Check for guard-camera.js reference
    const hasGuardCamera = html.includes('guard-camera.js');
    const hasQrcodeLib = html.includes('qrcode.js') || html.includes('vananQR');
    console.log('');
    console.log(`   guard-camera.js referenced: ${hasGuardCamera}`);
    console.log(`   qrcode.js/vananQR referenced: ${hasQrcodeLib}`);

    await browser.close();
    process.exit(0);
})();
