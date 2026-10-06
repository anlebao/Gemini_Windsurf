import { defineConfig, devices } from '@playwright/test';

// Booking P6/P7 RV (2026-10-06): booking E2E specs chạy production.
// - KhachLink PWA: https://diemthuong2.khachvip.online (gateway nginx → KhachLink VPS)
// - Gateway API:   https://api2.khachvip.online (public + tenant booking API)
// - Env: BOOKING_TEST_* từ seed RV (rv-booking.env — KHÔNG commit)
// - ignoreHTTPSErrors: cert SAN www2.khachvip.online — hợp lệ trên DNS thật.
export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'booking-*.spec.ts',
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  timeout: 180000,
  expect: { timeout: 20000 },
  use: {
    baseURL: 'https://diemthuong2.khachvip.online',
    ignoreHTTPSErrors: true,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    launchOptions: {
      args: [
        '--host-resolver-rules=MAP diemthuong2.khachvip.online 136.85.94.119, MAP api2.khachvip.online 136.85.94.119'
      ]
    }
  },
  projects: [
    {
      name: 'rv-booking',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
