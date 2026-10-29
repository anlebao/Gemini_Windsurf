import { defineConfig, devices } from '@playwright/test';

// RV 2026-10-29 (issue #195 booking-payment): production app2, commit 243112db.
// storageState: auth/rv-bp-hkd.json (sysadmin impersonate Test HKD — tạo bằng node rv-bp-auth.cjs).
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: /booking-payment-rv\.spec\.ts/,
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  timeout: 180000,
  expect: { timeout: 20000 },
  use: {
    baseURL: 'https://app2.khachvip.online',
    storageState: 'auth/rv-bp-hkd.json',
    ignoreHTTPSErrors: true,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    launchOptions: {
      args: [
        '--host-resolver-rules=MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, MAP api.khachvip.online 136.85.94.119, MAP khachvip.online 136.85.94.119, MAP www2.khachvip.online 136.85.94.119'
      ]
    }
  },
  projects: [
    {
      name: 'rv-bp',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});