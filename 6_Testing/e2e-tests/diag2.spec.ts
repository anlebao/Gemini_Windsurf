import { test, expect } from '@playwright/test';
import { createAuthenticatedPage } from './utils/prod-auth';
import { loadEnvConfig } from '../utils/env-config';

const config = loadEnvConfig();

test('diag2 — capture modal structure', async ({ browser }) => {
  const { page, context } = await createAuthenticatedPage(browser, '00000000-0000-0000-0000-000000000001');
  try {
    await page.goto(`${config.SHOPERP_URL}/products`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);

    // Click create button
    await page.locator('button:has-text("Thêm sản phẩm")').first().click();
    await page.waitForTimeout(2000);

    // Capture all modals
    const modalCount = await page.locator('.modal').count();
    const modalClasses = await page.locator('.modal').evaluateAll(els => els.map(e => e.className));
    const modalH5 = await page.locator('.modal h5, .modal h4, .modal .modal-title, .modal .vanan-modal-title').allTextContents();
    const dialogCount = await page.locator('[role="dialog"], .vanan-modal, .modal-dialog').count();
    const visibleModals = await page.locator('.modal:visible').count();

    // Get all visible elements with modal-like classes
    const vananModalCount = await page.locator('.vanan-modal').count();
    const vananModalVisible = await page.locator('.vanan-modal:visible').count();
    const vananModalTexts = await page.locator('.vanan-modal h5, .vanan-modal h4, .vanan-modal .vanan-modal-title').allTextContents();

    // Check for any visible overlay/dialog
    const overlayCount = await page.locator('[class*="modal"], [class*="dialog"], [class*="overlay"]').count();

    console.log('Modal count:', modalCount, 'classes:', JSON.stringify(modalClasses));
    console.log('Modal h5 texts:', JSON.stringify(modalH5));
    console.log('Dialog count:', dialogCount);
    console.log('Visible modals:', visibleModals);
    console.log('VananModal count:', vananModalCount, 'visible:', vananModalVisible);
    console.log('VananModal texts:', JSON.stringify(vananModalTexts));
    console.log('Overlay-like elements:', overlayCount);

    // Get the HTML of any visible modal-like element
    const html = await page.locator('[class*="modal"]:visible').first().innerHTML().catch(() => 'N/A');
    console.log('First visible modal HTML (first 500):', html.substring(0, 500));
  } finally {
    await context.close();
  }
});
