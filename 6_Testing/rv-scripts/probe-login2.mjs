import { chromium } from 'playwright';
const BASE = 'https://vanan.timlathay.com';
const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext();
const page = await ctx.newPage();
await page.goto(`${BASE}/login`, { waitUntil: 'domcontentloaded' });
await page.locator('h3', { hasText: 'Đăng nhập' }).waitFor({ timeout: 40000 });
await page.waitForTimeout(5000);
const info = await page.evaluate(() => ({
  hasWebAuthn: typeof window.vananWebAuthn,
  supported: window.vanonWebAuthn ? window.vanonWebAuthn.isSupported() : (window.vananWebAuthn ? window.vananWebAuthn.isSupported() : null),
  buttons: [...document.querySelectorAll('button')].map(b => (b.textContent||'').trim().slice(0,50)),
  testids: [...document.querySelectorAll('[data-testid]')].map(e => e.getAttribute('data-testid')),
}));
console.log(JSON.stringify(info, null, 1));
await browser.close();
