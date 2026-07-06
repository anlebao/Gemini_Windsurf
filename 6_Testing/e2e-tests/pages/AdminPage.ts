import { Page, expect } from '@playwright/test';

/**
 * Page Object Model for Shop Owner/Admin Dashboard
 * Handles order management, order acceptance, and order processing
 */
export class AdminPage {
  readonly page: Page;
  
  // Selectors (initialized in constructor)
  readonly loginUsernameInput: any;
  readonly loginPasswordInput: any;
  readonly loginButton: any;
  readonly orderManagementLink: any;
  readonly ordersList: any;
  readonly searchOrderIdInput: any;
  readonly searchButton: any;
  readonly acceptOrderButton: any;
  readonly rejectOrderButton: any;
  readonly updateStatusButton: any;
  readonly statusSelect: any;
  readonly confirmButton: any;
  readonly orderDetailsPanel: any;
  readonly orderStatusBadge: any;
  readonly customerInfo: any;

  constructor(page: Page) {
    this.page = page;
    
    // Initialize selectors after page is set
    this.loginUsernameInput = this.page.locator('input[name="username"], input[placeholder*="tên đăng nhập"], input[type="text"]');
    this.loginPasswordInput = this.page.locator('input[name="password"], input[type="password"]');
    this.loginButton = this.page.locator('button:has-text("Đăng nhập"), button:has-text("Login")');
    this.orderManagementLink = this.page.locator('a:has-text("Quản lý đơn hàng"), a:has-text("Order Management"), .order-management');
    this.ordersList = this.page.locator('.order-list, .orders-table tbody tr, .order-card');
    this.searchOrderIdInput = this.page.locator('input[name="search"], input[placeholder*="tìm kiếm"], input[placeholder*="search"]');
    this.searchButton = this.page.locator('button:has-text("Tìm kiếm"), button:has-text("Search")');
    this.acceptOrderButton = this.page.locator('button:has-text("Chấp nhận"), button:has-text("Accept")');
    this.rejectOrderButton = this.page.locator('button:has-text("Từ chối"), button:has-text("Reject")');
    this.updateStatusButton = this.page.locator('button:has-text("Cập nhật trạng thái"), button:has-text("Update Status")');
    this.statusSelect = this.page.locator('select[name="status"], .status-select');
    this.confirmButton = this.page.locator('button:has-text("Xác nhận"), button:has-text("Confirm")');
    this.orderDetailsPanel = this.page.locator('.order-details, .order-info-panel');
    this.orderStatusBadge = this.page.locator('.status-badge, .order-status');
    this.customerInfo = this.page.locator('.customer-info, .customer-details');
  }

