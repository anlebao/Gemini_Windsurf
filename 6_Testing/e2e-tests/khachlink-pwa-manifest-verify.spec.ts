import { test, expect } from '@playwright/test';

const BASE_URL = 'https://diemthuong.khachvip.online';

test('VERIFY: PWA manifest validity + icon dimensions match', async ({ browser }) => {
  const context = await browser.newContext({
    baseURL: BASE_URL,
    ignoreHTTPSErrors: true,
    userAgent: 'Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36',
    viewport: { width: 412, height: 915 },
    isMobile: true,
    hasTouch: true,
  });

  const page = await context.newPage();
  await page.goto(BASE_URL, { waitUntil: 'networkidle', timeout: 60000 });
  await page.waitForTimeout(5000);

  // 1. Check manifest is fetchable and parseable
  const manifestCheck = await page.evaluate(async () => {
    try {
      const resp = await fetch('/manifest.json');
      const ct = resp.headers.get('content-type');
      const manifest = await resp.json();
      return {
        status: resp.status,
        contentType: ct,
        name: manifest.name,
        shortName: manifest.short_name,
        display: manifest.display,
        startUrl: manifest.start_url,
        iconCount: manifest.icons?.length,
        icons: manifest.icons?.map(i => ({ src: i.src, sizes: i.sizes, type: i.type, purpose: i.purpose })),
      };
    } catch (e) {
      return { error: e.message };
    }
  });
  console.log('=== Manifest:', JSON.stringify(manifestCheck, null, 2));

  // 2. Check each icon's actual dimensions vs declared sizes
  const iconChecks = await page.evaluate(async () => {
    const manifest = await (await fetch('/manifest.json')).json();
    const results = [];
    for (const icon of manifest.icons) {
      try {
        const resp = await fetch('/' + icon.src);
        const blob = await resp.blob();
        const url = URL.createObjectURL(blob);
        const img = new Image();
        await new Promise((resolve, reject) => {
          img.onload = resolve;
          img.onerror = reject;
          img.src = url;
        });
        const actual = `${img.width}x${img.height}`;
        const declared = icon.sizes;
        const match = actual === declared || (declared === 'any' && img.width > 0);
        results.push({
          src: icon.src,
          declared: declared,
          actual: actual,
          match: match,
          bytes: blob.size,
        });
        URL.revokeObjectURL(url);
      } catch (e) {
        results.push({ src: icon.src, error: e.message });
      }
    }
    return results;
  });

  console.log('=== Icon dimension checks:');
  for (const ic of iconChecks) {
    console.log(`  ${ic.src}: declared=${ic.declared} actual=${ic.actual} match=${ic.match} bytes=${ic.bytes}`);
  }

  // 3. Verify all icons match
  const allMatch = iconChecks.every(ic => ic.match === true);
  expect(allMatch, 'All manifest icon sizes must match actual image dimensions').toBe(true);

  // 4. Verify required icons exist (192x192 + 512x512 with maskable)
  const has192 = iconChecks.some(ic => ic.actual === '192x192');
  const has512 = iconChecks.some(ic => ic.actual === '512x512');
  expect(has192, 'Must have 192x192 icon').toBe(true);
  expect(has512, 'Must have 512x512 icon').toBe(true);

  // 5. Check manifest Content-Type is JSON (application/json implies UTF-8 per RFC 7159)
  expect(manifestCheck.contentType, `Content-Type should be JSON, got: ${manifestCheck.contentType}`).toContain('json');

  // 6. Check SW is active
  const swInfo = await page.evaluate(async () => {
    const reg = await navigator.serviceWorker.getRegistration();
    return reg ? { state: reg.active?.state, scriptURL: reg.active?.scriptURL } : null;
  });
  expect(swInfo?.state, 'SW should be activated').toBe('activated');

  // 7. Check manifest is linked in HTML
  const manifestHref = await page.locator('link[rel="manifest"]').getAttribute('href');
  expect(manifestHref, 'Manifest link should exist in HTML').toBeTruthy();

  await context.close();
});
