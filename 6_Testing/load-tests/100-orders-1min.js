/**
 * VanAn Ecosystem - 100 Orders in 1 Minute Performance Test
 * File Logging Enabled for Analysis
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

// Custom metrics for detailed analysis
const orderCreationRate = new Rate('order_creation_rate');
const responseTimeRate = new Rate('response_time_rate');
const errorRate = new Rate('error_rate');

// Test configuration
export const options = {
  stages: [
    { duration: '10s', target: 10 }, // Ramp up to 10 users in 10s
    { duration: '40s', target: 10 }, // Stay at 10 users for 40s (main test)
    { duration: '10s', target: 0 },  // Ramp down to 0 users in 10s
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'], // 95% of requests under 500ms
    http_req_failed: ['rate<0.1'],     // Error rate under 10%
    order_creation_rate: ['rate>0.9'],  // 90%+ order creation success
  },
};

// Base URL configuration
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5001';
const API_BASE = `${BASE_URL}/api`;

// Test data generation
function generateOrderData() {
  const products = [
    { id: 'coffee-black', name: 'Cà phê đen', price: 25000 },
    { id: 'coffee-milk', name: 'Cà phê sữa', price: 30000 },
    { id: 'tea-special', name: 'Trà đặc biệt', price: 20000 },
    { id: 'juice-fresh', name: 'Nước ép tươi', price: 35000 },
  ];
  
  const randomProduct = products[Math.floor(Math.random() * products.length)];
  const quantity = Math.floor(Math.random() * 3) + 1; // 1-3 items
  
  return {
    customerId: `test-customer-${Math.random().toString(36).substr(2, 9)}`,
    orderType: 'DINEIN',
    items: [{
      productId: randomProduct.id,
      productName: randomProduct.name,
      quantity: quantity,
      unitPrice: randomProduct.price,
      totalPrice: randomProduct.price * quantity
    }],
    totalAmount: randomProduct.price * quantity,
    notes: `Performance test order - ${new Date().toISOString()}`
  };
}

// Main test function
export default function () {
  const startTime = Date.now();
  
  try {
    // Step 1: Create order directly (no customer creation needed)
    const orderData = generateOrderData();
    
    const orderResponse = http.post(`${API_BASE}/orders`, JSON.stringify(orderData), {
      headers: {
        'Content-Type': 'application/json',
        'X-Tenant-Id': 'test-tenant-' + Math.floor(Math.random() * 10)
      },
    });
    
    const endTime = Date.now();
    const responseTime = endTime - startTime;
    
    // Metrics collection
    const orderSuccess = check(orderResponse, {
      'order created status is 200': (r) => r.status === 200,
      'order response time < 500ms': (r) => r.timings.duration < 500,
      'order has ID': (r) => r.json('id') !== undefined,
    });
    
    orderCreationRate.add(orderSuccess);
    responseTimeRate.add(responseTime < 500);
    errorRate.add(orderResponse.status >= 400);
    
    // Detailed logging for file output
    console.log(`ORDER_TEST: ${JSON.stringify({
      timestamp: new Date().toISOString(),
      orderId: orderResponse.json('id') || 'FAILED',
      responseTime: responseTime,
      status: orderResponse.status,
      success: orderSuccess,
      totalAmount: orderData.totalAmount,
      itemCount: orderData.items.length
    })}`);
    
    // Brief pause between requests
    sleep(0.1); // 100ms pause
    
  } catch (error) {
    errorRate.add(1);
    console.log(`ERROR_TEST: ${JSON.stringify({
      timestamp: new Date().toISOString(),
      error: error.message,
      stack: error.stack
    })}`);
  }
}

// Setup function
export function setup() {
  console.log(`SETUP: Starting 100 Orders in 1 Minute Test`);
  console.log(`SETUP: Base URL: ${BASE_URL}`);
  console.log(`SETUP: Test started at: ${new Date().toISOString()}`);
  console.log(`SETUP: Expected total orders: ~100 (10 users × 60s ÷ 6s per order)`);
}

// Teardown function
export function teardown(data) {
  console.log(`TEARDOWN: Test completed at: ${new Date().toISOString()}`);
  console.log(`TEARDOWN: Check k6 output for detailed metrics`);
  console.log(`TEARDOWN: Logs saved to console output and can be redirected to .txt file`);
}
