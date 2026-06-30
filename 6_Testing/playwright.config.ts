import { defineConfig, devices } from '@playwright/test';
import { loadEnvConfig } from './utils/env-config';

const config = loadEnvConfig();

export default defineConfig({
  testDir: './',
  globalSetup: './global-setup',
  testMatch: '**/*.spec.ts',
  fullyParallel: config.E2E_TEST_PARALLEL,
  forbidOnly: !!process.env.CI,
  // Retry logic for network flakiness: 2 retries in CI, 1 locally
  retries: process.env.CI ? 2 : 1,
  // Wave 5: Optimize CI parallelization - use 4 workers in CI for faster execution
  workers: process.env.CI ? 4 : undefined,
  reporter: [
    ['html', { outputFolder: 'reports/playwright-html-report' }],
    ['json', { outputFile: 'reports/playwright-report.json' }],
    ['junit', { outputFile: 'reports/playwright-junit.xml' }]
  ],
  
  timeout: config.E2E_TEST_TIMEOUT * 1000,
  expect: {
    timeout: 10000
  },

  use: {
    baseURL: config.SHOPERP_URL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    actionTimeout: 15000,
    navigationTimeout: 30000,
    storageState: 'auth/admin.json'
  },

  projects: [
    {
      name: 'smoke-tests',
      testMatch: 'smoke-tests/**/*.spec.ts',
      use: {
        baseURL: config.COREHUB_URL,
        trace: 'on-first-retry',
        screenshot: 'only-on-failure',
        video: 'retain-on-failure'
      }
    },
    {
      name: 'e2e-tests',
      testMatch: 'e2e-tests/**/*.spec.ts',
      use: {
        baseURL: config.SHOPERP_URL,
        trace: 'on-first-retry',
        screenshot: 'only-on-failure',
        video: 'retain-on-failure',
        storageState: 'auth/admin.json'
      }
    },
    {
      name: 'accounting-e2e',
      testMatch: 'e2e-tests/accounting*.spec.ts',
      use: {
        baseURL: config.SHOPERP_URL,
        trace: 'on-first-retry',
        screenshot: 'only-on-failure',
        video: 'retain-on-failure',
        storageState: 'auth/admin.json'
      }
    },
    {
      name: 'omnichannel-e2e',
      testMatch: 'e2e-tests/omnichannel*.spec.ts',
      use: {
        baseURL: config.OMNICHANNEL_URL,
        trace: 'on-first-retry',
        screenshot: 'only-on-failure',
        video: 'retain-on-failure',
        // HTTP Basic Auth credentials if staging site is protected
        // httpCredentials: {
        //   username: process.env.STAGING_USERNAME || '',
        //   password: process.env.STAGING_PASSWORD || ''
        // }
      }
    },
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'firefox',
      use: { ...devices['Desktop Firefox'] },
    },
    {
      name: 'webkit',
      use: { ...devices['Desktop Safari'] },
    },
    {
      name: 'Mobile Chrome',
      use: { ...devices['Pixel 5'] },
    },
    {
      name: 'Mobile Safari',
      use: { ...devices['iPhone 12'] },
    },
  ],

  webServer: {
    command: 'echo "No web server - testing against running services"',
    port: 0,
    reuseExistingServer: !process.env.CI,
    timeout: 120 * 1000,
  },

  outputDir: 'reports/test-results/',
});
