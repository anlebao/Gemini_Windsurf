import { defineConfig, devices } from '@playwright/test';

// Storefront metering deny gate RV production — env STORE_GATE_TEST_STORE_URL.
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'storefront-metering-gate.spec.ts',
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  timeout: 120000,
  expect: { timeout: 30000 },
  use: {
    ignoreHTTPSErrors: true,
    screenshot: 'only-on-failure',
    launchOptions: {
      args: [
        '--host-resolver-rules=MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, MAP api.khachvip.online 136.85.94.119, MAP khachvip.online 136.85.94.119, MAP www2.khachvip.online 136.85.94.119'
      ]
    }
  },
  projects: [
    { name: 'rv-storegate', use: { ...devices['Desktop Chrome'] } },
  ],
});
