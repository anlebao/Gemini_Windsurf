import { defineConfig, devices } from '@playwright/test';

// RV — ATRS Wave 5 W5a recovery admin (2026-11-17): prod ShopERP /admin/recovery + /admin/atrs.
// Env: GATEWAY_URL, SHOPERP_URL, RECOVERY_STAFF_TOKEN (authenticated checks).
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'atrs-recovery-admin.spec.ts',
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