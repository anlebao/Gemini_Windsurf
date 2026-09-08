import { test, expect } from '@playwright/test';

/**
 * RV — Currency auto-format on /accounting/revenue + /accounting/expenses
 * Verifies "Số Tiền (VNĐ)" field formats with vi-VN thousands separator (".")
 * as user types: "55000" → "55.000", "1000000" → "1.000.000".
 *
 * Login via /Login form (adminvanan1/2026@vanan).
 */
const SHOPERP_URL = process.env.SHOPERP_URL || 'https://app2.khachvip.online';
const ADMIN_USER = process.env.ADMIN_USER || 'adminvanan1';
const ADMIN_PASS = process.env.ADMIN_PASS || '2026@vanan';

test.describe('RV — Currency auto-format @vps', () => {
  test.beforeEach(async ({ page, context }) => {
    // Login via /Login form
    await page.goto(`${SHOPERP_URL}/Login`, { waitUntil: 'domcontentloaded', timeout: 20000 });
    await page.fill('#username', ADMIN_USER);
    await page.fill('#password', ADMIN_PASS);
    await page.click('button[type="submit"]');
    await page.waitForURL(url => !url.toString().toLowerCase().includes('/login'), { timeout: 15000 });
  });

  test('revenue: amount field auto-formats with thousands separator', async ({ page }) => {
    await page.goto(`${SHOPERP_URL}/accounting/revenue`, { waitUntil: 'domcontentloaded', timeout: 20000 });
    await page.waitForTimeout(2500); // Blazor Server render

    // Check if JS helper exists
    const hasHelper = await page.evaluate(() => typeof (window as any).vananFormatCurrencyInput === 'function');
    console.log(`[RV-Currency] vananFormatCurrencyInput exists: ${hasHelper}`);

    // Find the amount input (FieldType.Currency renders <input type="text" inputmode="numeric">)
    const amountInput = page.locator('input[inputmode="numeric"]').first();
    await expect(amountInput).toBeVisible();

    // Check the input's id
    const inputId = await amountInput.getAttribute('id');
    console.log(`[RV-Currency] amount input id: ${inputId}`);

    // Test JS function directly — bypass Blazor @bind:after
    await page.evaluate((id) => {
      const el = document.getElementById(id);
      if (el) { el.value = '55000'; (window as any).vananFormatCurrencyInput(id); }
    }, inputId);
    await page.waitForTimeout(300);
    const directFormatted = await amountInput.inputValue();
    console.log(`[RV-Currency] direct JS call: "55000" → "${directFormatted}"`);

    // Type a raw number — should auto-format with vi-VN thousands separator
    await amountInput.fill('');
    await amountInput.pressSequentially('55000', { delay: 100 });
    await page.waitForTimeout(1000); // allow @bind:after + JS interop round-trip

    const formatted = await amountInput.inputValue();
    console.log(`[RV-Currency] revenue: typed "55000" → got "${formatted}"`);
    expect(formatted).toBe('55.000');

    // Type a larger number
    await amountInput.fill('');
    await amountInput.pressSequentially('1000000', { delay: 100 });
    await page.waitForTimeout(1000);
    const formatted2 = await amountInput.inputValue();
    console.log(`[RV-Currency] revenue: typed "1000000" → got "${formatted2}"`);
    expect(formatted2).toBe('1.000.000');
  });

  test('expenses: amount field auto-formats with thousands separator', async ({ page }) => {
    await page.goto(`${SHOPERP_URL}/accounting/expenses`, { waitUntil: 'domcontentloaded', timeout: 20000 });
    await page.waitForTimeout(2500); // Blazor Server render

    const amountInput = page.locator('input[inputmode="numeric"]').first();
    await expect(amountInput).toBeVisible();

    await amountInput.fill('');
    await amountInput.pressSequentially('75000', { delay: 50 });
    await page.waitForTimeout(500);

    const formatted = await amountInput.inputValue();
    console.log(`[RV-Currency] expenses: typed "75000" → got "${formatted}"`);
    expect(formatted).toBe('75.000');
  });
});
