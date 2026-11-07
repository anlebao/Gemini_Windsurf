import { test, expect, Page } from '@playwright/test';

// Task AI-P10 — admin UI /admin/tenant-ai/* (SRS §3.1-P2): hub status · upload doc →
// pending → approve · profile PUT → publish. Flag-gated (TenantAiEnabled) — auto-skip
// khi flag OFF hoặc vanan-ai unreachable (hub báo "không kết nối được dịch vụ AI").
// Auth: global storageState (auth/admin.json) via playwright.config.ts.

async function isAiEnabled(page: Page): Promise<boolean> {
  await page.goto('/admin/tenant-ai');
  await page.waitForLoadState('networkidle');
  // flag OFF → empty state hướng dẫn bật; service down → cảnh báo riêng.
  if (await page.getByTestId('tenant-ai-disabled').isVisible().catch(() => false)) return false;
  if (await page.getByTestId('alert-service-down').isVisible().catch(() => false)) return false;
  return true;
}

test.describe('Tenant AI admin — flag-independent', () => {
  test('NavMenu có mục Trợ lý AI (Tenant AI)', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await expect(
      page.getByRole('link', { name: /Trợ lý AI/ }).first()
    ).toBeVisible({ timeout: 15000 });
  });

  test('Sitemap có link-tenant-ai', async ({ page }) => {
    await page.goto('/sitemap');
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('link-tenant-ai')).toBeVisible({ timeout: 15000 });
  });

  test('route /admin/tenant-ai render (hub hoặc empty state)', async ({ page }) => {
    await page.goto('/admin/tenant-ai');
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('tenant-ai-hub')).toBeVisible({ timeout: 15000 });
  });
});

test.describe('Tenant AI admin — flag-gated (TenantAiEnabled + vanan-ai)', () => {
  test('hub hiển thị status cards + quick links', async ({ page }) => {
    test.skip(!(await isAiEnabled(page)), 'TenantAiEnabled OFF hoặc vanan-ai unreachable');

    await expect(page.getByTestId('card-ai-products')).toBeVisible();
    await expect(page.getByTestId('card-ai-docs')).toBeVisible();
    await expect(page.getByTestId('card-ai-profile')).toBeVisible();
    await expect(page.getByTestId('card-ai-usage')).toBeVisible();
    await expect(page.getByTestId('link-ai-profile')).toBeVisible();
    await expect(page.getByTestId('link-ai-knowledge')).toBeVisible();
    await expect(page.getByTestId('link-ai-policy')).toBeVisible();
  });

  test('upload doc → xuất hiện ở tab Chờ duyệt → duyệt thành công', async ({ page }) => {
    test.skip(!(await isAiEnabled(page)), 'TenantAiEnabled OFF hoặc vanan-ai unreachable');

    await page.goto('/admin/tenant-ai/knowledge');
    await page.waitForLoadState('networkidle');
    await page.getByTestId('tab-docs').click();
    await expect(page.getByTestId('card-upload')).toBeVisible();

    const title = `E2E FAQ ${Date.now()}`;
    await page.getByTestId('select-doc-type').selectOption('faq');
    await page.getByTestId('input-doc-title').fill(title);
    await page.getByTestId('input-doc-content').fill('E2E: quán mở cửa 9h-21h mỗi ngày.');
    // @bind = onchange: blur textarea để commit giá trị → nút upload enable (Playwright
    // không tự blur trước click → deadlock trên nút disabled-until-valid).
    await page.getByTestId('input-doc-content').press('Tab');
    await page.getByTestId('btn-upload-doc').click();

    // doc lands in pending queue (approval gate [G14]) — upload xong page vẫn ở
    // tab "docs", phải switch sang "Chờ duyệt" mới thấy pending-doc.
    await page.getByTestId('tab-pending').click();
    const pendingCard = page.getByTestId('pending-doc').filter({ hasText: title });
    await expect(pendingCard).toBeVisible({ timeout: 15000 });
    await pendingCard.getByTestId('btn-approve-doc').click();
    await expect(pendingCard).toHaveCount(0, { timeout: 15000 });

    // cleanup: xóa doc test đã duyệt để không để dữ liệu E2E trên production.
    await page.getByTestId('tab-docs').click();
    await page.getByTestId('select-status-filter').selectOption('approved');
    const docItem = page.getByTestId('doc-item').filter({ hasText: title });
    await expect(docItem).toBeVisible({ timeout: 15000 });
    await docItem.getByTestId('btn-delete-doc').click();
    await docItem.getByTestId('btn-confirm-delete').click();
    await expect(docItem).toHaveCount(0, { timeout: 15000 });
  });

  test('profile: lưu draft → preview → publish', async ({ page }) => {
    test.skip(!(await isAiEnabled(page)), 'TenantAiEnabled OFF hoặc vanan-ai unreachable');

    await page.goto('/admin/tenant-ai/profile');
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('card-profile-form')).toBeVisible({ timeout: 15000 });

    // nhớ bản published gốc để rollback về sau (spec không được đổi config production).
    const origPub = (await page.getByTestId('published-summary').textContent()) || '';
    const origVer = origPub.match(/v(\d+)/)?.[1] ?? null;

    const greeting = `Chào E2E ${Date.now() % 100000}`;
    await page.getByTestId('input-ai-name').fill('Bé E2E');
    await page.getByTestId('input-greeting').fill(greeting);
    await page.getByTestId('btn-save-draft').click();
    await expect(page.getByTestId('draft-summary')).toBeVisible({ timeout: 15000 });

    // preview-chat dùng draft config (LLM — cho phép skip khi backend tắt LLM)
    await page.getByTestId('input-preview').fill('xin chào');
    await page.getByTestId('btn-preview-send').click();
    await expect(page.getByTestId('preview-thread')).toContainText(/.+/, { timeout: 45000 });

    await page.getByTestId('btn-publish').click();
    await page.getByTestId('btn-confirm-publish').click();
    await expect(page.getByTestId('published-summary')).toContainText('Bé E2E', { timeout: 15000 });

    // rollback về bản published gốc — xác nhận rollback hoạt động + giữ config thật.
    if (origVer) {
      await page.getByTestId('select-rollback-version').selectOption(origVer);
      await page.getByTestId('btn-rollback').click();
      await page.getByTestId('btn-confirm-rollback').click();
      await expect(page.getByTestId('published-summary'))
        .not.toContainText('Bé E2E', { timeout: 15000 });
    }
  });
});
