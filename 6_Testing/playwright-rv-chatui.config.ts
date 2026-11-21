import { defineConfig, devices } from '@playwright/test';

// Chat UI Unification P0 — RV production trên pilot tenant An Tâm
// (AIChat_Enabled ON + chat identity). Cần AI_CHAT_TEST_STORE_URL env.
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'chat-ui-unification.spec.ts',
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  timeout: 180000,
  expect: { timeout: 30000 },
  use: {
    baseURL: 'https://diemthuong2.khachvip.online',
    ignoreHTTPSErrors: true,
    screenshot: 'only-on-failure',
    launchOptions: {
      args: [
        '--host-resolver-rules=MAP diemthuong2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, MAP app2.khachvip.online 136.85.94.119'
      ]
    }
  },
  projects: [
    { name: 'rv-chatui', use: { ...devices['Desktop Chrome'] } },
  ],
});
