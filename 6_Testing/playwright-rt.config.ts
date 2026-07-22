import { defineConfig, devices } from '@playwright/test';

/**
 * RT (Runtime) Test Config — for live-site verification
 * No global-setup (no local service health checks), no auth state.
 * Use: npx playwright test --config=playwright-rt.config.ts
 */
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'khachlink-pwa-offline-rt.spec.ts',
  fullyParallel: false,
  retries: 0,
  reporter: [['list'], ['html', { outputFolder: 'reports/rt-report' }]],
  timeout: 120000,
  expect: { timeout: 15000 },
  use: {
    baseURL: 'https://diemthuong.khachvip.online',
    ignoreHTTPSErrors: true,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    actionTimeout: 20000,
    navigationTimeout: 60000,
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  outputDir: 'reports/rt-results/',
});
