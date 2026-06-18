import { chromium, FullConfig } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

// T-16 FIX: ShopERP uses OpenID Connect redirect — there is no direct /login page
// with #username/#password form. The old approach would timeout waiting for
// page.waitForURL('/') which never resolves via OIDC redirect flow.
//
// Fix: Write an empty storageState (no cookies / no origins).
// Individual spec files that need auth use their own beforeEach login flow
// with test.use({ storageState: { cookies: [], origins: [] } }).
// This global-setup only ensures auth/admin.json exists so
// playwright.config.ts storageState reference does not crash at startup.

async function globalSetup(_config: FullConfig) {
  // Ensure auth directory exists
  const authDir = path.join(__dirname, 'auth');
  if (!fs.existsSync(authDir)) {
    fs.mkdirSync(authDir, { recursive: true });
  }

  // Write empty storageState — each spec manages its own auth via beforeEach
  const emptyState = { cookies: [], origins: [] };
  fs.writeFileSync(
    path.join(authDir, 'admin.json'),
    JSON.stringify(emptyState, null, 2)
  );

  // Smoke-check: verify ShopERP is reachable (optional, non-blocking)
  const shoperp = process.env.SHOPERP_URL ?? 'http://localhost:5003';
  try {
    const browser = await chromium.launch();
    const page = await browser.newPage();
    await page.goto(shoperp, { timeout: 10000, waitUntil: 'domcontentloaded' });
    await browser.close();
  } catch {
    // ShopERP not running in this environment — E2E tests will be skipped
    // via isTierEnabled('e2e') check inside each spec
  }
}

export default globalSetup;
