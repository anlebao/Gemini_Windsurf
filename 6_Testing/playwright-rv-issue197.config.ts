import { defineConfig, devices } from '@playwright/test';

// Issue #197 RV production — rate-card scope UI + master switch + topup tenant name.
// host-resolver map app2 → gateway IP (sandbox egress không tới CDN).
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'metering-issue197.spec.ts',
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  timeout: 120000,
  expect: { timeout: 30000 },
  use: {
    baseURL: 'https://app2.khachvip.online',
    ignoreHTTPSErrors: true,
    screenshot: 'only-on-failure',
    launchOptions: {
      args: [
        '--host-resolver-rules=MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, MAP api.khachvip.online 136.85.94.119, MAP khachvip.online 136.85.94.119, MAP www2.khachvip.online 136.85.94.119'
      ]
    }
  },
  projects: [
    { name: 'rv-issue197', use: { ...devices['Desktop Chrome'] } },
  ],
});