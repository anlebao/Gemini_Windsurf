import { defineConfig, devices } from '@playwright/test';

// RV 2026-10-29 (issue #195 follow-up): production app2.khachvip.online, commit d98fd639.
// - storageState mặc định: auth/rv-195b-quy.json (sysadmin impersonate Cafe Tân Quy)
// - admin test dùng auth/rv-195b-admin.json (test.use trong spec)
// - Tạo state: node rv-195b-auth.cjs
// - host-resolver-rules: sandbox egress không tới CDN → map thẳng gateway IP.
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: /rv-195-followup\.spec\.ts/,
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  timeout: 180000,
  expect: { timeout: 20000 },
  use: {
    baseURL: 'https://app2.khachvip.online',
    storageState: 'auth/rv-195b-quy.json',
    ignoreHTTPSErrors: true,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    launchOptions: {
      args: [
        '--host-resolver-rules=MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, MAP api.khachvip.online 136.85.94.119, MAP khachvip.online 136.85.94.119, MAP www2.khachvip.online 136.85.94.119'
      ]
    }
  },
  projects: [
    {
      name: 'rv-195b',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});