import { Page, APIRequestContext } from '@playwright/test';

/**
 * W3: Test Tenant ID — matches DevLoginController (11111111-1111-1111-1111-111111111111)
 * All E2E test data is created under this tenant. Cleanup methods filter by this tenant.
 *
 * AccountingEntry Strategy:
 *   AccountingEntry is 100% immutable by Domain design (append-only, Reversal Entry only).
 *   E2E tests that trigger payment confirmation will create AccountingEntries under the test tenant.
 *   These entries are ACCEPTED AS TEST GARBAGE — they cannot be deleted without violating Domain integrity.
 *   Production reports MUST filter by TenantId to exclude test tenant data:
 *     WHERE TenantId != '11111111-1111-1111-1111-111111111111'
 */
export const TEST_TENANT_ID = process.env.TEST_TENANT_ID || '11111111-1111-1111-1111-111111111111';

/**
 * Test Data Cleaner Utility
 * Handles cleanup of test data to prevent pollution of production-like states.
 *
 * W3: All cleanup methods are scoped to TEST_TENANT_ID to ensure test data isolation.
 */
export class TestDataCleaner {
  private readonly apiContext: APIRequestContext;
  private readonly baseURL: string;
  private readonly tenantId: string;
  private readonly authHeaders: Record<string, string>;

  constructor(apiContext: APIRequestContext, baseURL: string, tenantId?: string, authHeaders?: Record<string, string>) {
    this.apiContext = apiContext;
    this.baseURL = baseURL;
    this.tenantId = tenantId || TEST_TENANT_ID;
    this.authHeaders = authHeaders || {};
  }

  /**
   * Generate unique test phone number
   * Format: TEST + timestamp + random suffix
   */
  static generateTestPhoneNumber(): string {
    const timestamp = Date.now();
    const random = Math.floor(Math.random() * 1000);
    return `TEST${timestamp}${random}`;
  }

  /**
   * Generate unique test email
   */
  static generateTestEmail(): string {
    const timestamp = Date.now();
    const random = Math.floor(Math.random() * 1000);
    return `test${timestamp}${random}@vanantest.io.vn`;
  }

  /**
   * Generate unique test name
   */
  static generateTestName(): string {
    const timestamp = Date.now();
    return `Test Customer ${timestamp}`;
  }

  /**
   * Delete order by ID via API
   */
  async deleteOrder(orderId: string): Promise<boolean> {
    try {
      const response = await this.apiContext.delete(`${this.baseURL}/api/orders/${orderId}`);
      return response.status() === 200 || response.status() === 204;
    } catch (error) {
      console.error(`Failed to delete order ${orderId}:`, error);
      return false;
    }
  }

  /**
   * Delete orders by phone number via API
   */
  async deleteOrdersByPhone(phoneNumber: string): Promise<number> {
    try {
      const response = await this.apiContext.get(
        `${this.baseURL}/api/orders?phone=${encodeURIComponent(phoneNumber)}`
      );
      
      if (response.status() !== 200) return 0;
      
      const orders = await response.json();
      let deletedCount = 0;
      
      for (const order of orders) {
        if (await this.deleteOrder(order.id)) {
          deletedCount++;
        }
      }
      
      return deletedCount;
    } catch (error) {
      console.error(`Failed to delete orders for phone ${phoneNumber}:`, error);
      return 0;
    }
  }

  /**
   * Delete test customer account via API
   */
  async deleteCustomer(customerId: string): Promise<boolean> {
    try {
      const response = await this.apiContext.delete(`${this.baseURL}/api/customers/${customerId}`);
      return response.status() === 200 || response.status() === 204;
    } catch (error) {
      console.error(`Failed to delete customer ${customerId}:`, error);
      return false;
    }
  }

  /**
   * Cleanup test data after test execution
   */
  async cleanupTestData(testData: {
    orderId?: string;
    phoneNumber?: string;
    customerId?: string;
  }): Promise<void> {
    const cleanupResults = {
      ordersDeleted: 0,
      customerDeleted: false,
      errors: [] as string[]
    };

    // Delete specific order
    if (testData.orderId) {
      const deleted = await this.deleteOrder(testData.orderId);
      if (deleted) {
        cleanupResults.ordersDeleted++;
      } else {
        cleanupResults.errors.push(`Failed to delete order ${testData.orderId}`);
      }
    }

    // Delete all orders by phone number
    if (testData.phoneNumber) {
      const deleted = await this.deleteOrdersByPhone(testData.phoneNumber);
      cleanupResults.ordersDeleted += deleted;
    }

    // Delete customer account
    if (testData.customerId) {
      const deleted = await this.deleteCustomer(testData.customerId);
      cleanupResults.customerDeleted = deleted;
    }

    console.log('Test data cleanup completed:', cleanupResults);
  }

