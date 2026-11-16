import { defineConfig, devices } from '@playwright/test';

// RV — ATRS Wave 2 security center (2026-11-16): prod KhachLink /security.
// Env: KHACHLINK_URL (default https://vanan.timlathay.com), SECURITY_CUSTOMER_TOKEN.
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'atrs-security-center.spec.ts',
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
