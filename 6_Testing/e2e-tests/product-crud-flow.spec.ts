import { test, expect } from '@playwright/test';
import { ProductManagementPage } from './pages/ProductManagementPage';
import { createAuthenticatedPage } from './utils/prod-auth';

/**
 * Phase 6 — Product CRUD flow E2E spec (PRODUCTION VPS)
 * @golden
 *
 * RV tests via UI layout (not API calls).
 * Auth: SystemAdmin platform login + impersonate tenant (production pattern).
 *
 * Coverage:
 *  1. Page load + DataGrid renders (RV26)
 *  2. Create product → verify appears in list (RV31)
 *  3. Edit product → verify update (RV32)
 *  4. Deactivate → verify status badge (RV34)
 *  5. Reactivate → verify status (RV34)
 *  6. Delete → verify product disappears (RV33)
 */
const tenantId = '00000000-0000-0000-0000-000000000001';
const testProductName = `E2E-Test-Product-${Date.now()}`;
const editedName = `${testProductName}-edited`;

test.describe('Product CRUD Flow (PROD) @golden', () => {
  test('RV26 — should load /products and render DataGrid', async ({ browser }) => {
    const { page, context } = await createAuthenticatedPage(browser, tenantId);
    try {
      const pmp = new ProductManagementPage(page);
      await pmp.navigate();

      // Wait for Blazor to render — either DataGrid or empty state
      await page.waitForLoadState('networkidle');
      await page.waitForTimeout(2000); // Blazor SSR hydration

      // Page header visible (RV26)
      await expect(page.locator('h1:has-text("Quản lý sản phẩm")')).toBeVisible({ timeout: 15000 });

      // Either DataGrid rows OR empty state OR loading spinner
      const hasHeader = await page.locator('table.vanan-data-grid thead').count();
      const hasEmpty = await page.locator('.empty-state').count();
      const hasLoading = await page.locator('.loading-state').count();
      expect(hasHeader + hasEmpty + hasLoading).toBeGreaterThan(0);
    } finally {
      await context.close();
    }
  });

  test('RV31 — should create a new product and verify it appears in list', async ({ browser }) => {
    const { page, context } = await createAuthenticatedPage(browser, tenantId);
    try {
      const pmp = new ProductManagementPage(page);
      await pmp.navigate();
      await page.waitForLoadState('networkidle');
      await page.waitForTimeout(2000);

      await pmp.createProduct({
        name: testProductName,
        description: 'E2E test product description',
        price: 55000,
        costPrice: 35000,
        category: 'Đồ uống',
        vatRate: 0.10
      });

      // Wait for list refresh — product should appear
      await expect(pmp.rowForProduct(testProductName)).toBeVisible({ timeout: 15000 });

      // Verify price formatted as VNĐ (RV29)
      const row = pmp.rowForProduct(testProductName);
      await expect(row).toContainText(/55\.000/);
    } finally {
      await context.close();
    }
  });

  test('RV32 — should edit an existing product', async ({ browser }) => {
    const { page, context } = await createAuthenticatedPage(browser, tenantId);
    try {
      const pmp = new ProductManagementPage(page);
      await pmp.navigate();
      await page.waitForLoadState('networkidle');
      await page.waitForTimeout(2000);

      // Ensure product exists (create if not)
      let row = pmp.rowForProduct(testProductName);
      if (!(await row.isVisible().catch(() => false))) {
        await pmp.createProduct({
          name: testProductName,
          price: 55000,
          category: 'Đồ uống'
        });
        await expect(pmp.rowForProduct(testProductName)).toBeVisible({ timeout: 15000 });
      }

      await pmp.clickEditOn(testProductName);
      await pmp.editNameInput.fill(editedName);
      await pmp.editPriceInput.fill('66000');
      await pmp.submitEdit();

      // Verify edited name appears
      await expect(pmp.rowForProduct(editedName)).toBeVisible({ timeout: 15000 });
    } finally {
      await context.close();
    }
  });

  test('RV34 — should deactivate a product and verify status badge', async ({ browser }) => {
    const { page, context } = await createAuthenticatedPage(browser, tenantId);
    try {
      const pmp = new ProductManagementPage(page);
      await pmp.navigate();
      await page.waitForLoadState('networkidle');
      await page.waitForTimeout(2000);

      // Use the edited product from previous test (or create fresh)
      let row = pmp.rowForProduct(editedName);
      let targetName = editedName;
      if (!(await row.isVisible().catch(() => false))) {
        row = pmp.rowForProduct(testProductName);
        targetName = testProductName;
        if (!(await row.isVisible().catch(() => false))) {
          await pmp.createProduct({ name: testProductName, price: 55000, category: 'Đồ uống' });
          await expect(pmp.rowForProduct(testProductName)).toBeVisible({ timeout: 15000 });
          targetName = testProductName;
        }
      }

      // Verify initial status is "Đang bán"
      const initialStatus = await pmp.getStatusBadge(targetName);
      expect(initialStatus).toContain('Đang bán');

      // Deactivate
      await pmp.clickDeactivateOn(targetName);
      await page.waitForTimeout(2000);

      // Verify status changed to "Tạm ngưng"
      const newStatus = await pmp.getStatusBadge(targetName);
      expect(newStatus).toContain('Tạm ngưng');
    } finally {
      await context.close();
    }
  });

  test('RV34 — should reactivate a deactivated product', async ({ browser }) => {
    const { page, context } = await createAuthenticatedPage(browser, tenantId);
    try {
      const pmp = new ProductManagementPage(page);
      await pmp.navigate();
      await page.waitForLoadState('networkidle');
      await page.waitForTimeout(2000);

      // Find a product (edited or test)
      let targetName = editedName;
      let row = pmp.rowForProduct(editedName);
      if (!(await row.isVisible().catch(() => false))) {
        targetName = testProductName;
        row = pmp.rowForProduct(testProductName);
        if (!(await row.isVisible().catch(() => false))) {
          await pmp.createProduct({ name: targetName, price: 55000, category: 'Đồ uống' });
          await expect(pmp.rowForProduct(targetName)).toBeVisible({ timeout: 15000 });
        }
      }

      // If currently active, deactivate first
      let status = await pmp.getStatusBadge(targetName);
      if (status.includes('Đang bán')) {
        await pmp.clickDeactivateOn(targetName);
        await page.waitForTimeout(2000);
      }

      // Now activate
      await pmp.clickActivateOn(targetName);
      await page.waitForTimeout(2000);

      const finalStatus = await pmp.getStatusBadge(targetName);
      expect(finalStatus).toContain('Đang bán');
    } finally {
      await context.close();
    }
  });

  test('RV33 — should delete a product and verify it disappears from list', async ({ browser }) => {
    const { page, context } = await createAuthenticatedPage(browser, tenantId);
    try {
      const pmp = new ProductManagementPage(page);
      await pmp.navigate();
      await page.waitForLoadState('networkidle');
      await page.waitForTimeout(2000);

      // Use the edited product (or test product)
      let targetName = editedName;
      let row = pmp.rowForProduct(editedName);
      if (!(await row.isVisible().catch(() => false))) {
        targetName = testProductName;
        row = pmp.rowForProduct(testProductName);
        if (!(await row.isVisible().catch(() => false))) {
          await pmp.createProduct({ name: targetName, price: 55000, category: 'Đồ uống' });
          await expect(pmp.rowForProduct(targetName)).toBeVisible({ timeout: 15000 });
        }
      }

      await pmp.clickDeleteOn(targetName);
      await pmp.confirmDelete();

      // Verify product no longer appears
      await expect(pmp.rowForProduct(targetName)).not.toBeVisible({ timeout: 15000 });
    } finally {
      await context.close();
    }
  });
});
