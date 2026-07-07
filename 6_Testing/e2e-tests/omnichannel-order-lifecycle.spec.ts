import { test, expect } from '@playwright/test';
import { CustomerPage } from './pages/CustomerPage';
import { AdminPage } from './pages/AdminPage';
import { KitchenPage } from './pages/KitchenPage';
import { TestDataCleaner, TestDataGenerator } from './utils/test-data-cleaner';
import { loadEnvConfig } from '../utils/env-config';

const config = loadEnvConfig();

/**
 * Comprehensive E2E Test Suite for vanantech.io.vn Omnichannel Order Lifecycle
 * 
 * Architecture: Hybrid Online/Offline-First with NATS Sync Workers
 * Critical Timing: Uses fluent polling (expect.toBeVisible with timeout) instead of hardcoded sleep
 * for NATS synchronization (1-second polling interval)
 * 
 * URL: https://vanantech.io.vn
 */

test.describe('Omnichannel Order Lifecycle E2E Tests', () => {
  let testDataCleaner: TestDataCleaner;
  let testData: any = {};

  test.beforeEach(async ({ request, context }) => {
    testDataCleaner = new TestDataCleaner(request, config.GATEWAY_URL);
    
    // Generate unique test data for each test
    testData = TestDataGenerator.generateGuestData();
  });

  test.afterEach(async () => {
    // Cleanup test data after each test
    if (testDataCleaner && testData) {
      await testDataCleaner.cleanupTestData({
        orderId: testData.orderId,
        phoneNumber: testData.phone,
        customerId: testData.customerId
      });
    }
  });

  /**
   * SCENARIO 1: First-Time Guest Omnichannel Order Flow (Guest Web to Handover)
   * 
   * This test covers the complete order lifecycle from guest checkout to handover,
   * including NATS sync timing between different stations.
   */
  // W6/Bucket A: DEFERRED — Test spec assumes a guest checkout form (name/phone/address
  // inputs + "Đặt hàng" button) that doesn't exist in KhachLink's cart-based Checkout.razor.
  // User decision (2026-07-07): implement guest-form UI as a separate feature build.
  // Feature task card: docs/AI/tasks/feature_guest_checkout_form_task_card.md (to be created).
  // This test is skipped until the guest-form UI feature is implemented.
  test.skip('SCENARIO 1: First-Time Guest Omnichannel Order Flow @golden', async ({ browser }) => {
    // Step 1: Customer places order as guest
    const customerContext = await browser.newContext();
    const customerPage = new CustomerPage(await customerContext.newPage());
    
    await test.step('Customer: Browse menu and add items to cart', async () => {
      await customerPage.goto();
      
      // Get first menu item name for verification
      const firstItemName = await customerPage.getFirstMenuItemName();
      testData.itemName = firstItemName;
      
      // Add item to cart
      await customerPage.addFirstItemToCart();
      
      // Verify cart count increased
      const cartCount = await customerPage.getCartCount();
      expect(cartCount).toBeGreaterThan(0);
      
      console.log(`Added item "${firstItemName}" to cart. Cart count: ${cartCount}`);
    });

    await test.step('Customer: Complete guest checkout', async () => {
      await customerPage.proceedToCheckout();
      
      // Fill guest checkout form
      await customerPage.fillGuestCheckoutForm({
        name: testData.name,
        phone: testData.phone,
        address: testData.address
      });
      
      // Submit order
      await customerPage.submitGuestOrder();
      
      // Capture order ID
      const orderId = await customerPage.getOrderId();
      testData.orderId = orderId;
      expect(orderId).toBeTruthy();
      expect(orderId.length).toBeGreaterThan(0);
      
      console.log(`Order placed successfully. Order ID: ${orderId}`);
    });

    // Close customer context
    await customerContext.close();

    // Step 2: Admin accepts and processes order
    // CRITICAL: Allow up to 5s for NATS sync from edge to cloud
    const adminContext = await browser.newContext();
    const adminPage = new AdminPage(await adminContext.newPage());
    
    await test.step('Admin: Login and navigate to order management', async () => {
      await adminPage.gotoLogin();
      await adminPage.login(
        config.ADMIN_USERNAME,
        config.ADMIN_PASSWORD
      );
      await adminPage.gotoOrderManagement();
    });

    await test.step('Admin: Locate and accept the order (NATS sync wait)', async () => {
      // Wait for order to appear - NATS sync timing
      await adminPage.waitForOrderToAppear(testData.orderId, 5000);
      
      // Accept the order
      await adminPage.acceptOrder(testData.orderId);
      
      // Verify status changed to accepted
      const status = await adminPage.getOrderStatus(testData.orderId);
      expect(status).toContain('Đã chấp nhận');
      
      console.log(`Admin accepted order ${testData.orderId}. Status: ${status}`);
    });

    // Step 3: Kitchen processes order
    const kitchenContext = await browser.newContext();
    const kitchenPage = new KitchenPage(await kitchenContext.newPage());
    
    await test.step('Kitchen: Login and wait for order (NATS sync wait)', async () => {
      await kitchenPage.gotoLogin();
      await kitchenPage.login(
        config.KITCHEN_USERNAME,
        config.KITCHEN_PASSWORD
      );
      
      // Wait for order to appear in kitchen display - NATS sync timing
      await kitchenPage.waitForOrderToAppear(testData.orderId, 5000);
    });

    await test.step('Kitchen: Mark order as Preparing and Ready', async () => {
      // Complete kitchen workflow
      await kitchenPage.completeKitchenWorkflow(testData.orderId);
      
      // Verify final status
      const status = await kitchenPage.getOrderStatus(testData.orderId);
      expect(status).toContain('Sẵn sàng');
      
      console.log(`Kitchen completed order ${testData.orderId}. Status: ${status}`);
    });

    await kitchenContext.close();

    // Step 4: Customer views updated status and completes payment
    const customerTrackingContext = await browser.newContext();
    const customerTrackingPage = new CustomerPage(await customerTrackingContext.newPage());
    
    await test.step('Customer: View order tracking (NATS sync wait)', async () => {
      await customerTrackingPage.gotoOrderTracking(testData.orderId);
      
      // Wait for status to update to Ready - NATS sync timing
      await customerTrackingPage.waitForOrderStatus('Sẵn sàng', 5000);
      
      const status = await customerTrackingPage.getOrderStatus();
      expect(status).toContain('Sẵn sàng');
      
      console.log(`Customer sees order status: ${status}`);
    });

    await test.step('Customer: Complete payment with QR code', async () => {
      // Display QR code
      await customerTrackingPage.displayPaymentQRCode();
      
      // Simulate successful payment
      await customerTrackingPage.simulatePaymentSuccess();
      
      // Wait for order completion
      await customerTrackingPage.waitForOrderStatus('Hoàn thành', 15000);
      
      const finalStatus = await customerTrackingPage.getOrderStatus();
      expect(finalStatus).toContain('Hoàn thành');
      
      console.log(`Order completed. Final status: ${finalStatus}`);
    });

    await customerTrackingContext.close();
    await adminContext.close();
  });
});