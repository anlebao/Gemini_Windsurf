import { chromium, FullConfig } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

// T-16 / T-20: ShopERP uses OpenID Connect — there is no direct /login page.
//
// Strategy:
//   1. Try POST /dev/login  (available when ASPNETCORE_ENVIRONMENT=Development).
//      This signs in via Cookie auth with a fixed TenantId + Owner role claim.
//      The resulting .VanAn.Auth cookie is stored in auth/admin.json and reused
//      by all e2e-tests specs via playwright.config.ts storageState.
//
//   2. Fallback: write empty storageState so Playwright doesn't crash on startup.
//      Tests that need auth will fail with a clear "not authenticated" message
//      rather than a cryptic "file not found" error.
//
// TenantId injected by DevLoginController: 11111111-1111-1111-1111-111111111111
// This matches the dev seed data in ShopERP SQLite DB.

async function globalSetup(_config: FullConfig) {
  // Ensure auth directory exists
  const authDir = path.join(__dirname, 'auth');
  if (!fs.existsSync(authDir)) {
    fs.mkdirSync(authDir, { recursive: true });
  }

  const shopErpUrl = process.env.SHOPERP_URL ?? 'http://localhost:5003';
  const devLoginUrl = `${shopErpUrl}/dev/login`;

  const browser = await chromium.launch();
  const context = await browser.newContext();
  const page    = await context.newPage();

  let loginSucceeded = false;

  try {
    // Step 1: Smoke-check ShopERP is running
    await page.goto(shopErpUrl, { timeout: 15000, waitUntil: 'domcontentloaded' });

    // Step 2: POST to /dev/login — issues real Cookie auth session with TenantId claim
    const response = await context.request.post(devLoginUrl, {
      timeout: 10000,
    });

    if (response.ok()) {
      const body = await response.json();
      console.log(`[global-setup] Dev login OK — tenantId=${body.tenantId}, role=${body.role}`);
      loginSucceeded = true;
    } else {
      console.warn(`[global-setup] /dev/login returned ${response.status()} — ShopERP may not be in Development mode`);
    }
  } catch (err) {
    console.warn(`[global-setup] ShopERP not reachable or /dev/login failed: ${(err as Error).message}`);
    console.warn('[global-setup] Writing empty storageState — E2E tests requiring auth will fail with clear errors');
  }

  // Step 3: Save storageState (cookies) to auth/admin.json
  const storageStatePath = path.join(authDir, 'admin.json');
  await context.storageState({ path: storageStatePath });

  if (loginSucceeded) {
    console.log(`[global-setup] auth/admin.json written with live session cookies`);
  } else {
    console.log(`[global-setup] auth/admin.json written as empty storageState (no session)`);
  }

  await browser.close();
}

export default globalSetup;
