import { defineConfig, devices } from '@playwright/test';

// Voice Service P2 — E2E WebRTC thật (fake media stream, không cần mic thật):
// 2 page (caller + callee) → demo SDK → voice API local (dotnet run) → P2P audio.
// Yêu cầu: .NET 8 SDK (webServer tự chạy voice API).
// Chạy (validation phase — Playwright chỉ chạy sau khi implement xong):
//   npx playwright test --config=playwright-voice-p2p.config.ts

const VOICE_SECRET = process.env.VOICE_E2E_SECRET || 'voice-e2e-secret-0123456789-abcdef-123456';

export default defineConfig({
  testDir: './e2e-tests',
  testMatch: 'voice-p2p.spec.ts',
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  timeout: 180000,
  expect: { timeout: 15000 },
  use: {
    baseURL: 'http://127.0.0.1:5080',
    ignoreHTTPSErrors: true,
    screenshot: 'only-on-failure',
    // NOTE: WebRTC ICE requires working UDP/loopback networking. Sandboxed/headless CI
    // environments that block UDP keep RTCPeerConnection at 'new' (verified: even a bare
    // same-page loopback never connects). Run this spec on a machine with normal networking.
    headless: true,
    launchOptions: {
      args: [
        '--use-fake-ui-for-media-stream',
        '--use-fake-device-for-media-stream',
        '--autoplay-policy=no-user-gesture-required',
        // Loopback ICE: allow the loopback interface as a candidate, keep host candidates
        // as real IPs (no mDNS obfuscation).
        '--allow-loopback-in-peer-connection',
        '--disable-features=WebRtcHideLocalIpsWithMdns'
      ]
    }
  },
  webServer: [
    {
      command: `dotnet run --project ../10_Voice/vanan-voice/src/VanAn.Voice.Api/VanAn.Voice.Api.csproj --urls http://127.0.0.1:5070 --no-launch-profile`,
      url: 'http://127.0.0.1:5070/v1/health',
      reuseExistingServer: false,
      timeout: 120000,
      env: {
        Jwt__Secret: VOICE_SECRET,
        Jwt__Issuer: 'VanAnShopERP',
        Jwt__Audience: 'VanAnApi',
        Voice__DbPath: `voice-e2e-${Date.now()}.db`,
        Voice__CorsOrigins: 'http://127.0.0.1:5080',
        // C3 (2026-11-11): PUT /v1/tenants/{id}/voice-config bị chặn mặc định — dev/E2E bật flag.
        Voice__AllowInsecureAdminApi: 'true'
      }
    },
    {
      command: 'node scripts/voice-demo-server.mjs',
      url: 'http://127.0.0.1:5080/demo/index.html',
      reuseExistingServer: false,
      timeout: 30000
    }
  ],
  projects: [
    { name: 'voice-p2p', use: { ...devices['Desktop Chrome'] } },
  ],
});