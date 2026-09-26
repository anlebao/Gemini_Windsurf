import { test, expect } from '@playwright/test';

const BASE_URL = 'https://diemthuong.khachvip.online';

test('DEEP DEBUG: all beforeinstallprompt prerequisites', async ({ browser }) => {
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

  // Inject listener BEFORE any page script
  await page.addInitScript(() => {
    (window as any).__bipEvents = [];
    window.addEventListener('beforeinstallprompt', (e) => {
      (window as any).__bipEvents.push({
        type: 'beforeinstallprompt',
        platforms: (e as any).platforms,
        timestamp: Date.now(),
      });
      console.log('[BIP-CAPTURED] beforeinstallprompt fired!');
    });
  });

  await page.goto(BASE_URL, { waitUntil: 'networkidle', timeout: 60000 });
  await page.waitForTimeout(10000);

  // 1. Check manifest link tag
  const manifestLink = await page.evaluate(() => {
    const link = document.querySelector('link[rel="manifest"]');
    return link ? {
      href: link.getAttribute('href'),
      rel: link.getAttribute('rel'),
      crossorigin: link.getAttribute('crossorigin'),
      type: link.getAttribute('type'),
    } : null;
  });
  console.log('=== 1. Manifest link tag:', JSON.stringify(manifestLink));

  // 2. Fetch manifest and check all required fields
  const manifestData = await page.evaluate(async () => {
    try {
      const resp = await fetch('/manifest.json');
      const text = await resp.text();
      const manifest = JSON.parse(text);
      return {
        status: resp.status,
        contentType: resp.headers.get('content-type'),
        rawLength: text.length,
        fields: {
          name: manifest.name,
          short_name: manifest.short_name,
          start_url: manifest.start_url,
          scope: manifest.scope,
          display: manifest.display,
          orientation: manifest.orientation,
          theme_color: manifest.theme_color,
          background_color: manifest.background_color,
          lang: manifest.lang,
          id: manifest.id,
        },
        icons: manifest.icons,
        has192Any: manifest.icons?.some((i: any) => i.sizes === '192x192'),
        has192Maskable: manifest.icons?.some((i: any) => i.sizes === '192x192' && i.purpose?.includes('maskable')),
        has512Any: manifest.icons?.some((i: any) => i.sizes === '512x512'),
        has512Maskable: manifest.icons?.some((i: any) => i.sizes === '512x512' && i.purpose?.includes('maskable')),
      };
    } catch (e: any) {
      return { error: e.message };
    }
  });
  console.log('=== 2. Manifest data:', JSON.stringify(manifestData, null, 2));

  // 3. Check SW registration + fetch handler
  const swInfo = await page.evaluate(async () => {
    const reg = await navigator.serviceWorker.getRegistration();
    if (!reg) return { registered: false };

    // Check if SW has a fetch handler by sending a test request
    const hasFetchHandler = await fetch('/test-sw-fetch-check-' + Date.now(), { mode: 'same-origin' })
      .then(r => ({ status: r.status, intercepted: true }))
      .catch(e => ({ error: e.message }));

    return {
      registered: true,
      scope: reg.scope,
      scriptURL: reg.active?.scriptURL,
      state: reg.active?.state,
      fetchHandlerCheck: hasFetchHandler,
    };
  });
  console.log('=== 3. SW info:', JSON.stringify(swInfo, null, 2));

  // 4. Check if app is already installed (standalone mode)
  const installState = await page.evaluate(() => {
    const standalone = window.matchMedia('(display-mode: standalone)').matches;
    const iosStandalone = (window.navigator as any).standalone === true;
    const referrer = document.referrer;
    return {
      standalone: standalone,
      iosStandalone: iosStandalone,
      referrer: referrer,
      displayMode: window.matchMedia('(display-mode: standalone)').matches ? 'standalone' : 'browser',
    };
  });
  console.log('=== 4. Install state:', JSON.stringify(installState));

  // 5. Check protocol (must be HTTPS)
  console.log('=== 5. Protocol:', new URL(page.url()).protocol, '| Host:', new URL(page.url()).host);

  // 6. Check if beforeinstallprompt fired
  const bipEvents = await page.evaluate(() => (window as any).__bipEvents);
  console.log('=== 6. beforeinstallprompt events:', JSON.stringify(bipEvents));

  // 7. Check vananPWA state
  const pwaState = await page.evaluate(() => {
    const pwa = (window as any).vananPWA;
    return pwa ? {
      deferredPrompt: pwa.deferredPrompt ? 'NOT NULL' : 'null',
      isInstalled: pwa.isInstalled,
      canInstallNative: pwa.canInstallNative(),
    } : 'vananPWA not found';
  });
  console.log('=== 7. vananPWA state:', JSON.stringify(pwaState));

  // 8. Check localStorage for pwa_dismissed
  const ls = await page.evaluate(() => {
    return {
      pwa_dismissed: localStorage.getItem('pwa_dismissed'),
      customer_token: localStorage.getItem('customer_token') ? 'present' : 'absent',
    };
  });
  console.log('=== 8. localStorage:', JSON.stringify(ls));

  // 9. Check all network responses for manifest
  const allResponses: string[] = [];
  page.on('response', resp => {
    if (resp.url().includes('manifest') || resp.url().includes('service-worker')) {
      allResponses.push(`${resp.url()} → ${resp.status()} ${resp.headers()['content-type'] || ''}`);
    }
  });

  // Reload to capture manifest fetch
  await page.reload({ waitUntil: 'networkidle', timeout: 60000 });
  await page.waitForTimeout(5000);
  console.log('=== 9. Manifest/SW responses on reload:', JSON.stringify(allResponses));

  // 10. Check if Chrome thinks manifest is valid via getInstalledRelatedApps
  const relatedApps = await page.evaluate(async () => {
    try {
      if ('getInstalledRelatedApps' in navigator) {
        const apps = await (navigator as any).getInstalledRelatedApps();
        return apps;
      }
      return 'getInstalledRelatedApps not supported';
    } catch (e: any) {
      return { error: e.message };
    }
  });
  console.log('=== 10. Installed related apps:', JSON.stringify(relatedApps));

  // Print ALL console logs
  console.log('=== ALL console logs:');
  consoleLogs.forEach(l => console.log('  ', l));

  await context.close();
});
