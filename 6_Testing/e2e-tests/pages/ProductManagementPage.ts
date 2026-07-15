import { Page, expect, Locator } from '@playwright/test';
import { loadEnvConfig } from '../../utils/env-config';

const config = loadEnvConfig();

/**
 * Page Object Model for Product Management page (/products)
 * Phase 6 — selectors match ProductManagement.razor structure.
 */
export class ProductManagementPage {
  readonly page: Page;

  // Header actions
  readonly createButton: Locator;
  readonly refreshButton: Locator;
  readonly batchPrintButton: Locator;

  // DataGrid
  readonly dataGridRows: Locator;
  readonly emptyState: Locator;

  // Create modal
  readonly createModal: Locator;
  readonly createNameInput: Locator;
  readonly createDescriptionInput: Locator;
  readonly createPriceInput: Locator;
  readonly createCostPriceInput: Locator;
  readonly createCategoryInput: Locator;
  readonly createVatRateInput: Locator;
  readonly createSubmitButton: Locator;
  readonly createCancelButton: Locator;

  // Edit modal
  readonly editModal: Locator;
  readonly editNameInput: Locator;
  readonly editPriceInput: Locator;
  readonly editCategoryInput: Locator;
  readonly editVatRateInput: Locator;
  readonly editActiveSelect: Locator;
  readonly editSubmitButton: Locator;

  // Delete modal
  readonly deleteModal: Locator;
  readonly deleteConfirmButton: Locator;

  // QR modal
  readonly qrModal: Locator;
  readonly qrImage: Locator;
  readonly qrPrintButton: Locator;

  // Alert
  readonly errorAlert: Locator;

  constructor(page: Page) {
    this.page = page;

    this.createButton = page.locator('button:has-text("Thêm sản phẩm")').first();
    this.refreshButton = page.locator('button:has-text("Làm mới")').first();
    this.batchPrintButton = page.locator('button:has-text("In QR đã chọn")').first();

    this.dataGridRows = page.locator('table.vanan-data-grid tbody tr');
    this.emptyState = page.locator('.empty-state');

    this.createModal = page.locator('.modal:has(h5:has-text("Thêm sản phẩm mới"))');
    this.createNameInput = page.locator('#create-name');
    this.createDescriptionInput = page.locator('#create-description');
    this.createPriceInput = page.locator('#create-price');
    this.createCostPriceInput = page.locator('#create-costprice');
    this.createCategoryInput = page.locator('#create-category');
    this.createVatRateInput = page.locator('#create-vatrate');
    this.createSubmitButton = page.locator('.modal:has(h5:has-text("Thêm sản phẩm mới")) button[type="submit"]');
    this.createCancelButton = page.locator('.modal:has(h5:has-text("Thêm sản phẩm mới")) button:has-text("Huỷ")');

    this.editModal = page.locator('.modal:has(h5:has-text("Sửa sản phẩm"))');
    this.editNameInput = page.locator('#edit-name');
    this.editPriceInput = page.locator('#edit-price');
    this.editCategoryInput = page.locator('#edit-category');
    this.editVatRateInput = page.locator('#edit-vatrate');
    this.editActiveSelect = page.locator('#edit-active');
    this.editSubmitButton = page.locator('.modal:has(h5:has-text("Sửa sản phẩm")) button[type="submit"]');

    this.deleteModal = page.locator('.modal:has(h5:has-text("Xác nhận xoá"))');
    this.deleteConfirmButton = page.locator('.modal:has(h5:has-text("Xác nhận xoá")) button:has-text("Xoá")');

    this.qrModal = page.locator('.modal:has(h5:has-text("QR Code sản phẩm"))');
    this.qrImage = page.locator('.modal:has(h5:has-text("QR Code sản phẩm")) img[src*="/qr"]');
    this.qrPrintButton = page.locator('.modal:has(h5:has-text("QR Code sản phẩm")) button:has-text("In QR code")');

    this.errorAlert = page.locator('.vanan-alert, .alert-danger');
  }

  async navigate() {
    // Use full URL — page.goto('/products') resolves to domain root, not baseURL path
    await this.page.goto(`${config.SHOPERP_URL}/products`);
    await this.page.waitForLoadState('networkidle');
  }

  /** Get a row by product name (first matching row) */
  rowForProduct(name: string): Locator {
    return this.dataGridRows.filter({ hasText: name }).first();
  }

  /** Click the "Sửa" (Edit) button on a row matching productName */
  async clickEditOn(productName: string) {
    const row = this.rowForProduct(productName);
    await row.locator('button:has-text("Sửa")').click();
    await expect(this.editModal).toBeVisible({ timeout: 5000 });
  }

  /** Click the "Xoá" (Delete) button on a row matching productName */
  async clickDeleteOn(productName: string) {
    const row = this.rowForProduct(productName);
    await row.locator('button:has-text("Xoá")').click();
    await expect(this.deleteModal).toBeVisible({ timeout: 5000 });
  }

  /** Click the "Tạm ngưng" (Deactivate) button on a row matching productName */
  async clickDeactivateOn(productName: string) {
    const row = this.rowForProduct(productName);
    await row.locator('button:has-text("Tạm ngưng")').click();
  }

  /** Click the "Kích hoạt" (Activate) button on a row matching productName */
  async clickActivateOn(productName: string) {
    const row = this.rowForProduct(productName);
    await row.locator('button:has-text("Kích hoạt")').click();
  }

  /** Click the "📱 QR" button on a row matching productName */
  async clickQrOn(productName: string) {
    const row = this.rowForProduct(productName);
    await row.locator('button:has-text("QR")').click();
    await expect(this.qrModal).toBeVisible({ timeout: 5000 });
  }

  /** Toggle the checkbox for a row matching productName */
  async toggleCheckboxOn(productName: string) {
    const row = this.rowForProduct(productName);
    await row.locator('input[type="checkbox"]').click();
  }

  /** Create a product via the Create modal */
  async createProduct(dto: {
    name: string;
    description?: string;
    price: number;
    costPrice?: number;
    category: string;
    vatRate?: number;
  }) {
    await this.createButton.click();
    await expect(this.createModal).toBeVisible({ timeout: 5000 });

    await this.createNameInput.fill(dto.name);
    if (dto.description) await this.createDescriptionInput.fill(dto.description);
    await this.createPriceInput.fill(String(dto.price));
    if (dto.costPrice) await this.createCostPriceInput.fill(String(dto.costPrice));
    await this.createCategoryInput.fill(dto.category);
    if (dto.vatRate !== undefined) await this.createVatRateInput.fill(String(dto.vatRate));

    await this.createSubmitButton.click();
    // Wait for modal to close (modal disappears)
    await expect(this.createModal).not.toBeVisible({ timeout: 15000 });
  }

  /** Submit edit form */
  async submitEdit() {
    await this.editSubmitButton.click();
    await expect(this.editModal).not.toBeVisible({ timeout: 15000 });
  }

  /** Confirm delete */
  async confirmDelete() {
    await this.deleteConfirmButton.click();
    await expect(this.deleteModal).not.toBeVisible({ timeout: 15000 });
  }

  /** Get status badge text for a row */
  async getStatusBadge(productName: string): Promise<string> {
    const row = this.rowForProduct(productName);
    const badge = row.locator('.status-badge').first();
    return (await badge.textContent()) ?? '';
  }
}
