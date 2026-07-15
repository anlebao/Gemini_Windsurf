import { test } from '@playwright/test';

const BASE_URL = 'https://app.khachvip.online';

test('diag-markers — check Blazor SSR markers', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true });
  await context.request.post(`${BASE_URL}/api/platform/login`, {
    data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
    headers: { 'Content-Type': 'application/json' },
  });
  await context.request.post(`${BASE_URL}/api/admin/impersonate/00000000-0000-0000-0000-000000000001`);

  const page = await context.newPage();
  await page.goto(`${BASE_URL}/admin/tenants`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(3000);

  // Get full HTML
  const html = await page.content();

  // Check for Blazor markers
  const hasBlazorMarker = html.includes('<!--Blazor:');
  const hasBlazorServerType = html.includes('"type":"server"');
  const hasPrerenderId = html.includes('prerenderId');
  const hasComponentMarker = html.includes('<!--Blazor:');
  const markerCount = (html.match(/<!--Blazor:/g) || []).length;

  // Check for specific patterns
  const hasRenderMode = html.includes('render-mode');
  const hasInteractiveServer = html.includes('InteractiveServer') || html.includes('interactive-server');

  // Check the Routes component area
  const routesMatch = html.match(/<main[\s\S]*?<\/main>/);
  const routesHtml = routesMatch ? routesMatch[0].substring(0, 500) : 'N/A';

  // Check script tags
  const scripts = await page.locator('script').evaluateAll(els => els.map(e => e.getAttribute('src') || e.textContent?.substring(0, 100)));

  console.log('Has Blazor marker:', hasBlazorMarker);
  console.log('Marker count:', markerCount);
  console.log('Has server type:', hasBlazorServerType);
  console.log('Has prerenderId:', hasPrerenderId);
  console.log('Has render-mode attr:', hasRenderMode);
  console.log('Has InteractiveServer:', hasInteractiveServer);
  console.log('Routes HTML (first 500):', routesHtml);
  console.log('Scripts:', JSON.stringify(scripts.filter(s => s && s.length < 100)));

  // Check page source for @onclick patterns
  const literalOnclickCount = (html.match(/@onclick/g) || []).length;
  const blazorOnclickCount = (html.match(/blazor:onclick/g) || []).length;
  console.log('Literal @onclick count:', literalOnclickCount);
  console.log('blazor:onclick count:', blazorOnclickCount);

  await context.close();
});
