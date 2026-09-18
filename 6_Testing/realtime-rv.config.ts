import { defineConfig } from '@playwright/test';
import base from './playwright.config';

/**
 * Realtime Platform P6 (2026-09-18): production RV config for the realtime specs.
 *
 * The main playwright.config's global-setup waits for LOCAL services (5001-5003) and
 * injects an auth storageState — neither applies when RV-ing the deployed production
 * hosts directly. This config keeps every other option (reporters, timeouts, retries)
 * and only drops globalSetup + storageState.
 *
 * Usage:
 *   npx playwright test e2e-tests/realtime-shop-chat.spec.ts --config=realtime-rv.config.ts
 */
export default defineConfig({
  ...base,
  globalSetup: undefined,
  projects: undefined, // tier projects re-add their own testMatch — drop them for RV
  use: {
    ...base.use,
    storageState: undefined,
  },
  testMatch: ['e2e-tests/realtime-shop-chat.spec.ts', 'e2e-tests/realtime-tracking.spec.ts', 'e2e-tests/rv-realtime-order-ui.spec.ts'],
});
