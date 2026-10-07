import { defineConfig, devices } from '@playwright/test';

// THU CHI & CÔNG NỢ MVP P4 RV (2026-10-07): cong-no E2E chạy production qua app2.khachvip.online
// (ShopERP Blazor Server — gateway nginx proxy thẳng 10.148.0.3).
// - storageState: auth/rv-congno.json (login thật Owner adminvanan1 — rv-congno-auth.cjs)
// - host-resolver-rules: sandbox egress không tới CDN 161.118.212.110 → map thẳng gateway IP
//   (nginx gateway vẫn route app2.khachvip.online đúng vhost — Host/SNI giữ nguyên).
// - ignoreHTTPSErrors: cert www2.khachvip.online cho app2 (hợp lệ trên DNS thật).
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'cong-no.spec.ts',
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  timeout: 180000,
  expect: { timeout: 20000 },
  use: {
    baseURL: 'https://app2.khachvip.online',
    storageState: 'auth/rv-congno.json',
    ignoreHTTPSErrors: true,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    launchOptions: {
      args: [
        '--host-resolver-rules=MAP app2.khachvip.online 136.85.94.119, MAP api.khachvip.online 136.85.94.119, MAP khachvip.online 136.85.94.119, MAP www2.khachvip.online 136.85.94.119'
      ]
    }
  },
  projects: [
    {
      name: 'rv-congno',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
