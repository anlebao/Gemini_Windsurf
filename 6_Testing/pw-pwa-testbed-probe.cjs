// Probe local PWA testbed with REAL Chrome (channel=chrome) — getInstallabilityErrors
const { chromium } = require('playwright');
const url = process.argv[2] || 'http://127.0.0.1:8844/';
(async () => {
  const browser = await chromium.launch({ channel: process.env.CHANNEL || undefined });
  console.log('ENGINE:', browser.version());
  const ctx = await browser.newContext();
  const page = await ctx.newPage();
  const cdp = await ctx.newCDPSession(page);
  await cdp.send('Page.enable');
  page.on('console', (m) => { if (/icon|manifest|error/i.test(m.text())) console.log('[console] ' + m.text()); });
  try { await page.goto(url, { waitUntil: 'networkidle', timeout: 30000 }); } catch (e) { console.log('NAV', e.message.slice(0, 120)); }
  await page.waitForTimeout(5000);
  const verdict = await cdp.send('Page.getInstallabilityErrors').catch((e) => ({ cdpErr: e.message.slice(0, 200) }));
  const errs = (verdict.installabilityErrors || []).map((e) => e.errorId + (e.errorArguments && e.errorArguments.length ? ' :: ' + e.errorArguments.map((a) => a.value).join(',') : ''));
  console.log('URL:', url);
  console.log('RESULT:', errs.length ? JSON.stringify(errs) : 'INSTALLABLE');
  await browser.close();
})().catch((e) => { console.error('FAILED', e); process.exit(1); });