  /**
   * Navigate to admin login page
   */
  async gotoLogin() {
    await this.page.goto('/admin/login');
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Navigate to admin dashboard
   */
  async gotoDashboard() {
    await this.page.goto('/admin');
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Login to admin dashboard
   */
  async login(username: string, password: string) {
    await this.gotoLogin();
    
    await expect(this.loginUsernameInput).toBeVisible({ timeout: 5000 });
    await this.loginUsernameInput.fill(username);
    
    await expect(this.loginPasswordInput).toBeVisible();
    await this.loginPasswordInput.fill(password);
    
    await this.loginButton.click();
    await this.page.waitForLoadState('networkidle');
    
    // Verify login success - should be redirected to dashboard
    await expect(this.page).toHaveURL(/\/admin($|\/)/);
  }

  /**
   * Navigate to order management section
   */
  async gotoOrderManagement() {
    await this.orderManagementLink.click();
    await this.page.waitForLoadState('networkidle');
    
    // Verify we're on order management page
    await expect(this.ordersList).toBeVisible({ timeout: 5000 });
  }

  /**
   * Search for order by ID
   * Uses fluent polling for NATS sync timing
   */
  async searchOrderById(orderId: string) {
    await this.searchOrderIdInput.fill(orderId);
    await this.searchButton.click();
    await this.page.waitForLoadState('networkidle');
    
    // Wait for order to appear - allow up to 5s for NATS sync
    await expect(this.ordersList.first()).toBeVisible({ timeout: 5000 });
  }

  /**
   * Wait for order to appear in list
   * Critical for NATS sync timing - uses fluent polling
   */
  async waitForOrderToAppear(orderId: string, timeout: number = 5000) {
    const order = this.page.locator(`.order-list, .orders-table tbody tr, .order-card`).filter({ hasText: orderId });
    await expect(order.first()).toBeVisible({ timeout });
  }

  /**
   * Accept an order
   */
  async acceptOrder(orderId: string) {
    const order = this.page.locator(`.order-list, .orders-table tbody tr, .order-card`).filter({ hasText: orderId });
    await expect(order.first()).toBeVisible({ timeout: 5000 });
    
    const acceptBtn = order.first().locator(this.acceptOrderButton);
    await expect(acceptBtn).toBeVisible();
    await acceptBtn.click();
    
    // Confirm if modal appears
    if (await this.confirmButton.isVisible({ timeout: 2000 })) {
      await this.confirmButton.click();
    }
  }

  /**
   * Reject an order
   */
  async rejectOrder(orderId: string) {
    const order = this.page.locator(`.order-list, .orders-table tbody tr, .order-card`).filter({ hasText: orderId });
    await expect(order.first()).toBeVisible({ timeout: 5000 });
    
    const rejectBtn = order.first().locator(this.rejectOrderButton);
    await expect(rejectBtn).toBeVisible();
    await rejectBtn.click();
    
    // Confirm if modal appears
    if (await this.confirmButton.isVisible({ timeout: 2000 })) {
      await this.confirmButton.click();
    }
  }

  /**
   * Click on order to view details
   */
  async viewOrderDetails(orderId: string) {
    const order = this.page.locator(`.order-list, .orders-table tbody tr, .order-card`).filter({ hasText: orderId });
    await expect(order.first()).toBeVisible({ timeout: 5000 });
    await order.first().click();
    
    await expect(this.orderDetailsPanel).toBeVisible({ timeout: 5000 });
  }

  /**
   * Update order status
   */
  async updateOrderStatus(status: string) {
    if (await this.updateStatusButton.isVisible()) {
      await this.updateStatusButton.click();
    }
    
    await expect(this.statusSelect).toBeVisible({ timeout: 3000 });
    await this.statusSelect.selectOption({ label: status });
    
    await expect(this.confirmButton).toBeVisible();
    await this.confirmButton.click();
    
    // Wait for status update to reflect — fluent wait for network idle
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Get current order status
   */
  async getOrderStatus(orderId: string): Promise<string> {
    const order = this.page.locator(`.order-list, .orders-table tbody tr, .order-card`).filter({ hasText: orderId });
    const statusBadge = order.first().locator(this.orderStatusBadge);
    await expect(statusBadge).toBeVisible({ timeout: 5000 });
    return await statusBadge.textContent() || '';
  }

  /**
   * Wait for order status to change
   * Uses fluent polling for NATS sync timing
   */
  async waitForOrderStatus(orderId: string, status: string, timeout: number = 5000) {
    const order = this.page.locator(`.order-list, .orders-table tbody tr, .order-card`).filter({ hasText: orderId });
    const statusBadge = order.first().locator(this.orderStatusBadge);
    await expect(statusBadge).toContainText(status, { timeout });
  }

  /**
   * Get customer information from order
   */
  async getCustomerInfo(orderId: string): Promise<{ name: string; phone: string }> {
    await this.viewOrderDetails(orderId);
    
    const customerSection = this.orderDetailsPanel.locator(this.customerInfo);
    await expect(customerSection).toBeVisible({ timeout: 5000 });
    
    const name = await customerSection.locator('.customer-name, .name').textContent() || '';
    const phone = await customerSection.locator('.customer-phone, .phone').textContent() || '';
    
    return { name: name.trim(), phone: phone.trim() };
  }

  /**
   * Refresh order list
   */
  async refreshOrders() {
    await this.page.reload();
    await this.page.waitForLoadState('networkidle');
  }
}