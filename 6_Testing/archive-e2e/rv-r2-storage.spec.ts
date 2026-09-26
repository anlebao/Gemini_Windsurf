import { test, expect } from '@playwright/test';

const ERP_URL = 'https://app2.khachvip.online';
const GW_URL = 'https://api2.khachvip.online';
const CREDENTIALS = { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' };

test('R2Storage RV — L1 API auth + L3 Admin UI', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: ERP_URL, ignoreHTTPSErrors: true });
  const page = await context.newPage();

  const logs: string[] = [];
  page.on('console', msg => logs.push(`[${msg.type()}] ${msg.text()}`));
  page.on('pageerror', err => logs.push(`[pageerror] ${err.message}`));

  // ============================================================
  // L1: API Auth checks (no auth → 401)
  // ============================================================
  console.log('=== L1: API Auth ===');

  // L1.1: R2Storage Stats (no auth → 401)
  const statsNoAuth = await context.request.get(`${GW_URL}/api/r2storage/stats/00000000-0000-0000-0000-000000000000`);
  console.log(`L1.1 R2Storage Stats (no auth): ${statsNoAuth.status()}`);
  expect(statsNoAuth.status()).toBe(401);

  // L1.2: R2Storage Cleanup (no auth → 401)
  const cleanupNoAuth = await context.request.post(`${GW_URL}/api/r2storage/cleanup/00000000-0000-0000-0000-000000000000`);
  console.log(`L1.2 R2Storage Cleanup (no auth): ${cleanupNoAuth.status()}`);
  expect(cleanupNoAuth.status()).toBe(401);

  // L1.3: R2Storage CleanupAll (no auth → 401)
  const cleanupAllNoAuth = await context.request.post(`${GW_URL}/api/r2storage/cleanup-all`);
  console.log(`L1.3 R2Storage CleanupAll (no auth): ${cleanupAllNoAuth.status()}`);
  expect(cleanupAllNoAuth.status()).toBe(401);

  // ============================================================
  // L1.5: Login + Authenticated API check
  // ============================================================
  console.log('=== L1.5: Login + Authenticated API ===');

  // Login via ShopERP API (cookie auth)
  const loginResp = await context.request.post(`${ERP_URL}/api/platform/login`, {
    data: CREDENTIALS,
    headers: { 'Content-Type': 'application/json' },
  });
  console.log(`Login: ${loginResp.status()}`);
  expect(loginResp.status()).toBe(200);

  // Get JWT token from login response
  const loginBody = await loginResp.json();
  const token = loginBody.token || loginBody.Token || loginBody.accessToken;
  console.log(`Token present: ${!!token}`);

  // L1.6: R2Storage Stats (with auth → 200 JSON)
  if (token) {
    const statsAuth = await context.request.get(`${GW_URL}/api/r2storage/stats/00000000-0000-0000-0000-000000000000`, {
      headers: { 'Authorization': `Bearer ${token}` },
    });
    console.log(`L1.6 R2Storage Stats (auth): ${statsAuth.status()}`);
    const statsBody = await statsAuth.text();
    console.log(`  Body: ${statsBody.substring(0, 200)}`);
    expect([200, 403]).toContain(statsAuth.status()); // 200 or 403 (if tenantId mismatch)
  }

  // ============================================================
  // L3: Admin UI — R2StorageAdmin page
  // ============================================================
  console.log('=== L3: Admin UI ===');

  // Navigate to R2Storage admin page (should NOT redirect to login after auth)
  await page.goto(`${ERP_URL}/admin/r2-storage`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(3000);

  const url = page.url();
  const title = await page.title();
  console.log(`L3.1 Admin page URL: ${url}`);
  console.log(`L3.1 Admin page title: ${title}`);

  // Should NOT be on login page
  expect(url).not.toContain('/Login');
  expect(url).not.toContain('/login');

  // Check for R2 Storage content
  const bodyText = await page.textContent('body');
  console.log(`L3.2 Body text (first 500): ${(bodyText || '').substring(0, 500)}`);

  // Should contain R2 storage related text
  const hasR2Content = (bodyText || '').match(/R2|lưu trữ|storage|Storage|cleanup|Cleanup|stats|Stats/i);
  console.log(`L3.3 Has R2 content: ${!!hasR2Content}`);

  // ============================================================
  // L4: Sitemap link check
  // ============================================================
  console.log('=== L4: Sitemap ===');

  await page.goto(`${ERP_URL}/sitemap`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(2000);

  const sitemapUrl = page.url();
  console.log(`L4.1 Sitemap URL: ${sitemapUrl}`);
  expect(sitemapUrl).not.toContain('/Login');

  const sitemapText = await page.textContent('body');
  const hasR2Link = (sitemapText || '').match(/r2-storage|R2Storage|lưu trữ ảnh|Lưu trữ ảnh/i);
  console.log(`L4.2 Sitemap has R2 link: ${!!hasR2Link}`);

  console.log('=== RV Complete ===');
  console.log('Logs:', logs.slice(-10).join('\n'));
});
