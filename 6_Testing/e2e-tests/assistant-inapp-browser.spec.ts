import { test, expect } from '@playwright/test';

test('Zalo UA → in-app banner hiện', async ({ browser }) => {
  const ctx = await browser.newContext({
    userAgent: 'Mozilla/5.0 (Linux; Android 13; SM-G991B) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/120.0 Mobile Safari/537.36 Zalo/24.01.01',
    viewport: { width: 390, height: 844 },
  });
  const page = await ctx.newPage();
  await page.goto('https://shopai.timlathay.com/a/rvAntamAi01x', { waitUntil: 'domcontentloaded' });
  await expect(page.locator('.vanan-ai-widget')).toBeVisible({ timeout: 30000 });
  const banner = page.locator('.assistant-inapp-banner');
  await expect(banner).toBeVisible({ timeout: 15000 });
  await expect(banner).toContainText('Zalo');
  await expect(page.locator('.assistant-inapp-open')).toBeVisible();
  await ctx.close();
});

test('Chrome UA → không hiện banner', async ({ page }) => {
  await page.goto('https://shopai.timlathay.com/a/rvAntamAi01x', { waitUntil: 'domcontentloaded' });
  await expect(page.locator('.vanan-ai-widget')).toBeVisible({ timeout: 30000 });
  await page.waitForTimeout(2000);
  await expect(page.locator('.assistant-inapp-banner')).toHaveCount(0);
});
