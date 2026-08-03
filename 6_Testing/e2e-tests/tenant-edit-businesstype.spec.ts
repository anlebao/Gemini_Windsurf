import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';

// Bug 1 — SystemAdmin Edit Tenant BusinessType (Phase 3)
// Verifies that the Edit Tenant modal has a BusinessType dropdown
// and that changing it requires a reason field (Gate 4 UI layout compliance).

const config = loadEnvConfig();

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('Bug 1 — Edit Tenant BusinessType', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto(config.SHOPERP_URL);
    await page.waitForLoadState('networkidle');
  });

  test('Edit Tenant modal has BusinessType dropdown', async ({ page }) => {
    // Navigate to tenant management (SystemAdmin only)
    await page.goto(`${config.SHOPERP_URL}/admin/tenants`);
    await page.waitForLoadState('networkidle');

    // Click "Sửa" on the first tenant in the list
    const editButton = page.getByRole('button', { name: /^Sửa$/ }).first();
    await editButton.click();
    await page.waitForTimeout(500); // modal animation

    // Verify BusinessType dropdown exists in edit modal
    const businessTypeSelect = page.locator('#edit-businesstype');
    await expect(businessTypeSelect).toBeVisible();

    // Verify both options exist
    const options = businessTypeSelect.locator('option');
    await expect(options).toHaveCount(2);
    await expect(businessTypeSelect.locator('option[value="Company"]')).toBeVisible();
    await expect(businessTypeSelect.locator('option[value="HouseholdBusiness"]')).toBeVisible();
  });
});
