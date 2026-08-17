const { chromium } = require('playwright');

(async () => {
    const browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({
        // Clear all caches/storage to simulate fresh visit
        bypassCSP: true,
    });
    const page = await context.newPage();

    // Collect console logs
    const logs = [];
    page.on('console', msg => logs.push(`[${msg.type()}] ${msg.text()}`));

    console.log('=== RV: Navigate to timlathay.com (disabled instance) ===');
    await page.goto('https://timlathay.com', { waitUntil: 'networkidle', timeout: 30000 });

    // Wait for Blazor WASM to boot
    console.log('Waiting for Blazor WASM to render...');
    await page.waitForTimeout(8000);

    // Check for disabled page
    const disabledPage = await page.locator('.khachlink-disabled-page, .disabled-card').count();
    const disabledText = await page.locator('h1:has-text("Instance tạm ngưng")').count();
    const oldHeader = await page.locator('.khachlink-app-header, .app-header-btn').count();
    const cartIcon = await page.locator('a[aria-label="Giỏ hàng"]').count();
    const profileIcon = await page.locator('a[aria-label="Tài khoản"]').count();

    console.log('');
    console.log('=== RESULTS ===');
    console.log(`Disabled page (.khachlink-disabled-page): ${disabledPage > 0 ? 'FOUND ✅' : 'NOT FOUND ❌'}`);
    console.log(`Disabled text (h1 "Instance tạm ngưng"): ${disabledText > 0 ? 'FOUND ✅' : 'NOT FOUND ❌'}`);
    console.log(`Old header (.khachlink-app-header): ${oldHeader > 0 ? 'FOUND ❌ (should NOT render)' : 'NOT FOUND ✅'}`);
    console.log(`Cart icon: ${cartIcon > 0 ? 'FOUND ❌ (should NOT show for disabled)' : 'NOT FOUND ✅'}`);
    console.log(`Profile icon: ${profileIcon > 0 ? 'FOUND ❌ (should NOT show for disabled)' : 'NOT FOUND ✅'}`);

    // Check localStorage for instance config cache
    const lsKeys = await page.evaluate(() => Object.keys(localStorage));
    const instanceConfig = await page.evaluate(() => localStorage.getItem('khachlink_instance_config_v2'));
    console.log('');
    console.log('=== localStorage ===');
    console.log(`Keys: ${lsKeys.join(', ') || '(empty)'}`);
    console.log(`khachlink_instance_config_v2: ${instanceConfig || '(not set)'}`);

    // Check page title
    const title = await page.title();
    console.log('');
    console.log(`Page title: ${title}`);

    // Print relevant console logs
    console.log('');
    console.log('=== Console logs (filtered) ===');
    logs.filter(l => l.includes('Instance') || l.includes('config') || l.includes('error') || l.includes('Error') || l.includes('fetch')).forEach(l => console.log(l));

    // Screenshot
    await page.screenshot({ path: 'rv-timlathay-disabled.png', fullPage: true });
    console.log('');
    console.log('Screenshot saved: rv-timlathay-disabled.png');

    await browser.close();

    // Summary
    const pass = disabledPage > 0 && disabledText > 0 && oldHeader === 0 && cartIcon === 0 && profileIcon === 0;
    console.log('');
    console.log(pass ? '=== RV PASS: Disabled page shown, no FullCommerce fallback ===' : '=== RV FAIL: See details above ===');
    process.exit(pass ? 0 : 1);
})();
