import { defineConfig } from '@playwright/test';
import base from './playwright.config';

/**
 * Loyalty Points Integrity — Batch 4 (2026-09-21): production RV config.
 *
 * Same rationale as playwright-rv-batch3.config.ts — the main config's global-setup
 * waits for LOCAL services + injects an auth storageState; RV targets the deployed
 * production hosts directly (diemthuong2.khachvip.online).
 *
 * Usage:
 *   npx playwright test e2e-tests/rv-batch4.spec.ts --config=playwright-rv-batch4.config.ts
 */
export default defineConfig({
  ...base,
  globalSetup: undefined,
  projects: undefined, // drop tier projects — they re-add their own testMatch
  use: {
    ...base.use,
    storageState: undefined,
  },
  testMatch: ['e2e-tests/rv-batch4.spec.ts'],
});
