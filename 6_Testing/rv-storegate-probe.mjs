import { chromium } from 'playwright';
const urls = [
  'https://hoptacxa.timlathay.com/store/by-id/0934d7c1-ed0f-4078-9e8b-3e8b64a4fa18',
  'https://vanan.timlathay.com/store/by-id/00000000-0000-0000-0000-000000000001',
];
const browser = await chromium.launch({ args: ['--host-resolver-rules=MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, MAP api.khachvip.online 136.85.94.119, MAP khachvip.online 136.85.94.119, MAP www2.khachvip.online 136.85.94.119'] });
for (const url of urls) {
  const page = await browser.newPage({ ignoreHTTPSErrors: true });
  const errors = [];
  page.on('pageerror', e => errors.push(String(e)));
  await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 60000 });
  // chờ WASM render xong (tối đa 40s)
  try {
    await page.waitForSelector('.store-page, .store-not-found, .store-loading', { timeout: 40000 });
    await page.waitForFunction(() => !document.querySelector('.store-loading'), { timeout: 40000 });
  } catch {}
  const suspended = await page.getByText('tạm ngưng phục vụ').count();
  const storePage = await page.locator('.store-page').count();
  const notFound = await page.locator('.store-not-found').count();
  const title = await page.title();
  console.log(JSON.stringify({ url, title, suspended, storePage, notFound, errors: errors.slice(0,3) }));
  await page.close();
}
await browser.close();
