import { expect, Browser } from '@playwright/test';
import { loadEnvConfig } from '../../utils/env-config';

const config = loadEnvConfig();

/**
 * Production VPS auth helper — Phase 6 RV tests.
 * Login as SystemAdmin + impersonate tenant to get Owner-scoped auth cookies.
 * Returns a browser context + page with auth cookies pre-set.
 *
 * Pattern: use BrowserContext.request (shares cookies with browser pages)
 * to do API login, then navigate to UI pages — cookies are already set.
 */
export async function createAuthenticatedPage(browser: Browser, tenantId: string = '00000000-0000-0000-0000-000000000001') {
  const sysadminUser = 'sysadmin@vanan.vn';
  const sysadminPass = '2026@vanan';

  const context = await browser.newContext({
    baseURL: config.SHOPERP_URL,
    ignoreHTTPSErrors: true,
  });

  // Step 1: Platform login via API (sets auth cookie in context)
  const loginResp = await context.request.post(`${config.SHOPERP_URL}/api/platform/login`, {
    data: { Username: sysadminUser, Password: sysadminPass },
    headers: { 'Content-Type': 'application/json' },
  });
  expect(loginResp.ok(), `Platform login should succeed, got ${loginResp.status()}`).toBeTruthy();

  // Step 2: Impersonate tenant (sets tenant cookie in context)
  const impResp = await context.request.post(`${config.SHOPERP_URL}/api/admin/impersonate/${tenantId}`);
  expect(impResp.ok(), `Impersonate should succeed, got ${impResp.status()}`).toBeTruthy();

  const page = await context.newPage();
  return { page, context };
}
