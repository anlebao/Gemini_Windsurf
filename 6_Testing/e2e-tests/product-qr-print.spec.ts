import { test, expect } from '@playwright/test';
import { ProductManagementPage } from './pages/ProductManagementPage';

/**
 * Phase 6 — Product QR view + print E2E spec
 * @golden
 *
 * Coverage:
 *  1. Open QR modal → verify QR image renders (src contains /api/products/{id}/qr)
 *  2. Click "In QR code" → verify window.print JS call invoked (mock window.print + window.open)
 *  3. Checkbox 3 products → "In QR đã chọn" → verify printBatchQrCodes JS call invoked
 *
 * Auth: storageState = auth/admin.json (Owner login)
 */
test.describe('Product QR View + Print @golden', () => {
  let pmp: ProductManagementPage;
  const qrTestProduct = `QR-Test-${Date.now()}`;

  test.beforeEach(async ({ page }) => {
    pmp = new ProductManagementPage(page);
    await pmp.navigate();
  });

  test('should open QR modal and render QR image', async () => {
    // Ensure at least one product exists
    let row = pmp.rowForProduct(qrTestProduct);
    if (!(await row.isVisible().catch(() => false))) {
      // Use any existing product, or create one
      const existingRows = await pmp.dataGridRows.count();
      if (existingRows === 0) {
        await pmp.createProduct({ name: qrTestProduct, price: 30000, category: 'Thực phẩm' });
        await expect(pmp.rowForProduct(qrTestProduct)).toBeVisible({ timeout: 15000 });
      }
    }

    // Pick the first visible row
    const firstRow = pmp.dataGridRows.first();
    await expect(firstRow).toBeVisible();

    // Click QR button on first row
    await firstRow.locator('button:has-text("QR")').click();

    // Verify QR modal visible
    await expect(pmp.qrModal).toBeVisible({ timeout: 5000 });

    // Verify QR image has src containing /api/products/{id}/qr
    await expect(pmp.qrImage).toBeVisible({ timeout: 5000 });
    const src = await pmp.qrImage.getAttribute('src');
    expect(src).toBeTruthy();
    expect(src).toMatch(/\/api\/products\/[a-f0-9-]+\/qr/);
    expect(src).toContain('tenantId=');
  });

  test('should invoke window.print when clicking "In QR code"', async ({ page }) => {
    // Mock window.open to capture the print window
    // and stub window.print on the opened window
    await page.addInitScript(() => {
      const openedWindows: { printCalls: number }[] = [];
      (window as any).__openedWindows = openedWindows;
      const origOpen = window.open.bind(window);
      window.open = function(): Window | null {
        // Return a fake window object with print stubbed
        const fakeWin: any = {
          document: {
            write: () => {},
            close: () => {}
          },
          print: () => {
            openedWindows.push({ printCalls: 1 });
          },
          close: () => {}
        };
        return fakeWin;
      } as any;
    });

    // Ensure a product exists
    const existingRows = await pmp.dataGridRows.count();
    if (existingRows === 0) {
      await pmp.createProduct({ name: qrTestProduct, price: 30000, category: 'Thực phẩm' });
      await expect(pmp.rowForProduct(qrTestProduct)).toBeVisible({ timeout: 15000 });
    }

    // Open QR modal on first row
    const firstRow = pmp.dataGridRows.first();
    await firstRow.locator('button:has-text("QR")').click();
    await expect(pmp.qrModal).toBeVisible({ timeout: 5000 });

    // Click print button
    await pmp.qrPrintButton.click();

    // Verify window.open was called and print was invoked
    await page.waitForTimeout(500);
    const openedCount = await page.evaluate(() => (window as any).__openedWindows?.length ?? 0);
    expect(openedCount).toBeGreaterThanOrEqual(1);
  });

  test('should batch print selected products', async ({ page }) => {
    // Mock window.open + print for batch
    await page.addInitScript(() => {
      const openedWindows: { printCalls: number }[] = [];
      (window as any).__openedWindows = openedWindows;
      window.open = function(): Window | null {
        const fakeWin: any = {
          document: { write: () => {}, close: () => {} },
          print: () => { openedWindows.push({ printCalls: 1 }); },
          close: () => {}
        };
        return fakeWin;
      } as any;
    });

    // Ensure at least 3 products exist
    const existingRows = await pmp.dataGridRows.count();
    const needed = Math.max(0, 3 - existingRows);
    for (let i = 0; i < needed; i++) {
      await pmp.createProduct({
        name: `Batch-Print-${Date.now()}-${i}`,
        price: 25000,
        category: 'Đồ uống'
      });
      await pmp.page.waitForTimeout(500);
    }

    // Select first 3 rows via checkbox
    const rows = pmp.dataGridRows;
    const count = await rows.count();
    expect(count).toBeGreaterThanOrEqual(3);

    for (let i = 0; i < 3; i++) {
      await rows.nth(i).locator('input[type="checkbox"]').click();
    }

    // Verify batch print button enabled + shows count
    await expect(pmp.batchPrintButton).not.toBeDisabled();
    await expect(pmp.batchPrintButton).toContainText(/3/);

    // Click batch print
    await pmp.batchPrintButton.click();
    await page.waitForTimeout(500);

    // Verify window.open was called (batch print triggered)
    const openedCount = await page.evaluate(() => (window as any).__openedWindows?.length ?? 0);
    expect(openedCount).toBeGreaterThanOrEqual(1);
  });
});
