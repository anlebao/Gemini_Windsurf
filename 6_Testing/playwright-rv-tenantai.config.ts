import { defineConfig, devices } from '@playwright/test';

// Task AI-P6 — CP3 RV production: AI chat widget on pilot tenant An Tâm
// (flag AIChat_Enabled ON). Storefront on diemthuong2 → /api/ → gateway → vanan-ai.
// Requires AI_CHAT_TEST_STORE_URL env (full store URL with /store/by-id/<guid>).
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'ai-chat-widget.spec.ts',
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
    { name: 'rv-tenantai', use: { ...devices['Desktop Chrome'] } },
  ],
});
