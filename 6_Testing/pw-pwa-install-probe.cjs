// PWA installability probe — real Chromium, CDP Page.getInstallabilityErrors
// THE definitive check: Chrome's own installability verdict per origin (2026-10-24)
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ channel: process.env.CHANNEL || undefined });
  console.log('ENGINE:', browser.version());
  const ctx = await browser.newContext({
    viewport: { width: 412, height: 915 },
    hasTouch: true,
    isMobile: true,
    userAgent: 'Mozilla/5.0 (Linux; Android 14; SM-S928B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/155.0.0.0 Mobile Safari/537.36',
  });
  const page = await ctx.newPage();
  const cdp = await ctx.newCDPSession(page);
  await cdp.send('Page.enable');

  const events = [];
  page.on('console', (m) => { const t = m.text(); if (/pwa|manifest|install|error|fail/i.test(t)) events.push('[console] ' + t.slice(0, 160)); });
  page.on('requestfailed', (r) => events.push('[reqfail] ' + r.url().slice(0, 140) + ' :: ' + (r.failure() && r.failure().errorText)));

  for (const url of ['https://timlathay.com/', 'https://diemthuong2.khachvip.online/', 'https://commienphi.timlathay.com/']) {
    try {
      await page.goto(url, { waitUntil: 'networkidle', timeout: 45000 });
    } catch (e) { console.log(url, 'NAV_FAIL', e.message.slice(0, 120)); continue; }
    await page.waitForTimeout(6000);
    const verdict = await cdp.send('Page.getInstallabilityErrors').catch((e) => ({ cdpErr: e.message.slice(0, 200) }));
    const state = await page.evaluate(async () => {
      const out = { displayMode: window.matchMedia('(display-mode: standalone)').matches ? 'standalone' : 'browser' };
      try { out.related = (await navigator.getInstalledRelatedApps()).length; } catch (e) { out.related = -1; }
      try { out.swCtrl = !!(navigator.serviceWorker && navigator.serviceWorker.controller); } catch (e) {}
      const links = Array.from(document.querySelectorAll('link[rel="manifest"]')).map((l) => l.href);
      out.manifestLink = links[0] || null;
      return out;
    });
    const errs = (verdict.installabilityErrors || []).map((e) => e.errorId + (e.errorArguments && e.errorArguments.length ? ' :: ' + e.errorArguments.map((a) => a.value).join(', ') : ''));
    console.log('=== ' + url + ' ===');
    console.log('installabilityErrors:', errs.length ? JSON.stringify(errs, null, 2) : '[] (INSTALLABLE)');
    console.log('state:', JSON.stringify(state));
    console.log('events:', JSON.stringify(events.slice(-8)));
    events.length = 0;
  }
  await browser.close();
})().catch((e) => { console.error('PROBE_FAILED', e); process.exit(1); });