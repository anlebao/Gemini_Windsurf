import { defineConfig, devices } from '@playwright/test';

// RV — W3-2c search UX pattern (2026-11-15): timlathay.com Directory prod.
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'w3-2c-search-ux.spec.ts',
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  timeout: 180000,
  expect: { timeout: 30000 },
  use: {
    ignoreHTTPSErrors: true,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
});
