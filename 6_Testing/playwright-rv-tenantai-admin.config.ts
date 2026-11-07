import { defineConfig, devices } from '@playwright/test';

// Task AI-P10 RV (2026-11-07): admin UI /admin/tenant-ai/* production qua app2.
// - storageState: auth/rv-tenantai-admin.json (sysadmin impersonate An Tâm — rv-tenantai-admin-auth.cjs)
// - host-resolver-rules: sandbox egress không tới CDN → map thẳng gateway IP (nginx giữ vhost).
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'tenant-ai-admin.spec.ts',
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  timeout: 180000,
  expect: { timeout: 30000 },
  use: {
    baseURL: 'https://app2.khachvip.online',
    storageState: 'auth/rv-tenantai-admin.json',
    ignoreHTTPSErrors: true,
    screenshot: 'only-on-failure',
    launchOptions: {
      args: [
        '--host-resolver-rules=MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, MAP api.khachvip.online 136.85.94.119, MAP khachvip.online 136.85.94.119, MAP www2.khachvip.online 136.85.94.119'
      ]
    }
  },
  projects: [
    { name: 'rv-tenantai-admin', use: { ...devices['Desktop Chrome'] } },
  ],
});
