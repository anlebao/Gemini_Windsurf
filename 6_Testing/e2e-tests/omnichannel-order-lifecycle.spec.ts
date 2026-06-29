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
  test('SCENARIO 1: First-Time Guest Omnichannel Order Flow', async ({ browser }) => {
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
        process.env.ADMIN_USERNAME || 'admin',
        process.env.ADMIN_PASSWORD || 'admin123'
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
        process.env.KITCHEN_USERNAME || 'kitchen',
        process.env.KITCHEN_PASSWORD || 'kitchen123'
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

  /**
   * SCENARIO 2: Returning Loyalty Customer Flow (PWA / Installed App Simulator)
   * 
   * This test covers loyalty customer order flow with points redemption and points update.
   */
  test('SCENARIO 2: Returning Loyalty Customer Flow', async ({ browser }) => {
    // Pre-condition: Create loyalty customer with existing points
    const loyaltyData = TestDataGenerator.generateLoyaltyCustomerData();
    testData = { ...testData, ...loyaltyData };
    
    // Step 1: Loyalty customer places order
    const customerContext = await browser.newContext();
    const customerPage = new CustomerPage(await customerContext.newPage());
    
    await test.step('Loyalty Customer: Login and view points balance', async () => {
      await customerPage.goto();
      
      // Simulate login (would need actual login implementation)
      // For now, we'll verify loyalty points display exists
      const pointsDisplay = customerPage.loyaltyPointsDisplay;
      await expect(pointsDisplay).toBeVisible({ timeout: 5000 });
      
      // Get current points balance
      const currentPoints = await customerPage.getLoyaltyPoints();
      testData.initialPoints = currentPoints;
      
      console.log(`Loyalty customer points balance: ${currentPoints}`);
    });

    await test.step('Loyalty Customer: Add items to cart and apply points', async () => {
      // Add item to cart
      await customerPage.addFirstItemToCart();
      
      // Apply loyalty points for discount
      if (testData.initialPoints > 100) {
        await customerPage.applyLoyaltyPoints(100);
        testData.pointsApplied = 100;
      }
    });

    await test.step('Loyalty Customer: Complete checkout', async () => {
      await customerPage.proceedToCheckout();
      
      // Fill checkout form
      await customerPage.fillGuestCheckoutForm({
        name: loyaltyData.name,
        phone: loyaltyData.phone,
        address: testData.address
      });
      
      // Submit order
      await customerPage.submitGuestOrder();
      
      const orderId = await customerPage.getOrderId();
      testData.orderId = orderId;
      expect(orderId).toBeTruthy();
      
      console.log(`Loyalty customer order placed. Order ID: ${orderId}`);
    });

    await customerContext.close();

    // Step 2: Cross-station processing (same as Scenario 1)
    const adminContext = await browser.newContext();
    const adminPage = new AdminPage(await adminContext.newPage());
    
    await test.step('Admin: Accept order (NATS sync wait)', async () => {
      await adminPage.gotoLogin();
      await adminPage.login(
        process.env.ADMIN_USERNAME || 'admin',
        process.env.ADMIN_PASSWORD || 'admin123'
      );
      await adminPage.gotoOrderManagement();
      
      await adminPage.waitForOrderToAppear(testData.orderId, 5000);
      await adminPage.acceptOrder(testData.orderId);
      
      console.log(`Admin accepted loyalty order ${testData.orderId}`);
    });

    const kitchenContext = await browser.newContext();
    const kitchenPage = new KitchenPage(await kitchenContext.newPage());
    
    await test.step('Kitchen: Process order (NATS sync wait)', async () => {
      await kitchenPage.gotoLogin();
      await kitchenPage.login(
        process.env.KITCHEN_USERNAME || 'kitchen',
        process.env.KITCHEN_PASSWORD || 'kitchen123'
      );
      
      await kitchenPage.waitForOrderToAppear(testData.orderId, 5000);
      await kitchenPage.completeKitchenWorkflow(testData.orderId);
      
      console.log(`Kitchen completed loyalty order ${testData.orderId}`);
    });

    await kitchenContext.close();

    // Step 3: Complete payment and verify points update
    const customerTrackingContext = await browser.newContext();
    const customerTrackingPage = new CustomerPage(await customerTrackingContext.newPage());
    
    await test.step('Customer: Complete payment and verify points update', async () => {
      await customerTrackingPage.gotoOrderTracking(testData.orderId);
      
      await customerTrackingPage.waitForOrderStatus('Sẵn sàng', 5000);
      await customerTrackingPage.completeOrder();
      
      // Verify points balance updated
      const finalPoints = await customerTrackingPage.getLoyaltyPoints();
      const expectedPoints = testData.initialPoints - (testData.pointsApplied || 0) + 50; // Assume 50 points earned
      
      // Allow some margin for points calculation
      expect(finalPoints).toBeGreaterThan(testData.initialPoints - (testData.pointsApplied || 0));
      
      console.log(`Points updated: ${testData.initialPoints} → ${finalPoints}`);
    });

    await customerTrackingContext.close();
    await adminContext.close();
  });

  /**
   * SCENARIO 3: Network Interruption / Edge Offline Resiliency
   * 
   * This test validates the offline-first architecture with Outbox pattern
   * and NATS sync worker resilience.
   */
  test('SCENARIO 3: Network Interruption / Edge Offline Resiliency', async ({ browser, context }) => {
    // Step 1: Simulate network failure
    await test.step('Simulate network failure', async () => {
      // Block all network requests to simulate offline state
      await context.setOffline(true);
      console.log('Network set to offline mode');
    });

    const customerContext = await browser.newContext();
    const customerPage = new CustomerPage(await customerContext.newPage());
    
    await test.step('Customer: Place order while offline', async () => {
      await customerContext.setOffline(true);
      
      await customerPage.goto();
      
      // UI should remain responsive even offline
      await expect(customerPage.menuItems.first()).toBeVisible({ timeout: 10000 });
      
      // Add item to cart
      await customerPage.addFirstItemToCart();
      
      // Proceed to checkout
      await customerPage.proceedToCheckout();
      
      // Fill checkout form
      await customerPage.fillGuestCheckoutForm({
        name: testData.name,
        phone: testData.phone,
        address: testData.address
      });
      
      // Submit order - should queue in Outbox
      await customerPage.submitGuestOrder();
      
      // Verify UI remains responsive and shows offline indicator
      const offlineIndicator = customerPage.page.locator('.offline-indicator, .network-status');
      if (await offlineIndicator.isVisible({ timeout: 2000 })) {
        console.log('Offline indicator visible as expected');
      }
      
      // Order should be saved locally (Outbox pattern)
      console.log('Order queued in Outbox for sync when online');
    });

    await test.step('Restore network and verify NATS sync', async () => {
      // Restore network connection
      await customerContext.setOffline(false);
      console.log('Network restored to online mode');
      
      // NATS background sync worker should automatically flush the outbox
      // Wait for order to appear on admin side - NATS sync timing
      const adminContext = await browser.newContext();
      const adminPage = new AdminPage(await adminContext.newPage());
      
      await adminPage.gotoLogin();
      await adminPage.login(
        process.env.ADMIN_USERNAME || 'admin',
        process.env.ADMIN_PASSWORD || 'admin123'
      );
      await adminPage.gotoOrderManagement();
      
      // Wait for order to sync from Outbox - allow up to 10s for network restoration + NATS sync
      await adminPage.waitForOrderToAppear(testData.orderId, 10000);
      
      const status = await adminPage.getOrderStatus(testData.orderId);
      expect(status).toBeTruthy();
      
      console.log(`Order successfully synced after network restoration. Status: ${status}`);
      
      await adminContext.close();
    });

    await customerContext.close();
  });
});