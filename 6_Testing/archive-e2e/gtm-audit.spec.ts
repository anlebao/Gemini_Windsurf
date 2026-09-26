import { test, expect } from '@playwright/test';

/**
 * GTM Drill Machine W1 — Merchant Audit landing (Directory SSR)
 * Task card: docs/AI/tasks/gtm_drill_mvp_task_card.md (Task 1.4, Gate 4)
 *
 * Target: Directory app (timlathay.com in production, configurable via DIRECTORY_URL).
 *
 * Verifies:
 * 1. /kiem-tra-cua-hang renders hero + input, NO report before searching (pattern #157)
 * 2. Auditing an unknown store → "Chưa tìm thấy" not-found state
 * 3. Auditing a known Active tenant (AUDIT_TENANT_NAME env) → presence report renders
 * 4. Pending tenant audit → claim CTA visible
 *
 * Rate limit (growth-audit 10/IP/h) is verified at the Gateway API layer in RV,
 * not here — the Directory SSR forwards user IP via X-Forwarded-For.
 *
 * Run (per .devin/rules/playwright.rules.md — only after build passes + implementation complete):
 *   DIRECTORY_URL=https://timlathay.com AUDIT_TENANT_NAME="<tên tenant Active>" npx playwright test gtm-audit
 */

const DIRECTORY_URL = process.env.DIRECTORY_URL || 'https://timlathay.com';
const AUDIT_ROUTE = `${DIRECTORY_URL}/kiem-tra-cua-hang`;

test.describe('GTM W1 — Merchant Audit landing', () => {
  test('Landing renders hero + input, no report before searching', async ({ page }) => {
    await page.goto(AUDIT_ROUTE, { waitUntil: 'networkidle' });

    // Hero headline (outcome-first copy — "bán kết quả, không bán phần mềm")
    await expect(page.locator('h3:has-text("Khách gần cửa hàng bạn đang tìm gì?")')).toBeVisible();

    // Search input exists
    await expect(page.locator('#audit-input')).toBeVisible();

    // No report rendered before first search (pattern #157 — no initial load)
    await expect(page.locator('h6:has-text("Hiện trạng trên mạng lưới TimLaThay")')).toHaveCount(0);
    await expect(page.locator('text=Chưa tìm thấy')).toHaveCount(0);
  });

  test('Unknown store shows not-found state', async ({ page }) => {
    await page.goto(AUDIT_ROUTE, { waitUntil: 'networkidle' });
    await page.locator('#audit-input').fill('cua-hang-khong-ton-at-xyz-999');
    await page.locator('#audit-input').press('Enter');

    // Not-found message renders (SSR interactive — allow render time)
    await expect(page.locator('text=Chưa tìm thấy')).toBeVisible({ timeout: 15000 });
    // No presence report
    await expect(page.locator('h6:has-text("Hiện trạng trên mạng lưới TimLaThay")')).toHaveCount(0);
  });

  test('Known Active tenant shows presence report', async ({ page }) => {
    const tenantName = process.env.AUDIT_TENANT_NAME;
    test.skip(!tenantName, 'AUDIT_TENANT_NAME not set — requires a known Active tenant in the target environment');

    await page.goto(AUDIT_ROUTE, { waitUntil: 'networkidle' });
    await page.locator('#audit-input').fill(tenantName!);
    await page.locator('#audit-input').press('Enter');

    // Presence report renders with the tenant name
    await expect(page.locator('h6:has-text("Hiện trạng trên mạng lưới TimLaThay")')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('h5').filter({ hasText: tenantName! }).first()).toBeVisible();
  });

  test('Pending tenant shows claim CTA', async ({ page }) => {
    const pendingName = process.env.AUDIT_PENDING_TENANT_NAME;
    test.skip(!pendingName, 'AUDIT_PENDING_TENANT_NAME not set — requires a known Pending (crawled) tenant');

    await page.goto(AUDIT_ROUTE, { waitUntil: 'networkidle' });
    await page.locator('#audit-input').fill(pendingName!);
    await page.locator('#audit-input').press('Enter');

    // Pending checklist line + claim CTA
    await expect(page.locator('text=Đang chờ xác minh chủ sở hữu')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('button:has-text("Nhận sở hữu ngay")')).toBeVisible();
  });
});
