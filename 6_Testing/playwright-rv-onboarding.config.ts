import { defineConfig, devices } from '@playwright/test';

// Task P5 — Self-Serve Onboarding RV production.
// Domains: KL wizard diemthuong2 · admin app2 · API api2 → gateway 136.85.94.119.
// Spec tự tạo sysadmin storageState trong beforeAll (auth/rv-onboarding-admin.json).
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'merchant-onboarding.spec.ts',
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  timeout: 300000,
  expect: { timeout: 30000 },
  use: {
    baseURL: 'https://diemthuong2.khachvip.online',
    ignoreHTTPSErrors: true,
    screenshot: 'only-on-failure',
    launchOptions: {
      args: [
        '--host-resolver-rules=MAP diemthuong2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, MAP app2.khachvip.online 136.85.94.119, MAP api.khachvip.online 136.85.94.119'
      ]
    }
  },
  projects: [
    { name: 'rv-onboarding', use: { ...devices['Desktop Chrome'] } },
  ],
});
