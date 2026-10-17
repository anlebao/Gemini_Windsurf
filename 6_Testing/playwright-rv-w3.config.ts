import { defineConfig, devices } from '@playwright/test';

// RV W3 (2026-10-17, accounting-legal-compliance): HĐĐT Điều chỉnh/Thay thế (P0.3, Điều 10 TT 91/2026).
// Production qua app2.khachvip.online (ShopERP Blazor Server — gateway nginx proxy).
// - Project w3-einvoice: storageState auth/rv-w1-books.json (tái dùng auth W1/W2;
//   regenerate qua rv-w1-auth.cjs nếu cookie hết hạn sau redeploy).
// Spec skip gracefully khi tenant không có hóa đơn TaxApproved (case guard/modal vẫn kiểm được phần render).
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'einvoice-adjust-replace.spec.ts',
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
    { name: 'w3-einvoice', use: { ...devices['Desktop Chrome'], storageState: 'auth/rv-w1-books.json' } },
  ],
});
