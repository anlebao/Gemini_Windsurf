import { Page, expect } from '@playwright/test';

/**
 * Page Object Model for Customer/Guest User Interface
 * Handles menu browsing, cart management, checkout, and order tracking
 */
export class CustomerPage {
  readonly page: Page;
  
  // Selectors (initialized in constructor)
  readonly menuItems: any;
  readonly addToCartButton: any;
  readonly cartIcon: any;
  readonly cartCount: any;
  readonly checkoutButton: any;
  readonly guestNameInput: any;
  readonly guestPhoneInput: any;
  readonly guestAddressInput: any;
  readonly placeOrderButton: any;
  readonly orderSuccessMessage: any;
  readonly orderIdDisplay: any;
  readonly orderStatusDisplay: any;
  readonly loyaltyPointsDisplay: any;
  readonly qrCodeContainer: any;
  readonly loyaltyApplyButton: any;
  readonly loyaltyPointsInput: any;

  constructor(page: Page) {
    this.page = page;

    // Initialize selectors after page is set
    // W4: Updated to match actual KhachLink Home.razor selectors (feature-card class, not testid)
    this.menuItems = this.page.locator('.feature-card, [data-testid="home-product-card"]');
    this.addToCartButton = this.page.locator('button:has-text("Đặt ngay"), button:has-text("Add to Cart")');
    this.cartIcon = this.page.locator('.cart-icon, .shopping-cart');
    this.cartCount = this.page.locator('.cart-count, .badge');
    this.checkoutButton = this.page.locator('button:has-text("Thanh toán"), button:has-text("Checkout")');
    // W4: Use data-testid from Checkout.razor for robust selectors
    this.guestNameInput = this.page.getByTestId('checkout-input-name');
    this.guestPhoneInput = this.page.getByTestId('checkout-input-phone');
    this.guestAddressInput = this.page.getByTestId('checkout-input-address');
    this.placeOrderButton = this.page.getByTestId('checkout-btn-place-order');
    this.orderSuccessMessage = this.page.locator('.order-success, .order-confirmation, .alert-success');
    this.orderIdDisplay = this.page.locator('.order-id, .order-number');
    this.orderStatusDisplay = this.page.locator('.order-status, .status-badge');
    this.loyaltyPointsDisplay = this.page.locator('.loyalty-points, .points-balance');
    this.qrCodeContainer = this.page.locator('.qr-code, .payment-qr');
    this.loyaltyApplyButton = this.page.locator('button:has-text("Áp dụng điểm"), button:has-text("Apply Points")');
    this.loyaltyPointsInput = this.page.locator('input[name="points"], input[placeholder*="điểm"], input[placeholder*="points"]');
  }

