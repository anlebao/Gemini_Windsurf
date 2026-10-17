import { defineConfig, devices } from '@playwright/test';

// RV W4 (2026-10-18, accounting-legal-compliance): audit IP/UA (P1.1) + reopen gate (P1.2) + balance alerts (P1.3).
// Production qua app2.khachvip.online (ShopERP Blazor Server — gateway nginx proxy).
// - Project w4-audit: không storageState — spec tự login sysadmin (SystemAdmin policy cho /admin/audit-trail).
// - Project w4-books: storageState auth/rv-w1-books.json (Owner Vạn An Trading a5b6c7d8;
//   regenerate qua rv-w1-auth.cjs nếu cookie hết hạn sau redeploy).
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'accounting-legal-w4-rv.spec.ts',
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  timeout: 240000,
  expect: { timeout: 25000 },
  use: {
    baseURL: 'https://app2.khachvip.online',
    ignoreHTTPSErrors: true,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    launchOptions: {
      args: [
        '--host-resolver-rules=MAP app2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119, MAP api.khachvip.online 136.85.94.119, MAP khachvip.online 136.85.94.119, MAP www2.khachvip.online 136.85.94.119',
      ],
    },
  },
  projects: [
    { name: 'w4-audit', use: { ...devices['Desktop Chrome'] } },
    { name: 'w4-books', use: { ...devices['Desktop Chrome'], storageState: 'auth/rv-w1-books.json' } },
  ],
});
