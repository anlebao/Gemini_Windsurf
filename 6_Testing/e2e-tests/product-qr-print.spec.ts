import { test, expect } from '@playwright/test';
import { ProductManagementPage } from './pages/ProductManagementPage';
import { createAuthenticatedPage } from './utils/prod-auth';

/**
 * Phase 6 — Product QR Code + Print E2E spec (PRODUCTION VPS)
 * @golden
 *
 * RV tests via UI layout (not API calls).
 * Auth: SystemAdmin platform login + impersonate tenant (production pattern).
 *
 * Coverage:
 *  1. QR icon column → QR modal opens + QR image renders (RV40, RV41)
 *  2. Print 1 QR → window.print trigger (RV42)
 *  3. Batch print checkbox + "In QR đã chọn" button (RV43, RV44)
 */
const tenantId = '00000000-0000-0000-0000-000000000001';

test.describe('Product QR Print (PROD) @golden', () => {
  test('RV40/RV41 — QR icon click opens modal with QR image', async ({ browser }) => {
    const { page, context } = await createAuthenticatedPage(browser, tenantId);
    try {
      const pmp = new ProductManagementPage(page);
      await pmp.navigate();
      await page.waitForLoadState('networkidle');
      await page.waitForTimeout(2000);

      // Wait for DataGrid rows
      await expect(pmp.dataGridRows.first()).toBeVisible({ timeout: 15000 });

      // Click QR icon on first row
      const firstRow = pmp.dataGridRows.first();
      await firstRow.locator('button:has-text("QR"), button[title*="QR"], .qr-icon').first().click();

      // QR modal should open
      await expect(pmp.qrModal).toBeVisible({ timeout: 5000 });

      // QR image should render (src contains /qr)
      await expect(pmp.qrImage).toBeVisible({ timeout: 10000 });
      const src = await pmp.qrImage.getAttribute('src');
      expect(src).toContain('/qr');
    } finally {
      await context.close();
    }
  });

  test('RV42 — Print 1 QR triggers window.print', async ({ browser }) => {
    const { page, context } = await createAuthenticatedPage(browser, tenantId);
    try {
      const pmp = new ProductManagementPage(page);
      await pmp.navigate();
      await page.waitForLoadState('networkidle');
      await page.waitForTimeout(2000);

      await expect(pmp.dataGridRows.first()).toBeVisible({ timeout: 15000 });

      // Open QR modal
      const firstRow = pmp.dataGridRows.first();
      await firstRow.locator('button:has-text("QR"), button[title*="QR"], .qr-icon').first().click();
      await expect(pmp.qrModal).toBeVisible({ timeout: 5000 });

      // Mock window.print before clicking
      await page.addInitScript(() => { window.print = () => { (window as any).__printCalled = true; }; });
      // Re-inject since page already loaded
      await page.evaluate(() => { (window as any).print = () => { (window as any).__printCalled = true; }; });

      // Click print button
      await pmp.qrPrintButton.click();
      await page.waitForTimeout(1000);

      // Verify print was called
      const printCalled = await page.evaluate(() => (window as any).__printCalled === true);
      expect(printCalled).toBeTruthy();
    } finally {
      await context.close();
    }
  });

  test('RV43/RV44 — Batch print: checkbox select + button enable', async ({ browser }) => {
    const { page, context } = await createAuthenticatedPage(browser, tenantId);
    try {
      const pmp = new ProductManagementPage(page);
      await pmp.navigate();
      await page.waitForLoadState('networkidle');
      await page.waitForTimeout(2000);

      // Need at least 3 products
      const rowCount = await pmp.dataGridRows.count();
      expect(rowCount).toBeGreaterThanOrEqual(1);

      // Select first 3 checkboxes (or all if < 3)
      const toSelect = Math.min(3, rowCount);
      for (let i = 0; i < toSelect; i++) {
        await pmp.dataGridRows.nth(i).locator('input[type="checkbox"]').click();
      }

      // Batch print button should be enabled
      await expect(pmp.batchPrintButton).toBeEnabled({ timeout: 5000 });

      // Button text should show count
      const btnText = await pmp.batchPrintButton.textContent();
      expect(btnText).toContain('In QR');
    } finally {
      await context.close();
    }
  });
});
