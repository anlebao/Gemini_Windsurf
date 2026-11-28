import { chromium } from 'playwright';
import fs from 'fs';
const creds = JSON.parse(fs.readFileSync('../.devin/rv_presence_creds.json','utf8'));
const b = await chromium.launch({headless:true});
const ctx = await b.newContext();
ctx.on('console', m => console.log('['+m.type()+']', m.text().slice(0,180)));
ctx.on('pageerror', e => console.log('[pageerror]', String(e).slice(0,200)));
const r = await ctx.request.post('https://app2.khachvip.online/api/platform/login',{data:{Username:creds.username,Password:creds.password}});
const imp = await ctx.request.post('https://app2.khachvip.online/api/admin/impersonate/e2d91cdf-d3c4-4399-b96e-e772a7a41630');
console.log('login/imp', r.status(), imp.status());
const p = await ctx.newPage();
for (const url of ['/community/messages','/pos','/orders','/financial']) {
  await p.goto('https://app2.khachvip.online'+url,{waitUntil:'domcontentloaded',timeout:60000});
  await new Promise(r=>setTimeout(r,6000));
  const n = await p.locator('[data-testid="voice-presence-toggle"]').count();
  console.log(url, '→', p.url(), 'toggle=', n);
}
await b.close();
