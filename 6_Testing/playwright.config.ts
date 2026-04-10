import { defineConfig, devices } from '@playwright/test';
import { loadEnvConfig } from './utils/env-config';

const config = loadEnvConfig();

export default defineConfig({
  testDir: './',
  testMatch: '**/*.spec.ts',
  fullyParallel: config.E2E_TEST_PARALLEL,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: [
    ['html', { outputFolder: 'reports/playwright-html-report' }],
    ['json', { outputFile: 'reports/playwright-report.json' }],
    ['junit', { outputFile: 'reports/playwright-junit.xml' }],
    ['./utils/custom-reporter.ts']
  ],
  globalSetup: './utils/global-setup.ts',
  globalTeardown: './utils/global-teardown.ts',
  
  timeout: config.E2E_TEST_TIMEOUT * 1000,
  expect: {
    timeout: 10000
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
        baseURL: config.KHACHLINK_URL,
        trace: 'on-first-retry',
        screenshot: 'only-on-failure',
        video: 'retain-on-failure'
      }
    }
  ],

  use: {
    baseURL: config.COREHUB_URL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    actionTimeout: 15000,
    navigationTimeout: 30000
  },

  projects: [
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
