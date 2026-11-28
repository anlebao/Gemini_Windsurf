import { chromium } from '@playwright/test';
const browser = await chromium.launch();
const page = await browser.newPage();
await page.goto('https://commienphi.timlathay.com/', { waitUntil: 'domcontentloaded' });
// chờ search mode buttons render (trong VanAnCard ChildContent)
const modeBtn = page.locator('button', { hasText: 'Sản phẩm' }).first();
await modeBtn.waitFor({ state: 'visible', timeout: 90000 });
const clsBefore = await modeBtn.getAttribute('class');
await modeBtn.click();
await page.waitForTimeout(1500);
const clsAfter = await modeBtn.getAttribute('class');
console.log('mode btn class before:', clsBefore);
console.log('mode btn class after :', clsAfter);
console.log('changed:', clsBefore !== clsAfter);
await browser.close();
