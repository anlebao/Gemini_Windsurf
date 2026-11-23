import { defineConfig, devices } from '@playwright/test';

// UNIVERSAL AI ASSISTANT W3d — RV production voice wiring /a/{slug} + embed.
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'assistant-voice.spec.ts',
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
    { name: 'rv-aivoice', use: { ...devices['Desktop Chrome'] } },
  ],
});
