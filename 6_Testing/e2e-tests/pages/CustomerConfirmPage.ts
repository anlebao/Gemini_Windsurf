import { Page, expect } from '@playwright/test';

/**
 * W4-T5: Page Object for Customer Confirm + Loyalty + Thank You flow (KhachLink OrderTracking).
 * Handles "Xác nhận đã nhận hàng" button, loyalty modal, and "Cảm ơn" message verification.
 */
export class CustomerConfirmPage {
  readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  /**
   * Navigate to order tracking page.
   */
  async goto(orderId: string): Promise<void> {
    await this.page.goto(`/order-tracking/${orderId}`);
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Click "Xác nhận đã nhận hàng" button (visible when order status = "ready").
   */
  async clickConfirmReceived(): Promise<void> {
    const btn = this.page.getByTestId('btn-confirm-received');
    await expect(btn).toBeVisible({ timeout: 15000 });
    await btn.click();
  }

  /**
   * Wait for order status to change to "delivered" (polling 3s + buffer).
   */
  async waitForDeliveredStatus(timeout: number = 15000): Promise<void> {
    // OrderTracking polls every 3s — wait for "delivered" status text
    await expect(this.page.locator('text=delivered')).toBeVisible({ timeout });
  }

  /**
   * Verify loyalty upgrade modal is visible (when Loyalty_Program_Enabled = ON).
   */
  async verifyLoyaltyModalVisible(timeout: number = 5000): Promise<void> {
    const modal = this.page.locator('[data-testid="identity-upgrade-modal"], .modal:has-text("Đăng ký")');
    await expect(modal).toBeVisible({ timeout });
  }

  /**
   * Verify "Cảm ơn quý khách" message is visible (when Loyalty_Program_Enabled = OFF).
   */
  async verifyThankYouMessage(timeout: number = 5000): Promise<void> {
    const thankYou = this.page.locator('text=/Cảm ơn quý khách/i');
    await expect(thankYou).toBeVisible({ timeout });
  }

  /**
   * Verify PWA install prompt is visible (for guest users — not logged in).
   */
  async verifyPWAInstallPromptVisible(timeout: number = 5000): Promise<void> {
    const pwaPrompt = this.page.locator('[data-testid="pwa-install-prompt"], .pwa-install-prompt');
    await expect(pwaPrompt).toBeVisible({ timeout });
  }

  /**
   * Verify PWA install prompt is NOT visible (for logged-in users).
   */
  async verifyPWAInstallPromptHidden(): Promise<void> {
    const pwaPrompt = this.page.locator('[data-testid="pwa-install-prompt"], .pwa-install-prompt');
    await expect(pwaPrompt).not.toBeVisible();
  }

  /**
   * Full flow: confirm received → wait for delivered → verify loyalty modal OR thank you.
   */
  async confirmReceivedAndVerifyLoyalty(loyaltyEnabled: boolean): Promise<void> {
    await this.clickConfirmReceived();
    await this.waitForDeliveredStatus();
    if (loyaltyEnabled) {
      await this.verifyLoyaltyModalVisible();
    } else {
      await this.verifyThankYouMessage();
    }
  }
}
