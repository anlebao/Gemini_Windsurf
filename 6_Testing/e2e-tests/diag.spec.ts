import { test, expect } from '@playwright/test';
import { createAuthenticatedPage } from './utils/prod-auth';
import { loadEnvConfig } from '../utils/env-config';

const config = loadEnvConfig();

test('diag — capture page content', async ({ browser }) => {
  const { page, context } = await createAuthenticatedPage(browser, '00000000-0000-0000-0000-000000000001');
  try {
    await page.goto(`${config.SHOPERP_URL}/products`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);

    const url = page.url();
    const title = await page.title();
    const bodyText = await page.locator('body').innerText();
    const h1Count = await page.locator('h1').count();
    const h1Texts = await page.locator('h1').allTextContents();
    const buttonCount = await page.locator('button').count();
    const buttonTexts = await page.locator('button').allTextContents();

    console.log('URL:', url);
    console.log('Title:', title);
    console.log('H1 count:', h1Count, 'texts:', JSON.stringify(h1Texts));
    console.log('Button count:', buttonCount, 'texts:', JSON.stringify(buttonTexts));
    console.log('Body text (first 500):', bodyText.substring(0, 500));
  } finally {
    await context.close();
  }
});
