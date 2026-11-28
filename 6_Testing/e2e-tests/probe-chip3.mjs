import { chromium } from '@playwright/test';
const browser = await chromium.launch();
const page = await browser.newPage();
let searchCalls = 0;
page.on('request', r => { if (r.url().includes('/api/tenants/search')) searchCalls++; });
page.on('pageerror', e => console.log('PAGE-ERR:', String(e).slice(0,300)));
await page.goto('https://commienphi.timlathay.com/stores', { waitUntil: 'domcontentloaded' });
const input = page.locator('#search-input');
await input.waitFor({ state: 'visible', timeout: 90000 });
await input.fill('samo');
await input.press('Enter');
const chipBtn = page.locator('.alert', { hasText: 'Ý bạn là' }).locator('button').first();
await chipBtn.waitFor({ state: 'visible', timeout: 60000 });
console.log('searchCalls after Enter:', searchCalls);
// 1) click chip → expect +1 search call (LoadStores với "Samho")
await chipBtn.click();
await page.waitForTimeout(6000);
console.log('after chip click — input:', await input.inputValue(), '| searchCalls:', searchCalls, '| chipAlerts:', await page.locator('.alert', { hasText: 'Ý bạn là' }).count());
// 2) click "Tất cả cửa hàng" → events còn sống?
await page.locator('button', { hasText: 'Tất cả cửa hàng' }).click();
await page.waitForTimeout(4000);
console.log('after LoadAll — searchCalls:', searchCalls, '| input:', await input.inputValue());
await browser.close();
