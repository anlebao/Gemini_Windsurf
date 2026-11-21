// PRE-FLIGHT SWEEP — verify mọi điều kiện trước khi chạy full browser RV.
// Sweep-first: fail fast ở tầng rẻ (API/DB) trước khi tốn browser run.
import { chromium } from 'playwright';
import fs from 'fs';

const creds = JSON.parse(fs.readFileSync('../.devin/rv_presence_creds.json','utf8'));
const TENANT = 'e2d91cdf-d3c4-4399-b96e-e772a7a41630';
const APP2 = 'https://app2.khachvip.online';
const CUST = JSON.parse(fs.readFileSync('../.devin/rv_cust_token.json','utf8'));

const results = [];
const check = (n,p,d='') => { results.push(p); console.log(`[${p?'PASS':'FAIL'}] ${n}${d?' — '+String(d).slice(0,200):''}`); };

const b = await chromium.launch({headless:true});
const ctx = await b.newContext();

// S1: owner login qua Razor page → cookie auth
const p = await ctx.newPage();
await p.goto(`${APP2}/Login`, {waitUntil:'domcontentloaded', timeout:60000});
await p.fill('#username', creds.owner_user);
await p.fill('#password', creds.owner_pass);
await Promise.all([
  p.waitForNavigation({waitUntil:'domcontentloaded',timeout:30000}).catch(()=>{}),
  p.click('button[type="submit"], input[type="submit"]')
]);
await new Promise(r=>setTimeout(r,3000));
check('P1 owner login → authenticated', !p.url().includes('/Login'), p.url());

// S2: sub claim phải là GUID (voice participant id) — decode qua minted token
// MintOwnerTokenAsync không expose; thay vào đó check qua trang: evaluate localStorage? 
// Đọc claim từ JWT .VanAn.Jwt cookie (HttpOnly — không đọc được). Dùng API: gọi /api/shop/settings/features với cookie → 200 nghĩa là auth+tenant ok.
const featResp = await ctx.request.get(`${APP2}/api/shop/settings/features`);
let feat = {};
try { feat = await featResp.json(); } catch {}
check('P2 feature settings API (auth+tenant)', featResp.status()===200 && feat.voice_Call_Enabled===true, `status=${featResp.status()} voiceCall=${feat.voice_Call_Enabled}`);

// S3: voice presence toggle render trên /orders (sau fix VoicePresenceHost)
await p.goto(`${APP2}/orders`, {waitUntil:'domcontentloaded',timeout:60000});
await new Promise(r=>setTimeout(r,8000));
const tog = await p.locator('[data-testid="voice-presence-toggle"]').count();
check('P3 voice-presence toggle visible', tog>0, `count=${tog} url=${p.url()}`);

// S4: customer token còn hạn — gọi customer-identity/me
const meResp = await fetch('https://api2.khachvip.online/api/customer-identity/me', {headers:{'X-Customer-Token': CUST.customerToken}}).catch(e=>null);
if (meResp) { const j = await meResp.json().catch(()=>({})); check('P4 customer token valid', meResp.ok===true, `status=${meResp.status}`); }
else check('P4 customer token valid', false, 'fetch failed');

// S5: voice hub reachable
try {
  const vr = await fetch('https://voice.khachvip.online/health').catch(()=>null);
  check('P5 voice service reachable', vr!==null, vr?`status=${vr.status}`:'unreachable');
} catch(e){ check('P5 voice service reachable', false, String(e).slice(0,100)); }

await b.close();
const fails = results.filter(x=>!x).length;
console.log(`\n=== PRE-FLIGHT: ${results.length-fails}/${results.length} PASS ===`);
process.exit(fails>0?1:0);
