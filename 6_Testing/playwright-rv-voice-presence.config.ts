import { defineConfig, devices } from '@playwright/test';

// Voice Presence — RV production. Cần VOICE_PRESENCE_STORE_URL env trỏ tới
// storefront có customer session hợp lệ (localStorage customer_token).
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'voice-presence.spec.ts',
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  timeout: 180000,
  expect: { timeout: 30000 },
  use: {
    ignoreHTTPSErrors: true,
    screenshot: 'only-on-failure',
    launchOptions: {
      args: [
        '--host-resolver-rules=MAP diemthuong2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, MAP app2.khachvip.online 136.85.94.119, MAP voice.khachvip.online 136.85.94.119'
      ]
    }
  },
  projects: [
    { name: 'rv-voice-presence', use: { ...devices['Desktop Chrome'] } },
  ],
});
