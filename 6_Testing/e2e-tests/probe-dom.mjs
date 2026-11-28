import { chromium } from '@playwright/test';
const browser = await chromium.launch();
const page = await browser.newPage();
await page.goto('https://commienphi.timlathay.com/stores', { waitUntil: 'domcontentloaded' });
const input = page.locator('#search-input');
await input.waitFor({ state: 'visible', timeout: 90000 });
await input.fill('samo');
await input.press('Enter');
await page.locator('.alert', { hasText: 'Ý bạn là' }).waitFor({ state: 'visible', timeout: 60000 });
const html = await page.locator('.alert', { hasText: 'Ý bạn là' }).first().evaluate(el => el.outerHTML);
console.log('ALERT HTML:', html);
// check blazor internals on button
const info = await page.locator('.alert button').first().evaluate(el => ({
  outer: el.outerHTML.slice(0, 300),
  disabled: el.disabled,
  pe: getComputedStyle(el).pointerEvents,
}));
console.log('BTN INFO:', JSON.stringify(info));
await browser.close();
