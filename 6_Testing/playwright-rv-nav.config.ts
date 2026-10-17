import { defineConfig, devices } from '@playwright/test';

// RV — Nav "Kế Toán" nhóm con (2026-10-17): Sổ Sách / BCTC / BC Quản Trị / Sổ HKD.
// Production qua app2.khachvip.online (ShopERP Blazor Server — gateway nginx proxy).
// - Project nav-dn: storageState auth/rv-w1-books.json (Vạn An Trading DN — tái dùng auth W1;
//   regenerate qua rv-w1-auth.cjs nếu cookie hết hạn sau redeploy).
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'accounting-nav-groups-rv.spec.ts',
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
    { name: 'nav-dn', use: { ...devices['Desktop Chrome'], storageState: 'auth/rv-w1-books.json' } },
  ],
});
