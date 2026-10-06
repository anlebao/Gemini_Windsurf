// Probe UI: booking customer flow — in từng bước để tìm bước fail (RV demo data)
import { chromium } from 'playwright';

const QR = process.env.BOOKING_TEST_QR_TOKEN_A || process.env.BOOKING_TEST_QR_TOKEN || '';
const KL = 'https://diemthuong2.khachvip.online';

const browser = await chromium.launch({ headless: true, args: ['--ignore-certificate-errors'] });
const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
const page = await ctx.newPage();
page.on('console', (m) => { if (m.type() === 'error') console.log('[console.error]', m.text().slice(0, 200)); });
page.on('pageerror', (e) => console.log('[pageerror]', String(e).slice(0, 200)));

const step = async (name, fn) => {
  try { const r = await fn(); console.log(`  ✓ ${name}${r ? ` — ${String(r).slice(0, 150)}` : ''}`); return r; }
  catch (e) { console.log(`  ✗ ${name} — ${String(e).slice(0, 250)}`); return null; }
};

console.log(`KL=${KL} QR=${QR.slice(0, 12)}...`);
await step('goto /booking/{qr}', () => page.goto(`${KL}/booking/${QR}`, { waitUntil: 'load', timeout: 30000 }));
await page.waitForTimeout(8000); // WASM boot

const tenantName = await step('tenant-name visible', () => page.locator('[data-testid="booking-tenant-name"]').textContent({ timeout: 20000 }));
const offeringCards = await step('offering cards count', async () => page.locator('[data-testid="booking-offering-list"] .booking-offering-card').count());
const alertText = await step('alert text (nếu có)', async () => {
  const a = page.locator('.vanan-alert').first();
  return (await a.isVisible().catch(() => false)) ? await a.textContent() : '(không có alert)';
});
const continueBtn = await step('btn-continue visible', async () => page.locator('[data-testid="booking-btn-continue"]').isVisible({ timeout: 5000 }).catch(() => false));

// Thử click offering đầu tiên + continue
if (offeringCards > 0) {
  await step('click offering card', () => page.locator('[data-testid="booking-offering-list"] .booking-offering-card').first().click({ timeout: 5000 }));
  await page.waitForTimeout(3000);
  const cont2 = await step('btn-continue sau chọn offering', async () => page.locator('[data-testid="booking-btn-continue"]').isVisible({ timeout: 5000 }).catch(() => false));
  if (cont2) {
    await step('click continue', () => page.locator('[data-testid="booking-btn-continue"]').click({ timeout: 5000 }));
    await page.waitForTimeout(5000);
    const dateBtns = await step('date picker buttons', async () => page.locator('[data-testid="booking-date-picker"] button').count());
    if (dateBtns > 0) {
      await step('click ngày đầu', () => page.locator('[data-testid="booking-date-picker"] button').first().click());
      await page.waitForTimeout(4000);
      const slots = await step('slot grid buttons', async () => page.locator('[data-testid="booking-slot-grid"] button').count());
      const slotTexts = await step('slot texts', async () => page.locator('[data-testid="booking-slot-grid"] button').allTextContents().then((t) => t.slice(0, 5)));
      await step('screenshot screen2', () => page.screenshot({ path: '.devin/rv-screen2.png' }));
    }
  }
}

await browser.close();
