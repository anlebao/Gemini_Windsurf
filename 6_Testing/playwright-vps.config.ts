import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'vps-shoperp-ui-flow.spec.ts',
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  timeout: 120000,
  expect: { timeout: 15000 },
  use: {
    baseURL: 'https://app.khachvip.online',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    ignoreHTTPSErrors: true,
  },
  projects: [
    {
      name: 'vps-tests',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
