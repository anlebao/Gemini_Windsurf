import { test, expect } from '@playwright/test';
import { injectGpsMock } from './helpers/gps-mock';

const BASE_URL = 'https://diemthuong2.khachvip.online';
const TENANT_ID = '7c021960-6d6c-4de4-88a1-cf8373184f49';

test('DEBUG: chat panel — refresh button renders, mobile stacked layout, Enter-to-send', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true, viewport: { width: 375, height: 700 } });
  await injectGpsMock(context);
  const page = await context.newPage();
  const errors: string[] = [];
  page.on('pageerror', (err) => errors.push(err.message.slice(0, 200)));
  await page.goto(`${BASE_URL}/store/by-id/${TENANT_ID}`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(5000);

  await expect(page.locator('.chat-panel')).toHaveCount(1);

  // 1. Refresh button renders (was crashing → empty before the Disabled-param fix).
  await expect(page.locator('.chat-panel button .bi-arrow-clockwise')).toHaveCount(1);

  // 2. Mobile layout: input + Gửi stacked, button on its own line, no horizontal overflow.
  const layout = await page.evaluate(() => {
    const row = document.querySelector('.chat-input-row');
    const input = document.querySelector('.chat-input-row .vanan-input') as HTMLElement | null;
    const btn = document.querySelector('.chat-input-row .chat-send-btn') as HTMLElement | null;
    if (!row || !input || !btn) return null;
    const inputRect = input.getBoundingClientRect();
    const btnRect = btn.getBoundingClientRect();
    return {
      flexDirection: getComputedStyle(row).flexDirection,
      btnBelowInput: btnRect.top >= inputRect.bottom - 1,
      noHorizontalOverflow: document.documentElement.scrollWidth <= window.innerWidth,
    };
  });
  console.log(`[MOBILE LAYOUT] ${JSON.stringify(layout)}`);
  expect(layout).not.toBeNull();
  expect(layout!.flexDirection).toBe('column');
  expect(layout!.btnBelowInput).toBe(true);
  expect(layout!.noHorizontalOverflow).toBe(true);

  // 3. Enter-to-send works (OnKeyPress param — previously rendered as a literal attribute).
  const content = `RV-ENTER ${Date.now()}`;
  await page.locator('.chat-panel input.vanan-input').fill(content);
  await page.locator('.chat-panel input.vanan-input').press('Enter');
  await page.waitForTimeout(3000);
  await expect(page.locator('.chat-panel').getByText(content)).toHaveCount(1);
  console.log('PASS: Enter-to-send delivered the message');

  expect(errors.filter((e) => !e.includes('favicon') && !e.includes('ERR_CONNECTION_REFUSED'))).toEqual([]);
  console.log('PASS: no render/page errors (except OSM tile refusals)');
  await context.close();
});
