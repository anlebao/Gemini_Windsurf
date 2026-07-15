import { test, expect } from '@playwright/test';
import { createAuthenticatedPage } from './utils/prod-auth';
import { loadEnvConfig } from '../utils/env-config';

const config = loadEnvConfig();

test('diag3 — check Blazor circuit + button click', async ({ browser }) => {
  const { page, context } = await createAuthenticatedPage(browser, '00000000-0000-0000-0000-000000000001');
  try {
    // Listen for console errors
    const consoleErrors: string[] = [];
    page.on('console', msg => {
      if (msg.type() === 'error') consoleErrors.push(msg.text());
    });

    await page.goto(`${config.SHOPERP_URL}/products`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);

    // Check Blazor errors
    const blazorError = await page.locator('#blazor-error-ui').isVisible().catch(() => false);
    const blazorErrorText = await page.locator('#blazor-error-ui').textContent().catch(() => '');

    // Check if Blazor circuit is connected (look for SignalR connection indicators)
    const hasBlazorScript = await page.locator('script[src*="blazor"]').count();
    const hasCircuit = await page.evaluate(() => {
      return typeof (window as any).Blazor !== 'undefined';
    }).catch(() => false);

    // Check for any error text on page
    const errorTexts = await page.locator('.text-danger, .alert-danger, [class*="error"]').allTextContents();

    // Click create button and check what happens
    const createBtn = page.locator('button:has-text("Thêm sản phẩm")').first();
    const btnText = await createBtn.textContent();
    const btnDisabled = await createBtn.isDisabled();
    const btnVisible = await createBtn.isVisible();

    console.log('Blazor error UI visible:', blazorError);
    console.log('Blazor error text:', blazorErrorText?.substring(0, 200));
    console.log('Blazor script count:', hasBlazorScript);
    console.log('Has Blazor global:', hasCircuit);
    console.log('Console errors:', JSON.stringify(consoleErrors.slice(0, 5)));
    console.log('Error texts on page:', JSON.stringify(errorTexts.slice(0, 5)));
    console.log('Create button text:', btnText, 'disabled:', btnDisabled, 'visible:', btnVisible);

    // Try clicking and wait
    await createBtn.click();
    await page.waitForTimeout(3000);

    // Check what appeared after click
    const allVisibleTexts = await page.locator('body *:visible').evaluateAll(els =>
      els.filter(e => e.children.length === 0).map(e => e.textContent?.trim()).filter(t => t && t.length > 0).slice(0, 20)
    );
    console.log('Visible leaf texts after click:', JSON.stringify(allVisibleTexts));

    // Check for any new elements that appeared
    const newModals = await page.locator('[class*="modal"], [class*="dialog"], [role="dialog"]').count();
    const newOverlays = await page.locator('[class*="overlay"], [class*="backdrop"]').count();
    console.log('Modal-like elements after click:', newModals, 'overlays:', newOverlays);

    // Check page HTML for form elements
    const formCount = await page.locator('form').count();
    const inputCount = await page.locator('input').count();
    console.log('Forms:', formCount, 'Inputs:', inputCount);

  } finally {
    await context.close();
  }
});