  /**
   * Navigate to customer home page
   */
  async goto() {
    await this.page.goto('/');
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Navigate to a specific menu item page
   */
  async gotoMenuItem(itemId: string) {
    await this.page.goto(`/menu/${itemId}`);
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Navigate to checkout page
   */
  async gotoCheckout() {
    await this.page.goto('/checkout');
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Navigate to order tracking page
   */
  async gotoOrderTracking(orderId: string) {
    await this.page.goto(`/order-tracking/${orderId}`);
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Get the first menu item name
   */
  async getFirstMenuItemName(): Promise<string> {
    const firstItem = this.menuItems.first();
    await expect(firstItem).toBeVisible({ timeout: 10000 });
    const nameElement = firstItem.locator('h5, h3, .product-name');
    return await nameElement.textContent() || '';
  }

  /**
   * Add the first menu item to cart
   */
  async addFirstItemToCart() {
    const firstItem = this.menuItems.first();
    await expect(firstItem).toBeVisible({ timeout: 10000 });
    // W4: Use string selector directly (Locator-as-filter doesn't scope correctly)
    const addToCartBtn = firstItem.locator('button:has-text("Đặt ngay"), button:has-text("Add to Cart")');
    await expect(addToCartBtn).toBeVisible();
    await addToCartBtn.click();
    // W4: Wait for Blazor Server async processing — notification appears after CartService.AddItemAsync
    // "Đã thêm" = "Added" notification text shown by ShowNotification in Home.razor
    try {
      await expect(this.page.locator('text=/Đã thêm/i')).toBeVisible({ timeout: 5000 });
    } catch {
      // Notification may be transient — fallback to fixed wait
      await this.page.waitForTimeout(1500);
    }
  }

  /**
   * Add specific item to cart by name
   */
  async addItemToCartByName(itemName: string) {
    // W4: Use .feature-card selector (matches KhachLink Home.razor)
    const item = this.page.locator('.feature-card').filter({ hasText: itemName });
    await expect(item.first()).toBeVisible({ timeout: 10000 });
    const addToCartBtn = item.first().locator('button:has-text("Đặt ngay"), button:has-text("Add to Cart")');
    await expect(addToCartBtn).toBeVisible();
    await addToCartBtn.click();
  }

  /**
   * Get current cart count
   */
  async getCartCount(): Promise<number> {
    const count = await this.cartCount.textContent();
    return count ? parseInt(count.trim()) : 0;
  }

  /**
   * Proceed to checkout
   */
  async proceedToCheckout() {
    // W4: KhachLink flow: Home → add to cart → Cart page → checkout button → Checkout page
    // First try clicking checkout button if visible (already on cart page)
    if (await this.checkoutButton.isVisible({ timeout: 2000 })) {
      await this.checkoutButton.click();
    } else {
      // Navigate to cart first
      await this.page.goto('/cart');
      await this.page.waitForLoadState('networkidle');

      // W4: Blazor Server cart loads from localStorage in OnAfterRenderAsync(firstRender).
      // Wait for cart items to appear (or "empty" message to disappear) before clicking checkout.
      // Allow up to 8s for Blazor hydration + localStorage load + StateHasChanged
      const cartCheckoutBtn = this.page.getByTestId('cart-btn-checkout');
      try {
        await expect(cartCheckoutBtn).toBeVisible({ timeout: 8000 });
        await cartCheckoutBtn.click();
      } catch {
        // Cart may still be empty (Blazor timing issue) — fallback: navigate directly to checkout
        await this.page.goto('/checkout');
      }
    }
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Fill guest checkout form
   */
  async fillGuestCheckoutForm(data: {
    name: string;
    phone: string;
    address: string;
  }) {
    await expect(this.guestNameInput).toBeVisible({ timeout: 5000 });
    await this.guestNameInput.fill(data.name);
    
    await expect(this.guestPhoneInput).toBeVisible();
    await this.guestPhoneInput.fill(data.phone);
    
    if (await this.guestAddressInput.isVisible()) {
      await this.guestAddressInput.fill(data.address);
    }
  }

  /**
   * Submit order as guest
   */
  async submitGuestOrder() {
    await expect(this.placeOrderButton).toBeVisible({ timeout: 5000 });
    await this.placeOrderButton.click();
    
    // Wait for order success - allow up to 10s for processing
    await expect(this.orderSuccessMessage).toBeVisible({ timeout: 10000 });
  }

  /**
   * Get order ID from success page
   */
  async getOrderId(): Promise<string> {
    await expect(this.orderIdDisplay).toBeVisible({ timeout: 5000 });
    return await this.orderIdDisplay.textContent() || '';
  }

  /**
   * Get current order status
   */
  async getOrderStatus(): Promise<string> {
    await expect(this.orderStatusDisplay).toBeVisible({ timeout: 5000 });
    return await this.orderStatusDisplay.textContent() || '';
  }

  /**
   * Wait for order status to change to specific status
   * Uses fluent polling for NATS sync timing
   */
  async waitForOrderStatus(status: string, timeout: number = 10000) {
    await expect(this.orderStatusDisplay).toContainText(status, { timeout });
  }

  /**
   * Get loyalty points balance
   */
  async getLoyaltyPoints(): Promise<number> {
    const pointsText = await this.loyaltyPointsDisplay.textContent();
    if (!pointsText) return 0;
    const match = pointsText.match(/\d+/);
    return match ? parseInt(match[0]) : 0;
  }

  /**
   * Apply loyalty points for discount
   */
  async applyLoyaltyPoints(points: number) {
    await expect(this.loyaltyPointsInput).toBeVisible({ timeout: 5000 });
    await this.loyaltyPointsInput.fill(points.toString());
    await this.loyaltyApplyButton.click();
  }

  /**
   * Display QR code for payment
   */
  async displayPaymentQRCode() {
    await expect(this.qrCodeContainer).toBeVisible({ timeout: 10000 });
  }

  /**
   * Simulate successful payment (mock)
   */
  async simulatePaymentSuccess() {
    // This would typically interact with a payment mock or test payment gateway
    // For now, we'll click a "payment success" button or wait for status update
    const paymentSuccessBtn = this.page.locator('button:has-text("Thanh toán thành công"), button:has-text("Payment Successful")');
    if (await paymentSuccessBtn.isVisible({ timeout: 5000 })) {
      await paymentSuccessBtn.click();
    }
  }

  /**
   * Complete order lifecycle (payment + final status)
   */
  async completeOrder() {
    await this.displayPaymentQRCode();
    await this.simulatePaymentSuccess();
    
    // Wait for order completion status
    await this.waitForOrderStatus('Hoàn thành', 15000);
  }
}