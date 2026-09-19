import { defineConfig } from '@playwright/test';
import base from './playwright.config';

/**
 * Issue Batch #176-#181 — Batch 2 (2026-09-19): production RV config.
 *
 * The main playwright.config's global-setup waits for LOCAL services (5001-5003)
 * and injects an auth storageState — neither applies when RV-ing the deployed
 * production hosts directly (diemthuong2.khachvip.online / app2.khachvip.online).
 *
 * Usage:
 *   npx playwright test e2e-tests/rv-batch2.spec.ts --config=playwright-rv-batch2.config.ts
 */
export default defineConfig({
  ...base,
  globalSetup: undefined,
  projects: undefined, // drop tier projects — they re-add their own testMatch
  use: {
    ...base.use,
    storageState: undefined,
  },
  testMatch: ['e2e-tests/rv-batch2.spec.ts'],
});
