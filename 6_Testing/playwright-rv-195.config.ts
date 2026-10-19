import { defineConfig, devices } from '@playwright/test';

// RV 2026-10-19 (issue #195): Order management upgrade trên production app2.khachvip.online.
// - storageState mặc định: auth/rv-195-owner.json (sysadmin impersonate Test HKD)
// - admin test dùng auth/rv-195-admin.json (test.use trong spec)
// - Tạo state: node rv-195-auth.cjs
// - host-resolver-rules: sandbox egress không tới CDN → map thẳng gateway IP.
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: /orders-management-195-rv\.spec\.ts/,
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  timeout: 180000,
  expect: { timeout: 20000 },
  use: {
    baseURL: 'https://app2.khachvip.online',
    storageState: 'auth/rv-195-owner.json',
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
      name: 'rv-195',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
