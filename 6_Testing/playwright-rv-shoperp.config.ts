import { defineConfig, devices } from '@playwright/test';

// RV 2026-10-08 (Feature 3 + issue #188): ShopERP UI flow production qua app2.khachvip.online.
// - storageState: auth/rv-booking.json (sysadmin impersonate Test HKD — rv-booking-auth.cjs)
// - host-resolver-rules: sandbox egress không tới CDN → map thẳng gateway IP (nginx giữ vhost).
// - ignoreHTTPSErrors: cert www2.khachvip.online cho app2 (hợp lệ trên DNS thật).
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: /(booking-shoperp-create|rv-shoperp)\.spec\.ts/,
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  timeout: 180000,
  expect: { timeout: 20000 },
  use: {
    baseURL: 'https://app2.khachvip.online',
    storageState: 'auth/rv-booking.json',
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
      name: 'rv-shoperp',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
