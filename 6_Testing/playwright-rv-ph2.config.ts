import { defineConfig, devices } from '@playwright/test';

// Task PH2 — RV production config (app2 + host-resolver — pattern playwright-rv-voucher.config.ts).
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'ph2-voucher-coverage-rv.spec.ts',
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  timeout: 180000,
  expect: { timeout: 30000 },
  use: {
    baseURL: 'https://app2.khachvip.online',
    ignoreHTTPSErrors: true,
    launchOptions: {
      args: [
        '--host-resolver-rules=MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, MAP api.khachvip.online 136.85.94.119, MAP khachvip.online 136.85.94.119, MAP www2.khachvip.online 136.85.94.119'
      ]
    }
  },
  projects: [
    { name: 'rv-ph2', use: { ...devices['Desktop Chrome'] } },
  ],
});