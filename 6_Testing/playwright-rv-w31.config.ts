import { defineConfig, devices } from '@playwright/test';

// RV — W3-1 contextual AI/voice entries (2026-11-14): prod public surfaces
// (hoptacxa portal + member storefronts). Guest, absolute URLs in spec.
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'contextual-ai-entries.spec.ts',
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
