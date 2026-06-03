import { test, expect } from '@playwright/test';
import { loadEnvConfig } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

const reporter = new TestReporter('Smoke Tests');

test.describe('VanAn Ecosystem - Smoke Tests', () => {
  const config = loadEnvConfig();
  
  test.beforeAll(async () => {
    reporter.log('Starting Smoke Tests...');
    reporter.log(`Test Environment: ${config.TEST_ENVIRONMENT}`);
  });

  test('CoreHub Health Check - Port 5010', async ({ request }) => {
    const startTime = Date.now();
    
    try {
      const response = await request.get(`${config.COREHUB_URL}/health`, {
        timeout: config.SMOKE_TEST_TIMEOUT * 1000
      });
      
      const responseTime = Date.now() - startTime;
      
      expect(response.status()).toBe(200);
      expect(responseTime).toBeLessThan(config.SMOKE_TEST_TIMEOUT * 1000);
      
      reporter.pass('CoreHub Health Check', {
        url: config.COREHUB_URL,
        status: response.status(),
        responseTime: `${responseTime}ms`
      });
      
    } catch (error) {
      reporter.fail('CoreHub Health Check', {
        url: config.COREHUB_URL,
        error: error.message
      });
      throw error;
    }
  });

  test('Gateway Health Check - Port 5001', async ({ request }) => {
    const startTime = Date.now();
    
    try {
      const response = await request.get(`${config.GATEWAY_URL}/health`, {
        timeout: config.SMOKE_TEST_TIMEOUT * 1000
      });
      
      const responseTime = Date.now() - startTime;
      
      expect(response.status()).toBe(200);
      expect(responseTime).toBeLessThan(config.SMOKE_TEST_TIMEOUT * 1000);
      
      reporter.pass('Gateway Health Check', {
        url: config.GATEWAY_URL,
        status: response.status(),
        responseTime: `${responseTime}ms`
      });
      
    } catch (error) {
      reporter.fail('Gateway Health Check', {
        url: config.GATEWAY_URL,
        error: error.message
      });
      throw error;
    }
  });

  test('KhachLink Health Check - Port 5002', async ({ request }) => {
    const startTime = Date.now();
    
    try {
      const response = await request.get(`${config.KHACHLINK_URL}/health`, {
        timeout: config.SMOKE_TEST_TIMEOUT * 1000
      });
      
      const responseTime = Date.now() - startTime;
      
      expect(response.status()).toBe(200);
      expect(responseTime).toBeLessThan(config.SMOKE_TEST_TIMEOUT * 1000);
      
      reporter.pass('KhachLink Health Check', {
        url: config.KHACHLINK_URL,
        status: response.status(),
        responseTime: `${responseTime}ms`
      });
      
    } catch (error) {
      reporter.fail('KhachLink Health Check', {
        url: config.KHACHLINK_URL,
        error: error.message
      });
      throw error;
    }
  });

  test('ShopERP Health Check - Port 5003', async ({ request }) => {
    const startTime = Date.now();
    
    try {
      const response = await request.get(`${config.SHOPERP_URL}/health`, {
        timeout: config.SMOKE_TEST_TIMEOUT * 1000
      });
      
      const responseTime = Date.now() - startTime;
      
      expect(response.status()).toBe(200);
      expect(responseTime).toBeLessThan(config.SMOKE_TEST_TIMEOUT * 1000);
      
      reporter.pass('ShopERP Health Check', {
        url: config.SHOPERP_URL,
        status: response.status(),
        responseTime: `${responseTime}ms`
      });
      
    } catch (error) {
      reporter.fail('ShopERP Health Check', {
        url: config.SHOPERP_URL,
        error: error.message
      });
      throw error;
    }
  });

  test('Database Connectivity Check', async ({ request }) => {
    // Basic database connectivity check through API
    try {
      const response = await request.get(`${config.COREHUB_URL}/api/health/database`);
      const data = await response.json();
      
      expect(data.status).toBe('healthy');
      expect(data.database).toBe('connected');
      
      reporter.pass('Database Connectivity', {
        status: data.status,
        database: data.database
      });
      
    } catch (error) {
      reporter.fail('Database Connectivity', {
        error: error.message
      });
      throw error;
    }
  });

  test('NATS Messaging Check', async ({ request }) => {
    // Check NATS connectivity through API
    try {
      const response = await request.get(`${config.COREHUB_URL}/api/health/messaging`);
      const data = await response.json();
      
      expect(data.status).toBe('healthy');
      expect(data.nats).toBe('connected');
      
      reporter.pass('NATS Messaging', {
        status: data.status,
        nats: data.nats
      });
      
    } catch (error) {
      reporter.fail('NATS Messaging', {
        error: error.message
      });
      throw error;
    }
  });

  test.afterAll(async () => {
    reporter.log('Smoke Tests Completed');
    await reporter.generateReport();
  });
});
