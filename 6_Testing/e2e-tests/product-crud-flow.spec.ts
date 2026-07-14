import { test, expect } from '@playwright/test';
import { ProductManagementPage } from './pages/ProductManagementPage';

/**
 * Phase 6 — Product CRUD flow E2E spec
 * @golden
 *
 * Coverage:
 *  1. Page load + DataGrid renders
 *  2. Create product → verify appears in list
 *  3. Edit product → verify update
 *  4. Deactivate → verify status badge
 *  5. Reactivate → verify status
 *  6. Delete → verify product disappears
 *
 * Multi-tenant isolation is verified via API smoke (separate test).
 *
 * Auth: storageState = auth/admin.json (Owner login) — applied via playwright.config.ts
 */
test.describe('Product CRUD Flow @golden', () => {
  let pmp: ProductManagementPage;
  const testProductName = `E2E-Test-Product-${Date.now()}`;
  const editedName = `${testProductName}-edited`;

  test.beforeEach(async ({ page }) => {
    pmp = new ProductManagementPage(page);
    await pmp.navigate();
  });

  test('should load /products and render DataGrid', async () => {
    // Page header visible
    await expect(pmp.page.locator('h1:has-text("Quản lý sản phẩm")')).toBeVisible();
    // Either DataGrid rows OR empty state visible (depends on test data)
    const hasRows = await pmp.dataGridRows.count();
    if (hasRows === 0) {
      await expect(pmp.emptyState).toBeVisible();
    } else {
      // DataGrid header row exists
      await expect(pmp.page.locator('table.vanan-data-grid thead')).toBeVisible();
    }
  });

  test('should create a new product and verify it appears in list', async () => {
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

    // Verify price formatted as VNĐ (55.000 ₫)
    const row = pmp.rowForProduct(testProductName);
    await expect(row).toContainText(/55\.000/);
  });

  test('should edit an existing product', async () => {
    // First ensure product exists (create if not)
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
  });

  test('should deactivate a product and verify status badge', async () => {
    // Use the edited product from previous test (or create fresh)
    let row = pmp.rowForProduct(editedName);
    if (!(await row.isVisible().catch(() => false))) {
      row = pmp.rowForProduct(testProductName);
      if (!(await row.isVisible().catch(() => false))) {
        await pmp.createProduct({ name: testProductName, price: 55000, category: 'Đồ uống' });
        await expect(pmp.rowForProduct(testProductName)).toBeVisible({ timeout: 15000 });
        row = pmp.rowForProduct(testProductName);
      }
    }
    const targetName = (await row.isVisible()) ? editedName : testProductName;

    // Verify initial status is "Đang bán"
    const initialStatus = await pmp.getStatusBadge(targetName);
    expect(initialStatus).toContain('Đang bán');

    // Deactivate
    await pmp.clickDeactivateOn(targetName);
    // Wait for list refresh
    await pmp.page.waitForTimeout(1000);

    // Verify status changed to "Tạm ngưng"
    const newStatus = await pmp.getStatusBadge(targetName);
    expect(newStatus).toContain('Tạm ngưng');
  });

  test('should reactivate a deactivated product', async () => {
    // Find a deactivated product (from previous test) — or deactivate one first
    let row = pmp.rowForProduct(editedName);
    if (!(await row.isVisible().catch(() => false))) {
      row = pmp.rowForProduct(testProductName);
    }
    const targetName = (await row.isVisible()) ? editedName : testProductName;
    if (!(await pmp.rowForProduct(targetName).isVisible().catch(() => false))) {
      // Create fresh
      await pmp.createProduct({ name: targetName, price: 55000, category: 'Đồ uống' });
      await expect(pmp.rowForProduct(targetName)).toBeVisible({ timeout: 15000 });
    }

    // If currently active, deactivate first
    let status = await pmp.getStatusBadge(targetName);
    if (status.includes('Đang bán')) {
      await pmp.clickDeactivateOn(targetName);
      await pmp.page.waitForTimeout(1000);
    }

    // Now activate
    await pmp.clickActivateOn(targetName);
    await pmp.page.waitForTimeout(1000);

    const finalStatus = await pmp.getStatusBadge(targetName);
    expect(finalStatus).toContain('Đang bán');
  });

  test('should delete a product and verify it disappears from list', async () => {
    // Use the edited product (or test product)
    let row = pmp.rowForProduct(editedName);
    let targetName = editedName;
    if (!(await row.isVisible().catch(() => false))) {
      row = pmp.rowForProduct(testProductName);
      targetName = testProductName;
    }
    if (!(await row.isVisible().catch(() => false))) {
      // Create fresh for this test
      await pmp.createProduct({ name: targetName, price: 55000, category: 'Đồ uống' });
      await expect(pmp.rowForProduct(targetName)).toBeVisible({ timeout: 15000 });
    }

    await pmp.clickDeleteOn(targetName);
    await pmp.confirmDelete();

    // Verify product no longer appears
    await expect(pmp.rowForProduct(targetName)).not.toBeVisible({ timeout: 15000 });
  });
});
