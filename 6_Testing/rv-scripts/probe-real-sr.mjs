// Probe real SpeechRecognition path trên timlathay (không fake inject)
import { chromium } from 'playwright';
const browser = await chromium.launch({
    headless: true,
    args: [
        '--use-fake-device-for-media-stream',
        '--use-fake-ui-for-media-stream',
        '--autoplay-policy=no-user-gesture-required'
    ]
});
const ctx = await browser.newContext({ bypassCSP: true, ignoreHTTPSErrors: true });
const page = await ctx.newPage();
page.on('pageerror', e => console.log('PAGEERROR:', String(e).slice(0,200)));
page.on('console', m => { if (m.type() === 'error' || m.type() === 'warning') console.log('CONSOLE', m.type(), m.text().slice(0,200)); });
await page.goto('https://timlathay.com/stores', { waitUntil: 'domcontentloaded' });
await page.waitForTimeout(5000);
const srInfo = await page.evaluate(() => {
    const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    return { exists: !!SR, voiceInput: !!window.vananVoiceInput,
             supported: window.vananVoiceInput ? window.vananVoiceInput.isSupported() : 'N/A' };
});
console.log('SR:', JSON.stringify(srInfo));
// instrument: gắn probe vào instance tiếp theo
await page.evaluate(() => {
    const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SR) return;
    const orig = SR.prototype.start;
    window.__srEvents = [];
    const events = ['audiostart','soundstart','speechstart','speechend','audioend','start','end','result','error','nomatch'];
    const wrap = (inst) => events.forEach(ev => {
        const h = inst['on'+ev];
        inst['on'+ev] = function(e) {
            window.__srEvents.push(ev + (e.error?':'+e.error:''));
            h && h.call(inst, e);
        };
    });
    SR.prototype.start = function() { wrap(this); return orig.call(this); };
});
const mic = page.locator('button[aria-label*="giọng nói"], button[aria-label*="Dừng nói"]').first();
await mic.waitFor({ timeout: 20000 }).catch(()=>{});
console.log('mic count:', await mic.count());
if (await mic.count() > 0) {
    await mic.click();
    // poll events + listening state + error text 30s
    for (let i = 0; i < 30; i++) {
        await page.waitForTimeout(1000);
        const st = await page.evaluate(() => ({
            evts: window.__srEvents || [],
            active: window.vananVoiceInput ? window.vananVoiceInput.isActive() : 'N/A',
            input: (document.querySelector('.vanan-search-pill input, input')||{}).value || '',
            body: (document.body.innerText||'').match(/(Không nghe|Lỗi|Quyền|micro|Đang nghe)[^\n]*/g) || []
        }));
        if (i === 0 || i === 4 || i === 9 || i === 14 || st.evts.length > 0 && i % 3 === 0)
            console.log(`t=${i+1}s`, JSON.stringify(st).slice(0, 300));
        if (st.evts.includes('error') || st.evts.includes('end')) { console.log('END at t='+(i+1)+'s', JSON.stringify(st).slice(0,300)); break; }
    }
}
await browser.close();
