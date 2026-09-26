import { test, expect } from '@playwright/test';

const BASE_URL = 'https://diemthuong.khachvip.online';

test('DEBUG: PWA install — beforeinstallprompt capture + deferredPrompt state', async ({ browser }) => {
  const context = await browser.newContext({
    baseURL: BASE_URL,
    ignoreHTTPSErrors: true,
    userAgent: 'Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36',
    viewport: { width: 412, height: 915 },
    isMobile: true,
    hasTouch: true,
  });

  const page = await context.newPage();
  const consoleLogs: string[] = [];
  page.on('console', msg => consoleLogs.push(`[${msg.type()}] ${msg.text()}`));

  // Inject a listener BEFORE page scripts run to catch beforeinstallprompt
  await page.addInitScript(() => {
    window.__bipFired = false;
    window.__bipDetail = null;
    window.addEventListener('beforeinstallprompt', (e) => {
      window.__bipFired = true;
      window.__bipDetail = {
        platforms: (e as any).platforms,
        userChoice: 'pending',
      };
      console.log('[DEBUG-INJECTED] beforeinstallprompt fired!');
    });
  });

  await page.goto(BASE_URL, { waitUntil: 'networkidle', timeout: 60000 });
  await page.waitForTimeout(8000);

  // Check: did beforeinstallprompt fire?
  const bipFired = await page.evaluate(() => (window as any).__bipFired);
  console.log('=== beforeinstallprompt fired:', bipFired);

  // Check: deferredPrompt in vananPWA
  const deferredPrompt = await page.evaluate(() => (window as any).vananPWA?.deferredPrompt);
  console.log('=== vananPWA.deferredPrompt is null:', deferredPrompt === null || deferredPrompt === undefined);

  // Check: isInstalled
  const isInstalled = await page.evaluate(() => (window as any).vananPWA?.isInstalled);
  console.log('=== vananPWA.isInstalled:', isInstalled);

  // Check: pwa_dismissed in localStorage
  const dismissed = await page.evaluate(() => localStorage.getItem('pwa_dismissed'));
  console.log('=== pwa_dismissed:', dismissed);

  // Check: install prompt visible?
  const promptVisible = await page.locator('.pwa-install-prompt').isVisible({ timeout: 3000 }).catch(() => false);
  const promptDisplay = await page.locator('.pwa-install-prompt').getAttribute('style').catch(() => 'N/A');
  console.log('=== Install prompt visible:', promptVisible, '| style:', promptDisplay);

  // Check: service worker state
  const swState = await page.evaluate(async () => {
    const reg = await navigator.serviceWorker.getRegistration();
    return reg ? { scriptURL: reg.active?.scriptURL, state: reg.active?.state } : null;
  });
  console.log('=== SW state:', JSON.stringify(swState));

  // Check: manifest link
  const manifestLink = await page.locator('link[rel="manifest"]').getAttribute('href');
  console.log('=== manifest link:', manifestLink);

  // Check: HTTPS (required for beforeinstallprompt)
  console.log('=== protocol:', new URL(page.url()).protocol);

  // Print all PWA-related console logs
  const pwaLogs = consoleLogs.filter(l => l.includes('PWA') || l.includes('beforeinstallprompt') || l.includes('install'));
  console.log('=== PWA console logs:', JSON.stringify(pwaLogs, null, 2));

  // Try clicking install button and see what happens
  if (promptVisible) {
    console.log('=== Clicking install button...');
    const installBtn = page.locator('.pwa-install-prompt button:has-text("Cài đặt"), .pwa-install-prompt button:has-text("Hướng dẫn")');
    await installBtn.click().catch(() => console.log('Click failed'));
    await page.waitForTimeout(3000);

    // Check if alert appeared
    const dialogText = await page.evaluate(() => document.querySelector('.pwa-notification')?.textContent || 'no notification element');
    console.log('=== After click - dialog/notification:', dialogText);
  }

  await context.close();
});
