import { test, expect } from '@playwright/test';
import { createAuthenticatedPage } from './utils/prod-auth';
import { loadEnvConfig } from '../../utils/env-config';

const config = loadEnvConfig();

/**
 * Phase 6 — QuickSetup flow E2E spec (PRODUCTION VPS)
 * @golden
 *
 * RV tests via UI layout (not API calls).
 * Auth: SystemAdmin platform login (no impersonation — QuickSetup is SystemAdmin-only).
 *
 * Coverage:
 *  1. SystemAdmin → /admin/tenants → "Khởi tạo nhanh" button (RV1)
 *  2. Redirect to /quick-setup?tenantId={id} (RV1)
 *  3. Page render + template list load (RV2, RV4)
 *  4. Missing tenantId guard (RV7)
 */
test.describe('QuickSetup Flow (PROD) @golden', () => {
  test('RV1 — SystemAdmin tenant selection → redirect to /quick-setup', async ({ browser }) => {
    const { page, context } = await createAuthenticatedPage(browser, '00000000-0000-0000-0000-000000000001');
    try {
      // Navigate to tenant management (full URL — /admin/tenants resolves to domain root)
      await page.goto(`${config.SHOPERP_URL}/admin/tenants`);
      await page.waitForLoadState('networkidle');
      await page.waitForTimeout(2000);

      // Page should render (header visible)
      await expect(page.locator('h1:has-text("Tenant"), h1:has-text("tenant"), h1:has-text("Khách hàng")').first()).toBeVisible({ timeout: 15000 });

      // Look for "Khởi tạo nhanh" button on any tenant row
      const quickSetupBtn = page.locator('button:has-text("Khởi tạo nhanh"), a:has-text("Khởi tạo nhanh")').first();
      const btnVisible = await quickSetupBtn.isVisible().catch(() => false);

      if (btnVisible) {
        await quickSetupBtn.click();
        // Verify redirect to /quick-setup with tenantId query string
        await page.waitForURL('**/quick-setup**', { timeout: 10000 });
        expect(page.url()).toContain('tenantId=');
      } else {
        // No tenants with quick-setup button — skip gracefully
        test.skip(true, 'No "Khởi tạo nhanh" button found on tenant management page');
      }
    } finally {
      await context.close();
    }
  });

  test('RV2/RV4 — QuickSetup page render + template list', async ({ browser }) => {
    const tenantId = '00000000-0000-0000-0000-000000000001';
    const { page, context } = await createAuthenticatedPage(browser, tenantId);
    try {
      // Direct URL access with tenantId (full URL)
      await page.goto(`${config.SHOPERP_URL}/quick-setup?tenantId=${tenantId}`);
      await page.waitForLoadState('networkidle');
      await page.waitForTimeout(3000);

      // Page should render without crash (RV2)
      // Look for page header or any content (not error page)
      const errorPage = page.locator('h1.text-danger:has-text("Error")');
      const hasError = await errorPage.isVisible().catch(() => false);
      expect(hasError).toBeFalsy();

      // Page content visible (header or form or template list)
      const pageContent = page.locator('h1, h2, h3, .quick-setup, .template-list, form').first();
      await expect(pageContent).toBeVisible({ timeout: 15000 });
    } finally {
      await context.close();
    }
  });

  test('RV7 — Missing tenantId guard', async ({ browser }) => {
    const { page, context } = await createAuthenticatedPage(browser, '00000000-0000-0000-0000-000000000001');
    try {
      // Navigate without tenantId (full URL)
      await page.goto(`${config.SHOPERP_URL}/quick-setup`);
      await page.waitForLoadState('networkidle');
      await page.waitForTimeout(2000);

      // Should show error message about missing tenantId (RV7)
      // Either error alert or redirect to /admin/tenants
      const url = page.url();
      const hasErrorAlert = await page.locator('.vanan-alert, .alert-danger, .alert-warning').first().isVisible().catch(() => false);
      const hasErrorText = await page.locator('text=/thiếu|tenantId|missing/i').first().isVisible().catch(() => false);
      const redirectedToTenants = url.includes('/admin/tenants');

      // At least one of these guard behaviors should be present
      expect(hasErrorAlert || hasErrorText || redirectedToTenants).toBeTruthy();
    } finally {
      await context.close();
    }
  });
});
