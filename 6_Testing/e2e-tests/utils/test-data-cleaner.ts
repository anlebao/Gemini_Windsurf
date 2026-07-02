import { Page, APIRequestContext } from '@playwright/test';

/**
 * Test Data Cleaner Utility
 * Handles cleanup of test data to prevent pollution of production-like states
 */
export class TestDataCleaner {
  private readonly apiContext: APIRequestContext;
  private readonly baseURL: string;

  constructor(apiContext: APIRequestContext, baseURL: string) {
    this.apiContext = apiContext;
    this.baseURL = baseURL;
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