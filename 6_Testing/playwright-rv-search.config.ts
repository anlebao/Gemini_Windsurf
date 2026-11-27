import { defineConfig } from '@playwright/test';
import base from './playwright.config';

/**
 * Search Unification v2 — production RV config (2026-11).
 * Same rationale as playwright-rv-batch4.config.ts — main config's global-setup
 * waits for LOCAL services; RV targets deployed prod hosts
 * (timlathay.com / commienphi.timlathay.com / hoptacxa.timlathay.com).
 *
 * Usage:
 *   npx playwright test e2e-tests/search-unification.spec.ts --config=playwright-rv-search.config.ts
 */
export default defineConfig({
  ...base,
  globalSetup: undefined,
  projects: undefined, // drop tier projects — they re-add their own testMatch
  use: {
    ...base.use,
    storageState: undefined,
  },
  testMatch: ['e2e-tests/search-unification.spec.ts'],
});
