import { Page, expect } from '@playwright/test';

/**
 * Page Object Model for Kitchen Display System (KDS)
 * Handles order preparation status updates and kitchen workflow
 */
export class KitchenPage {
  readonly page: Page;

  // Selectors
  readonly loginUsernameInput = this.page.locator('input[name="username"], input[type="text"]');
  readonly loginPasswordInput = this.page.locator('input[name="password"], input[type="password"]');
  readonly loginButton = this.page.locator('button:has-text("Đăng nhập"), button:has-text("Login")');
  readonly kitchenOrdersList = this.page.locator('.kitchen-orders, .kds-orders, .order-station-list');
  readonly orderCard = this.page.locator('.order-card, .kds-order-card, .station-order');
  readonly preparingButton = this.page.locator('button:has-text("Đang chuẩn bị"), button:has-text("Preparing")');
  readonly readyButton = this.page.locator('button:has-text("Sẵn sàng"), button:has-text("Ready")');
  readonly completeButton = this.page.locator('button:has-text("Hoàn thành"), button:has-text("Complete")');
  readonly orderStatusBadge = this.page.locator('.status-badge, .order-status, .kds-status');
  readonly orderIdDisplay = this.page.locator('.order-id, .order-number');
  readonly orderItems = this.page.locator('.order-items, .kds-items');
  readonly orderTimer = this.page.locator('.order-timer, .prep-time');
  readonly stationFilter = this.page.locator('.station-filter, .kitchen-station-select');
  readonly refreshButton = this.page.locator('button:has-text("Làm mới"), button:has-text("Refresh")');

  constructor(page: Page) {
    this.page = page;
  }

  /**
   * Navigate to kitchen station login
   */
  async gotoLogin() {
    await this.page.goto('/kitchen/login');
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Navigate to kitchen station dashboard
   */
  async gotoKitchen() {
    await this.page.goto('/kitchen');
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Login to kitchen station
   */
  async login(username: string, password: string) {
    await this.gotoLogin();
    
    await expect(this.loginUsernameInput).toBeVisible({ timeout: 5000 });
    await this.loginUsernameInput.fill(username);
    
    await expect(this.loginPasswordInput).toBeVisible();
    await this.loginPasswordInput.fill(password);
    
    await this.loginButton.click();
    await this.page.waitForLoadState('networkidle');
    
    // Verify login success
    await expect(this.kitchenOrdersList).toBeVisible({ timeout: 5000 });
  }

  /**
   * Wait for order to appear in kitchen display
   * Critical for NATS sync timing - uses fluent polling
   */
  async waitForOrderToAppear(orderId: string, timeout: number = 5000) {
    const order = this.orderCard.filter({ hasText: orderId });
    await expect(order.first()).toBeVisible({ timeout });
  }

  /**
   * Find order by ID in kitchen display
   */
  async findOrder(orderId: string) {
    const order = this.orderCard.filter({ hasText: orderId });
    await expect(order.first()).toBeVisible({ timeout: 5000 });
    return order.first();
  }

  /**
   * Mark order as "Preparing"
   */
  async markAsPreparing(orderId: string) {
    const order = await this.findOrder(orderId);
    
    const preparingBtn = order.locator(this.preparingButton);
    await expect(preparingBtn).toBeVisible({ timeout: 3000 });
    await preparingBtn.click();
    
    // Wait for status update to reflect
    await this.page.waitForTimeout(500);
  }

  /**
   * Mark order as "Ready for Handover"
   */
  async markAsReady(orderId: string) {
    const order = await this.findOrder(orderId);
    
    const readyBtn = order.locator(this.readyButton);
    await expect(readyBtn).toBeVisible({ timeout: 3000 });
    await readyBtn.click();
    
    // Wait for status update to reflect
    await this.page.waitForTimeout(500);
  }

  /**
   * Mark order as "Complete"
   */
  async markAsComplete(orderId: string) {
    const order = await this.findOrder(orderId);
    
    const completeBtn = order.locator(this.completeButton);
    await expect(completeBtn).toBeVisible({ timeout: 3000 });
    await completeBtn.click();
    
    // Wait for status update to reflect
    await this.page.waitForTimeout(500);
  }

  /**
   * Get current order status in kitchen display
   */
  async getOrderStatus(orderId: string): Promise<string> {
    const order = await this.findOrder(orderId);
    const statusBadge = order.locator(this.orderStatusBadge);
    await expect(statusBadge).toBeVisible({ timeout: 3000 });
    return await statusBadge.textContent() || '';
  }

  /**
   * Wait for order status to change
   * Uses fluent polling for NATS sync timing
   */
  async waitForOrderStatus(orderId: string, status: string, timeout: number = 5000) {
    const order = this.orderCard.filter({ hasText: orderId });
    const statusBadge = order.first().locator(this.orderStatusBadge);
    await expect(statusBadge).toContainText(status, { timeout });
  }

  /**
   * Get order items from kitchen display
   */
  async getOrderItems(orderId: string): Promise<string[]> {
    const order = await this.findOrder(orderId);
    const items = order.locator(this.orderItems);
    await expect(items).toBeVisible({ timeout: 3000 });
    
    const itemElements = await items.locator('.item, .product-item').all();
    const itemNames: string[] = [];
    
    for (const item of itemElements) {
      const name = await item.textContent();
      if (name) itemNames.push(name.trim());
    }
    
    return itemNames;
  }

  /**
   * Get order preparation time
   */
  async getOrderPrepTime(orderId: string): Promise<string> {
    const order = await this.findOrder(orderId);
    const timer = order.locator(this.orderTimer);
    
    if (await timer.isVisible({ timeout: 2000 })) {
      return await timer.textContent() || '';
    }
    
    return '';
  }

  /**
   * Filter orders by station
   */
  async filterByStation(station: string) {
    if (await this.stationFilter.isVisible({ timeout: 2000 })) {
      await this.stationFilter.selectOption({ label: station });
      await this.page.waitForLoadState('networkidle');
    }
  }

  /**
   * Refresh kitchen display
   */
  async refresh() {
    if (await this.refreshButton.isVisible({ timeout: 2000 })) {
      await this.refreshButton.click();
    } else {
      await this.page.reload();
    }
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Complete full kitchen workflow: Accept → Preparing → Ready
   */
  async completeKitchenWorkflow(orderId: string) {
    // Mark as preparing
    await this.markAsPreparing(orderId);
    await this.waitForOrderStatus(orderId, 'Đang chuẩn bị', 5000);
    
    // Mark as ready
    await this.markAsReady(orderId);
    await this.waitForOrderStatus(orderId, 'Sẵn sàng', 5000);
  }
}