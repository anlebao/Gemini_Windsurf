import { test, expect } from '@playwright/test';

const BASE_URL = 'https://diemthuong.khachvip.online';

test('pwa-install — check install prompt appears', async ({ browser }) => {
  const context = await browser.newContext({
    baseURL: BASE_URL,
    ignoreHTTPSErrors: true,
    // Emulate mobile device for PWA install prompt
    userAgent: 'Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36',
    viewport: { width: 412, height: 915 },
    isMobile: true,
    hasTouch: true,
  });

  const page = await context.newPage();
  const consoleErrors: string[] = [];
  page.on('console', msg => { if (msg.type() === 'error') consoleErrors.push(msg.text()); });

  try {
    await page.goto(BASE_URL);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(5000); // Wait 5s for PWA prompt (shows after 3s)

    // Check if PWA install prompt is visible
    const pwaPrompt = page.locator('.pwa-install-prompt');
    const promptStyle = await pwaPrompt.getAttribute('style');
    const promptVisible = await pwaPrompt.isVisible().catch(() => false);

    // Check for install button
    const installBtn = page.locator('.pwa-install-prompt button:has-text("Cài đặt"), .pwa-install-prompt button:has-text("Hướng dẫn")');
    const btnVisible = await installBtn.isVisible().catch(() => false);
    const btnCount = await installBtn.count();

    // Check Blazor circuit
    const blazorErrors = consoleErrors.filter(e => e.includes('circuit') || e.includes('blazor'));

    // Check localStorage
    const dismissed = await page.evaluate(() => localStorage.getItem('pwa_dismissed')).catch(() => 'N/A');

    console.log('Prompt style:', promptStyle);
    console.log('Prompt visible:', promptVisible);
    console.log('Install button count:', btnCount, 'visible:', btnVisible);
    console.log('Blazor errors:', JSON.stringify(blazorErrors.slice(0, 3)));
    console.log('All console errors:', JSON.stringify(consoleErrors.slice(0, 5)));
    console.log('pwa_dismissed:', dismissed);

    // Check if blazor-error-ui is visible
    const errorUiVisible = await page.locator('#blazor-error-ui').isVisible().catch(() => false);
    console.log('Blazor error UI visible:', errorUiVisible);
  } finally {
    await context.close();
  }
});
