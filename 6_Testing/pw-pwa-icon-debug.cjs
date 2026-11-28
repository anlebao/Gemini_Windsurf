// Debug: full Chrome 155 console output around the manifest icon download failure
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ channel: process.env.CHANNEL || undefined });
  console.log('ENGINE:', browser.version());
  const ctx = await browser.newContext({
    viewport: { width: 1280, height: 800 },
  });
  const page = await ctx.newPage();
  const cdp = await ctx.newCDPSession(page);
  await cdp.send('Page.enable');

  page.on('console', (m) => {
    console.log('[console:' + m.type() + '] ' + m.text());
  });
  page.on('pageerror', (e) => console.log('[pageerror] ' + e.message));
  page.on('requestfailed', (r) => console.log('[reqfail] ' + r.url() + ' :: ' + (r.failure() ? r.failure().errorText : '?')));

  // Log all network responses for khachlink-icons + manifest
  page.on('response', (r) => {
    const u = r.url();
    if (/khachlink-icons|manifest/.test(u)) {
      console.log('[resp] ' + r.status() + ' ' + r.request().method() + ' ' + u + ' ct=' + (r.headers()['content-type'] || '?'));
    }
  });
  page.on('request', (r) => {
    const u = r.url();
    if (/khachlink-icons|manifest/.test(u)) {
      console.log('[req ] ' + r.method() + ' ' + u);
    }
  });

  try {
    await page.goto('https://timlathay.com/', { waitUntil: 'networkidle', timeout: 45000 });
  } catch (e) { console.log('NAV', e.message.slice(0, 200)); }
  await page.waitForTimeout(8000);
  const verdict = await cdp.send('Page.getInstallabilityErrors').catch((e) => ({ cdpErr: e.message.slice(0, 300) }));
  console.log('VERDICT:', JSON.stringify(verdict, null, 2));
  await browser.close();
})().catch((e) => { console.error('FAILED', e); process.exit(1); });