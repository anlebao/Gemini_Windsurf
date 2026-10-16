import { defineConfig, devices } from '@playwright/test';

// RV W1 (2026-10-16, accounting-legal-compliance): Sổ Nhật ký chung + Sổ Cái + kiểm soát ≥20 triệu.
// Production qua app2.khachvip.online (ShopERP Blazor Server — gateway nginx proxy).
// - Project w1-books: storageState auth/rv-w1-books.json (Vạn An Trading DN — Journal/Ledger có data)
// - Project w1-pos:   storageState auth/rv-w1-pos.json (Hoàng Nguyên — POS ≥20tr + review report)
// - host-resolver-rules: map app2 → gateway IP (sandbox egress không tới CDN thật).
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'accounting-legal-w1-rv.spec.ts',
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
    { name: 'w1-books', use: { ...devices['Desktop Chrome'], storageState: 'auth/rv-w1-books.json' } },
    { name: 'w1-pos', use: { ...devices['Desktop Chrome'], storageState: 'auth/rv-w1-pos.json' } },
  ],
});