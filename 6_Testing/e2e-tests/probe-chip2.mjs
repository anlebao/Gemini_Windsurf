import { chromium } from '@playwright/test';
const browser = await chromium.launch();
const page = await browser.newPage();
page.on('console', m => console.log('CON[' + m.type() + ']:', m.text().slice(0,200)));
page.on('pageerror', e => console.log('PAGE-ERR:', String(e).slice(0,300)));
await page.goto('https://commienphi.timlathay.com/stores', { waitUntil: 'domcontentloaded' });
const input = page.locator('#search-input');
await input.waitFor({ state: 'visible', timeout: 90000 });
await input.fill('samo');
await input.press('Enter');
const chipBtn = page.locator('.alert', { hasText: 'Ý bạn là' }).locator('button').first();
await chipBtn.waitFor({ state: 'visible', timeout: 60000 });
// đếm alert/button để chắc chắn đúng node
console.log('alerts:', await page.locator('.alert').count());
console.log('chip html:', (await chipBtn.innerHTML()).slice(0,120));
// thử click bằng JS dispatch trực tiếp
await chipBtn.evaluate(el => el.click());
await page.waitForTimeout(4000);
console.log('after JS click — input:', await input.inputValue(), '| alerts:', await page.locator('.alert', { hasText: 'Ý bạn là' }).count());
// nếu vẫn không đổi → thử click thật lần nữa rồi check state
await chipBtn.click({ force: true }).catch(e => console.log('click err', String(e).slice(0,150)));
await page.waitForTimeout(4000);
console.log('after force click — input:', await input.inputValue(), '| alerts:', await page.locator('.alert', { hasText: 'Ý bạn là' }).count());
await browser.close();