  /**
   * Verify no test data pollution exists
   */
  async verifyNoTestPollution(phoneNumber: string): Promise<boolean> {
    try {
      const response = await this.apiContext.get(
        `${this.baseURL}/api/orders?phone=${encodeURIComponent(phoneNumber)}`
      );
      
      if (response.status() !== 200) return true;
      
      const orders = await response.json();
      return orders.length === 0;
    } catch (error) {
      console.error('Failed to verify test pollution:', error);
      return false;
    }
  }

  /**
   * W3: Bulk cleanup — delete all orders for the test tenant.
   * Use afterAll() in test suites to prevent data accumulation.
   *
   * Note: AccountingEntries are NOT deleted (immutable by Domain design).
   * They remain as test tenant garbage, filtered out of production reports.
   *
   * @returns Number of orders deleted
   */
  async cleanupTestTenant(): Promise<number> {
    let deletedCount = 0;
    const errors: string[] = [];

    try {
      // Fetch all orders for the test tenant
      const response = await this.apiContext.get(
        `${this.baseURL}/api/orders?tenantId=${this.tenantId}&pageSize=100`,
        { headers: this.authHeaders }
      );

      if (response.status() !== 200) {
        console.warn(`[cleanupTestTenant] Failed to fetch orders: ${response.status()}`);
        return 0;
      }

      // W4 Fix: Handle non-JSON responses (e.g., HTML error pages when auth fails)
      const contentType = response.headers()['content-type'] || '';
      if (!contentType.includes('application/json')) {
        console.warn(`[cleanupTestTenant] Non-JSON response (content-type: ${contentType}). Skipping cleanup.`);
        return 0;
      }

      const data = await response.json();
      const orders = Array.isArray(data) ? data : (data.items || data.data || []);

      for (const order of orders) {
        const orderId = order.id || order.Id || order.orderId;
        if (orderId && await this.deleteOrder(orderId)) {
          deletedCount++;
        } else if (orderId) {
          errors.push(`Failed to delete order ${orderId}`);
        }
      }

      // Also clean up test customers (those with TEST prefix in phone)
      try {
        const customersResponse = await this.apiContext.get(
          `${this.baseURL}/api/customers?tenantId=${this.tenantId}&pageSize=100`
        );

        if (customersResponse.status() === 200) {
          const customersData = await customersResponse.json();
          const customers = Array.isArray(customersData) ? customersData : (customersData.items || customersData.data || []);

          for (const customer of customers) {
            const customerId = customer.id || customer.Id || customer.customerId;
            const phone = customer.phone || customer.Phone || '';
            // Only delete customers with TEST prefix (created by TestDataGenerator)
            if (customerId && phone.startsWith('TEST')) {
              await this.deleteCustomer(customerId);
            }
          }
        }
      } catch (err) {
        // Customer cleanup is best-effort — don't fail the whole cleanup
        console.warn('[cleanupTestTenant] Customer cleanup failed:', (err as Error).message);
      }

      if (errors.length > 0) {
        console.warn(`[cleanupTestTenant] ${errors.length} errors:`, errors.slice(0, 5));
      }

      console.log(`[cleanupTestTenant] Deleted ${deletedCount} orders for tenant ${this.tenantId}`);
      return deletedCount;
    } catch (error) {
      console.error('[cleanupTestTenant] Failed:', error);
      return 0;
    }
  }
}

/**
 * Test data generator for creating realistic test data
 */
export class TestDataGenerator {
  /**
   * Generate guest checkout data
   */
  static generateGuestData() {
    return {
      name: TestDataCleaner.generateTestName(),
      phone: TestDataCleaner.generateTestPhoneNumber(),
      address: `Test Address ${Date.now()}`,
      email: TestDataCleaner.generateTestEmail()
    };
  }

  /**
   * Generate order item data
   */
  static generateOrderItem(productId: string, quantity: number = 1) {
    return {
      productId,
      quantity,
      notes: `Test order item ${Date.now()}`
    };
  }
}