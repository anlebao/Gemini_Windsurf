import { test } from '@playwright/test';

const BASE_URL = 'https://app.khachvip.online';

test('verify-raw — check if prerendering is disabled', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true });
  await context.request.post(`${BASE_URL}/api/platform/login`, {
    data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
    headers: { 'Content-Type': 'application/json' },
  });
  await context.request.post(`${BASE_URL}/api/admin/impersonate/00000000-0000-0000-0000-000000000001`);

  const page = await context.newPage();
  const responsePromise = page.waitForResponse(resp => resp.url().includes('/admin/tenants'));

  await page.goto(`${BASE_URL}/admin/tenants`);
  const response = await responsePromise;
  const rawHtml = await response.text();

  // With prerender:false, there should be NO prerendered buttons
  const rawButtonCount = (rawHtml.match(/vanan-button/g) || []).length;
  const rawOnclickCount = (rawHtml.match(/@onclick/g) || []).length;
  const hasBlazorMarker = rawHtml.includes('<!--Blazor:');
  const markerCount = (rawHtml.match(/<!--Blazor:/g) || []).length;
  const hasPrerenderId = rawHtml.includes('prerenderId');

  console.log('=== RAW HTML (prerender:false check) ===');
  console.log('HTML length:', rawHtml.length);
  console.log('Raw vanan-button count:', rawButtonCount);
  console.log('Raw @onclick count:', rawOnclickCount);
  console.log('Has Blazor marker:', hasBlazorMarker);
  console.log('Marker count:', markerCount);
  console.log('Has prerenderId:', hasPrerenderId);

  // Check if the page has content or is mostly empty (prerender:false = empty until circuit renders)
  const hasMainContent = rawHtml.includes('Quản lý tenant') || rawHtml.includes('Tạo tenant');
  console.log('Has main content (prerendered):', hasMainContent);

  // Now wait for circuit to render
  await page.waitForTimeout(8000);

  // Check DOM after circuit renders
  const domButtonCount = await page.locator('button.vanan-button').count();
  const blazorClickAttrs = await page.locator('[blazor\\:onclick]').count();
  const literalOnclick = await page.evaluate(() => document.querySelectorAll('button[\\@onclick]').length);

  console.log('=== DOM AFTER 8s ===');
  console.log('DOM button count:', domButtonCount);
  console.log('Blazor onclick attrs:', blazorClickAttrs);
  console.log('Literal @onclick attrs:', literalOnclick);

  await context.close();
});
