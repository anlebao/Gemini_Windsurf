import { defineConfig, devices } from '@playwright/test';

// UNIVERSAL AI ASSISTANT W1 — RV production /a/{slug} hosted page.
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'assistant-ui-polish.spec.ts',
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  timeout: 180000,
  expect: { timeout: 30000 },
  use: {
    baseURL: 'https://shopai.timlathay.com',
    ignoreHTTPSErrors: true,
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'rv-ailink', use: { ...devices['Desktop Chrome'] } },
  ],
});
