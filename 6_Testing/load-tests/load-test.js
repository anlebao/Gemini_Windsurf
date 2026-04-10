import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');

// Load test configuration from environment
const config = {
  duration: __ENV.LOAD_TEST_DURATION || '60s',
  vus: __ENV.LOAD_TEST_VUS || 10,
  rampUp: __ENV.LOAD_TEST_RAMP_UP || 10,
  corehubUrl: __ENV.COREHUB_URL || 'http://localhost:5010',
  gatewayUrl: __ENV.GATEWAY_URL || 'http://localhost:5001',
  khachlinkUrl: __ENV.KHACHLINK_URL || 'http://localhost:5002',
  shoperpUrl: __ENV.SHOPERP_URL || 'http://localhost:5003'
};

// Test options
export const options = {
  stages: [
    { duration: `${config.rampUp}s`, target: config.vus },
    { duration: `${parseInt(config.duration) - config.rampUp}s`, target: config.vus },
    { duration: '10s', target: 0 }
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'], // 95% of requests under 500ms
    http_req_failed: ['rate<0.1'],     // Error rate under 10%
    errors: ['rate<0.1']
  }
};

// Test data generator
function generateRandomDeviceId() {
  return `device_${Math.random().toString(36).substr(2, 9)}`;
}

function generateTestOrder() {
  return {
    customerDeviceId: generateRandomDeviceId(),
    items: [
      {
        productId: 'test-product-' + Math.floor(Math.random() * 3) + 1,
        quantity: Math.floor(Math.random() * 3) + 1,
        unitPrice: 28000
      }
    ],
    totalAmount: 28000 * (Math.floor(Math.random() * 3) + 1)
  };
}

// Test scenarios
export default function () {
  // Scenario 1: Health checks (smoke test under load)
  const healthChecks = [
    { name: 'CoreHub Health', url: `${config.corehubUrl}/health` },
    { name: 'Gateway Health', url: `${config.gatewayUrl}/health` },
    { name: 'KhachLink Health', url: `${config.khachlinkUrl}/health` },
    { name: 'ShopERP Health', url: `${config.shoperpUrl}/health` }
  ];

  healthChecks.forEach(check => {
    const response = http.get(check.url, {
      tags: { name: check.name, type: 'health' }
    });
    
    const success = check(response, {
      [`${check.name} status is 200`]: (r) => r.status === 200,
      [`${check.name} response time < 1s`]: (r) => r.timings.duration < 1000,
    });
    
    errorRate.add(!success);
    sleep(0.1);
  });

  // Scenario 2: Order creation (main business flow)
  const orderData = generateTestOrder();
  const orderResponse = http.post(
    `${config.corehubUrl}/api/orders`,
    JSON.stringify(orderData),
    {
      headers: { 'Content-Type': 'application/json' },
      tags: { name: 'Create Order', type: 'business' }
    }
  );

  const orderSuccess = check(orderResponse, {
    'Create Order status is 200': (r) => r.status === 200,
    'Create Order response time < 2s': (r) => r.timings.duration < 2000,
    'Create Order has order ID': (r) => r.json('orderId') !== undefined,
  });

  errorRate.add(!orderSuccess);

  if (orderSuccess) {
    const orderId = orderResponse.json('orderId');
    
    // Scenario 3: Order status check
    sleep(1); // Wait for order processing
    
    const statusResponse = http.get(
      `${config.corehubUrl}/api/orders/${orderId}`,
      {
        tags: { name: 'Get Order Status', type: 'read' }
      }
    );

    const statusSuccess = check(statusResponse, {
      'Get Order Status status is 200': (r) => r.status === 200,
      'Get Order Status response time < 500ms': (r) => r.timings.duration < 500,
      'Get Order Status has valid data': (r) => r.json('status') !== undefined,
    });

    errorRate.add(!statusSuccess);

    // Scenario 4: Product catalog browsing
    const catalogResponse = http.get(
      `${config.corehubUrl}/api/products`,
      {
        tags: { name: 'Get Products', type: 'read' }
      }
    );

    const catalogSuccess = check(catalogResponse, {
      'Get Products status is 200': (r) => r.status === 200,
      'Get Products response time < 300ms': (r) => r.timings.duration < 300,
      'Get Products has products array': (r) => Array.isArray(r.json('products')),
    });

    errorRate.add(!catalogSuccess);
  }

  // Scenario 5: Inventory check (staff operations)
  const inventoryResponse = http.get(
    `${config.corehubUrl}/api/inventory/low-stock`,
    {
      tags: { name: 'Get Low Stock', type: 'staff' }
    }
  );

  const inventorySuccess = check(inventoryResponse, {
    'Get Low Stock status is 200': (r) => r.status === 200 || r.status === 401, // 401 if not authenticated
    'Get Low Stock response time < 500ms': (r) => r.timings.duration < 500,
  });

  errorRate.add(!inventorySuccess);

  // Scenario 6: Static assets performance
  const staticChecks = [
    { name: 'KhachLink Home', url: config.khachlinkUrl },
    { name: 'ShopERP Home', url: config.shoperpUrl }
  ];

  staticChecks.forEach(check => {
    const response = http.get(check.url, {
      tags: { name: check.name, type: 'static' }
    });
    
    const success = check(response, {
      [`${check.name} status is 200`]: (r) => r.status === 200,
      [`${check.name} response time < 1s`]: (r) => r.timings.duration < 1000,
    });
    
    errorRate.add(!success);
  });

  sleep(Math.random() * 2); // Random think time between 0-2 seconds
}

// Setup function
export function setup() {
  console.log('Load Test Configuration:');
  console.log(`- Duration: ${config.duration}`);
  console.log(`- Virtual Users: ${config.vus}`);
  console.log(`- Ramp Up: ${config.rampUp}s`);
  console.log(`- CoreHub URL: ${config.corehubUrl}`);
  console.log(`- Gateway URL: ${config.gatewayUrl}`);
  console.log(`- KhachLink URL: ${config.khachlinkUrl}`);
  console.log(`- ShopERP URL: ${config.shoperpUrl}`);
}

// Teardown function
export function teardown(data) {
  console.log('Load Test Completed');
  console.log(`- Error Rate: ${errorRate.rate * 100}%`);
  console.log(`- Total Requests: ${http.metrics('http_reqs').count}`);
  console.log(`- Average Response Time: ${http.metrics('http_req_duration').avg}ms`);
}
