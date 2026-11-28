// Final verdict: real Chrome 155 + persistent (non-incognito) profile → getInstallabilityErrors
const { chromium } = require('playwright');
const fs = require('fs');
const os = require('os');
const path = require('path');

(async () => {
  const profileDir = fs.mkdtempSync(path.join(os.tmpdir(), 'pw-verdict-'));
  const ctx = await chromium.launchPersistentContext(profileDir, {
    channel: 'chrome',
    viewport: { width: 412, height: 915 },
    hasTouch: true,
    isMobile: true,
    userAgent: 'Mozilla/5.0 (Linux; Android 14; SM-S928B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/155.0.0.0 Mobile Safari/537.36',
  });
  const page = ctx.pages()[0] || await ctx.newPage();
  const cdp = await ctx.newCDPSession(page);
  await cdp.send('Page.enable');
  page.on('console', (m) => { if (/icon|manifest|valid image|error/i.test(m.text())) console.log('[console] ' + m.text()); });

  for (const url of ['https://timlathay.com/', 'https://diemthuong2.khachvip.online/', 'https://commienphi.timlathay.com/']) {
    try { await page.goto(url, { waitUntil: 'networkidle', timeout: 45000 }); }
    catch (e) { console.log(url, 'NAV_FAIL', e.message.slice(0, 120)); continue; }
    await page.waitForTimeout(6000);
    const verdict = await cdp.send('Page.getInstallabilityErrors').catch((e) => ({ cdpErr: e.message.slice(0, 200) }));
    const errs = (verdict.installabilityErrors || []).map((e) => e.errorId + (e.errorArguments && e.errorArguments.length ? ' :: ' + e.errorArguments.map((a) => a.value).join(',') : ''));
    console.log('=== ' + url + ' ===');
    console.log('VERDICT:', errs.length ? JSON.stringify(errs) : 'INSTALLABLE (no errors)');
  }
  await ctx.close();
  fs.rmSync(profileDir, { recursive: true, force: true });
})().catch((e) => { console.error('FAILED', e); process.exit(1); });